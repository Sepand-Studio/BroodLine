---
status: superseded
folder: 99-archive
superseded-by: broodline_trait_utility.md
note: >
  Names the trait Plating. The rename to Carapace is current; the trait
  utility document supersedes this in full.
---

# Broodline — Utility Trait Spec

*Design spec. Defines Plating, Litter, Regrow and Screen — the four traits that counter nothing.*

Extends bible §1.2 and §1.3. Where this contradicts an earlier document, this is current.

---

## 1. Why This Document Exists

Bible §1.2 states the problem and does not solve it:

> If Plating, Litter, Regrow and Screen are weak, the single-counter species become "the one trait I need in a body I don't want," and the roster collapses toward two species.

Ember and Hollow each carry two counters. Vetch, Skitter, Loam and Pale each carry one. If a species' second trait is filler, that species is a delivery vehicle for a single counter and every splice into it is a tax. Four of six species become chores, the roster narrows to Ember and Hollow lines, and the consumption economy — which depends on players wanting to hold a wide roster — loses its reason to exist.

This is not a tuning problem. Four traits have no defined effect at all. Nothing downstream can be balanced or playtested until they do.

---

## 2. The Governing Principle

**A utility trait changes the cost of a fight, never its result.**

Wave outcome is decided entirely by counter presence. Under §4.4 there is no damage floor: a deployment without the answering trait loses regardless of how much power it brings. Utility traits sit entirely on the other side of that line. They decide what a win *costs* — regeneration downtime, skip shards, splice charges, sample income, deployment slots, time.

**The test for any proposed utility effect:** could a deployment missing the answering trait clear the wave because of it? If yes, it is a damage floor wearing a different name, and it invalidates the single decision the whole design rests on.

Three rules follow, and they constrain every effect in §3:

**No damage, no damage amplification.** Not raw damage, not a percentage buff, not a vulnerability debuff on raiders. Courser's threat is specified as "damage cannot kill it in time" — any amplification erodes that by degrees until Chill becomes optional.

**No effect on a raider's defining behaviour.** Nothing that blunts a Brood split, a Delver burrow, a Drift flight path, a Bulwark shield or a Lash's reach. Each of the eight raiders' defining behaviours is its counter's exclusive property. A utility trait that partially answers one is a partial paywall bypass in the other direction — it makes the Codex's teaching copy false.

**Ark hit points are off limits.** Utility traits protect *creatures*. Only counters protect the Ark. This is what makes generosity safe: because §1.5 and the combat spec guarantee no creature is ever lost involuntarily, a damaged creature costs regeneration time and nothing else. Survivability therefore has exactly one currency, that currency is economic, and it can be tuned hard without touching a single wave outcome.

These traits are the same class of thing as Instinct (§4.6): they change how well a correct composition performs, never whether an incorrect one can win.

---

## 3. The Four Traits

Each takes a distinct job. No two touch the same lever.

| Trait | Species | Job | Lever |
|---|---|---|---|
| **Plating** | Vetch | Prevent damage in-wave | Regeneration downtime |
| **Regrow** | Loam | Recover damage post-wave | Regeneration downtime and skip shards |
| **Litter** | Skitter | Pay the player back for splicing | Sample income |
| **Screen** | Pale | Buy uptime for the back line | Effective damage output of a correct composition |

### 3.1 Plating — Vetch

**Effect: incoming damage reduced 40%. The magnitude is fixed at every tier; the tier decides who receives it.**

| Tier | Covers |
|---|---|
| I | Self |
| II | Self and creatures on adjacent tiles |
| III | Every deployed creature in the lane |

Vetch is the wall. Taunt makes raiders attack it; Plating is what lets it survive being attacked. Without Plating defined, Taunt is a trait that volunteers your creature for destruction — which is why Vetch currently reads as the worst body in the game to inherit.

**Why it is worth building around.** Plating III on a Taunt carrier means the lane's damage concentrates on the one creature that ignores it, and the other four come out of the wave undamaged. That converts directly into regeneration timers that never start, which is the sink the economy model expects to absorb roughly 25% of a core player's income. A Vetch line is worth holding for the shards it does not cost you.

**Guardrails.** Mitigation, never immunity — a creature reduced to zero still leaves the field and enters regeneration. Plating never reduces damage the Ark takes. Two Plating carriers in a deployment do not stack; the highest tier applies.

### 3.2 Regrow — Loam

**Effect: regeneration time reduced 50%, plus 1% max HP per second recovered after three seconds without taking damage.** Both figures fixed at every tier; the tier decides who receives them.

| Tier | Covers |
|---|---|
| I | Self |
| II | Self and creatures deployed on adjacent tiles |
| III | Every creature in the deployment, plus creatures already in regeneration when the wave ends |

Plating stops damage happening. Regrow undoes it. The two are complementary rather than redundant, and a deployment carrying both is meaningfully cheaper to run than one carrying either.

**Why it is worth building around.** This is the clearest economic case of the four. Regeneration skips cost 1.5 shards per remaining minute and are meant to be a real drag on income. A Regrow III Loam in the deployment halves that line permanently. Loam stops being the body you tolerate to get Burrow and becomes the body you build a second copy of.

**Guardrails.** In-wave regeneration only fires after three damage-free seconds, so it never functions as sustain under fire — a creature being focused does not heal. It never revives. Tier III's reach into already-regenerating creatures is retroactive on wave completion only, never continuous. Tier II's adjacency is evaluated from end-of-wave positions, which are fixed — creatures reposition between waves, never during.

