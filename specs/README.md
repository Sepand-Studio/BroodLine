# Broodline — Spec Index

*Creature-splicing tower defense. Mobile, portrait. Relocatable base, alliance territory.*

Splice two creatures into a hybrid. Place hybrids to hold tower-defense waves. Relocate a mobile base across a world map onto richer ground, because resource nodes deplete. Every raider has exactly one answering trait, so what you have bred decides whether you win — not how fast you tap. Splicing consumes both parents; the lineage record survives.

---

## How this folder is organized

Files are grouped by **status**, not topic. Topic is in the filename; status is what you couldn't see before.

| Folder | Meaning | Build from it? |
|---|---|---|
| `00-bible/` | The design. One file. | **Yes.** The only one. |
| `01-companions/` | Current specs the bible points to but doesn't contain. | Yes, alongside the bible. |
| `02-decisions/` | Why the design is what it is. Decision register and supersession map. | No — read before reopening a decision. |
| `99-archive/` | Superseded in full. | Never. |

Every file opens with front matter: `status`, `superseded-by` where applicable, and a `note` stating what it holds and what is known to be stale. **A file without front matter is treated as archive.** Nothing lives in the root except this index.

---

## The three eras

Understanding this explains every inconsistency you will find.

**Era 1 — nine original specs.** One system each, written against an unsettled design. Ten chassis, three armor types, a damage triangle. Superseded by the bible; mapped section-by-section in `02-decisions/broodline_supersession_map.md`.

**Era 2 — twelve companion documents.** Campaign, regions, economy, raiding, live-ops, mid-game, moderation, monetization, trait codex, chassis roster, enemy archetypes, and a change-control register. Written to fill gaps in era 1, using era 1's model. **All twelve are now dealt with:** four archived outright, eight reconciled against the bible and reissued as `01-companions/` (their era-2 versions archived alongside). The reconciliation raised twelve decisions, logged as register Part 6.

**Era 3 — the bible.** Six species, eight raiders, twelve counter traits, hard counters with no damage floor, Gene Lab. Replaced the whole model, not just the details. Thirty-two decisions in `02-decisions/broodline_reconciliation.md`.

The tell for era: a document that says *chassis* is era 1 or 2. A document that says *species* or *Gene Lab* is era 3.

---

## Every file

### 00-bible
| File | What it is |
|---|---|
| `broodline_bible.md` | §1–10, complete, current. Where anything disagrees with it, the bible wins. Amended 2026-09-05 at §5.2, §5.6, §6.1, §7.2, §8.2, §8.3, §8.4, §8.6, §9.6, §10.5 (register Part 6). |

### 01-companions
| File | What it is |
|---|---|
| `broodline_screen_inventory_v2.md` | 34 screens, build priority, rework column for the designed prototypes. Revised 2026-09-05 for register Part 6; changed screens marked `⚠`. |
| `broodline_splice_confirm_spec.md` | Copy and interaction states for splice confirmation. Bible §2.7. |
| `broodline_trait_utility.md` | What Carapace, Litter, Regrow and Screen do: pressure shapes, tier curves, stacking rules, the campaign's three utility waves. Bible §1.2, §4.4. Written 2026-09-05; register 6.13. |
| `broodline_campaign_structure.md` | 60 waves, 7 chapters, 12 Core milestones, species guarantee. Bible §4.8, §7.3. Reconciled 2026-09-05. |
| `broodline_region_roster.md` | All 30 regions authored: bands, lanes, nodes, adjacency, gates, travel times. Bible §5, §6.8. Reconciled 2026-09-05. |
| `broodline_collectors_raiding.md` | Collector classes, per-gate exposure, raid engagement, loss caps, protection, Marks. Bible §4.9, §5.6. Reconciled 2026-09-05. Its §14 amendments are applied to the bible and logged at register 6.1–6.4. |
| `broodline_economy_model.md` | 40/hr anchor, node depletion pools, faucets and sinks by archetype, facility curve, the sample economy, pack ladder, server population. Bible §5.3, §7, §8. Reconciled 2026-09-05; register 6.5. |
| `broodline_monetization.md` | Short addendum to bible §8: Double Regen anchor, upper shard tiers, offer cadence, ads, regional pricing. Reconciled 2026-09-05; register 6.6–6.7. |
| `broodline_live_ops_events.md` | The design behind each §8.4 row: Splice Census, always-on Roulette, Apex Cup gauntlet, Mutation Surge, Recipe Share, the calendar. Reconciled 2026-09-05; register 6.8–6.10. |
| `broodline_moderation_ugc.md` | Six UGC surfaces, the four required mechanisms, age gating, enforcement, response times, rating-integrity. The submission requirement. Reconciled 2026-09-05; register 6.11–6.12. |
| `broodline_midgame_arc.md` | Days 14–90 as one timeline: every companion's clock aligned, collisions resolved, phase goals, metrics. Reconciled 2026-09-05. |

