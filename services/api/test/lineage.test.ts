import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { accounts, creatures, players, servers } from '../src/db/schema.ts'
import { RETAINED_DEPTH, pruneLineage } from '../src/roster/lineage.ts'
import { startTestDb, type TestDb } from './harness.ts'

/**
 * design §3.2's retention rule, tested as what it is: a function over rows.
 *
 * THESE LINEAGES ARE CONSTRUCTED DIRECTLY rather than by splicing seven
 * times, and that is not a shortcut. The Splicing Chamber caps generation at
 * G4 this phase (`combat_numbers` §7), so a seven-deep line is not reachable
 * through `POST /v1/splice/commit` at all - the route's own use of this
 * function is covered in splice-commit.test.ts, and what needs testing here
 * is the rule, at depths the route cannot produce.
 *
 * DEPTH IS MEASURED FROM THE CHILD, not from generation 1: the child is
 * depth 0, its parents depth 1. That is the only reading under which the
 * rule is coherent - "retain five generations" has to mean the five NEAREST
 * ancestors, or the immediate parent of a deep line would be the first thing
 * pruned.
 */

const SERVER_ID = 1

let t: TestDb
let playerId: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open',
    tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
}, 240_000)

afterAll(async () => { await t?.stop() })

async function freshPlayer(): Promise<string> {
  const [acc] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_ID }).returning()
  const [p] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_ID, accountId: acc!.accountId }).returning()
  return p!.playerId
}

/** A fresh player per test, so no test depends on another's rows. */
beforeEach(async () => { playerId = await freshPlayer() })

interface NewRow {
  parentA?: string | null
  parentB?: string | null
  generation?: number
  isFounder?: boolean
  /** Dead by default: an ancestor is a creature some splice consumed. */
  live?: boolean
  owner?: string
}

/**
 * One creature, through the OWNER connection.
 *
 * Fixtures, not grants: `grantBaseStock` mints Gen-1 base stock with no way
 * to ask for a parent or a generation, which is correct for base stock and
 * useless for building a line. harness.ts's ownerDb is exactly what that is
 * for, and the same idiom splice-preview.test.ts uses.
 */
async function give(o: NewRow = {}): Promise<string> {
  const [row] = await t.ownerDb.insert(creatures).values({
    serverId: SERVER_ID,
    playerId: o.owner ?? playerId,
    species: 'Vetch',
    generation: o.generation ?? 1,
    trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1,
    instinct: 'Vanguard', hpCurrent: 260,
    isFounder: o.isFounder ?? false,
    // Only Founders may be named - 0005's only_founders_named - and the name
    // is what bible §3.3 makes the anchor of every descendant's tree, so a
    // Founder fixture without one would not be able to show that the
    // retention rule protects anything.
    name: o.isFounder === true ? 'Ossuary' : null,
    parentA: o.parentA ?? null,
    parentB: o.parentB ?? null,
    consumedAt: o.live === true ? null : new Date('2026-09-01T00:00:00Z'),
  }).returning()
  return row!.creatureId
}

/**
 * A single-parent chain `depth` deep. Index 0 is the child, index d is the
 * ancestor d generations back.
 *
 * Built deepest-first because the parent keys are real: a child cannot name
 * a parent row that does not exist yet.
 */
async function buildLine(
  depth: number, o: { founderAt?: number; liveAt?: number } = {},
): Promise<string[]> {
  const ids: string[] = []
  let parent: string | null = null
  for (let d = depth; d >= 0; d--) {
    parent = await give({
      parentA: parent,
      generation: depth - d + 1,
      isFounder: o.founderAt === d,
      // The child at depth 0 is the LIVE creature the line hangs off; every
      // ancestor is dead, because an ancestor is a consumed parent.
      live: d === 0 || o.liveAt === d,
    })
    ids[d] = parent
  }
  return ids
}

type Row = typeof creatures.$inferSelect

async function read(id: string): Promise<Row | undefined> {
  const [row] = await t.ownerDb.select().from(creatures)
    .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, id)))
  return row
}

/** Pruned or not - the only sense in which a creature row stops existing. */
async function isPruned(id: string): Promise<boolean> {
  return (await read(id))?.pruned === true
}

/** Runs the prune as the APP role, under RLS, the way a handler runs it. */
async function prune(childId: string): Promise<number> {
  return withServer(t.db, SERVER_ID, (tx) => pruneLineage(tx, SERVER_ID, childId))
}

