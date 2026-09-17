import { and, eq } from 'drizzle-orm'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import type { Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { campaignProgress, servers } from '../src/db/schema.ts'
import { markersFrom, readMarkers, setMarker } from '../src/ftue/markers.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { setupPlayer } from './wave-helpers.ts'

/**
 * `src/ftue/markers.ts` - Phase 7, Task 4.
 *
 * Three write-once instants on `campaign_progress` back the FTUE's
 * exactly-once grants (Founder, tutorial stock, the wave-6 Pale). NOTHING
 * READS THEM YET - the grant paths land in Tasks 6-8. This file pins the
 * storage and the accessor only: that `setMarker` reports whether THIS call
 * performed the write (the `settle()` shape - see wave/issuance.ts), and
 * that it can write a marker for a player with no `campaign_progress` row
 * yet, since the Founder grant fires ON the first wave clear, before
 * `advanceCampaign` has written anything.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.2')

let t: TestDb
let deps: Deps
let bundleRoot: string
let playerId: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', ...TICK,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-ftue-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim or submits a replay - node-claim.test.ts's
  // idiom, reused because setupPlayer only needs a working /v1/account.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an aborted
  // beforeAll would otherwise throw on top of the real error and bury it -
  // see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

beforeEach(async () => {
  const p = await setupPlayer(deps)
  playerId = p.playerId
})

describe('ftue markers', () => {
  it('setMarker is write-once and reports whether THIS call wrote it', async () => {
    const first = await withServer(t.db, SERVER_ID, (tx) => setMarker(tx, SERVER_ID, playerId, 'founder_granted_at'))
    const second = await withServer(t.db, SERVER_ID, (tx) => setMarker(tx, SERVER_ID, playerId, 'founder_granted_at'))
    expect(first).toBe(true)
    expect(second).toBe(false)
    const m = await withServer(t.db, SERVER_ID, (tx) => readMarkers(tx, SERVER_ID, playerId))
    expect(m.founderGrantedAt).not.toBeNull()
    expect(m.tutorialStockGrantedAt).toBeNull()
  })

  it('creates the campaign_progress row if the player has none yet', async () => {
    // A brand-new player has no progress row until their first clear, and the
    // Founder grant fires ON that first clear - so the marker write must not
    // depend on a row advanceCampaign has not written yet.
    const fresh = await setupPlayer(deps)
    const before = await withServer(t.db, SERVER_ID, (tx) => tx.select().from(campaignProgress)
      .where(and(eq(campaignProgress.serverId, SERVER_ID), eq(campaignProgress.playerId, fresh.playerId))))
    expect(before.length).toBe(0)
    expect(await withServer(t.db, SERVER_ID, (tx) => setMarker(tx, SERVER_ID, fresh.playerId, 'wave6_pale_granted_at'))).toBe(true)
    const after = await withServer(t.db, SERVER_ID, (tx) => tx.select().from(campaignProgress)
      .where(and(eq(campaignProgress.serverId, SERVER_ID), eq(campaignProgress.playerId, fresh.playerId))))
    expect(after.length).toBe(1)
    expect(after[0]!.highestWaveCleared).toBe(0)           // the insert took the column defaults
    expect(after[0]!.wave6PaleGrantedAt).not.toBeNull()
  })

  // Deferred from Task 4's review: readMarkers' no-row case (a player with no
  // campaign_progress row yet) was correct by inspection - `row?.x ?? null`
  // - but never asserted. markersFrom being a pure function over an
  // already-fetched row (Task 5's fix round) makes that a one-line test with
  // no database involved at all.
  it('markersFrom(undefined) returns all three markers null', () => {
    expect(markersFrom(undefined)).toEqual({
      founderGrantedAt: null,
      tutorialStockGrantedAt: null,
      wave6PaleGrantedAt: null,
    })
  })
})
