import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { arks, servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { settle } from '../src/wave/issuance.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  asRosterSpecs, balance, buildLosingReplay, buildReplayOf, buildWinningReplay, giveRoster,
  ledgerRowCount, liveIssuance, rosterCount, setupPlayer, startLosing, startWave,
  startWinning, submit, submitInit, winningDeployment, winningRoster, withDotnetBuildLock,
} from './wave-helpers.ts'

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, and .pathname percent-encodes it (wave-start.test.ts's
// own comment, carried forward - both files resolve the same repo root).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')
// serverId is always 1 in this file - the one server beforeAll creates
// (wave-start.test.ts's own convention, carried forward).
const SERVER_ID = 1
const SIM_PORT = 5299 // distinct from generate-contract.sh's 5199, so this file can run alongside contract.test.ts
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

// withDotnetBuildLock is imported from wave-helpers.ts - see that file's
// comment above it for the full account of why a lock exists here at all
// (a shared services/sim/obj/ race between this file, replays.test.ts and
// generate-contract.sh) and why it is mkdir-based with pid-owned staleness
// reclaim rather than a plain directory-exists mutex.

/**
 * Starts the REAL sim service as a child process, once for this file.
 *
 * A stub standing in for sim would be a second implementation of the exact
 * boundary this phase exists to prove - see the brief. This mirrors
 * implementation/scripts/generate-contract.sh's own build-then-run dance
 * (`dotnet build` + `dotnet <dll>`, not `dotnet run`): `dotnet run` on THIS
 * project does not work on this machine, while build-then-run consistently
 * serves /healthz within a second. It does not hang, which is what this
 * comment used to say - it is killed immediately and silently. See that
 * script's comment above its own `dotnet build` for the reproduction, what
 * has been ruled out, and what to check once CI exists; that note is the
 * authoritative one and this is only a pointer to it.
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
  // afterAll's stop() is not a guarantee: vitest terminates a test file's
  // worker by signal on several of its own teardown paths, and a worker
  // that dies by signal runs no afterAll - orphaning this host, which keeps
  // SIM_PORT and makes the NEXT run of this file fail to bind. Observed,
  // not projected; see child-reaper.ts.
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
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-submit-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  deps = { db: t.db, bundleStore: store, simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it, so no invoker check to satisfy')), replayStore: new LocalReplayStore(bundleRoot) }

  await setupPlayer(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // GUARDED, like the two above it. bundleRoot is assigned PARTWAY through
  // beforeAll, so when beforeAll aborts before that point - a container that
  // will not start, a sim host that never binds 5299 - this line ran with
  // undefined and threw
  //   TypeError: The "path" argument must be of type string ...
  // on top of the real error. That TypeError is what the tail of the output
  // carries, and it reads like the cause. It is not cosmetic: that exact
  // shape got an intermittent flake in this suite misattributed twice, and
  // three reviews passed over a real bug because the reports they rested on
  // carried vitest's FAIL line and this TypeError, never the actual error.
  // masked-teardown.test.ts pins both halves.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/submit', () => {
  it('pays a winning submission exactly once', async () => {
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))

    const res = await submit(issuanceId, replay, 'key-1')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: 40 } })

    expect(await balance('shards')).toBe(250 + 40)
    expect(await ledgerRowCount()).toBe(3) // 2 from the starter grant, 1 from this
  })

  // DO NOT DELETE OR RENAME THIS TEST WITHOUT READING THIS FIRST.
  //
  // Design §4.2's GUARD ONE - "pays exactly once under retry", the response
  // half - has NO COVERAGE IN adversarial.test.ts, which is design §7's gate.
  // Every double-submit test in that file uses a DIFFERENT idempotency key,
  // deliberately: different-key replay is the attack a modified client mounts
  // and the issuance must stop it, while same-key replay is the honest retry
  // and a correctness property about not lying to a client that did nothing
  // wrong. Those are two properties and they live in two files. The ruling not
  // to duplicate this test into the gate is recorded as row 10 of
  // test/weakenings.md, which also shows the weakening it hides: refusing
  // straight from submit's step-2 read leaves that suite 15/15 GREEN while
  // answering a retrying client 409 for a wave it was in fact paid for.
  //
  // The line row 10 cannot write, because it is a record of the gate rather
  // than of this file: guard one's coverage is HERE, in this test and in
  // `refuses a dead issuance without paying for a re-simulation,
  // indistinguishably from before` below - the two that go red under that
  // weakening. They are the whole of it. Delete both and guard one is covered
  // NOWHERE, and nothing anywhere will say so.
  it('returns the stored response on a resend with the SAME key', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-2')
    const second = await submit(issuanceId, replay, 'key-2')

    expect(second.status).toBe(200)
    // Guard one: the idempotency key protects the RESPONSE.
    expect(await balance('shards')).toBe(before + 40)
  })

  it('replays a resend whose issuanceId casing changed', async () => {
    // The quiet half of the case-sensitivity defect Task 7's fix round found
    // on /v1/splice/commit. This route never compares issuanceId in JS - it
    // goes to Postgres, where uuid equality is case-insensitive - but it
    // HASHES the parsed body for the idempotency key. Without normalisation
    // the same logical retry with different casing hashes differently and
    // the caller gets 422 for a request that is theirs and identical, having
    // already been paid for the first one.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-case')
    const second = await submit(issuanceId.toUpperCase(), replay, 'key-case')

    expect(second.status).toBe(200)
    expect(await balance('shards')).toBe(before + 40)
  })

  it('pays nothing on a resend with a DIFFERENT key', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-3')
    const second = await submit(issuanceId, replay, 'key-4-different')

    // Guard two: the issuance protects the LEDGER. §6.3's key is
    // client-supplied and a modified client simply sends a new one, so
    // idempotency alone is not a defence - design 4.2.
    expect(second.status).toBe(409)
    expect(await second.json()).toMatchObject({ code: 'issuance_invalid' })

    // Assert the BALANCE, not the code. A test that only checks for
    // issuance_invalid passes identically against a server that rejects
    // everything - design 7's vacuity guard.
    expect(await balance('shards')).toBe(before + 40)
  })

  it('pays nothing under a genuinely concurrent submission with a different key', async () => {
    // KNOWN LIMITATION, recorded rather than hidden (task-6-report.md has
    // the full account). The test above sends its two requests
    // SEQUENTIALLY: by the time the second even starts, the first has
    // already committed and settled, so it cannot tell settle() gating
    // credit() (design 4.2's guard two) apart from settle() running
    // anywhere else, as long as it happens before the response returns -
    // confirmed by deliberately weakening the handler (settle() moved out
    // of the credit's transaction, ungated) and watching that sequential
    // test stay green. This is "a concurrency test satisfied equally by the
    // serialized path", the exact failure shape this run was warned about.
    //
    // This test fires both requests through a BARRIER - each gets its own
    // SimClient that completes the REAL call to sim first, then blocks
    // until BOTH have arrived before releasing them into their
    // transactions together - specifically to close that gap without
    // touching production code. It is a genuine improvement (it removes
    // the guaranteed-serial ordering above) but NOT a guaranteed repro: the
    // critical section between "issuance still live" and "settle it" is
    // ~1-2ms against this local Postgres, comparable to or smaller than the
    // jitter in the second request simply acquiring its own pool
    // connection, so the two requests still frequently finish serially by
    // accident even after being released together. Verified directly: with
    // the handler deliberately weakened the same way, this barrier-based
    // test still passed across repeated runs, and only a hard,
    // production-code delay inserted at the exact critical point (added
    // and removed for that one check, never committed) reliably reproduced
    // the defect. This test is kept because it is strictly better than the
    // sequential one and asserts a real invariant, not because it reliably
    // catches guard two's removal - that is guaranteed by the code
    // structure (settle() runs INSIDE the same transaction as credit(), and
    // credit() is gated on its return - see routes/wave.ts), not by this
    // test.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    let arrived = 0
    let release: () => void
    const bothArrived = new Promise<void>((r) => { release = r })
    const barrierClient = (real: typeof deps.simClient): typeof deps.simClient => ({
      simulate: async (r: string) => {
        const verdict = await real.simulate(r)
        arrived += 1
        if (arrived >= 2) release()
        await bothArrived
        return verdict
      },
    }) as typeof deps.simClient

    const appA = createApp({ ...deps, simClient: barrierClient(deps.simClient) })
    const appB = createApp({ ...deps, simClient: barrierClient(deps.simClient) })

    // Robustness fix, review finding: if EITHER request fails before ever
    // calling simulate() (a bug earlier in the handler - session, parsing -
    // rather than anything this test is about), `arrived` never reaches 2,
    // `release()` is never called, and both requests would otherwise hang
    // until vitest's own 60s test timeout with no indication why. A bounded
    // race with a clear message turns that into an immediate, diagnosable
    // failure instead.
    const [a, b] = await Promise.race([
      Promise.all([
        appA.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-race-a')),
        appB.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-race-b')),
      ]),
      new Promise<never>((_resolve, reject) => setTimeout(() => reject(new Error(
        'barrier never released within 5s - one request likely failed before '
        + 'reaching simulate() (arrived never reached 2), not a real hang in '
        + 'the handler under test.',
      )), 5_000)),
    ])

    // Exactly one request wins (200, paid) and the other finds nothing left
    // to consume (409 issuance_invalid) - never both winning, which is the
    // double-credit this guard exists to prevent.
    const statuses = [a.status, b.status].sort()
    expect(statuses).toEqual([200, 409])
    expect(await balance('shards')).toBe(before + 40)
  })

  it('settle() returns false on a second call - nothing today asserted this in isolation', async () => {
    // Cheap, and currently untested: wave-helpers.ts's consumeLiveIssuance
    // and clearWave both discard settle()'s return value, so nothing in the
    // suite pinned the contract routes/wave.ts's guard two depends on.
    //
    // NOTE: this does NOT close design §7's named weakening on its own -
    // weakening (b) modifies routes/wave.ts (moves settle() out of the
    // credit's transaction, stops checking its return), not wave/issuance.ts
    // itself, so this unit test on settle() stays green even under that
    // weakening: settle() itself is untouched, still returns false
    // correctly, routes/wave.ts is simply the one that stops listening.
    // Necessary, not sufficient - see the next test for what actually
    // closes the gate.
    await setupPlayer(deps)
    await startWinning(6)
    const live = await liveIssuance()
    expect(live).toBeDefined()

    const first = await withServer(deps.db, SERVER_ID, (tx) => settle(tx, live!, 'consumed'))
    expect(first).toBe(true)

    const second = await withServer(deps.db, SERVER_ID, (tx) => settle(tx, live!, 'consumed'))
    expect(second).toBe(false)
  })

  it('closes design §7\'s named weakening: a settle forced to block on a real row lock refuses rather than double-pays', async () => {
    // THE TEST THAT CLOSES THE GATE. §7 names "settle the issuance outside
    // the credit's transaction" as a required weakening the double-submit
    // test must be shown to fail against. Neither the sequential test above
    // nor the barrier-based one can do it (see both tests' own comments,
    // and task-6-report.md §2/§8): the critical section is too short
    // (~1-2ms) relative to ordinary connection-acquisition jitter for a
    // bare race to reliably land inside it.
    //
    // This forces the SAME race deterministically instead of hoping timing
    // cooperates, by taking the row lock directly rather than racing for
    // it - Task 5's instinct (claimIssuance's own conflicting-insert test)
    // applied one level up, to an UPDATE's row lock instead of an INSERT's
    // unique-index conflict.
    //
    // T_ext: a SEPARATE raw connection (this pool's max is 5, so it does
    // not starve the handler's own transaction), its own manually-managed
    // transaction - Drizzle's db.transaction() auto-commits on callback
    // return, which cannot hold a lock open across the await below - runs
    // the SAME settling UPDATE settle() itself would run, and does NOT
    // commit. This simulates "another submission (or a future sweep) is in
    // the middle of consuming this issuance right now."
    await setupPlayer(deps)
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
      // Sanity: T_ext genuinely took the row. If this is not 1, nothing
      // below proves anything and the test would otherwise pass vacuously.
      expect(locked.rowCount).toBe(1)

      // Fire the submit WITHOUT awaiting - it must now contend with T_ext
      // for the SAME row. Under the shipped (gated) handler, its own
      // settle() call blocks here, on a REAL Postgres row lock, not on
      // anything this test schedules or times.
      const pending = submit(issuanceId, replay, 'key-lock-race')

      // Wait for a REAL synchronization point - the handler's backend
      // actually blocked - rather than a sleep. Bounded, with a fallback:
      // if the waiter never appears (which is exactly what the WEAKENED
      // handler looks like - nothing inside its transaction touches this
      // row until after it has already committed a credit), that absence
      // is itself part of the failure this test exists to catch, not a
      // reason to hang. Comfortably inside the lock_timeout=5000 that
      // src/db/client.ts's createPool sets (there is no pool.ts - the name
      // this comment used to cite does not exist in this package), so
      // a genuinely blocked backend is never killed out from under us.
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

      // Against the shipped code: T_ext's commit makes settled_at no
      // longer NULL, the handler's blocked UPDATE re-evaluates its WHERE
      // under READ COMMITTED, matches nothing, settle() returns false, and
      // the gate refuses BEFORE advanceCampaign/credit ever run.
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'issuance_invalid' })
    } finally {
      client.release()
    }

    expect(await balance('shards')).toBe(before)
  })

  it('answers a malformed issuanceId with 400, not with a 500 from Postgres', async () => {
    // A LIVE DEFECT SINCE PHASE 5, found while Task 6 gave creature ids the
    // same shape check on /v1/splice/preview. `parseSubmit` accepted any
    // non-empty string, so the value reached `loadLiveIssuance`'s comparison
    // against `wave_issuances.issuance_id` - a Postgres `uuid` column - where
    // 22P02 was raised inside the read and app.ts's onError turned it into
    // `internal`. Any authenticated player could make the API report 500 for
    // a body they had malformed.
    //
    // Measured before the fix: HTTP 500 `{"code":"internal"}`.
    await setupPlayer(deps)
    const res = await submit('not-a-uuid', buildWinningReplay(6, 1n), 'key-bad-uuid')

    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('still refuses a WELL-FORMED issuanceId nobody issued the ordinary way', async () => {
    // THE OTHER HALF, and without it the test above is satisfied by a check
    // that swallowed every unknown id into 400. The shape check is not a
    // statement about which issuances exist - the same thing wave/start's
    // MAX_WAVE_ID and region.ts's MAX_NODE_SLOT say about their own bounds -
    // so a fabricated uuid must still travel to step 2 and come back
    // `issuance_invalid`, exactly as it did before.
    await setupPlayer(deps)
    const res = await submit(randomUUID(), buildWinningReplay(6, 1n), 'key-unknown-uuid')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'issuance_invalid' })
  })

  it('refuses a replay whose seed is not the issued one', async () => {
    await setupPlayer(deps)
    const { issuanceId } = await (await startWinning(6)).json() as { issuanceId: string }
    const replay = buildWinningReplay(6, 0xDEADBEEFn) // a seed nobody issued

    const res = await submit(issuanceId, replay, 'key-5')
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await liveIssuance()).toBeUndefined() // consumed, not re-usable
  })

  it('refuses a replay carrying a second, different seed against this issuance', async () => {
    // NAMED FOR WHAT IT ACTUALLY TESTS - review finding. This is a SECOND
    // seed mismatch case (waveId 6, same as the issued wave; seed 1n, not
    // the one issued), not a different-WAVE case: with one authored wave
    // (config/bundles/0.1.1/waves.json carries only id 6) there is no real
    // wave-7 replay to build. Step 5 and step 2 are DIFFERENT checks - the
    // issuance proves the player was given a wave; the seed proves this
    // replay is of THAT wave - and weakening (a) (§3 of task-6-report.md)
    // confirms this test and 'refuses a replay whose seed is not the issued
    // one' above flip TOGETHER when the seed comparison is deleted, because
    // both exercise the same half of matchesIssuance. The wave-id half of
    // that same check is covered separately and directly, without needing
    // a second authored wave, by sim-client.test.ts's 'refuses a genuine
    // mismatch regardless of which branch the type took'.
    await setupPlayer(deps)
    const { issuanceId } = await (await startWinning(6)).json() as { issuanceId: string }

    const res = await submit(issuanceId, buildWinningReplay(6, 1n), 'key-6')
    expect(res.status).toBe(409)
  })

  it('refuses a submission from a superseded engine and tells the client to update', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed), { engineVersion: '0.1.0' })

    const res = await submit(issuanceId, replay, 'key-7')
    expect(res.status).toBe(426)
    expect(await res.json()).toMatchObject({ code: 'engine_too_old' })
  })

  it('leaves the issuance live when sim is unreachable', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }

    const broken = createApp({ ...deps, simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route under test here calls sim')) })
    const res = await broken.request(
      '/v1/wave/submit', submitInit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-8'))

    expect(res.status).toBe(503)
    expect(await res.json()).toMatchObject({ code: 'sim_unavailable' })

    // design 8: this phase does NOT grant optimistically and does NOT build
    // a clawback. The issuance surviving is what makes a retryable 503 an
    // honest answer rather than a lost wave.
    expect(await liveIssuance()).toMatchObject({ issuanceId })
  })

  it('records a loss without paying', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'key-9')
    const body = await res.json() as { result: string; reward?: unknown; breaches: unknown[] }

    expect(body.result).toBe('Loss')
    expect(body.reward).toBeUndefined()
    // combat_engine 7's three booleans, forwarded for the Wave Defeat
    // screen. api stores none of them and interprets none of them.
    expect(body.breaches[0]).toHaveProperty('access')
  })

  it('refuses a dead issuance without paying for a re-simulation, indistinguishably from before', async () => {
    // Design §4.2's step 2 runs BEFORE its step 3. This handler shipped
    // with the two transposed, so a fabricated or already-settled
    // issuanceId bought a full re-simulation before being refused - an
    // abuse surface and a cost, not a correctness bug.
    //
    // ASSERTED ON A CALL COUNTER, NOT ON LATENCY. A timing assertion for
    // "sim was not reached" is a flake against a sim host whose response
    // time varies by an order of magnitude between a warm and a cold
    // process; the counter is exact and cannot be satisfied by a fast run.
    await setupPlayer(deps)

    let calls = 0
    const counting = {
      simulate: async (r: string) => { calls += 1; return deps.simClient.simulate(r) },
    } as typeof deps.simClient
    const app2 = createApp({ ...deps, simClient: counting })

    // CONTROL, AND IT IS NOT DECORATION. Without it every assertion below
    // is satisfied identically by a handler that never calls sim at all -
    // including a broken one - and by a counter wired to nothing. This
    // pins that the live path DOES reach sim, exactly once, through this
    // very counter, before anything claims the dead path does not.
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const paid = await app2.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-count-live'))
    expect(paid.status).toBe(200)
    expect(calls).toBe(1)

    // The SAME issuance, now settled by the call above - the
    // "already-settled" half. Under the shipped order this costs one DB
    // read; under the transposed order it costs a re-simulation.
    const settledAgain = await app2.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-count-settled'))
    expect(settledAgain.status).toBe(409)
    expect(calls).toBe(1)

    // The "fabricated" half: an issuance id that never existed.
    const fabricated = await app2.request(
      '/v1/wave/submit', submitInit(randomUUID(), replay, 'key-count-fabricated'))
    expect(fabricated.status).toBe(409)
    expect(calls).toBe(1)

    // GUARD ONE SURVIVES THE REORDER, and this is here because it did not
    // at first. A client retrying across a network failure resends the
    // SAME key - and by then the issuance it paid for is settled, so it is
    // "dead" to the step-2 read exactly like the two requests above. It
    // must still get its stored 200 receipt back, never a 409 for a wave
    // it was in fact paid for. The first draft of the reorder refused
    // straight from the step-2 read and broke precisely this; the dead
    // path now skips sim and still falls through to withIdempotency.
    //
    // `calls` unchanged is the other half: the stored response is replayed
    // WITHOUT a re-simulation, which is what the resend was costing before.
    const receipt = await app2.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-count-live'))
    expect(receipt.status).toBe(200)
    expect(await receipt.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: 40 } })
    expect(calls).toBe(1)

    // A CLIENT CANNOT DETECT THE REORDER. The refusal for a dead issuance
    // is `issuance_invalid` with the same message it carried before the
    // steps were swapped, and a fabricated id is answered byte-identically
    // to a real-but-settled one - so the reorder leaks nothing about which
    // issuance ids ever existed, which is the property that would have
    // made the transposed order load-bearing. Compared as whole bodies
    // rather than as codes: a future change that added a `details` field
    // on one branch and not the other would pass a code-only assertion.
    const settledBody = await settledAgain.json()
    const fabricatedBody = await fabricated.json()
    expect(settledBody).toEqual({
      code: 'issuance_invalid', message: 'That issuance is not live for this player.',
    })
    expect(fabricatedBody).toEqual(settledBody)
  })
})

/**
 * DESIGN §6.2 - the third field of the echo comparison.
 *
 * Steps 5 of `POST /v1/wave/submit` already compared `echo.seed` and
 * `echo.waveId` against the issuance. The deployment joins them: `api` stored
 * the specs it resolved from the player's OWN rows at issuance (design §6.1),
 * `sim` reports the deployment the submitted replay actually claimed, and a
 * disagreement is a breach on the same path a seed mismatch takes.
 *
 * THE POSITIVE CONTROL IS `pays a winning submission exactly once`, above.
 * Every test here asserts a REFUSAL, and a handler that refused every
 * submission would pass all of them; that test is what says the comparison
 * accepts an honest pair, and it is in this file deliberately so the two
 * cannot be separated.
 */
describe('POST /v1/wave/submit - the deployment comparison', () => {
  it('rejects a submission whose deployment is not the issued one', async () => {
    // THE ATTACK, IN ITS REAL SHAPE: deploy what you actually own, submit a
    // replay claiming what wins. The issuance is issued against a frontline
    // with no Chill behind it - a deployment that LOSES wave 6 - and the
    // replay claims the winning one.
    //
    // WHICH DIRECTION THIS RUNS MATTERS. The mirror image (issue the winning
    // roster, submit the losing replay) is a much weaker test: a losing
    // replay pays nothing whether or not the comparison exists, so the
    // balance assertion below would hold vacuously. This way round, deleting
    // the comparison PAYS 40 shards for a wave the player deployed to lose.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-swap')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'deployment_mismatch' })
    expect(await balance('shards')).toBe(before)
    // A spent attempt, settled the same way a seed mismatch is - they
    // simulated a wave and got an answer.
    expect(await liveIssuance()).toBeUndefined()
  })

  it('rejects a submission that deploys MORE creatures than it was issued', async () => {
    // THE LENGTH CASE, AND IT IS THE ONE THAT KEEPS THE COMPARISON HONEST.
    // `claimIssuance`'s `deployment` parameter defaulted to `[]` until this
    // task, and a comparison written as a loop OVER THE STORED ARRAY runs
    // ZERO ITERATIONS against an empty one and agrees with everything. The
    // two creatures here are a stored deployment that is SHORTER than the
    // echo rather than empty, so it catches the same bug without depending
    // on a default that no longer exists.
    await setupPlayer(deps)
    const twoOfThem = (await winningRoster()).slice(0, 2)
    const { issuanceId, seed } = await (await startWave(6, twoOfThem)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-longer')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'deployment_mismatch' })
    expect(await balance('shards')).toBe(before)
  })

  it('rejects a submission that deploys the SAME creatures in a different order', async () => {
    // A SET COMPARISON WOULD PASS THIS ONE, which is the whole reason it is
    // here. Every spec below appears in the issuance, with the same pocket -
    // only the ORDER differs, and order is meaning: the engine indexes its
    // parallel arrays by deployment order (SimState: "Index == deployment
    // order"), and `resolveDeployment` iterates the REQUEST's order for
    // exactly that reason.
    //
    // Asserting the CODE and not only the balance is what makes this
    // discriminating: under a set comparison the reordered replay is
    // verified, and whether it then pays depends on an engine outcome this
    // test has no business predicting. `deployment_mismatch` cannot be
    // reached that way at all.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const reordered = winningDeployment()
    reordered.unshift(reordered.pop()!) // the Chill Pale moves to index 0, keeping pocket 4
    const res = await submit(issuanceId, buildReplayOf(6, BigInt(seed), reordered), 'key-reorder')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'deployment_mismatch' })
    expect(await balance('shards')).toBe(before)
  })

  it('rejects a submission against an issuance minted before the column had a writer', async () => {
    // THE NULLABLE-COLUMN RULING, PINNED - and without this test the ruling
    // is code-inspection only. `wave_issuances.deployment` is nullable
    // because drizzle/0006 is the EXPAND step, so for up to ISSUANCE_TTL_MS
    // after the comparison deploys, a player can hold a live issuance minted
    // by a build that never populated the column.
    //
    // "No stored deployment, so skip the check" is the obvious handling and
    // it reopens the hole this phase exists to close - for every player, for
    // two hours, on every deploy that crosses this boundary, WITH A GREEN
    // SUITE THROUGHOUT. That is the weakening this test exists to redden:
    // `deploymentMatches` returning true for a null stored deployment
    // changes no other assertion in the repository.
    //
    // The row is nulled by hand rather than by checking out the previous
    // build, for the reason adversarial.test.ts ages an issuance by hand
    // rather than waiting out the TTL: the previous build is not what is
    // under test, the shape of the row it left behind is.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    const nulled = await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET deployment = NULL WHERE issuance_id = ${issuanceId}::uuid`)
    // Without this the assertions below are satisfied by an UPDATE that
    // silently matched nothing, against an issuance whose deployment is
    // simply wrong for some other reason.
    expect(nulled.rowCount).toBe(1)

    // An OTHERWISE PERFECT submission: the issued wave, the issued seed, and
    // the deployment the player really does own. Only the stored column is
    // absent, so nothing but the null branch can produce this refusal.
    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-null-deployment')

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'deployment_mismatch' })
    expect(await balance('shards')).toBe(before)
    expect(await liveIssuance()).toBeUndefined()
  })

  it('accepts an issuance and a replay that agree on all five', async () => {
    // THE CONTROL FOR THE THREE ABOVE, stated as an assertion about the
    // STORED DEPLOYMENT rather than only about the 200. Without it, a
    // comparison that rejected every non-empty deployment would pass all
    // three refusals, and the win in `pays a winning submission exactly
    // once` would be the only thing standing between this suite and a route
    // that had stopped accepting real submissions - and nothing there says
    // WHAT was compared.
    await setupPlayer(deps)
    const deployed = await winningRoster()
    const started = await (await startWave(6, deployed)).json() as { issuanceId: string; seed: string }

    const stored = (await liveIssuance())!.deployment
    expect(stored).toHaveLength(5)
    // Resolved from the owned rows, and those rows were minted from the very
    // specs the replay claims - so this is the pairing the comparison sees.
    expect(stored).toEqual(asRosterSpecs(winningDeployment()).map(({ hp: _hp, ...spec }) => spec))

    const res = await submit(started.issuanceId, buildWinningReplay(6, BigInt(started.seed)), 'key-agree')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win' })
  })
})

