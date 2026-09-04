# Broodline — Art Direction Brief

---

## 1. The Tension, and How It Resolves

There's a genuine conflict sitting in the design. The fiction is clinical biotech — Gene Ark, Gene Vault, Splice Charges, Collectors, probability tables. The title and the emotional core are organic — broods, lineage, descent, creatures you name.

Blending these produces mush. Instead, **assign each register a job.**

> **The interface is a laboratory. The creatures are what escapes it.**

UI is sterile, precise, instrumented: clean surfaces, exact numerals, restrained colour, the feel of equipment that measures things. Creatures are warm, strange, asymmetric, and visibly *more* than the system that produced them.

This isn't only a look — it's the game's argument. You control the inputs to a splice. You don't control the output. The art should make that felt before a player reads a single tooltip.

---

## 2. Palette

**Interface** — cool and neutral, deliberately recessive:
- Bone white, pale grey, cool steel
- Deep charcoal for backgrounds
- **Amber** as the single accent, used sparingly for actions and highlights

**Creatures** — warm and organic, high contrast against the UI:
- Ochre, rust, clay, cream, moss, deep teal
- Skin and hide over armour and plate
- Colour variation carries trait information (see §6)

The intended effect: a creature on a lab-grey card should look like it doesn't belong there.

**Reserved on the map**, per the node spec: green for rich/available, red for contested/depleted, gold pulse for active Apex Veins. These are load-bearing and shouldn't be reused for anything else at the map layer.

---

## 3. Creature Design Rules

**Silhouette first.** All 10 chassis must be distinguishable as flat black shapes at 40px. If two chassis are confusable in silhouette, one is wrong. Test this before any texturing work.

**Hybrids must read as hybrids.** A player should look at a creature and see two things fused — asymmetry, mismatched limb pairs, one half heavier than the other. This is the core fantasy and it's easy to lose by designing coherent, balanced animals.

**Traits must be visible on the body.** A creature with Plated Hide, Corrosive Spines, and a Chill Aura should be identifiable as such without opening its detail screen. This is the biggest constraint on the whole art pipeline (see §7) and the biggest payoff — it's what makes the roster screen readable and the splice result exciting.

**Damage is posture, not injury.** At a 9+ rating and with a broad audience, wounded creatures show stress through stance, breathing, and a desaturating glow — never blood, gore, or visible trauma.

**Appeal over horror.** These are creatures a player names and keeps in a family tree for two years. Strange and unsettling is fine. Repulsive is not.

---

## 4. The Anti-Tyranid Checklist

Games Workshop's Genestealers and Broodlord occupy adjacent thematic ground — genetic hybrids, broods, cults of descent — and GW enforces its IP aggressively. The name is legally clear; the art is where an unnecessary association could form.

Avoid:
- Chitinous carapace plating as a dominant surface
- The purple-and-bone palette
- Elongated, backswept skulls
- Six-limbed insectoid builds with scything upper limbs
- Ribbed, sinewy "bio-ship" architecture in environments

Lean instead toward **mammalian and reptilian mass** — fur, hide, keratin, muscle under skin. Warm-blooded rather than carapaced. This also serves the appeal requirement above, so the constraint costs nothing.

---

## 5. Typography & Interface

The UI carries an unusual density of numbers — probability tables, regen timers, Hold accrual, yield rates, generation counts. Type selection should be driven by numerals, not headlines.

- A neutral grotesque with **tabular figures**, so numbers don't jitter as timers tick
- Clear distinction between 1/l/I and 0/O — misread stats erode trust
- Minimum 11pt for any number a decision depends on
- Generous spacing; the lab register wants air, not density

Interface chrome should feel like instrumentation — thin rules, precise alignment, restrained motion. Nothing gilded, nothing ornate. The creatures supply all the visual richness.

---

## 6. Rarity Language

Four trait tiers need to be distinguishable at pip size, on a small screen, for colourblind players.

| Tier | Colour | Secondary cue |
|---|---|---|
| Common | Neutral grey | One pip |
| Refined | Cool teal | Two pips |
| Rare | Amber | Three pips |
| **Apex** | Iridescent / white-hot | Four pips + subtle motion |

**Never colour alone.** Pip count carries the same information, so the system works in greyscale. Apex gets the only animated treatment in the tier system — it's mutation-only and should feel like an event.

Note the potential confusion: gold is already the map's Apex Vein pulse. Keeping trait Apex as iridescent-white rather than gold avoids two different "apex" signals reading as the same thing.

---

## 7. The Modular Art Problem

This is the largest production decision in the document and it should be settled before any creature is finalised.

Traits must be visible on the body, which means creatures are **chassis base + four swappable visual layers**. With 10 chassis and a meaningful trait pool, the combinations run into the thousands. That's only viable as a modular system — never as hand-authored creatures.

Implications:
- Chassis must be built with **standardised attachment points** for Frame, Armament, Field, and Instinct expression
- Not every trait needs a unique model. Frame and Armament are the visible ones; Field can be an aura effect, Instinct can be a posture or eye treatment.
- Scope realistically: perhaps 15 Frame variants and 15 Armament variants get unique geometry, the rest are recolours and scale variants
- Animation must be chassis-level, not trait-level, or the budget is unbounded

**Recommendation:** decide the modular rig before the first creature is modelled. Retrofitting modularity onto hand-built creatures is the single most expensive mistake available here.

---

## 8. Tone Reference

**Yes:** natural history illustration, veterinary anatomy plates, greenhouse and conservatory light, field-research equipment, the warmth of something alive in a cold clean room.

**No:** bio-horror, body horror, viscera, military sci-fi, grimdark, neon cyberpunk, cute collectible mascots.

The target feeling when a splice resolves: *surprise and affection*, not shock.

---

## 9. Open Questions

1. ~~**2D or 3D creatures?**~~ **Resolved: 3D.** Attachment-point rigging is what lets ten chassis carry four swappable trait layers without hand-authoring every combination — the exact retrofit problem §7 warns is the most expensive mistake available here. 3D also serves the combat spec's elevation and footprint mechanics natively, where 2D tile tricks only go so far, and it's the pattern Whiteout Survival and Kingshot already ship at commercial scale on modular units. Cost is real and upfront: rig and animate one chassis first as a pipeline proof before committing art budget to the other nine.
2. **How much of Instinct is visible?** Behaviour is expressed in motion during combat, but the roster screen is static. Possibly an eye or posture treatment as a static cue.
3. **Do chassis have canonical colour identity**, or is colour entirely trait-driven? Canonical colour aids recognition; trait-driven colour makes each creature feel individual.
4. **Environment art scope.** 30 hand-authored regions with distinct terrain is a large line item that hasn't been costed.

---

*This closes the current spec set: monetization, nodes, raiding, genetics, combat, alliance, screens, and art direction. The remaining undocumented systems are the Gene Vault progression and the FTUE.*
