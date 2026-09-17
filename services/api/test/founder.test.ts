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
import { servers } from '../src/db/schema.ts'
import type { Markers } from '../src/ftue/markers.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { baseStockPool, speciesForSeed } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { coldOpenDeployment, wave2TrioDeployment } from './replay-format.ts'
import {
  buildReplayOf, type Deployed, setupPlayer, startWave, submit, withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * Task 6 (Phase 7): the Founder - granted on the first wave completion,
 * named by the player, with Pale withheld from base stock until wave 6's
 * grant fires. design §5 beats 3-4; "what this plan found the design
 * missed" items 1-2.
 *
 * DRIVEN OVER REAL HTTP AGAINST A REAL SIM, the same discipline
 * wave-submit.test.ts, loop.test.ts and their siblings hold: the whole point
 * of beat 3 is that the FIRST WIN, verified by the real engine, mints a
 * Founder rather than a roll. A test that inserted a Hollow directly or
 * mocked the submit path would prove nothing about the path a player
 * actually takes. See services/api/README.md's "why the sim is not faked"
 * for the fuller argument.
 *
 * BUNDLE 0.1.3, not 0.1.1/0.1.2: it is the bundle that authors starter.json's
 * cold-open pair AND waves 1 and 2 (config/bundles/0.1.3/waves.json) - the
 * two waves beats 3-5 are actually played on. Earlier bundles author neither.
 */

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.3')
const SERVER_ID = 1
// Distinct from every other file's sim port (see test/preflight.ts's
// SIM_PORTS, which preflight.test.ts pins against this exact list).
const SIM_PORT = 5699
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

/** Starts the REAL sim service for this file - wave-submit.test.ts's dance, same reasoning. */
async function startSim(): Promise<{ stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-founder-sim-'))
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
// A local app, driven directly (rather than through wave-helpers) for the
// two routes wave-helpers does not cover: GET /v1/roster and
// POST /v1/creature/name. wave-helpers' own `startWave`/`submit` are used
// for the wave routes and act against the SAME account, set by whichever
// setupPlayer() call this file made most recently - see its own header.
let app: ReturnType<typeof createApp>

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-founder-bundle-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.3')
  await store.setPointer('0.1.3')
  clearBundleCache()
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // Guarded, like every other file's: bundleRoot is assigned partway through
  // beforeAll, so an aborted beforeAll would otherwise throw a path
  // TypeError on top of the real error - see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

// --- Route drivers this file needs beyond wave-helpers'.

interface RosterCreature {
  creatureId: string
  species: string
  name: string | null
  isFounder: boolean
}

async function roster(token: string): Promise<RosterCreature[]> {
  const res = await app.request('/v1/roster', { headers: { authorization: `Bearer ${token}` } })
  const body = await res.json() as { creatures: RosterCreature[] }
  return body.creatures
}

async function nameCreature(token: string, creatureId: string, name: string): Promise<Response> {
  return app.request('/v1/creature/name', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      authorization: `Bearer ${token}`,
      'idempotency-key': randomUUID(),
    },
    body: JSON.stringify({ creatureId, name }),
  })
}

/**
 * The starter pair's own ids, read off the roster rather than assumed - the
 * ONLY thing account creation guarantees about them is that a Vetch and an
 * Ember exist (starter.json), not which order `POST /v1/account` inserted
 * them in or which order `GET /v1/roster` (ordered by creatureId) returns
 * them in.
 */
async function ownedPair(token: string): Promise<{ vetch: string; ember: string }> {
  const rows = await roster(token)
  const vetch = rows.find((r) => r.species === 'Vetch')
  const ember = rows.find((r) => r.species === 'Ember')
  if (vetch === undefined || ember === undefined) {
    throw new Error(`expected a starter Vetch and Ember on the roster; got ${JSON.stringify(rows)}`)
  }
  return { vetch: vetch.creatureId, ember: ember.creatureId }
}

// State threaded from the first test to the ones after it - beats 3, 4 and 5
// are one continuous arc for a single player, and driving them as one player
// across sequential `it`s is what makes "the SECOND completion" and "name a
// Founder" mean the same Founder the first test actually granted, rather
// than a stand-in inserted for each test's own convenience.
let arcToken: string
let vetchId: string
let emberId: string
let founderId: string

