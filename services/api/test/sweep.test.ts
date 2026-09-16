import { randomUUID } from 'node:crypto'
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, isNull, sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { ISSUANCE_TTL_MS } from '../src/wave/issuance.ts'
import { sweepRetention } from '../src/wave/sweep.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { CREATURE_HP, type RosterSpec, SPECIES } from './replay-format.ts'
import { clearWave, giveRoster, setupPlayer, startWave } from './wave-helpers.ts'

/**
 * Task 11, design §10.2. Two things this file exists to close.
 *
 * FIRST - the "a wave_locked refusal no longer strands creatures..." test
 * below: `issueWave`'s checks 1-3 used to be able to refuse a start WITHOUT
 * ever reaching check 4, which was the only place a live-but-expired
 * issuance got settled. A player whose wave becomes unavailable (a bundle
 * rollback un-authoring it, exercised here) was left with creatures
 * `committed_to` a row nothing would ever settle - every later splice of
 * them refused `creature_committed` forever.
 *
 * SECOND - `weakenings.md` row 7's CONTROLLER RULING books design §4.3's
 * retention split (`'expired'` rows one hour past `expires_at`, `'consumed'`
 * rows 48 hours past `issued_at`) as owed, precisely because the sweep did
 * not exist to have a test written against it. `sweepRetention` below is
 * that test's subject.
 */

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.2, NOT 0.1.3: it is the earliest bundle that authors wave 7 (alongside
// wave 6), and - unlike 0.1.3 - it predates starter.json's cold-open pair
// (config/bundle.ts's Bundle.starterCreatures doc: "bundles 0.1.0-0.1.2
// predate the field"). The first test below asserts every creature on the
// roster is committed after a five-creature deployment; a starter pair
// granted at account creation would sit there uncommitted and make that
// assertion false for a reason that has nothing to do with this task.
const SEED = join(REPO, 'config/bundles/0.1.2')
const SERVER_ID = 1

let t: TestDb
let deps: Deps
let bundleRoot: string
let store: LocalBundleStore

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-sweep-'))
  store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // Neither test in this file ever calls POST /v1/wave/submit - wave-start.
  // test.ts's own reasoning applies: simClient points nowhere reachable
  // rather than standing up a real sim host nothing here needs.
  deps = {
    db: t.db, bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('sweep.test.ts never calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  // wave-helpers' setupPlayer() creates its own app from these deps - see
  // its own header for why every test file shares this one definition.
  createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an
  // aborted beforeAll would otherwise throw a path TypeError on top of the
  // real error - wave-submit.test.ts's afterAll note, same reasoning.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/** The same five loop.test.ts mints to beat wave 7 - only the shape matters here, never the win. */
function wave7RosterSpecs(): RosterSpec[] {
  return [
    { species: 'Vetch', trait1: 'Taunt', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 0, hp: CREATURE_HP[SPECIES.Vetch]! },
    { species: 'Ember', trait1: 'Splash', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 1, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: 'Ember', trait1: 'Splash', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 2, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: 'Hollow', trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 3, hp: CREATURE_HP[SPECIES.Hollow]! },
    { species: 'Hollow', trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 4, hp: CREATURE_HP[SPECIES.Hollow]! },
  ]
}

/**
 * Publishes a fresh version of 0.1.2 with wave 7 stripped out of waves.json
 * (wave 6 stays), and makes it the active one - standing in for "a bundle
 * rollback un-authored the wave a player has an issuance for" (design
 * §10.2).
 *
 * A NEW VERSION STRING, not a re-publish of '0.1.2': `LocalBundleStore`
 * bundles are immutable (`putBundle` refuses a version that already
 * exists), the same discipline `publishBundle`'s own doc describes.
 */
async function publishBundleWithoutWave7(): Promise<void> {
  const dir = await mkdtemp(join(tmpdir(), 'broodline-sweep-no-wave-7-'))
  try {
    await cp(SEED, dir, { recursive: true })
    const version = '0.1.4'

    const manifest = JSON.parse(await readFile(join(dir, 'manifest.json'), 'utf8')) as Record<string, unknown>
    manifest.version = version
    await writeFile(join(dir, 'manifest.json'), JSON.stringify(manifest), 'utf8')

    const waves = JSON.parse(await readFile(join(dir, 'waves.json'), 'utf8')) as Array<{ id: number }>
    await writeFile(join(dir, 'waves.json'), JSON.stringify(waves.filter((w) => w.id !== 7)), 'utf8')

    await publishBundle(store, dir, version)
    await store.setPointer(version)
    clearBundleCache()
  } finally {
    await rm(dir, { recursive: true, force: true })
  }
}

describe('issueWave releases a stranded deployment before refusing (Task 11)', () => {
  it('a wave_locked refusal no longer strands creatures committed to an expired issuance', async () => {
    const { playerId } = await setupPlayer(deps)
    // cleared=6 makes 7 the "next" wave (the smallest authored id past what
    // is cleared) - a fresh player (cleared=0) would be refused wave_locked
    // on wave 7 for an unrelated reason, since 1 is authored and comes first.
    await clearWave(6)

    // Seeded on purpose: this test is about committed_to, not the supply line.
    const five = await giveRoster(wave7RosterSpecs())

    expect((await startWave(7, five)).status).toBe(200)

    const myRoster = () => t.ownerDb.select().from(creatures)
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.playerId, playerId)))

    expect((await myRoster()).every((c) => c.committedTo !== null)).toBe(true)

    // Age the live issuance past its expiry by hand - standing in for "two
    // hours passed", wave-start.test.ts's own "settles an abandoned...
    // issuance" test does the same thing the same way.
    await withServer(t.db, SERVER_ID, (tx) => tx.update(waveIssuances)
      .set({ expiresAt: new Date(Date.now() - 60_000) })
      .where(and(
        eq(waveIssuances.serverId, SERVER_ID),
        eq(waveIssuances.playerId, playerId),
        isNull(waveIssuances.settledAt))))

    // The bundle rollback design §10.2 names: wave 7 stops being authored
    // while this player's issuance for it is still (abandoned-)live.
    await publishBundleWithoutWave7()

    const res = await startWave(7, five)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })

    // THE ASSERTION WITH TEETH. Before this task, a refusal at check 1
    // (wave_locked, exactly what fires here once wave 7 is unauthored)
    // never reached check 4 - the only place that used to settle an
    // expired live issuance - so these five stayed committed_to a row
    // nothing would ever settle. settleExpiredForPlayer now runs before
    // check 1, so the refusal above still released them.
    expect((await myRoster()).every((c) => c.committedTo === null)).toBe(true)
  })
})

