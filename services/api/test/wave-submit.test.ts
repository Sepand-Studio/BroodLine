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
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  balance, buildLosingReplay, buildWinningReplay, ledgerRowCount, liveIssuance,
  setupPlayer, startWave, submit, submitInit,
} from './wave-helpers.ts'

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, and .pathname percent-encodes it (wave-start.test.ts's
// own comment, carried forward - both files resolve the same repo root).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')
const SIM_PORT = 5299 // distinct from generate-contract.sh's 5199, so this file can run alongside contract.test.ts
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

/**
 * Starts the REAL sim service as a child process, once for this file.
 *
 * A stub standing in for sim would be a second implementation of the exact
 * boundary this phase exists to prove - see the brief. This mirrors
 * implementation/scripts/generate-contract.sh's own build-then-run dance
 * (`dotnet build` + `dotnet <dll>`, not `dotnet run`): that script found
 * `dotnet run` hangs indefinitely on this machine, while build-then-run
 * consistently serves /healthz within a second.
 */
async function startSim(): Promise<{ proc: ChildProcess; stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-sim-build-'))
  execFileSync('dotnet', [
    'build', 'services/sim/Broodline.Sim.Service.csproj', '-c', 'Debug', '-o', work, '--nologo',
  ], { cwd: REPO, stdio: 'inherit' })

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
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-submit-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  deps = { db: t.db, bundleStore: store, simClient: new SimClient(SIM_URL) }

  await setupPlayer(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/submit', () => {
  it('pays a winning submission exactly once', async () => {
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))

    const res = await submit(issuanceId, replay, 'key-1')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: 40 } })

    expect(await balance('shards')).toBe(250 + 40)
    expect(await ledgerRowCount()).toBe(3) // 2 from the starter grant, 1 from this
  })

  it('returns the stored response on a resend with the SAME key', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-2')
    const second = await submit(issuanceId, replay, 'key-2')

    expect(second.status).toBe(200)
    // Guard one: the idempotency key protects the RESPONSE.
    expect(await balance('shards')).toBe(before + 40)
  })

  it('pays nothing on a resend with a DIFFERENT key', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
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
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
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

    const [a, b] = await Promise.all([
      appA.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-race-a')),
      appB.request('/v1/wave/submit', submitInit(issuanceId, replay, 'key-race-b')),
    ])

    // Exactly one request wins (200, paid) and the other finds nothing left
    // to consume (409 issuance_invalid) - never both winning, which is the
    // double-credit this guard exists to prevent.
    const statuses = [a.status, b.status].sort()
    expect(statuses).toEqual([200, 409])
    expect(await balance('shards')).toBe(before + 40)
  })

  it('refuses a replay whose seed is not the issued one', async () => {
    await setupPlayer(deps)
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
    const replay = buildWinningReplay(6, 0xDEADBEEFn) // a seed nobody issued

    const res = await submit(issuanceId, replay, 'key-5')
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await liveIssuance()).toBeUndefined() // consumed, not re-usable
  })

  it('refuses a replay of a different wave against this issuance', async () => {
    // Step 5 and step 2 are DIFFERENT checks. The issuance proves the player
    // was given a wave; the seed proves this replay is of THAT wave.
    await setupPlayer(deps)
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }

    const res = await submit(issuanceId, buildWinningReplay(6, 1n), 'key-6')
    expect(res.status).toBe(409)
  })

  it('refuses a submission from a superseded engine and tells the client to update', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed), { engineVersion: '0.1.0' })

    const res = await submit(issuanceId, replay, 'key-7')
    expect(res.status).toBe(426)
    expect(await res.json()).toMatchObject({ code: 'engine_too_old' })
  })

  it('leaves the issuance live when sim is unreachable', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }

    const broken = createApp({ ...deps, simClient: new SimClient('http://127.0.0.1:1') })
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
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'key-9')
    const body = await res.json() as { result: string; reward?: unknown; breaches: unknown[] }

    expect(body.result).toBe('Loss')
    expect(body.reward).toBeUndefined()
    // combat_engine 7's three booleans, forwarded for the Wave Defeat
    // screen. api stores none of them and interprets none of them.
    expect(body.breaches[0]).toHaveProperty('access')
  })
})
