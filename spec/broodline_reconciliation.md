# Broodline — Spec/Design Reconciliation

*Decision register, v9 — complete. Nine written specs vs. the 20-screen design handoff.*

---

## What this is

> **Superseded as a design reference.** `broodline_bible.md` is now the current design. This document remains the record of *why* each decision was made, which the bible deliberately does not carry.

The two artifacts describe the same game at the meta layer and different games at the core. Territory, nodes, monetization and the Ark relocation loop line up almost exactly. The creature model, the counter model and the combat layer did not.

Every conflict is a **decision slot** stating what each side says, what rides on it, a recommendation, and the rework cost. All three parts are closed. Part 0 holds two corrections that need no decision.

**Key:** `[S]` specs win · `[D]` designs win · `[X]` third option · `[ ]` still open

## Where this stands

| # | Decision | Outcome | |
|---|---|---|---|
| 1.1 | Counter model | Hard counters, no damage floor | `[D]` |
| 1.2 | Creature model | 6 species, 3 slots: 2 combat + 1 Instinct | `[X]` |
| 1.3 | Instinct | 6 behaviors, inherited independently of body | `[X]` |
| 1.4 | Trait tier | Coverage, not access | `[X]` |
| 1.5 | Samples vs traits | Access is bred, coverage is fused | `[X]` |
| 1.6 | Apex | A trait class, not a tier. Renamed **Aberrant** | `[X]` |
| 1.7 | Instinct tiering | Untiered. No samples, no fusing | `[X]` |
| 2.1 | Lineage View | Build it, with a species composition strip | `[S]` |
| 2.2 | Gene Vault | Two screens, six facilities remapped | `[X]` |
| 2.3 | Combat terrain | Designs, lane count as difficulty dial | `[D]` |
| 2.4 | Growth stages | Cosmetic. Feeding cut | `[X]` |
| 2.5 | Art register | Designs, three carried requirements | `[D]` |
| 3.1 | Mutation rate | 9%, with an Aberrant sub-roll | `[X]` |
| 3.2 | Affinity | +12%, capped at one, inheritance only | `[X]` |
| 3.3 | VIP ladder name | Geneticist Tier | `[S]` |
| 3.4 | Store tab | No Store tab, contextual entry | `[D]` |
| 3.5 | Recessive traits | Adopt | `[D]` |
| 3.6 | Roster cap | 20 as floor, scaling with Hatchery | `[X]` |
| 3.7 | Screen count | Rebuild inventory; six screens missing | `[X]` |

**All conflicts are settled.** What remains is new work, not reconciliation — see Next steps.

Ten derived constraints at **1.8** are consequences of the above and are not open.

---

## Part 0 — Corrections, not decisions

### 0.1 — The splice screen has no destruction warning — **RESOLVED**

*Closed by `broodline_splice_confirm_spec.md`, which supplies the copy, the two confirmation levels, and the updated screen requirements.*


`Splice Chamber.dc.html` is a static file with no logic block, despite the handoff README stating all screens are interactive and instructing implementers to read its `renderVals()` first. It has none.

The screen ends on a Cost row and a **Begin Splice** CTA. Parent consumption is never mentioned. The player learns both parents are gone on the following screen, at reveal.

The genetics spec calls the pre-splice warning non-negotiable and names accidental consumption as the most likely source of refund requests and one-star reviews. The design ships the exact failure the spec was written to prevent.

Required:
- Destruction line on Splice Chamber above the CTA, in the same language the confirm uses
- Confirm step between CTA and reveal
- Named second dialog when a Founder is selected

### 0.2 — "Founder" carries two meanings

Specs: the player's first five named creatures, at the root of every lineage tree. Designs: the six base species. Both appear in player-facing copy.

Rename the design sense to **base species**. The spec meaning carries the FTUE's emotional anchor and should keep the word.

### 0.3 — Four store packs promise trait pulls

Breeder Bundle includes one rare trait pull, Lab Expansion three, Geneticist's Vault eight, and Splice Roulette is described as a rare-trait wheel.

Under constraint 1 no purchase may grant trait access. Shipped as written, these break the guardrail holding up the entire hard-counter decision — and they break it on the store page rather than through any deliberate design choice, which is how this kind of failure usually happens.

Correction: **pulls grant samples, which are coverage.** Reword all four packs, the Roulette description, and the Season Pass's "guaranteed rare-trait creature," which must be a species creature rather than counter access.

