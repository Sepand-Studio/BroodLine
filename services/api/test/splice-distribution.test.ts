import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
  coverageLost, sampleSplice, spliceDistribution,
  type Distribution, type SpliceParent, type TraitRef, type TraitTable,
} from '../src/splice/distribution.ts'
import { baseStockSpecies } from '../src/roster/creatures.ts'

/**
 * The pure half of the splice - design §5.1's ONE function, and the test it
 * exists to make possible.
 *
 * No container: `spliceDistribution` and `sampleSplice` read no database and
 * no clock, so the published odds can be pinned exactly and the sampler run
 * two hundred thousand times for the price of a unit test.
 */

// fileURLToPath, not .pathname - this repo's own directory contains a space.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))

/**
 * The REAL authored dominance table, not four booleans retyped here - the
 * same thing base-stock.test.ts and rotation.test.ts do, and for the same
 * reason: a test carrying its own copy of the content cannot tell a code
 * regression from a content change, and these four flags are explicitly
 * provisional (validate.ts's validateTraitDominance, owed to
 * `combat_numbers` §4).
 */
const BUNDLE = JSON.parse(
  readFileSync(join(REPO, 'config/bundles/0.1.2/traits.json'), 'utf8'),
) as TraitTable

const dominanceOf = (id: string) => BUNDLE.traits.find((t) => t.id === id)!.dominant

/** Base stock, by species - the only creatures this phase can actually mint. */
const stock = (species: string) => baseStockSpecies.find((s) => s.species === species)!

/**
 * A parent as the roster holds one. Gen-1 base stock is Tier I in both
 * slots (roster/creatures.ts), so a test that needs a higher tier says so.
 */
function parent(species: string, over: Partial<SpliceParent> = {}): SpliceParent {
  const s = stock(species)
  return {
    trait1: s.trait1, tier1: 1,
    trait2: s.trait2, tier2: 1,
    // Every grant carries Vanguard today - bible §1.4's per-species
    // weighting is owed and deliberately not guessed (roster/creatures.ts).
    instinct: 'Vanguard',
    ...over,
  }
}

const VETCH = parent('Vetch')   // Taunt I (REC), Carapace I (REC)
const PALE = parent('Pale')     // Chill I (DOM), Carapace I (REC)
const EMBER = parent('Ember')   // Splash I (DOM), Carapace I (REC)

/** Lock parent A's own species counter - slot 1 is always the species' own. */
const LOCK_A1: TraitRef = { slot: 'trait_1', from: 'a' }

const p = (d: Distribution, trait: string) =>
  d.combat2.filter((o) => o.trait === trait).reduce((s, o) => s + o.p, 0)

describe('the dominance table this file is pinned against', () => {
  it('is the one Task 6 was written for', () => {
    // If `combat_numbers` §4 ratifies different flags, THIS is the assertion
    // that should fail first and say so - rather than four tests below
    // failing with arithmetic that looks like a code bug.
    expect(dominanceOf('Chill')).toBe(true)
    expect(dominanceOf('Splash')).toBe(true)
    expect(dominanceOf('Taunt')).toBe(false)
    expect(dominanceOf('Carapace')).toBe(false)
  })
})

