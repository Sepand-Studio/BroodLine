---
status: current
folder: 03-technical
note: >
  The socket standard and the Phase 1 art gate.
---

# Broodline — Rig Proof Brief

*Production, the Phase 1 gate*

> **CURRENT — production spec.** `broodline_build_order.md` §3 makes the rig proof a gate rather than a milestone: nothing large should start before it clears. This document is what to build to clear it, and what "clear" means.

**One recommendation up front, and it changes the brief.** The build order says rig one species. **Rig two.** The risk this proof exists to retire is not whether a part attaches to a body — it is whether the *same* part attaches to *dissimilar* bodies. One species cannot test that. See §2.

---

## 1. What is being proved

Bible §10.3 commits to attachment-point rigging and states the reason plainly: it is what lets six species carry two swappable trait parts and an Instinct cue without hand-authoring every combination. Twenty-four creature assets and twelve raider assets rest on it.

**If the modular approach fails, the budget is not thirty-odd assets.** It is six bodies × the combinations that actually need hand-authoring, which is a different project with a different cost and a different art direction. That is worth two weeks of proof before it is worth two months of production.

**The specific risk.** Twelve trait parts must each mount on any of six bodies — seventy-two fit combinations. The bodies are deliberately dissimilar; bible §10.2 requires all six distinguishable as flat black shapes at 40px, which means a low four-legged dome and a broad hanging wing arc are both in scope. A Cinder crest authored to sit on Ember's head crest has to also sit on Loam, which has no legs and a blunt snout.

Two ways that resolves, and the proof decides which:

| | Assets | Risk |
|---|---|---|
| **One part per trait**, placed by socket transform | 12 | A part that reads correctly on two bodies and wrongly on four |
| **Per-body variants** | up to 72 | Budget is wrong by a factor of six |

---

## 2. Rig two species, not one

The build order names **Vetch** — it carries the tutorial parents, it is Founder 2, and it is Cinderplate's body, so it is the most-seen creature in the game.

**Add Pale.** The two are the most dissimilar pair in the roster:

| | Silhouette |
|---|---|
| **Vetch** | Low dome, four stubby legs, no neck |
| **Pale** | Broad wing arc, small hanging body |

A trait part that works on both works on the four in between.

**Pale is chosen for the problem it shares with two other species, not for being the most different.** Its body is small and its mass is in its wings, so both combat sockets sit on a minor volume while the silhouette is carried by an appendage. **Hollow (tiny body, stilt legs) and Skitter (small body, six long legs) have the same problem** — half the roster. Proving the sockets on Pale retires it for all three.

**Loam is the tempting alternative and it is the wrong test.** The argument for it is that it is segmented, legless and neckless. But no socket attaches to a leg: §3 puts them on the spine or upper mass, the side of the body, and the head or leading edge. Leglessness is a silhouette difference, not an attachment difference, and Loam's long back is among the easiest dorsal surfaces in the roster.

**What Loam does hold that Pale does not** is a deforming parent. A socket is a transform parented to a bone, so on a segmented body a part rides one segment and moves with it. Neither Vetch nor Pale is segmented, so the proof does not exercise it — which is why §4 item 10 exists.

**The cost of the second body is small against what it retires.** One extra body and one extra rig, in exchange for retiring the seventy-two-combination risk rather than deferring it to the fifth species — where it would surface after four bodies were already built to a standard that does not hold.

---

## 3. The socket standard

**Standardised before the first creature is modelled**, per bible §10.3 — retrofitting modularity onto hand-built creatures is the most expensive mistake available.

Three sockets per creature body.

| Socket | Carries | Placement |
|---|---|---|
| **`sk_dorsal`** | One combat trait part | Along the spine or upper mass. The primary read at 40px |
| **`sk_flank`** | One combat trait part | Side of the body, below the dorsal line. Visible in profile, must not occlude the dorsal part |
| **`sk_crown`** | The Instinct cue | Head or leading edge. Smallest of the three |

**Each socket is a named transform** — position, orientation and uniform scale — authored per body. A trait part is authored once, in a canonical orientation, and the body's socket transform places it. **No per-body part geometry.**

**The two combat sockets must be spatially separate enough that any two parts can coexist without collision.** Bible §10.4 makes both combat traits visible on the body and calls that the single most important functional requirement in the art direction; two parts that overlap defeat it.

