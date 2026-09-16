/**
 * Units earned on a node since it was last settled - a pure function of two
 * timestamps and the node's current rate, never a clock read. That is what
 * makes design §4.2's twelve-hour offline cap, and the no-drift property the
 * ledger credit depends on, testable in milliseconds instead of by waiting
 * on a real clock. `solo_execution` §5.5: "never tick players."
 */

const HOUR_MS = 3_600_000
const MAX_OFFLINE_MS = 12 * HOUR_MS

export interface AccrueArgs {
  lastSettledAt: Date
  now: Date
  ratePerHour: number
  arrayTier: number
  remaining: number | null
}

/**
 * Harvest Array tier -> shard-rate multiplier, in hundredths (tier 1 = 100
 * means 1.00x). Calibrated points, from `economy_model`'s Harvest Array
 * scaling table: T1 = 1.00x, T4 = 1.35x, T8 = 2.00x, T12 = 3.00x.
 *
 * Not specified by the brief, which leaves this helper's body to be filled
 * in. Choice made here: Phase 6 pins every Ark's Harvest Array at tier 1 -
 * no facility upgrades ship this phase (`phase6_loop` §3.3) - so tier 1 is
 * the only value `accrue` is ever actually called with today. The other
 * three tiers are recorded because they are real numbers out of the design
 * docs, not because this phase reaches them. Every OTHER tier has no
 * specified multiplier anywhere and is refused rather than interpolated or
 * guessed - a wrong guess here is a currency bug, not a cosmetic one.
 *
 * EXPORTED ONLY so loop-schema.test.ts can probe it directly. That test
 * diffs this table against 0005_loop.sql's `harvest_array_tier_calibrated`
 * CHECK, which is the one thing keeping the two from drifting, and probing
 * it through `accrue` instead would be probing the wrong function: accrue
 * returns 0 on a non-positive interval BEFORE it ever reaches this lookup,
 * so a reordering there would quietly turn the gate into "accepts
 * everything". `rosterCap` and `maxGeneration` - the other two halves of
 * the same pattern - are already exported, and the gate treats all three
 * identically because they ARE the same thing three times.
 */
export function shardMultiplierHundredths(arrayTier: number): number {
  const knownTiers: Record<number, number> = { 1: 100, 4: 135, 8: 200, 12: 300 }
  const mult = knownTiers[arrayTier]
  if (mult === undefined) throw new Error(`no calibrated Harvest Array multiplier for tier ${arrayTier}`)
  return mult
}

/**
 * Integer throughout, and via BigInt rather than `number`: a realistic
 * absolute timestamp (~1.8e12 ms today) times a rate and a hundredths
 * multiplier can exceed 2^53 and silently lose precision as a float, which
 * is exactly the currency bug this function exists to rule out.
 *
 * Computed as the DIFFERENCE OF TWO FLOORS against absolute timestamps -
 * floor(now * rate / HOUR) - floor(effectiveLastSettled * rate / HOUR) -
 * rather than flooring a single elapsed duration. That telescoping is what
 * makes summing many short, adjacent intervals equal one long interval:
 * each interval's fractional remainder is carried forward into the next
 * call's absolute-timestamp floor instead of being thrown away every time.
 * A per-call `floor(elapsedMs * rate / HOUR)` looks equally "integer", but
 * at 20/hour a one-minute call is `floor(1/3) = 0` on EVERY call - 720 of
 * them sum to 0 rather than the 12-hour total of 240. That is real drift
 * hiding behind integer division, and it is what this shape avoids.
 *
 * Order matters (design §4.2): the twelve-hour offline cap is applied to
 * the EFFECTIVE `lastSettledAt` first, so it participates in the floor
 * difference like any other timestamp and the telescoping property still
 * holds; the node's `remaining` yield bounds the result last. Reversed, a
 * long absence against a nearly-dead node reports units the node cannot
 * supply, and the credit would be written before anything noticed.
 *
 * `remaining` can itself arrive negative - `node_depletion.harvested_units`
 * carries no CHECK against a node's `total_yield`, so two concurrent claims
 * against a nearly-dead node can overshoot it, and the next caller computes
 * a negative `remaining`. `Math.min(units, remaining)` alone would return
 * that negative number unchanged: a negative credit against a currency
 * ledger, reported far from where the real bug (the missing overshoot
 * guard upstream) lives - the exact class of problem the non-positive-
 * interval guard above exists for. Floored at zero for the same reason.
 */
