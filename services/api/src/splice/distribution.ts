import { createHash } from 'node:crypto'

/**
 * ONE FUNCTION, TWO CALLERS - design §5.1, and the load-bearing decision of
 * §5 rather than a stylistic one.
 *
 * `POST /v1/splice/preview` RETURNS `spliceDistribution`. `POST
 * /v1/splice/commit` (Task 7) SAMPLES the same value with a server-generated
 * seed. The alternative - a forecast function beside a roll function - is the
 * normal way to build this and it is wrong here: bible §2.6 commits to
 * publishing odds, and two functions make those odds a CLAIM ABOUT CODE
 * rather than a PROPERTY OF IT. The difference is a test that can exist,
 * `samples to the distribution it published`, and it is the gate on this file.
 *
 * So: if a change to this module ever needs probability logic in two places,
 * that is the signal to stop, not a shape to tidy afterwards.
 *
 * PURE, and checkable by inspection: this file imports `node:crypto` and
 * nothing else. No `db/`, no `config/`, no clock - the same discipline
 * map/rotation.ts and map/accrual.ts keep, and what lets the convergence test
 * run two hundred thousand samples without a container.
 */

/**
 * The slice of the bundle this module reads, DECLARED LOCALLY rather than
 * imported from config/bundle.ts - map/rotation.ts's convention, and for the
 * same reason: the purity above stays checkable by reading the imports, and
 * TypeScript's structural typing already lets a real `Bundle` satisfy it.
 *
 * `dominant` is content and provisional: bible §2.2 adopts dominance as a
 * mechanic and assigns it to no trait, and config/bundles/0.1.2/traits.json's
 * four flags are owed to `combat_numbers` §4 for ratification (design §11).
 * config/validate.ts's `validateTraitDominance` is what makes a bundle
 * missing one unpublishable.
 */
export interface TraitTable {
  traits: ReadonlyArray<{ id: string; dominant: boolean }>
}

/**
 * A parent as the roster holds one - structurally a `CreatureDto` or a
 * `creatures` row, narrowed to what the roll actually reads.
 *
 * `tier` is NULLABLE because null means an Aberrant, which has no coverage
 * (data_model §2). It is never zero: 0005_loop.sql's
 * `coverage_tier_N_not_zero` refuses that outright, because zero would sort
 * and display as "less than Tier I".
 */
export interface SpliceParent {
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
  instinct: string
}

export type CombatSlot = 'trait_1' | 'trait_2'

/** Which of the four parent combat traits the player locked - by POSITION. */
export interface TraitRef {
  slot: CombatSlot
  from: 'a' | 'b'
}

export interface CombatOutcome {
  trait: string
  /** null for an Aberrant - never 0. See SpliceParent. */
  tier: number | null
  p: number
}

export interface Distribution {
  /** The rolled combat slot. One entry per OUTCOME - see `spliceDistribution`. */
  combat2: CombatOutcome[]
  instinct: Array<{ instinct: string; p: number }>
  /** Per splice. */
  mutation: number
  /** OF mutations, not absolute - `sample_economy` §9. */
  aberrant: number
}

export interface SpliceOutcome {
  combat2: { trait: string; tier: number | null }
  instinct: string
  mutated: boolean
  aberrant: boolean
}

export interface PoolEntry extends TraitRef {
  trait: string
  tier: number | null
}

/**
 * Base mutation rate and the Aberrant sub-roll inside it - `sample_economy`
 * §9 at its base rates, with no catalyst and no Surge (design §4.1).
 *
 * The two are NOT the same event and the confirm screen shows them
 * separately (`splice_confirm_spec` §2): 9% of splices mutate, and 5% OF
 * THOSE are Aberrant - 0.45% of splices, not 5% of them.
 */
export const MUTATION_RATE = 0.09
export const ABERRANT_SUB_ROLL = 0.05