describe('spliceDistribution', () => {
  it('carries the locked trait at full coverage, always', () => {
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    // design §5.3: the locked slot is not a roll and must not appear in one.
    expect(d.combat2.every((o) => o.trait !== VETCH.trait1)).toBe(true)
  })

  it('keeps the locked trait out of the roll even when the OTHER parent carries it too', () => {
    // NOT IN THE BRIEF, and it is the assertion that decides what "the
    // remaining three" means. Base stock rolls uniformly over three species
    // (roster/creatures.ts), so two Vetch parents are one pair in nine - and
    // every species shares Carapace, so a shared trait is the common case,
    // not the corner one.
    //
    // Removing only the locked INSTANCE leaves parent B's Taunt in the pool,
    // and the assertion above - the brief's own - is then false for those
    // pairs. Removing every instance of the locked TRAIT is what makes it
    // true, and it is also the only reading under which the child cannot end
    // up holding one trait in both slots, which nothing in the design gives
    // a stacking rule for.
    const d = spliceDistribution(VETCH, parent('Vetch'), LOCK_A1, BUNDLE)

    expect(d.combat2.map((o) => o.trait)).not.toContain('Taunt')
    // What is left is both parents' Carapace, merged: a certain roll, and
    // published as one - design §2.2's "the forecast is a certainty" is a
    // content property of this pair, not a defect to hide.
    expect(d.combat2).toEqual([{ trait: 'Carapace', tier: 1, p: 1 }])
  })

  it('downtiers a recessive rolled trait by exactly one', () => {
    // Taunt is recessive in 0.1.2. A Taunt II parent yields Taunt I.
    const a = parent('Pale')                                   // lock Chill
    const b = parent('Vetch', { trait1: 'Taunt', tier1: 2 })
    const d = spliceDistribution(a, b, LOCK_A1, BUNDLE)

    expect(d.combat2.find((o) => o.trait === 'Taunt')!.tier).toBe(1)
  })

  it('floors the downtier at Tier I rather than producing zero or null', () => {
    // design §5.3's derivation. Bible §2.2: a downtier "costs coverage and
    // never access". Zero would sort below Tier I and null means Aberrant -
    // either would misrender a perfectly ordinary trait.
    const d = spliceDistribution(parent('Pale'), VETCH, LOCK_A1, BUNDLE)
    const taunt = d.combat2.find((o) => o.trait === 'Taunt')!

    expect(taunt.tier).toBe(1)
    expect(taunt.tier).not.toBeNull()
    expect(taunt.tier).not.toBe(0)
  })

  it('does not downtier a dominant rolled trait', () => {
    const a = parent('Vetch')                                  // lock Taunt
    const b = parent('Pale', { trait1: 'Chill', tier1: 2 })
    const d = spliceDistribution(a, b, LOCK_A1, BUNDLE)

    expect(d.combat2.find((o) => o.trait === 'Chill')!.tier).toBe(2)
  })

  it('carries an Aberrant forward with no coverage rather than inventing a tier', () => {
    // NOT IN THE BRIEF. Mutation's Aberrant sub-roll (Task 7) writes a
    // combat slot with tier NULL - data_model §2 - so an Aberrant parent is
    // reachable the generation after this one ships. `Math.max(1, null - 1)`
    // is NaN, and a floor of 1 would hand an Aberrant coverage it never had.
    const b = parent('Pale', { trait1: 'Chill', tier1: null })
    const d = spliceDistribution(parent('Vetch'), b, LOCK_A1, BUNDLE)

    expect(d.combat2.find((o) => o.trait === 'Chill')!.tier).toBeNull()
  })

  it('publishes probabilities that sum to one', () => {
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    expect(d.combat2.reduce((s, o) => s + o.p, 0)).toBeCloseTo(1, 10)
    expect(d.instinct.reduce((s, o) => s + o.p, 0)).toBeCloseTo(1, 10)
  })

  it('publishes ONE entry per outcome, not one per parent copy', () => {
    // NOT IN THE BRIEF, and the brief's own convergence test cannot pass
    // without it: that test counts samples by trait NAME, so two Carapace
    // entries at p = 1/3 each would be measured at 2/3 against a published
    // 1/3. It is also what the player is owed - "Carapace 33%, Carapace 33%"
    // is not the odds, it is the pool.
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)

    expect(d.combat2).toHaveLength(2)
    // ABSOLUTE, not merely relative: a pool that lost or gained an entry
    // shifts every p together, which an assertion comparing entries to each
    // other cannot see.
    expect(p(d, 'Carapace')).toBeCloseTo(2 / 3, 10)
    expect(p(d, 'Chill')).toBeCloseTo(1 / 3, 10)
  })

  it('keeps two copies of a trait at DIFFERENT tiers as different outcomes', () => {
    // The other half of the merge: Carapace III and Carapace I are not the
    // same result, and collapsing them would publish one tier for a roll
    // that can produce two. Carapace is recessive, so III downtiers to II.
    const a = parent('Vetch', { trait2: 'Carapace', tier2: 3 })   // lock Taunt
    const b = parent('Pale')
    const d = spliceDistribution(a, b, LOCK_A1, BUNDLE)

    expect(d.combat2).toEqual([
      { trait: 'Carapace', tier: 2, p: 1 / 3 },
      { trait: 'Chill', tier: 1, p: 1 / 3 },
      { trait: 'Carapace', tier: 1, p: 1 / 3 },
    ])
  })

  it('merges the Instinct pool the same way', () => {
    // NOT IN THE BRIEF, and this one fires TODAY: every base-stock grant
    // carries Vanguard, so both parents share it and the brief's sketch
    // publishes "Vanguard 50%, Vanguard 50%" for a certainty.
    expect(spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE).instinct)
      .toEqual([{ instinct: 'Vanguard', p: 1 }])

    const d = spliceDistribution(VETCH, parent('Pale', { instinct: 'Bloodscent' }), LOCK_A1, BUNDLE)
    expect(d.instinct).toEqual([
      { instinct: 'Vanguard', p: 0.5 },
      { instinct: 'Bloodscent', p: 0.5 },
    ])
  })

  it('states mutation at 9% and the Aberrant sub-roll at 5% of it', () => {
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    expect(d.mutation).toBeCloseTo(0.09, 10)
    expect(d.aberrant).toBeCloseTo(0.05, 10)   // OF mutations, not absolute
  })

  it('refuses a trait the bundle does not author a dominance flag for', () => {
    // Same discipline as rosterCap's unknown Hatchery tier: dominance
    // decides the child's coverage on a PAID action, and a guessed flag is
    // a roll nobody authored. validate.ts makes this unpublishable; this is
    // what happens if it is reached anyway.
    const b = parent('Pale', { trait1: 'Screen' })
    expect(() => spliceDistribution(parent('Vetch'), b, LOCK_A1, BUNDLE))
      .toThrow(/Screen/)
  })

  it('refuses a pool with nothing left to roll', () => {
    // Unreachable by construction - no creature can hold one trait twice,
    // because base stock refuses it (base-stock.test.ts) and this function
    // never rolls the locked trait into slot 2. Guarded anyway: `1 / 0` is
    // Infinity, and publishing Infinity as a probability on a paid action
    // is worse than refusing.
    const doubled = parent('Vetch', { trait1: 'Taunt', trait2: 'Taunt' })
    expect(() => spliceDistribution(doubled, doubled, LOCK_A1, BUNDLE))
      .toThrow(/nothing to roll/)
  })
})