### 02-decisions
| File | What it is |
|---|---|
| `broodline_reconciliation.md` | Thirty-two settled decisions and the reasoning. Part 4 shows what falls over if each is reopened; Part 6 holds the companion-reconciliation decisions. |
| `broodline_supersession_map.md` | Era 1 → bible, section by section. Does not cover era 2. |

### 99-archive
Nine era-1 originals (`genetics_system`, `combat_system`, `gene_vault`, `art_direction`, `alliance_territory`, `ftue`, `screen_inventory`, `splice_monetization_spec`, `splice_resource_node_system`) plus twelve era-2 documents, four whose model no longer exists and eight replaced by reconciled versions:

| File | Replaced by |
|---|---|
| `broodline_chassis_roster.md` | Bible §1.2, six species |
| `broodline_enemy_archetypes.md` | Bible §4.4, eight raiders. **Name collision:** "Skitter" is an enemy body here and a player species in the bible. |
| `broodline_trait_codex.md` | Bible §1.2/§4.4. The "Trait Codex" of §4.7 is a UI component, not this file. |
| `broodline_spec_reconciliation.md` | `02-decisions/broodline_reconciliation.md` |
| `broodline_campaign_structure_era2.md` | `01-companions/broodline_campaign_structure.md` |
| `broodline_region_roster_era2.md` | `01-companions/broodline_region_roster.md` |
| `broodline_collectors_raiding_era2.md` | `01-companions/broodline_collectors_raiding.md` |
| `broodline_economy_model_era2.md` | `01-companions/broodline_economy_model.md` |
| `broodline_monetization_era2.md` | Bible §8 and `01-companions/broodline_monetization.md` |
| `broodline_live_ops_events_era2.md` | `01-companions/broodline_live_ops_events.md` |
| `broodline_moderation_ugc_era2.md` | `01-companions/broodline_moderation_ugc.md` |
| `broodline_midgame_arc_era2.md` | `01-companions/broodline_midgame_arc.md` |

---

## Reading order by role

**Engineer** — bible §1–4, then `01-companions/` screen inventory for build order, then the campaign, region and Collectors companions for the data the engine consumes.

**Designer** — bible in full, then the screen inventory's rework column.

**Illustrator** — bible §10, §1.2 for the six species, §4.4 for the eight raiders. Ignore `99-archive/enemy_archetypes` entirely; its names conflict.

**Economy / live-ops** — bible §7.6 and §8, then the economy, monetization and live-ops companions.

**New to the project** — this file, then the bible, then the reconciliation register's summary table.

---

## Promotion rule

`03-unreconciled/` is empty and has been removed. If it is ever recreated for a new document, the rule stands: a file leaves it for `01-companions/` only when someone has read it against the bible, fixed every stale reference and rewritten its front-matter note to say so, or for `99-archive/` if the bible already covers it. No file stays there past production start.

---

## Project status

**Design: complete.** Ten bible sections, thirty-two decisions, eleven companions.

**Not done:**

- **Combat numbers table.** The one document the set still lacks. Owes per-raider costs, per-wave counts, and absolute values for the four utility traits across twelve tiers.
- **Economy tuning.** Structure and starting values settled in the economy companion; every value needs session data. First check: first tier-III counter by day 21–25.
- **Instinct source weighting.** Unset.
- **Localization plan.** Does not exist; moderation filter coverage and regional pricing both depend on it.

**Settled 2026-09-05:** campaign length (60 / 7), season length (four weeks), rating (12+), convoy grace period (step-down to day 22), trait utility (keep all four).

**The spec set is internally consistent as of 2026-09-05.** Bible plus ten companions, thirty-one register decisions, twenty-one archived documents. Every current file has been read against the bible in the same pass.

---

## The five things that hold the design together

1. **No purchase grants trait access.** Packs, pulls, tier perks and alliance tech grant coverage only.
2. **Acquiring all six species yields all eight counters**, and campaign milestones guarantee species so luck cannot lock anyone out.
3. **Waves escalate by volume, never by resistance.** No raider is ever immune to a tier-I answer.
4. **No creature is ever lost involuntarily.** Only splicing and retirement remove one, and both are the player's choice.
5. **Control confers advantage, never exclusion.** Common Veins are unclaimable, the non-ally penalty is capped, there are no base attacks.
