# Broodline

*Creature-breeding tower defense. Mobile, portrait. Relocatable base, alliance territory.*

---

## The game in one paragraph

Splice two creatures into a hybrid. Place hybrids to hold tower-defense waves. Relocate a mobile base across a world map onto richer ground, because resource nodes deplete. Spend the yield on more splices. Every raider has exactly one answering trait, so what you have bred decides whether you win — not how fast you tap. Splicing consumes both parents, so individuals do not survive; the lineage record does.

---

## Start here

**`broodline_bible.md`** is the design. Ten sections, complete, current.

It does not carry every number. Sixteen companion documents hold detail it deliberately does not duplicate, and they are current alongside it. Everything else is either a decision record or a historical one.

If you read one document, read the bible. If you are about to build something, check whether a companion owns it.

---

## Current documents

| Document | What it is | Who needs it |
|---|---|---|
| **`broodline_bible.md`** | The design, §1–10. Current truth. | Everyone |
| **`broodline_screen_inventory_v2.md`** | 56 screens, build priority, what the designed prototypes still need | Design, engineering, production |
| **`broodline_whats_left.md`** | Every open question in the set, classified by who can close it — decisions, procurement, bench tests, playtest, soft launch | **Production. Start here after the bible** |
| **`broodline_build_order.md`** | What to build and in what order, the procurement to start now, and what playtest has to answer | Production |
| **`broodline_waves_01_12.md`** | Chapters 1 and 2 authored — waves 1 to 12, the wave schema, and the designed Courser loss | Design, engineering, anyone authoring waves 21+ |
| **`broodline_waves_13_20.md`** | Chapter 3 authored — Brood, the two-lane transition, and the four-type cap first reached | The same |
| **`broodline_waves_21_28.md`** | Chapter 4 authored — Drift, the four-species worst case, and the three Wave Defeat states | The same |
| **`broodline_waves_29_36.md`** | Chapter 5 authored — Bulwark, and the shield lock correction | The same |
| **`broodline_waves_37_44.md`** | Chapter 6 authored — Delver, the third lane, and the budget formula ceasing to bind | The same |
| **`broodline_waves_45_52.md`** | Chapter 7 authored — Breaker, Delta terrain, and the first wave a utility trait answers | The same |
| **`broodline_waves_53_60.md`** | Chapter 8 authored — eight shapes rather than eight steps, and the Sunder | The same |
| **`broodline_region_graph.md`** | Region adjacency — three rings, forty-three edges, gate pairings, per-segment travel times | Engineering, level design |
| **`broodline_rig_proof.md`** | The socket standard, and what the Phase 1 art pipeline gate has to demonstrate | Art, production |
| **`broodline_combat_engine.md`** | Tick order, determinism, the replay format, counter application sites, breach diagnosis | Engineering. The Phase 1 build gate |
| **`broodline_data_model.md`** | Entity shapes, lineage retention, the client/server authority split | Engineering, backend |
| **`broodline_telemetry.md`** | The seven open questions as events, metrics and thresholds, with build priority | Engineering, production |
| **`broodline_server_topology.md`** | Server population and its derivation, lifecycle, regional assignment, the weekly tick job | Engineering, live-ops |
| **`broodline_notifications.md`** | Notification categories, the daily budget, copy shape, quiet hours, re-engagement | Design, live-ops |
| **`broodline_audio.md`** | The audio division of labour, effect classes, species voice, the 61-asset sound budget | Audio, design |
| **`broodline_seasonal_chapters.md`** | How to author waves past 60, the shape vocabulary, the cost of a ninth raider | Design, live-ops |
| **`broodline_accessibility.md`** | The audit, the four gaps, the settings list, the palette finding | Design, art |
| **`broodline_store_iap.md`** | Entitlement, validation, refunds, offer placement, and why there are no ads | Engineering, monetization |
| **`broodline_offers.md`** | What triggers a pack promotion, frequency caps, and what the algorithm may know | Monetization, engineering |
| **`broodline_design_audit.md`** | What the audit found, how it closed, and six patterns worth not relearning | Anyone tracking how the design got here |
| **`design_handoff_broodline/`** | 20 interactive screen prototypes, design tokens, Character Bible | Design, engineering, illustration |