describe('coverageLost', () => {
  it('names the duplicate copy of the locked trait, by trait and tier', () => {
    // `sample_economy` §7's screen requirement - "Chill III will not carry"
    // - and the ONLY coverage a preview can name before the roll. Lock the
    // Tier I copy of a trait both parents carry and the Tier III copy is
    // destroyed with certainty, which is exactly the mistake a player would
    // want stopping.
    const a = parent('Vetch')                                    // Taunt I, Carapace I
    const b = parent('Pale', { trait2: 'Carapace', tier2: 3 })   // Chill I, Carapace III
    const locked: TraitRef = { slot: 'trait_2', from: 'a' }      // lock Carapace I

    const d = spliceDistribution(a, b, locked, BUNDLE)
    expect(coverageLost(a, b, locked, d)).toEqual([{ trait: 'Carapace', tier: 3 }])
  })

  it('is empty when every trait that is not locked can still carry', () => {
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    expect(coverageLost(VETCH, PALE, LOCK_A1, d)).toEqual([])
  })
})

describe('sampleSplice', () => {
  it('re-derives the same outcome from the same seed', () => {
    // design §5.1: the seed is stored on the `splices` row precisely so a
    // paid randomised action can be re-derived after the fact. A sampler
    // that is not a pure function of it leaves a dispute with no evidence.
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    for (const seed of [0n, 1n, 42n, 18_446_744_073_709_551_615n]) {
      expect(sampleSplice(d, seed)).toEqual(sampleSplice(d, seed))
    }
  })

  it('samples to the distribution it published', () => {
    // THE test design §5.1 exists to make possible. If forecast and roll
    // were two functions this could not be written, and the published odds
    // would be a claim about code rather than a property of it.
    //
    // The parents are chosen so the forecast is NOT uniform - Carapace 2/3
    // against Chill 1/3 - because a sampler that ignored `p` entirely and
    // picked uniformly would agree with a uniform forecast and prove
    // nothing.
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)

    const N = 200_000
    const combat2 = new Map<string, number>()
    const instinct = new Map<string, number>()
    let mutated = 0
    let aberrant = 0

    for (let i = 0; i < N; i++) {
      const o = sampleSplice(d, BigInt(i))
      // Keyed by the whole OUTCOME, not by trait: two copies of one trait
      // at different tiers are different results, and a key of `trait`
      // alone would sum them and hide a sampler that confused the two.
      const key = `${o.combat2.trait}:${String(o.combat2.tier)}`
      combat2.set(key, (combat2.get(key) ?? 0) + 1)
      instinct.set(o.instinct, (instinct.get(o.instinct) ?? 0) + 1)
      if (o.mutated) mutated++
      if (o.aberrant) aberrant++
    }

    // TOLERANCE: ±0.01 on a channel whose tightest standard error here is
    // 0.00105 (p = 1/3 at N = 200,000), so ~9.5 sigma of headroom - while
    // still 17x smaller than the 0.167 gap a uniform sampler would open at
    // p = 2/3. Wide enough never to be brittle, narrow enough to be the
    // gate. The seeds are fixed (0 .. N-1), so this is a deterministic
    // computation rather than a random draw: it cannot flake, and the sigma
    // argument is about staying correct if the sampler's mixing changes.
    for (const o of d.combat2) {
      const seen = (combat2.get(`${o.trait}:${String(o.tier)}`) ?? 0) / N
      expect(Math.abs(seen - o.p), `${o.trait} sampled at ${seen}, published ${o.p}`)
        .toBeLessThan(0.01)
    }
    for (const o of d.instinct) {
      const seen = (instinct.get(o.instinct) ?? 0) / N
      expect(Math.abs(seen - o.p)).toBeLessThan(0.01)
    }

    // AND AGAINST THE LITERAL VALUES, not only against what preview
    // published. Convergence alone is blind to a forecast and a sampler
    // that are wrong together in the same direction - a pool that dropped
    // an entry would shift both and still converge.
    expect(Math.abs((combat2.get('Carapace:1') ?? 0) / N - 2 / 3)).toBeLessThan(0.01)
    expect(Math.abs((combat2.get('Chill:1') ?? 0) / N - 1 / 3)).toBeLessThan(0.01)

    // 9% per splice - `sample_economy` §9.
    expect(Math.abs(mutated / N - 0.09)).toBeLessThan(0.01)
    // 5% OF MUTATIONS, which is 0.45% of splices. Pinned both ways on
    // purpose: the absolute rate alone would pass for a sampler that rolled
    // the sub-roll against every splice at some other number, and the
    // conditional rate alone would pass for one that mutated far too often.
    expect(Math.abs(aberrant / mutated - 0.05)).toBeLessThan(0.01)
    expect(Math.abs(aberrant / N - 0.0045)).toBeLessThan(0.001)
    // An Aberrant is a mutation. Never the other way round, and never on
    // its own - `sample_economy` §9 makes it a SUB-roll.
    expect(aberrant).toBeLessThan(mutated)
  })

  it('never returns an outcome the distribution did not publish', () => {
    // The floating-point tail: a cumulative walk that falls off the end
    // must land on a published outcome rather than on undefined.
    const d = spliceDistribution(VETCH, PALE, LOCK_A1, BUNDLE)
    const published = new Set(d.combat2.map((o) => `${o.trait}:${String(o.tier)}`))

    for (let i = 0; i < 5_000; i++) {
      const o = sampleSplice(d, BigInt(i))
      expect(published.has(`${o.combat2.trait}:${String(o.combat2.tier)}`)).toBe(true)
      expect(d.instinct.some((x) => x.instinct === o.instinct)).toBe(true)
    }
  })
})

