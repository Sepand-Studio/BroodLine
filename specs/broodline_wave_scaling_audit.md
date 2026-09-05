# Broodline — Wave Scaling Audit

> **Partly superseded.** §2's diagnosis stands and its fix is now the design.
> Every table in §2 and §3 uses a lane schedule of three lanes from wave 40,
> which `broodline_wave_scaling_addenda.md` §3 corrects to two lanes through
> wave 40 and four from wave 59. The tables are kept as the record of how the
> failure was found; the corrected figures are in the addenda §6 and in
> `broodline_bastion_economy_check.md` §3. §5's purchasable Bastion is
> retracted in full — the Bastion is free and equals Core tier.

*Findings document. Runs the wave budget against the species stat lines and reports what breaks.*

Inputs: `broodline_species_stats.md` §4 and §7, `broodline_utility_traits.md`, bible §1.3 and §4.5, the budget formula `100 × 1.04^(w-1) × L`, and the Core milestone map in `broodline_campaign_structure.md` §5.

**Verdict: the campaign is unwinnable from roughly wave 15 onward.** Two independent failures, both structural rather than tuning. Three fixes below; all three are needed.

---

## 1. Method

Species stat lines give a fixed defender: five creatures at roughly 70 effective DPS each, **350 DPS, flat for the entire campaign**, since §6 of the stats spec establishes that nothing modifies a creature's stats.

Species stats §7 gives the raider side: total enemy hit points in a wave equal `50 × budget`, regardless of composition. That collapses the whole raider roster into one number and makes the two sides directly comparable.

Wave duration is assumed at 20 s at wave 1, rising to 90 s late. Required sustained DPS is enemy HP over duration.

---

## 2. Finding One — Coverage Defined as a Count Breaks at the First Two-Lane Wave

Bible §1.3 defines coverage in absolute units: Splash I catches two of the eight Skirmishers a wave sends, Splash III catches five.

Raider counts are not fixed at eight. They follow the budget.

| Wave | Lanes | Budget | Bodies of one raider type at ¼ budget | Splash III carriers needed |
|---|---|---|---|---|
| 1 | 1 | 100 | 2 | 0.5 |
| 15 | 2 | 346 | 9 | 1.7 |
| 40 | 3 | 1,385 | 35 | 6.9 |
| 50 | 3 | 2,050 | 51 | 10.3 |
| 60 | 3 | 3,035 | 76 | 15.2 |

Five deployment slots carry ten trait slots, and a legal wave sends four raider types, so the ceiling is about **1.25 carriers per counter.** The requirement passes that at wave 15 — the first two-lane wave — and reaches fifteen carriers by wave 60 against a ceiling of one and a quarter.

Tier III becomes indistinguishable from tier I well before the midpoint, not because it stops working but because five of anything is a rounding error against seventy-six.

### Fix: coverage is a proportion, not a count

> **Splash I handles 25% of that raider type in the wave. Splash II handles 40%. Splash III handles 60%.**

The bible already wrote these numbers as fractions and mistook them for counts. Two of eight is 25%. Five of eight is 62.5%. **Restating §1.3 in percentages changes no wave-1 behaviour at all** — it changes every wave after it.

This does more than fix the arithmetic. It makes §4.5's four-type cap correct rather than a guess:

- Full coverage of one raider type needs 100%, so **two carriers per type** — a III and a II, or two IIIs with overlap
- Four types × two carriers = **eight trait slots**
- Five creatures × two combat traits = **ten trait slots**

Eight of ten, leaving two slots for utility. That is exactly the composition the design has been describing, and it now falls out of the numbers instead of being asserted.

It also keeps §1.3's central promise intact at every wave size: tier I is never useless, no raider is ever immune to it, and tier decides how much rather than whether.

---

## 3. Finding Two — Defender Output Is Flat Against a 37× Volume Curve

| Wave | Lanes | Budget | Enemy HP | × wave 1 | Required DPS | Supply | Ratio |
|---|---|---|---|---|---|---|---|
| 1 | 1 | 100 | 5,000 | 1.0 | 250 | 350 | 1.40 |
| 40 | 3 | 1,385 | 69,245 | 13.8 | 1,154 | 350 | 0.30 |
| 50 | 3 | 2,050 | 102,500 | 20.5 | 1,367 | 350 | 0.30 |
| 60 | 3 | 3,035 | 151,725 | 30.3 | 1,686 | 350 | 0.21 |
| 65 | 3 | 3,692 | 184,597 | 36.9 | 2,051 | 350 | 0.17 |

Nothing on the defender's side of that table moves. Generation grants nothing, growth grants nothing, traits grant nothing, Instinct grants nothing permanent. Broodline Affinity's +5% is the only stat modifier in the game.

Coverage cannot close a 5× gap either, even proportionally — proportional coverage decides *whether* a raider type dies, and something still has to deliver the damage.

