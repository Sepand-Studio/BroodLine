# Broodline — Four Decisions

*Closes the three items flagged in `broodline_errata_pass.md` §12–§14, plus a fourth found while resolving them.*

---

## 1. Module Naming — Resolved

The reconciliation document already settled this and the ruling was not propagated: **"Hatchery | Roster capacity | Design name, spec Habitat."** The design name wins. The bible uses Hatchery and Splicing Chamber throughout.

**Canonical, from the bible:**

| Module | Governs |
|---|---|
| **Splicing Chamber** | Maximum creature generation, and therefore the coverage ceiling |
| **Hatchery** | Roster capacity, floor of 20, scaling upward |
| **Gene Vault** | Sample capacity |
| **Harvest Array** | Harvest yield |
| **Drive** | Transit speed |
| **Core** | Ark integrity in region defence; caps all other modules |

"Gene Lab" is the screen containing all six. "Sample Store" is the screen where samples are viewed, renamed from Gene Vault — the module keeps the name, the screen does not. That collision is survivable but it is the kind of thing that generates this exact problem again; if anything is renamed later, rename the module.

Every document produced this session used Habitat and Splice Chamber. All corrected.

---

## 2. The Gene Vault Has Six Modules, and the Economy Model Prices Five

This fell out of the naming pass and it is the largest single error in the set.

The Gene Vault progression spec describes **five parallel modules** — Harvest Array, Hatchery, Splicing Chamber, Drive, Core. The bible lists **six**, adding Gene Vault for sample capacity. The economy model prices "Core plus four modules at 35% of Core's cost each," which is the five-module structure.

| | Total | Full Vault at Core rate |
|---|---|---|
| Core + 4 modules, as priced | 1,402,682 | 21.5 months |
| Core + 5 modules, as specified | **1,607,240** | **24.7 months** |

**The long-term arc is 24.7 months, not 21.5.** Every reference to 21.5 in this session's documents was inherited from the economy model and is wrong by three months.

Nothing else moves. The Bastion is free, cosmetics are a share of income rather than a fixed total, and Calibration begins after the Vault is complete — so it starts later and its three-year figures shift, but the rates in `broodline_terminal_sink.md` §4 are unaffected.

**The economy model needs the sixth module added to §5 and the arc restated in §7.** This is a repricing, not a redesign, and it makes the game longer rather than shorter — which is the safe direction for an error to have run.

---

## 3. The Bastion Is PvE Only

The Gene Vault spec carries a guardrail this session walked past: *"No module increases creature damage, health, or the five-creature deployment cap. Core's Ark integrity is the sole exception and it applies only to PvE region defence — raids never target the Ark."*

The Bastion gives Core tier a creature-damage effect. That is a real amendment to a stated rule and it should be made deliberately rather than absorbed.

**Amend the guardrail. Do not amend the scope.**

> **The Bastion applies to campaign waves and region defence. It does not apply to raids, in either direction.**

The guardrail's second clause is the important one: Core's combat effect was deliberately confined to PvE. Extending it into raids would make Core tier a PvP power stat, and with matchmaking banded at ±2 Core tier that is a 1.44× damage spread inside a legal match — decided before either player picks a creature.

Keeping the Bastion out of raids means **raids are settled by roster and composition alone.** Both sides fight at baseline stat lines, coverage does all the work, and a well-composed weaker account can beat a badly-composed stronger one. That is the only place in the game where that is true and it is worth protecting.

The revised guardrail: *no module increases creature damage, health, or the deployment cap. Core is the sole exception, through Ark integrity and the Bastion, and both apply only to PvE.*

This also removes the second open item in `broodline_bastion_economy_check.md` §6 — there is no attacker Bastion level for a defender to need shown.

---

## 4. Brood — Cost Prices Total Effective Hit Points

Confirming the provisional fix in the errata.

> **A raider's budget cost prices its total effective hit points, including everything it spawns and everything that absorbs damage on its behalf.**

A Brood at 20 points is 1,000 hit points: a 400 HP parent and three 200 HP splits. A Bulwark at 35 points is 1,750 including its shield.

This keeps the identity that a wave's total hit points equal `50 × budget`, which is what every ratio table in the set depends on. The alternative — letting splits ride free — means a Brood-heavy wave carries hit points its budget never priced, and every ratio understates the requirement by an amount that varies with composition.

**Brood is still the right kind of dangerous.** Its threat was never that it was cheap; it is that four bodies across a lane is a different problem from one body, and that without Cinder the splits arrive faster than single-target damage can clear them. Splitting a fixed hit point pool into four pieces makes it harder to kill, not easier, at no extra cost to the wave budget. The archetype does not need underpricing to work.

---

## 5. Wave 1 — The FTUE Owns Chapter 1

The budget formula puts ten Skirmishers on the board in the first wave a player ever sees. That is not a tuning problem, it is the formula being applied where it does not belong.

> **Waves 1 through 6 are hand-authored FTUE content. The budget formula binds from wave 7.**

Chapter 1 is already a teaching chapter — one lane, Defile terrain, Skirmisher and Brood only. Its job is to introduce one thing at a time, and a formula that starts at full volume cannot do that. **Wave 1 is three Skirmishers.**

**The raider HP constant survives, re-anchored.** Wave 7 is the first formula wave: budget 126, about thirteen Skirmishers, 6,327 hit points at `50 × cost`, requiring 253 DPS across a 25-second single-lane wave against a supply of 350. That is a ratio of 1.38 — comfortable, correctly, for the wave immediately after the tutorial ends.

Nothing else in the ratio tables moves; wave 1 was the only row derived from the old anchor and it is no longer a formula wave.

---

## 6. What This Changes Elsewhere

| Document | Change |
|---|---|
| Economy model §5, §7 | Sixth module priced; arc restated at 24.7 months |
| Gene Vault spec §2, §5 | Six modules, not five; guardrail amended per §3 |
| Bible §1.2, §1.6 | Carapace → Plating |
| Bible §1.3 | Coverage restated as percentages |
| Campaign structure | Chapter 1 authored, not generated; lane and duration schedules added |
| Species stats §7 | Cost prices total effective HP; anchor moves to wave 7 |
| Bastion economy check §5, §6 | PvE only; the PvP open item closes |
| This session's eight documents | Habitat → Hatchery, Splice Chamber → Splicing Chamber |

---

## 7. Still Open

**1. Skittish repositioning mid-wave** versus the end-of-wave adjacency that Plating II and Regrow II use. Small, but a rules question if adjacency traits multiply.

**2. Whether the Gene Vault module should be renamed** to end the module-versus-screen collision with Sample Store. Sample Capacity is the obvious candidate and it is dull enough to be safe.

**3. The economy model's other derived figures.** If Core-plus-four was wrong in §5, anything downstream that used the 1.4M total or the 21.5-month arc needs checking — the payer ceiling and the Geneticist Tier pacing both plausibly reference it.
