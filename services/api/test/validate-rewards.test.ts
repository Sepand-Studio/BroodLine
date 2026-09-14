import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { validateBundle } from '../src/config/validate.ts'

// fileURLToPath (not .pathname) so a space anywhere in the path - as in this
// very repo's parent directory - is decoded rather than left as a literal
// %20. Same convention as config.test.ts.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const FIX = (name: string) => join(REPO, 'services/api/test/fixtures', name)

describe('wave reward validation', () => {
  it('refuses a wave with no reward', async () => {
    const v = await validateBundle(FIX('missing-wave-reward'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/wave 6.*reward/i)
  }, 120_000)

  it('refuses a reward of zero', async () => {
    const v = await validateBundle(FIX('zero-wave-reward'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/wave 6.*reward/i)
  }, 120_000)
})
