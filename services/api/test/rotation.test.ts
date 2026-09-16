import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { epochFor, nodesFor, type Bundle } from '../src/map/rotation.ts'

// fileURLToPath (not .pathname) so a space anywhere in the path - as in this
// repo's own directory name - does not get percent-encoded. Same pattern as
// config-validate.test.ts's REPO/BUNDLE_0_1_2 constants.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const BUNDLE_0_1_2 = join(REPO, 'config/bundles/0.1.2')

// Real bundle 0.1.2 nodes.json, not hardcoded rates - it carries common_vein
// at 20/hour with no total yield, and rich_deposit at 60/hour with a total
// yield of 8640.
const bundle: Bundle = { nodes: JSON.parse(readFileSync(join(BUNDLE_0_1_2, 'nodes.json'), 'utf8')) }

describe('nodesFor', () => {
  it('returns the same node set for the same epoch, from independently-parsed bundle data', () => {
    // TWO SEPARATE JSON.parse calls, not the same bundle object passed
    // twice - reusing one reference could only ever catch nodesFor
    // returning a cached object from its first call, not prove it
    // recomputes the same ANSWER from the same VALUES. (Reported as
    // near-vacuous in fix round 1; weakening G - Math.random() injected
    // into a returned field - did redden this test as originally written,
    // so it was not proving nothing, but this version proves more: the
    // result is a function of bundle CONTENT, not of bundle IDENTITY.)
    const raw = readFileSync(join(BUNDLE_0_1_2, 'nodes.json'), 'utf8')
    const bundleA: Bundle = { nodes: JSON.parse(raw) }
    const bundleB: Bundle = { nodes: JSON.parse(raw) }
    expect(bundleA).not.toBe(bundleB)
    expect(bundleA.nodes).not.toBe(bundleB.nodes)

    const a = nodesFor(1, 'defile', 12n, 99n, bundleA)
    const b = nodesFor(1, 'defile', 12n, 99n, bundleB)
    expect(a).toEqual(b)
  })

  it('always includes a Common Vein, in every epoch', () => {
    // bible §5.3: the livable floor, and it can never be taken from anyone.
    // With one region and no relocation this is also what stops the loop
    // seizing when the Rich Deposit runs dry - design §4.1.
    for (let e = 0n; e < 60n; e++)
      expect(nodesFor(1, 'defile', e, 99n, bundle).some(n => n.type === 'common_vein')).toBe(true)
  })
})

describe('epochFor', () => {
  it('advances the epoch exactly once per week at the server tick', () => {
    const server = { tickDayOfWeek: 3, tickMinuteOfDay: 600 }
    const before = epochFor(server, new Date('2026-09-16T09:59:00Z'))
    const after  = epochFor(server, new Date('2026-09-16T10:01:00Z'))
    expect(after).toBe(before + 1n)
    expect(epochFor(server, new Date('2026-09-22T09:59:00Z'))).toBe(after)
  })

  it('handles a Thursday-tick server, where the reference tick lands after the epoch origin', () => {
    // 1970-01-01T00:00:00Z is itself a Thursday. A Thursday-tick server with
    // a nonzero tickMinuteOfDay is the one case where the tick-on-the-
    // origin's-own-calendar-day candidate lands AFTER the origin instant -
    // exercising the `referenceTick > 0` step-back-a-week branch in
    // epochFor, which fix round 1 flagged as correct but uncovered by every
    // other test in this file (all of which use tickDayOfWeek: 3).
    const server = { tickDayOfWeek: 4, tickMinuteOfDay: 600 }
    const before = epochFor(server, new Date('2026-09-17T09:59:00Z')) // Thursday
    const after  = epochFor(server, new Date('2026-09-17T10:01:00Z'))
    expect(after).toBe(before + 1n)
    expect(epochFor(server, new Date('2026-09-24T09:59:00Z'))).toBe(after)
  })

  it('anchors a Thursday-tick server at epoch 0 for the Unix epoch itself, not -1', () => {
    // The test above is BLIND to the `referenceTick > 0` branch: stepping
    // the reference tick back by exactly one week shifts EVERY epoch number
    // by the same constant, which no before/after/one-week-later comparison
    // can detect (confirmed directly - disabling the branch left the test
    // above green). This is the one assertion in the file that pins an
    // ABSOLUTE epoch value rather than a relative one, at the boundary
    // where the branch's effect is actually observable: for a Thursday-tick
    // server the un-stepped-back candidate (1970-01-01T10:00:00Z) is AFTER
    // the Unix epoch instant, so without stepping it back a full week, "the
    // first tick at or before the origin" would actually be after it -
    // putting the origin itself one epoch too early.
    expect(epochFor({ tickDayOfWeek: 4, tickMinuteOfDay: 600 }, new Date(0))).toBe(0n)
  })
})
