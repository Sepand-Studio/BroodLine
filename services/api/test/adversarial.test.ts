import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import type { Deps } from '../src/app.ts'
import { type Bundle, clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { REPLAY_CAP_PER_DAY } from '../src/wave/issuance.ts'
import { rewardForWave } from '../src/wave/rewards.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  balance, buildLosingReplay, buildWinningReplay, clearThrough, liveIssuance,
  setupPlayer, startWave, submit, withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * DESIGN §7, AND THE PHASE GATE. One test per row of design §4.4's table.
 *
 * What makes this file the gate is not that it passes - it is that every
 * assertion in it has been WATCHED TO FAIL against a deliberately weakened
 * guard. See `weakenings.md` alongside this file for the seven weakenings,
 * each with the named test that reddened under it. Phase 4's recorded
 * lesson, twice over, and Phase 5's six times over, is that a suite can be
 * green while proving nothing; a test nobody has seen fail is a test nobody
 * has shown to test anything.
 *
 * A REAL `sim` child process, never a stub - the api/sim boundary is the
 * exact thing this phase exists to make authoritative, and a stub standing
 * in for it would be a second implementation of the boundary under test.
 */

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, and .pathname percent-encodes it (wave-start.test.ts
// and wave-submit.test.ts carry the same note; all three resolve the same
// repo root).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')
const SERVER_ID = 1
// Distinct from generate-contract.sh's 5199, wave-submit.test.ts's 5299 and
// replays.test.ts's 5399 - file parallelism is ON (and must stay on), so
// every sim-hosting file needs its own port.
const SIM_PORT = 5499
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

/** config/bundles/0.1.1/waves.json - wave 6 is the only authored wave. */
const WAVE_6_REWARD = 40

/**
 * Starts the REAL sim service as a child process, once for this file.
 * Build-then-run rather than `dotnet run`, and under the shared
 * dotnet-build lock - see wave-submit.test.ts's copy of this function and
 * wave-helpers.ts's comment above `withDotnetBuildLock` for why both.
 */
async function startSim(): Promise<{ proc: ChildProcess; stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-sim-build-'))
  await withDotnetBuildLock(() => execFileSync('dotnet', [
    'build', 'services/sim/Broodline.Sim.Service.csproj', '-c', 'Debug', '-o', work, '--nologo',
  ], { cwd: REPO, stdio: 'inherit' }))

  const proc = spawn('dotnet', [join(work, 'Broodline.Sim.Service.dll'), '--urls', SIM_URL], {
    cwd: REPO,
    stdio: 'ignore',
  })

  let ready = false
  for (let i = 0; i < 80; i++) {
    try {
      const res = await fetch(`${SIM_URL}/healthz`)
      if (res.ok) { ready = true; break }
    } catch {
      // not up yet
    }
    await new Promise((r) => setTimeout(r, 250))
  }
  if (!ready) {
    proc.kill()
    await rm(work, { recursive: true, force: true })
    throw new Error(`sim host never became ready on ${SIM_URL}`)
  }

  return {
    proc,
    stop: async () => {
      proc.kill()
      await rm(work, { recursive: true, force: true })
    },
  }
}

let t: TestDb
let deps: Deps
let bundleRoot: string
let sim: { proc: ChildProcess; stop: () => Promise<void> }

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])

  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-adversarial-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  deps = { db: t.db, bundleStore: store, simClient: new SimClient(SIM_URL), replayStore: new LocalReplayStore(bundleRoot) }
}, 240_000)

/**
 * A FRESH PLAYER PER TEST, and this is load-bearing rather than tidiness.
 *
 * DEVIATION FROM THE BRIEF, reported rather than worked around silently:
 * the brief's snippet shares one player across all eight cases, and that
 * suite cannot pass. Tests 1-3 leave three CONSUMED wave-6 issuances behind
 * (a loss, a rejection and a win all settle 'consumed'), and test 3's win
 * advances campaign progress to 6 - so test 4's `startWave(6)` takes design
 * §4.1's REPLAY branch, finds the day's count already at
 * REPLAY_CAP_PER_DAY, and answers 429 with no issuance id at all. Test 4
 * would then submit `undefined` and fail on a 400, never reaching the guard
 * it names. wave-submit.test.ts re-seeds per test for the same reason; this
 * hoists that into a hook instead of repeating the call eight times.
 */