/**
 * DESIGN §2.4 AND `base_stock` §3 - the supply line under the loop.
 *
 * A splice is net -1 creature, so without a source the loop runs for as many
 * splices as the player has fodder and then seizes. `POST /v1/node/claim` is
 * one source (Task 5); this is the other, and it is the one `base_stock` §3
 * makes a FLOOR: "wave-completion base stock never scales with any facility,
 * purchase, tier or event."
 */
describe('POST /v1/wave/submit - wave-completion base stock', () => {
  /**
   * An `arks` row does not necessarily exist - `loadArk` falls back to
   * DEFAULT_ARK and nothing in this phase writes the table - so this is an
   * upsert rather than an update.
   */
  async function setArk(
    playerId: string, o: { harvestArrayTier?: number; hatcheryTier?: number },
  ): Promise<void> {
    const harvestArrayTier = o.harvestArrayTier ?? 1
    const hatcheryTier = o.hatcheryTier ?? 1
    await t.ownerDb.insert(arks).values({
      // THE_REGION (src/map/claim.ts) - this phase's one region, and the
      // column is `text NOT NULL` with no default, so a row cannot be
      // written without naming one.
      serverId: SERVER_ID, playerId, regionId: 'verdant-shelf',
      harvestArrayTier, hatcheryTier,
    }).onConflictDoUpdate({
      target: [arks.serverId, arks.playerId],
      set: { harvestArrayTier, hatcheryTier },
    })
  }

  it('grants base stock on the verified submit path', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await rosterCount()

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-stock-1')
    expect(res.status).toBe(200)

    // +2, not +1 - Task 8. This fresh player's first-ever completion is wave
    // 6, so `grantWaveBaseStock` mints the Founder (Task 6) AND, separately,
    // wave 6's own settlement grants the Pale (Task 8's
    // `grantWave6Pale`) - two independent grants sharing this one
    // transaction, not one grant that got bigger.
    expect(await rosterCount()).toBe(before + 2)
    // THE SAME TRANSACTION AS THE REWARD CREDIT. A grant that could land
    // without the credit, or the credit without the grant, is two writes
    // pretending to be one - and a player paid but not granted has lost a
    // creature to a crash and cannot tell.
    expect(await balance('shards')).toBe(250 + 40)
  })

  it('grants NOTHING FROM BASE STOCK on a losing submission', async () => {
    // The BASE-STOCK grant is the REWARD's companion, so it lives on the
    // branch the reward lives on. This is also the test that reddens if
    // that grant is hoisted out of the credit's transaction and run
    // unconditionally.
    //
    // NOT "grants nothing" any more - Task 8. Wave 6's own Loss branch
    // grants a Pale (wave6-pale.test.ts owns that behaviour in full); this
    // test's subject is narrower and unchanged: `grantWaveBaseStock` itself
    // still never runs on a Loss.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const before = await rosterCount()
    const shards = await balance('shards')

    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'key-stock-2')
    expect(await res.json()).toMatchObject({ result: 'Loss' })

    expect(await rosterCount()).toBe(before + 1) // the wave-6 Pale, not base stock
    expect(await balance('shards')).toBe(shards)
  })

  it('grants NOTHING on a submission the deployment comparison refuses', async () => {
    // The grant follows the CREDIT, not the submission. A tampered
    // submission earns nothing - the sentence Phase 5's done-when was
    // deliberately written narrower than, and which this phase earns - and
    // "nothing" has to include the creature.
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const before = await rosterCount()

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-stock-3')
    expect(res.status).toBe(409)

    expect(await rosterCount()).toBe(before)
  })

  it('does not scale wave base stock with the Harvest Array', async () => {
    // `base_stock` §3's guardrail, and it is absolute: "wave-completion base
    // stock never scales with any facility, purchase, tier or event." It is
    // the floor under every player and the only supply line nothing can
    // accelerate - which is exactly why the loop leans on it.
    //
    // Tier 12 is the top of §3.1's table, where NODE-sourced base stock
    // doubles. If this line ever took a multiplier it would be that one, so
    // this is the tier that makes the difference visible: 1, not 2.
    const { playerId } = await setupPlayer(deps)
    await setArk(playerId, { harvestArrayTier: 12 })
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }
    const before = await rosterCount()

    expect((await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-stock-4')).status).toBe(200)

    // +2: this fresh player's first win mints the Founder AND the wave-6
    // Pale (Task 8) - see 'grants base stock on the verified submit path'
    // above for the same arithmetic.
    expect(await rosterCount()).toBe(before + 2)
  })

  it('skips the BASE-STOCK grant at the Hatchery cap and still pays the reward', async () => {
    // SKIPPED, NOT FAILED - and this is the one place the wave path and the
    // claim path (design §4.3) deliberately differ over the same write.
    // `POST /v1/node/claim` refuses the WHOLE claim rather than truncating a
    // grant, because a partial grant is a loss a player reports as theft.
    // Here the grant is ONE creature and the alternative is refusing a wave
    // the player won: that would make the Hatchery cap a soft lockout, which
    // bible §7.2 forbids. So the reward is paid and the creature is not
    // minted.
    await setupPlayer(deps)
    const deployed = await winningRoster()
    // rosterCap(1) is 20 - bible §7.2's floor, and design §3.3 pins every
    // Ark at Hatchery tier 1 this phase. Five are already on the roster.
    await giveRoster(asRosterSpecs(winningDeployment()))
    await giveRoster(asRosterSpecs(winningDeployment()))
    await giveRoster(asRosterSpecs(winningDeployment()))
    expect(await rosterCount()).toBe(20)

    const { issuanceId, seed } = await (await startWave(6, deployed)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'key-stock-5')

    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: 40 } })
    expect(await balance('shards')).toBe(250 + 40)
    // 21, not 20 - Task 8. `grantWaveBaseStock`'s OWN grant is skipped at
    // the cap, exactly as before; the wave-6 Pale is a SEPARATE grant that
    // deliberately checks no cap at all (ftue/pale.ts's own doc: refusing
    // it `roster_full` would teach the opposite of what the Wave Defeat
    // screen just told the player). So the cap holds against base stock and
    // does not hold against the one creature this beat requires landing.
    expect(await rosterCount()).toBe(21)
  })
})
