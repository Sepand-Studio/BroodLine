# Broodline — Supersession Map

*v2. Read this before opening any document that is not the bible or one of its six companions.*

> **`broodline_bible.md` is the design.** This map exists so that anyone opening one of the twenty other documents knows within one line whether they are reading current truth, a live companion, or an argument that was overtaken.

---

## Why this exists

The design was written in two waves. Nine specs came first, against an unsettled game. Nineteen reconciliation decisions then moved large parts of it, and the bible absorbed the result.

**A second wave of twelve documents was written between those two events.** They were never mapped, and half of them still describe ten chassis, a damage triangle, four trait tiers and five Vault modules. Ten of the twelve have since been brought into line or rewritten and are current; one is superseded pending rewrite; one is superseded outright.

This map states, for every file, which of those it is.

**Status key:** ✅ current · ⚙️ current companion · ⚠️ partly superseded · ❌ substantially superseded · 🔁 superseded, rewrite queued

---

## 1. The document set at a glance

| Document | Status | Notes |
|---|---|---|
| `broodline_bible.md` | ✅ | The design, §1–10 |
| `broodline_screen_inventory_v2.md` | ✅ | 45 screens against the bible |
| `broodline_combat_numbers.md` | ⚙️ | Species stats, trait values, raider profiles, wave budget |
| `broodline_sample_economy.md` | ⚙️ | Sample rates, weighting, fusing, capacity, the catalyst |
| `broodline_base_stock.md` | ⚙️ | Creature supply, species distribution, guarantees, Instinct weights |
| `broodline_campaign_structure.md` | ⚙️ | **Rewritten.** 60 waves, 8 chapters, milestones, the designed loss, replay |
| `broodline_region_roster.md` | ⚙️ | **Rewritten.** All 30 regions, 8 families, species weighting, region defence |
| `broodline_trait_codex.md` | ⚙️ | **Rewritten.** 34 entries, four schemas, the threat board, disclosure |
| `broodline_raider_roster.md` | ⚙️ | **Rewritten** from `broodline_enemy_archetypes.md`. Four bodies, art budget, the Sunder |
| `broodline_alliance_territory.md` | ⚙️ | **Brought current.** Convoy staging and the weekly tick added |
| `broodline_localization.md` | ⚙️ | Launch languages, naming policy, pricing per storefront, fonts |
| `broodline_build_order.md` | ✅ | The production plan. Not a design document |
| `broodline_waves_01_12.md` | ✅ | Authored content, chapters 1 and 2 |
| `broodline_waves_13_20.md` | ✅ | Authored content, chapter 3 |
| `broodline_waves_21_28.md` | ✅ | Authored content, chapter 4 |
| `broodline_waves_29_36.md` | ✅ | Authored content, chapter 5 |
| `broodline_waves_37_44.md` | ✅ | Authored content, chapter 6 |
| `broodline_waves_45_52.md` | ✅ | Authored content, chapter 7 |
| `broodline_waves_53_60.md` | ✅ | Authored content, chapter 8 |
| `broodline_region_graph.md` | ✅ | Authored content, region adjacency |
| `broodline_rig_proof.md` | ✅ | Production spec, the art pipeline gate |
| `broodline_combat_engine.md` | ✅ | Technical spec, the simulation |
| `broodline_splice_confirm_spec.md` | ⚙️ | Splice screen copy and states |
| `broodline_economy_model.md` | ⚙️ | Shard income, facility curve, payer ceiling |
| `broodline_collectors_raiding.md` | ⚙️ | The full raid ruleset |
| `broodline_live_ops_events.md` | ⚙️ | Event mechanics, Roulette odds |
| `broodline_monetization.md` | ⚙️ | Offer structure, never-sold list |
| `broodline_moderation_ugc.md` | ⚙️ | UGC, age gating, submission requirements |
| `broodline_midgame_arc.md` | ⚙️ | Days 14–90 |
| `broodline_gap_register.md` | ✅ | The design audit, twelve passes. Historical — every gap is closed |
| `broodline_reconciliation.md` | ✅ | Decision register — *why*, not *what* |
| `broodline_chassis_roster.md` | 🔁 | Ten chassis. Replaced by bible §1.2 |
| `broodline_spec_reconciliation.md` | ❌ | An earlier register, superseded by `broodline_reconciliation.md` |
| The nine original specs | ❌ | See §5 |

