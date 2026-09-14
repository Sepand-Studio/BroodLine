import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  balance, buildLosingReplay, buildWinningReplay, setupPlayer, startWave, submit, submitInit,
  withDotnetBuildLock,
} from './wave-helpers.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded (wave-submit.test.ts's own comment, carried forward).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')
/**
 * The PREVIOUS published bundle, and the reason `wave_locked` is reachable
 * from the submit path at all.
 *
 * 0.1.0's waves.json authors wave 6 with NO `reward` field
 * (config/bundles/0.1.0/waves.json) - the field did not exist when it was
 * published - so `rewardForWave(bundle, 6)` returns null against it and the
 * handler's step-6 reward lookup refuses `wave_locked`. Pointing at it is
 * not a contrivance: config/store.ts's `setPointer` exists precisely so a
 * rollback is "a config change, not a deploy", and it does NOT re-validate
 * the bundle it names - only `publishBundle` validates. So an operator
 * rolling back to 0.1.0 while a player holds a live wave-6 issuance is the
 * real, supported operation that produces this state.
 */
const SEED_010 = join(REPO, 'config/bundles/0.1.0')
// serverId is always 1 in this file - the one server beforeAll creates.
const SERVER_ID = 1
const SIM_PORT = 5399 // distinct from contract.test.ts's 5199 and wave-submit.test.ts's 5299
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

// withDotnetBuildLock is imported from wave-helpers.ts - see that file's
// comment above it for the full account of why a lock exists here at all
// and why it is mkdir-based with pid-owned staleness reclaim rather than a
// plain directory-exists mutex. Mirrors wave-submit.test.ts's usage exactly.

/**
 * Starts the REAL sim service as a child process, once for this file.
 *
 * A stub standing in for sim would be a second implementation of the exact
 * boundary this phase exists to prove, and this task specifically needs
 * sim's real ACCEPT/REJECT split - a stub can't be trusted to reproduce it.
 * Mirrors wave-submit.test.ts's startSim() exactly.
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
  // not projected; see child-reaper.ts. Mirrors wave-submit.test.ts.
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
// Module-scoped so the wave_locked test can move the POINTER mid-test the
// way a rollback does, then put it back.
let bundleStore: LocalBundleStore
let sim: { proc: ChildProcess; stop: () => Promise<void> }

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])

  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-replays-bundle-'))
  bundleStore = new LocalBundleStore(bundleRoot)
  await publishBundle(bundleStore, SEED, '0.1.1')

  // `putBundle` DIRECTLY, not `publishBundle`, and the difference is the
  // whole point rather than a shortcut around a slow validator. 0.1.0
  // CANNOT be published today: Task 7 added `validateWaveRewards`
  // (src/config/validate.ts), which rejects any authored wave carrying no
  // reward, and 0.1.0's wave 6 carries none. It is nevertheless already
  // published - it predates that check, and config/bundle.ts records that
  // it "is already published to GCS and is never edited in place". So the
  // state this file needs is reached the same way production reaches it:
  // an immutable bundle that was published before the rule existed, still
  // sitting there, one `setPointer` away from being live again.
  await bundleStore.putBundle('0.1.0', SEED_010)

  await bundleStore.setPointer('0.1.1')
  clearBundleCache()

  // replayStore is per-test (see freshReplayStore below) - deps here is a
  // template, always shallow-copied with a fresh store before use, so one
  // test's writes are never visible to another's `list()` assertion.
  deps = {
    db: t.db, bundleStore, simClient: new SimClient(SIM_URL), replayStore: new LocalReplayStore(bundleRoot),
  }
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an
  // aborted beforeAll left this throwing ERR_INVALID_ARG_TYPE on top of the
  // real error and burying it. See wave-submit.test.ts's afterAll for the
  // full account, and masked-teardown.test.ts for the test.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/**
 * A fresh, isolated LocalReplayStore per test.
 *
 * The brief's own snippet shares one top-level `store` across all three
 * `it` blocks and asserts `store.list()` has an EXACT length (0, or
 * contains exactly one key) - that only holds if tests never share state,
 * and vitest runs the `it`s in a `describe` in declaration order against
 * shared module state, so a naive port of the snippet would make test 2's
 * "toHaveLength(0)" depend on test 1 running first and writing to a
 * DIFFERENT store than the one test 2 reads. Isolating the store per test
 * makes each assertion true regardless of ordering, and provably so: run
 * either test alone and it still passes.
 */
