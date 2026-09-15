import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import {
  accounts, arks, creatures, creatureTombstones, harvestPositions, nodeDepletion, players,
  servers, splices,
} from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const SERVER_A = 1
const SERVER_B = 2

/** The six tables 0005 adds, as the isolation gate would enumerate them. */
const NEW_TABLES = [
  'creatures', 'creature_tombstones', 'arks', 'node_depletion', 'harvest_positions', 'splices',
] as const

let t: TestDb
let playerA: string
let playerB: string

let n = 0
/** Distinct, readable creature ids - no two inserts may collide on the PK. */
const nextId = () => `cccccccc-0000-0000-0000-${String(++n).padStart(12, '0')}`

beforeAll(async () => {
  t = await startTestDb()

  // Fixtures go in as the OWNER: seeding two servers is precisely what a
  // policy-bound connection is not allowed to do (isolation.test.ts's idiom).
  await t.ownerDb.insert(servers).values([
    { serverId: SERVER_A, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200 },
    { serverId: SERVER_B, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1217 },
  ])
  const [accA] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_A }).returning()
  const [accB] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_B }).returning()
  const [pA] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_A, accountId: accA!.accountId }).returning()
  const [pB] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_B, accountId: accB!.accountId }).returning()
  playerA = pA!.playerId
  playerB = pB!.playerId
}, 240_000)

afterAll(async () => { await t?.stop() })

type CreatureFields = Partial<typeof creatures.$inferInsert>

/**
 * A creature that satisfies every column the schema requires, so a rejection
 * is attributable to the one field a test overrode and to nothing else.
 */
const insertCreature = (o: CreatureFields = {}, serverId = SERVER_A) =>
  withServer(t.db, serverId, (tx) => tx.insert(creatures).values({
    serverId,
    creatureId: nextId(),
    playerId: serverId === SERVER_A ? playerA : playerB,
    species: 'vetch',
    generation: 1,
    trait1: 'chill', tier1: 1,
    trait2: 'lash', tier2: 1,
    instinct: 'forage',
    hpCurrent: 100,
    ...o,
  }).returning())

/**
 * design 3.2's prune: the live row is DELETED and a tombstone takes its
 * place. Both halves matter - a "prune" that left the creature row behind
 * would make the reuse test below pass on the primary key instead of on the
 * trigger, which is the vacuity this file's regexes are written to catch.
 */
const pruneToTombstone = async (creatureId: string) => {
  await withServer(t.db, SERVER_A, async (tx) => {
    const gone = await tx.execute(sql`
      DELETE FROM creatures WHERE server_id = ${SERVER_A} AND creature_id = ${creatureId}`)
    expect(gone.rowCount).toBe(1)
    await tx.insert(creatureTombstones).values({
      serverId: SERVER_A, creatureId, species: 'vetch', generation: 1, wasFounder: false,
    })
  })
  return creatureId
}

describe('creatures', () => {
  it('refuses coverage_tier = 0, and permits NULL', async () => {
    // data_model 2: an Aberrant has coverage_tier NULL, not 0. Zero would
    // sort and display as "less than tier I", so the two must not be
    // conflated - and design 5.3's downtier floors at I, so nothing else
    // ever produces a zero either.
    await expect(insertCreature({ tier1: 0 })).rejects.toThrow(/coverage_tier/)
    // The SECOND slot, too. One constraint proven and its twin unproven is
    // how a rolled trait ends up with a tier the locked slot could never
    // hold, and nothing in this file would have noticed.
    await expect(insertCreature({ tier2: 0 })).rejects.toThrow(/coverage_tier/)

    await expect(insertCreature({ tier1: null })).resolves.toBeDefined()
    await expect(insertCreature({ tier2: null })).resolves.toBeDefined()
  })

  it('refuses a creature whose parent lives on another server', async () => {
    // solo_execution 12: server_id leads every key, and the composite FK is
    // what makes that structural rather than conventional.
    const [onB] = await insertCreature({}, SERVER_B)

    await expect(insertCreature({ parentA: onB!.creatureId }))
      .rejects.toThrow(/foreign key/)
    // parent_b carries the same FK and would otherwise be unproven.
    await expect(insertCreature({ parentB: onB!.creatureId }))
      .rejects.toThrow(/foreign key/)
  })

  it('accepts a parent on the SAME server - the positive control for the FK above', async () => {
    // Without this, the test above passes identically against a schema whose
    // parent FKs reject EVERY parent, and "cross-server lineage is
    // unrepresentable" would be indistinguishable from "lineage is
    // unrepresentable".
    const [parent] = await insertCreature()
    await expect(insertCreature({ parentA: parent!.creatureId, parentB: parent!.creatureId, generation: 2 }))
      .resolves.toBeDefined()
  })

  it('refuses generation 0, and refuses a name on a non-Founder', async () => {
    // Both are in 0005 and neither is load-bearing enough to get its own
    // test - but a constraint nobody has seen fire is a constraint nobody
    // has shown to exist.
    await expect(insertCreature({ generation: 0 })).rejects.toThrow(/generation/)
    await expect(insertCreature({ name: 'Bramble', isFounder: false })).rejects.toThrow(/founders_named/)
    await expect(insertCreature({ name: 'Bramble', isFounder: true })).resolves.toBeDefined()
  })
})