/**
 * THE SEED TO OUTCOME MAP - golden vectors.
 *
 * splice/distribution.ts's `uniforms` calls its channel layout and its hashed
 * text FILE FORMAT, because Task 7 stores a seed on every `splices` row so a
 * disputed splice can be re-derived (design §5.1). Until this block existed
 * NOTHING TESTED THAT CLAIM: reordering the four digest words, or hashing
 * `seed + 1` instead of `seed`, left all nineteen tests GREEN - measured,
 * not supposed. Every splice already recorded would then re-derive to a
 * different outcome, the stored seed would prove nothing, and the audit trail
 * would be void with no test reddening at any point.
 *
 * That is the distinction row h in the report draws from the other side:
 * one-function-two-callers guarantees the forecast and the roll AGREE. It
 * does not pin WHICH outcome a seed selects, and agreement with a moved map
 * is still agreement.
 *
 * SO THESE NUMBERS ARE THE CONTRACT. If a change here is deliberate, it is
 * not a test to update - it is a migration, because every seed already on
 * disk means something different afterwards.
 */
describe('the seed to outcome map', () => {
  /**
   * A PROBE distribution rather than a real forecast, and it is built to be
   * maximally sensitive to which digest word feeds which channel:
   *
   *  - four equal combat outcomes and four equal Instincts, so each channel
   *    reads off the top two bits of its own word rather than a rare tail;
   *  - `mutation` and `aberrant` at 0.5, because at the real 0.09/0.05 a
   *    transposed word almost never flips either boolean, and a vector that
   *    cannot change is a vector that pins nothing.
   *
   * The tiers deliberately include `null` - an Aberrant carried forward -
   * so the map is pinned over the full shape of an outcome.
   */
  const PROBE: Distribution = {
    combat2: [
      { trait: 'Q0', tier: 1, p: 0.25 },
      { trait: 'Q1', tier: 2, p: 0.25 },
      { trait: 'Q2', tier: 3, p: 0.25 },
      { trait: 'Q3', tier: null, p: 0.25 },
    ],
    instinct: [
      { instinct: 'R0', p: 0.25 }, { instinct: 'R1', p: 0.25 },
      { instinct: 'R2', p: 0.25 }, { instinct: 'R3', p: 0.25 },
    ],
    mutation: 0.5,
    aberrant: 0.5,
  }

  /**
   * The seeds are CHOSEN FOR COVERAGE, not taken in order: between them they
   * reach all four combat quartiles, all four Instincts, and all three
   * mutation states - clean, mutated-only, and Aberrant. Without the Aberrant
   * vector (seed 3) a sampler that returned `aberrant: false` unconditionally
   * would satisfy every other row here. `2n ** 64n - 1n` is the largest seed
   * a `ulong` can carry into `splices.seed`.
   */
  const VECTORS: Array<[bigint, { trait: string; tier: number | null }, string, boolean, boolean]> = [
    [0n, { trait: 'Q1', tier: 2 }, 'R3', false, false],
    [1n, { trait: 'Q1', tier: 2 }, 'R2', true, false],
    [2n, { trait: 'Q3', tier: null }, 'R3', true, false],
    [3n, { trait: 'Q1', tier: 2 }, 'R1', true, true],
    [4n, { trait: 'Q1', tier: 2 }, 'R0', false, false],
    [8n, { trait: 'Q0', tier: 1 }, 'R0', true, false],
    [14n, { trait: 'Q2', tier: 3 }, 'R1', false, false],
    [18_446_744_073_709_551_615n, { trait: 'Q0', tier: 1 }, 'R0', false, false],
  ]

  it.each(VECTORS)('maps seed %s to exactly one outcome, permanently', (seed, combat2, instinct, mutated, aberrant) => {
    expect(sampleSplice(PROBE, seed)).toEqual({ combat2, instinct, mutated, aberrant })
  })

  it('covers every channel value, so no vector can be satisfied by a constant', () => {
    // A guard on the TABLE rather than on the code: if someone trims these
    // vectors, this says what the trimmed set stopped covering. Without it,
    // a table that had lost its only Aberrant row would still look thorough.
    expect(new Set(VECTORS.map((v) => v[1].trait)).size).toBe(PROBE.combat2.length)
    expect(new Set(VECTORS.map((v) => v[2])).size).toBe(PROBE.instinct.length)
    expect(VECTORS.some((v) => v[3] && v[4])).toBe(true)    // an Aberrant
    expect(VECTORS.some((v) => v[3] && !v[4])).toBe(true)   // mutated, not Aberrant
    expect(VECTORS.some((v) => !v[3])).toBe(true)           // neither
  })

  /**
   * And through the REAL thing, end to end: the bundle's dominance flags, the
   * merge, the first-appearance ORDER of the merged array - which
   * distribution.ts also calls file format, for the same reason - and the
   * sampler on top of it. Seeds 0 and 2 select different entries of a
   * three-outcome forecast whose first and third entries are the same trait
   * at different tiers, so a reordering of the merge moves them.
   *
   * IF THIS FAILS AND THE VECTORS ABOVE DO NOT, the map is intact and the
   * CONTENT moved: check `the dominance table this file is pinned against`
   * at the top of this file first. That is a real event - the four flags are
   * provisional and owed to `combat_numbers` §4 - and it is worth knowing
   * that ratifying them re-derives every splice already recorded.
   */
  it('maps a seed through the real forecast, merge order and all', () => {
    const a = parent('Vetch')                                     // Taunt I, Carapace I
    const b = parent('Pale', { trait2: 'Carapace', tier2: 3, instinct: 'Bloodscent' })
    const d = spliceDistribution(a, b, LOCK_A1, BUNDLE)

    expect(d.combat2).toEqual([
      { trait: 'Carapace', tier: 1, p: 1 / 3 },
      { trait: 'Chill', tier: 1, p: 1 / 3 },
      { trait: 'Carapace', tier: 2, p: 1 / 3 },
    ])
    expect(sampleSplice(d, 0n)).toEqual({
      combat2: { trait: 'Chill', tier: 1 }, instinct: 'Bloodscent',
      mutated: false, aberrant: false,
    })
    expect(sampleSplice(d, 2n)).toEqual({
      combat2: { trait: 'Carapace', tier: 2 }, instinct: 'Bloodscent',
      mutated: true, aberrant: false,
    })
    expect(sampleSplice(d, 14n)).toEqual({
      combat2: { trait: 'Chill', tier: 1 }, instinct: 'Vanguard',
      mutated: false, aberrant: false,
    })
  })
})
