# Broodline

*Creature-breeding tower defense. Mobile, portrait. Relocatable base, alliance territory.*

---

## The game in one paragraph

Splice two creatures into a hybrid. Place hybrids to hold tower-defense waves. Relocate a mobile base across a world map onto richer ground, because resource nodes deplete. Spend the yield on more splices. Every raider has exactly one answering trait, so what you have bred decides whether you win — not how fast you tap. Splicing consumes both parents, so individuals do not survive; the lineage record does.

---

## Start here

**`broodline_bible.md`** is the design. Ten sections, complete, current. Everything else in this repository is either a companion to it or a historical record of how it was reached.

If you read one document, read that one.

---

## Current documents

| Document | What it is | Who needs it |
|---|---|---|
| **`broodline_bible.md`** | The design, §1–10. Current truth. | Everyone |
| **`broodline_screen_inventory_v2.md`** | 32 screens, build priority, what the designed prototypes still need | Design, engineering, production |
| **`broodline_splice_confirm_spec.md`** | Full copy and interaction states for the splice confirmation flow. Referenced by bible §2.7. | Whoever builds the splice screen |
| **`design_handoff_broodline/`** | 20 interactive screen prototypes, design tokens, Character Bible. Visual language and layout. | Design, engineering, illustration |

**Where the bible and the prototypes disagree, the bible is current.** The screen inventory names every case.

---

## Historical documents

Kept for provenance. Do not build from these.

| Document | Why it still exists |
|---|---|
| **`broodline_reconciliation.md`** | The decision register — nineteen decisions with the reasoning behind each. The bible states what is true; this states *why*. Worth keeping: when someone proposes reopening a decision, Part 4 shows what falls over. |
| **`broodline_supersession_map.md`** | Maps each original spec section to what replaced it. Useful only for tracing where an idea came from. |
| **The nine original specs** | Superseded in full by the bible. See below. |

---

## The nine original specs

These were written one system at a time against an unsettled design. They are superseded and should carry a deprecation header.

`broodline_genetics_system.md` · `broodline_combat_system.md` · `broodline_gene_vault.md` · `broodline_art_direction.md` · `broodline_alliance_territory.md` · `broodline_ftue.md` · `broodline_screen_inventory.md` · `splice_monetization_spec.md` · `splice_resource_node_system.md`

**Header to prepend to each:**

```
> **SUPERSEDED.** This document is no longer current and should not be
> built from. The design lives in `broodline_bible.md`.
>
> Kept for historical reference. See `broodline_supersession_map.md`
> for a section-by-section map of what replaced what.
```

The two files still named `splice_*` predate the project rename; Splice was the working title.

---

## Reading order by role

**Engineer** — bible §1–4 for the core loop, then the screen inventory for what to build in what order, then the splice confirm spec before touching that screen. Note the Splice Chamber prototype is a static file with no logic; it is built from spec, not ported.

**Designer** — bible in full, then the screen inventory's rework column. Eleven designed screens are wrong in ways not visible from the prototype.

**Illustrator** — bible §10, then §1.2 for the six species and §4.4 for the eight raiders. The Character Bible in the handoff bundle is the commission reference. Total roster is 24 assets.

**Economy / live-ops** — bible §7.6 and §8. Every number in §7.6 is a soft-launch starting point, not a decision. §8.6 is the list to check any new offer against.

**New to the project** — this file, then the bible, then skim the reconciliation register's summary table to see what was decided and what it replaced.

---

## Project status

**Design: complete.** Ten bible sections, nineteen settled decisions, ten derived constraints.

**Not done:**

- **Economy tuning.** Sample drop rates, fusing costs, capacity curve, catalyst frequency, charge regen. Structure is settled; every value needs session data.
- **Trait utility balance.** Four of twelve traits counter nothing — Carapace, Litter, Regrow, Screen. If they are weak, four of six species become delivery vehicles for a single counter and the roster collapses toward Ember and Hollow. This is the most under-specified thing in the design.
- **Instinct source weighting.** Gen-1 base stock rolls an Instinct weighted by species; the weights are unset.
- **Season length.** Six or eight weeks.
- **Convoy interception grace period.** Day 14 by default, unconfirmed.

---

## The five things that hold the design together

If a change would break one of these, it needs a conversation rather than a commit.

1. **No purchase grants trait access.** Packs, pulls, tier perks and alliance tech grant coverage only. This is the sole thing keeping hard counters from being a paywall, and it has already leaked in three separate systems.
2. **Acquiring all six species yields all eight counters**, and campaign milestones guarantee species so luck cannot lock anyone out.
3. **Waves escalate by volume, never by resistance.** No raider is ever immune to a tier-I answer.
4. **No creature is ever lost involuntarily.** Combat, raids and assaults never remove one. Only splicing and retirement do, and both are the player's choice.
5. **Control confers advantage, never exclusion.** Common Veins are permanently unclaimable, the non-ally penalty is capped, and there are no base attacks.