**Author the two combat sockets to present comparable surfaces.** Similar local curvature and similar scale at `sk_dorsal` and `sk_flank`, on every body. A part authored once reads in both sockets when both sockets hand it a surface of the same character; it fails when one is a tight convex ridge and the other a broad flat plane. This costs nothing at authoring time and it is the single cheapest thing that can be done to make §4 item 2 pass.

### 3.1 Sockets are assigned by slot, not by trait

**Every trait part must work in either combat socket.** The part authored for Cinder mounts at `sk_dorsal` on one creature and `sk_flank` on another, and reads correctly in both.

**A fixed dorsal/flank split per trait was the obvious answer and it does not survive the splice rule.** Bible §1.2 gives each species one trait that reads naturally along the spine and one that reads along the flank — Vetch has Carapace and Taunt, Ember has Cinder and Splash, and the pattern holds for all six — so **base stock would always have been safe.**

Hybrids are not. Bible §2.2: Combat 1 locks from any of the parents' four combat traits, and Combat 2 rolls from the remaining three. Splice a Vetch with an Ember, lock Carapace, roll Cinder, and the child carries **two traits that both want the spine.** Nothing in the genetics prevents it and nothing should — constraining which traits can coexist to suit a socket layout would be the art pipeline dictating the breeding system.

**So: slot index determines socket.** Combat 1 goes to `sk_dorsal`, Combat 2 to `sk_flank`, whatever the traits are.

**Every pair of the twelve traits can co-occur, so no socket layout can separate them.** Combat 1 locks from any of the parents' four traits and Combat 2 rolls from the remaining three, so a child carries any two of its parents' four. Closing that over generations from base stock reaches **all sixty-six possible pairs** — the co-occurrence graph is complete. Grouping traits so that no two in a group co-occur is a colouring of that graph, and colouring a complete graph on twelve nodes takes twelve colours: **one socket per trait**. There is no three-socket or four-socket arrangement that makes a fixed trait-to-socket assignment safe. Slot index is not the preferred answer, it is the only one.

**This raises what the proof has to demonstrate.** Twelve parts across two bodies in two sockets is **forty-eight combinations**, not twenty-four. That is the cost of finding it now rather than after the parts are authored.

### 3.2 Raider sockets

Four raider bodies, one socket each.

| Socket | Carries |
|---|---|
| **`sk_kit`** | The variant kit — fused plate, front shield, furled membrane, deployed membrane |

**The Sunder mounts two kits at once** — plate and shield — at 1.5× body scale. That is the only case in the game where a body carries two kits, and the Hauler's `sk_kit` must accommodate both without collision. **It should be tested in the proof**, because discovering it after the Hauler is final is a re-rig.

---

## 4. What the proof must demonstrate

Ten items. All ten pass, or the approach is wrong.

| # | Demonstration |
|---|---|
| **1** | Vetch and Pale bodies, rigged, with all three sockets authored |
| **2** | **All twelve trait parts** mounted on both bodies, **in both combat sockets**, via socket transform only — forty-eight combinations, no per-body geometry |
| **3** | Two parts on one body simultaneously, dorsal and flank, no collision, both readable |
| **4** | **Cinderplate** — the Vetch body carrying Ember's Cinder part, per bible §10.9. The game's mascot and store icon, produced by the pipeline rather than by hand |
| **5** | One Instinct cue at `sk_crown`, plus its **trigger state** — the animation or colour shift bible §10.4 requires when Last Stand fires or Skittish repositions |
| **6** | **The growth proportion curve** — runt to apex on one rig, mass moved outward, same profile. Bible §10.2 rule 3, and it must be a curve over one rig rather than separate assets |
| **7** | Both bodies as flat black shapes at 40px, distinguishable, with and without parts attached |
| **8** | **Damage as posture** — stance, breathing and desaturation, no blood, no visible trauma. Bible §10.7 |
| **9** | The Hauler body carrying plate and shield simultaneously, per §3.2 |
| **10** | **A socket on a deforming parent.** A part mounted to a segmented, articulating section — a dummy is sufficient, not a third body — showing what the socket transform does when the surface under it moves. §2 explains why Vetch and Pale cannot answer this, and Loam, Skitter and Hollow all need the answer |

---

## 5. Acceptance criteria

