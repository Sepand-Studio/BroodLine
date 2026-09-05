# Broodline — Rig Proof Brief

*Production, the Phase 1 gate*

> **CURRENT — production spec.** `broodline_build_order.md` §3 makes the rig proof a gate rather than a milestone: nothing large should start before it clears. This document is what to build to clear it, and what "clear" means.

**One recommendation up front, and it changes the brief.** The build order says rig one species. **Rig two.** The risk this proof exists to retire is not whether a part attaches to a body — it is whether the *same* part attaches to *dissimilar* bodies. One species cannot test that. See §2.

---

## 1. What is being proved

Bible §10.3 commits to attachment-point rigging and states the reason plainly: it is what lets six species carry two swappable trait parts and an Instinct cue without hand-authoring every combination. Twenty-four creature assets and twelve raider assets rest on it.

**If the modular approach fails, the budget is not 36 assets.** It is six bodies × the combinations that actually need hand-authoring, which is a different project with a different cost and a different art direction. That is worth two weeks of proof before it is worth two months of production.

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

A trait part that works on both works on the four in between. A part that works on Vetch alone proves nothing about Loam, which has no legs, or Hollow, which is mostly neck.

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

### 3.1 Sockets are assigned by slot, not by trait

**Every trait part must work in either combat socket.** The part authored for Cinder mounts at `sk_dorsal` on one creature and `sk_flank` on another, and reads correctly in both.

**A fixed dorsal/flank split per trait was the obvious answer and it does not survive the splice rule.** Bible §1.2 gives each species one trait that reads naturally along the spine and one that reads along the flank — Vetch has Carapace and Taunt, Ember has Cinder and Splash, and the pattern holds for all six — so **base stock would always have been safe.**

Hybrids are not. Bible §2.2: Combat 1 locks from any of the parents' four combat traits, and Combat 2 rolls from the remaining three. Splice a Vetch with an Ember, lock Carapace, roll Cinder, and the child carries **two traits that both want the spine.** Nothing in the genetics prevents it and nothing should — constraining which traits can coexist to suit a socket layout would be the art pipeline dictating the breeding system.

**So: slot index determines socket.** Combat 1 goes to `sk_dorsal`, Combat 2 to `sk_flank`, whatever the traits are.

**This raises what the proof has to demonstrate.** Twelve parts across two bodies in two sockets is **forty-eight combinations**, not twenty-four. That is the cost of finding it now rather than after the parts are authored.

### 3.2 Raider sockets

Four raider bodies, one socket each.

| Socket | Carries |
|---|---|
| **`sk_kit`** | The variant kit — fused plate, front shield, furled membrane, deployed membrane |

**The Sunder mounts two kits at once** — plate and shield — at 1.5× body scale. That is the only case in the game where a body carries two kits, and the Hauler's `sk_kit` must accommodate both without collision. **It should be tested in the proof**, because discovering it after the Hauler is final is a re-rig.

---

## 4. What the proof must demonstrate

Nine items. All nine pass, or the approach is wrong.

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

---

## 5. Acceptance criteria

Pass or fail, judged before anyone is invested in the result.

**Item 2 is the one that decides the budget.** Forty-eight combinations, and the standard is that **every one reads as intentional.** Not "acceptable" — a part that looks bolted on wrongly to four of six bodies means the twelve-part budget is wrong, and it is better to know that from forty-eight renders than from a hundred and forty-four.

**Both sockets, every part.** A part that reads well on the spine and badly on the flank fails, because §3.1 means it will sit in both.

**Item 7 is the one that decides the art direction.** Bible §10.2 rule 1: if two species are confusable as flat black shapes, one is wrong. The proof must show both bodies at 40px **with parts attached**, because parts change silhouettes and a species that is distinct bare and muddy loaded has failed.

**Item 3's standard is the roster screen, not a hero render.** Two parts must both be identifiable at the size a creature appears in a list, not just in a turntable.

**Item 6's standard is recognition.** Bible §10.2 rule 3: "players must keep recognising their own animal." Runt and apex side by side should read as the same creature at two sizes, not as two creatures.

**Nothing here is judged on appeal.** Bible §10.7's anti-bio-horror checklist and the appeal-over-horror register are creative constraints and they will be assessed separately. This proof is about whether the pipeline works.

---

## 6. If it fails

**Fail item 2 across bodies** — parts do not travel between dissimilar silhouettes. Two options: reduce the dissimilarity of the six, which costs bible §10.2 rule 1 and the 40px read; or author per-body variants, multiplying the trait-part budget by up to six. **Neither is cheap and the decision is a design decision, not an art one** — it goes back to the bible rather than being absorbed by the art team.

**Fail item 2 across sockets only** — parts read on the spine but not the flank. Cheaper: two variants per trait rather than six, so twenty-four parts instead of twelve. Still a budget change, and it should be costed before it is accepted.

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

**Deliverables:** two rigged creature bodies with three sockets each, twelve socket-agnostic trait part assets, one Instinct cue with a trigger state, one raider body with two kits, a growth curve on one rig, and forty-eight combination renders plus 40px silhouette sheets.

**Total roster this proves out:** 36 assets — 6 creature bodies, 12 trait parts, 6 Instinct cues, 4 raider bodies, 8 variant kits.

**The three shape rules at bible §10.2 go to the illustrator verbatim, with no exceptions.** They are the load-bearing part of the brief and they are already written to be handed over.

---

## 9. Open questions

1. **Socket-agnostic parts are more demanding to author than fixed ones.** A shape that reads along a spine and along a flank has less room to be specific to either. If the artist finds that forty-eight combinations cannot all read as intentional, the fallback is a **third combat socket** with traits grouped so that no two in a group can co-occur — which is possible but requires working out the co-occurrence graph from bible §2.2, and it is not obviously solvable.
2. **Is Pale the right second body?** It is the most dissimilar to Vetch. Loam — segmented, legless, no neck — is arguably the harder attachment problem, and proving Vetch and Loam might retire more risk than Vetch and Pale. It is worth ten minutes of an artist's opinion before committing.
3. **Six Instinct cues at `sk_crown` may crowd the head.** Bible §10.4 already moves Instinct to a card badge for static display because two trait parts plus a third element muddies the silhouette. If the crown cue is visible in combat only, it may not need to be a mesh at all — which would cut six assets.
4. **The proof has no stated duration.** It should have one before it starts, because a gate with no deadline is not a gate.

---

*Owns: the socket standard, the proof's contents, acceptance criteria and failure paths. Does not own: the art direction (bible §10), species silhouettes (bible §1.2), raider bodies and kits (`broodline_raider_roster.md` §3), or build sequencing (`broodline_build_order.md`).*