---

## Part 1 — Settled

*In the order the decisions were forced. Each one made the next answerable.*

### 1.1 — Counter model → **hard counters, as designed. No damage floor.** `[D]`

Eight raiders, each with exactly one answering trait. A wrong-trait defense does not work.

The soft floor from v1 is withdrawn. It solved the right problem in the wrong place: if enough raw power substitutes for the right trait, Alliance Rally's thesis collapses — that screen exists to show a Breaker wall that will not break regardless of power committed without Pierce. That's the strongest single idea in the bundle.

The guardrail moves to **supply**. Hard counters are safe when the sample economy hands every player all eight counter traits, free, on a reliable schedule. The wall stays; the key is always issued. Wave Defeat covers the other end — it names the raider that broke through and the trait that answers it, then grants a free retry with no paywall.

### 1.2 — Creature model → **6 base species, 3 slots: 2 combat + 1 Instinct** `[X]`

Six species: the Character Bible has done real work here, each reads at 40px, and every one answers at least one raider so none is a skip. Ten chassis has no design work behind it.

Two combat traits, not three. Under hard counters this is load-bearing rather than a depth setting. Thin creatures are what force a wide roster, and a wide roster is what keeps splicing necessary. A third combat trait would let one creature answer most of a wave and would quietly weaken the consumption economy the whole game rests on.

The third slot is safe **only because Instinct counters nothing** (see 1.3, 1.7). It adds a breeding axis without adding counter coverage.

**How the slots fill.** Never reconciled until now — the specs had the player locking two traits from a combined pool of eight, the designs showed pure probability with no lock at all. 1.2 settled the slot count and left this open.

The pools are separate: combat slots fill only from the parents' four combat traits, the Instinct slot only from their two Instincts.

- **One combat trait is locked by the player**, guaranteed to carry
- **One combat slot rolls** from the remaining three, with displayed odds
- **The Instinct slot rolls** from the two parent Instincts, modified by affinity and DOM/REC

Steering matters more under hard counters than it did under a damage triangle. Constraint 2 guarantees a player can *acquire* a species; without a lock it would not guarantee they can keep its counter through a generation, and breeding toward Reach would be luck rather than a plan.

One lock of three rather than the specs' two of four. With only three slots, locking two would make splices near-deterministic and solve the game inside a month.

Parent-body choice at splice survives alongside it.

### 1.3 — Instinct → **6 behaviors, inherited independently of body** `[X]`

One per species as signature would make Instinct flavor with nothing to breed toward. Decoupling it from the body makes it a target: a Vetch-bodied creature can carry Hollow's Instinct.

That gives the splice screen a third odds row that changes what a pairing is worth, and gives a deep line a reason to exist beyond generation count.

Six is legible and affordable. Trigger states must be visible in Wave Defense or it is a hidden stat.

### 1.4 — Trait tier → **coverage, not access** `[X]`

Tier never decides *whether* a counter works. It decides *how much it covers.*

Chill I slows one Courser; Chill III slows a pack. Reach I answers a flier in one lane; Reach III covers more sky. Splash I catches two of the eight Skirmishers a wave sends; Splash III catches five. Cinder I burns Brood's first generation of splits; Cinder III catches the second — the raider's own behavior supplies the ladder.

The two obvious readings both fail:

- **Tier as magnitude only** keeps the lock honest but kills fusing. Once you hold Chill I you have the key, and the Vault's central mechanic becomes optional — bad for a screen that now carries the entire hard-counter guardrail.
- **Tier as gate** makes fusing matter but lets a recessive drop hand a player a key that doesn't turn. Chill II splices down to Chill I and Coursers silently stop being stoppable. It also degrades the teaching copy from "only answer to Courser" to "only answer to Courser, at tier 2 or above."

Coverage-scaling keeps both halves. The lock stays binary and the copy stays true, while fusing matters because coverage matters. Later waves pressure the player with *more*, never with resistance.

**The design already assumes this.** Alliance Rally's outcome check is `hasPierce` — a boolean presence test that gates the outcome note independently of whether raw power wins. Not a tier threshold. The screen carrying the design thesis already treats any Pierce as breaking the wall, and tier-gating would require rewriting it.

Recessive inheritance becomes safe as a result: a downtier is a real setback that costs throughput without ever locking anyone out. That is the "texture and stakes without the frustration" the genetics spec wanted from recessives and doubted it could get.