async function freshReplayStore(): Promise<{ store: LocalReplayStore; cleanup: () => Promise<void> }> {
  const root = await mkdtemp(join(tmpdir(), 'broodline-replays-store-'))
  return { store: new LocalReplayStore(root), cleanup: () => rm(root, { recursive: true, force: true }) }
}

describe('replay storage', () => {
  it('writes the replay after a verified submission', async () => {
    const { store, cleanup } = await freshReplayStore()
    try {
      const testDeps = { ...deps, replayStore: store }
      const { playerId } = await setupPlayer(testDeps)

      const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
      const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'r-1')
      expect(res.status).toBe(200)

      expect(await store.list()).toEqual([`replays/${SERVER_ID}/${playerId}/${issuanceId}.bin`])
    } finally {
      await cleanup()
    }
  })

  it('writes the replay after a verified LOSS, not only a win', async () => {
    // design 5.2's clause is "written after sim verifies", not "written on
    // a win" - a Loss is exactly as verified as a Win (it passes sim,
    // matchesIssuance and settle() identically; only reward computation
    // differs, and a Loss never reaches that code at all). Proven here the
    // same way the Win case is proven above, not left to code inspection.
    const { store, cleanup } = await freshReplayStore()
    try {
      const testDeps = { ...deps, replayStore: store }
      const { playerId } = await setupPlayer(testDeps)

      const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
      const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'r-4')
      expect(res.status).toBe(200)
      expect(await res.json()).toMatchObject({ result: 'Loss' })

      expect(await store.list()).toEqual([`replays/${SERVER_ID}/${playerId}/${issuanceId}.bin`])
    } finally {
      await cleanup()
    }
  })

  it('writes nothing for a rejected submission', async () => {
    const { store, cleanup } = await freshReplayStore()
    try {
      const testDeps = { ...deps, replayStore: store }
      await setupPlayer(testDeps)

      const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
      // A well-formed, winning replay, but for a seed nobody issued - sim
      // verifies it fine (it's internally consistent), and it is
      // matchesIssuance (routes/wave.ts step 5) that refuses it, exactly
      // as wave-submit.test.ts's 'refuses a replay whose seed is not the
      // issued one' proves independently.
      const res = await submit(issuanceId, buildWinningReplay(6, 0xBADn), 'r-2')
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'submission_rejected' })

      // Storage is not an attacker's write primitive - design 5.2.
      expect(await store.list()).toHaveLength(0)
    } finally {
      await cleanup()
    }
  })

  it('still pays when the replay write fails', async () => {
    // Sets up the shared `app`/`token` wave-helpers.ts drives, and the
    // player this test uses. deps.replayStore (a working LocalReplayStore)
    // is irrelevant here - this test never submits through the app that
    // uses it, only through app2 below.
    await setupPlayer(deps)

    // COUNTED, not merely thrown from - review finding. A failing store
    // that is never CALLED also never throws, so without this counter the
    // assertions below cannot tell "the swallow works" from "the write is
    // not reached on this path at all", and a future change that re-gated
    // the write away from here would leave this test green while the
    // property it is named for went unguarded.
    let puts = 0
    const failing = { put: async () => { puts += 1; throw new Error('gcs down') } }
    const app2 = createApp({ ...deps, replayStore: failing })
    const before = await balance('shards')

    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const res = await app2.request('/v1/wave/submit', submitInit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'r-3'))

    // A GCS failure after a successful credit must not roll back a
    // payment. A missing replay is a degraded viewer; a rolled-back
    // credit is a ledger defect - design 5.2. If the write were inside
    // withIdempotency's transaction instead, this 200/+40 would fail -
    // the throw above would abort the transaction and roll the credit
    // back with it, which is exactly the regression this test exists to
    // catch.
    expect(puts).toBe(1)
    expect(res.status).toBe(200)
    expect(await balance('shards')).toBe(before + 40)
  })

  it('writes the replay for a wave_locked refusal - a verified, settled Win answered 409', async () => {
    // DESIGN §5.2's AMENDMENT. The replay follows VERIFICATION, not the
    // response status. This is the one path where those differ: sim
    // verifies the bytes, they prove to be of the issued wave, settle()
    // consumes the issuance - and then the reward lookup comes back null
    // because the bundle moved under the issuance's two-hour TTL, so the
    // player is answered 409 for an attempt that was real and is now
    // spent. It is precisely the refusal a player cannot appeal without
    // the stored replay, which is why it is the one that most needs it.
    //
    // Note the campaign is NOT advanced here - routes/wave.ts checks the
    // reward BEFORE advanceCampaign for its own reasons (task-6-report
    // §8.3.1). The issuance settling is what makes this a spent attempt.
    const { store, cleanup } = await freshReplayStore()
    try {
      const testDeps = { ...deps, replayStore: store }
      const { playerId } = await setupPlayer(testDeps)

      // Issued under 0.1.1, where wave 6 pays 40 shards.
      const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }

      // THE ROLLBACK, mid-TTL. try/finally because bundleRoot and the
      // bundle-module cache are shared by every test in this file and
      // vitest runs them in declaration order in one worker - a pointer
      // left at 0.1.0 by a failing assertion would silently change what
      // every later test is running against.
      await bundleStore.setPointer('0.1.0')
      clearBundleCache()
      try {
        const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'r-5')

        // A Win, refused. Asserting the CODE and not only the 409: three
        // different refusals on this route return 409, and the two others
        // (issuance_invalid, submission_rejected) must NOT write a replay -
        // so a status-only assertion would stay green against a handler
        // that had stopped reaching the reward check at all.
        expect(res.status).toBe(409)
        expect(await res.json()).toMatchObject({ code: 'wave_locked' })

        expect(await store.list()).toEqual([`replays/${SERVER_ID}/${playerId}/${issuanceId}.bin`])
      } finally {
        await bundleStore.setPointer('0.1.1')
        clearBundleCache()
      }
    } finally {
      await cleanup()
    }
  })

  it('still answers 409 when the replay write fails on a wave_locked refusal', async () => {
    // The write moved ABOVE the refusal branch, so it now runs on a path
    // that has no payment to protect - and a throw there would escape to
    // app.ts's onError and turn this 409 into a 500. The swallow is what
    // stops that. Same shape as 'still pays when the replay write fails'
    // above, for the other half of the write's new reach.
    await setupPlayer(deps)

    // COUNTED, and this test is the reason the counter exists in both.
    // REVIEW FINDING, demonstrated rather than predicted: under the
    // write-moved-back-under-the-success-path mutation, its neighbour
    // `writes the replay for a wave_locked refusal` went RED and THIS TEST
    // PASSED - the failing store was simply never invoked, nothing threw,
    // and the handler returned its natural 409. It was proving only that a
    // 409 is a 409, rescued entirely by the neighbour. `puts` is what makes
    // it prove that the swallow is what produced the 409.
    let puts = 0
    const failing = { put: async () => { puts += 1; throw new Error('gcs down') } }
    const app2 = createApp({ ...deps, replayStore: failing })

    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }

    await bundleStore.setPointer('0.1.0')
    clearBundleCache()
    try {
      const res = await app2.request(
        '/v1/wave/submit', submitInit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'r-6'))

      // ORDER MATTERS: assert the write was ATTEMPTED before asserting what
      // the response was, so a failure reads as "the write never happened"
      // rather than as a status mismatch two lines further down.
      expect(puts).toBe(1)
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'wave_locked' })
    } finally {
      await bundleStore.setPointer('0.1.1')
      clearBundleCache()
    }
  })
})
