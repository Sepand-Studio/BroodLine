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
import { startTestDb, type TestDb } from './harness.ts'
import {
  clearWave, consumeLiveIssuance, setupPlayer, startWave as start,
} from './wave-helpers.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.1, not 0.1.0: wave 6 carries no reward in 0.1.0, and rewardForWave has
// nothing to read there - see config/bundles/0.1.1 and the human ruling in
// task-5-brief.md Step 3 (0.1.0 is already published to GCS and must stay
// byte-identical to what shipped in Phase 4).
const SEED = join(REPO, 'config/bundles/0.1.1')

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-start-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  deps = { db: t.db, bundleStore: store }
  app = createApp(deps)

  await setupPlayer(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/start', () => {
  it('issues a seed for the next uncleared wave', async () => {
    const res = await start(6)
    expect(res.status).toBe(200)
    const body = await res.json() as { issuanceId: string; seed: string; expiresAt: string }

    expect(body.issuanceId).toMatch(/^[0-9a-f-]{36}$/)
    // A STRING. The seed is a ulong; a JSON number loses the top bits and
    // the client would re-simulate against a different seed than the server
    // stored - which presents as a hash mismatch on an honest submission,
    // the single most misleading failure this phase could ship.
    expect(typeof body.seed).toBe('string')
    expect(new Date(body.expiresAt).getTime() - Date.now()).toBeGreaterThan(7_100_000)
  })

  it('returns the SAME issuance rather than minting a second', async () => {
    const first = await (await start(6)).json() as { issuanceId: string; seed: string }
    const second = await (await start(6)).json() as { issuanceId: string; seed: string }

    // design 2.1. Determinism is what makes verification cheap; it is also
    // what makes seed-shopping cheap, and this is the defence.
    expect(second.issuanceId).toBe(first.issuanceId)
    expect(second.seed).toBe(first.seed)
  })

  it('refuses a wave beyond the next one', async () => {
    const res = await start(20)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a wave absent from the bundle', async () => {
    // The bundle carries wave 6 only. A wave id the content does not define
    // is wave_locked, not a 500 - the bundle is the content source, per
    // solo_execution 5.2's content-versus-data split.
    const res = await start(1)
    expect([409]).toContain(res.status)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a fourth replay of a cleared wave in one day', async () => {
    await clearWave(6) // helper: consume any live issuance and advance progress

    for (let i = 0; i < 3; i++) {
      expect((await start(6)).status).toBe(200)
      await consumeLiveIssuance()
    }

    // broodline_campaign_structure.md: three replays per wave per day, then
    // nothing until tomorrow. WITHOUT THIS CHECK wave 1 is farmable
    // indefinitely and re-simulation never notices, because every one of
    // those runs is honest - design 4.1 check 2.
    const res = await start(6)
    expect(res.status).toBe(429)
    expect(await res.json()).toMatchObject({ code: 'replay_cap_reached' })
  })

  it('refuses without a session', async () => {
    const res = await app.request('/v1/wave/start', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ waveId: 6 }),
    })
    expect(res.status).toBe(401)
  })

  it('translates a concurrent insert collision into the same issuance, not a 500', async () => {
    // A fresh player, run last: setupPlayer() reassigns wave-helpers'
    // module-level app/token/playerId, and every earlier test in this file
    // has already finished using the original one.
    //
    // With no live issuance, both racing calls find `live === undefined`
    // and both reach the INSERT. wave_issuances_one_live is what makes that
    // safe - the loser gets 23505 here, not a second seed, and Task 4's
    // carried-forward concurrency note is what issueWave must do with it:
    // translate the collision into "return the row the winner just wrote"
    // rather than a 500.
    await setupPlayer(deps)
    const [a, b] = await Promise.all([start(6), start(6)])

    expect(a.status).toBe(200)
    expect(b.status).toBe(200)
    const bodyA = await a.json() as { issuanceId: string; seed: string }
    const bodyB = await b.json() as { issuanceId: string; seed: string }
    expect(bodyA.issuanceId).toBe(bodyB.issuanceId)
    expect(bodyA.seed).toBe(bodyB.seed)
  })
})
