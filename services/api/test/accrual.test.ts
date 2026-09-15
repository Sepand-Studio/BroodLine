import { describe, expect, it } from 'vitest'
import { accrue } from '../src/map/accrual.ts'

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
})
