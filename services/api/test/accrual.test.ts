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
   * Both timestamps are exact multiples of a day, so the floor difference
   * carries no phase and the assertion is on the RATE rather than on where
   * a boundary happened to fall.
   */
  const perDay = (ratePerHour: number, arrayTier: number) =>
    baseStockFor({ lastSettledAt: new Date(0), now: new Date(12 * HOUR), ratePerHour, arrayTier, remaining: null })
    + baseStockFor({ lastSettledAt: new Date(12 * HOUR), now: new Date(DAY), ratePerHour, arrayTier, remaining: null })

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
    for (let i = 0; i < 96; i++) {
      total += baseStockFor({
        lastSettledAt: new Date(i * 30 * 60_000), now: new Date((i + 1) * 30 * 60_000),
        ratePerHour: 20, arrayTier: 1, remaining: null,
      })
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
    })).toBe(0)
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
    })).toBe(0)     // floor(100 / 480)

    // ...and a node with enough left to cover a whole creature does pay it.
    expect(baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 60, arrayTier: 1, remaining: 500,
    })).toBe(1)     // floor(500 / 480)
  })

  it('refuses a Harvest Array tier the base-stock table does not calibrate', () => {
    // The same refusal shardMultiplierHundredths makes, and the two tables
    // must stay key-for-key identical: `baseStockFor` divides one by the
    // other on the clipped branch.
    expect(() => baseStockFor({
      lastSettledAt: new Date(0), now: new Date(DAY),
      ratePerHour: 20, arrayTier: 2, remaining: null,
    })).toThrow(/base-stock multiplier for Harvest Array tier 2/)
  })
})
