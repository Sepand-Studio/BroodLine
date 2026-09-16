import { randomUUID } from 'node:crypto'
import { cp, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, isNull, sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { creatureHp, lockRoster } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { commitSplice } from '../src/splice/commit.ts'
import { ISSUANCE_TTL_MS, issueWave } from '../src/wave/issuance.ts'
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

describe("wave/start's two-statement creature lock (fix round 1)", () => {
  /**
   * REPRODUCES A REAL POSTGRES DEADLOCK, then proves the fix closes it -
   * built by construction, not by reasoning about it (the same standing
   * instruction fix round 1's own analysis was held to, and the second time
   * on this branch that construction found what reasoning missed).
   *
   * THE SHAPE. `issueWave` now locks creatures in TWO statements:
   * `settleExpiredForPlayer` releases S1 (whatever this player's
   * live-past-expiry issuance committed - `hi` below), sorted; later,
   * `resolveDeployment` -> `loadOwnedCreatures` locks S2 (the NEW
   * deployment's own creatures - `lo` below), also sorted. Each statement
   * is internally ascending; the transaction's combined order across both
   * is not, whenever an S1 id sorts above an S2 id (exactly the case
   * constructed here: `lo < hi`). `splice/commit.ts`'s `commitSplice`
   * names `lo` and `hi` as its two parents and locks them - via
   * `lockParents` - in the ONE order every writer of `creatures` on this
   * branch agrees on: ascending, i.e. `lo` then `hi`. That is the OPPOSITE
   * of `issueWave`'s S1-then-S2 order for this exact pair, and a pure
   * row-lock inversion - no advisory lock is involved on either side if
   * `wave/start` never takes one, which is what made this deadlock
   * possible in a way `commitSplice`'s existing `lockRoster` (taken before
   * ITS OWN row locks, for an unrelated, earlier deadlock) could not
   * prevent by itself.
   *
   * THE FIX under test: `POST /v1/wave/start` (routes/wave.ts) now takes
   * `lockRoster` as the first statement in its transaction, before
   * `issueWave` runs. The general rule, worth restating here because this
   * branch has now paid for it twice: a transaction that locks a player's
   * creature rows in MORE THAN ONE STATEMENT must hold `lockRoster` first;
   * one that locks them in exactly one sorted statement needs no advisory
   * lock at all, because there is nothing for that one statement to invert
   * against.
   *
   * DETERMINISTIC, not timing-based, for the interesting half: `afterRelease`
   * (`IssuanceHooks`, forwarded through `settleExpiredForPlayer`) pauses TX1
   * in the EXACT window fix round 2's own `afterRelease` doc describes for
   * the submit path - `hi`'s row lock held, released, nothing past this
   * point run yet - which is the only place this race can be constructed
   * without hoping a `setTimeout` lands right. The second half (proving TX2
   * is genuinely BLOCKED, not merely slow) is the same 400ms real-time
   * check `splice-commit.test.ts`'s own "does not deadlock against a
   * wave-submit..." test uses, for the same reason: there is no hook inside
   * Postgres's lock manager to await instead.
   *
   * VERIFIED BOTH WAYS - task-11-report.md has the transcripts. With the
   * `lockRoster` line in routes/wave.ts's `/v1/wave/start` handler
   * (shipped): this test is GREEN, both transactions settle, `hi` is
   * genuinely released (queried directly below, not inferred), and TX2 is
   * refused `creature_committed` for an unrelated and CORRECT reason - `lo`
   * is now committed to the fresh issuance TX1's wave/start just minted, so
   * splicing it away is rightly refused. With that ONE `lockRoster` line
   * removed: this test FAILS - `Promise.all` rejects with a genuine
   * Postgres `deadlock detected` (`40P01`) thrown out of whichever side
   * lost the race, exactly as fix round 1 asked to have demonstrated rather
   * than assumed.
   */
  it('does not deadlock against a splice naming the creature it just released', async () => {
    const { playerId } = await setupPlayer(deps)
    const bundle = await loadBundle(deps.bundleStore)

    async function give(spec: { species: string; trait1: string; trait2: string }): Promise<string> {
      const [row] = await t.ownerDb.insert(creatures).values({
        serverId: SERVER_ID, playerId, species: spec.species, generation: 1,
        trait1: spec.trait1, tier1: 1, trait2: spec.trait2, tier2: 1,
        instinct: 'Vanguard', hpCurrent: creatureHp(spec.species), isFounder: false,
      }).returning()
      return row!.creatureId
    }

    // splice-commit.test.ts's own compatible pair (Vetch/Pale, Taunt/Chill
    // over Carapace) - a splice that can actually reach `kind: 'ok'`, not
    // merely a refusal that happens not to throw.
    const vetchId = await give({ species: 'Vetch', trait1: 'Taunt', trait2: 'Carapace' })
    const paleId = await give({ species: 'Pale', trait1: 'Chill', trait2: 'Carapace' })
    const [lo, hi] = vetchId < paleId ? [vetchId, paleId] : [paleId, vetchId]

    // `hi` is the stranded creature - committed to a stale, expired,
    // unsettled issuance, exactly what settleExpiredForPlayer exists to
    // clean up, and named the way check 4's own abandoned-wave path always
    // has been: settled_at IS NULL, expires_at in the past.
    const staleIssuanceId = randomUUID()
    await t.ownerDb.insert(waveIssuances).values({
      serverId: SERVER_ID, issuanceId: staleIssuanceId, playerId, waveId: 6, seed: '1',
      issuedAt: new Date(Date.now() - 3 * ISSUANCE_TTL_MS), expiresAt: new Date(Date.now() - 3_600_000),
      deployment: [],
    })
    await t.ownerDb.update(creatures).set({ committedTo: staleIssuanceId })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, hi)))

    let releaseTx1: () => void = () => {}
    const paused = new Promise<void>((res) => { releaseTx1 = res })
    let tx1ReachedTheWindow: () => void = () => {}
    const tx1IsInTheWindow = new Promise<void>((res) => { tx1ReachedTheWindow = res })

    // TX1 reproduces `routes/wave.ts`'s FIXED `/v1/wave/start` sequence
    // directly - `splice-commit.test.ts`'s own "does not deadlock against a
    // wave-submit..." test does the same thing for the same reason: hooks
    // are only reachable by driving the lower-level functions, and the
    // route itself carries no hook seam (nothing under src/routes/ ever
    // passes one). `lockRoster` first (the fix), THEN `issueWave`, whose
    // `settleExpiredForPlayer` releases `hi` and pauses, then (once
    // released below) `resolveDeployment` locks `lo` for THIS new
    // deployment. Wave 6 is authored by this file's bundle and this is a
    // fresh player (cleared 0), so it is the "next" wave - no clearWave
    // needed.
    let tx1Settled = false
    const tx1 = withServer(t.db, SERVER_ID, async (tx) => {
      await lockRoster(tx, SERVER_ID, playerId)
      return issueWave(
        tx, SERVER_ID, playerId, 6, [{ creatureId: lo, pocket: 0 }], bundle, {
          afterRelease: async () => { tx1ReachedTheWindow(); await paused },
        })
    }).then((r) => { tx1Settled = true; return r })

    await tx1IsInTheWindow

    // TX2: splice/commit's real shape - `lockRoster`, then `lockParents` in
    // ascending id order (`lo` then `hi`) - the SAME two rows TX1 touches,
    // in the OPPOSITE relative order TX1 acquires them in.
    let tx2Settled = false
    const tx2 = withServer(t.db, SERVER_ID, (tx) => commitSplice(
      tx, SERVER_ID, playerId, bundle,
      { parentA: vetchId, parentB: paleId, locked: { slot: 'trait_1', from: 'a' }, bodyFrom: 'Vetch' },
      new Date(), randomUUID()))
      .then((r) => { tx2Settled = true; return r })

    await new Promise((r) => setTimeout(r, 400))
    // TX2 has made no progress - correctly BLOCKED. WITH the fix, that is
    // `lockRoster` (TX1 holds it). WITHOUT it, TX2 sails past `lockRoster`
    // uncontested, locks `lo`, and blocks wanting `hi` instead - "not
    // settled" holds either way, which is exactly why this check alone
    // cannot distinguish the two; the deadlock (or its absence) only shows
    // up once TX1 is released, below.
    expect(tx2Settled).toBe(false)

    releaseTx1()
    const [r1, r2] = await Promise.all([tx1, tx2])

    // NEITHER transaction was aborted by the deadlock detector - which is
    // what the `Promise.all` above would have surfaced as a rejection
    // (a genuine Postgres `deadlock detected` thrown out of `lockParents`,
    // confirmed by hand with the fix removed - see task-11-report.md).
    expect(tx1Settled).toBe(true)
    expect(tx2Settled).toBe(true)
    expect('refused' in r1).toBe(false) // TX1's wave/start succeeded

    // `hi` was genuinely released by TX1, not merely "the transactions
    // didn't crash" - queried directly rather than inferred from TX2's
    // outcome, which turns out to be `creature_committed` for an entirely
    // different and CORRECT reason below.
    const [hiRow] = await t.ownerDb.select().from(creatures)
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, hi)))
    expect(hiRow!.committedTo).toBeNull()

    // TX2 is refused, and correctly so: `lo` - one of the splice's own two
    // parents - is now committed to the FRESH issuance TX1's wave/start
    // just minted (`lo` is what TX1 deployed). That is `commitSplice`
    // working as designed, not a residual of the race: a creature cannot be
    // spliced away while it is out fighting, and `lo` genuinely is, now.
    // `kind: 'ok'` would in fact be the WRONG outcome here - it would mean
    // `commitSplice` let a currently-deployed creature be spliced.
    expect(r2.kind).toBe('creature_committed')
  })

  /**
   * THE OTHER HALF OF THE EVIDENCE. The test above proves the MECHANISM -
   * that taking `lockRoster` before `issueWave` prevents the deadlock -
   * but it drives TX1 by manually reproducing `routes/wave.ts`'s sequence
   * (`lockRoster` then `issueWave`, `commitSplice.test.ts`'s own established
   * idiom for reaching a hook), so it stays GREEN even if the `lockRoster`
   * line were deleted from the ACTUAL route: confirmed by hand, deleting it
   * from `routes/wave.ts` and re-running the test above leaves it green,
   * because it never calls the route at all.
   *
   * This test closes that gap by driving the REAL route over HTTP
   * (`startWave`, wave-helpers' own driver) and checking the one thing that
   * distinguishes "takes the lock" from "does not": a concurrent holder of
   * the SAME advisory lock blocks it. No deadlock needed for this half -
   * just the lock itself, held deterministically (a manual promise gate,
   * not a timing guess) by a transaction that does nothing else.
   */
  it('POST /v1/wave/start blocks on lockRoster while another transaction for this player holds it', async () => {
    const { playerId } = await setupPlayer(deps)
    const lo = await (async () => {
      const [row] = await t.ownerDb.insert(creatures).values({
        serverId: SERVER_ID, playerId, species: 'Vetch', generation: 1,
        trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1,
        instinct: 'Vanguard', hpCurrent: creatureHp('Vetch'), isFounder: false,
      }).returning()
      return row!.creatureId
    })()

    let releaseHolder: () => void = () => {}
    const holderPaused = new Promise<void>((res) => { releaseHolder = res })
    let holderHasTheLock: () => void = () => {}
    const holderGotTheLock = new Promise<void>((res) => { holderHasTheLock = res })

    // Holds `lockRoster` and nothing else - not even a read of `creatures` -
    // so there is nothing here for the route's OWN row locks to contend
    // with. If the route blocks, it can only be on this advisory lock.
    const holder = withServer(t.db, SERVER_ID, async (tx) => {
      await lockRoster(tx, SERVER_ID, playerId)
      holderHasTheLock()
      await holderPaused
    })

    await holderGotTheLock

    let requestSettled = false
    const req = startWave(6, [{ creatureId: lo, pocket: 0 }])
      .then((res) => { requestSettled = true; return res })

    // try/finally: `holder` pauses on a promise nothing else resolves, so a
    // failed assertion between here and `releaseHolder()` must still
    // release it - otherwise the held advisory lock and open transaction
    // outlive this test and hang the file's teardown (measured: a genuine
    // 120s hook timeout on `t.stop()`, the first time this test's own
    // assertion below was made to fail on purpose for the RED run).
    try {
      await new Promise((r) => setTimeout(r, 400))
      // WITH the fix: still blocked on `lockRoster`, held by `holder`.
      // WITHOUT it (verified by hand, task-11-report.md): this is already
      // `true` here - nothing in the route asks for the lock, so there is
      // nothing to block on.
      expect(requestSettled).toBe(false)
    } finally {
      releaseHolder()
      await holder
    }

    const res = await req
    expect(res.status).toBe(200)
  })
})