export function accrue(a: AccrueArgs): number {
  const nowMs = a.now.getTime()
  const effectiveLastSettledMs = effectiveFrom(a)
  if (nowMs <= effectiveLastSettledMs) return 0 // clock skew, or a non-positive interval, pays nothing

  const rate = BigInt(a.ratePerHour)
  const mult = BigInt(shardMultiplierHundredths(a.arrayTier))

  const units = floorDifference(effectiveLastSettledMs, nowMs, rate * mult, BigInt(HOUR_MS) * 100n)

  return a.remaining === null ? units : Math.max(0, Math.min(units, a.remaining))
}

/** The twelve-hour cap, applied to `lastSettledAt` - see accrue's comment on order. */
function effectiveFrom(a: AccrueArgs): number {
  return Math.max(a.lastSettledAt.getTime(), a.now.getTime() - MAX_OFFLINE_MS)
}

/**
 * `floor(to * numer / denom) - floor(from * numer / denom)`, in BigInt.
 *
 * The telescoping shape accrue's comment above argues for.
 *
 * SHARDS ONLY, as of 0007. Base stock used to share it, and that was the
 * bug: dividing by 480 units a creature turns this denominator into a
 * wall-clock grid - 24 hours wide for the Common Vein, 8 for the Rich
 * Deposit - so a grant depended on whether a window happened to straddle an
 * absolute boundary rather than on how long it was. Shards have no such
 * problem because a shard IS the unit; there is no second division to turn
 * the remainder into a grid. Base stock now carries its remainder in a
 * column instead - see baseStockFor.
 */
function floorDifference(fromMs: number, toMs: number, numer: bigint, denom: bigint): number {
  return Number((BigInt(toMs) * numer) / denom - (BigInt(fromMs) * numer) / denom)
}

/**
 * Shard units of harvest that buy one Gen-1 base-stock creature.
 *
 * PROVISIONAL, in the Phase 5 sense the plan's Values table uses: a real
 * number chosen against a stated property and owed to a content document
 * for ratification, not invented to fill a blank. Owed to
 * `broodline_base_stock.md` 3 alongside the node rates design 11 already
 * books against `broodline_region_roster.md`.
 *
 * DERIVED, from numbers that are authored:
 *   - The twelve-hour offline cap means a player claiming both nodes at the
 *     cap twice a day accrues 24h of yield on each - 24 x (20 + 60) = 1,920
 *     units a day at Harvest Array tier 1 (`nodes.json`, bundle 0.1.2).
 *   - `base_stock` 3 puts a Core player's NODE-SOURCED supply at 4.0
 *     creatures a day.
 *   - 1,920 / 4 = 480.
 *
 * WHAT THE ANCHOR DOES NOT REPRODUCE, stated rather than papered over:
 * `base_stock` 3's spread is 1.0 / 4.0 / 8.0 across Casual / Core /
 * Optimiser, and against this phase's map only the Core figure comes out
 * right (Casual, claiming once a day, gets 2.0; an Optimiser cannot beat
 * Core, because two claims a day already saturate the twelve-hour cap on
 * both of the only two nodes that exist). That spread is bought with map
 * access - relocation, better ground, Apex Veins - and this phase ships one
 * region and no relocation (design 4.1). Calibrating the constant to
 * recover a spread the map cannot express would mean mispricing the one
 * archetype that IS expressible.
 */
const UNITS_PER_CREATURE = 480

/**
 * Harvest Array tier -> base-stock multiplier, in hundredths.
 * `base_stock` 3.1's table, verbatim: 1.00x / 1.18x / 1.50x / 2.00x against
 * the shard table's 1.00x / 1.35x / 2.00x / 3.00x - "half the rate shards
 * do", because shards are throughput and can spread widely while base stock
 * is species, and species are counters.
 *
 * Refuses an uncalibrated tier for the same reason
 * shardMultiplierHundredths does, and the two tables MUST keep the same
 * key set: `baseStockFor` divides one by the other, so a tier calibrated in
 * one and not the other is a division by an undefined value rather than a
 * refusal. 0005_loop.sql's `harvest_array_tier_calibrated` CHECK is what
 * stops a row reaching either of them with a tier neither knows.
 */
