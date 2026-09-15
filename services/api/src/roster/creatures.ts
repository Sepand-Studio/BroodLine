import { createHash } from 'node:crypto'
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
 * The Gen-1 creatures a base-stock grant can mint, one entry per species.
 *
 * THREE SPECIES, AND THE NUMBER IS CONTENT RATHER THAN A CHOICE. Bible 1.2
 * gives every species exactly two combat traits; bundle 0.1.2's
 * `traits.json` authors four traits across three species (Chill/Pale,
 * Splash/Ember, Taunt/Vetch, Carapace/Vetch). The other three species -
 * Skitter, Hollow, Loam - have no authored trait at all here, so a grant of
 * one would have to invent both of its slots.
 *
 * WHY NOT VETCH ALONE, which is what the first version of this shipped:
 * this function is the ONLY `insert(creatures)` in `src/`, and
 * `starter.json` grants currency only - so single-species base stock makes
 * every obtainable creature a Vetch, and design 5.2's body choice (the
 * child takes EITHER parent's species) has no reachable input. A rule that
 * can only be exercised by hand-writing rows is a rule nothing tests.
 *
 * WHERE EACH FIELD COMES FROM:
 *   species      - engine `Species`, and `traits.json`'s `species` field.
 *   trait 1      - the species' own authored counter (`traits.json`).
 *   trait 2      - Carapace for every species, and this is THE ONE STAND-IN
 *                  here. Vetch genuinely authors it. Pale's and Ember's real
 *                  second traits ARE authored - `combat_numbers` 4.3 gives
 *                  Pale **Screen** and `species_stats` 2 gives Ember
 *                  **Cinder** - but the engine's `Trait` enum is
 *                  `{None, Chill, Taunt, Splash, Carapace}` and cannot
 *                  represent either, and Task 6 reads `dominant` off
 *                  `traits.json`, so a trait absent from that file makes the
 *                  splice roll undefined. Carapace is chosen over the other
 *                  two candidates because it is the bundle's only trait with
 *                  `counters: null`: a stand-in that answers no raider
 *                  cannot hand Pale or Ember a counter their authored pair
 *                  never gave them, which is the only property of this
 *                  substitution that could change combat.
 *   tier 1/2     - Tier I. Gen-1; `combat_numbers` 7 ties the coverage
 *                  ceiling to generation, and design 5.3 derives Tier I as
 *                  the floor coverage never drops below.
 *   hpCurrent    - engine `Stats.CreatureHp`, mirrored in
 *                  test/replay-format.ts's CREATURE_HP.
 *
 * OWED, and marked the way `UNITS_PER_CREATURE` is: `base_stock` 4.2 weights
 * node-sourced species by REGION (30/30 for the region's two, 10 each for
 * the other four). That needs per-region content for thirty regions which
 * design 11 already books as owed, and this phase ships one region - so the
 * roll below is uniform over what exists. Bible 1.4's per-species Instinct
 * weighting is owed in the same way and deliberately NOT guessed: every
 * grant carries Vanguard, the one Instinct the engine's own Vetch fixture
 * supplies (tests/engine/Combat/GoldenTests.cs).
 */
const BASE_STOCK_SPECIES = [
  { species: 'Vetch', trait1: 'Taunt', trait2: 'Carapace', hpCurrent: 260 },
  { species: 'Pale', trait1: 'Chill', trait2: 'Carapace', hpCurrent: 120 },
  { species: 'Ember', trait1: 'Splash', trait2: 'Carapace', hpCurrent: 130 },
] as const

export type BaseStockSpecies = (typeof BASE_STOCK_SPECIES)[number]

/** Every species a base-stock grant can mint - exported for tests. */
export const baseStockSpecies: readonly BaseStockSpecies[] = BASE_STOCK_SPECIES

/**
 * Which species a given grant mints - a pure function of a seed string, so
 * the roll is reproducible rather than a `Math.random()` nobody can
 * re-derive after a dispute. Same discipline design 5.1 applies to the
 * splice, for the same reason: this is a grant a player can complain about.
 *
 * SHA-256 rather than a hand-rolled mixer because the input is structured
 * and adjacent (the same key with a different index, one claim to the next),
 * and a weak mix over adjacent inputs correlates in exactly the way that
 * would make a claim granting three creatures give the same species three
 * times. The modulo is biased by about one part in 1.4 billion over three
 * species out of 2^32, which is far below anything this distribution's
 * provisional status could justify correcting.
 */
export function speciesForSeed(seed: string): BaseStockSpecies {
  const digest = createHash('sha256').update(seed).digest()
  return BASE_STOCK_SPECIES[digest.readUInt32BE(0) % BASE_STOCK_SPECIES.length]!
}

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
  tx: Tx, serverId: number, playerId: string, n: number, seed: string,
): Promise<CreatureRow[]> {
  if (n <= 0) return []
  return tx.insert(creatures)
    .values(Array.from({ length: n }, (_unused, i) => ({
      serverId,
      playerId,
      generation: 1,
      tier1: 1,
      tier2: 1,
      instinct: 'Vanguard',
      // Founders are granted deliberately and named; base stock is neither.
      // 0005's only_founders_named refuses a named non-Founder outright.
      isFounder: false,
      name: null,
      // The INDEX is in the seed, so a claim granting three creatures rolls
      // three times rather than minting one species three times.
      ...speciesForSeed(`${seed}:${i}`),
    })))
    .returning()
}
