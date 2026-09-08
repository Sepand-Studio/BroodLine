---
status: superseded
folder: 99-archive
superseded-by: broodline_combat_numbers.md
note: >
  Transitional chassis-to-species rename document. The six stat lines now
  live in combat numbers section 3, which carries the full table.
---

# Broodline — Species Stat Lines

*Design spec. The six bodies as numbers.*

Extends bible §1.2 and §4.3. Supersedes `broodline_chassis_roster.md` in full — that document describes ten chassis across five roles under the abandoned damage triangle. Its multiplier grammar is retained; its roster is not.

---

## 1. Why This Document Exists

Bible §1.1 says species "sets silhouette, combat role and base stats." No document says what those stats are.

The consequence is the same one the economy model had before it fixed the Common Vein: every combat number in the set is currently unfalsifiable. Wave budgets, coverage tiers, raider counts and the four-type cap are all expressed relative to a defender whose output is undefined. Nothing can be tuned against nothing.

There is a second reason, newer. `broodline_utility_traits.md` gives Vetch, Skitter, Loam and Pale real second traits. All four were implicitly balanced around a dead slot. Their stat lines have to be set now, with the utility traits in hand, or they will be set later by whoever writes the first wave table.

---

## 2. Rename Applied

**Carapace is now Plating.** The anti-Tyranid checklist rules out *carapace* as vocabulary; the trait had no dependencies outside one table. Bible §1.2 and §1.6 both carry the old string and need replacing. Every table below uses the new name.

| Species | Role | Traits |
|---|---|---|
| **Vetch** | Wall | Plating, **Taunt** |
| **Ember** | Splash | **Cinder**, **Splash** |
| **Skitter** | Swarm | **Sprint**, Litter |
| **Hollow** | Sniper | **Reach**, **Pierce** |
| **Loam** | Support | Regrow, **Burrow** |
| **Pale** | Control | Screen, **Chill** |

---

## 3. The Baseline

Multipliers are meaningless without an absolute. One unit of everything:

| | Baseline (1.0×) |
|---|---|
| Hit points | 1,000 |
| Damage per hit | 100 |
| Attack interval | 1.0 s |
| Range | 4 tiles |
| Footprint | 1 tile |

A 1.0× creature therefore does **100 damage per second**. No species is exactly 1.0× — the baseline is a reference point, not a creature.

---

## 4. The Six Stat Lines

| Species | Role | Footprint | HP | Damage | Range | Atk speed | Effective DPS |
|---|---|---|---|---|---|---|---|
| **Vetch** | Wall | 2 tiles | 2.4× | 0.50× | 0.7× | 0.8× | 0.40 |
| **Ember** | Splash | 1 tile | 0.55× | 1.00× | 0.9× | 0.9× | 0.90 |
| **Skitter** | Swarm | 2 tiles, 2 bodies | 0.45× each | 0.31× each | 0.9× | 1.6× | 0.50 each, 1.00 total |
| **Hollow** | Sniper | 1 tile | 0.45× | 1.60× | 1.7× | 0.55× | 0.88 |
| **Loam** | Support | 1 tile | 1.30× | 0.50× | 0.8× | 0.9× | 0.45 |
| **Pale** | Control | 1 tile | 0.90× | 0.70× | 1.2× | 1.0× | 0.70 |

**Ember's damage is area damage**, applied in full to every raider within 1.5 tiles of the impact. Against a single target it is a mediocre body; against the eight Skirmishers a wave sends it is the best in the game. This is what makes Splash a coverage trait rather than a damage trait, and it is why 1.00× reads high here and plays fair.

**Skitter is one creature with two bodies** occupying adjacent tiles, against the deployment cap of five. Both bodies share the creature's traits, generation and lineage entry. Bodies fall individually; the creature enters regeneration when the second falls. Screen and Plating auras project from the lead body only — without that rule a two-body creature doubles every aura in the game.

---

## 5. The Rule Behind the Numbers

**Counter density is paid for in fragility, never in output.**

Ember and Hollow each carry two counters. Bible §1.2 calls that unavoidable arithmetic and accepts the asymmetry; this is where the asymmetry gets priced. Both are the two most fragile bodies in the roster at 0.55× and 0.45× HP — roughly a fifth of Vetch's. They have the highest single-body output and they cannot survive a lane on their own.