---

## 2. `broodline_spec_reconciliation.md` — superseded outright

The most dangerous file in the set, because it presents itself as authoritative: *"This document supersedes. Where it conflicts with an original spec, this is correct."* True when written, now wrong on most rows.

Its **constants ledger at §5 must not be used.** Current values:

| Ledger says | Now |
|---|---|
| Mutation rate 3% per splice | ~9%, with an Aberrant sub-roll inside it |
| Broodline Affinity +5% | +12%, inheritance probability only |
| Chassis 10 | Six species |
| Traits 56 | Twelve species traits, plus an Aberrant class |
| Enemy archetypes 12 | Eight raiders |
| Damage multipliers ×1.5 / ×0.67 | The triangle is cut. Hard counters, no damage floor |
| Campaign waves 65 | Unset pending rewrite |
| New-player raid immunity 14 days | Steps down across days 15–21 |
| Vault Core tiers 12 | **Still current** |
| Non-ally penalty −40% | **Still current** |
| Alliance stake cap 5 | **Still current** |
| Cargo loss cap 40%, raids/day 2 | **Still current** |
| Common Vein yield 40/hr, offline cap 12hr, spread ≤6× | **Still current** |
| Lineage depth 5 generations | **Still current** |
| Regions 30 | **Still current** |

Its §4 "Open Questions Now Closed" closed four questions the reconciliation later reopened and answered differently — recessive traits (now adopted), mutation rate, trait tiers, and canonical chassis colour (species now carry colours).

**What survives:** §3's flag that Collector scope needed explicit sign-off, which produced the decision now recorded in bible §5.5.

---

## 3. Global renames

Apply everywhere, in all documents and all screen copy.