Pass or fail, judged before anyone is invested in the result.

**Item 2 is the one that decides the budget.** Forty-eight combinations, and the standard is that **every one reads as intentional.** Not "acceptable" — a part that looks bolted on wrongly to four of six bodies means the twelve-part budget is wrong, and it is better to know that from forty-eight renders than from a hundred and forty-four.

**Both sockets, every part.** A part that reads well on the spine and badly on the flank fails, because §3.1 means it will sit in both.

**Item 7 is the one that decides the art direction.** Bible §10.2 rule 1: if two species are confusable as flat black shapes, one is wrong. The proof must show both bodies at 40px **with parts attached**, because parts change silhouettes and a species that is distinct bare and muddy loaded has failed.

**Item 3's standard is the roster screen, not a hero render.** Two parts must both be identifiable at the size a creature appears in a list, not just in a turntable.

**Item 6's standard is recognition.** Bible §10.2 rule 3: "players must keep recognising their own animal." Runt and apex side by side should read as the same creature at two sizes, not as two creatures.

**Item 10's standard is a stated rule, not a beautiful result.** The deliverable is the socket standard saying what happens — the part rides one segment, or it deforms with the section, or segmented bodies carry the socket at a fixed anchor. Any of those can be right; leaving it unstated is what cannot.

**Nothing here is judged on appeal.** Bible §10.7's anti-bio-horror checklist and the appeal-over-horror register are creative constraints and they will be assessed separately. This proof is about whether the pipeline works.

---

## 6. If it fails

**Fail item 2 across bodies** — parts do not travel between dissimilar silhouettes. Two options: reduce the dissimilarity of the six, which costs bible §10.2 rule 1 and the 40px read; or author per-body variants, multiplying the trait-part budget by up to six. **Neither is cheap and the decision is a design decision, not an art one** — it goes back to the bible rather than being absorbed by the art team.

**Fail item 2 across sockets only** — parts read on the spine but not the flank. Cheaper: two variants per trait rather than six, so twenty-four parts instead of twelve. Still a budget change, and it should be costed before it is accepted. **This is the fallback**; a third combat socket is not one, for the reason §3.1 gives.

**Fail item 6** — growth needs separate assets rather than a proportion curve. That is four assets per species instead of one, and it was the original justification for 3D. If growth cannot be a curve, **re-open the 2D question**, because the reasoning at bible §10.3 no longer holds.

**Fail item 7** — silhouettes are not distinguishable at 40px with parts attached. That is an art direction problem rather than a pipeline problem, and it is recoverable by redesigning bodies before they are final. **This is exactly why the proof happens first.**

**Fail item 9** — the Hauler cannot carry two kits. Cheapest of the failures: the Sunder becomes a scaled Hauler with a single combined kit asset, one extra asset, no structural change.

---

## 7. What this is not

**Not a look development pass.** Colour, material, lighting and the natural-history register at bible §10.7 come after the pipeline is known to work. Producing beautiful renders of an approach that then has to be abandoned is the expensive version of this exercise.

**Not a full species.** Vetch and Pale need to be right enough to test attachment, silhouette and growth. They will be remade.

**Not a performance test.** `broodline_waves_37_44.md` §7 flags close to a hundred entities on screen at wave 44 and it recurs across chapter 8. That is a separate proof against the combat engine, and it should run in parallel rather than being folded in here.

---

## 8. The commission

For whoever is briefed.

**Read first:** bible §10 in full, then §1.2 for the six species and `broodline_raider_roster.md` §4 for the recognition rules. The Character Bible in the design handoff bundle is the visual reference.

### 8.1 What to make

**Deliverables:** two rigged creature bodies with three sockets each, twelve socket-agnostic trait part assets, one Instinct cue with a trigger state, one raider body with two kits, a growth curve on one rig, and forty-eight combination renders plus 40px silhouette sheets.

**Total roster this proves out:** 30 assets — 6 creature bodies, 12 trait parts, 4 raider bodies, 8 variant kits — **plus up to 6 Instinct cues, contingent** on §9.3. That contingency does not change anything asked for above: the proof delivers one cue either way, because it is what proves `sk_crown` works.

### 8.2 The shape rules

**The three shape rules at bible §10.2 go to the illustrator verbatim, with no exceptions.** They are the load-bearing part of the brief and they are already written to be handed over.

