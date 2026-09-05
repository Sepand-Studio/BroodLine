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
| `03-unreconciled/` | Written before the bible, against an older model. Contain structure the bible lacks, and references it contradicts. | **Not yet.** Each needs a reconciliation pass first. |
| `99-archive/` | Superseded in full. | Never. |

Every file opens with front matter: `status`, `superseded-by` where applicable, and a `note` stating what it holds and what is known to be stale. **A file without front matter is treated as archive.** Nothing lives in the root except this index.

---

## The three eras

Understanding this explains every inconsistency you will find.

**Era 1 — nine original specs.** One system each, written against an unsettled design. Ten chassis, three armor types, a damage triangle. Superseded by the bible; mapped section-by-section in `02-decisions/broodline_supersession_map.md`.

**Era 2 — twelve companion documents.** Campaign, regions, economy, raiding, live-ops, mid-game, moderation, monetization, trait codex, chassis roster, enemy archetypes, and a change-control register. Written to fill gaps in era 1, using era 1's model. **Never mapped onto the bible.** Five are now replaced and archived, one has been reconciled and promoted; seven hold content the bible still lacks and sit in `03-unreconciled/`.

**Era 3 — the bible.** Six species, eight raiders, twelve counter traits, hard counters with no damage floor, Gene Lab. Replaced the whole model, not just the details. Nineteen decisions in `02-decisions/broodline_reconciliation.md`.

The tell for era: a document that says *chassis* is era 1 or 2. A document that says *species* or *Gene Lab* is era 3.

---

## Every file

### 00-bible
| File | What it is |
|---|---|
| `broodline_bible.md` | §1–10, complete, current. Where anything disagrees with it, the bible wins. |

### 01-companions
| File | What it is |
|---|---|
| `broodline_screen_inventory_v2.md` | 32 screens, build priority, rework column for the designed prototypes. |
| `broodline_splice_confirm_spec.md` | Copy and interaction states for splice confirmation. Bible §2.7. |
| `broodline_campaign_structure.md` | 60 waves, 7 chapters, 12 Core milestones, species guarantee. Bible §4.8, §7.3. Reconciled 2026-09-05. |

### 02-decisions
| File | What it is |
|---|---|
| `broodline_reconciliation.md` | Nineteen settled decisions and the reasoning. Part 4 shows what falls over if each is reopened. |
| `broodline_supersession_map.md` | Era 1 → bible, section by section. Does not cover era 2. |

### 03-unreconciled
| File | What the bible lacks that this holds | Known stale |
|---|---|---|
| `broodline_region_roster.md` | 30 regions in 3 bands with gates; bible §5.7 has no bands | Chassis-weighted drops; eight terrain families (bible has none); 21 regions unauthored |
| `broodline_economy_model.md` | Harvest Array scaling, Core cost table, store ladder | Every table built on era-2 trait economics |
| `broodline_collectors_raiding.md` | Transit Board, raid cap, immunity windows, cargo maths | Unchecked against §4.9/§5.6 |
| `broodline_live_ops_events.md` | Full calendar | Duplicates bible §8.4 — merge, don't keep both |
| `broodline_midgame_arc.md` | Day 14–90; bible §9 stops at day 14 | Day-28 chassis beat |
| `broodline_moderation_ugc.md` | Moderation policy; bible has none | Recipe shape names chassis |
| `broodline_monetization.md` | Offer detail | Overlaps bible §8 heavily; chassis in guardrail table |

### 99-archive
Nine era-1 originals (`genetics_system`, `combat_system`, `gene_vault`, `art_direction`, `alliance_territory`, `ftue`, `screen_inventory`, `splice_monetization_spec`, `splice_resource_node_system`) plus five era-2 documents whose model no longer exists:

| File | Replaced by |
|---|---|
| `broodline_chassis_roster.md` | Bible §1.2, six species |
| `broodline_enemy_archetypes.md` | Bible §4.4, eight raiders. **Name collision:** "Skitter" is an enemy body here and a player species in the bible. |
| `broodline_trait_codex.md` | Bible §1.2/§4.4. The "Trait Codex" of §4.7 is a UI component, not this file. |
| `broodline_spec_reconciliation.md` | `02-decisions/broodline_reconciliation.md` |
| `broodline_campaign_structure_era2.md` | `01-companions/broodline_campaign_structure.md` |

---

## Reading order by role

**Engineer** — bible §1–4, then `01-companions/` screen inventory for build order, then the splice confirm spec before that screen. Do not open `03-unreconciled/` until its files have been promoted.

**Designer** — bible in full, then the screen inventory's rework column.

**Illustrator** — bible §10, §1.2 for the six species, §4.4 for the eight raiders. Ignore `99-archive/enemy_archetypes` entirely; its names conflict.

**Economy / live-ops** — bible §7.6 and §8, then `03-unreconciled/economy_model` and `live_ops_events` knowing they need reconciling first.

**New to the project** — this file, then the bible, then the reconciliation register's summary table.

---

## Promotion rule

A file moves from `03-unreconciled/` to `01-companions/` when someone has read it against the bible, fixed or removed every stale reference, and rewritten the front-matter note to say so. It moves to `99-archive/` if the bible turns out to already cover it. No file stays in `03-unreconciled/` past production start.

---

## Project status

**Design: complete** at the bible level. Ten sections, nineteen decisions.

**Not done:**

- **Era-2 reconciliation.** Seven documents in `03-unreconciled/` hold structure the bible needs and references it contradicts.
- **Trait utility balance.** Four of twelve traits (Carapace, Litter, Regrow, Screen) counter nothing. If they are weak the roster collapses toward Ember and Hollow. Most under-specified thing in the design.
- **Economy tuning.** Structure settled; every value needs session data.
- **Campaign length.** Settled at 60 waves / 7 chapters (2026-09-05); 65 is retired.
- **Instinct source weighting, season length, convoy grace period.** Unset.

---

## The five things that hold the design together

1. **No purchase grants trait access.** Packs, pulls, tier perks and alliance tech grant coverage only.
2. **Acquiring all six species yields all eight counters**, and campaign milestones guarantee species so luck cannot lock anyone out.
3. **Waves escalate by volume, never by resistance.** No raider is ever immune to a tier-I answer.
4. **No creature is ever lost involuntarily.** Only splicing and retirement remove one, and both are the player's choice.
5. **Control confers advantage, never exclusion.** Common Veins are unclaimable, the non-ally penalty is capped, there are no base attacks.