/** The parents' four combat traits, in a fixed order - design §5.2's pool. */
export function combatPool(a: SpliceParent, b: SpliceParent): PoolEntry[] {
  return [
    { from: 'a', slot: 'trait_1', trait: a.trait1, tier: a.tier1 },
    { from: 'a', slot: 'trait_2', trait: a.trait2, tier: a.tier2 },
    { from: 'b', slot: 'trait_1', trait: b.trait1, tier: b.tier1 },
    { from: 'b', slot: 'trait_2', trait: b.trait2, tier: b.tier2 },
  ]
}

/** Positional identity - the locked INSTANCE, not every copy of its trait. */
function isLockedInstance(t: PoolEntry, locked: TraitRef): boolean {
  return t.from === locked.from && t.slot === locked.slot
}

/**
 * Dominance, or a refusal.
 *
 * THROWS for a trait the bundle does not author, rather than defaulting to
 * either branch - the same discipline `rosterCap` applies to an unknown
 * Hatchery tier and `accrual.ts` to an unknown multiplier. Dominance decides
 * the child's coverage on a PAID action, so a guessed flag is a roll nobody
 * authored. `validateTraitDominance` makes it unpublishable; this is the
 * answer if it is reached anyway, and routes/splice.ts turns it into a named
 * content failure rather than a silent one.
 */
function isDominant(traits: TraitTable, id: string): boolean {
  const authored = traits.traits.find((t) => t.id === id)
  if (authored === undefined) {
    throw new Error(`the active config bundle authors no dominance flag for trait ${id}`)
  }
  return authored.dominant
}

/**
 * The coverage a rolled trait carries - design §5.3.
 *
 * Full if dominant; one tier lower if recessive, FLOORED AT TIER I.
 *
 * No document states that floor; the design set determines it.
 * `sample_economy` §7 says a recessive trait carries "one tier lower" and
 * does not say what happens at Tier I, where there is no lower tier. Bible
 * §2.2 settles it - a downtier "costs coverage and never access" - and
 * `combat_numbers` §4.1 says it from the other side: "tier decides how much a
 * trait covers, never whether it works". A Tier I trait that downtiered out
 * of existence would cost access, which both forbid.
 *
 * So coverage NEVER reaches zero or null through this path. Zero would sort
 * below Tier I; null is reserved for Aberrants (design §3.1), and conflating
 * the two would render an ordinary downtiered trait AS an Aberrant.
 *
 * An Aberrant in the pool carries forward unchanged, at null. It has no
 * coverage to lose, `Math.max(1, null - 1)` is NaN, and flooring it at 1
 * would hand it coverage it never had.
 */
function carriedTier(traits: TraitTable, t: PoolEntry): number | null {
  if (t.tier === null) return null
  return isDominant(traits, t.trait) ? t.tier : Math.max(1, t.tier - 1)
}

/**
 * Merge equal outcomes and normalise. One entry per DISTINCT result, with the
 * probabilities of the pool draws that produce it summed.
 *
 * WHY THIS IS NOT OPTIONAL, and it is the brief's sketch's one real defect.
 * Every species in the base-stock table carries Carapace in slot 2
 * (roster/creatures.ts - Pale's Screen and Ember's Cinder are outside the
 * engine's `Trait` enum), so two arbitrary parents share a trait as the
 * COMMON case. One entry per pool draw would publish "Carapace 33%, Carapace
 * 33%", which is the pool rather than the odds - and bible §2.6 commits to
 * publishing odds. The convergence test cannot pass against it either: it
 * counts outcomes, and an outcome reached two ways is reached twice as often
 * as either entry claims.
 *
 * Order is FIRST APPEARANCE in the pool, and it is load-bearing:
 * `sampleSplice` walks this array cumulatively, so re-ordering it changes
 * what a stored seed re-derives. design §5.1 stores the seed precisely so a
 * paid randomised action can be re-derived after the fact; the order is part
 * of that.
 */
