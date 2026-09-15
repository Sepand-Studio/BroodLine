import { and, count, eq, sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { creatures } from '../db/schema.ts'

/**
 * Roster reads, the Hatchery cap, and the base-stock grant.
 *
 * The one place a roster COUNT is written, on purpose - see rosterCount for
 * the predicate that has to be in it and the measured cost of leaving it out.
 */

export interface CreatureDto {
  creatureId: string
  species: string
  generation: number
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
  instinct: string
  name: string | null
  isFounder: boolean
  committedTo: string | null
}

type CreatureRow = typeof creatures.$inferSelect

/**
 * PRUNED ROWS HAVE NO DTO. Every field below is nullable in the schema only
 * so the prune can strip it (0005_loop.sql's live_creatures_are_whole), so a
 * pruned row would render as a creature with no traits and no instinct - and
 * the only paths that reach this function are roster reads that have already
 * excluded them. Throwing says so, rather than emitting `null` into a
 * response for a creature that does not exist any more.
 */
export function toCreatureDto(row: CreatureRow): CreatureDto {
  if (row.pruned || row.trait1 === null || row.trait2 === null || row.instinct === null) {
    throw new Error(`creature ${row.creatureId} is pruned and has no roster representation`)
  }
  return {
    creatureId: row.creatureId,
    species: row.species,
    generation: row.generation,
    trait1: row.trait1,
    // NULL is an Aberrant, never zero - data_model 2, and 0005's
    // coverage_tier_N_not_zero is what makes the two unconflatable in
    // storage. Forwarded as null for the same reason.
    tier1: row.tier1,
    trait2: row.trait2,
    tier2: row.tier2,
    instinct: row.instinct,
    name: row.name,
    isFounder: row.isFounder,
    committedTo: row.committedTo,
  }
}

/**
 * The Hatchery's roster capacity. bible 7.2 gives 20 as the FLOOR, and
 * design 3.3 pins every Ark at tier 1 with no upgrade path this phase, so
 * this is a one-entry table rather than a curve.
 *
 * `broodline_playtest_tuning_sheet.md` carries the 20 -> 60 curve as an
 * explicit placeholder; writing a guessed curve here would put a second,
 * unauthored one in code. A tier this does not know throws for the same
 * reason accrual.ts's multiplier tables throw: the roster cap decides
 * whether a player's creatures are REFUSED, and a guessed cap is a refusal
 * nobody authored.
 */
export function rosterCap(hatcheryTier: number): number {
  const knownTiers: Record<number, number> = { 1: 20 }
  const cap = knownTiers[hatcheryTier]
  if (cap === undefined) throw new Error(`no authored roster cap for Hatchery tier ${hatcheryTier}`)
  return cap
}

/**
 * Live creatures this player holds. `NOT pruned` IS THE LOAD-BEARING HALF.
 *
 * A pruned creature keeps its row (0005_loop.sql's header: tombstones-in-a-
 * second-table and composite parent keys were mutually exclusive, and the
 * keys won), and the prune NULLS `committed_to` - so `committed_to IS NULL`
 * is TRUE of every dead ancestor. A cap query written as "available means
 * uncommitted" therefore counts them, and data_model 4 puts that population
 * at roughly nine thousand rows per player over two years against a cap of
 * twenty. Measured on this schema during Task 3, not argued: eleven against
 * a correct five.
 *
 * COMMITTED CREATURES DO COUNT, which is the other half and the one that
 * looks like the bug. A creature that is out fighting still occupies a
 * Hatchery slot - it comes back. Excluding it would let a player hold more
 * creatures than the cap by deploying some of them, and design 2.5 makes
 * `committed_to` the thing that stops a commitment from being spliced away,
 * not a thing that stops it from being owned.
 *
 * So the predicate is `NOT pruned` ALONE. It is spelled with the same words
 * 0005's partial indexes use, so a query that forgets it loses the index
 * rather than matching it.
 */
export async function rosterCount(tx: Tx, serverId: number, playerId: string): Promise<number> {
  const [row] = await tx.select({ n: count() }).from(creatures)
    .where(and(
      eq(creatures.serverId, serverId),
      eq(creatures.playerId, playerId),
      sql`NOT ${creatures.pruned}`,
    ))
  return Number(row?.n ?? 0)
}

/**
 * The Gen-1 creature a base-stock grant mints.
 *
 * Every field is a value that EXISTS in authored content, and each one says
 * where it comes from. None of it is in the config bundle, because none of
 * it is authored there: `nodes.json` carries rates, `traits.json` carries
 * the trait table, and the creature's own stats are the ENGINE's (the same
 * split test/replay-format.ts's CREATURE_HP mirror already lives on).
 *
 *   species    Vetch      - engine `Species.Vetch`. THE ONLY SPECIES THIS
 *                           BUNDLE CAN BUILD A WHOLE CREATURE FROM: bible
 *                           1.2 gives every species exactly two combat
 *                           traits, and of bundle 0.1.2's four authored
 *                           traits Vetch holds two (Taunt, Carapace) while
 *                           Pale and Ember hold one each. A Pale grant
 *                           would have to invent Pale's second trait.
 *   trait1/2   Taunt,     - `config/bundles/0.1.2/traits.json`, the two
 *              Carapace     traits authored against Vetch.
 *   tier1/2    I          - Gen-1. combat_numbers 7 ties the coverage
 *                           ceiling to generation and Chamber tier, and
 *                           design 5.3 derives Tier I as the floor coverage
 *                           never drops below. Base stock sits on that floor.
 *   instinct   Vanguard   - the Instinct the engine's own Vetch fixture
 *                           carries (tests/engine/Combat/GoldenTests.cs,
 *                           mirrored in test/replay-format.ts). Bible 1.4
 *                           says Gen-1 base stock ROLLS an Instinct weighted
 *                           per species and that the weights are "to be set
 *                           with the drop tables" - they are not set
 *                           anywhere, so this grants the one value content
 *                           does supply rather than inventing a distribution.
 *   hpCurrent  260        - engine `Stats`, Species.Vetch.
 *
 * TWO ROLLS ARE THEREFORE NOT IMPLEMENTED, and both are owed rather than
 * forgotten: the per-species Instinct weighting above, and `base_stock`
 * 4.2's region weighting of the species itself (30/30/10/10/10/10 across
 * the region's two weighted species and the other four). The second needs
 * per-region content for thirty regions that design 11 already books as
 * owed, and this phase ships one region.
 */
const BASE_STOCK_TEMPLATE = {
  species: 'Vetch',
  generation: 1,
  trait1: 'Taunt', tier1: 1,
  trait2: 'Carapace', tier2: 1,
  instinct: 'Vanguard',
  hpCurrent: 260,
  // Founders are granted deliberately and named; base stock is neither.
  // 0005's only_founders_named refuses a named non-Founder outright.
  isFounder: false,
  name: null,
} as const

/**
 * Mints `count` Gen-1 creatures for this player, in the caller's transaction.
 *
 * DOES NOT CHECK THE CAP. design 4.3 refuses a claim that would exceed the
 * Hatchery cap BEFORE the transaction opens rather than truncating the grant
 * inside it, and Task 10's wave path SKIPS the grant instead of failing the
 * submission - two different policies over the same write. Putting the check
 * here would force one of them on the other; the callers decide, and this
 * function grants exactly what it is asked for.
 */
export async function grantBaseStock(
  tx: Tx, serverId: number, playerId: string, n: number,
): Promise<CreatureRow[]> {
  if (n <= 0) return []
  const rows = await tx.insert(creatures)
    .values(Array.from({ length: n }, () => ({ serverId, playerId, ...BASE_STOCK_TEMPLATE })))
    .returning()
  return rows
}
