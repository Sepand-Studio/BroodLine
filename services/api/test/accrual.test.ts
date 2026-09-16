import { describe, expect, it } from 'vitest'
import { accrue, baseStockFor } from '../src/map/accrual.ts'

const HOUR = 3_600_000

describe('accrue', () => {
  it('clamps to the twelve-hour offline cap', () => {
    // solo_execution §5.5. Three days away pays twelve hours, not three days.
    const units = accrue({
      lastSettledAt: new Date(0), now: new Date(72 * HOUR),
      ratePerHour: 20, arrayTier: 1, remaining: null,
    })
    expect(units).toBe(240)     // 12h x 20
  })

  it('clamps BEFORE bounding by remaining yield, not after', () => {
    // design §4.2 fixes the order. Reversed, a long absence against a nearly
    // dead node reports units the node cannot supply - and the credit would
    // be written before anything noticed.
    const units = accrue({
      lastSettledAt: new Date(0), now: new Date(72 * HOUR),
      ratePerHour: 60, arrayTier: 1, remaining: 100,
    })
    expect(units).toBe(100)     // min(12h x 60 = 720, 100)
  })

  it('does not drift when summed over many small intervals', () => {
    // The property that makes integer arithmetic non-negotiable. This feeds
    // credit(); a float losing a thousandth per claim compounds silently and
    // is unfalsifiable afterwards.
    let total = 0
    for (let i = 0; i < 720; i++)
      total += accrue({
        lastSettledAt: new Date(i * 60_000), now: new Date((i + 1) * 60_000),
        ratePerHour: 20, arrayTier: 1, remaining: null,
      })

    const oneShot = accrue({
      lastSettledAt: new Date(0), now: new Date(12 * HOUR),
      ratePerHour: 20, arrayTier: 1, remaining: null,
    })
    expect(total).toBe(oneShot)
  })

  it('pays nothing for a non-positive interval', () => {
    // Clock skew between instances is real. A negative interval must pay
    // zero rather than a negative credit, which credit() would reject far
    // from where the bug is.
    expect(accrue({
      lastSettledAt: new Date(HOUR), now: new Date(0),
      ratePerHour: 20, arrayTier: 1, remaining: null,
    })).toBe(0)
  })

  it('pays nothing when remaining has already gone negative', () => {
    // node_depletion.harvested_units carries no CHECK against total_yield,
    // so two concurrent claims on a nearly-dead node can overshoot it - the
    // next call's `remaining` then arrives negative. `Math.min(units,
    // remaining)` alone returns that negative number unchanged, which is a
    // negative credit against a currency ledger: the same class of hazard
    // the non-positive-interval guard above exists for, unguarded one line
    // later.
    expect(accrue({
      lastSettledAt: new Date(0), now: new Date(HOUR),
      ratePerHour: 20, arrayTier: 1, remaining: -5,
    })).toBe(0)
  })
})

const DAY = 24 * HOUR

