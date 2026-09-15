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
 */
function shardMultiplierHundredths(arrayTier: number): number {
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
  const effectiveLastSettledMs = Math.max(a.lastSettledAt.getTime(), nowMs - MAX_OFFLINE_MS)
  if (nowMs <= effectiveLastSettledMs) return 0 // clock skew, or a non-positive interval, pays nothing

  const rate = BigInt(a.ratePerHour)
  const mult = BigInt(shardMultiplierHundredths(a.arrayTier))
  const denom = BigInt(HOUR_MS) * 100n

  const upper = (BigInt(nowMs) * rate * mult) / denom
  const lower = (BigInt(effectiveLastSettledMs) * rate * mult) / denom
  const units = Number(upper - lower)

  return a.remaining === null ? units : Math.max(0, Math.min(units, a.remaining))
}
