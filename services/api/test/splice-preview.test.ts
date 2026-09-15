import { readFileSync } from 'node:fs'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { creatures, servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { spliceDistribution, type TraitRef } from '../src/splice/distribution.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { setupPlayer } from './wave-helpers.ts'

/**
 * `POST /v1/splice/preview` - design §5.1's first caller.
 *
 * The forecast's ARITHMETIC is pinned in splice-distribution.ts, without a
 * container. What is tested here is the part that needs one: that the route
 * resolves both parents from OWNED, LIVE rows and returns the distribution
 * function's value unaltered.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, which .pathname percent-encodes.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.2: the first bundle carrying a populated traits.json, and the splice
// has no dominance flags to read without it.
const SEED = join(REPO, 'config/bundles/0.1.2')

const AUTHORED = JSON.parse(readFileSync(join(SEED, 'traits.json'), 'utf8')) as
  { traits: Array<{ id: string; dominant: boolean }> }

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

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-splice-preview-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim or submits a replay - the same idiom
  // node-claim.test.ts uses, with the sim address pointing nowhere reachable.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
  await loadBundle(store)
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

interface NewCreature {
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
  instinct?: string
  owner?: string
  pruned?: boolean
  consumed?: boolean
}

/**
 * Inserted through the OWNER connection, not the app's.
 *
 * roster/creatures.ts's `grantBaseStock` is the only insert in `src/` and it
 * mints Gen-1 Vetch/Pale/Ember at Tier I with no way to ask for a tier or a
 * trait - which is correct for base stock and useless for pinning a downtier.
 * These rows are fixtures, and a fixture is exactly what ownerDb is for
 * (harness.ts).
 */
async function give(c: NewCreature): Promise<string> {
  const [row] = await t.ownerDb.insert(creatures).values({
    serverId: SERVER_ID,
    playerId: c.owner ?? playerId,
    species: 'Vetch',
    generation: 1,
    trait1: c.trait1, tier1: c.tier1,
    trait2: c.trait2, tier2: c.tier2,
    instinct: c.instinct ?? 'Vanguard',
    hpCurrent: 260,
    isFounder: false,
    // A creature a splice destroyed: dead, but WHOLE. Pruning is a further
    // state, below - 0005's pruned_creatures_are_consumed makes every
    // tombstone a consumed row too, so a pruned fixture carries both.
    consumedAt: c.consumed === true || c.pruned === true
      ? new Date('2026-09-01T00:00:00Z') : null,
  }).returning()
  const id = row!.creatureId
  if (c.pruned === true) {
    // Stripped the way design §3.2's prune strips it - trait/tier/instinct
    // nulled - so 0005's live_creatures_are_whole is satisfied on both sides
    // and the row is a genuine tombstone rather than a live row wearing a
    // flag.
    await t.ownerDb.update(creatures)
      .set({
        pruned: true, trait1: null, tier1: null, trait2: null, tier2: null,
        instinct: null, hpCurrent: null, committedTo: null,
      })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, id)))
  }
  return id
}

const LOCK_A1: TraitRef = { slot: 'trait_1', from: 'a' }

async function preview(body: unknown, auth = `Bearer ${token}`): Promise<Response> {
  return app.request('/v1/splice/preview', {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: auth },
    body: JSON.stringify(body),
  })
}

const VETCH = { trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1 }
const PALE = { trait1: 'Chill', tier1: 1, trait2: 'Carapace', tier2: 1 }