beforeEach(async () => {
  await setupPlayer(deps)
})

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

describe('adversarial: what a modified client cannot do', () => {
  it('cannot claim a win that did not happen', async () => {
    // The client claims Win; sim says Loss. api pays what sim returns and
    // never what the client claims - which is why the submission body
    // carries only the replay and an issuance id, with no outcome field
    // for a client to lie in.
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'adv-1')

    expect((await res.json() as { result: string }).result).toBe('Loss')
    expect(await balance('shards')).toBe(before)
  })

  it('cannot submit forged bytes', async () => {
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, 'bm90IGEgcmVwbGF5', 'adv-2')
    expect(res.status).toBe(409)
    // Assert the CODE, not only the status. 409 alone is also what
    // issuance_invalid and wave_locked return, so a status-only assertion
    // would stay green if `sim`'s rejection stopped reaching this handler
    // as a rejection at all - which is exactly weakening 8.
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await balance('shards')).toBe(before)
  })

  it('cannot replay a winning submission twice', async () => {
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    expect((await submit(issuanceId, replay, 'adv-3a')).status).toBe(200)
    const before = await balance('shards')

    // A DIFFERENT idempotency key, deliberately: §6.3's key is
    // client-supplied and a modified client simply mints a new one, so the
    // idempotency layer is not the guard under test here. The issuance is.
    const second = await submit(issuanceId, replay, 'adv-3b')
    expect(second.status).toBe(409)
    expect(await second.json()).toMatchObject({ code: 'issuance_invalid' })
    expect(await balance('shards')).toBe(before)
  })

  it('cannot replay a winning submission twice under a genuinely concurrent second attempt', async () => {
    // WRITTEN BECAUSE THREE WEAKENINGS LEFT THE TEST ABOVE GREEN
    // (weakenings.md rows 2, 3 and 4). The sequential double-submit above
    // proves the issuance is consumed; it cannot prove WHERE the
    // consumption happens relative to the credit, because by the time its
    // second request starts, the first has finished entirely - the guard
    // could live anywhere in the handler and that test would not know.
    //
    // This forces the race deterministically rather than hoping timing
    // cooperates (the critical section is ~1-2ms against a local Postgres,
    // smaller than ordinary connection-acquisition jitter - wave-submit.
    // test.ts's barrier-based attempt is documented there as NOT a reliable
    // repro for exactly that reason). T_ext is a separate raw connection
    // running the same settling UPDATE inside a transaction it does not
    // commit, so the handler's own settle() blocks on a REAL Postgres row
    // lock, at the exact instant the guard is supposed to be looking.
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    const client = await t.pool.connect()
    try {
      await client.query('BEGIN')
      await client.query(`SELECT set_config('app.server_id', $1, true)`, [String(SERVER_ID)])
      const locked = await client.query(
        `UPDATE wave_issuances SET settled_at = now(), settlement = 'consumed'
         WHERE server_id = $1 AND issuance_id = $2 AND settled_at IS NULL`,
        [SERVER_ID, issuanceId],
      )
      // Sanity: T_ext genuinely took the row. If this is not 1, everything
      // below would pass vacuously.
      expect(locked.rowCount).toBe(1)

      const pending = submit(issuanceId, replay, 'adv-3c')

      // Wait for a REAL synchronization point - the handler's backend
      // actually blocked - not a sleep. Bounded, and the absence of a
      // waiter is itself part of what this test catches (a handler that
      // settles outside the credit's transaction never contends here at
      // all), so the deadline falls through rather than hanging.
      const deadlineAt = Date.now() + 3_000
      for (;;) {
        const [row] = (await t.ownerDb.execute(sql`
          SELECT count(*)::int AS n FROM pg_stat_activity
          WHERE wait_event_type = 'Lock' AND query ILIKE '%wave_issuances%'
            AND pid <> pg_backend_pid()`)).rows as { n: number }[]
        if ((row?.n ?? 0) > 0) break
        if (Date.now() >= deadlineAt) break
        await new Promise((r) => setTimeout(r, 25))
      }

      await client.query('COMMIT')
      const res = await pending

      // T_ext's commit makes settled_at non-NULL; the handler's blocked
      // UPDATE re-evaluates its WHERE under READ COMMITTED, matches
      // nothing, settle() returns false, and the gate refuses BEFORE
      // advanceCampaign and credit ever run.
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'issuance_invalid' })
    } finally {
      client.release()
    }

    expect(await balance('shards')).toBe(before)
  })

  it('cannot submit against a self-chosen seed', async () => {
    // 0x1111n is a seed the server never issued. The replay is internally
    // HONEST - it really is a winning wave 6 played at that seed - so sim
    // verifies it happily and the refusal must come from matchesIssuance
    // comparing echo.seed to the issuance's, NOT from sim rejecting junk.
    //
    // ASSERT THE REASON, NOT ONLY THE BALANCE. An unchanged balance is
    // satisfied by a submission refused for ANY reason - a malformed body, a
    // 500, a seed the helper failed to encode. This phase has already shipped
    // six assertions that were green while proving nothing; this is exactly
    // that shape. The status and code pin WHICH guard fired.
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildWinningReplay(6, 0x1111n), 'adv-4')
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await balance('shards')).toBe(before)
  })

  it('cannot skip to wave 60', async () => {
    const res = await startWave(60)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
    // No issuance was minted for the skipped wave - the refusal is a
    // refusal, not a seed handed out with an error attached.
    expect(await liveIssuance()).toBeUndefined()
  })

  it('cannot seed-shop for a favourable run', async () => {
    // DESIGN §4.4's NINTH ROW, which the brief's eight cases silently omit -
    // reported rather than left as a hole a reader would have to notice for
    // themselves. §4.4's table has nine rows; the brief specifies eight
    // tests, and this is the one with no entry.
    //
    // WHAT THIS TEST CAN AND CANNOT BE SHOWN TO DO, stated plainly because
    // an unfalsifiable test in THIS file would be self-defeating. Its guard
    // is the partial unique index wave_issuances_one_live, and that index
    // cannot be weakened in isolation: claimIssuance's `ON CONFLICT ... DO
    // NOTHING` infers against it by target list AND predicate, so removing
    // it makes every wave/start raise 42P10 and 500 - every test in this
    // file reddens together, which proves liveness rather than this
    // property (weakenings.md row 2 records the observed run). Nor does
    // deleting issueWave's check 4 redden it: the insert then simply
    // conflicts and claimIssuance returns the existing row, which is the
    // index doing the work check 4 only optimises. The nearest thing to a
    // discriminating gate is wave-start.test.ts:203, which drives
    // claimIssuance directly against a real conflict.
    const firstRes = await startWave(6)
    const secondRes = await startWave(6)
    // ASSERT BOTH SUCCEEDED FIRST. Found by running weakening 2 against an
    // earlier draft of this very test: when both calls 500, both bodies are
    // error envelopes, both `issuanceId`s read `undefined`, and
    // `undefined === undefined` passed - a test green on two failed
    // requests. That is the exact vacuity shape this file exists to hunt,
    // and it was in the file's own newest test.
    expect(firstRes.status).toBe(200)
    expect(secondRes.status).toBe(200)
    const first = await firstRes.json() as { issuanceId: string; seed: string }
    const second = await secondRes.json() as { issuanceId: string; seed: string }
    expect(typeof first.issuanceId).toBe('string')
    expect(typeof first.seed).toBe('string')

    // design 2.1. Determinism is what makes verification cheap; it is also
    // what makes seed-shopping cheap, and one live issuance is the defence.
    expect(second.issuanceId).toBe(first.issuanceId)
    expect(second.seed).toBe(first.seed)
  })

  it('cannot inflate the reward by echoing a different wave', async () => {
    // THE test Task 6 Step 6(c) found missing. Every honest submission
    // agrees on both wave ids, so nothing in wave-submit.test.ts
    // distinguishes issuance.waveId from verdict.echo.waveId - and the
    // reward must come from the first.
    //
    // WHAT THIS DOES AND DOES NOT PROVE, stated here rather than only in
    // the report: with exactly one authored wave (WaveDef.ForId returns
    // wave 6 and throws for every other id, which sim maps to
    // rules_violated) there is no second reward to inflate TO, so this
    // asserts the paid amount is wave 6's bundle reward and nothing else.
    // The end-to-end inflation proof is owed against the engine content
    // fill - see weakenings.md row 5.
    await clearThrough(6)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'adv-6')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: WAVE_6_REWARD } })
    expect(await balance('shards')).toBe(before + WAVE_6_REWARD)
  })

  it('cannot farm a cleared wave past the daily cap', async () => {
    await clearThrough(6)
    for (let i = 0; i < REPLAY_CAP_PER_DAY; i++) {
      const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
      expect((await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), `adv-7-${i}`)).status).toBe(200)
    }
    const before = await balance('shards')

    const res = await startWave(6)
    expect(res.status).toBe(429)
    expect(await res.json()).toMatchObject({ code: 'replay_cap_reached' })
    expect(await balance('shards')).toBe(before)
  })

  it('still counts a consumed issuance from earlier today against the cap', async () => {
    // WRITTEN BECAUSE WEAKENING 7 LEFT THE CAP TEST GREEN. Design §4.3
    // splits retention precisely so the replay count can still see a
    // consumed row from earlier in the UTC day: 'expired' rows age out
    // three hours after issuance, 'consumed' rows survive 48 hours BECAUSE
    // THEY ARE THE COUNTER. Every row the cap test creates is seconds old,
    // so a count that only looked back three hours would satisfy it
    // identically - and nothing else in the phase reads the split.
    //
    // Back-dated within the current UTC day, never blindly by N hours: at
    // 02:00 UTC a three-hour back-date lands in YESTERDAY, where the row
    // correctly stops counting and this test would fail for a reason that
    // has nothing to do with its subject. `greatest(day start, now - 3h)`
    // is the oldest instant that is both still today and old enough for a
    // three-hour window to have dropped it; when the day is younger than
    // three hours the row simply stays where it is and the test degrades to
    // a weaker but still-correct assertion rather than a false alarm.
    await clearThrough(6)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    expect((await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'adv-7c')).status).toBe(200)

    await t.ownerDb.execute(sql`
      UPDATE wave_issuances
      SET issued_at = greatest(
        date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC',
        now() - interval '3 hours')
      WHERE issuance_id = ${issuanceId}`)

    // Two more, and then the cap must bind - which it only can if the
    // back-dated row is still being counted.
    for (let i = 0; i < REPLAY_CAP_PER_DAY - 1; i++) {
      const started = await (await startWave(6)).json() as { issuanceId: string; seed: string }
      expect((await submit(started.issuanceId, buildWinningReplay(6, BigInt(started.seed)), `adv-7d-${i}`)).status).toBe(200)
    }

    expect((await startWave(6)).status).toBe(429)
  })

  it('cannot spend a replay it never took by abandoning a wave', async () => {
    // WRITTEN BECAUSE WEAKENING 4b LEFT THE CAP TEST ABOVE GREEN. That
    // test consumes every issuance it starts, so wave/start's abandoned-
    // wave path (issueWave check 4: settle the expired live row 'expired',
    // then insert) is never reached and the settlement it writes is never
    // observed. Settling it 'consumed' instead would silently charge a
    // replay the player never took - design §4.3 says so in prose, and
    // until now nothing failed when it stopped being true.
    //
    // The row is aged by hand rather than by waiting out ISSUANCE_TTL_MS -
    // two hours is not a thing a test can wait for, and the TTL itself is
    // not what is under test here.
    await clearThrough(6)
    const first = await (await startWave(6)).json() as { issuanceId: string }

    // Abandon it: expired, still unsettled, exactly what backgrounding the
    // app mid-wave leaves behind.
    await t.ownerDb.update(waveIssuances)
      .set({ expiresAt: new Date(Date.now() - 60_000) })
      .where(sql`${waveIssuances.issuanceId} = ${first.issuanceId}`)

    // Now take all three replays properly. Under the shipped code the
    // abandoned row settled 'expired' and counts for nothing, so all three
    // succeed. Under 4b it settled 'consumed', the third start hits the cap
    // and this loop fails on a 429 for a wave the player never played.
    for (let i = 0; i < REPLAY_CAP_PER_DAY; i++) {
      const res = await startWave(6)
      expect(res.status).toBe(200)
      const { issuanceId, seed } = await res.json() as { issuanceId: string; seed: string }
      expect((await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), `adv-7b-${i}`)).status).toBe(200)
    }

    // And the abandoned row is settled 'expired', named directly so a
    // future change cannot satisfy the loop above by some other route.
    const [abandoned] = await t.ownerDb.select().from(waveIssuances)
      .where(sql`${waveIssuances.issuanceId} = ${first.issuanceId}`)
    expect(abandoned?.settlement).toBe('expired')
  })

  it('CAN still deploy creatures the player does not own — Phase 6', async () => {
    // design 2.2 and 4.4's last row. NOT a defence: a marker, asserted so
    // that the day a creature table exists this test fails and names the
    // thing that changed. A hole recorded as a passing assertion about the
    // current behaviour is a hole nobody re-reads.
    //
    // { trait: 'Chill', tier: 3 } is a LEGAL winning deployment
    // (Deployments.MaxCoverageTier is 3 and Stats.ChillCapacity(3) is 4),
    // so a 200 here means the unowned deployment was ACCEPTED - not that
    // the replay was waved through as rules_violated. The reward assertion
    // is what pins that distinction: a rejected replay pays nothing.
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed), { trait: 'Chill', tier: 3 }), 'adv-8')

    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { amount: WAVE_6_REWARD } })
    // When this flips to 409, Phase 6 has landed the roster check. Update
    // design 4.4's table in the same change.
  })
})

