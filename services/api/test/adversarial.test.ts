import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import type { Deps } from '../src/app.ts'
import { type Bundle, clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import {
  type Issuance, type IssuanceRefusal, issueWave, REPLAY_CAP_PER_DAY,
} from '../src/wave/issuance.ts'
import { rewardForWave } from '../src/wave/rewards.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  balance, buildLosingReplay, buildWinningReplay, clearThrough, liveIssuance,
  setupPlayer, startLosing, startWave, startWinning, submit, withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * DESIGN §7, AND THE PHASE GATE. One test per row of design §4.4's table.
 *
 * What makes this file the gate is not that it passes - it is that its
 * guards have been WATCHED TO FAIL when deliberately weakened. See
 * `weakenings.md` alongside this file for all TEN weakenings applied
 * (rows 1, 2, 3, 4, 4b, 6, 7, 8, 9, 10 - row 5 is struck and says why in
 * its own section) and what each one actually did. SEVEN reddened a named
 * test here (1, 3, 4b, 6, 7, 8, 9); row 2 reddens the file wholesale
 * rather than discriminatingly; and TWO - rows 4 and 10 - reddened nothing
 * in this file at all, both recorded as findings rather than smoothed over.
 *
 * ROW 10 IS A RECORDED HOLE IN THIS GATE, AND IT IS LEFT OPEN DELIBERATELY.
 * Making submit's step-2 liveness read refuse DIRECTLY, instead of only
 * gating the `sim` call, leaves this file 15/15 GREEN while answering a
 * retrying client 409 for a wave it was in fact paid for - the phase's
 * central claim ("pays exactly once under retry") failing in the direction
 * a player notices. What catches it is `returns the stored response on a
 * resend with the SAME key`, in `wave-submit.test.ts`, not anything here.
 * That split is a controller ruling, not an oversight: closing it here
 * would put a non-adversarial property in the adversarial suite. So do NOT
 * read this file's 15/15 as covering it. weakenings.md row 10 carries the
 * reasoning; that test carries a do-not-delete note pointing back.
 *
 * Phase 4's recorded lesson, twice over, and Phase 5's NINE times over, is
 * that a suite can be green while proving nothing; a test nobody has seen
 * fail is a test nobody has shown to test anything. One of those nine was
 * caught in THIS file: under weakening 2 both `wave/start` calls 500, both
 * `issuanceId`s read `undefined`, and `undefined === undefined` was a green
 * assertion - see weakenings.md row 2.
 *
 * A REAL `sim` child process, never a stub - the api/sim boundary is the
 * exact thing this phase exists to make authoritative, and a stub standing
 * in for it would be a second implementation of the boundary under test.
 *
 * PHASE 6 ADDED TWO TESTS AND FLIPPED ONE, so this file ships SIXTEEN. Every
 * "15/15" above is a MEASUREMENT taken at the time, against the fifteen that
 * existed then, and it is left as measured rather than restated at the new
 * count - rewriting a number nobody re-ran would be the same defect this file
 * exists to hunt. The ten rows above are likewise Phase 5's; Phase 6's nine
 * are in `weakenings.md`'s own Phase 6 section, and the two tests they cover
 * here are `CANNOT deploy creatures the player does not own` (the flipped
 * marker) and `cannot submit a replay claiming a deployment it was not
 * issued`.
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
  // Reaped if this worker is signalled rather than torn down cleanly, so an
  // interrupted run does not orphan this host on 5499 - see child-reaper.ts.
  const unreap = reapOnExit(proc)

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
    unreap()
    await rm(work, { recursive: true, force: true })
    throw new Error(`sim host never became ready on ${SIM_URL}`)
  }

  return {
    proc,
    stop: async () => {
      proc.kill()
      unreap()
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

  deps = { db: t.db, bundleStore: store, simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it, so no invoker check to satisfy')), replayStore: new LocalReplayStore(bundleRoot) }
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

describe('adversarial: what a modified client cannot do', () => {
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
   *
   * SCOPED TO THIS DESCRIBE, not the file - review finding. At file level it
   * also ran before the pure-unit `reward's source of truth` block below,
   * creating an account and doing a database round trip for two synchronous
   * assertions that touch neither.
   */
  beforeEach(async () => {
    await setupPlayer(deps)
  })

  /**
   * A live creature belonging to `owner`, written through the OWNER
   * connection - wave-start.test.ts's and splice-commit.test.ts's idiom, and
   * for their reason: `grantBaseStock` is the only `insert(creatures)` on a
   * grant path and it mints Gen-1 Tier-I base stock with no way to ask for a
   * species, a trait or an owner.
   *
   * A REAL SECOND PLAYER's creature, not a row invented under a fabricated
   * uuid. `creatures.player_id` carries no foreign key, so a fabricated one
   * would insert happily - and would prove only that `wave/start` refuses an
   * id nothing owns, which is a weaker statement than refusing one SOMEBODY
   * ELSE owns.
   */
  async function give(owner: string): Promise<string> {
    const [row] = await t.ownerDb.insert(creatures).values({
      serverId: SERVER_ID,
      playerId: owner,
      species: 'Vetch',
      generation: 1,
      trait1: 'Taunt', tier1: 1,
      trait2: 'Carapace', tier2: 1,
      instinct: 'Vanguard',
      hpCurrent: 260, // engine Stats.CreatureHp(Vetch)
      isFounder: false,
    }).returning()
    return row!.creatureId
  }

  async function committedTo(creatureId: string): Promise<string | null> {
    const [row] = await t.ownerDb.select().from(creatures)
      .where(sql`${creatures.serverId} = ${SERVER_ID} AND ${creatures.creatureId} = ${creatureId}`)
    if (row === undefined) throw new Error(`no creature ${creatureId}`)
    return row.committedTo
  }

  it('cannot claim a win that did not happen', async () => {
    // The client claims Win; sim says Loss. api pays what sim returns and
    // never what the client claims - which is why the submission body
    // carries only the replay and an issuance id, with no outcome field
    // for a client to lie in.
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'adv-1')

    expect((await res.json() as { result: string }).result).toBe('Loss')
    expect(await balance('shards')).toBe(before)
  })

  it('cannot submit forged bytes', async () => {
    const { issuanceId } = await (await startWinning(6)).json() as { issuanceId: string }
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
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
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
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
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
      // actually blocked - not a sleep.
      //
      // sawWaiter IS AN ASSERTION, NOT A DIAGNOSTIC. Review finding: an
      // earlier version of this loop simply fell out of the deadline
      // branch and carried on. If the sync point is ever missed - a sim
      // call slower than the deadline, or a future handler that settles
      // earlier - T_ext commits before the handler's transaction even
      // opens, loadLiveIssuance returns undefined, and the 409 +
      // issuance_invalid below is observed ANYWAY. The test would pass
      // having silently collapsed back into the sequential double-submit
      // above, which is the very case it exists because that case proves
      // too little. Unasserted, this file's flagship new test was one slow
      // sim call away from being the seventh green-but-proving-nothing
      // assertion of the phase.
      let sawWaiter = false
      const deadlineAt = Date.now() + 3_000
      while (Date.now() < deadlineAt) {
        const [row] = (await t.ownerDb.execute(sql`
          SELECT count(*)::int AS n FROM pg_stat_activity
          WHERE wait_event_type = 'Lock' AND query ILIKE '%wave_issuances%'
            AND pid <> pg_backend_pid()`)).rows as { n: number }[]
        if ((row?.n ?? 0) > 0) { sawWaiter = true; break }
        await new Promise((r) => setTimeout(r, 25))
      }
      // The handler's backend was observed BLOCKED on this row. Everything
      // below is about what happens when that block is released; without
      // this, none of it is about contention at all.
      expect(sawWaiter).toBe(true)

      await client.query('COMMIT')
      const res = await pending

      // T_ext's commit makes settled_at non-NULL; the handler's blocked
      // UPDATE re-evaluates its WHERE under READ COMMITTED, matches
      // nothing, settle() returns false, and the gate refuses BEFORE
      // advanceCampaign and credit ever run.
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'issuance_invalid' })
    } finally {
      // ROLLBACK BEFORE RELEASE, and the order matters. Review finding:
      // every assertion above sits BETWEEN this connection's BEGIN and its
      // COMMIT, and `t.pool` is the APP pool the handlers themselves draw
      // from. pg-pool issues no ROLLBACK of its own on release, so a
      // throwing assertion would hand back a connection still inside an
      // open transaction, still holding a row lock on wave_issuances.
      //
      // MEASURED CONSEQUENCE, not a guessed one - an earlier version of
      // this comment claimed a cascade of 60s timeouts, and the round-2
      // reviewer tried to produce one and could not: with the ROLLBACK
      // deleted and the deadline forced, the run was 1 failed / 13 passed
      // in 4.8s. createPool's `max: 5` and `lock_timeout=5000` bound it,
      // and later tests use fresh players and different rows. So the real
      // cost is two consumed pool slots and an idle-in-transaction backend
      // for the rest of the file, not a cascade. The ROLLBACK stays because
      // it is correct and free; the claim about it is now the one that was
      // observed. Overstating a hazard in the gate file's own documentation
      // costs the same credibility as understating one.
      //
      // Swallowed because after a successful COMMIT this is a harmless
      // no-op, and a failure here must never mask the real assertion
      // failure above.
      await client.query('ROLLBACK').catch(() => {})
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
    const { issuanceId } = await (await startWinning(6)).json() as { issuanceId: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildWinningReplay(6, 0x1111n), 'adv-4')
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await balance('shards')).toBe(before)
  })

  it('cannot skip to wave 60', async () => {
    const res = await startWinning(60)
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
    // discriminating gate is wave-start.test.ts's `a conflicting insert on
    // wave_issuances_one_live resolves with the existing row rather than
    // aborting the transaction` - :207 today, but the NAME is the stable
    // anchor; the old :203 citation rotted when that file grew. It drives
    // claimIssuance directly against a real conflict.
    const firstRes = await startWinning(6)
    const secondRes = await startWinning(6)
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
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'adv-6')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: WAVE_6_REWARD } })
    expect(await balance('shards')).toBe(before + WAVE_6_REWARD)
  })

  it('cannot farm a cleared wave past the daily cap', async () => {
    await clearThrough(6)
    for (let i = 0; i < REPLAY_CAP_PER_DAY; i++) {
      const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
      expect((await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), `adv-7-${i}`)).status).toBe(200)
    }
    const before = await balance('shards')

    const res = await startWinning(6)
    expect(res.status).toBe(429)
    expect(await res.json()).toMatchObject({ code: 'replay_cap_reached' })
    expect(await balance('shards')).toBe(before)
  })

  it('counts a consumed issuance against the UTC day boundary, to the second', async () => {
    // WRITTEN BECAUSE WEAKENING 7 LEFT THE CAP TEST GREEN, THEN REWRITTEN
    // BECAUSE THE FIRST VERSION OF IT WAS INERT FOR THREE HOURS A DAY.
    //
    // Design §4.3: the replay cap counts consumed issuances since the UTC
    // DAY BOUNDARY, and 'consumed' rows are retained 48 hours rather than
    // three BECAUSE THEY ARE THE COUNTER. Every row the cap test above
    // creates is seconds old, so a count looking back only three hours
    // satisfies it identically.
    //
    // THE FIRST FIX PINNED A DISTANCE AND THAT WAS NOT ENOUGH - review
    // finding, with two demonstrations. Back-dating to
    // `greatest(day_start, now() - 3h)` only discriminates against windows
    // NARROWER than the distance it happened to travel, so (a) replacing
    // the day boundary with a ROLLING 24-HOUR window left the whole suite
    // green at 141/141 - a real player-visible regression, "three per UTC
    // day" silently becoming "three per rolling 24h", locking out a player
    // who took three at 23:50 until 23:50 the next day - and (b) between
    // 00:00 and 03:00 UTC the back-date resolves to less than three hours
    // and the three-hour weakening passed 14/14. A gate that is inert for
    // an eighth of every day, against the likelier of the two regressions,
    // is not a gate.
    //
    // SO PIN THE BOUNDARY, NOT A DISTANCE. One row exactly AT the boundary
    // must count; one row ONE SECOND BEFORE it must not. That pair is
    // independent of the time of day and of how wide any replacement
    // window is: a rolling window of ANY width that reaches back past
    // midnight fails the second half, and a window too narrow to reach
    // midnight fails the first.
    await clearThrough(6)

    // Captured ONCE and reused as a literal, so both halves pin the same
    // instant even though now() moves between them.
    //
    // As epoch MILLISECONDS rather than as a timestamp column: node-postgres
    // hands a timestamptz back as a string here, not a Date, so a
    // `dayStart.getTime()` on the raw row throws at runtime while
    // type-checking clean against an asserted row type. Caught by running
    // it. An integer needs no parser agreement between the driver and this
    // file.
    const [msRow] = (await t.ownerDb.execute(sql`
      SELECT (extract(epoch from date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC') * 1000)::bigint AS ms`))
      .rows as { ms: string | number }[]
    if (msRow === undefined) throw new Error('could not read the UTC day boundary')
    const dayStartMs = Number(msRow.ms)
    expect(Number.isFinite(dayStartMs)).toBe(true)

    // Three real consumed issuances. Two stay where they are; the third is
    // the probe that gets moved across the boundary.
    const ids: string[] = []
    for (let i = 0; i < REPLAY_CAP_PER_DAY; i++) {
      const started = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
      expect((await submit(started.issuanceId, buildWinningReplay(6, BigInt(started.seed)), `adv-7c-${i}`)).status).toBe(200)
      ids.push(started.issuanceId)
    }
    const probe = ids[REPLAY_CAP_PER_DAY - 1]!

    // Moving issued_at is not a settlement rewrite, so the write-once
    // trigger does not fire: it raises only when settled_at/settlement
    // themselves change on an already-settled row.
    const moveProbe = async (at: Date): Promise<void> => {
      const res = await t.ownerDb.execute(sql`
        UPDATE wave_issuances SET issued_at = ${at} WHERE issuance_id = ${probe}`)
      // Without this the two halves below could both be satisfied by an
      // UPDATE that silently matched nothing.
      expect(res.rowCount).toBe(1)
    }

    // THE BOUNDARY MUST NOT HAVE MOVED under us. clearThrough plus three
    // start/submit round trips take ~1-2s, and if UTC midnight falls inside
    // that window the captured boundary is now YESTERDAY's - the probe
    // would sit a day back, stop counting, and half one would read 200: a
    // spurious RED, roughly 2 seconds in every day. The failure direction
    // is safe (never a false green), but a gate that reddens for reasons
    // unrelated to what it tests is a gate people learn to re-run, which is
    // the same disease this file exists to treat. Re-read and compare
    // rather than assume.
    const [nowMsRow] = (await t.ownerDb.execute(sql`
      SELECT (extract(epoch from date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC') * 1000)::bigint AS ms`))
      .rows as { ms: string | number }[]
    if (nowMsRow === undefined || Number(nowMsRow.ms) !== dayStartMs) {
      // Deliberately a hard failure with a self-explaining message rather
      // than a silent skip: a skip that fires once a year is a test nobody
      // notices has stopped running. Re-run it; it cannot recur.
      throw new Error(
        'UTC midnight crossed during this test\'s setup, so the captured day '
        + 'boundary is stale and the assertions below would be meaningless. '
        + 'This is a ~2-second-per-day race in the TEST, not a defect in the '
        + 'replay cap - re-run.')
    }

    // HALF ONE: exactly AT the boundary. Still today, so it counts, so all
    // three count, so the cap binds. A window too narrow to reach midnight
    // (weakening 7's three hours, at any hour of the day) drops it and
    // this reads 200.
    await moveProbe(new Date(dayStartMs))
    const bound = await startWinning(6)
    expect(bound.status).toBe(429)
    expect(await bound.json()).toMatchObject({ code: 'replay_cap_reached' })

    // HALF TWO: one second BEFORE the boundary. Yesterday, so it must NOT
    // count, so only two do, so the cap must not bind. ANY rolling window
    // wide enough to reach back past midnight still counts it and this
    // reads 429 - which is what kills the rolling-24h mutation the first
    // version of this test could not see.
    await moveProbe(new Date(dayStartMs - 1_000))
    const free = await startWinning(6)
    expect(free.status).toBe(200)
    // The file asserts the CODE beside the status everywhere else; this is
    // the success side, so the equivalent is that a real issuance came back
    // rather than merely a non-429.
    expect(typeof ((await free.json()) as { issuanceId?: unknown }).issuanceId).toBe('string')
  })

  it('counts against midnight UTC even when the session TimeZone is not UTC', async () => {
    // THE GUARD FOR A REAL PRODUCT BUG THIS FILE EXPOSED, in Task 5's code
    // rather than in this phase's: issuance.ts's replay-cap count compared
    // the timestamptz issued_at column against
    // `date_trunc('day', now() AT TIME ZONE 'UTC')`, which is a `timestamp`
    // WITHOUT time zone. That comparison silently re-converts it through
    // the SESSION's TimeZone GUC, so the boundary landed on the session's
    // LOCAL midnight - under America/New_York, 04:00 UTC instead of 00:00
    // UTC, and every player's replay cap reset four hours late. The inner
    // `AT TIME ZONE 'UTC'` was written to remove exactly that dependence
    // and the implicit cast put it straight back.
    //
    // It was correct in production only because the GUC defaults to UTC on
    // postgres:16-alpine and Cloud SQL - so no existing test could see it,
    // because every existing test runs on that default.
    //
    // SET LOCAL inside the transaction, driving the REAL issueWave rather
    // than a copy of its SQL: a duplicated query here could drift from the
    // one that ships, which would make this gate guard nothing. The setting
    // is transaction-scoped and reverts on commit, so it cannot leak into
    // any other test or into the pooled connection.
    const { playerId } = await setupPlayer(deps)
    await clearThrough(6)

    for (let i = 0; i < REPLAY_CAP_PER_DAY; i++) {
      const started = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
      expect((await submit(started.issuanceId, buildWinningReplay(6, BigInt(started.seed)), `adv-tz-${i}`)).status).toBe(200)
    }

    const bundle = await loadBundle(deps.bundleStore)
    const countUnder = async (tz: string): Promise<Issuance | IssuanceRefusal> =>
      withServer(deps.db, SERVER_ID, async (tx) => {
        // set_config(..., true) is SET LOCAL: parameterised, so the zone
        // name is bound rather than interpolated into SQL text.
        await tx.execute(sql`SELECT set_config('TimeZone', ${tz}, true)`)
        // An empty deployment: this gate is about the replay CAP, which is
        // counted off consumed rows and is reached before design 6.1's roster
        // checks ever run. Task 8 grew the parameter; nothing here asserts on it.
        return issueWave(tx, SERVER_ID, playerId, 6, [], bundle)
      })

    // Put all three consumed rows EXACTLY on midnight UTC. Under the fixed
    // query that instant is inside today (the comparison is `>=`), so all
    // three count and the cap binds. Under the broken one the boundary is
    // the session's local midnight - LATER than midnight UTC in any zone
    // west of it - so all three fall outside and nothing counts.
    //
    // Pinning the boundary itself rather than an offset from it is what
    // makes this hold at every hour of the day: the broken boundary is
    // always the same calendar date read in the session's zone, so it is
    // always displaced from midnight UTC by that zone's offset, never
    // coincidentally equal.
    const onBoundary = await t.ownerDb.execute(sql`
      UPDATE wave_issuances
      SET issued_at = date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'
      WHERE player_id = ${playerId}::uuid AND settlement = 'consumed'`)
    expect(onBoundary.rowCount).toBe(REPLAY_CAP_PER_DAY)

    expect(await countUnder('America/New_York')).toEqual({ refused: 'replay_cap_reached' })

    // WRONG-REASON CHECK. Without this, a query that counted NOTHING under
    // a non-UTC session - or one that counted EVERYTHING regardless of date
    // - would be indistinguishable from a correct one above. One second
    // earlier is yesterday in UTC and must not count, under the same
    // hostile session.
    const beforeBoundary = await t.ownerDb.execute(sql`
      UPDATE wave_issuances
      SET issued_at = (date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC') - interval '1 second'
      WHERE player_id = ${playerId}::uuid AND settlement = 'consumed'`)
    expect(beforeBoundary.rowCount).toBe(REPLAY_CAP_PER_DAY)

    const free = await countUnder('America/New_York')
    expect('refused' in free).toBe(false)
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
    const first = await (await startWinning(6)).json() as { issuanceId: string }

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
      const res = await startWinning(6)
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

  it('CANNOT deploy creatures the player does not own', async () => {
    // THE MARKER, FLIPPED. This test was written in Phase 5 as
    // `CAN still deploy creatures the player does not own - Phase 6`: design
    // 2.2 left the hole open knowingly and marked it with an assertion about
    // the CURRENT behaviour rather than a comment about it, so that the day a
    // creature table existed the suite would fail and name the thing that
    // changed. It is REWRITTEN IN PLACE rather than deleted and replaced -
    // the flip is what "Phase 6 landed the roster check" looks like in the
    // suite (design 6.3), and a fresh test beside a deleted one says nothing.
    //
    // WHAT CLOSED THE HOLE IS NOT THIS CHECK. design 6.1 resolves every spec
    // from the row the request names, so an unowned deployment is not refused
    // after being simulated - it is INEXPRESSIBLE, and refused here on the
    // CHEAP path, before any simulation is paid for. The refusal is what a
    // client sees; the mechanism is that there is no path from a
    // client-supplied value to a stored spec.
    //
    // ASSERTS ON STATE, not only on the code: a route that refused
    // everything would pass a status-code assertion perfectly. Nothing was
    // issued, nothing was paid, and the other player's creature was not
    // committed to anything.
    const other = await setupPlayer(deps)
    const theirs = await give(other.playerId)
    await setupPlayer(deps)
    const before = await balance('shards')

    const res = await startWave(6, [{ creatureId: theirs, pocket: 0 }])

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'creature_not_owned' })
    expect(await liveIssuance()).toBeUndefined()
    expect(await balance('shards')).toBe(before)
    expect(await committedTo(theirs)).toBeNull()
  })

  it('cannot submit a replay claiming a deployment it was not issued', async () => {
    // THE OTHER HALF OF THE MARKER, and the half the rewrite above moves
    // away from. Phase 5's version mounted its attack at SUBMIT - a replay
    // carrying creatures nothing checked - and closing the boundary at
    // issuance would be worth nothing if a submitted replay were still free
    // to claim a deployment other than the one the issuance froze. design
    // 6.2: a deployment in the echo that does not match the issuance is a
    // breach, taken on the path Phase 5 built for a seed or wave-id mismatch.
    //
    // { tier: 3 } IS THE ORIGINAL MARKER'S OWN ATTACK, PRESERVED. It is a
    // LEGAL winning deployment (Deployments.MaxCoverageTier is 3 and
    // Stats.ChillCapacity(3) is 4), so sim VERIFIES it and returns a Win -
    // which is what makes this a test of the comparison rather than of sim
    // rejecting junk, exactly as the reward assertion was in Phase 5's
    // version. The player's own Pale is tier 1 (winningRoster), so the
    // submitted deployment is one they do not own.
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed), { tier: 3 }), 'adv-8')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'deployment_mismatch' })
    // A VERIFIED WIN THAT PAID NOTHING. The balance pins that the refusal
    // beat the credit rather than the credit being absent for some unrelated
    // reason, and the settled issuance pins that this was a spent attempt -
    // the same settlement a seed mismatch takes.
    expect(await balance('shards')).toBe(before)
    expect(await liveIssuance()).toBeUndefined()
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
 * 1. `matchesIssuance` rejects an echo/issuance wave-id mismatch at its
 *    call site in submit step 5 (routes/wave.ts:372), BEFORE `rewardForWave`
 *    is reached in step 6 (:417). The FUNCTION NAMES are the stable anchors:
 *    the old :223/:257 citations rotted when Task 5 reordered the handler,
 *    and :372/:417 were re-read out of the shipped file at this round. By
 *    the time the lookup runs, `echo.waveId === issuance.waveId` is
 *    guaranteed, so reading either is behaviourally identical - the
 *    weakening is a no-op.
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
    // Task 5 gave Bundle a `nodes` field and Task 6 a `traits` one. Both are
    // empty here because rewardForWave reads waves and nothing else - a
    // fixture that carried either would imply this test had an opinion about
    // the map or about the splice, which it does not.
    nodes: [],
    traits: [],
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