### 1.5 — Samples vs traits → **access is bred, coverage is fused** `[X]`

A sample is not a trait you don't have yet. It is an upgrade to a trait you already carry.

- **Access** — whether a creature can answer a raider at all — comes only from breeding. Inherited from a parent, or produced by mutation. Nothing else grants it.
- **Coverage** — how much that answer covers, per 1.4 — comes from samples. Fuse three tier-I into a tier-II and apply it to a creature that already carries the trait.

**This exists because the store would otherwise break constraint 1 by accident.** Breeder Bundle includes a rare trait pull, Lab Expansion three, Geneticist's Vault eight, and Splice Roulette is a sample gacha with a discounted spins pack. If samples converted into traits a player could place on a creature, $20 would buy Reach and counter traits would be purchasable — constraint 1 broken by the store page rather than by any deliberate design call. It would also weaken the splice loop, since breeding would no longer be the only route to a trait.

The split keeps every existing pack unchanged. Purchases buy coverage, which is rate rather than reach — the line the monetization spec draws. Fusing still matters, Vault capacity still bites, and the Roulette still turns a near-miss into progress.

**Naming.** "Trait fragment" misleads under this model, since a fragment cannot produce a trait. *Gene sample* or *tier fragment* carries it better. Cheap to change now, annoying after the copy is written.

### 1.6 — Apex → **a trait class, not a tier. Renamed Aberrant.** `[X]`

The specs' four tiers cannot map onto the designs' three, because the fourth was never a tier doing tier work.

Under 1.5 mutation grants access and samples grant coverage. If Apex were the top coverage tier reachable by mutation only, mutation would be handing out coverage — crossing the line 1.5 draws — and the fusing ladder would break either way: fuse three tier-IIIs into an Apex and mutation-only dies, or stop the ladder at III with a tier above it fusing can never reach.

Resolution:

- **Twelve species traits** scale I → II → III through fusing. These absorb the specs' Common, Refined and Rare.
- **Aberrant traits** belong to no species and enter only through mutation. Off the ladder entirely — you carry one or you don't.

**The monetization guardrail gets stronger.** Money cannot buy an Aberrant trait because money cannot buy access at all, per 1.5. Structural rather than a rule someone must remember when writing pack contents.

**It gives Apex Veins a job nothing else has.** Under 1.5 any species trait can be bred toward deliberately, so the map economy is about coverage and throughput. Aberrant traits are the one thing that cannot be planned for.

Aberrants have no tiers, so there are no Aberrant samples — an earlier draft of this entry said otherwise and contradicted its own ruling. Apex Veins instead yield high-tier species samples plus a **catalyst** that raises mutation chance on the next splice. The Vein remains the only route to an Aberrant while granting the chance rather than the trait, which is what keeps it unbuyable: a purchasable catalyst would buy attempts, which the monetization spec already permits.

**Renamed because "Apex" was carrying four meanings** — Apex Vein, Apex Cup, Apex form (growth stage 4), and now the trait class. The art direction brief already hit the first collision and worked around it by speccing trait-Apex as iridescent-white so two gold "apex" signals wouldn't read as one. Renaming is cheaper than the workaround. *Aberrant* also reads as something the lab didn't intend, which is the register the brief wanted.

### 1.7 — Instinct tiering → **untiered** `[X]`

Coverage scaling needs a magnitude that can grow. Behavior has none. Bloodscent targets lowest-HP or it doesn't, and a tier-III Bloodscent would have to mean something invented — stronger turns Instinct into a power stat and breaks the reason the third slot was safe in 1.2; more frequent is a hidden number, breaking the combat spec's rule that every Instinct is inferable from watching one wave.

So: six behaviors, carried or not. No Instinct samples, no Instinct fusing. The Vault holds twelve species traits and nothing else — simpler to build and simpler to explain.

**Aberrant Instincts should exist, and are the strongest chase item available.** A creature that acts in a way no bred lineage can produce is more distinctive than one covering slightly more sky, and it is visible in play rather than in a stat readout — legible in a replay, which is what makes it screenshot-worthy. That is what the specs wanted from Apex rolls while relying on rarity alone to deliver it.

### 1.8 — Derived constraints

Consequences of the above. These are not open.