describe('the Founder', () => {
  it('the first wave completion grants a Hollow Founder instead of rolled stock', async () => {
    const { token } = await setupPlayer(deps) // 0.1.3: roster is Vetch + Ember
    arcToken = token
    const pair = await ownedPair(token)
    vetchId = pair.vetch
    emberId = pair.ember
    const deployment: Deployed[] = [{ creatureId: pair.vetch, pocket: 0 }, { creatureId: pair.ember, pocket: 2 }]

    const start = await startWave(1, deployment)
    expect(start.status).toBe(200)
    const { issuanceId, seed } = await start.json() as { issuanceId: string; seed: string }

    const sub = await submit(
      issuanceId, buildReplayOf(1, BigInt(seed), coldOpenDeployment()), randomUUID())
    expect(sub.status).toBe(200)
    expect(await sub.json()).toMatchObject({ result: 'Win' })

    const rows = await roster(token)
    expect(rows.length).toBe(3)
    const founder = rows.find((r) => r.isFounder)
    expect(founder?.species).toBe('Hollow')
    expect(founder?.name).toBeNull()
    founderId = founder!.creatureId
  }, 60_000)

  it('the SECOND completion grants rolled base stock, never a second Founder', async () => {
    const deployment: Deployed[] = [
      { creatureId: vetchId, pocket: 0 },
      { creatureId: emberId, pocket: 2 },
      { creatureId: founderId, pocket: 4 },
    ]

    const start = await startWave(2, deployment)
    expect(start.status).toBe(200)
    const { issuanceId, seed } = await start.json() as { issuanceId: string; seed: string }

    const sub = await submit(
      issuanceId, buildReplayOf(2, BigInt(seed), wave2TrioDeployment()), randomUUID())
    expect(sub.status).toBe(200)
    expect(await sub.json()).toMatchObject({ result: 'Win' })

    const rows = await roster(arcToken)
    expect(rows.filter((r) => r.isFounder).length).toBe(1)
    expect(rows.length).toBe(4)
  }, 60_000)

  it('POST /v1/creature/name names a Founder and refuses everything else', async () => {
    const ok = await nameCreature(arcToken, founderId, '  Ash  ')
    expect(ok.status).toBe(200)
    expect(((await ok.json()) as { name: string }).name).toBe('Ash') // trimmed

    // THE CODE, NOT JUST THE STATUS. 409 is shared by six refusals in
    // `errors.ts` - generation_ceiling, insufficient_charges,
    // ftue_stock_unavailable and the rest - so a status-only assertion holds
    // if this route starts refusing a non-Founder for an entirely different
    // reason, which is the regression worth catching on the route the client
    // now drives through the outbox.
    const notFounder = await nameCreature(arcToken, vetchId, 'Ash')
    expect(notFounder.status).toBe(409)
    expect((await notFounder.json() as { code: string }).code).toBe('not_a_founder')
    expect((await nameCreature(arcToken, founderId, '')).status).toBe(400)
    expect((await nameCreature(arcToken, founderId, 'x'.repeat(17))).status).toBe(400)
    expect((await nameCreature(arcToken, founderId, `Ash${String.fromCharCode(7)}`)).status).toBe(400) // a control character

    // Renameable at any time - bible 3.3 - so a second name is a 200, not a conflict.
    const renamed = await nameCreature(arcToken, founderId, 'Ember Ash')
    expect(renamed.status).toBe(200)
    expect(((await renamed.json()) as { name: string }).name).toBe('Ember Ash')
  })

  it('refuses a name with no Idempotency-Key, before it reads the body', async () => {
    // The header gate is the FIRST statement in the handler, ahead of the
    // body parse, and nothing covered it. It matters more since Task 18: the
    // client reaches this route through `OutboxClient`, whose whole design is
    // a key persisted at action time and replayed - so a build that stopped
    // sending the header would queue mutations this service silently accepted
    // once per retry rather than once per action.
    const res = await app.request('/v1/creature/name', {
      method: 'POST',
      headers: { 'content-type': 'application/json', authorization: `Bearer ${arcToken}` },
      body: JSON.stringify({ creatureId: founderId, name: 'Keyless' }),
    })

    expect(res.status).toBe(400)
    expect((await res.json() as { code: string }).code).toBe('invalid_request')

    // AND IT REFUSED BEFORE MUTATING. A gate that 400s after the update is
    // the same status with none of the protection.
    //
    // 'Ember Ash' IS STATE FROM THE PRECEDING `it`, which renamed the Founder.
    // This file has a `beforeAll` and no per-test reset, so its tests are
    // ordered by construction - the same reason `founderId` itself is set by
    // an earlier one. That makes the assertion STRONGER than a null check
    // would be (a non-default value that something already wrote is exactly
    // what §4 of the phase record says a fixture should be), and it means
    // this test cannot be run in isolation. Said here rather than worked
    // around, because the coupling is the file's existing shape.
    const unchanged = await roster(arcToken)
    expect(unchanged.find((r) => r.creatureId === founderId)?.name).toBe('Ember Ash')
  })

  it('base stock never mints a Pale before the wave-6 grant has fired', () => {
    const none: Markers = { founderGrantedAt: null, tutorialStockGrantedAt: null, wave6PaleGrantedAt: null }
    const before = baseStockPool(none)
    const after = baseStockPool({ ...none, wave6PaleGrantedAt: new Date() })

    for (let i = 0; i < 200; i++) expect(speciesForSeed(`s:${i}`, before).species).not.toBe('Pale')
    expect(Array.from({ length: 200 }, (_, i) => speciesForSeed(`s:${i}`, after).species)).toContain('Pale')
  })
})