**Something on the defender's side has to scale, and the only safe thing to scale is raw output.** Output scaling cannot create a damage floor, because an unanswered raider stays unanswered at any multiplier: Courser's threat is specified as damage that cannot kill it in time, and multiplying both sides preserves that exactly.

---

## 4. Finding Three — Core Tiers Cannot Be the Scaling Vehicle

The obvious home for output scaling is the Core tier track. It is already campaign-gated, already twelve steps, already the spine of the mutual staircase.

It does not work, for a reason worth stating plainly:

> **Core tier N is granted by the wave that needed it.**

Milestone map: tier 8 is granted at wave 45, so a player fighting wave 40 has tier 7. Tier 12 is granted at wave 65, so the final wave is fought at tier 11. Every rung arrives after the fight it was supposed to win.

Any scaling attached directly to campaign milestones has this defect. The scaling has to be something the player buys *between* waves.

---

## 5. The Fixes

### Fix 1 — Proportional coverage

Per §2. Rewrite bible §1.3 in percentages. Free, changes nothing at wave 1, fixes everything after.

### Fix 2 — The Bastion, a Vault module

A sixth module in the Gene Lab, alongside Core, Harvest Array, Habitat, Splice Chamber and Drive. *(Retracted — see the banner above.)*

- **Twelve levels. +18% damage to every deployed creature per level.** Level 12 is 4.4×.
- **Purchased with Gene Shards**, on the same steepening curve as the Array
- **Capped by Core tier**, exactly like every other module

The cap is what preserves the mutual staircase: a player cannot buy past the campaign, because Bastion cannot exceed Core and Core cannot exceed the milestone. But because the level itself is shard-purchased, a player sitting at Core 7 can hold Bastion 7 *during* wave 40 rather than being handed it at wave 45.

**The Bastion multiplies damage only.** It never grants a trait, never raises coverage, never touches HP. It is output and nothing else, which is what keeps it clear of the hard-counter guarantee.

### Fix 3 — A knee in the budget curve at wave 30

`1.04^64` is 12.2×. Compounding at four percent across sixty-five waves is the actual root cause of the gap, and no defender track fits an exponential that steep without becoming an exponential itself.

> **Budget growth drops from 1.04 to 1.03 per wave from wave 31 onward.**

Wave 65's budget falls from 3,692 to 2,633 — still 26× wave 1 across three lanes. Chapters 6 through 8 stop outrunning everything.

---

## 6. Result

Bastion level available *during* each wave, given the existing milestone map:

| Wave | Lanes | Bastion | Budget | Required DPS | Supply | Ratio |
|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 100 | 250 | 350 | 1.40 |
| 15 | 2 | 3 | 346 | 495 | 487 | 0.98 |
| 20 | 2 | 3 | 421 | 527 | 487 | 0.93 |
| 40 | 3 | 7 | 1,257 | 1,048 | 945 | 0.90 |
| 50 | 3 | 8 | 1,690 | 1,127 | 1,115 | 0.99 |
| 60 | 3 | 10 | 2,271 | 1,262 | 1,552 | 1.23 |
| 65 | 3 | 11 | 2,633 | 1,463 | 1,832 | 1.25 |

Two observations.

**The dips land on lane increases.** Waves 20 and 40 sit below parity because a lane was added and the next Bastion level has not been earned yet. That is the correct shape for a chapter gate — the wave after a terrain change should be the wall — and it is where the campaign should feel hardest.

**The shortfall at those dips is 7 to 10%, and that is exactly the size of the utility traits.** Plating III cutting incoming damage 40% keeps bodies firing for longer; Screen III buys the back line acquisition-free seconds; Rally is specified at roughly 10% of a wave's outcome; Broodline Affinity adds 5%. A composition carrying two utility traits in its spare slots clears wave 40. One carrying none does not.

That is a better outcome than a comfortable margin. It means the utility traits are load-bearing by arithmetic rather than by assertion, which is what §1.2 of the bible asked for and could not previously demonstrate.

---

## 7. Open Items

**1. Campaign length is still 65 vs 60.** The constants ledger says 65 and the milestone map above assumes it; a later chapter breakdown says 60 across seven chapters. Every number in §6 shifts if it is 60. This is one of the two items already logged as pending sign-off and it now blocks the wave tables.

**2. The lane schedule is assumed, not specified.** §6 assumes one lane through wave 14, two through 39, three from 40. Findings One and Two are both driven by lane count more than by wave number, so this needs writing down before anything is authored.

**3. Wave duration targets.** 20 s to 90 s is an assumption made here to produce a required-DPS figure. A 90-second wave is long for a mobile session and it is the cheapest lever available if the ratios need loosening — every ten seconds added to wave 65 is 10% off the requirement.

**4. Bastion pricing.** Twelve levels at +18% on a steepening shard curve needs costing against the economy model's daily faucets, and it is a new sink competing with Harvest Array for the same early shards.

---

*Downstream: bible §1.3 rewritten in percentages. The Vault and the economy model are untouched — see `broodline_bastion_economy_check.md`.*