1. **No purchase may grant trait access.** Packs, pulls and Roulette spins grant coverage only, per 1.5. This is the sole thing preventing hard counters from becoming a paywall, and it is enforced by the sample model rather than by store discipline.
2. **Species acquisition must guarantee all eight counters.** Six species carry twelve traits, two each — guarantee that a player acquires all six and every counter follows. The genetics spec already has the mechanism: campaign milestones grant specific base stock so no player is locked out by bad luck. This replaces the sample-drop guarantee stated in v3, which cannot work now that samples don't grant access. It is also more legible — "I need a Hollow line" is a goal a player can hold, and the Vault already frames it that way: *no Hollow line has passed it down.*
3. **Roster cap must never bind below eight counter answers plus working depth.** Under hard counters a cap that forces a player to drop a key is a soft lockout. See 3.6.
4. **Nothing may make a counter carrier unavailable when a wave demands it.** Directly constrains 2.4, and the Vault overflow rule in 2.2.
5. **The creature card must show which counter traits a creature carries, readable at roster scale.** Two combat traits makes this tractable. See 2.5.
6. **Waves escalate by volume, never by counter resistance.** Per 1.4, no raider may ever be immune to a tier-I answer. Difficulty comes from more raiders, more lanes, and tighter simultaneity — never from a lock that stops turning.
7. **Retirement yields samples and pedigree, never traits.** The Archive tab currently says retiring a creature "banks the traits." If banked traits can be re-applied to another creature, that is an access path around 1.5. Retirement returns coverage and preserves the lineage record; the trait itself dies with the animal, which is the point the whole system is making.
8. **No Aberrant trait may ever counter a raider.** Per 1.6 they are unbuyable and unplannable, which makes them pure upside — and would make them a hard wall if any were required. The specs already have the category: Carapace, Litter, Regrow and Screen counter nothing by design. Aberrants join that family. One counter-carrying Aberrant collapses the supply guarantee at constraint 2.
9. **Generation gates the coverage ceiling; samples fill toward it.** Per 2.2. Neither alone reaches tier III — a deep line with no samples and a sample stockpile on a G2 creature both fall short. This is what keeps the Splicing Chamber meaningful and stops sample purchases from short-cutting lineage.
10. **The eight counters live only among the twelve species traits.** The counter pool is closed at launch and stays closed. New species in later seasons may add traits, but a new raider must be answerable by an existing counter or ship with its answer already in general supply.

---

## Part 2 — Re-run against Part 1

*All settled. 2.2 carried the guardrail; the rest followed from Part 1 with one new requirement each.*

### 2.1 — Lineage View → **build it, with a species composition strip** `[S]`

Specs win on the screen existing. Part 1 changed what it is for.

Under 1.5 access comes only from breeding, and per constraint 2 the species carry the counters. So the tree is not only a record of what a player made — it is the record of **what they can make**. Wanting Reach means needing a Hollow line, and the tree is the only place showing whether one exists in the ancestry.

**New requirement: a species composition summary above the tree.** "This line carries Vetch, Hollow, Ember" answers the planning question faster than reading eight ancestor nodes. The tree is the trophy; the strip is the tool. Neither document specifies this because neither had a model where species meant access.

Also needed, per the specs: trait contributions per ancestor, mutation markers showing where an Aberrant entered, and Founder roots.

**Depth: five generations.** The designs display G7, so the view shows a five-generation window of a longer count — which is what the specs meant. Closes genetics spec open question 3.

The four-node strip already on Splice Chamber is most of the visual language. The FTUE closes session one on this screen and it does not currently exist.

---

### 2.2 — Gene Vault → **split into two screens, six facilities remapped** `[X]`

The design's Gene Vault (sample inventory, fusing, archive) and the spec's Gene Vault (five-module infrastructure, Core cap, campaign gate) share a name and nothing else. The spec's version is really the design's **Gene Lab** screen — isometric base-building, six upgradeable facilities, one confusingly named Gene Vault.

**Two screens, renamed.** Gene Lab holds the facilities. The design's Gene Vault screen becomes the **Sample Store**, holding inventory, fusing and the archive.

**Facility mapping, 6-for-6.** Two designed facilities shouldn't be facilities. Trait Archive is a reference function, not a building — and note it is *not* the same thing as the Archive tab on the Vault screen, which holds retired creatures. Alliance Hall duplicates alliance tech, which the alliance spec funds from a shared treasury rather than a personal building. Cutting both frees room for the two modules the specs need and the designs never had.

**The Trait Codex survives as a screen.** Cutting the facility does not close the gap the screen inventory flagged: players must be able to read what Burrow does and what each Instinct's behavior is. Per 3.7 it is now required rather than optional, since eight counters have to be learnable somewhere.