| Old | New | Source |
|---|---|---|
| Splice *(project name)* | Broodline | Established |
| Breeder Tier / XP / Pass | Geneticist Tier / XP / Season Pass | 3.3 |
| Breeder's Cup | Apex Cup | 3.3 |
| Breeder Profile *(screen)* | Geneticist Profile | 3.3 |
| Chassis | Species | 1.2 |
| Founder *(as base creature type)* | Base species | 0.2 |
| Founder *(player's first five)* | **unchanged** — keeps the word | 0.2 |
| Trait fragment | Sample | 1.5, 2.2 |
| Apex *(trait tier)* | Aberrant *(trait class)* | 1.6 |
| Enemy archetype | Raider | 1.1, 4.4 |
| Gene Vault *(infrastructure)* | Gene Lab *(screen)* | 2.2 |
| Gene Vault *(inventory screen)* | Sample Store | 2.2 |
| Gene Vault | **retained** as the name of the sample-capacity facility | 2.2 |
| Vault tier / Vault Core tier | Core tier | 2.2 |
| Habitat *(module)* | Hatchery *(facility)* | 2.2 |
| Lab tier | Harvest Array tier | 2.2 |
| Defense Wall *(module)* | Core *(facility)* | 2.2 |
| Frame / Armament / Field *(trait categories)* | **cut** — twelve species traits, uncategorised | 1.2 |
| Trait Archive, Alliance Hall *(facilities)* | **cut** | 2.2 |

"Apex" still correctly names Apex Vein, Apex Cup and Apex form. Only the trait sense is renamed.

**One residual collision, unresolved.** The Gene Vault *facility* governs the capacity of the Sample Store *screen*. Coherent — the building sets the limit, the screen shows the contents — but two things a player reads as related carry unrelated names. Worth a second look when screen copy is written.

---

## 4. The document queued for rewrite

Each carries real work that survives the model change. None should be built from as written.

### 🔁 `broodline_chassis_roster.md`

| Section | Status | Replaced by |
|---|---|---|
| §2 Five roles, §3 the ten-chassis stat table | ❌ | **Bible §1.2** — six species, six roles |
| §4 Footprint belongs to chassis | ❌ | No footprint mechanic exists; placement is one creature per pocket |
| §5 Armor is not a chassis property | ❌ | Armor types are cut with the damage triangle |
| §6 Swarm chassis | ❌ | Cut. Skitter is the swarm species and occupies one slot |
| §7 Chassis can only leave a roster, never enter it through splicing | ✅ | **Rescued into `broodline_base_stock.md` §7**, where it is quantified: species diversity drains at roughly six body-exits a day and uniform wave drops alone hold it |
| §8 Silhouette requirements | ✅ | Carried into bible §10.2, applied to six species |
| §9 The sidegrade envelope | ✅ | **Rescued into `broodline_combat_numbers.md` §3.1** as a power budget of 3.40 per species |
| §10 No chassis is ever sold | ✅ | Reinforced — bible §8.6 now forbids selling a named species creature |

*`broodline_trait_codex.md` was in this list and has been rewritten. Thirty-four entries against twelve traits, three coverage tiers, six Instincts, eight Aberrants and eight raiders. The disclosure policy, the entry schema, the behaviour-preview requirement, the bottom-sheet placement, progressive surfacing and the collection layer were all rescued; the pool shape and the fixed-tier ruling were not.*

*`broodline_enemy_archetypes.md` was in this list and has been rewritten as `broodline_raider_roster.md`. Eight raiders on four shared bodies, with the art and animation budget, recognition rules, telegraphing and spawn patterns. The unbound-splice fiction, the variants-per-body budget logic, the telegraphing requirement and the raids exclusion were all rescued; the twelve-archetype roster and the armour mapping were not.*

*`broodline_campaign_structure.md` was in this list and has been rewritten. Sixty waves in eight chapters against eight raiders, with the twelve Core milestones, the species-guarantee schedule and the designed Courser loss. The mutual staircase, the replay cap and the seasonal rule were rescued verbatim.*

*`broodline_region_roster.md` was in this list and has been rewritten. Eight terrain families redefined by lane arrangement alone, all thirty regions authored with their species weighting, and region defence cadence and composition specified for the first time. The band topology, the inverse richness rule and the travel-time model had already been rescued into bible §5.2 and §5.7.*

---

## 5. The nine original specs

All superseded. Each file now carries a header pointing here.

### ❌ `broodline_genetics_system.md`

| Section | Status | Replaced by |
|---|---|---|
| §2 Ten chassis | ❌ | **1.2** — six species |
| §2 Four trait slots | ❌ | **1.2** — three: two combat, one Instinct |
| §2 Generation gates trait quality ceiling | ⚠️ | **Constraint 9** — gates the *coverage* ceiling; samples fill toward it |
| §3 Frame/Armament/Field/Instinct | ❌ | **1.2** — Field is cut, and so are the categories |
| §3 Tiers Common/Refined/Rare/Apex | ❌ | **1.4, 1.6** — coverage tiers I–III, plus Aberrant outside the ladder |
| §4 Two guaranteed, two rolled | ❌ | **1.2** — one combat trait locked, one rolled, Instinct rolled. Separate pools |
| §4 Show odds before the charge | ✅ | Reinforced |
| §4 No failure state | ✅ | Reinforced |
| §5 Mutation ~3% | ❌ | **3.1** — ~9% with an Aberrant sub-roll inside it |
| §5 Mutation is the only entry for Apex | ✅ | Stronger: the only entry for Aberrants, and money cannot buy access at all |
| §6 Lineage, Founders, consumption | ✅ | Reinforced by 2.1 |
| §6 Affinity +5%, capped at one | ⚠️ | **3.2** — +12%, still capped at one, inheritance probability only |
| §7 Base stock sources | ⚠️ | The four sources survive. "Gen 1 creatures with Refined or Rare traits pre-slotted" does not — nodes yield samples, not pre-slotted quality |
| §7 Roster cap scales with Vault tier | ⚠️ | **3.6** — floor of 20, scaling with Hatchery |
| §8 Splice screen requirements | ⚠️ | Superseded by `broodline_splice_confirm_spec.md` |
| §9 Guardrails | ⚠️ | Superseded by constraints 1–10 |
| §10 Monetization hooks | ✅ | Absorbed into bible §3.7 and §7.5 |
| §11 Q2 recessive, Q3 depth | ✅ | **Closed** — 3.5 adopts recessive; 2.1 sets five generations |
| §11 Q4 sample extraction — copy a creature rather than consume it | ✅ | **Closed: no.** It weakens the consumption economy every other system rests on. Recorded here because the bible never states the rejection |

### ❌ `broodline_combat_system.md`

| Section | Status | Replaced by |
|---|---|---|
| §1 Placement over reflexes | ✅ | Reinforced |
| §2 Lanes 1–4, emplacement tiles, terrain modifiers | ❌ | **2.3** — 1–3 lanes, pockets beside the lane, no modifiers |
| §3 Five creatures, Rally | ✅ | Rally is now one per wave with **no cooldown** — bible §4.3 |
| §4 The damage triangle | ❌ | **1.1** — cut entirely; eight raiders, one answering trait each, no damage floor |
| §5 Trait → stat mapping, four slots | ❌ | **1.2** — three slots, Field cut |
| §6 Instinct, 12–16 behaviour trees | ❌ | **1.3, 1.7** — six behaviours, untiered, inherited independently of the body |
| §7 Wave structure | ⚠️ | **2.3, constraint 6** — max four raider types per wave; escalation by volume only |
| §8 Raid combat | ✅ | Detail now in `broodline_collectors_raiding.md` |
| §9 Auto-resolve and replay | ✅ | Replay Viewer still undesigned — screen inventory v2 §4 |
| §10 Never permanently lost | ✅ | Reinforced by constraint 4 |
| §12 Q3 Rally cooldown | ✅ | **Closed: one per wave, no cooldown** |
| §12 Q4 attacker Instinct agency in raids | ⚠️ | **Still open.** Bible §4.9 keeps the full attacker loadout including Instinct and calls the drawback interesting; whether an attacker-side placement phase is needed is untested |

### ❌ `broodline_gene_vault.md`

| Section | Status | Replaced by |
|---|---|---|
| §1 Vault vs Geneticist Tier split | ✅ | Still the right distinction |
| §2 Five modules | ❌ | **2.2** — six facilities on the Gene Lab screen |
| §3 Twelve tiers, campaign gate | ✅ | The milestone gate is the anti-whale guardrail and carries unchanged |
| §3 Core's Ark integrity | ⚠️ | **2.2** — PvE region defence **only**; never a PvP stat |
| §4 Splice Chamber and the generation ceiling | ✅ | Reinforced by constraint 9 |
| §5 Cost and timer philosophy | ✅ | Values now in `broodline_economy_model.md` §5 |
| §6 What the Vault must never do | ✅ | Reinforced by constraints 1–3 |
| §7 Screen requirements | ⚠️ | Now two screens — Gene Lab and Sample Store |
| §9 Q2 Drive and raid exposure | ✅ | **Closed: Drive touches the Ark only** — bible §7.2 |
| §9 Q3 relocation cost to the Vault | ✅ | **Closed: none.** Bible §5.2 — forfeited harvest and transit time are the cost |
| §9 Q5 do modules downgrade | ✅ | **Closed: no.** Nothing in the design removes progress involuntarily |

### ❌ `broodline_art_direction.md`

| Section | Status | Replaced by |
|---|---|---|
| §1 Sterile lab vs organic creature | ❌ | **2.5** — the executed direction wins |
| §2 Charcoal, bone, amber | ❌ | **2.5** — design tokens in the handoff README |
| §3 Silhouette first | ✅ | Reinforced; matches the Character Bible |
| §3 Traits visible on the body | ⚠️ | **2.5** — two combat traits on the body; Instinct uses a badge and behaviour |
| §4 Anti-Tyranid checklist | ✅ | **Carried into bible §10.7.** Keep on file for final creature art |
| §5 Typography | ⚠️ | **2.5** — Baloo 2 + Nunito; the tabular-figures requirement carries and must be verified |
| §6 Four rarity tiers, one to four pips | ❌ | **2.5** — three pips for tiers I–III; Aberrant is a distinct marker |
| §7 Chassis plus four layers | ❌ | **1.2** — one body, two trait parts, one Instinct cue. 24 assets |
| §8 Tone reference | ✅ | **Still valid and restated nowhere.** Natural history illustration, veterinary plates, greenhouse light. No bio-horror, no grimdark, no cute mascots |
| §9 Q1 2D or 3D | ✅ | **Closed: 3D** — bible §10.3. The original reasoning was ten chassis × four layers; the answer survives the reduction because the driver was the rig, not the combination count |
| §9 Q2 Instinct visibility | ✅ | **Closed** — bible §10.4: a card badge when static, behaviour plus a visible trigger state in combat |
| §9 Q3 canonical chassis colour | ⚠️ | **Reversed.** The old answer was no; bible §1.2 now assigns a hex per species, reinforced by silhouette |
| §9 Q4 environment scope | ⚠️ | Bible §10.8 shrinks it — regions differ in look and lane arrangement, not bespoke geometry. Still uncosted |

### ⚙️ `broodline_alliance_territory.md` — now current

Almost entirely intact from the start — the meta layer was never in conflict — and now brought fully current with a vocabulary pass, convoy staging at §7.1 and the weekly tick decision.

*It has moved out of the superseded set. The table below records what changed for anyone tracking the history.*

| Section | Status | Note |
|---|---|---|
| §1–6 Structure, stakes, assault, garrisons, control | ✅ | |
| §7 Alliance tech | ✅ | Alliance Hall cut as a personal facility; tech remains treasury-funded. The Convoy branch now also unlocks Collector classes, since Drive no longer does. **§7.1 convoy staging is new** |
| §8 Anti-monopoly guardrails | ✅ | Still load-bearing |
| §9–11 Decay, solo players, monetization | ✅ | |
| §12 Q1 alliance cap | ✅ | **Closed at 40.** Activity, not capacity, is the binding constraint |
| §12 Q2 assault requires holding a Stake | ✅ | **Closed: yes** — bible §6.4 |
| §12 Q3 weekly tick timing across timezones | ✅ | **Closed: per-server, three fixed slots, set at creation, never changed**, displayed in local time |
| §12 Q4 does −40% apply to Common Veins | ✅ | **Closed: no**, now stated explicitly in bible §6.6 |

### ⚠️ `broodline_ftue.md`

| Section | Status | Note |
|---|---|---|
| §1 Core principle, the ad-match advantage | ✅ | |
| §2 Beats 1–4, 6–8 | ✅ | |
| §2 Beat 5 — a visible armour type | ⚠️ | **1.1** — teaches a counter trait, not an armour type |
| §2 Beat 7 scripted mutation | ⚠️ | **3.1** — at 9% base this sets a far less unrealistic expectation than at 3% |
| §3 The consumption problem | ✅ | Reinforced; see `broodline_splice_confirm_spec.md` |
| §4 Founder naming cadence | ✅ | Bible §3.3 adds a required skip path with a good default |
| §5 Drip schedule | ⚠️ | Gene Vault entry becomes Gene Lab; add the Sample Store. Continues into `broodline_midgame_arc.md` at day 14 |
| §9 Q1, Q2, Q4, Q5 | ✅ | Closed by bible §9 |
| §9 Q3 Founder naming skip path | ✅ | **Closed: yes, with a good default** — bible §3.3 |

### ⚠️ `broodline_screen_inventory.md`

Superseded by v2 in full. §4's gap list is closed except for the Replay Viewer, and §7 Q4 on map legibility is answered by band zoom, now in bible §5.7.

### ⚠️ `splice_monetization_spec.md`

Superseded by `broodline_monetization.md`. Its four packs promising trait pulls were the highest-risk copy in the set; the correction is recorded as reconciliation item **0.3** and the corrected ladder is in bible §8.3.

### ⚠️ `splice_resource_node_system.md`

| Section | Status | Replaced by |
|---|---|---|
| §2 Node tiers and yields | ✅ | Matches bible §5.3 |
| §2 Apex Vein "rare trait fragments" | ⚠️ | **1.6** — high-tier species samples plus a **catalyst** raising the Aberrant sub-roll |
| §2 Rich Deposit 5–7 days | ✅ | **Closed at six days**, syncing with the weekly rotation |
| §3 Spawn and rotation | ✅ | |
| §4 Harvesting scales with "Lab tier" | ⚠️ | **2.2** — Harvest Array facility |
| §4 Collectors are Apex-only | ❌ | **Bible §5.5** — Collectors harvest anything outside the home region. Apex additionally requires presence |
| §5 Competition and contested nodes | ✅ | |
| §6 Decay and "why move" | ✅ | |
| §7 Map visualization | ✅ | Matches the designed World Map |
| §8 Scout reports | ✅ | Matches the designed Apex Alert |

---

## 6. Handling

Every superseded file now carries a header pointing here, and every current companion carries one saying so. That was the cheap half.

The expensive half is the one file left at §4. Its superseded sections are load-bearing rather than incidental — someone reading the chassis roster today learns ten bodies with armour types and footprints — though its two genuinely valuable sections, the sidegrade envelope and the never-sold rule, have both already been rescued.

**Rewrite what is left of it, and rescue that section first.** They are the parts most likely to be lost, precisely because they are correct and therefore invisible.