describe('pruneLineage', () => {
  it('prunes an ancestor six deep to a tombstone, and nothing shallower', async () => {
    // data_model §4: bible §2.1 caps the lineage view at five generations, so
    // an ancestor six deep is never displayed and never needs to exist. ~300
    // records per player instead of ~9,000.
    const line = await buildLine(6)

    expect(await prune(line[0]!)).toBe(1)

    expect(await isPruned(line[6]!)).toBe(true)
    for (const d of [1, 2, 3, 4, 5]) {
      expect(await isPruned(line[d]!), `depth ${d}`).toBe(false)
    }
  })

  it('leaves the tombstone holding exactly {species, generation, is_founder}', async () => {
    // data_model §4 gives the shape literally, including `was_founder: false`
    // - false rather than null, because the column is NOT NULL and the prune
    // cannot blank it even by naming it. Everything else must be gone, or the
    // forty-byte saving the whole rule exists for is not achieved.
    const line = await buildLine(6)
    await prune(line[0]!)

    const tombstone = await read(line[6]!)
    expect(tombstone).toMatchObject({
      species: 'Vetch', generation: 1, isFounder: false, pruned: true,
    })
    expect(tombstone?.trait1).toBeNull()
    expect(tombstone?.tier1).toBeNull()
    expect(tombstone?.trait2).toBeNull()
    expect(tombstone?.tier2).toBeNull()
    expect(tombstone?.instinct).toBeNull()
    expect(tombstone?.name).toBeNull()
    expect(tombstone?.hpCurrent).toBeNull()
    expect(tombstone?.regenUntil).toBeNull()
    expect(tombstone?.committedTo).toBeNull()
    // NOT stripped: it is the liveness marker, and a tombstone is the most
    // dead a row gets. 0005's pruned_creatures_are_consumed would refuse the
    // write if this were nulled.
    expect(tombstone?.consumedAt).not.toBeNull()
  })

  it('keeps the descendant\'s pointer resolving to the tombstone', async () => {
    // The property the whole ruling exists to protect: the prune is an
    // UPDATE, so the lineage view still reaches a row that really exists. An
    // ON DELETE SET NULL resolution - which the ruling rejected - would have
    // blanked this pointer, and a DELETE could not have run at all.
    const line = await buildLine(6)
    await prune(line[0]!)

    const descendant = await read(line[5]!)
    expect(descendant?.parentA).toBe(line[6]!)
    expect(await read(descendant!.parentA!)).toBeDefined()
  })

  it('never prunes a Founder, at any depth', async () => {
    // bible §3.3: Founder names appear in every descendant's tree regardless
    // of depth, so they are the one permanent exception. 0005's
    // founders_are_never_pruned would REFUSE the write, so a prune that
    // forgot this would not merely retain the wrong thing - it would abort
    // the transaction that is destroying two creatures.
    const line = await buildLine(9, { founderAt: 8 })

    // Everything past five except the Founder: depths 6, 7 and 9.
    expect(await prune(line[0]!)).toBe(3)

    expect(await isPruned(line[8]!)).toBe(false)
    const founder = await read(line[8]!)
    // "Retained permanently" means the WHOLE row, not a surviving id.
    expect(founder?.name).toBe('Ossuary')
    expect(founder?.trait1).toBe('Taunt')
  })

  it('never prunes a LIVE creature, even past the retained depth', async () => {
    // The one thing this function must never do. An ancestor is by
    // definition a creature some splice consumed, so this should be
    // unreachable - which is exactly why it is in the WHERE rather than
    // assumed: a bad parent pointer should cost nothing, not a roster row
    // the player still owns.
    const line = await buildLine(7, { liveAt: 7 })

    expect(await prune(line[0]!)).toBe(1) // depth 6 only
    expect(await isPruned(line[7]!)).toBe(false)
    expect((await read(line[7]!))?.trait1).toBe('Taunt')
  })

  it('retains an ancestor that is deep by one path and shallow by another', async () => {
    // A creature can be its own descendant's ancestor by more than one
    // route - nothing forbids splicing two creatures that share an ancestor
    // - so the same row appears at several depths. It is retained if ANY
    // path reaches it within five, which is what MIN(depth) says. A
    // per-path prune would destroy it on the long path and the short path
    // would then resolve to a tombstone.
    const shared = await give({ generation: 1 })

    // A long arm: shared is at depth 7 through it.
    let long: string = shared
    for (let d = 6; d >= 1; d--) long = await give({ parentA: long, generation: 8 - d })
    // A short arm: shared is also at depth 2, as parent_b of the child.
    const short = await give({ parentA: shared, generation: 2 })

    const child = await give({ parentA: long, parentB: short, generation: 9, live: true })

    await prune(child)

    expect(await isPruned(shared)).toBe(false)
    expect((await read(shared))?.trait1).toBe('Taunt')
  })

  it('is idempotent - a second prune finds nothing left to do', async () => {
    // The return value is the evidence the prune did something, so
    // re-stripping a tombstone must not be counted. It also means the splice
    // path can call this unconditionally.
    const line = await buildLine(8)

    expect(await prune(line[0]!)).toBe(3)
    expect(await prune(line[0]!)).toBe(0)
  })

  it('cannot reach another player\'s creature', async () => {
    // The parent keys are composite on (server_id, creature_id) and say
    // nothing about player_id, so a cross-player pointer is REPRESENTABLE
    // even though no write path creates one. Without the owner clause in the
    // UPDATE, one bad pointer would strip a creature off a roster that never
    // spliced anything.
    const stranger = await freshPlayer()
    const theirs = await give({ owner: stranger, generation: 1 })

    let chain: string = theirs
    for (let d = 6; d >= 1; d--) chain = await give({ parentA: chain, generation: 8 - d })
    const child = await give({ parentA: chain, generation: 9, live: true })

    await prune(child)

    expect(await isPruned(theirs)).toBe(false)
    expect((await read(theirs))?.trait1).toBe('Taunt')
  })

  it('leaves a line no deeper than the retained depth completely alone', async () => {
    // The ordinary case, and the one a splice actually produces this phase:
    // the Chamber caps at G4, so a line reachable through the route is at
    // most four deep and the prune is a no-op on it.
    const line = await buildLine(RETAINED_DEPTH)

    expect(await prune(line[0]!)).toBe(0)
    for (const id of line) expect(await isPruned(id)).toBe(false)
  })
})
