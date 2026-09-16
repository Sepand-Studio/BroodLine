import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers } from '../src/db/schema.ts'
import { readMarkers } from '../src/ftue/markers.ts'
import { grantTutorialStock } from '../src/ftue/stock.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { creatureHp } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { commitSplice } from '../src/splice/commit.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { clearWave, setupPlayer } from './wave-helpers.ts'

/**
 * `POST /v1/ftue/splice-stock` - design §5 beats 6-7, `splice_confirm_spec` §6.
 *
 * The guided splice's provided pair, "provided specifically for the tutorial
 * and framed as sample stock" - never anything the player earned and never
 * the Founder they just named. `ftue/stock.ts`'s `grantTutorialStock` gates it
 * on three facts, none of which a client can fabricate: wave 2 cleared, this
 * player's first splice not yet spent, and the write-once marker not yet set.
 * NO CAP CHECK is deliberate there; this file confirms the grant fires anyway,
 * not the design reasoning for it.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, which .pathname percent-encodes.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.2')

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundleRoot: string
let playerId: string
let token: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', ...TICK,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-ftue-stock-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim - node-claim.test.ts's idiom, reused
  // because setupPlayer only needs a working /v1/account and the splice
  // fixture below only needs /v1/splice/commit.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an aborted
  // beforeAll would otherwise throw on top of the real error and bury it -
  // see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/** A fresh player per test - nothing here should depend on file order. */
beforeEach(async () => {
  ({ playerId, token } = await setupPlayer(deps))
})

/** Body-less, exactly as `ftue/stock.ts`'s grant needs nothing from the caller. */
async function stock(key: string = randomUUID()): Promise<Response> {
  return app.request('/v1/ftue/splice-stock', {
    method: 'POST',
    headers: { authorization: `Bearer ${token}`, 'idempotency-key': key },
  })
}

/** A live, ordinary base-stock-shaped creature, minted through the owner role. */
async function give(): Promise<string> {
  const [row] = await t.ownerDb.insert(creatures).values({
    serverId: SERVER_ID, playerId, species: 'Vetch', generation: 1,
    trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1, instinct: 'Vanguard',
    hpCurrent: creatureHp('Vetch'), isFounder: false,
  }).returning()
  return row!.creatureId
}