| Facility | Governs | Origin |
|---|---|---|
| **Splicing Chamber** | Generation ceiling | Both |
| **Hatchery** | Roster capacity | Design name, spec Habitat |
| **Gene Vault** | Sample capacity | Design |
| **Harvest Array** | Yield rate, contested presence weight | Spec, new to designs |
| **Drive** | Relocation speed, transit exposure | Spec, new to designs |
| **Core** | Ark integrity **in PvE region defense only**, caps all other modules | Spec, replaces Defense Wall |

Same count as designed. One new piece of facility art, since Defense Wall's slot becomes Core.

**Generation needed a job restoring.** The genetics spec had generation gating trait quality ceiling, but under 1.4 trait quality is coverage and coverage comes from fusing — leaving generation governing nothing and the Splicing Chamber capping an empty variable.

Resolution: **generation gates the coverage ceiling, samples fill toward it.** A G3 creature holds tier II at most; tier III requires a deeper line. That restores the Chamber's purpose, makes a G7 line mechanically meaningful rather than a bragging number, and gives the Lineage View a second reason to exist. It also keeps the spec's original intent — deep lineages reach the top of the curve — expressed in the new vocabulary.

Core's integrity stat applies to region defense and nothing else. Raids never target the Ark and the alliance spec forbids base attacks outright, so without the qualifier this becomes a PvP stat by accident.

**The campaign milestone gate on Core carries across unchanged.** It is the anti-whale guardrail — spending accelerates shard and timer costs and then stops at the same campaign wall as everyone else. Selling around it defeats the structure.

**Reversal on the overflow rule.** v2 called the 88% discard rule dangerous because it could strand a player without a counter key. Under 1.5 that is false — samples grant coverage, breeding grants access, so a discard costs recoverable progress rather than access. The v2 exemption is withdrawn and the capacity valve ships as designed. Derived constraint 4 is not in play here.

**Naming.** Per 1.5, "fragment" misleads when it cannot produce a trait. **Sample** is the settled term — clinical, fits the art direction register, and avoids further overloading "Gene," which already carries Ark, Shards, Vault, Lab and Geneticist.

**What the Sample Store holds.** Twelve species traits only. No Instinct samples, per 1.7. No Aberrant samples, per 1.6.

**Residual naming collision.** The Gene Vault *facility* governs the capacity of the Sample Store *screen*. Coherent — the building sets the limit, the screen shows the contents — but two things a player reads as related now carry unrelated names. Worth a second look when the screen copy is written.

**Still to spec.** Sample drop rates, fusing costs, capacity curve, and catalyst frequency. This is now its own document rather than a reconciliation item.

---

### 2.3 — Combat terrain → **designs, with lane count as a difficulty dial** `[D]`

Keep pockets-beside-the-lane placement. The legibility argument wins on a phone, and under hard counters composition is deciding outcomes anyway — terrain modifiers would add noise to a decision already made at the splice screen.

Let the region set lane count, 1–3. Emphasis shifts: this is no longer mainly about giving relocation a second axis. **More lanes means more raiders arriving at once, which means more counter traits held simultaneously.** Lane count becomes the cleanest difficulty dial in the game and ties directly to the combat spec's rule that region defense scales with richness.

Richer ground demands a broader roster. That is the pressure curve the node spec wants, expressed through combat instead of yield.

**Derived cap: four raider types per wave.** Five deployed creatures at two combat traits each is ten trait slots, but no player reliably holds ten distinct counters in one deployment. With the wave rule forbidding two raiders answered by the same trait, four types is the ceiling the sample economy can supply. Beyond it, waves demand roster breadth that constraint 2 does not guarantee.

Drops: terrain modifiers, emplacement variety. Region Detail needs a lane-count preview.

---

### 2.4 — Growth stages → **cosmetic. Feeding cut.** `[X]`

Roster cards show `Growing · 2h 40m`. Under hard counters, if the only Chill carrier is mid-growth when a Courser wave lands, the player loses a fight they had the answer to — constraint 4 violated by a timer, and the kind of loss players correctly read as unfair.

Ship the visual: four stages, one rig, mass moved outward, same profile. The design's rule is good and the payoff is real. Ship it with the creature combat-ready from reveal. **Growth changes how a creature looks, never whether it fights.**