describe('sweepRetention (design §4.3)', () => {
  it('retention: expired rows go one hour past expires_at; consumed rows go 48h past issued_at', async () => {
    const { playerId } = await setupPlayer(deps)

    /** A wave_issuances row written directly through the OWNER connection, with full control over its timestamps. */
    async function insertIssuance(o: {
      settlement: 'consumed' | 'expired' | null
      issuedAt?: string
      expiresAt?: string
    }): Promise<string> {
      const issuanceId = randomUUID()
      const issuedAt = o.issuedAt !== undefined
        ? new Date(o.issuedAt)
        : new Date(new Date(o.expiresAt!).getTime() - ISSUANCE_TTL_MS)
      const expiresAt = o.expiresAt !== undefined
        ? new Date(o.expiresAt)
        : new Date(issuedAt.getTime() + ISSUANCE_TTL_MS)
      await t.ownerDb.insert(waveIssuances).values({
        serverId: SERVER_ID,
        issuanceId,
        playerId,
        waveId: 7,
        seed: '1',
        issuedAt,
        expiresAt,
        // The write-once CHECK (0003): settled_at IS NULL iff settlement IS
        // NULL. The exact settled_at instant does not matter to either
        // sweep query below (only issued_at/expires_at do) - it only has to
        // be non-null exactly when settlement is.
        settledAt: o.settlement === null ? null : issuedAt,
        settlement: o.settlement,
        deployment: [],
      })
      return issuanceId
    }

    /**
     * Consumed rows currently on record for this player+wave - what a
     * check-2-style count sees once retention has run. Deliberately NOT a
     * reproduction of check-2's own UTC-day-boundary predicate
     * (issuance.ts's, pinned separately by adversarial.test.ts): this test
     * is about the SWEEP keeping a row long enough to be countable at all,
     * not about the day-boundary arithmetic layered on top of it in
     * production.
     */
    async function replayCapCountFor(forPlayerId: string, waveId: number): Promise<number> {
      const [row] = await t.ownerDb.select({ used: sql<number>`count(*)::int` })
        .from(waveIssuances)
        .where(and(
          eq(waveIssuances.serverId, SERVER_ID),
          eq(waveIssuances.playerId, forPlayerId),
          eq(waveIssuances.waveId, waveId),
          eq(waveIssuances.settlement, 'consumed')))
      return row!.used
    }

    const now = new Date('2026-09-15T00:10:00Z')
    await insertIssuance({ settlement: 'consumed', issuedAt: '2026-09-14T23:50:00Z' }) // kept
    await insertIssuance({ settlement: 'consumed', issuedAt: '2026-09-12T23:50:00Z' }) // deleted (>48h)
    await insertIssuance({ settlement: 'expired', expiresAt: '2026-09-14T22:00:00Z' }) // deleted (>1h past)
    await insertIssuance({ settlement: null, expiresAt: '2026-09-14T22:00:00Z' }) // live-past-expiry: SETTLED then deleted

    const r = await sweepRetention(t.ownerDb, SERVER_ID, now)
    expect(r).toEqual({ expiredDeleted: 2, consumedDeleted: 1 })
    expect(await replayCapCountFor(playerId, 7)).toBe(1) // the 23:50 row still counts
  })

  /**
   * task-11-report.md's Step 4 finding: the pair above (23:50 issued,
   * checked at 00:10 - twenty minutes later) does NOT actually discriminate
   * 24h from 48h. Twenty minutes is nowhere near either threshold, so BOTH
   * windows keep that row and weakening CONSUMED_RETENTION_MS to 24h leaves
   * the test above green. A row aged BETWEEN the two windows is what
   * distinguishes them, and this is that row - kept at the shipped 48h,
   * verified by hand to be DELETED if CONSUMED_RETENTION_MS is ever
   * weakened back to 24h (task-11-report.md's weakening table).
   */
  it('a consumed row strictly between 24h and 48h old survives the 48h window', async () => {
    const { playerId } = await setupPlayer(deps)
    const now = new Date('2026-09-15T00:10:00Z')
    const issuedAt30h = new Date(now.getTime() - 30 * 3_600_000)

    await t.ownerDb.insert(waveIssuances).values({
      serverId: SERVER_ID, issuanceId: randomUUID(), playerId, waveId: 7, seed: '1',
      issuedAt: issuedAt30h, expiresAt: new Date(issuedAt30h.getTime() + ISSUANCE_TTL_MS),
      settledAt: issuedAt30h, settlement: 'consumed', deployment: [],
    })

    expect(await sweepRetention(t.ownerDb, SERVER_ID, now)).toEqual({ expiredDeleted: 0, consumedDeleted: 0 })
  })
})
