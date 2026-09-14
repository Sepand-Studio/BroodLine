import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.1, not 0.1.0: wave 6 carries no reward in 0.1.0, and Task 7 makes a
// missing reward a publish-time validation failure - 0.1.0 is already
// published to GCS and must stay byte-identical to what shipped in Phase 4.
const SEED = join(REPO, 'config/bundles/0.1.1')

let t: TestDb
let app: ReturnType<typeof createApp>
let bundleRoot: string
let token: string
let playerId: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-sync-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  app = createApp({
    db: t.db, bundleStore: store, simClient: new SimClient('http://127.0.0.1:1'),
    replayStore: new LocalReplayStore(bundleRoot), // this file never submits a wave
  })

  // Create a real player through the real route, so sync reads what the
  // grant actually wrote rather than a fixture shaped like it.
  const res = await app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': 'sync-setup' },
    body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
  })
  const body = await res.json() as { accountId: string; playerId: string; accessToken: string }
  token = body.accessToken
  playerId = body.playerId
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

const sync = (headers: Record<string, string> = {}) =>
  app.request('/v1/sync', { headers: { authorization: `Bearer ${token}`, ...headers } })

describe('GET /v1/sync', () => {
  it('returns the whole cold start in one call', async () => {
    const res = await sync()
    expect(res.status).toBe(200)

    const body = await res.json() as {
      player: { playerId: string; serverId: number }
      balances: Record<string, number>
      campaign: { highestWaveCleared: number }
      config: { bundleVersion: string; minimumClientVersion: string }
    }

    expect(body.player.playerId).toBe(playerId)
    expect(body.player.serverId).toBe(1)
    // Read from wallets, never summed from the ledger - solo_execution 5.3.
    expect(body.balances).toEqual({ splice_charges: 3, shards: 250 })
    expect(body.campaign.highestWaveCleared).toBe(0)
    expect(body.config.bundleVersion).toBe('0.1.1')
    expect(body.config.minimumClientVersion).toBe('0.1.0')
  })

  it('refuses a request with no token', async () => {
    const res = await app.request('/v1/sync')
    expect(res.status).toBe(401)
    expect(await res.json()).toMatchObject({ code: 'unauthorized' })
  })

  it('refuses a token this server did not sign', async () => {
    const res = await app.request('/v1/sync', { headers: { authorization: 'Bearer not-a-token' } })
    expect(res.status).toBe(401)
  })

  it('tells a client below the floor to update, rather than serving it', async () => {
    // minimumClientVersion ships from day one because, added after players
    // exist, the players who most need to upgrade are running the build that
    // cannot be told to - solo_execution 6.2.
    const res = await sync({ 'x-client-version': '0.0.1' })
    expect(res.status).toBe(426)
    expect(await res.json()).toMatchObject({ code: 'client_too_old' })
  })

  it('serves a client at or above the floor', async () => {
    expect((await sync({ 'x-client-version': '0.1.0' })).status).toBe(200)
    expect((await sync({ 'x-client-version': '1.4.2' })).status).toBe(200)
  })
})