function merged<T>(items: T[], key: (t: T) => string): Array<{ item: T; p: number }> {
  const index = new Map<string, number>()
  const out: Array<{ item: T; p: number }> = []
  for (const item of items) {
    const k = key(item)
    const at = index.get(k)
    if (at === undefined) {
      index.set(k, out.length)
      out.push({ item, p: 1 / items.length })
    } else {
      out[at]!.p += 1 / items.length
    }
  }
  return out
}

/** The key two pool draws must share to be the same published outcome. */
function outcomeKey(trait: string, tier: number | null): string {
  // JSON, not a separator character: trait ids are authored content and a
  // delimiter one of them could contain would collide `Chill` at tier 11
  // with `Chill1` at tier 1, merging two outcomes into one published odds.
  return JSON.stringify([trait, tier])
}

/**
 * The distribution a splice of these two parents rolls against - design §5.1.
 *
 * THE LOCKED TRAIT IS REMOVED FROM THE POOL BY ID, NOT BY POSITION, and that
 * is a decision the design does not spell out. §5.2 says the rolled slot
 * fills from "the remaining three", written when every species was assumed to
 * author two distinct traits of its own. Three things settle it the other
 * way:
 *
 *  - Two same-species parents are one base-stock pair in nine, and every
 *    species shares Carapace, so a duplicated trait is ordinary rather than
 *    exotic. Removing only the locked instance leaves the other copy
 *    rollable, and the child then holds one trait in BOTH slots.
 *  - Nothing in the design gives two instances of one trait a stacking rule.
 *    `combat_numbers` §4.1 ties a trait's effect to its tier, one tier per
 *    creature, and the engine applies a trait per slot. Shipping an undefined
 *    interaction on a paid action is exactly what `validateTraitDominance`
 *    exists to prevent one file over.
 *  - Nothing else in the game can mint such a creature - base-stock.test.ts
 *    refuses `trait2 === trait1` outright - so the splice would become the
 *    sole source of an object no other code expects.
 *
 * The cost is that the pool can hold two entries rather than three, and for
 * two same-species parents it can collapse to a single certain outcome. That
 * is design §2.2's "the forecast is a certainty" showing through, and it is a
 * property of the authored trait set, not of this function - the forecast
 * says so plainly, before the charge is spent, which is the whole point of
 * publishing it.
 */
export function spliceDistribution(
  a: SpliceParent, b: SpliceParent, locked: TraitRef, traits: TraitTable,
): Distribution {
  const pool = combatPool(a, b)
  const lockedEntry = pool.find((t) => isLockedInstance(t, locked))
  if (lockedEntry === undefined) {
    throw new Error(`no combat slot ${locked.slot} on parent ${locked.from}`)
  }

  const candidates = pool.filter((t) => t.trait !== lockedEntry.trait)
  if (candidates.length === 0) {
    // Unreachable by construction (see above), and guarded rather than
    // assumed: `1 / 0` is Infinity, and publishing Infinity as a probability
    // on a paid action is worse than refusing to publish at all.
    throw new Error(
      `both parents carry only ${lockedEntry.trait}: nothing to roll for the second combat slot`)
  }

  const combat2 = merged(
    candidates.map((t) => ({ trait: t.trait, tier: carriedTier(traits, t) })),
    (o) => outcomeKey(o.trait, o.tier),
  ).map(({ item, p }) => ({ trait: item.trait, tier: item.tier, p }))

  // The pools are SEPARATE - bible §1.2 and `splice_confirm_spec` §2: combat
  // slots fill only from the four combat traits, the Instinct slot only from
  // the two Instincts.
  //
  // Uniform over the two, and the weighting bible §1.4 and design §5.2 call
  // for - "modified by affinity and DOM/REC" - is OWED, not guessed:
  // dominance is authored per TRAIT in traits.json and no document assigns it
  // to an Instinct, and affinity is defined nowhere. Every base-stock grant
  // carries Vanguard today, so both parents usually share one and the merge
  // above publishes the certainty rather than a 50/50 over a single value.
  const instinct = merged([a.instinct, b.instinct], (i) => i)
    .map(({ item, p }) => ({ instinct: item, p }))

  return { combat2, instinct, mutation: MUTATION_RATE, aberrant: ABERRANT_SUB_ROLL }
}

