import { eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import {
  accounts, arks, creatures, harvestPositions, nodeDepletion, players, servers, splices,
} from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const SERVER_A = 1
const SERVER_B = 2

/**
 * The FIVE tables 0005 adds, as the isolation gate would enumerate them.
 * There is no creature_tombstones: design 3.2 as amended keeps a pruned
 * creature in `creatures` behind a `pruned` flag.
 */
const NEW_TABLES = [
  'creatures', 'arks', 'node_depletion', 'harvest_positions', 'splices',
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
 * design 3.2's prune, as amended: an UPDATE, not a DELETE. The row keeps its
 * id and its parent pointers and is stripped to
 * {species, generation, is_founder}.
 *
 * Written as raw SQL naming every stripped column rather than through the
 * typed surface, because what is being pinned is that the SCHEMA tolerates
 * this exact statement - the NOT NULLs that used to sit on trait_1, trait_2,
 * instinct and hp_current would each reject it.
 */
const prune = async (creatureId: string) => {
  const res = await withServer(t.db, SERVER_A, (tx) => tx.execute(sql`
    UPDATE creatures
       SET pruned = true,
           trait_1 = NULL, tier_1 = NULL, trait_2 = NULL, tier_2 = NULL,
           instinct = NULL, name = NULL, hp_current = NULL,
           regen_until = NULL, committed_to = NULL
     WHERE server_id = ${SERVER_A} AND creature_id = ${creatureId}`))
  expect(res.rowCount).toBe(1)
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

describe('a pruned creature keeps its row - design 3.2, as amended', () => {
  // THE PROPERTY THIS WHOLE RULING EXISTS TO PROTECT. The first version of
  // this schema put tombstones in a second table and kept composite parent
  // keys back onto creatures; those are mutually exclusive, because every
  // ancestor is referenced by its own child and the keys are NO ACTION, so
  // the prune could delete nothing at all.

  it('prunes an ancestor that still has a living descendant', async () => {
    // The exact operation that was impossible before the ruling. Measured
    // then: `update or delete on table "creatures" violates foreign key
    // constraint "creatures_server_id_parent_a_fkey"`.
    // NOT a Founder - founders_are_never_pruned forbids that, and this
    // test's first draft tripped on it. A pruned row therefore always
    // carries is_founder = false, which is exactly data_model 4's tombstone
    // shape: {id, species, generation, was_founder: false}.
    const [ancestor] = await insertCreature({ name: null, isFounder: false })
    const [child] = await insertCreature({ parentA: ancestor!.creatureId, generation: 2 })

    await prune(ancestor!.creatureId)

    // The descendant is untouched and STILL POINTS AT IT. An ON DELETE
    // SET NULL resolution - which the ruling rejected - would have blanked
    // this, and the lineage view would have lost the row it exists to reach.
    const [stillThere] = await withServer(t.db, SERVER_A, (tx) => tx.select().from(creatures)
      .where(eq(creatures.creatureId, child!.creatureId)))
    expect(stillThere?.parentA).toBe(ancestor!.creatureId)

    // And the pointer RESOLVES - to a row that is stripped but real, with
    // exactly the three fields a lineage view renders.
    const [tombstone] = await withServer(t.db, SERVER_A, (tx) => tx.select().from(creatures)
      .where(eq(creatures.creatureId, stillThere!.parentA!)))
    expect(tombstone).toBeDefined()
    expect(tombstone?.pruned).toBe(true)
    expect(tombstone?.species).toBe('vetch')
    expect(tombstone?.generation).toBe(1)
    // is_founder SURVIVES the strip - false here, and false rather than
    // null, which is the part that matters: the column is NOT NULL so the
    // prune cannot blank it even by naming it. It is what distinguishes a
    // prunable ancestor from a Founder that must never be stripped at all.
    expect(tombstone?.isFounder).toBe(false)
    // The payload is gone - this is the forty bytes, not a live creature
    // wearing a flag.
    expect(tombstone?.trait1).toBeNull()
    expect(tombstone?.instinct).toBeNull()
    expect(tombstone?.hpCurrent).toBeNull()
    // only_founders_named still holds across the strip: the prune nulls
    // `name`, so the constraint is satisfied rather than merely unexamined.
    expect(tombstone?.name).toBeNull()
  })

  it('refuses to DELETE a creature that is still someone\'s parent', async () => {
    // The NO ACTION keys, pinned. ON DELETE CASCADE would take the living
    // descendant with the ancestor; ON DELETE SET NULL would blank the
    // child's pointer. Both make this DELETE succeed, so both redden here -
    // which is the point: the prune is an UPDATE and nothing needs this
    // DELETE to work.
    const [ancestor] = await insertCreature()
    await insertCreature({ parentA: ancestor!.creatureId, generation: 2 })

    await expect(t.ownerDb.execute(sql`
      DELETE FROM creatures WHERE server_id = ${SERVER_A} AND creature_id = ${ancestor!.creatureId}`))
      .rejects.toThrow(/foreign key/)
  })

  it('refuses a LIVE creature with no trait, instinct or hp', async () => {
    // The prune needed those four column-level NOT NULLs dropped. Without
    // live_creatures_are_whole that would have quietly made a live creature
    // with no trait insertable - a strictly worse schema than the one the
    // ruling replaced, arrived at as a side effect of fixing something else.
    await expect(insertCreature({ trait1: null })).rejects.toThrow(/live_creatures_are_whole/)
    // trait_2 as well - the constraint names it and the same "one twin
    // proven, the other not" argument that put tier_2 in the coverage test
    // applies here unchanged.
    await expect(insertCreature({ trait2: null })).rejects.toThrow(/live_creatures_are_whole/)
    await expect(insertCreature({ instinct: null })).rejects.toThrow(/live_creatures_are_whole/)
    await expect(insertCreature({ hpCurrent: null })).rejects.toThrow(/live_creatures_are_whole/)
  })

  it('refuses a flag-only prune that keeps the payload', async () => {
    // live_creatures_are_whole is ONE-DIRECTIONAL: it constrains live rows
    // and says nothing about what a pruned one may keep. So this statement
    // used to SUCCEED - rowCount 1 - leaving a live creature hidden behind
    // the flag with its whole payload intact, while the comment at `pruned`
    // claimed a stripped row keeps three fields "and nothing else".
    //
    // The failure that makes it worth a constraint is not the storage
    // saving. It is a prune that omits ONE column from its SET list: the row
    // leaves every `NOT pruned` roster query while still holding a live
    // committed_to, so a garrison is held by a creature the roster cannot
    // show and the player cannot recall.
    const [whole] = await insertCreature()
    await expect(withServer(t.db, SERVER_A, (tx) => tx.execute(sql`
      UPDATE creatures SET pruned = true
       WHERE server_id = ${SERVER_A} AND creature_id = ${whole!.creatureId}`)))
      .rejects.toThrow(/pruned_creatures_are_stripped/)

    // The near miss, which is the realistic shape of the bug: everything
    // stripped EXCEPT committed_to.
    const [garrisoned] = await insertCreature({ committedTo: nextId() })
    await expect(withServer(t.db, SERVER_A, (tx) => tx.execute(sql`
      UPDATE creatures
         SET pruned = true, trait_1 = NULL, tier_1 = NULL, trait_2 = NULL,
             tier_2 = NULL, instinct = NULL, name = NULL, hp_current = NULL,
             regen_until = NULL
       WHERE server_id = ${SERVER_A} AND creature_id = ${garrisoned!.creatureId}`)))
      .rejects.toThrow(/pruned_creatures_are_stripped/)
  })

  it('permits a BORN-PRUNED skeleton, deliberately', async () => {
    // Recorded as a DECISION, not overlooked. A row inserted already pruned
    // and already stripped satisfies both constraints. It is allowed
    // because: no request path reaches it (handlers create live creatures);
    // forbidding it needs a BEFORE INSERT trigger on the roster's hottest
    // write for a hazard nothing can reach; and solo_execution 4 makes a
    // server merge a re-keying exercise, which must re-insert already-pruned
    // ancestors directly. If that last reason ever stops being true, this
    // test is the place the decision is written down.
    await expect(insertCreature({
      pruned: true, trait1: null, tier1: null, trait2: null, tier2: null,
      instinct: null, name: null, hpCurrent: null,
    })).resolves.toBeDefined()
  })

  it('refuses to prune a Founder', async () => {
    // design 3.2: "Retain all Founders permanently." Pruning one nulls the
    // name bible 3.3 makes the anchor of every descendant's tree, and
    // only_founders_named means it could never be written back.
    const [founder] = await insertCreature({ name: 'Ossuary', isFounder: true })
    await expect(prune(founder!.creatureId)).rejects.toThrow(/founders_are_never_pruned/)

    // "Retained permanently" means the WHOLE row, not merely a surviving id:
    // the refusal above would be worth little if the statement had stripped
    // the name on its way to being rejected.
    const [intact] = await withServer(t.db, SERVER_A, (tx) => tx.select().from(creatures)
      .where(eq(creatures.creatureId, founder!.creatureId)))
    expect(intact?.name).toBe('Ossuary')
    expect(intact?.trait1).toBe('chill')
    expect(intact?.pruned).toBe(false)
  })

  it('keeps both roster indexes partial on NOT pruned', async () => {
    // A CATALOG assertion, not an EXPLAIN one. This file previously recorded
    // that the index predicate could only be pinned by asserting on a query
    // plan - which would pin the planner rather than the schema - and that
    // was simply wrong: pg_indexes.indexdef is a catalog read, it pins no
    // plan, and it reddens the moment a predicate is dropped.
    //
    // Worth pinning because pruned rows share this table now: without the
    // predicate, creatures_by_player grows to roughly nine thousand dead
    // entries per player against a live roster the Hatchery caps at twenty.
    const r = await t.db.execute(sql`
      SELECT indexname, indexdef FROM pg_indexes
       WHERE schemaname = 'public'
         AND indexname IN ('creatures_by_player', 'creatures_available')
       ORDER BY indexname`)

    const defs = new Map((r.rows as Array<{ indexname: string; indexdef: string }>)
      .map((row) => [row.indexname, row.indexdef]))
    // Both present first: a missing index contributes no row, and "every row
    // I found mentions NOT pruned" is vacuously true of no rows.
    expect([...defs.keys()]).toEqual(['creatures_available', 'creatures_by_player'])
    for (const [name, def] of defs) expect(def, name).toMatch(/NOT pruned/)
    // creatures_available carries BOTH halves of its predicate.
    expect(defs.get('creatures_available')).toMatch(/committed_to IS NULL/)
  })

  it('the availability predicate must say NOT pruned - committed_to IS NULL is true of a tombstone', async () => {
    // THE TRAP, demonstrated rather than described, because Tasks 5 and 7
    // write the Hatchery-cap query and this is the schema fact that decides
    // whether they get it right. Pruning nulls committed_to, so a stripped
    // ancestor satisfies `committed_to IS NULL` - the exact predicate
    // creatures_available carried before the ruling, when pruned rows lived
    // in a different table and could not possibly have matched it.
    const [doomed] = await insertCreature()
    await prune(doomed!.creatureId)

    const naive = await withServer(t.db, SERVER_A, (tx) => tx.select().from(creatures)
      .where(sql`player_id = ${playerA} AND committed_to IS NULL`))
    const correct = await withServer(t.db, SERVER_A, (tx) => tx.select().from(creatures)
      .where(sql`player_id = ${playerA} AND committed_to IS NULL AND NOT pruned`))

    // The naive predicate DOES pick the tombstone up. If this assertion ever
    // flips, the hazard is gone and this test should go with it - but it
    // must not flip silently.
    expect(naive.some((c) => c.creatureId === doomed!.creatureId)).toBe(true)
    expect(correct.some((c) => c.creatureId === doomed!.creatureId)).toBe(false)
    expect(correct.length).toBeLessThan(naive.length)
  })
})

describe('node_depletion', () => {
  it('refuses a node on a server that does not exist', async () => {
    // node_depletion was the ONLY table across 0001-0005 with no foreign key
    // at all - it is the only one that hangs off no player, so nothing tied
    // its server_id to a server that exists and `server_id = 999` inserted
    // happily, while the same bogus id on harvest_positions was refused by
    // its composite key to players. Every other table reaches servers
    // transitively; this one says it directly, as accounts does in 0001.
    //
    // Driven from the OWNER connection on purpose: a superuser bypasses RLS,
    // so the foreign key is the only thing that can refuse this. Through the
    // app role the policy would reject it first and the FK would go
    // unexercised.
    await expect(t.ownerDb.insert(nodeDepletion).values({
      serverId: 999, regionId: 'verdant-shelf', nodeSlot: 1, epoch: 1, harvestedUnits: 0,
    })).rejects.toThrow(/foreign key/)
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

describe('row-level security on the five new tables', () => {
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
         AND c.relname IN ('creatures', 'arks', 'node_depletion',
                           'harvest_positions', 'splices')
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

  it('shows a scoped read only its own server, on every one of the five', async () => {
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
      arks: await tx.select().from(arks),
      nodeDepletion: await tx.select().from(nodeDepletion),
      harvestPositions: await tx.select().from(harvestPositions),
      splices: await tx.select().from(splices),
    }))

    const scoped: Array<[string, Array<{ serverId: number }>]> = [
      ['creatures', seen.creatures],
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

  it('refuses an UPDATE that migrates a row onto another server - WITH CHECK, not just USING', async () => {
    // The other direction, and it was untested on all five new tables. It is
    // NOT the INSERT case above: the row already exists and is legitimately
    // visible and writable in this session; the attack is changing WHICH
    // server it belongs to. USING alone would not stop it - it only filters
    // what the UPDATE can see going in. isolation.test.ts pins this for the
    // five tables that predate 0005.
    //
    // player_id moves with server_id wherever a composite FK to players
    // exists, so the FK is satisfied by the new row and RLS is the only
    // thing left that can refuse it - otherwise this would pass on a foreign
    // key violation and prove nothing about the policy.
    const [victim] = await insertCreature()
    // Each row carries the rejection it must produce, because they are not
    // all the same and collapsing them to "it threw something" would hide
    // the difference. splices is refused ONE STEP EARLIER than the policy:
    // it carries no UPDATE grant at all (a splice is a record of something
    // that happened), so the grant refuses before RLS is consulted. That is
    // a strictly stronger guarantee than the policy would give, and it is
    // the same argument 0002 makes for accounts' column-level grant - so it
    // is asserted as what it is rather than bent into an RLS failure.
    const migrations: Array<[string, ReturnType<typeof sql>, RegExp]> = [
      ['creatures', sql`UPDATE creatures SET server_id = ${SERVER_B}, player_id = ${playerB}
                         WHERE creature_id = ${victim!.creatureId}`, /row-level security/i],
      ['arks', sql`UPDATE arks SET server_id = ${SERVER_B}, player_id = ${playerB}
                    WHERE player_id = ${playerA}`, /row-level security/i],
      ['node_depletion', sql`UPDATE node_depletion SET server_id = ${SERVER_B}
                              WHERE region_id = 'verdant-shelf'`, /row-level security/i],
      ['harvest_positions', sql`UPDATE harvest_positions SET server_id = ${SERVER_B}, player_id = ${playerB}
                                 WHERE player_id = ${playerA}`, /row-level security/i],
      ['splices', sql`UPDATE splices SET server_id = ${SERVER_B}, player_id = ${playerB}
                       WHERE player_id = ${playerA}`, /permission denied/i],
    ]
    for (const [table, stmt, why] of migrations) {
      await expect(withServer(t.db, SERVER_A, (tx) => tx.execute(stmt)), table)
        .rejects.toThrow(why)
    }
  })

  it('returns ZERO rows when nothing scoped the query, rather than everything', async () => {
    const rows = await t.db.select().from(creatures)
    expect(rows).toEqual([])
  })
})
