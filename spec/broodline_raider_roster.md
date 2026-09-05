# Broodline — The Raider Roster

*Design spec, what attacks and what it costs to build*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Rewritten against eight raiders and hard counters. Replaces the version built
> on twelve archetypes, armour types and a damage triangle.

---

## 1. What this document owns

`broodline_combat_numbers.md` §6 gives the eight raiders their stats and mechanics. This document owns everything else about them: the fiction, how eight raiders are built from four bodies, the recognition rules, telegraphing, spawn patterns, and the wave-60 capstone.

**The art budget is the reason it exists.** Bible §10.3 counts twenty-four creature assets and says nothing about raiders, which means the largest uncosted production line item in the project is the thing the player spends every wave looking at.

---

## 2. What a raider is

**Raiders are unbound splices.** Creatures that came out of the Fracture without a Warden, or got away from one. They are the same biology as the player's roster with nobody's hand on it.

That fiction costs nothing and earns three things. It explains why raiders are creature-shaped rather than robots or soldiers, which keeps the art budget on one silhouette language. It gives the Geneticist-Warden premise something to push against — the player is not fighting an army, they are cleaning up after the same accident that gave them their own animals. And it makes the counter system make sense in the fiction: a raider is answered by a trait because the trait is a biological answer to a biological problem, not because of a damage-type lookup.

**Raiders are never sympathetic and never villainous.** They are animals. Bible §10.7's rule against bio-horror applies to them as much as to creatures — no gore, no writhing, no chittering hordes. A Breaker is a big slow animal with a plate on it, and the reason it is frightening is that it does not stop.

---

## 3. The body budget

> **Four bodies, two variants each.**

Eight raiders is enough for the counter system and too many to model individually at the same quality as six player species. Sharing a mesh and a rig across two raiders halves the modelling cost and lets the variant carry the difference.

| Body | Variants | Shared | The variant carries |
|---|---|---|---|
| **Runner** | **Skirmisher** · **Courser** | Low quadruped mesh, run cycle | Scale — Skirmisher at 0.6×, Courser at 1.2× — plus Courser's head-down charge stride |
| **Hauler** | **Breaker** · **Bulwark** | Heavy slow-walking mesh | Breaker wears fused plate over the body; Bulwark carries a separate front shield it holds ahead of itself |
| **Lifter** | **Lash** · **Drift** | Long-limbed upright mesh with a dorsal membrane | Lash keeps the membrane furled and reaches with its limbs; Drift deploys it and flies |
| **Segment** | **Delver** · **Brood** | Low segmented body, burrow-capable rig | Delver submerges and surfaces; Brood splits |

**Two of these pairings do more work than the others.**

**Lifter is the elegant one.** A furled membrane and a deployed membrane is a variant, not a second mesh — but it produces a ground raider that reaches past the front line and an airborne raider that ignores lanes entirely. Two of the most mechanically distinct raiders in the game off one body.

**Brood costs nothing extra.** It splits into three Broodlings and each into three Mites, and all of them are the same Segment mesh at 0.55× and 0.3× scale. Thirteen bodies from one asset. The most visually dramatic raider in the game is also the cheapest.

### 3.1 Asset count

| | Assets |
|---|---|
| Raider bodies | 4 |
| Variant kits — plate, shield, furled membrane, deployed membrane, and so on | 8 |
| **Raider total** | **12** |
| Creature total, per bible §10.3 | 24 |
| **Combined** | **36** |

**Thirty-six assets is the whole game's character art.** That is the number to hold when scope pressure arrives, and it is achievable at a quality that a game leaning this hard on silhouette recognition requires.

### 3.2 Animation

| | Clips |
|---|---|
| Locomotion, one per body | 4 |
| Variant locomotion deltas — Courser's charge, Drift's flight, Delver's burrow cycle | 3 |
| Ability and attack, one per raider | 8 |
| Death, one per body | 4 |
| Brood split | 1 |
| **Total** | **20** |

Against eleven Codex behaviour previews and six creature Instinct behaviours, the animation load is real but bounded.

---

## 4. Recognition

Bible §10.5 requires that raiders read as threats at thumbnail size and are never mistaken for creatures. Two rules do that work.

**Raiders share a visual language creatures do not.** Creatures are symmetric, whole, and visibly cared for — bible §10.1's "someone's animal." Raiders are **asymmetric**, with visible unfinished structure: a plate grown over one shoulder and not the other, limbs of unequal length, a membrane torn on one side. Nothing gory, nothing wounded. They look like they were assembled without supervision, which is exactly what happened.

**The mechanic is the silhouette.** This is the more important rule and it applies without exception.

