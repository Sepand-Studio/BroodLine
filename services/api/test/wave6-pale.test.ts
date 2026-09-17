import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { servers } from '../src/db/schema.ts'
import { readMarkers } from '../src/ftue/markers.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { baseStockPool } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  buildLosingReplay, buildWinningReplay, setupPlayer, startLosing, startWinning, submit,
  withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * Task 8 (Phase 7): the wave-6 Pale - "what this plan found the design
 * missed" item 1. waves_01_12 wave 6 is a DESIGNED LOSS (a single Courser
 * runs an empty lane, and the only counter is Chill), and its own Wave
 * Defeat screen hands over a Pale carrying Chill as a Warden resupply -
 * "If they somehow win, the Pale grant fires anyway."
 *
 * DRIVEN OVER REAL HTTP AGAINST A REAL SIM, the same discipline
 * wave-submit.test.ts and founder.test.ts hold - a LOSS here has to be one
 * the engine actually verified, not a fixture claiming to be one. See
 * services/api/README.md's "why the sim is not faked".
 *
 * BUNDLE 0.1.1, not 0.1.2: it authors ONLY wave 6 (config/bundles/0.1.1
 * carries no wave 7), so every fresh player's first-ever completion - win
 * or loss - is of wave 6, which is exactly the scenario this grant exists
 * for. wave-submit.test.ts's own `startWinning(6)` / `startLosing(6)` drive
 * the identical fixture.
 */

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')
const SERVER_ID = 1
// Distinct from every other file's sim port - test/preflight.ts's SIM_PORTS,
// which preflight.test.ts pins against this exact list.
const SIM_PORT = 5799
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

/** Starts the REAL sim service for this file - wave-submit.test.ts's dance, same reasoning. */
async function startSim(): Promise<{ stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-wave6-pale-sim-'))
  await withDotnetBuildLock(() => execFileSync('dotnet', [
    'build', 'services/sim/Broodline.Sim.Service.csproj', '-c', 'Debug', '-o', work, '--nologo',
  ], { cwd: REPO, stdio: 'inherit' }))
  const proc: ChildProcess = spawn('dotnet', [join(work, 'Broodline.Sim.Service.dll'), '--urls', SIM_URL],
    { cwd: REPO, stdio: 'ignore' })
  const unreap = reapOnExit(proc)

  let ready = false
  for (let i = 0; i < 80; i++) {
    try { if ((await fetch(`${SIM_URL}/healthz`)).ok) { ready = true; break } } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 250))
  }
  if (!ready) {
    proc.kill(); unreap()
    await rm(work, { recursive: true, force: true })
    throw new Error(`sim host never became ready on ${SIM_URL}`)
  }
  return { stop: async () => { proc.kill(); unreap(); await rm(work, { recursive: true, force: true }) } }
}

let t: TestDb
let deps: Deps
let sim: { stop: () => Promise<void> }
let bundleRoot: string

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave6-pale-bundle-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // Guarded, like every other file's - see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

interface SubmitBody {
  result: string
  reward?: { currency: string; amount: number }
  granted?: Array<{ species: string; trait1: string }>
}

describe('the wave-6 Pale', () => {
  it('a LOST wave 6 grants one Pale, once, and reports it', async () => {
    await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }

    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), randomUUID())
    expect(res.status).toBe(200)
    const body = await res.json() as SubmitBody
    expect(body.result).toBe('Loss')
    expect(body.reward).toBeUndefined()
    expect(body.granted?.map((c) => c.species)).toEqual(['Pale'])
    expect(body.granted?.[0]?.trait1).toBe('Chill')

    // A second loss for the SAME player grants nothing more - the marker is
    // set once, and wave 6 stays replayable forever because a loss never
    // advances campaign_progress (issueWave's forward branch, not its
    // replay-cap-gated one, is what a repeated wave-6 start takes).
    const again = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const r2 = await submit(again.issuanceId, buildLosingReplay(6, BigInt(again.seed)), randomUUID())
    expect(r2.status).toBe(200)
    expect(((await r2.json()) as SubmitBody).granted ?? []).toEqual([])
  })

  it('a WON wave 6 grants the Pale too - the beat is degraded, not broken', async () => {
    await setupPlayer(deps)
    // `startWinning` claims `winningDeployment()`, whose fifth creature
    // carries Chill (replay-format.ts) - the only trait that answers wave
    // 6's lone Courser, so this really is "somehow win" rather than an
    // impossible fixture.
    const { issuanceId, seed } = await (await startWinning(6)).json() as { issuanceId: string; seed: string }

    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), randomUUID())
    expect(res.status).toBe(200)
    const body = await res.json() as SubmitBody
    expect(body.result).toBe('Win')
    // Also this player's first-ever completion, so `granted` carries the
    // Founder (or a roll) ALONGSIDE the Pale - asserted by containment, not
    // equality, because which of those two also lands is Task 6's concern,
    // not this one's.
    expect(body.granted?.map((c) => c.species)).toContain('Pale')
    expect(body.granted?.find((c) => c.species === 'Pale')?.trait1).toBe('Chill')
  })

  it('after the grant, base stock can mint a Pale', async () => {
    const { playerId } = await setupPlayer(deps)
    const { issuanceId, seed } = await (await startLosing(6)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), randomUUID())
    expect(((await res.json()) as SubmitBody).result).toBe('Loss')

    const markers = await withServer(deps.db, SERVER_ID, (tx) => readMarkers(tx, SERVER_ID, playerId))
    expect(markers.wave6PaleGrantedAt).not.toBeNull()
    expect(baseStockPool(markers).some((s) => s.species === 'Pale')).toBe(true)
  })
})