describe('POST /v1/ftue/splice-stock', () => {
  it('grants Vetch + Ember once, only after wave 2, only before the first splice', async () => {
    // wave 2 not cleared: ftue_stock_unavailable
    const early = await stock()
    expect(early.status).toBe(409)
    expect(await early.json()).toMatchObject({ code: 'ftue_stock_unavailable' })

    await clearWave(2)
    const ok = await stock()
    expect(ok.status).toBe(200)
    const body = await ok.json() as
      { creatures: Array<{ species: string; isFounder: boolean; generation: number }> }
    expect(body.creatures.map((c) => c.species).sort()).toEqual(['Ember', 'Vetch'])
    expect(body.creatures.every((c) => !c.isFounder && c.generation === 1)).toBe(true)

    // already granted
    const again = await stock()
    expect(again.status).toBe(409)
    expect(await again.json()).toMatchObject({ code: 'ftue_stock_unavailable' })

    const markers = await withServer(t.db, SERVER_ID, (tx) => readMarkers(tx, SERVER_ID, playerId))
    expect(markers.tutorialStockGrantedAt).not.toBeNull()
  })

  it('the same Idempotency-Key replays the stored 200 rather than a 409', async () => {
    // design §4.2's guard one: the key protects the RESPONSE. A client
    // retrying its own successful grant - a dropped connection after the
    // server committed, say - must not be told the grant is unavailable,
    // which is what a second call under a FRESH key would correctly answer.
    await clearWave(2)
    const key = randomUUID()

    const first = await stock(key)
    expect(first.status).toBe(200)
    const firstBody = await first.json()

    const second = await stock(key)
    expect(second.status).toBe(200)
    expect(await second.json()).toEqual(firstBody)
  })

  it('refuses the pair once this player has already spliced, even after wave 2', async () => {
    // NOT IN THE BRIEF'S OWN SNIPPET, but named directly by
    // `grantTutorialStock`'s three gates: a player who spliced away base
    // stock before ever reaching this beat must not also collect the pair
    // meant to teach the lesson on their FIRST splice. A real splice through
    // the paid route, not a hand-inserted `splices` row, so this is exactly
    // the same fact `isFirstSplice` reads for the guaranteed-mutation tests
    // in splice-preview.test.ts and splice-commit.test.ts.
    await clearWave(2)
    const a = await give()
    const b = await give()
    const spliced = await app.request('/v1/splice/commit', {
      method: 'POST',
      headers: {
        'content-type': 'application/json', authorization: `Bearer ${token}`,
        'idempotency-key': randomUUID(),
      },
      body: JSON.stringify({
        parentA: a, parentB: b, locked: { from: 'a', slot: 'trait_1' }, bodyFrom: 'Vetch',
      }),
    })
    expect(spliced.status).toBe(200)

    const res = await stock()
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'ftue_stock_unavailable' })

    const markers = await withServer(t.db, SERVER_ID, (tx) => readMarkers(tx, SERVER_ID, playerId))
    expect(markers.tutorialStockGrantedAt).toBeNull()
  })

  it('requires an Idempotency-Key', async () => {
    await clearWave(2)
    const res = await app.request('/v1/ftue/splice-stock', {
      method: 'POST',
      headers: { authorization: `Bearer ${token}` },
    })
    expect(res.status).toBe(400)
  })

  it('requires a session', async () => {
    const res = await app.request('/v1/ftue/splice-stock', {
      method: 'POST',
      headers: { authorization: 'Bearer nonsense', 'idempotency-key': randomUUID() },
    })
    expect(res.status).toBe(401)
  })

  it('refuses the pair against a splice commit landing concurrently - fix round 1', async () => {
    // THE RACE fix round 1 found: `isFirstSplice` was a bare, unlocked
    // `count(*)` on `splices`, and `commitSplice` shared no lock with it -
    // so a real commit landing in the window between this grant's read and
    // its write could interleave with it, both proceeding on the same stale
    // "zero splices" answer. Reachable in production, not theoretical:
    // starter creatures are granted at sign-up, so every player holds
    // splice-eligible creatures from their first session.
    //
    // CONSTRUCTED, not hoped for - `commit.ts`'s own idiom
    // (splice-commit.test.ts's "refuses two concurrent splices..." and
    // "does not deadlock..." tests) via `SpliceHooks.afterParentsLocked`,
    // which pauses `commitSplice` in its read-to-write window. Since the
    // fix, `commitSplice` takes `lockRoster` BEFORE `lockParents`, so by the
    // time this hook fires the advisory lock is already held - the grant
    // below calling `grantTutorialStock` (which now takes the SAME lock
    // before its `isFirstSplice` read) must block on it rather than read
    // straight past.
    await clearWave(2)
    const a = await give()
    const b = await give()
    const bundle = await loadBundle(deps.bundleStore)
    const locked = { from: 'a' as const, slot: 'trait_1' as const }

    let release!: () => void
    const paused = new Promise<void>((r) => { release = r })
    let commitIsInTheWindow!: () => void
    const commitReachedTheWindow = new Promise<void>((r) => { commitIsInTheWindow = r })

    const commitTx = withServer(deps.db, SERVER_ID, (tx) => commitSplice(
      tx, SERVER_ID, playerId, bundle,
      { parentA: a, parentB: b, locked, bodyFrom: 'Vetch' },
      new Date(), randomUUID(), {
        afterParentsLocked: async () => { commitIsInTheWindow(); await paused },
      }))

    // The commit holds its parent locks - and, with the fix, `lockRoster` -
    // and has written nothing yet.
    await commitReachedTheWindow

    let grantSettled = false
    const grantTx = withServer(deps.db, SERVER_ID, (tx) => grantTutorialStock(tx, SERVER_ID, playerId))
      .then((r) => { grantSettled = true; return r })

    // try/finally around the timed check: `commitTx` is paused on `paused`
    // regardless of what this assertion does, and a failed assertion here
    // (as happens pre-fix - see below) must still `release()` it, or the
    // hung transaction outlives the test and takes the file's `afterAll`
    // down with a hook timeout instead of a clean, attributable failure.
    // Measured, not guarded against on paper: this is exactly what happened
    // running this test against the pre-fix code without the `finally`.
    try {
      await new Promise((r) => setTimeout(r, 400))
      // THE FIX'S EFFECT, DIRECTLY OBSERVED, and the assertion that fails
      // without it: pre-fix, `grantTutorialStock` reads `isFirstSplice`
      // (zero rows - the commit above has written nothing yet), takes its
      // OWN uncontended `lockRoster` and completes well inside 400ms,
      // granting the pair to a player whose splice is landing at that very
      // moment. Measured, not assumed: reverting the two `lockRoster` calls
      // this round added reproduces exactly that - `grantSettled` is `true`
      // here and the outcome below is `'ok'`.
      expect(grantSettled).toBe(false)
    } finally {
      release()
    }

    const commitResult = await commitTx
    const grantResult = await grantTx

    expect(commitResult.kind).toBe('ok')
    // Unblocked, the grant now observes the TRUE state - this player's
    // first splice already happened - and refuses rather than granting a
    // second guaranteed-mutation pair over one moment nobody could see both
    // halves of.
    expect(grantResult.kind).toBe('unavailable')
  })
})