### 8.3 The performance budget

**Per-asset budget — MEASURED.** Measured on the **A14 / 4 GB reference device** (iPad Air 4, `iPad13,1`): 40 combinations at 104 entities, 300 frames each, rigs animated every frame. **No margin is applied, because this is the floor device rather than a proxy for one** — the measured value is the budget. The earlier 30% reduction existed only to cover an A14→A13 gap that the floor change on 2026-09-14 removed. Source and reasoning: `implementation/2026-09-08-phase0-entity-count-proof.md`.

| | Budget | Why |
|---|---|---|
| Triangles per body | **10000** | Wave 44 puts 104 entities on screen at once. 10000 held 60 fps on the floor device at every bone count swept; 16000 failed at every bone count |
| Bones per rig | **Not a constraint** | 80 bones per rig held 60 fps at every triangle count; no ceiling was reached |
| Materials per body | **2** | Each material is a draw call before batching |
| Trait part | Within the body budget, not additional | A body carries two parts, and a crown cue if §9.3 keeps them |

**This budget is valid for production assets, and it carries one measured caveat and one unmeasured one.**

*Measured:* at 10000 triangles the worst row is **12.58 ms GPU p95 against a 16.667 ms frame** — roughly a quarter of the frame is unspent. **That headroom is not spare capacity to re-budget.** It is what the next paragraph is reserved against.

*Unmeasured:* the sweep drove rigs with a `BoneAnimator` writing local rotations directly, and drew them as synthetic meshes. **A real `Animator` evaluating a graph, sampling curves and blending clips — and a production shader with real textures — were never measured.** Both cost time this budget has not seen. A rig that is expensive to *evaluate* rather than expensive to *skin* remains unmeasured, which is the one way a body can hit 10000 triangles and still be too expensive.

**What is still owed against these numbers is not a re-measure on different hardware. It is a re-run on real geometry.** `broodline_client_architecture.md` §4 requires the proof be made *"with real creature meshes rather than capsules"*, and no synthetic mesh can satisfy that. **When this commission delivers Vetch and Pale, re-run the same harness with `SyntheticCreature.Build` swapped for the delivered prefabs and every threshold unchanged — before any of the remaining four species are modelled.** §7 makes these two bodies throwaway, *"They will be remade"*, so a number that moves on that run moves it for the four species nobody has paid for yet. That is the whole point of running it here.

**Bones are measured, and they are not what constrains you.** The Phase 0 harness now animates every bone every frame and sweeps rigs from 12 to 80 bones. Going from 12 bones to 80 — nearly seven times as many — costs **0.3 to 0.7 ms of CPU across all 104 on-screen creatures combined**, against a 16.667 ms frame, and about 5 MB of memory. Every bone count tested held 60 fps at every triangle count that passed, so no bone ceiling was found and none is quoted: inventing one from an unreached limit would be worse than saying it is not binding.

**Rig the creatures as the animation requires.** If a body reads better at 60 bones than at 40, use 60. The measured cost of that decision is a fraction of a millisecond. What this does *not* cover is expensive animation *evaluation* — deep blend trees, many simultaneous layers, heavy IK — which is a different cost from the rig's bone count and is not measured here. If the rig depends on something in that category, flag it.

**The number that does bind is triangles**, and it binds on the GPU.

**The triangle figure is optimistic, and by a knowable amount.** Every vertex in the synthetic meshes carries a single bone influence, while rigged art normally carries two to four — this project's own quality settings allow four. Per-vertex skinning is therefore cheaper in the proof than in the real thing, which inflates the triangle number rather than the bone one. The correction is not another synthetic run: §8.3 requires this harness to re-run against Vetch and Pale once they are delivered, and real meshes carry real weights. **Treat 10000 as an upper bound that will move down, not a target to fill.**

**The 2026-09-14 floor change raised this number and did not make it safer.** Withdrawing the A13 margin took the budget from 7000 to the measured 10000, but the margin was never what covered *this* — single-influence vertices, an unmeasured `Animator`, and a shader that is not a production shader are gaps in the harness, not in the hardware, and no device change touches them. **The headroom that covers them is the ~25% of frame left unspent at 10000** (§8.3), and it is the reason the re-run against real meshes is a gate rather than a formality.

