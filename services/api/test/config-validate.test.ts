import { cp, mkdtemp, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { validateBundle } from '../src/config/validate.ts'
import { REQUIRED_NODE_IDS } from '../src/map/rotation.ts'

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

describe('node rates', () => {
  // map/accrual.ts's accrue() (Task 4) converts a node's ratePerHour to a
  // BigInt; a fractional rate throws a cryptic RangeError deep inside a
  // claim instead of failing here, at publish time. Fix round 1's finding.
  //
  // No fixture in this file carries a nodes.json - none was ever given one,
  // because every one of them predates config/bundle.ts reading the file at
  // all (Task 5, bundle.ts:119; this comment claimed that had not happened
  // for three tasks after it did). Adding a dedicated fixture directory here
  // would mean duplicating six files just to change a seventh. A scratch copy of the real, valid 0.1.2
  // bundle with only nodes.json swapped gets the same isolation more
  // cheaply - and it never touches config/bundles/0.1.2 itself, which must
  // stay immutable (config/store.ts's whole argument). Same `cp(src, dest,
  // { recursive: true })` call LocalBundleStore.putBundle already uses.
  it('refuses a non-integer node rate', async () => {
    const dir = await mkdtemp(join(tmpdir(), 'broodline-bundle-'))
    try {
      await cp(BUNDLE_0_1_2, dir, { recursive: true })
      await writeFile(join(dir, 'nodes.json'), JSON.stringify([
        { id: 'common_vein', ratePerHour: 20.5, totalYield: null, multiplier: 1 },
        { id: 'rich_deposit', ratePerHour: 60, totalYield: 8640, multiplier: 3 },
      ]))

      const v = await validateBundle(dir)
      expect(v).toHaveLength(1)
      expect(v[0]).toMatch(/ratePerHour/)
    } finally {
      await rm(dir, { recursive: true, force: true })
    }
  }, 120_000)

  it('accepts the real bundle 0.1.2 node rates unchanged - the positive control', async () => {
    // Without this, the test above could not tell "refuses a bad rate" apart
    // from "refuses every bundle that carries a nodes.json at all".
    const dir = await mkdtemp(join(tmpdir(), 'broodline-bundle-'))
    try {
      await cp(BUNDLE_0_1_2, dir, { recursive: true })
      expect(await validateBundle(dir)).toEqual([])
    } finally {
      await rm(dir, { recursive: true, force: true })
    }
  }, 120_000)
})

describe('node identity', () => {
  /**
   * THE SHARPER HALF OF THE SAME CHECK, and the one the per-task reviews
   * could not see: Task 4 wrote `nodesFor`, which looks these ids up with a
   * non-null assertion, and Task 5 wrote `nodeSet`, which guards only
   * `nodes.length === 0`. Each assumed the other checked that the ids
   * themselves exist, so ONE typo - `rich_desposit` - validated clean and
   * then threw `TypeError: Cannot read properties of undefined (reading
   * 'ratePerHour')` out of `nodesFor`, as a 500 on GET /v1/region/state and
   * on the claim route, for every player on the server, until someone rolled
   * the bundle back.
   *
   * Driven off REQUIRED_NODE_IDS rather than two hand-written cases, for the
   * same reason the validator imports it: adding a third required node must
   * extend this test by construction, not by somebody remembering to.
   */
  const withNodes = async (nodes: unknown[]): Promise<string[]> => {
    const dir = await mkdtemp(join(tmpdir(), 'broodline-bundle-'))
    try {
      // Same scratch copy of the real, valid 0.1.2 bundle as above - so the
      // only thing that can be wrong with what validateBundle sees is the
      // nodes.json this writes, and config/bundles/0.1.2 is never touched.
      await cp(BUNDLE_0_1_2, dir, { recursive: true })
      await writeFile(join(dir, 'nodes.json'), JSON.stringify(nodes))
      return await validateBundle(dir)
    } finally {
      await rm(dir, { recursive: true, force: true })
    }
  }

  /** Every required node, authored correctly - the shape each case mutates. */
  const complete = () => REQUIRED_NODE_IDS.map((id) => (
    { id, ratePerHour: 20, totalYield: null, multiplier: 1 }))

  for (const missing of REQUIRED_NODE_IDS) {
    it(`refuses a bundle whose nodes.json omits '${missing}'`, async () => {
      const v = await withNodes(complete().filter((n) => n.id !== missing))
      expect(v).toHaveLength(1)
      expect(v[0]).toContain(missing)
    }, 120_000)
  }

  it("refuses a typo'd id rather than reading it as a new node", async () => {
    // The reproduction verbatim. A typo is not a missing node from the
    // author's point of view - the file still has two entries - so a check
    // that counted nodes rather than naming them would pass this.
    const v = await withNodes(complete().map((n) => (
      n.id === 'rich_deposit' ? { ...n, id: 'rich_desposit' } : n)))
    expect(v).toHaveLength(1)
    expect(v[0]).toContain('rich_deposit')
  }, 120_000)

  it('accepts a nodes.json that authors every required id - the positive control', async () => {
    // Without this, the cases above would be satisfied by a check that
    // refused every nodes.json, and the suite could not tell the two apart.
    expect(await withNodes(complete())).toEqual([])
  }, 120_000)
})