/**
 * The coverage this splice destroys with CERTAINTY, by trait and tier -
 * `sample_economy` §7's screen requirement, quoted there as "Chill III will
 * not carry".
 *
 * WHAT IT CAN HONESTLY NAME, which is narrower than it first looks. Four
 * traits go in and two come out, so two are destroyed - but WHICH two is not
 * known until the roll, and naming a trait that may yet carry would be a
 * warning that is false half the time. So this names only what cannot carry
 * under ANY outcome the forecast published.
 *
 * With four distinct traits that set is empty, and with a duplicate it is
 * exactly the copy the player did not lock. That is the case worth warning
 * about and the one a player would otherwise meet after the fact: lock the
 * Tier I copy of a trait both parents carry, and the Tier III copy is gone.
 */
export function coverageLost(
  a: SpliceParent, b: SpliceParent, locked: TraitRef, d: Distribution,
): Array<{ trait: string; tier: number | null }> {
  const rollable = new Set(d.combat2.map((o) => o.trait))
  return combatPool(a, b)
    .filter((t) => !isLockedInstance(t, locked) && !rollable.has(t.trait))
    .map((t) => ({ trait: t.trait, tier: t.tier }))
}

/**
 * Four independent uniforms in [0, 1) from one seed.
 *
 * SHA-256, for the reason `speciesForSeed` gives: the convergence test draws
 * seeds 0, 1, 2 ... and Task 7 will store consecutive ones, so a weak mix
 * over ADJACENT inputs is the failure that would correlate the combat roll
 * with the mutation roll while looking random across unrelated seeds. One
 * digest gives 32 bytes - four 64-bit words, one per channel - so the four
 * rolls are independent without hashing four times.
 *
 * Each word is shifted to 53 bits and divided by 2^53: the standard exact
 * construction for a double in [0, 1), with no rounding up to 1.0.
 *
 * THE CHANNEL LAYOUT IS FILE FORMAT. The seed is stored on the `splices` row
 * so the roll can be re-derived after a dispute (design §5.1), so changing
 * which word feeds which channel - or the text that is hashed - silently
 * reinterprets every splice already recorded. Add a channel by taking a
 * SECOND digest over a suffixed input, never by renumbering these.
 */
function uniforms(seed: bigint): [number, number, number, number] {
  const digest = createHash('sha256').update(seed.toString()).digest()
  const u = (word: number) => Number(digest.readBigUInt64BE(word * 8) >> 11n) / 2 ** 53
  return [u(0), u(1), u(2), u(3)]
}

/**
 * Walks the cumulative probabilities. The LAST entry is the fallback rather
 * than `undefined`: the p's are floats and sum to one only to within a few
 * ulps, so a draw inside that last ulp must still land on a published
 * outcome.
 */
function pick<T extends { p: number }>(items: readonly T[], u: number): T {
  let acc = 0
  for (const item of items) {
    acc += item.p
    if (u < acc) return item
  }
  return items[items.length - 1]!
}

/**
 * The roll - design §5.1's second caller, sampling the SAME value
 * `POST /v1/splice/preview` published.
 *
 * Server-side, including the mutation roll: `data_model` §8 says so in the
 * strongest terms that document uses anywhere.
 */
export function sampleSplice(d: Distribution, seed: bigint): SpliceOutcome {
  const [uCombat, uInstinct, uMutation, uAberrant] = uniforms(seed)
  const combat2 = pick(d.combat2, uCombat)

  // A SUB-roll, not a second independent one: an Aberrant IS a mutation, and
  // `d.aberrant` is 5% OF the 9%, never 5% of splices.
  const mutated = uMutation < d.mutation

  return {
    combat2: { trait: combat2.trait, tier: combat2.tier },
    instinct: pick(d.instinct, uInstinct).instinct,
    mutated,
    aberrant: mutated && uAberrant < d.aberrant,
  }
}