**A body over budget is not a rejection of the art**; it is a request to hit the number, and it is far cheaper to hear now than after six bodies are final.

---

## 9. Open questions

1. ~~**Socket-agnostic parts are more demanding to author than fixed ones.**~~ **The difficulty is real; the fallback proposed here was not.** A shape that reads along a spine and along a flank still has less room to be specific to either, and that is what §4 item 2 exists to test. But the third combat socket named here **cannot work at any socket count**: all sixty-six trait pairs are reachable by splicing, so grouping traits to keep them apart would need one socket per trait. §3.1 carries the derivation. **The fallback is §6's** — two variants per trait, twenty-four parts instead of twelve — which is costed and does not depend on separating traits that cannot be separated.
2. ~~**Is Pale the right second body?**~~ **Resolved — Pale, and for a different reason than originally given.** The case was silhouette dissimilarity. The stronger case is that Pale's small body with mass in an appendage is the same attachment problem Hollow and Skitter have, so proving it retires half the roster, while Loam's segmentation is a singleton. Loam's leglessness is not an attachment problem at all — no socket attaches to a leg. §2 carries the reasoning. The one risk Loam held that Pale does not is a deforming parent, which is now **§4 item 10** and costs a dummy rather than a body. An artist's opinion is still worth ten minutes, but the question no longer blocks the commission.
3. **How many Instinct cues does production need — six, one, or none?** Bible §10.3 budgets six and calls a creature "one body plus two trait parts plus one Instinct cue"; §10.4 gives Instinct a card badge when static, *because* "a third would muddy the silhouette", and behaviour plus a trigger state in combat. Neither of §10.4's channels is body geometry, so the two sections disagree and §10.3's six are marked contingent there pending this.

   **This does not block the commission.** The proof asks for **one** cue (§4 item 5) under every possible answer, and one cue is also what validates `sk_crown` — a socket that has never carried geometry is not standardised, and discovering its scale or orientation is wrong during a later retrofit is precisely the expense bible §10.3 warns about. Author the socket on every body regardless; a named transform costs nothing and keeps the option real.

   **What decides it is playtest, not analysis.** The specific question is whether **Bloodscent, Vanguard and Overwatch** are identifiable in a busy wave. Those three have no trigger in bible §1.4 — no animation change, no colour shift — so a trigger state can only ever express the other half of the roster, and targeting behaviour is their only signal. If a wave proves that insufficient, the cheapest answer is a 2D combat icon reusing the card badge, as *Beyond All Reason* does across four hundred unit types, rather than six meshes on a silhouette §10.4 already calls full.
4. ~~**The proof has no stated duration.**~~ **Resolved — three weeks**, per `broodline_whats_left.md` §2, which already records "the rig proof runs three weeks" as a taken decision. A gate with no deadline is not a gate.
5. **§4 item 10 has an answer for Loam, and it is a rule without a rig.** §5 asks for "a stated rule, not a beautiful result", and the rule is stated: **the part rides one segment.** Loam is authored with its bone chain rooted at the mass centre — `seg0 – seg1 – root – seg3 – seg4`, `root` in the middle rather than a `seg2`, because `CreatureMotion.Awake` does `transform.Find("root")` and falls back to the creature transform when there is none — and `sk_dorsal` sits directly over that middle segment. The derivation is in `client/Assets/Creatures/Recipes/SpeciesRecipes.cs`'s Loam header.

   **What is not built, and this half must travel with the answer.** `CreatureGenerator` parents **every** socket to the creature root, not to a bone, and `CreatureAssembler.Mount` finds it there. So no socket on any body follows a bone today: Loam's `sk_dorsal` is over the middle segment *by placement*, and would not move if that segment deformed. Making it literally ride the bone is a generator change, and it was correctly declined by a content task rather than smuggled in.

   **Item 10 is therefore answered to the letter of §5 and not to its spirit.** The rule exists and is discoverable where a future recipe author will meet it; the demonstration §4 asks for — a part on a section that actually articulates — has not been made. An artist commission inherits the rule and the gap together.

---

*Owns: the socket standard, the proof's contents, acceptance criteria and failure paths. Does not own: the art direction (bible §10), species silhouettes (bible §1.2), raider bodies and kits (`broodline_raider_roster.md` §3), or build sequencing (`broodline_build_order.md`).*