**Feeding is cut.** If growth grants nothing mechanical, the feeding mechanic is a sink with no purpose. It could survive as a pure cosmetic accelerator — a shard sink that buys appearance and no power, which is clean — but it is not worth the build at launch. Cut it and let growth advance on age.

---

### 2.5 — Art register → **designs, with three carried requirements** `[D]`

The design direction is executed, tokenized and complete. The spec direction is written and undrawn. That settles it.

Three things carry over, all functional rather than aesthetic:

**Tabular figures.** The UI is dense with odds, timers, yields and countdowns. Numbers must not jitter as they tick, and 1/l/I and 0/O must be unambiguous. Check Baloo 2's numerals; if they fail, keep it for titles and CTAs and pair a tabular face for numerals only.

**Two combat traits visible on the body.** Under hard counters a player scanning the roster must see at a glance who carries Chill and who carries Reach — that read decides waves. The design's own rule serves this well: a hybrid keeps one parent's body and bolts on the other's signature part, never averaging them. Two traits maps cleanly onto that; four would not have.

**Instinct needs a non-body channel.** Newly urgent. The art direction brief's open question 2 asked how much of Instinct is visible; per 1.3 and 1.7 it is now a full slot holding the Aberrant chase items, and behavior cannot be drawn the way a spine or plate can. Options are an eye treatment, a posture, or a card badge. Aberrants take the iridescent-white treatment already specced, which stays distinct from the map's gold Apex Vein pulse.

**The rarity pip system needs remapping.** The art direction brief specced four tiers at one to four pips, with Apex animated. Under 1.6 there are three coverage tiers plus a class outside them. Three pips for tiers I–III; Aberrant becomes a distinct marker rather than a fourth pip, since it is not further along the same ladder. The brief's rule that colour never carries information alone still holds.

The anti-Tyranid checklist stays on file for final creature art. The geometric placeholders are nowhere near that territory but rendered art can drift.

---

## Part 3 — Settled

Four changed under Part 1. Three were already clean.

### 3.1 — Mutation rate → **9%, with an Aberrant sub-roll** `[X]`

Mutation now does two jobs: per 1.5 it is a route to a species trait neither parent had, and per 1.6 it is the *only* route to an Aberrant. A single rate covering both makes Aberrants either too common or species access too slow.

- **Base mutation ~9%**, per the designs. The spec's 3% predates mutation carrying this much load, and its own open question already doubted 3% sustained the "any splice could be the one" feeling.
- **An Aberrant sub-roll inside it.** Most mutations produce a species trait outside the parents' pool; a small fraction produce an Aberrant.
- **The Apex Vein catalyst raises the sub-roll, not the base rate.** This is what makes the Vein specifically the Aberrant route rather than a generic splice booster, and it keeps 1.6's unbuyable guarantee meaningful.

Both numbers tune at soft launch. The structure is the decision; the values are not.

### 3.2 — Affinity → **designs' number, specs' cap, inheritance only** `[X]`

Affinity boosts **inheritance probability and nothing else.** Under 1.5 that means it makes access more reliable, which is supply — safe, and player-friendly. It must never boost coverage, which would make it a power axis and reintroduce the runaway-meta failure the genetics spec named.

Take the design's +12% since it buys reliability rather than strength. Keep the spec's hard cap of one affinity per creature.

### 3.6 — Roster cap → **20 as the floor, scaling with Hatchery** `[X]`

Inverts the earlier reading. Twenty is not the problem — it is a reasonable minimum. Holding eight counters takes four creatures at best distribution, and a player also needs five deployed, plus garrison and escort commitments.

So the design's 20 becomes the floor rather than the ceiling, scaling upward with Hatchery tier per 2.2. Satisfies constraint 3 without discarding a tested number.

### 3.7 — Screen count → **rebuild the inventory; little actually died** `[X]`

The framing in earlier versions was wrong. Reconciliation killed almost no specced screens — it split one (Gene Vault into Gene Lab and Sample Store) and removed some content from others (armour type displays on Creature Detail, Field trait UI, feeding UI per 2.4).

The real delta is coverage. The designed 20 handles Tiers 1–3 well. Missing:

| Missing screen | Source | Note |
|---|---|---|
| **Lineage View** | 2.1 | FTUE closes session one on it |
| **Trait Codex** | 2.2, constraint 10 | Eight counters must be learnable |
| **Replay Viewer** | Combat spec §9 | Argued as launch-critical — it is what makes a lost raid tolerable rather than infuriating |
| **Mail / notifications** | Screen inventory §4 | Raid alerts, alliance notices, rewards |
| **Player profile** | Screen inventory §4 | Alliance applications, leaderboard |
| **Settings / account / support** | Screen inventory §4 | Required for store submission |
| Route Plotter, Convoy Status, Raid Party Select, Marks Shop | Raiding spec | Tier 4, not yet designed |

Full rebuild is its own pass, not a reconciliation item.

### Settled without change

| # | Conflict | Outcome |
|---|---|---|
| 3.3 | VIP ladder name | **Geneticist Tier.** The design's own tier 12 already awards that title. |
| 3.4 | Store tab | **No Store tab**, contextual entry. Revisit only if conversion suffers. |
| 3.5 | Recessive traits | **Adopt.** Per 1.4 a downtier costs coverage, never access, and is recoverable by re-fusing. The frustration the spec feared is designed out. |

---

## Part 4 — Change impact

Everything is settled, so this is no longer a map of what unblocks what. It is a map of **what falls over if you reopen something.** Read it before revisiting any decision.

**1.1 — hard counters**

The root. Reopening it invalidates most of the register: 1.2's slot rationale, 1.4's coverage semantics, constraints 1–4 and 6–10, 2.2's guardrail priority, 2.3's lane dial, 2.4's growth ruling, 2.5's visibility requirement, 3.6's roster floor and 3.7's Codex requirement.

If the damage triangle comes back, restart the register.

**1.2 — six species, three slots**