describe('creature ids are allocated, never recycled', () => {
  it('keeps a tombstone id unusable by a live creature', async () => {
    // design 3.2: ids are never reused, including for pruned records. A
    // reused id attaches a dead creature's lineage to a living one, which
    // gets reported as a ghost rather than as a bug.
    const [victim] = await insertCreature()
    const id = await pruneToTombstone(victim!.creatureId)

    // /pruned/, not a bare toThrow(). A bare rejection would also be
    // satisfied by a primary-key violation - i.e. by the creature row still
    // being there - which would pass while the trigger did not exist.
    await expect(insertCreature({ creatureId: id })).rejects.toThrow(/pruned/)
  })

  it('keeps a tombstone id unreachable by an UPDATE, not only by an INSERT', async () => {
    // The guard is about the ID SPACE, not about one statement shape. A
    // trigger scoped to INSERT alone is bypassed by a single UPDATE, which
    // attaches the dead creature's lineage just as effectively.
    const [victim] = await insertCreature()
    const id = await pruneToTombstone(victim!.creatureId)
    const [live] = await insertCreature()

    await expect(withServer(t.db, SERVER_A, (tx) => tx.execute(sql`
      UPDATE creatures SET creature_id = ${id}
       WHERE server_id = ${SERVER_A} AND creature_id = ${live!.creatureId}`)))
      .rejects.toThrow(/pruned/)
  })
})

describe('arks', () => {
  it('defaults the Splicing Chamber to tier 3, and the other two to tier 1', async () => {
    // design 3.3, and it is NOT an inconsistency to be tidied away:
    // combat_numbers 7 caps tiers 1-2 at G2 and therefore at Tier I
    // coverage, which would make design 5.3's recessive downtier
    // unreachable in the only configuration this phase ships.
    const [ark] = await withServer(t.db, SERVER_A, (tx) => tx.insert(arks)
      .values({ serverId: SERVER_A, playerId: playerA, regionId: 'verdant-shelf' }).returning())

    expect(ark!.harvestArrayTier).toBe(1)
    expect(ark!.hatcheryTier).toBe(1)
    expect(ark!.splicingChamberTier).toBe(3)
  })
})

