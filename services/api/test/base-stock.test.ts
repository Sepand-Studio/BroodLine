import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import { baseStockSpecies, rosterCap, speciesForSeed } from '../src/roster/creatures.ts'

/**
 * The pure half of the base-stock grant. No container: `speciesForSeed` and
 * `rosterCap` read no database, so the distribution can be pinned exactly
 * and cheaply here instead of being sampled through a claim.
 */

// fileURLToPath, not .pathname - this repo's own directory contains a space.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const TRAITS = JSON.parse(
  readFileSync(join(REPO, 'config/bundles/0.1.2/traits.json'), 'utf8'),
) as { traits: Array<{ id: string; species: string; counters: string | null; dominant: boolean }> }

describe('the base-stock species table', () => {
  it('mints only species and traits bundle 0.1.2 actually authors', () => {
    // The check that makes this table content rather than invention. Task 6
    // reads `dominant` off traits.json by trait id, so a granted creature
    // carrying a trait absent from that file makes the splice roll
    // undefined on a paid action.
    const authored = new Set(TRAITS.traits.map((t) => t.id))
    const speciesWithTraits = new Set(TRAITS.traits.map((t) => t.species))

    for (const v of baseStockSpecies) {
      expect(speciesWithTraits).toContain(v.species)
      expect(authored).toContain(v.trait1)
      expect(authored).toContain(v.trait2)
    }
  })

  it('gives each species its OWN authored counter in the locked slot', () => {
    // Slot 1 is the species' own trait - bible §1.2. Slot 2 is allowed to
    // be a stand-in (see roster/creatures.ts), slot 1 is not.
    for (const v of baseStockSpecies) {
      const trait = TRAITS.traits.find((t) => t.id === v.trait1)!
      expect(trait.species).toBe(v.species)
    }
  })

  it('never hands a species a counter its authored pair does not give it', () => {
    // The one property the slot-2 stand-in could actually break. Pale and
    // Ember each author ONE counter (Chill, Splash); their real second
    // traits - Screen and Cinder - are outside the engine's Trait enum, so
    // slot 2 stands in with Carapace, whose `counters` is null. If that
    // stand-in ever became a counter, a Gen-1 Pale would quietly answer a
    // raider the design never gave it.
    const counterOf = (id: string) => TRAITS.traits.find((t) => t.id === id)!.counters

    for (const v of baseStockSpecies) {
      const own = TRAITS.traits.filter((t) => t.species === v.species).map((t) => t.id)
      // Slot 2 either belongs to this species, or answers nothing at all.
      if (!own.includes(v.trait2)) expect(counterOf(v.trait2)).toBeNull()
    }
  })

  it('is a function of its seed, not of a clock or a counter', () => {
    // design §5.1's discipline, applied to the other thing this phase
    // grants: a randomised award whose outcome cannot be re-derived
    // afterwards has no evidence on either side of a dispute.
    for (const seed of ['a', 'node:0:1', 'e3f1-…-9', '']) {
      expect(speciesForSeed(seed)).toEqual(speciesForSeed(seed))
    }
  })

  it('reaches every species, and does not collapse onto adjacent seeds', () => {
    // ADJACENT seeds specifically: a claim granting three creatures seeds
    // them `…:0`, `…:1`, `…:2`, so a weak mix over neighbouring inputs is
    // exactly the failure that would mint one species three times while
    // looking random across unrelated seeds.
    const runs: string[] = []
    for (let i = 0; i < 240; i++) runs.push(speciesForSeed(`claim-seed:${i}`).species)

    expect(new Set(runs).size).toBe(baseStockSpecies.length)
    // Roughly uniform - a wide band, because the point is that no species is
    // starved or dominant, not that SHA-256 is well distributed.
    for (const v of baseStockSpecies) {
      const n = runs.filter((r) => r === v.species).length
      expect(n).toBeGreaterThan(240 / baseStockSpecies.length / 2)
    }
  })
})

describe('rosterCap', () => {
  it('is bible §7.2\'s floor of 20 at Hatchery tier 1', () => {
    expect(rosterCap(1)).toBe(20)
  })

  it('refuses a tier nothing authors a cap for', () => {
    // The cap decides whether a player's creatures are REFUSED, so a
    // guessed one is a refusal nobody authored. Same discipline as
    // accrual.ts's multiplier tables.
    expect(() => rosterCap(2)).toThrow(/Hatchery tier 2/)
  })
})