/**
 * WEAKENING ROW 5's STAND-IN, AND IT IS A STAND-IN, NOT A CLOSURE.
 *
 * design §4.4's "inflate the reward" row names `verdict.echo.waveId` as the
 * source a modified client would want the reward read from. That weakening
 * CANNOT be exercised end to end in this phase, and this block is what
 * stands in its place. The reasoning was re-derived against the code rather
 * than taken on trust:
 *
 * 1. `matchesIssuance` rejects an echo/issuance wave-id mismatch at
 *    routes/wave.ts:223, BEFORE the reward is computed at :257. By the time
 *    the lookup runs, `echo.waveId === issuance.waveId` is guaranteed, so
 *    reading either is behaviourally identical - the weakening is a no-op.
 * 2. Deleting step 5 as well would let wave B's reward be paid for a wave A
 *    issuance - but only if two waves with DIFFERENT rewards exist.
 * 3. The engine authors exactly one. `WaveDef.ForId` returns wave 6 and
 *    throws `WaveCompositionException` for every other id, which
 *    `SimulateEndpoint.Handle` maps to `rejected: rules_violated`, so a
 *    wave-7 replay never reaches the handler at all. A test-only BUNDLE
 *    fixture does not help: the gate is the engine, and no task in this
 *    phase modifies `engine/`.
 *
 * So what follows proves the WIRING - that the reward is a function of the
 * wave id it is handed, so handing it the issuance's rather than the echo's
 * is a decision that has consequences - and NOT that an end-to-end inflation
 * is impossible. That proof is OWED against the engine content fill, when a
 * second authored wave exists. It is recorded as owed in weakenings.md and
 * in this task's report; do not read this block as closing the row.
 */
