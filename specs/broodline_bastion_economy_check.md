# Broodline — Bastion Economy Check

*Runs the Bastion against the economy model's sink table. Corrects `broodline_wave_scaling_addenda.md` §5.3–§5.4.*

**Result: the Bastion should not be a purchasable module.** It should be free and equal to Core tier. The scaling itself, the +20% per level and the ratio table in the addenda §6, all stand unchanged — only the acquisition method is wrong.

---

## 1. The Check

Economy model §5 sets the Vault at Core plus four modules, each module costing 35% of Core's price at the same tier.

| | Total |
|---|---|
| Core, 12 tiers | 584,450 |
| One module at 35% | 204,558 |
| **Vault as specced** | **1,402,680** |

At a Core player's 3,570/day with 60% going to the Vault, that is **21.5 months *(corrected to 24.7 — see `broodline_four_decisions.md` §2)*** — the arc the economy model is built around.

### Finding one: the addenda's pricing breaks the arc

The addenda priced the Bastion at base 600 × 1.70 per level, totalling **498,533**. That is **85% of Core's entire cost** and roughly two and a half times what any other module costs.

| Vault | Total | Arc |
|---|---|---|
| As specced | 1,402,680 | 21.5 months *(corrected to 24.7 — see `broodline_four_decisions.md` §2)* |
| With the Bastion at 499k | 1,901,213 | **29.2 months** |
| With the Bastion at standard 35% | 1,607,238 | 24.7 months |

Eight months added to a twenty-one month arc is not a tuning error, it is a different game. Even repricing to module standard adds three.

### Finding two: at module pricing the Bastion dominates the Harvest Array

Priced like every other module, the Bastion and the Array cost exactly the same at every level. They deliver:

| | Level 8 | Level 12 |
|---|---|---|
| Harvest Array | 2.00× income | 3.00× income |
| Bastion | 3.58× damage | 7.43× damage |

The Array's late tiers are already a slow payback — going from tier 8 to tier 12 costs 184,800 and adds about 1,300/day to a Core player, which takes 142 days to recover. The Bastion at the same price is mandatory for campaign progress.

No rational player buys the Array above tier 8 while the Bastion is unbought. The design would have shipped a module that exists to be skipped.

---

## 2. The Actual Problem

Both findings are symptoms. The real error is a category one:

> **The Bastion is mandatory. Mandatory things must not be purchasable.**

Every other Vault module buys *rate*. A player with a tier-4 Array earns less per hour than one at tier 12 and reaches everything the same, slower. That is the economy model's founding principle and the reason the payer ceiling holds.

The Bastion buys *reach*. Addenda §6 shows waves 20, 30 and 45 sitting at ratios of 1.08, 1.05 and 1.00 with the Bastion at its cap. A player who spent those shards on the Array instead does not progress slowly — they stop. That is a soft lockout produced by a purchase decision, and it is the exact failure the design forbids in four separate places.

It also quietly reintroduces the thing the whole counter model exists to prevent. A player who cannot clear wave 45 can buy shards, buy Bastion levels, and clear it. The wall would have a price.

---

## 3. Correction

**The Bastion is not a module. It is not purchasable, not built, not timed, and not a screen.**

> **Bastion level equals the player's current Core tier. It is granted with the tier, at no cost.**

Everything else in the addenda holds: twelve levels, +20% damage per level above the first, 7.43× at level 12, damage only, applies everywhere a creature deals damage, never grants a trait or coverage.

### Why the addenda's objection was wrong

Addenda §5.3 argued the Bastion could not track Core tier because tier N is granted by the wave that needed it — tier 8 arrives at wave 45, so wave 40 is fought at tier 7.

That observation is true and the conclusion drawn from it was not. **The ratio table in addenda §6 already assumes the tier-already-earned value at every wave** — wave 40 at Bastion 7, wave 45 at Bastion 7, wave 65 at Bastion 11. The 1.20 growth rate was tuned against that lag. The lag is priced in.

Purchasability was solving a problem that the growth rate had already solved. It was never doing work.

### The ratio table is unchanged

| Wave | Lanes | Bastion | Required DPS | Supply | Ratio |
|---|---|---|---|---|---|
| 1 | 1 | 1 | 200 | 350 | 1.75 |
| 20 | 2 | 3 | 468 | 504 | 1.08 |
| 30 | 2 | 5 | 693 | 726 | 1.05 |
| 40 | 2 | 7 | 931 | 1,045 | 1.12 |
| 45 | 3 | 7 | 1,041 | 1,045 | 1.00 |
| 50 | 3 | 8 | 1,207 | 1,254 | 1.04 |
| 65 | 4 | 11 | 1,847 | 2,167 | 1.17 |

---

## 4. What This Buys

**The economy is untouched.** The Vault stays at 1,402,680 and the arc stays at 21.5 months *(corrected to 24.7 — see `broodline_four_decisions.md` §2)*. No new sink, no Array conflict, no repricing of anything.

**The mutual staircase gets stronger.** Core tier now visibly does three things: it caps every module, it caps generation through the Splicing Chamber, and it sets combat output. All three are behind a campaign milestone, and campaign milestones cannot be bought. The load-bearing fact of the economy carries more weight without becoming more complicated.

**One screen fewer.** The Gene Lab keeps its six facilities — Splicing Chamber, Hatchery, Gene Vault, Harvest Array, Drive and Core. The Bastion value belongs on the Core module's readout — one line, alongside the tier and its milestone requirement, stating what the current tier is worth in combat. That is information the Core screen should have been showing anyway; nothing in the set currently tells a player what a Core tier does for them in a fight.

**Difficulty tuning gains a lever.** Because Bastion tracks Core tier and Core tier tracks the milestone map, moving a milestone wave now moves the difficulty curve. Wave 45 sits at 1.00 because Core 8 lands at wave 45; pulling that milestone to wave 43 loosens the hardest fight in the game without touching a single combat number.

---

## 5. What This Costs

**The endgame sink claim was wrong.** The addenda argued Bastion levels 10–12 would become the shard sink the Vault lacks after Core 12. That is retracted with the rest. Economy model §8's terminal sink problem is still open and the Bastion is not its answer.

**Free scaling has no agency in it.** A player never decides anything about the Bastion. That is correct for a mandatory system, but it means the only combat-power decision in the game remains composition — which is the design's intent and worth stating rather than discovering later.

---

## 6. Remaining Open

**1. The terminal sink.** Untouched by this and now one candidate poorer. Economy model §8.

**2. Whether Core tier should also be visible as a combat number in PvP.** Raid matchmaking bands at ±2 Core tier, which is now also ±2 Bastion levels — a 1.44× damage spread inside a legal match. That is defensible, but a defender should probably be able to see an attacker's Bastion level in the replay, or losses will read as inexplicable.

**3. Milestone map as a difficulty dial.** §4 notes this is now possible. It is also now a coupling: nobody can move a Core milestone for economy reasons without re-running the ratio table.

---

*Downstream: addenda §5.3–§5.4 retracted, Vault spec unchanged, screen inventory unchanged, Core module screen gains a line.*