describe('baseStockFor', () => {
  /**
   * One whole day of harvest, as the twelve-hour cap forces it to be taken:
   * two twelve-hour claims back to back. A SINGLE call cannot express a day
   * - `accrue`'s cap clamps it to twelve hours - so a test that passed
   * `new Date(0)` and `new Date(DAY)` would silently be measuring half a
   * day, which is exactly the mistake that wrote this helper.
   *
   * The timestamps used to have to be exact multiples of a day, because the
   * count came from a floor difference over absolute time and would
   * otherwise have depended on where a boundary fell. 0007 removed that:
   * the carry is threaded between the two claims below the way the column
   * threads it between two real ones, and the answer is now the same wherever
   * the window sits.
   */
  const perDay = (ratePerHour: number, arrayTier: number, from = 0) => {
    const first = baseStockFor(
      { lastSettledAt: new Date(from), now: new Date(from + 12 * HOUR), ratePerHour, arrayTier, remaining: null }, 0)
    const second = baseStockFor(
      { lastSettledAt: new Date(from + 12 * HOUR), now: new Date(from + DAY), ratePerHour, arrayTier, remaining: null },
      first.carried)
    return first.creatures + second.creatures
  }

  it('pays one Gen-1 creature per 24h of Common Vein harvest at tier 1', () => {
    // The ABSOLUTE pin on the constant, and it is absolute on purpose: a
    // relative assertion ("twice the window pays twice the creatures")
    // passes against any UNITS_PER_CREATURE at all, which is precisely the
    // constant most likely to be wrong. 480 units a creature against the
    // Common Vein's authored 20/hour (config/bundles/0.1.2/nodes.json) is
    // 24 hours, and `base_stock` 3's Core figure of 4.0 a day is what that
    // 480 was derived from - one from the Common Vein plus three from the
    // Rich Deposit is that 4.0, which is the pair of numbers below.
    expect(perDay(20, 1)).toBe(1)
    expect(perDay(60, 1)).toBe(3)
  })

  it('does not drift when summed over many small intervals', () => {
    // The reason this is a telescoping difference of floors rather than a
    // count derived from one claim`s `units`. At 480 units a creature, a
    // per-claim floor is zero for EVERY claim the Common Vein can produce -
    // its largest possible one is the twelve-hour cap at 240 units - so the
    // Common Vein would grant base stock never, at any cadence, forever.
    // 96 half-hour claims must sum to what the same 48 hours pays whole.
    let total = 0
    let carried = 0
    for (let i = 0; i < 96; i++) {
      const step = baseStockFor({
        lastSettledAt: new Date(i * 30 * 60_000), now: new Date((i + 1) * 30 * 60_000),
        ratePerHour: 20, arrayTier: 1, remaining: null,
      }, carried)
      total += step.creatures
      carried = step.carried
    }
    expect(total).toBe(2)   // 48h at one creature a day
  })

  it('scales with the Harvest Array at HALF the rate shards do', () => {
    // `base_stock` 3.1, and the direction is the whole point: tier 12 pays
    // 3.00x the shards and 2.00x the base stock over the SAME window.
    // Dividing by the shard multiplier as well as multiplying by the base
    // one - the symmetric-looking mistake - gives 0.67x here, and every
    // assertion that only compared tier 1 against itself would stay green.
    const window = { lastSettledAt: new Date(0), now: new Date(DAY), ratePerHour: 20, remaining: null }

    expect(accrue({ ...window, arrayTier: 1 })).toBe(240)     // 12h cap x 20
    expect(accrue({ ...window, arrayTier: 12 })).toBe(720)    // x 3.00

    expect(perDay(20, 1)).toBe(1)
    expect(perDay(20, 12)).toBe(2)   // x 2.00, not x 3.00 and not x 0.67
  })

  it('grants nothing from a node whose yield is exhausted', () => {
    // Base stock is earned on shards actually harvested, so a node that can
    // pay none pays no creatures either - however long the player was away.
    expect(baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 60, arrayTier: 1, remaining: 0,
    }, 0).creatures).toBe(0)
  })

  it('counts against what a dying node actually paid, not the whole window', () => {
    // 12h at 60/hour would be 720 units and 1 creature; the node holds 100.
    // Leaving the window-based count in place here would pay a creature for
    // ore the node could not supply.
    expect(accrue({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 60, arrayTier: 1, remaining: 100,
    })).toBe(100)
    expect(baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 60, arrayTier: 1, remaining: 100,
    }, 0).creatures).toBe(0)     // floor(100 / 480)

    // ...and a node with enough left to cover a whole creature does pay it.
    expect(baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 60, arrayTier: 1, remaining: 500,
    }, 0).creatures).toBe(1)     // floor(500 / 480)
  })

  it('grants the same count wherever the window sits in the day', () => {
    // THE 0007 BUG, and the assertion that would have caught it. The count
    // used to come from a telescoping difference of absolute floors, which
    // at 480 units a creature turns the denominator into a wall-clock grid -
    // 24 hours wide for the Common Vein, 8 for the Rich Deposit - so a claim
    // granted a creature if and only if its window straddled one of those
    // boundaries. Six hours of Common Vein paid 1 creature at 00:15 UTC and
    // 0 at 21:15, and every test in this file happened to use windows
    // anchored at multiples of a day, where the phase is always zero.
    //
    // Swept across a whole day at half-hour offsets: the answer must not
    // move. Both nodes, because their grids were different widths.
    for (const ratePerHour of [20, 60]) {
      const counts = new Set<number>()
      for (let offsetMinutes = 0; offsetMinutes < 24 * 60; offsetMinutes += 30) {
        const from = offsetMinutes * 60_000
        counts.add(baseStockFor({
          lastSettledAt: new Date(from), now: new Date(from + 6 * HOUR),
          ratePerHour, arrayTier: 1, remaining: null,
        }, 0).creatures)
      }
      expect(counts.size, `rate ${ratePerHour} moved with the time of day`).toBe(1)
    }

    // And the same for a full day's harvest, at every offset.
    for (let offsetMinutes = 0; offsetMinutes < 24 * 60; offsetMinutes += 30) {
      expect(perDay(20, 1, offsetMinutes * 60_000)).toBe(1)
      expect(perDay(60, 1, offsetMinutes * 60_000)).toBe(3)
    }
  })

  it('carries the remainder forward rather than discarding it', () => {
    // Six hours of Common Vein is 120 units against 480 to the creature, so
    // no single claim pays one - but four of them must, and the fourth is
    // where the carry proves it is being persisted rather than recovered
    // from a global origin.
    let carried = 0
    const counts: number[] = []
    for (let i = 0; i < 4; i++) {
      const step = baseStockFor({
        lastSettledAt: new Date(i * 6 * HOUR), now: new Date((i + 1) * 6 * HOUR),
        ratePerHour: 20, arrayTier: 1, remaining: null,
      }, carried)
      counts.push(step.creatures)
      carried = step.carried
    }
    expect(counts).toEqual([0, 0, 0, 1])
    expect(carried).toBe(0)
  })

  it('never carries a whole creature, so the column cannot hide one', () => {
    // The carry is what the NEXT claim starts from, so anything at or above
    // one creature's worth left in it is a creature the player earned and
    // was not given. Swept over a range of windows including ones far past
    // the twelve-hour cap.
    //
    // THE THRESHOLD IS 480 x shardMult, NOT 480. The carry is held in
    // base-stock numerator units - `paid x baseMult` - so that the tier
    // ratio stays exact without dividing (and losing a remainder) on every
    // claim. At tier 1 both multipliers are 100, so one creature is 48,000
    // of these units rather than 480 shards.
    const oneCreature = 480 * 100
    for (const hours of [0.5, 1, 6, 11.9, 12, 13, 48]) {
      const { carried } = baseStockFor({
        lastSettledAt: new Date(0), now: new Date(hours * HOUR),
        ratePerHour: 60, arrayTier: 1, remaining: null,
      }, 0)
      expect(carried, `${hours}h left a whole creature in the carry`).toBeLessThan(oneCreature)
      expect(carried).toBeGreaterThanOrEqual(0)
    }
  })

  it('refuses a Harvest Array tier the base-stock table does not calibrate', () => {
    // The same refusal shardMultiplierHundredths makes, and the two tables
    // must stay key-for-key identical: `baseStockFor` divides one by the
    // other on the clipped branch.
    expect(() => baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 20, arrayTier: 2, remaining: null,
    }, 0)).toThrow(/base-stock multiplier for Harvest Array tier 2/)
  })
})