### 3.3 Litter — Skitter

**Effect: when a creature carrying Litter is consumed in a splice, it returns samples of its own traits.**

| Tier | Returns |
|---|---|
| I | 1 sample |
| II | 2 samples |
| III | 3 samples — one guaranteed fuse |

Samples are drawn from the traits the consumed creature actually carried, at the tier it carried them.

This is the only utility trait that acts outside combat, and it is deliberately the strongest of the four, because it addresses the sharpest pain in the game. Splicing consumes both parents. Every splice is a small loss, and the Founder confirmation dialog exists precisely because that loss has weight. Litter is the trait that pays some of it back.

**Why it is worth building around.** A player who keeps a Litter line running is compounding: every splice returns coverage, coverage fuses into tiers, tiers make the next wave cheaper. Skitter stops being the body you keep only because it is the sole source of Sprint.

**Guardrails.** This is safe to be generous with because of §1.7 — samples are coverage, never access. Litter cannot hand a player a counter they did not breed, at any tier, in any quantity. Its output is still bounded by generation under constraint 9, so a Litter III stockpile on a shallow line still cannot reach tier III coverage. Litter never triggers on retirement, only on splice consumption, so it does not become a farming loop detached from the splice charge economy.

### 3.4 Screen — Pale

**Effect: raiders inside the screened area require 2 seconds to acquire a target, and 2 seconds to re-acquire after their current target leaves the field.** Fixed at every tier; the tier decides the radius.

| Tier | Radius |
|---|---|
| I | 2 tiles |
| II | 4 tiles |
| III | The full depth of the lane |

**Why it is worth building around.** Back-line compositions — Hollow snipers behind a Vetch wall — are the most interesting formations available and currently the most fragile, because anything that gets past the front rank deletes them. Screen is what makes them viable. Screen III is the difference between a Hollow line that fires for the whole wave and one that fires for six seconds.

**Guardrails.** Acquisition delay only. Screen never reduces a raider's range, movement speed or damage, which is what keeps it clear of Lash and Courser. It does not conceal, does not redirect, and does not force targeting — redirection is Taunt's job and duplicating it would collapse Vetch's identity into Pale's.

**Distinguishing it from Chill.** Chill acts on movement and hard-answers exactly one raider. Screen acts on targeting and mildly affects all eight. Sharing a species is intentional: Pale is the control body, and control is the axis where "one hard lock plus one soft tempo effect" reads naturally. If playtest shows the two feel like the same trait, Screen is the one to rework — the counter is not negotiable.

---

## 4. Tier Grammar Check

All four express tier as **scope**, never magnitude — consistent with §1.3, where tier decides how much a trait covers and never whether it works.

| Trait | Fixed magnitude | Tier expands |
|---|---|---|
| Plating | −40% damage taken | Who is covered |
| Regrow | −50% regen time, 1%/s in-wave | Who is covered |
| Litter | 1 sample per tier | How many returned |
| Screen | 2s acquisition delay | Area covered |

Litter is the one variation: because it has no spatial or per-creature dimension, its tier scales quantity directly. This is consistent in spirit — three samples fuse into one of the next tier, so Litter III returns exactly one guaranteed coverage step, which is a scope statement expressed in the sample economy's own units.

---

## 5. What Playtest Should Read

The question these traits answer is roster diversity, so that is what to measure. Everything else is secondary.

**The primary metric: species share of deployments at wave 40.** Wave 40 is where the four-type cap binds and compositions stop being budget-driven. Take every deployment in the cohort and count how often each species appears.

- **Healthy:** no species below 10% of deployments.
- **Failing:** any species below 10%. Its utility trait is at fault, not its counter — the counter is mandatory by construction, so under-representation can only come from players fielding the body reluctantly.
- **Diagnostic:** if Ember and Hollow together exceed 50% of deployments, the utility traits collectively are too weak regardless of the individual numbers.

**Per-trait reads:**

| Trait | Read | Too weak | Too strong |
|---|---|---|---|
| Plating | Shards spent on regen skips by players holding Vetch vs not | No difference | Vetch in >80% of deployments |
| Regrow | Same sink, Loam holders vs not | No difference | Regen skips fall below 10% of core income |
| Litter | Share of splices with a Litter parent | Below 20% | Above 70% — it has become a tax rather than a choice |
| Screen | Share of deployments placing a ranged creature behind the front rank | Back-line comps stay rare | Front-rank compositions disappear |

**The failure mode to watch for specifically:** a player who holds all six species but deploys only four. That is the roster collapse the bible warns about, and it will not show up in acquisition metrics — only in deployment ones. Instrument deployments, not collection.

---

## 6. Open Items

**1. ~~Naming conflict on Carapace.~~ Resolved — renamed to Plating.** The anti-Tyranid checklist lists *carapace* explicitly as vocabulary to avoid, and the bible named the trait Carapace. The rule protects the art direction and the trait name had no dependencies outside the bible's §1.2 table, so the trait moved. Bible §1.2 and §1.6 both need the string replaced.

**2. Whether Screen is distinct enough from Chill.** Flagged in §3.4. Watch rather than redesign — the effects are mechanically unrelated and the concern is perceptual.

**3. Whether Litter should also trigger on retirement.** Currently no, to keep it attached to the splice charge economy. If retirement volume turns out to be high, revisit — it would make the roster cap gentler.

---

*Downstream: these four now need Codex entries per §4.7. The species stat lines were the other half of this problem and are settled in `broodline_species_stats.md`.*