| Raider | What the player sees | What it means |
|---|---|---|
| **Skirmisher** | Small, and there are a lot of them | Volume |
| **Courser** | Same shape, twice the size, head down | Speed |
| **Breaker** | Plate fused across the body | Damage will not get through |
| **Bulwark** | A shield held out front, separate from the body | Nothing gets through until the shield does |
| **Lash** | Long limbs, membrane furled | It reaches |
| **Drift** | Membrane deployed, off the ground | You cannot touch it |
| **Delver** | Segmented, digging limbs, and a disturbance tracking up the lane | It goes under, and you cannot touch it |
| **Brood** | Segmented, visibly divisible | It becomes more |

A player who has never read the Codex should be able to look at a Bulwark and understand that the shield is the problem. That is what makes the counter system teachable by playing rather than only by losing, and it is the strongest argument for spending the art budget where §3.1 puts it.

**Variant pairs must not be confusable.** Skirmisher and Courser share a mesh at 0.6× and 1.2× — a factor of two, plus a distinct stride. If playtest shows players confusing them, **scale is the lever, not the mesh.** The pairing is what makes the budget work.

---

## 5. Telegraphing

Bible §4.12 requires wave composition visible before commitment. A counter system where the player finds out what is coming after they have placed is not a counter system.

**Before the wave starts, the player sees:**

- **Which raiders**, by silhouette and name
- **How many of each**
- **Which lane each stream enters**
- **The answering trait for each**, and whether the current deployment carries it

That last line is the one that matters. A pre-wave screen that shows a Courser and does not show that the player has no Chill in their five is technically compliant and practically useless. **The check should be explicit**: eight raider types, the answer each needs, and a mark against the ones the current deployment cannot handle.

**The check runs on coverage and lane assignment, not merely on presence.** A deployment holding Reach I against two Drifts in two lanes passes a presence test and loses the wave. Same three states the loss screen distinguishes at bible §4.11 — access, coverage, placement — applied before the wave rather than after it.

**This is not the same as telling the player what to do.** It names the gap, it does not fill it — the player still decides whether to swap a creature, accept the leak, or lose two integrity and get on with it. Bible §4.11's Wave Defeat screen does the same job after the fact; this does it before, which is cheaper for everyone.

**Region defence uses the same screen**, with composition derived from richness per `broodline_region_roster.md` §6.

---

## 6. Spawn patterns

How many arrive at once, and how. This is a design dial that costs nothing and is doing more work than the stats.

| Raider | Unit | Interval | Note |
|---|---|---|---|
| **Skirmisher** | **8** | 1.5s apart | The pattern that makes Splash necessary — see below |
| **Courser** | 2, rising to 4 | Simultaneous | Arrive together or Chill I answers the wave |
| **Drift** | 3 | Simultaneous, in line | Tests Reach coverage across lanes |
| **Delver** | 2–3 | Simultaneous | One per lane. Untargetable end to end unless Burrow forces them up |
| **Lash** | 1 | — | Arrives alone, always |
| **Brood** | 1 | — | Becomes thirteen on its own |
| **Bulwark** | 1–2 | Simultaneous | Pairs from wave 32, where the Sprint coverage lesson lands |
| **Sunder** | 1 | — | Wave 60 only. Never alongside Breaker or Bulwark |
| **Breaker** | 1 | — | Never needs company |

**Skirmisher's eight-at-1.5-seconds is doing most of the work in the whole opening.** `broodline_combat_numbers.md` §5 puts retarget delay at 0.4s, so a single-target defender loses bodies through the gaps even when it out-DPSes the wave on paper. That interval is why Splash is a counter and not a convenience, and it should be tuned before anything in the raider stat table is touched.

**Simultaneous arrival is what makes coverage tiers matter.** Two Coursers arriving together demand Chill II; two Coursers arriving nine seconds apart do not. Six of the eight counters scale by simultaneity, so the spawn pattern is what converts a coverage tier into a felt difference.

---

## 7. The Sunder

Campaign chapter 8 is the combination gauntlet and has no new raider, which left it eight waves of difficulty ramp with nothing new in them. This is the answer, and it costs one variant.

> **The Sunder.** Hauler body at 1.5× scale, carrying **both** Plate and Shield.
> Answered by **Pierce and Sprint together.** Appears once, at wave 60.

It is the only raider in the game requiring two counters at once, and that is a deliberate, single exception rather than a new category.

**Why it is legal.** Bible §4.5's rules are that no raider is ever immune to a tier-I answer, and that a legal wave never sends two raiders answered by the same trait. The Sunder violates neither — Pierce I and Sprint I both work on it. What it does is demand two answers on one body, which is a difficulty spike, and wave 60 is the one place in the design where a difficulty spike is the point.

