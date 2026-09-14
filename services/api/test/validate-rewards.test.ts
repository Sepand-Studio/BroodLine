import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { validateBundle } from '../src/config/validate.ts'

// fileURLToPath (not .pathname) so a space anywhere in the path - as in this
// very repo's parent directory - is decoded rather than left as a literal
// %20. Same convention as config.test.ts.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const FIX = (name: string) => join(REPO, 'services/api/test/fixtures', name)

// WHY BOTH FIXTURES PIN `minimumClientVersion: "0.1.0"` IN THEIR manifest.json,
// and why that is deliberate rather than copy-paste.
//
// Both tests below assert `toHaveLength(1)` - EXACTLY the reward violation and
// nothing else. validateBundle runs six checks over the directory, so every
// other file in the fixture must validate CLEANLY or the length assertion
// fails for a reason that has nothing to do with rewards. validateManifest
// (src/config/validate.ts) rejects a manifest whose minimumClientVersion is
// absent or not MAJOR.MINOR.PATCH, so the field has to be present and
// well-formed here. It is pinned, not derived, because THERE IS NOTHING TO
// DERIVE IT FROM: the real manifests (config/bundles/0.1.0, 0.1.1) each carry
// the literal too, and no shared constant for it exists anywhere in the repo -
// validate.ts only checks the SHAPE, via VERSION_PATTERN.
//
// The value is otherwise inert: validateBundle takes a directory and compares
// this field against nothing. That inertness is the thing to re-check if it
// ever stops being true. 0.1.0 has been RETIRED as a bootstrap target
// (commit 8c1180f; see config.test.ts's SEED comment for the full account), so
// the day a manifest check validates minimumClientVersion against the set of
// live or retired versions - rather than merely against a regex - these two
// fixtures start failing on a SECOND violation, and the fix is to bump this
// field to whatever the then-current floor is, NOT to relax toHaveLength(1).
//
// The other seven fixtures under test/fixtures/ pin the same literal for the
// same reason; only these two are in this phase's ledger.

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