describe('POST /v1/splice/preview', () => {
  it('returns the distribution function\'s value, unaltered', async () => {
    // THE point of design §5.1, asserted at the route rather than argued: the
    // body the player is shown is `spliceDistribution`'s return value, which
    // is the same value Task 7's commit samples. A handler that rounded for
    // display, or re-derived the odds from the parents itself, would pass
    // every other test in this file.
    const a = await give(VETCH)
    const b = await give(PALE)

    const res = await preview({ parentA: a, parentB: b, locked: LOCK_A1 })
    expect(res.status).toBe(200)

    const bundle = await loadBundle(deps.bundleStore)
    expect(await res.json()).toEqual({
      forecast: JSON.parse(JSON.stringify(
        spliceDistribution(
          { ...VETCH, instinct: 'Vanguard' }, { ...PALE, instinct: 'Vanguard' },
          LOCK_A1, bundle))),
      coverageLost: [],
    })
  })

  it('reads dominance from the BUNDLE, not from a table in the service', async () => {
    // The four flags are provisional and owed to `combat_numbers` §4
    // (config/validate.ts). Pinned against the authored file rather than
    // against four booleans retyped here, so a content change moves this test
    // with it instead of against it.
    const a = await give({ trait1: 'Chill', tier1: 1, trait2: 'Carapace', tier2: 1 })
    const b = await give({ trait1: 'Taunt', tier1: 2, trait2: 'Splash', tier2: 2 })

    const res = await preview({ parentA: a, parentB: b, locked: LOCK_A1 })
    const { forecast } = await res.json() as
      { forecast: { combat2: Array<{ trait: string; tier: number }> } }

    for (const o of forecast.combat2) {
      const authored = AUTHORED.traits.find((x) => x.id === o.trait)!
      const parentTier = o.trait === 'Carapace' ? 1 : 2
      expect(o.tier).toBe(authored.dominant ? parentTier : Math.max(1, parentTier - 1))
    }
  })

  it('names the coverage a duplicated trait destroys, by trait and tier', async () => {
    // `sample_economy` §7's screen requirement reaching the wire. Lock the
    // Tier I copy of a trait both parents carry and the Tier III copy is gone
    // with certainty - the mistake this element exists to stop.
    const a = await give({ trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1 })
    const b = await give({ trait1: 'Chill', tier1: 1, trait2: 'Carapace', tier2: 3 })

    const res = await preview({
      parentA: a, parentB: b, locked: { slot: 'trait_2', from: 'a' },
    })
    expect(await res.json()).toMatchObject({
      coverageLost: [{ trait: 'Carapace', tier: 3 }],
    })
  })

  it('writes nothing, so two previews in a row are identical', async () => {
    // design §5.1: the charge is spent at commit. A preview that consumed or
    // recorded anything would make looking at the odds cost something, which
    // is the opposite of what bible §2.6 commits to.
    const a = await give(VETCH)
    const b = await give(PALE)
    const body = { parentA: a, parentB: b, locked: LOCK_A1 }

    const first = await (await preview(body)).json()
    const second = await (await preview(body)).json()

    expect(second).toEqual(first)
    const rows = await t.ownerDb.select().from(creatures)
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.playerId, playerId)))
    expect(rows).toHaveLength(2)
    expect(rows.every((r) => !r.pruned)).toBe(true)
  })

  it('refuses a parent belonging to another player', async () => {
    // A REAL second player, not a random uuid: `creatures` carries a
    // composite FK to (server_id, player_id), so an invented owner is
    // refused by the schema before the route is ever reached - which would
    // make this test fail for a reason that has nothing to do with the route.
    const other = await setupPlayer(deps)
    const mine = await give(VETCH)
    const theirs = await give({ ...PALE, owner: other.playerId })

    const res = await preview({ parentA: mine, parentB: theirs, locked: LOCK_A1 })
    expect(res.status).toBe(404)
  })

  it('refuses a PRUNED ancestor as a parent', async () => {
    // The predicate that is easy to leave out and that the schema cannot
    // enforce: a prune nulls `committed_to`, so every availability filter
    // written as "uncommitted" is TRUE of a dead ancestor. A tombstone has no
    // traits at all, so forecasting against one would publish odds over null.
    const a = await give(VETCH)
    const ancestor = await give({ ...PALE, pruned: true })

    const res = await preview({ parentA: a, parentB: ancestor, locked: LOCK_A1 })
    expect(res.status).toBe(404)
  })

  it('refuses a CONSUMED parent - the state `NOT pruned` does not exclude', async () => {
    // A creature an earlier splice destroyed keeps every trait it had
    // (design §3.2 retains it so the lineage can render it), so a liveness
    // predicate written as `NOT pruned` alone forecasts against it happily -
    // and the commit route would then splice it a second time. The pruned
    // case above cannot catch this: these rows are not pruned.
    const a = await give(VETCH)
    const spent = await give({ ...PALE, consumed: true })

    const res = await preview({ parentA: a, parentB: spent, locked: LOCK_A1 })
    expect(res.status).toBe(404)
  })

  it('refuses a creature spliced with itself', async () => {
    // A splice CONSUMES both parents. Without this the route would happily
    // forecast a pool of that one creature's own two traits.
    const a = await give(VETCH)
    const res = await preview({ parentA: a, parentB: a, locked: LOCK_A1 })

    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('refuses a creature spliced with itself under a DIFFERENT CASE', async () => {
    // The same body `/v1/splice/commit` must refuse, asserted on BOTH routes
    // because they share `parseParents` precisely so they cannot disagree.
    // The uuid regex accepts upper case and Postgres `uuid` equality is
    // case-insensitive; JS `===` is not, so this body passed the
    // self-splice check until the ids were normalised.
    const a = await give(VETCH)
    const res = await preview({ parentA: a, parentB: a.toUpperCase(), locked: LOCK_A1 })

    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('accepts an UPPER-CASE parent id', async () => {
    // The positive control: normalising must not become "reject upper case",
    // which would break every client sending canonical upper-case uuids.
    const a = await give(VETCH)
    const b = await give(PALE)
    const res = await preview({ parentA: a.toUpperCase(), parentB: b, locked: LOCK_A1 })

    expect(res.status).toBe(200)
  })

  it('refuses a malformed creature id as a bad request, not as a server error', async () => {
    // `creature_id` is a Postgres uuid: without the parse-layer shape check
    // the read raises 22P02 from inside the transaction and the caller is
    // told 500 for a request they malformed.
    const b = await give(PALE)
    const res = await preview({ parentA: 'not-a-uuid', parentB: b, locked: LOCK_A1 })

    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('refuses a lock that names no real slot', async () => {
    const a = await give(VETCH)
    const b = await give(PALE)

    for (const locked of [
      { slot: 'trait_3', from: 'a' }, { slot: 'trait_1', from: 'c' },
      { slot: 'trait_1' }, 'trait_1', null,
    ]) {
      const res = await preview({ parentA: a, parentB: b, locked })
      expect(res.status, JSON.stringify(locked)).toBe(400)
    }
  })

  it('requires a session', async () => {
    const a = await give(VETCH)
    const b = await give(PALE)
    const res = await preview({ parentA: a, parentB: b, locked: LOCK_A1 }, 'Bearer nonsense')
    expect(res.status).toBe(401)
  })
})