function baseStockMultiplierHundredths(arrayTier: number): number {
  const knownTiers: Record<number, number> = { 1: 100, 4: 118, 8: 150, 12: 200 }
  const mult = knownTiers[arrayTier]
  if (mult === undefined) throw new Error(`no calibrated base-stock multiplier for Harvest Array tier ${arrayTier}`)
  return mult
}

/**
 * Gen-1 creatures earned on a node over the same window `accrue` pays
 * shards for - design 4.3's second write, and the supply line design 2.4
 * makes the difference between a loop that closes and one that seizes.
 *
 * THE REMAINDER IS CARRIED IN A COLUMN, not recovered from the phase of an
 * absolute floor grid - 0007, and the reason is a real bug rather than
 * taste.
 *
 * A count derived by flooring one claim's `units` throws the remainder away
 * every time, which at 480 units a creature and a twelve-hour cap means the
 * Common Vein - largest possible single claim 12 x 20 = 240 units - grants
 * base stock NEVER, at any cadence, forever. That is why this used to
 * telescope over absolute timestamps the way `accrue` still does.
 *
 * But `accrue`'s denominator is an hour and this one's is 480 units, and
 * that difference is the whole problem: dividing by 480 turns the grid into
 * 24 hours wide for the Common Vein and 8 for the Rich Deposit, so a claim
 * granted a creature if and only if its window crossed one of those
 * ABSOLUTE boundaries. Measured: six hours of Common Vein granted 1 creature
 * at 00:15 UTC and 0 at 21:15. Two players harvesting identical windows got
 * different counts; a player claiming at 23:59 and again at 00:01 could be
 * handed a whole creature for two minutes of harvesting; and the long-run
 * rate was only right while a player kept claiming inside the twelve-hour
 * cap, because going past it breaks the chain and leaves nothing but the
 * wall-clock dependence.
 *
 * Carrying the remainder keeps the property the telescoping was chosen for -
 * two twelve-hour Common Vein claims in a day still grant exactly the one
 * creature a day the rate says they should - and drops the grid. The result
 * is a pure function of units actually paid.
 *
 * `nodeType` is absent for a different reason: it selects the SPECIES
 * (`base_stock` 4.2's region weighting), not the count, and species
 * selection lives with the grant in roster/creatures.ts.
 *
 * THE DEPLETION BOUND NEEDS NO SPECIAL BRANCH ANY MORE. `accrue` already
 * clamps a dry node's payout, and base stock is earned on shards actually
 * harvested, so a clipped window simply contributes less to the carry. The
 * two branches this function used to have - one telescoping, one flooring
 * `paid` - disagreed with each other; there is now one rule for both.
 */
export interface BaseStock {
  /** Whole creatures this claim earns. */
  creatures: number
  /**
   * What to persist for the next claim - always strictly below one
   * creature's worth, because anything at or above it was converted above.
   */
  carried: number
}

export function baseStockFor(a: AccrueArgs, carried: number): BaseStock {
  // The base-stock table is consulted FIRST so that an uncalibrated tier is
  // refused by the table this function owns rather than by the shard table
  // it happens to share a key set with - otherwise that guard is
  // unreachable, and an edit that dropped a key from one table only would
  // be caught by nothing.
  const baseMult = BigInt(baseStockMultiplierHundredths(a.arrayTier))
  const shardMult = BigInt(shardMultiplierHundredths(a.arrayTier))

  // CARRIED IN BASE-STOCK NUMERATOR UNITS - `paid x baseMult` - rather than
  // in shards, so the tier ratio stays exact. Converting to shards first
  // would divide by `shardMult` on every claim and lose a remainder to the
  // very rounding this carry exists to remove. At tier 1 both multipliers
  // are 100, so the threshold is the plain 480 units a reader expects.
  const threshold = BigInt(UNITS_PER_CREATURE) * shardMult
  const total = BigInt(carried) + BigInt(accrue(a)) * baseMult

  return {
    creatures: Number(total / threshold),
    carried: Number(total % threshold),
  }
}

