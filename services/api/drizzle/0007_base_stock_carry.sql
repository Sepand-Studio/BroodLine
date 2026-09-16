-- Base stock stops depending on what time of day it is.
--
-- THE BUG. `baseStockFor` derived creatures the same way `accrue` derives
-- shards: a telescoping difference of two ABSOLUTE floors,
-- floor(now * rate / perCreature) - floor(from * rate / perCreature). That
-- shape is exactly right for shards, because a shard is a continuous
-- currency and the fractional remainder genuinely carries forward into the
-- next call's floor - which is what stops 720 one-minute claims summing to
-- less than one twelve-hour claim.
--
-- Applied to CREATURES it does something else. Dividing by 480 units per
-- creature turns the denominator into a wall-clock grid: 24 hours wide for
-- the Common Vein at 20/hour, 8 hours for the Rich Deposit at 60/hour. A
-- claim then grants a creature if and only if its window happens to cross
-- one of those absolute boundaries, so the answer depends on WHEN the
-- window sits rather than on how long it is. Measured:
--
--   6h of Common Vein  -> 1 creature at 00:15 UTC, 0 creatures at 21:15
--   12h of Rich Deposit -> 2 creatures at 00:15 UTC, 1 creature at 21:15
--
-- Two players harvesting identical windows got different counts, a player
-- claiming at 23:59 and again at 00:01 could be granted a whole creature
-- for two minutes of harvesting, and the long-run rate was only correct
-- while a player kept claiming inside the twelve-hour cap - past it the
-- chain breaks and the wall-clock dependence is all that is left. It also
-- made the suite flaky by the clock: `loop.test.ts` and `node-claim.test.ts`
-- both assert deterministic grant counts and both fail when run within a
-- few hours of a boundary.
--
-- THE FIX. Carry the remainder in a column instead of in the phase of an
-- absolute grid. A claim now takes the units it actually paid, adds what
-- the previous claim left over, converts whole creatures out of the total
-- and stores the rest:
--
--   total     = carried + paid * baseMult
--   creatures = total / (480 * shardMult)
--   carried   = total % (480 * shardMult)
--
-- The units are base-stock NUMERATOR units, not shards: carrying `paid`
-- itself would mean dividing by the shard multiplier on every claim and
-- losing a remainder to the very rounding this column exists to remove. At
-- Harvest Array tier 1 both multipliers are 100, so one creature is 48,000
-- of these units - the plain 480 shards a reader expects, scaled by 100.
--
-- No drift, because nothing is discarded - the remainder is persisted
-- rather than recovered from a global origin. No wall-clock dependence,
-- because the result is a pure function of units paid. And the two branches
-- of `baseStockFor` agree: the clipped branch (a depleted node) already
-- worked from `paid`, and now the unclipped one does too.
--
-- `accrue` is UNCHANGED. Shards keep the telescoping difference, which is
-- correct for them and which this column does not touch.
--
-- Re-runnable, per 0005's stated property for this directory.
ALTER TABLE harvest_positions
  ADD COLUMN IF NOT EXISTS base_stock_carried bigint NOT NULL DEFAULT 0;

-- Never negative, and never a whole creature's worth: anything at or above
-- the conversion threshold should have been converted by the claim that
-- produced it. The upper bound is deliberately NOT the literal 480 - the
-- threshold is content (UNITS_PER_CREATURE, owed to broodline_base_stock.md
-- 3) and a CHECK naming it would have to move in the same migration as a
-- content change. Non-negative is the half that cannot drift.
ALTER TABLE harvest_positions
  DROP CONSTRAINT IF EXISTS base_stock_carried_non_negative;
ALTER TABLE harvest_positions
  ADD CONSTRAINT base_stock_carried_non_negative CHECK (base_stock_carried >= 0);