**Why it is worth it.** Core tier 12, the last rung of the game's longest progression track, sits behind wave 60. A capstone that can be beaten by stacking one species would make that rung meaningless. The Sunder requires a Hollow and a Skitter alive at the same time, which is proof the player engaged with the breeding system rather than optimising into a monoculture.

**It is also chapter 8's only source of escalation.** `broodline_waves_45_52.md` §6 found that the composition ceiling is reached at wave 51 — a legal three-lane wave cannot exceed about 1,665 points, and eight further waves have the same eight raiders, the same four-type cap and the same coverage tiers. The Sunder was designed as a capstone and turns out to be the only way chapter 8 can be harder than chapter 7.

**It cannot share a wave with Breaker or Bulwark.** Bible §4.5 forbids two raiders answered by the same trait, and the Sunder is answered by Pierce and Sprint — which are Breaker's and Bulwark's answers. The obvious capstone, a Sunder escorted by the two variants it combines, is illegal. It would also have been the wrong wave: four bodies wanting Pierce is one Pierce III carrier's problem, and the wave would have been easier than wave 51. Removing both from wave 60 is what pushes it to five counters across five species. See `broodline_waves_53_60.md` §5.

**Any future dual-lock raider excludes both its parents from its own waves**, which sharply limits what can be built around one — a further argument for the rule below.

**Its guardrails are tight and non-negotiable:**

- **Exactly one dual-lock raider exists.** No second one, in any season, ever
- **It appears at wave 60 only.** Never in region defence, never in raids, never in an event
- **It is a variant, not a body.** A scaled Hauler wearing both kits — no new mesh, no new rig
- **No seasonal raider may carry two locks.** Bible §4.12's sidegrade principle applies to raiders as much as to species

---

## 8. Raiders never appear in raids

Raids are player against player. The defender's escorts fight the attacker's creatures and nothing else — no raiders, no neutral third party, no environmental threat.

This is carried forward unchanged and it matters more now than it did. Raid combat runs the same engine with roles inverted, per bible §4.9, and every raider in this document is designed against a defender who is emplaced in pockets with a wall at the front. An escort convoy is none of those things. Dropping raiders into a raid would produce encounters nothing has been tuned for, and it would blur the one clean line the design draws between PvE and PvP.

---

## 9. Guardrails

- Eight raiders, four bodies, two variants each. Seasonal raiders reuse existing bodies
- The mechanic is the silhouette — the answering trait must be readable off the body at thumbnail size
- Raiders are asymmetric and unfinished; creatures are symmetric and whole. Never confusable
- No gore, no viscera, no writhing hordes. Bible §10.7 applies to raiders in full
- Composition, counts, lane assignment and the answering traits are visible before every wave
- Escalation is by count and spawn pattern, never by raider stats
- Exactly one dual-lock raider exists, at wave 60, and it is a variant
- Raiders never appear in raids
- No raider is ever sold, unlocked, or skipped by any purchase

---

## 10. Open questions

1. **Skirmisher's 1.5-second interval is the highest-leverage untested number in the combat design.** It determines whether Splash is a counter or a convenience. Test at 1.0s and 2.0s before touching any raider stat.
2. **Are Skirmisher and Courser too similar at a glance?** A factor-of-two scale difference plus a distinct stride should carry it, and it is the pairing most likely to fail. Scale is the lever if it does; splitting the mesh would cost the budget its best saving.
3. **Does Drift need a distinct flight altitude per lane?** Three arriving in line is the pattern; whether they fly at the same height or stagger affects whether one Reach carrier can cover them and therefore what Reach II actually buys.
4. **Twenty animation clips is an estimate, not a quote.** The variant locomotion deltas in particular — Courser's charge, Drift's flight, Delver's burrow — may be full clips rather than deltas depending on rig decisions that have not been made.
5. **Should the pre-wave answer check be toggleable?** Explicitly naming which raiders the current deployment cannot handle is a large accessibility win and a small loss of discovery. Leaning always-on, with the Codex carrying the discovery instead.
6. **Seasonal raiders have no design yet.** The body budget assumes they reuse existing meshes, which constrains what a new raider can mechanically be — a new lock needs a new silhouette, and a new silhouette is a new body.

---

*Owns: raider fiction, the four-body budget and variant pairings, asset and animation counts, recognition rules, telegraphing requirements, spawn patterns, the Sunder, and the raids exclusion. Does not own: raider stats and mechanics (`broodline_combat_numbers.md` §6), region defence composition (`broodline_region_roster.md` §6), the introduction schedule (`broodline_campaign_structure.md` §2.1), or Codex raider entries (`broodline_trait_codex.md` §5.4).*
