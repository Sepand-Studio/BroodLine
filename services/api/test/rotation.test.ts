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
  it('returns the same node set for the same epoch', () => {
    const a = nodesFor(1, 'defile', 12n, 99n, bundle)
    const b = nodesFor(1, 'defile', 12n, 99n, bundle)
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
})
