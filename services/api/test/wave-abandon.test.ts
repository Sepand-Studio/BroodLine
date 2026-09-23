import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  abandon, type Deployed, liveIssuance, setupPlayer, startWave as start, winningRoster,
} from './wave-helpers.ts'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
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
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-abandon-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()
  deps = {
    db: t.db, bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('abandon never calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/abandon', () => {
  let mine: Deployed[]
  let token: string

  beforeEach(async () => {
    const p = await setupPlayer(deps)
    token = p.token
    mine = await winningRoster()
  })

  async function committedTo(ids: string[]): Promise<Map<string, string | null>> {
    const r = await app.request('/v1/roster', { headers: { authorization: `Bearer ${token}` } })
    expect(r.status).toBe(200)
    const body = await r.json() as { creatures: { creatureId: string; committedTo: string | null }[] }
    const m = new Map<string, string | null>()
    for (const c of body.creatures) if (ids.includes(c.creatureId)) m.set(c.creatureId, c.committedTo)
    return m
  }

  it('settles the live issuance as expired and releases every deployed creature', async () => {
    const deployed = mine.slice(0, 2)
    const res = await start(6, deployed)
    expect(res.status).toBe(200)
    const { issuanceId } = await res.json() as { issuanceId: string }
    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBe(issuanceId)

    const ab = await abandon()
    expect(ab.status).toBe(200)
    expect(await ab.json()).toEqual({ settled: true })

    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBeNull()
    expect(await liveIssuance()).toBeUndefined()
    const [row] = await t.ownerDb.select().from(waveIssuances)
      .where(eq(waveIssuances.issuanceId, issuanceId))
    expect(row?.settlement).toBe('expired')
  })

  it('is a no-op with nothing live', async () => {
    const ab = await abandon()
    expect(ab.status).toBe(200)
    expect(await ab.json()).toEqual({ settled: false })
  })

  it('does not double-settle: the second call answers settled:false', async () => {
    await start(6, mine.slice(0, 2))
    expect(await (await abandon()).json()).toEqual({ settled: true })
    expect(await (await abandon()).json()).toEqual({ settled: false })
  })

  it('settles a live issuance that is already past expiry, because that is the case the client hits', async () => {
    const deployed = mine.slice(0, 2)
    const res = await start(6, deployed)
    const { issuanceId } = await res.json() as { issuanceId: string }
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${issuanceId}`)

    expect(await (await abandon()).json()).toEqual({ settled: true })
    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBeNull()
  })

  it('needs a session', async () => {
    const r = await app.request('/v1/wave/abandon', { method: 'POST' })
    expect(r.status).toBe(401)
  })
})

describe('GET /v1/roster heals', () => {
  it('releases creatures committed to an issuance past its expiry, with no abandon call', async () => {
    const p = await setupPlayer(deps)
    const mine = await winningRoster()
    const deployed = mine.slice(0, 2)
    const res = await start(6, deployed)
    const { issuanceId } = await res.json() as { issuanceId: string }
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${issuanceId}`)

    const r = await app.request('/v1/roster', { headers: { authorization: `Bearer ${p.token}` } })
    expect(r.status).toBe(200)
    const body = await r.json() as { creatures: { creatureId: string; committedTo: string | null }[] }
    for (const c of body.creatures) expect(c.committedTo).toBeNull()
    expect(await liveIssuance()).toBeUndefined()
  })
})