### Companions — current, and each owns something the bible does not

| Document | Owns |
|---|---|
| **`broodline_economy_model.md`** | Shard income by archetype, the twelve-tier facility curve, sinks, the payer ceiling, Calibration |
| **`broodline_collectors_raiding.md`** | The full raid ruleset: Collector classes, routes, exposure, loss caps, protection systems, Marks |
| **`broodline_live_ops_events.md`** | Event mechanics, Roulette cost and odds and pity, the Apex Cup gauntlet format |
| **`broodline_monetization.md`** | Offer structure, the pack ladder, and the consolidated never-sold list |
| **`broodline_moderation_ugc.md`** | UGC surfaces, filtering, reporting, blocking, age gating, the 12+ rating decision |
| **`broodline_midgame_arc.md`** | Days 14 to 90 — the arc the bible's onboarding section stops short of |
| **`broodline_combat_numbers.md`** | Species stat profiles, all twelve traits at three coverage tiers, Instinct numbers, eight raider profiles, the generation ceiling, the wave budget |
| **`broodline_sample_economy.md`** | Sample sources and rates, weighting, fusing, the Gene Vault capacity curve, retirement yield, the catalyst and the Aberrant sub-roll |
| **`broodline_base_stock.md`** | Where creatures come from: supply rates, species distribution, the Founder and milestone guarantees, the scarcity ratchet, Instinct roll weights |
| **`broodline_campaign_structure.md`** | Sixty waves in eight chapters, the twelve Core milestones, raider introduction order, the designed Courser loss, replay economy, seasonal extension |
| **`broodline_region_roster.md`** | All thirty regions authored, the eight terrain families, band profiles, species weighting, region defence cadence and composition |
| **`broodline_trait_codex.md`** | The 34-entry Codex pool, four entry schemas, the threat board, disclosure and discovery policy, behaviour previews, the collection layer |
| **`broodline_raider_roster.md`** | Raider fiction, the four-body art budget, recognition rules, telegraphing, spawn patterns, the Sunder capstone |
| **`broodline_alliance_territory.md`** | Alliance structure, Stakes and Hold, Stake Assault, garrisons, alliance tech, convoy staging, the weekly tick |
| **`broodline_localization.md`** | Launch language set, naming policy, translation scope and cost, per-storefront pricing, filter procurement, font coverage, age thresholds |
| **`broodline_splice_confirm_spec.md`** | Full copy and interaction states for the splice confirmation flow |

**Where a companion contradicts the bible, the bible is current and the companion needs an edit.**

---

## Decision records

Not designs. Kept because they hold reasoning the current documents deliberately omit.

| Document | Why it still exists |
|---|---|
| **`broodline_reconciliation.md`** | The decision register — nineteen decisions with the reasoning behind each. The bible states what is true; this states *why*. Part 4 shows what falls over if any decision is reopened. |
| **`broodline_supersession_map.md`** | What replaced what, document by document and section by section. Read it before opening anything below this line. |

---

## Superseded — do not build from these

Every file below carries a header saying so.

**Nothing is queued for rewrite.** `broodline_chassis_roster.md` was the last; everything in it worth keeping is now in the bible, the combat numbers and the base-stock spec.

**Superseded outright.** `broodline_spec_reconciliation.md` — an earlier register, replaced by `broodline_reconciliation.md`. Its constants ledger is wrong on most rows and must not be used.

**The eight original specs still superseded.** Written one system at a time against an unsettled design. A ninth, `broodline_alliance_territory.md`, has since been brought current and moved up. `broodline_chassis_roster.md` joins them.