That converts the imbalance into a dependency rather than a nerf. An Ember and a Hollow together answer four of the eight raiders, which is close to a legal wave's entire composition — and they will both be dead in twelve seconds without a Vetch holding the mouth of the lane with Taunt and Plating, or a Pale delaying acquisition with Screen. The efficient species need the utility species to function, which is exactly the roster breadth the design wants and the thing §1.2 was worried it would lose.

The alternative — flattening Ember and Hollow's damage — was rejected. Hollow is the sniper; high damage per shot is its identity, and taking that away leaves a body with two counters and no character.

---

## 6. What Does Not Modify Stats

This list matters more than the table, because it is where the design differs from the genre default.

- **Generation grants nothing.** A gen-5 creature has the identical stat line to a gen-1 of the same species. Generation gates the coverage ceiling (constraint 9) and nothing else.
- **Growth grants nothing.** Cosmetic, per §1.5.
- **Traits grant nothing.** Counters and utility change what a creature *handles*, never what it *hits for*. No trait in the game modifies HP, damage, range or attack speed. Plating's −40% is damage taken, which is mitigation applied at resolution, not a stat on the card.
- **Instinct grants nothing permanent.** Overwatch, Last Stand and Pack Sense apply conditional in-combat modifiers that appear in the log, never on the creature card.

**The only permanent stat modifier in the game is Broodline Affinity, +5%, one per creature.**

So a player's combat power grows through **coverage and composition only.** Wave 60 is fought with the same stat lines as wave 1, at tier III coverage instead of tier I, with utility traits that halve the running cost. This is consistent with §4.5's escalation-by-volume rule and with the economy model's founding principle that time and money buy rate rather than reach — but it is a real constraint on the wave tables and §8 flags it.

---

## 7. Raider Hit Points Follow From Wave Budget

The stat lines above make the raider side derivable rather than a second free-floating set of numbers.

**Anchor:** a wave-1 deployment of five gen-1 creatures at tier-I coverage should clear the wave comfortably, because wave 1 is the FTUE. Five average bodies run about 70 effective DPS each, so 350 DPS against a single-lane wave with a 25-second duration, resolved in about 14 — roughly 40% headroom.

Wave 1's budget is 100 points and a Skirmisher costs 10, so wave 1 is ten Skirmishers. 350 DPS × 14 s ≈ 5,000 damage across ten bodies:

> **Raider HP = 50 × its budget cost.**

Skirmisher at 10 points is therefore **500 HP**, half a baseline creature. Every other raider inherits its HP from its budget cost the same way, which means the budget formula — `100 × 1.04^(w-1) × L` — now sets raider durability and raider count from one number instead of two.

Indicative costs, to be checked against the existing wave tables rather than imposed on them:

| Raider | Cost | HP |
|---|---|---|
| Skirmisher | 10 | 500 |
| Brood | 20 | 1,000 |
| Courser | 25 | 1,250 |
| Drift | 25 | 1,250 |
| Delver | 30 | 1,500 |
| Lash | 30 | 1,500 |
| Bulwark | 35 | 1,750 |
| Breaker | 40 | 2,000 |

**Cost prices total effective hit points, not bodies.** A Brood's 1,000 covers the parent and every split it produces — a 400 HP parent and three 200 HP splits. A Bulwark's 1,750 includes its shield. Without this rule the identity in the paragraph above fails: a Brood-heavy wave would carry more hit points than its budget priced, and every ratio derived from `50 × budget` would understate it.

---

## 8. Open Items

**1. ~~Whether coverage alone carries 65 waves.~~ Answered — it does not.** §6 leaves raw output flat while the budget grows 1.04× per wave and lane count quadruples. `broodline_wave_scaling_audit.md` ran this and found the campaign unwinnable from about wave 15; `broodline_wave_scaling_addenda.md` and `broodline_bastion_economy_check.md` resolve it with proportional coverage and a free Bastion multiplier tied to Core tier. The instinct recorded here was right — the fix was Ark-side scaling, not creature stat growth, which would have reintroduced a damage floor through the back door.

**2. Skitter's two bodies against the deployment cap.** Two tiles for one slot is a real cost on a narrow board, and whether it is the right cost depends on emplacement counts per region, which the region roster sets. Verify against the three-lane regions specifically.

**3. Ember's splash radius.** 1.5 tiles is a placeholder chosen to catch two adjacent Skirmishers and not three. It is the number that decides what Splash I means in practice, so it belongs in the coverage tier table rather than here.

---

*Downstream: the wave tables need a pass against §7 as amended, and the Codex entries for the four utility traits still need authoring per §4.7.*