describe("the reward's source of truth (design §2.2)", () => {
  // Two waves with DIFFERENT rewards - the shape the engine cannot yet
  // produce, constructed here so the lookup has something to discriminate.
  const twoWaves: Bundle = {
    version: '0.1.1-test',
    minimumClientVersion: '0.1.0',
    starterGrants: [],
    waves: [
      { id: 6, integrity: 10, laneCount: 1, reward: { currency: 'shards', amount: 40 }, spawns: [] },
      { id: 7, integrity: 10, laneCount: 1, reward: { currency: 'shards', amount: 9_999 }, spawns: [] },
    ],
  }

  it('pays what the wave id it is given pays, and nothing else', () => {
    // If these two were not different, "read the reward from the echo"
    // could not be a weakening at all, and the handler's choice of source
    // would carry no information.
    expect(rewardForWave(twoWaves, 6)).toEqual({ currency: 'shards', amount: 40 })
    expect(rewardForWave(twoWaves, 7)).toEqual({ currency: 'shards', amount: 9_999 })
  })

  it('refuses a wave the live bundle no longer carries rather than defaulting', () => {
    // routes/wave.ts turns this null into wave_locked BEFORE advancing
    // campaign progress - a bundle republished during the issuance's
    // two-hour TTL must not be able to silently pay zero, or to record a
    // clear under an error response.
    expect(rewardForWave(twoWaves, 60)).toBeNull()
  })
})