`broodline_genetics_system.md` · `broodline_combat_system.md` · `broodline_gene_vault.md` · `broodline_art_direction.md` · `broodline_ftue.md` · `broodline_screen_inventory.md` · `splice_monetization_spec.md` · `splice_resource_node_system.md`

The two files still named `splice_*` predate the project rename; Splice was the working title.

---

## Reading order by role

**Engineer** — bible §1–4 for the core loop, then `broodline_combat_engine.md` and `broodline_data_model.md`, which everything else runs on. Then the screen inventory for build order, and the splice confirm spec before touching that screen. Instrument `broodline_telemetry.md`'s first four questions from the first playable build. `broodline_collectors_raiding.md` before anything in the PvP layer. Note the Splice Chamber prototype is a static file with no logic; it is built from spec, not ported.

**Designer** — bible in full, then the screen inventory's rework column. Twelve designed screens are wrong in ways not visible from the prototype, and thirty-five screens have never been designed at all.

**Illustrator** — `broodline_rig_proof.md` first, since it is the gate. Then bible §10, §1.2 for the six species, and `broodline_raider_roster.md` for the eight raiders. The Character Bible in the handoff bundle is the commission reference. **Thirty-six assets total** — 24 creature, 12 raider. Creatures are 3D; two species get rigged as the pipeline proof before anything else.

**Economy / live-ops** — bible §7.6 and §8, then `broodline_economy_model.md` and `broodline_live_ops_events.md`, which hold the actual numbers. Every value is a soft-launch starting point. Bible §8.6 is the list to check any new offer against.

**Production** — this file, then `broodline_whats_left.md` for what remains, then `broodline_build_order.md` for the order. Two procurement items should be in motion before the first screen is built.

**New to the project** — this file, then the bible, then skim the reconciliation register's summary table.

---

## Project status

**The design and the content are done. Almost none of the game is made.**

Ten bible sections, nineteen settled decisions, ten derived constraints, eighteen current companions. Every mechanic specified. **All sixty campaign waves authored, all thirty regions authored, the region graph authored.** Every constant set to a soft-launch starting value; the open questions that remain are tuning questions, answered by playtest rather than by writing.

Against that: **fifty-nine screens of which eight exist**, thirty-six character assets of which none are modelled, twenty animation clips and eleven behaviour previews of which none are produced, and two procurement items with external lead times that nobody has started.

**`broodline_build_order.md` is the document that says what to do about it.** In short: start the English moderation filter procurement now from a vendor that covers the full language list, prove the creature rig on two species before anything large begins, and instrument three things from the first playable build — the Litter question, Skirmisher's spawn interval, and whether anyone picks Screen at wave 50.

**Authoring the campaign found ten errors in the specs it was written against**, including three raider locks that were not locks. None would have been found by reading. The same argument applies to the seasonal chapters: author them before the season is scheduled.

---

## The five things that hold the design together

If a change would break one of these, it needs a conversation rather than a commit.

1. **No purchase grants trait access.** Packs, pulls, tier perks and alliance tech grant coverage only. This is the sole thing keeping hard counters from being a paywall, and it has already leaked in four separate systems.
2. **Acquiring all six species yields all eight counters.** A Gen-1 creature always carries its species' trait pair, never rolled, so acquiring the species is acquiring its counters. Five Founders and one campaign milestone deliver all six inside the first week.
3. **Waves escalate by volume, never by resistance.** No raider is ever immune to a tier-I answer.
4. **No creature is ever lost involuntarily.** Combat, raids and assaults never remove one. Only splicing and retirement do, and both are the player's choice.
5. **Control confers advantage, never exclusion.** Common Veins are permanently unclaimable and never carry the non-ally penalty, the penalty itself is capped, and there are no base attacks.
6. **No third-party advertising.** No rewarded video, no interstitials, no ad SDK. The charges an ad would have granted were surplus, so the free path loses nothing and gains an uninterrupted one. Broodline promotes its own packs only when a player runs short of something, never unprompted, on an engine that never prices by what a player has spent.
