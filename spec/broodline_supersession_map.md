# Broodline — Supersession Map

*Read this before opening any of the nine source specs.*

> **The design bible is now complete (§1–10) and replaces all nine specs.** This map remains useful only for tracing where a particular idea came from, or for auditing whether something in an original spec was carried across deliberately. For current truth, read `broodline_bible.md`.

---

## Why this exists

The nine specs are still the best description of most of the game. But nineteen decisions in the reconciliation have moved parts of it, and someone reading a spec cold will build toward things that no longer exist — a damage triangle, four trait slots, ten chassis, an Apex tier.

This maps every affected section to the decision that replaced it and states the new truth in one line. Where a spec is unaffected, it says so, because knowing what is *still* trustworthy matters as much.

**Status key:** ✅ current · ⚠️ partly superseded · ❌ substantially superseded

---

## Global renames

Apply everywhere, in all specs and all screen copy.

| Old | New | Source |
|---|---|---|
| Splice *(project name)* | Broodline | Established |
| Breeder Tier / Breeder XP / Breeder Pass | Geneticist Tier / Geneticist XP / Season Pass | 3.3 |
| Chassis | Species | 1.2 |
| Founder *(as base creature type)* | Base species | 0.2 |
| Founder *(player's first five)* | **unchanged** — keeps the word | 0.2 |
| Trait fragment | Sample | 1.5, 2.2 |
| Apex *(trait tier)* | Aberrant *(trait class)* | 1.6 |
| Habitat *(module)* | Hatchery *(facility)* | 2.2 |
| Lab tier | Harvest Array tier | 2.2 |

"Apex" still correctly names Apex Vein, Apex Cup and Apex form. Only the trait sense is renamed.

---

## ❌ broodline_genetics_system.md

Most affected. Roughly half is superseded.

| Section | Status | Replaced by |
|---|---|---|
| §2 Creature Anatomy — 10 chassis | ❌ | **1.2** — six species |
| §2 — four trait slots | ❌ | **1.2** — three slots: two combat, one Instinct |
| §2 — generation gates trait quality ceiling | ⚠️ | **Constraint 9** — generation gates the *coverage* ceiling; samples fill toward it |
| §3 Trait Categories — Frame/Armament/Field/Instinct | ❌ | **1.2** — Field is cut |
| §3 — tiers Common/Refined/Rare/Apex | ❌ | **1.4, 1.6** — coverage tiers I–III, plus Aberrant as a class outside the ladder |
| §4 The Splice — two guaranteed, two rolled | ❌ | **1.2** — one combat trait locked, one combat slot rolled, Instinct rolled. Separate pools. |
| §4 — show odds before the charge | ✅ | Reinforced |
| §4 — no failure state | ✅ | Reinforced |
| §5 Mutation — ~3% | ❌ | **3.1** — ~9% base with an Aberrant sub-roll inside it |
| §5 — mutation is the only entry for Apex | ✅ | Now stronger: the only entry for Aberrants, and money cannot buy access at all |
| §6 The Broodline — lineage, Founders, consumption | ✅ | Reinforced by 2.1 |
| §6 — Affinity +5%, capped at one | ⚠️ | **3.2** — +12%, still capped at one, **inheritance probability only, never coverage** |
| §7 Base Stock — roster cap scales with Vault tier | ⚠️ | **3.6** — floor of 20, scaling with Hatchery |
| §8 Splice Screen Requirements | ⚠️ | Superseded by `broodline_splice_confirm_spec.md` |
| §9 Guardrails | ⚠️ | Superseded by constraints 1–10 |
| §11 open questions 2 (recessive) and 3 (depth) | ✅ | **Closed** — 3.5 adopts recessive; 2.1 sets five generations |

---

## ❌ broodline_combat_system.md

| Section | Status | Replaced by |
|---|---|---|
| §1 Core Format — placement over reflexes | ✅ | Reinforced |
| §2 Battlefield — lanes 1–4, emplacement tiles, terrain modifiers | ❌ | **2.3** — 1–3 lanes, pockets beside the lane, no terrain modifiers |
| §3 Deployment — five creatures, Rally | ✅ | |
| §4 Damage & Armor — the triangle | ❌ | **1.1** — cut entirely; eight raiders, each with one answering trait, no damage floor |
| §5 Trait → Stat Mapping — four slots incl. Field | ❌ | **1.2** — three slots, Field cut |
| §6 Instinct — 12–16 behavior trees | ❌ | **1.3, 1.7** — six behaviors, untiered, inherited independently of the body |
| §7 Wave Structure | ⚠️ | **2.3, constraint 6** — max four raider types per wave; escalation by volume only, never by resistance |
| §8 Raid Combat | ✅ | |
| §9 Auto-Resolve & replay | ✅ | Replay Viewer is undesigned — see 3.7 |
| §10 Loss & Regeneration — never permanently lost | ✅ | Reinforced by constraint 4 |

---

## ❌ broodline_gene_vault.md

| Section | Status | Replaced by |
|---|---|---|
| §1 Core Concept — Vault vs Geneticist Tier split | ✅ | Still the right distinction |
| §2 Five Modules | ❌ | **2.2** — six facilities on the Gene Lab screen: Splicing Chamber, Hatchery, Gene Vault, Harvest Array, Drive, Core |
| §3 Core as the Spine — 12 tiers, campaign gate | ✅ | The campaign milestone gate is the anti-whale guardrail and carries unchanged |
| §3 — Core's Ark integrity | ⚠️ | **2.2** — PvE region defense **only**; never a PvP stat |
| §4 Splice Chamber & generation ceiling | ✅ | Reinforced by constraint 9 |
| §5 Cost & Timer Philosophy | ✅ | |
| §6 What the Vault Must Never Do | ✅ | Reinforced by constraints 1–3 |
| §7 Screen Requirements | ⚠️ | Now two screens — Gene Lab (facilities) and Sample Store (inventory, fusing, archive) |

---

## ⚠️ broodline_art_direction.md

| Section | Status | Replaced by |
|---|---|---|
| §1 The Tension — sterile lab vs organic creature | ❌ | **2.5** — the designs' executed direction wins |
| §2 Palette — charcoal, bone, amber accent | ❌ | **2.5** — design tokens in the handoff README |
| §3 Creature Design Rules — silhouette first | ✅ | Reinforced; matches the Character Bible |
| §3 — traits visible on the body | ⚠️ | **2.5** — two combat traits on the body; Instinct needs a non-body channel |
| §4 Anti-Tyranid Checklist | ✅ | Keep on file for final creature art |
| §5 Typography | ⚠️ | **2.5** — Baloo 2 + Nunito, but the tabular-figures requirement carries over and must be verified |
| §6 Rarity Language — four tiers, one to four pips | ❌ | **2.5** — three pips for tiers I–III; Aberrant is a distinct marker, not a fourth pip |
| §7 Modular Art — chassis plus four layers | ❌ | **1.2** — two visible trait layers, which makes the pipeline substantially cheaper |
| §9 open question 2 (Instinct visibility) | ⚠️ | **Now urgent** — Instinct is a full slot holding the Aberrant chase items |

---

## ⚠️ splice_monetization_spec.md

Structurally sound. The risk is in the wording of what packs contain.

| Section | Status | Replaced by |
|---|---|---|
| §1 Core Currencies | ⚠️ | Rename per the global table |
| §2 Splice Charges | ✅ | |
| §3 Breeder Tier ladder | ⚠️ | Rename to Geneticist Tier; structure unchanged |
| §4 Store — "rare trait pulls" in four packs | ❌ | **Constraint 1** — no purchase may grant trait access. Pulls grant **samples**, which are coverage. Reword every pack. |
| §4 Breeder Pass — "guaranteed rare-trait creature" | ⚠️ | Must be a species creature, never counter access |
| §5 Splice Roulette — "rare trait pull wheel" | ⚠️ | A sample wheel |
| §5 Mutation Surge — "new trait type available only during event" | ⚠️ | **3.1** — raises the Aberrant sub-roll; grants no trait directly |
| §6 Trial / Hook Mechanics | ✅ | |
| §7 Guardrails | ⚠️ | Now stronger — see constraints 1–3 |

**This is the highest-risk file after the splice screen.** Four pack descriptions currently promise trait pulls. Shipped as written, they break the guardrail holding up the entire hard-counter decision. Recorded as reconciliation item **0.3** — a correction, not a decision.

---

## ⚠️ splice_resource_node_system.md

| Section | Status | Replaced by |
|---|---|---|
| §2 Node tiers and yields | ✅ | Matches the designs |
| §2 Apex Vein — "chance of rare trait fragments" | ⚠️ | **1.6** — high-tier species samples plus a **catalyst** raising the Aberrant sub-roll |
| §2 Rich Deposit depletes in 5–7 days | ⚠️ | Designs say ~4 days. Unreconciled; pick one. |
| §3 Spawn & Rotation | ✅ | |
| §4 Harvesting — scales with "Lab tier" | ⚠️ | **2.2** — Harvest Array facility |
| §5 Competition & Contested Nodes | ✅ | |
| §6 Decay & "Why Move" | ✅ | |
| §7 Map Visualization | ✅ | Matches the designed World Map |
| §8 Scout reports | ✅ | Matches the designed Apex Alert |

---

## ✅ broodline_alliance_territory.md

Almost entirely intact — the meta layer was never in conflict.

| Section | Status | Note |
|---|---|---|
| §1–6 Structure, Stakes, Assault, Garrisons, Control | ✅ | |
| §7 Alliance Tech | ⚠️ | Alliance Hall is cut as a personal facility per 2.2; alliance tech remains treasury-funded and unchanged |
| §8 Anti-Monopoly Guardrails | ✅ | Still load-bearing |
| §9–11 Decay, Solo Players, Monetization | ✅ | |
| §12 open question 1 (alliance cap) | ⚠️ | Still open. Specs suggest ~100, designs suggest 25, both say 40 is probably wrong — and they disagree on direction. |

---

## ⚠️ broodline_screen_inventory.md

| Section | Status | Replaced by |
|---|---|---|
| §2 Navigation — five tabs including Store | ⚠️ | **3.4** — Map · Ark · Splice · Lab · Allies, no Store tab |
| §3 Screen Inventory — 34 screens | ⚠️ | **3.7** — rebuild. Gene Vault splits into two; armour displays and Field trait UI are removed. |
| §4 Gaps — Gene Vault undefined | ✅ | **Closed** by 2.2 |
| §4 Gaps — Trait Codex, Mail, Profile, FTUE, Settings | ⚠️ | Still open, plus the Replay Viewer — see 3.7 |
| §5 Design Priority | ✅ | Lineage View in Tier 2 is more correct now than when written |
| §6 Shared Components | ⚠️ | Creature card now carries two trait pips, not four |

---

## ✅ broodline_ftue.md

| Section | Status | Note |
|---|---|---|
| §1 Core Principle, the ad-match advantage | ✅ | |
| §2 Session One beats 1–4, 6–8 | ✅ | |
| §2 beat 5 — "one new enemy archetype with a visible armour type" | ⚠️ | **1.1** — teaches a counter trait, not an armour type |
| §2 beat 7 — scripted mutation | ⚠️ | **3.1** — at 9% base this sets a less unrealistic expectation than it did at 3% |
| §3 The Consumption Problem | ✅ | Reinforced; see `broodline_splice_confirm_spec.md` |
| §4 Founder Naming Cadence | ✅ | The word "Founder" is retained for this sense per 0.2 |
| §5 Drip Schedule | ⚠️ | Gene Vault entry becomes Gene Lab; add the Sample Store |
| §6–8 Hidden systems, monetization, metrics | ✅ | |

---

## Handling

Three options, in rough order of cost:

1. **Ship this map alongside the specs.** Cheapest. Works if the reader is disciplined about checking it first — which is a real assumption about people under deadline.
2. **Add a header block to each spec** pointing at its row here. Better. A reader cannot open a superseded section without seeing the notice.
3. **Rewrite the four ❌ specs** — genetics, combat, Gene Vault, art direction. Those four carry most of the divergence, and their superseded sections are load-bearing rather than incidental. The five ⚠️/✅ specs are fine with option 2.

Option 3 for the four, option 2 for the rest, is the proportionate answer.