Reopening changes 1.3 (the Instinct slot's existence), 1.7 (untiered only holds because the slot counters nothing), 2.5 (four visible traits was unaffordable, two is not), the splice resolution model, and the arithmetic under constraints 2 and 3 — twelve traits, eight counters, roster floor of 20 all derive from six species carrying two each.

**1.4 — tier is coverage**

Reopening changes 1.5 (the split is built on it), 1.6 (Apex could then be a tier again), constraint 9 (generation's ceiling), 3.5 (recessives stop being safe), and 2.2's overflow reversal.

**1.5 — access bred, coverage fused**

Reopening changes 1.6, constraints 1, 2, 7 and 8, correction 0.3, and 2.2's overflow reversal. This is the decision the store depends on — if it moves, every pack description needs re-auditing.

**1.6 — Aberrant as a class**

Reopening changes constraint 8, 3.1's sub-roll structure, 2.5's pip remap, and the Apex Vein yield in the node spec.

**1.7 — Instinct untiered**

Reopening changes 2.2 (Sample Store contents), 2.5 (the Instinct visual channel), and open item 1.

**Smaller reaches**

- **2.3 lane count** → the four-raider-type cap
- **3.1 mutation rate** → Aberrant availability and catalyst value
- **2.2 facility mapping** → constraint 9's generation ceiling, roster cap scaling

---

## Part 5 — Open items

1. **Where the excitement axis sits.** Per 1.7 the Instinct slot now holds the untiered behaviors and the Aberrant chase items, while the two combat slots hold the counter grind. That is probably right — it puts the chase on the axis that cannot threaten the counter guarantee — but it gives Instinct more weight than the "safe because it counters nothing" framing in 1.2 assumed. Watch in playtest rather than design around now.

2. ~~**Rich Deposit depletion.**~~ **Resolved** at six days, syncing with the weekly rotation — see bible §5.3.
3. **Economy untuned on both sides.** Charge regen, sample drop rates, Vault capacity, yield curves.
4. ~~**Alliance cap.**~~ **Resolved** — held at 40. The debate is misplaced: an alliance of 40 with 12 actives behaves like one of 12, so activity rather than capacity is the binding constraint. Design effort moves to the decay rules. See bible §6.2.
5. **Convoy interception grace period.** Specs say day 14; designs flag it unresolved. Specs by default.
6. **Season length.** 6 or 8 weeks, designs only.
7. **`support.js` is not to be ported.** The prototypes' runtime is scaffolding; the target stack needs its own binding layer.

---

## Next steps

Reconciliation is done. Everything below is new work.

1. ~~Fix 0.1 and 0.2~~ — **done.** See `broodline_splice_confirm_spec.md` and the global rename table in `broodline_supersession_map.md`.
2. **Write the sample economy spec** — drop rates, fusing costs, capacity curve, base mutation rate, Aberrant sub-roll, catalyst frequency. Constraint 2 cannot be audited until it exists, which makes this the critical path.
3. **Audit** species acquisition against constraint 2 and sample drops against constraint 3.
4. **Pass over wave tables** against constraint 6 and the four-type cap at 2.3.
5. **Rebuild the screen inventory**, adding the six missing screens at 3.7.
6. **Update the nine source specs.** `broodline_supersession_map.md` maps every affected section. Four specs — genetics, combat, Gene Vault, art direction — carry enough divergence to warrant rewriting rather than annotating.

---

## Revision history

**Since v11**

- **Part 4 rewritten** as a change-impact map. The dependency version was stale — written mid-process, missing 1.4–1.7, and every node reading SETTLED.
- **Splice spec made standalone-readable** — it carried numbered references with no pointer to where they resolve.

**Since v10**

- **Splice resolution model added to 1.2** — never reconciled before. One combat trait locked, one rolled, Instinct rolled.
- **0.3 added** — four store packs promise trait pulls, breaking constraint 1.
- **Rich Deposit depletion** added to open items.

**Since v8**

- **Part 3 closed.** 3.1 splits mutation into a base rate and an Aberrant sub-roll; 3.2 restricts affinity to inheritance; 3.6 inverts — 20 becomes a floor, not a ceiling; 3.7 reframed, since reconciliation killed almost nothing and the real gap is six undesigned screens including the Replay Viewer.

**Since v7**

- **Check pass on Part 2.** Generation had lost its mechanical job under 1.4 and gets it back as the coverage ceiling — new constraint 9. Trait Codex un-conflated from the Vault's Archive tab. Core qualified to PvE. Rarity pips remapped from four tiers to three plus a class. Duplicate line removed from 2.3.

**Since v6**

- **Part 2 closed.** 2.1 gains a species composition strip, 2.3 gains a raider-type cap, 2.4 cuts feeding, 2.5 gains an Instinct visibility requirement.

*Section numbers below reflect the current numbering, not what they were called at the time.*

**Since v5**

- **2.2 settled.** Facility mapping, fragment economy, and Apex Vein yield.
- **Reversal:** the Vault overflow rule is no longer dangerous. Under 1.5 it costs coverage, not access, so the v2 exemption is withdrawn.
- **1.6 corrected.** It referred to Aberrant fragments, which cannot exist under its own ruling. Apex Veins yield a mutation catalyst instead.
- **Naming settled:** fragments become **samples**.

**Since v4**

- **Apex is no longer a tier.** Recorded as 1.6 — it becomes a trait class outside the coverage ladder, renamed **Aberrant**.
- **Instinct is untiered.** Recorded as 1.7. No Instinct fragments, no fusing.
- **Two constraints added** covering Aberrant traits and the counter pool. Constraints section renumbered to 1.8 so it reads after the decisions.
- **Part 5.1 closes.** New open item on where the excitement axis sits.

**Since v3**

- **Fragment-to-trait relationship settled** as an access/coverage split. Recorded as 1.5.
- **Derived constraints renumbered.** Constraint 2 was wrong and is rewritten — the supply guarantee moves from fragment drops to species acquisition.
- **Two constraints added** covering purchase paths and retirement.
- **New open item at 5.1:** the trait tier mapping, four tiers in the specs against three in the designs.

**Since v2**

- **Tier semantics settled** as coverage-scaling. Recorded as 1.4. This closes the v2 open item that gated the fragment economy.
- **The constraints section gains a sixth entry** covering wave escalation.
- **3.5 recessive inheritance is now safe to adopt** — the reason it was risky has been designed out.
- **New open item at 5.1:** the fragment-to-trait relationship, which the tier decision exposed.

**Since v1**

- **Part 1 is decided.** Recorded below with rationale.
- **The v1 ordering was wrong.** Counter model had to be settled before slot count, because under hard counters the number of combat traits per creature is an economic lever, not a depth dial. Corrected.
- **I withdrew the soft damage floor** I recommended in v1 for 1.2. Reasoning in the entry.
- **A derived-constraints section is new** (§1.8 as of v5) — constraints that became non-negotiable as a consequence of the Part 1 calls, including one that contradicts a shipped design behavior.
- **Part 2 re-run.** 2.2 changes substantially, 2.4 hardens, 2.5 gains a requirement. 2.1 and 2.3 shift in emphasis.
- **One v1 recommendation reverses:** the Gene Vault "unanswerable" copy. See 2.2.
