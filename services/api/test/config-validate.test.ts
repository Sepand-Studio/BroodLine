import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { validateBundle } from '../src/config/validate.ts'

// fileURLToPath (not .pathname) so a space anywhere in the path - as in this
// very repo's parent directory - is decoded rather than left as a literal
// %20. Same convention as config.test.ts and validate-rewards.test.ts.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const FIX = (name: string) => join(REPO, 'services/api/test/fixtures', name)
const BUNDLE_0_1_2 = join(REPO, 'config/bundles/0.1.2')

describe('trait dominance', () => {
  // design §5.3: the rolled slot in a splice carries at full coverage if the
  // trait is dominant and one tier lower if recessive. A trait authored with
  // no `dominant` flag makes that roll undefined, and an undefined roll on a
  // paid action (splicing spends splice_charges) is the failure this rule
  // exists to make unshippable.
  it('refuses a bundle whose traits lack a dominance flag', async () => {
    const v = await validateBundle(FIX('missing-trait-dominance'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/dominance/i)
  }, 120_000)
})

describe('wave reward distinctness', () => {
  // Not a general rule about content - a guard on weakenings.md row 5. The
  // reward-inflation property that row is owed is only testable once two
  // authored waves carry DIFFERENT rewards; a bundle that quietly equalised
  // them would let a future gate turn green while proving nothing.
  it('refuses two authored waves carrying the same reward', async () => {
    const v = await validateBundle(FIX('duplicate-wave-rewards'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/distinct reward/i)
  }, 120_000)
})

describe('bundle 0.1.2', () => {
  it('accepts 0.1.2', async () => {
    expect(await validateBundle(BUNDLE_0_1_2)).toEqual([])
  }, 120_000)
})