describe('row-level security on the six new tables', () => {
  it('has FORCE ROW LEVEL SECURITY on every new table', async () => {
    // Picked up by the existing pg_class isolation gate WITHOUT being named
    // in it - the gate enumerates tables rather than listing them, so a new
    // table that forgot RLS fails this without anyone updating a list. Kept
    // here as well so this file localises the failure to 0005.
    const r = await t.db.execute(sql`
      SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
       WHERE n.nspname = 'public'
         AND c.relname IN ('creatures', 'creature_tombstones', 'arks',
                           'node_depletion', 'harvest_positions', 'splices')
       ORDER BY c.relname`)

    // Asserted BEFORE the flags: the brief's shape ("select the ones missing
    // RLS, expect zero rows") is satisfied by a table that does not exist,
    // so it would have gone green against an empty database.
    expect(r.rows.map((row) => (row as { relname: string }).relname))
      .toEqual([...NEW_TABLES].sort())

    for (const row of r.rows as Array<{ relname: string; relrowsecurity: boolean; relforcerowsecurity: boolean }>) {
      expect(row.relrowsecurity, `${row.relname}.relrowsecurity`).toBe(true)
      expect(row.relforcerowsecurity, `${row.relname}.relforcerowsecurity`).toBe(true)
    }
  })

  it('shows a scoped read only its own server, on every one of the six', async () => {
    // ENABLE/FORCE above is metadata: it says a policy exists, not that it
    // DISCRIMINATES. isolation.test.ts learned this twice already (its own
    // comments on idempotency_keys, campaign_progress and wave_issuances) -
    // a policy comparing the wrong column, or no policy at all under
    // ENABLE/FORCE, passes a default-deny check identically to a correct
    // one. Seeded on both servers as the owner, then read scoped.
    await t.ownerDb.insert(creatures).values([SERVER_A, SERVER_B].map((s) => ({
      serverId: s, creatureId: nextId(), playerId: s === SERVER_A ? playerA : playerB,
      species: 'vetch', generation: 1, trait1: 'chill', tier1: 1, trait2: 'lash', tier2: 1,
      instinct: 'forage', hpCurrent: 100,
    })))
    await t.ownerDb.insert(creatureTombstones).values([SERVER_A, SERVER_B].map((s) => ({
      serverId: s, creatureId: nextId(), species: 'vetch', generation: 1, wasFounder: false,
    })))
    await t.ownerDb.insert(arks).values(
      { serverId: SERVER_B, playerId: playerB, regionId: 'verdant-shelf' })
    await t.ownerDb.insert(nodeDepletion).values([SERVER_A, SERVER_B].map((s) => ({
      serverId: s, regionId: 'verdant-shelf', nodeSlot: 1, epoch: 1, harvestedUnits: s * 10,
    })))
    await t.ownerDb.insert(harvestPositions).values([SERVER_A, SERVER_B].map((s) => ({
      serverId: s, playerId: s === SERVER_A ? playerA : playerB, regionId: 'verdant-shelf',
      nodeSlot: 1, epoch: 1, lastSettledAt: new Date(),
    })))
    await t.ownerDb.insert(splices).values([SERVER_A, SERVER_B].map((s) => ({
      serverId: s, spliceId: `dddddddd-0000-0000-0000-00000000000${s}`,
      playerId: s === SERVER_A ? playerA : playerB,
      parentA: nextId(), parentB: nextId(), childId: nextId(),
      seed: String(s), mutated: false, aberrant: false,
    })))

    const seen = await withServer(t.db, SERVER_A, async (tx) => ({
      creatures: await tx.select().from(creatures),
      creatureTombstones: await tx.select().from(creatureTombstones),
      arks: await tx.select().from(arks),
      nodeDepletion: await tx.select().from(nodeDepletion),
      harvestPositions: await tx.select().from(harvestPositions),
      splices: await tx.select().from(splices),
    }))

    const scoped: Array<[string, Array<{ serverId: number }>]> = [
      ['creatures', seen.creatures],
      ['creature_tombstones', seen.creatureTombstones],
      ['arks', seen.arks],
      ['node_depletion', seen.nodeDepletion],
      ['harvest_positions', seen.harvestPositions],
      ['splices', seen.splices],
    ]
    for (const [table, rows] of scoped) {
      // A table with no visible rows would satisfy "every row is server A"
      // vacuously - which is exactly what a policy comparing the wrong
      // column produces.
      expect(rows.length, `${table} must have rows to discriminate between`).toBeGreaterThan(0)
      expect([...new Set(rows.map((r) => r.serverId))], table).toEqual([SERVER_A])
    }
    // Named explicitly: server B's rows must not appear anywhere.
    expect(seen.nodeDepletion.some((d) => d.harvestedUnits === 20)).toBe(false)
    expect(seen.splices.some((s) => s.seed === '2')).toBe(false)
  })

  it('refuses to INSERT a row belonging to another server', async () => {
    // WITH CHECK, not USING. The brief's policy snippet carried USING alone;
    // 0002's does both, and writing into a neighbour is the more damaging
    // direction of the two.
    await expect(insertCreature({ serverId: SERVER_B, playerId: playerB }, SERVER_A))
      .rejects.toThrow(/row-level security/i)
  })

  it('returns ZERO rows when nothing scoped the query, rather than everything', async () => {
    const rows = await t.db.select().from(creatures)
    expect(rows).toEqual([])
  })
})
