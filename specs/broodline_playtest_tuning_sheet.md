---
status: current
folder: 01-companions
note: >
  Every number in the design that is an assumption rather than a
  measurement, with a starting value and the condition that says it was
  wrong.
---

# Broodline — Playtest Tuning Sheet

*Every number in the design that is an assumption rather than a measurement, in one place, with a starting value and a condition that says it was wrong.*

Sources: bible, economy model, campaign structure, collectors and raiding, and the six documents produced in this pass — utility traits, species stats, wave scaling audit, wave scaling addenda, Bastion economy check, terminal sink, cosmetic economy.

**How to read this.** Section 1 must be filled in before a build can run at all — these have no value yet. Section 2 has values and the test either confirms them or moves them. Section 3 is what to instrument. Section 4 is the small set of numbers whose failure invalidates the design rather than the tuning.

---

## 1. Unset — Blocks the Build

Nine values with no number. Nothing can be tested until someone picks one for each, and a wrong starting guess costs far less than a missing one.

| Value | Where it bites | Recommended start |
|---|---|---|
| **Sample drop rate per source** | Coverage income, the entire fusing economy | 3 per campaign wave, 1 per replay, scaling with wave number |
| **Catalyst frequency per Apex Vein** | Aberrant supply, the only chase item | 1 in 3 Apex captures |
| **Habitat capacity curve** | Roster cap. Currently a placeholder 20→60 | Linear across 12 Core tiers |
| **Instinct source weighting, gen-1 stock** | Species identity vs breeding reachability | 40% signature, 12% each of the other five |
| **Ember splash radius** | Defines what Splash coverage means in practice | 1.5 tiles |
| **Convoy interception grace period** | Day 14 by default, never confirmed | Confirm 14, step down over a week |
| **Regeneration duration range** | Combat spec says 20–60 min, no rule for which | 20 min base, +10 per creature lost, cap 60 |
| **Splice Roulette pool size** | Always-on with a rotating pool, size unspecified | 8 traits, rotating weekly |
| **Season length** | **Contradiction, not a gap** — see below | Four weeks |

**On season length.** The README logs this as open at six or eight weeks. The monetization spec and the live-ops calendar both specify four-week Season Pass cycles, and `broodline_cosmetic_economy.md` sizes the catalogue at thirteen four-week seasons a year. Three documents say four weeks and one says the question is open. Resolve it as four and delete the open item.

---

## 2. Under Test

Values with a number. The playtest confirms or moves them.

### 2.1 Economy

| Value | Start | Kill criterion |
|---|---|---|
| Common Vein yield | 40/hr | Every price in the set moves with it. Set against session length, not in advance |
| Offline accrual cap | 12 hr | Drop below 12 and overnight play is taxed |
| Free-play income spread | ≤6× | Modelled at 8.3×. If measured spread exceeds 6×, cut Apex amortisation first |
| Apex capture rate | 30% of theoretical | Above 40% and the top archetype detaches from the economy |
| Splice charge regen / cap | 25 min / 5 | Below 12 splices a day for a Core player and mutation cadence breaks |
| Mutation rate | 3% per splice | Rolled once per splice, not per slot |
| Aberrant sub-roll | 5%, 50% with catalyst | Aberrants appearing in under 10% of rosters by month 3 means the chase item is invisible |
| Regen skip sink | 25% of Core income | Above 30% means the combat curve is too hard, not the price too high |
| Cargo loss cap | 40% | One of the three most fragile numbers. Any rise reads as pay-to-not-lose pressure |
| Attacker share of take | 50% | Raises raid income directly; check against the spread |
| Raids per player per day | 2 | 3 pushes a raider to ~600/day and breaks the spread |
| Calibration base / growth | 50,000 / +3% each | Flat pricing gave Optimiser +48% vs Casual +2% over 3 years |
| Cosmetic share of income | 15% post-max | Core should clear ~4 of 5 season items |
| Season catalogue | 16,500 shards, 5 items | If Core clears all five easily, the catalogue is underpriced |

### 2.2 Combat structure

| Value | Start | Kill criterion |
|---|---|---|
| Baseline creature | 1,000 HP, 100 dmg, 1.0 s, 4 tiles | Reference point; no species is exactly 1.0× |
| Species stat lines | Six lines, `broodline_species_stats.md` §4 | See roster diversity, §3.1 below |
| Raider HP | 50 × budget cost, covering splits and shields | Wave 1 should clear with ~40% time headroom |
| Wave budget | 100 × 1.04^(w-1) × L, knee to 1.03 at wave 31 | If wave 45 is unclearable for the median player, move the knee earlier before touching anything else |
| Lane schedule | 1 / 2 / 3 / 4 at waves 1 / 15 / 41 / 59 | Lane increases are the chapter walls by design |
| Wave durations | 25 / 45 / 70 / 95 s by lane count | 95 s is long for mobile. If chapter 8 drags, cut to three lanes rather than shortening |
| Bastion | +20% damage per Core tier, free | If waves 20, 30 and 45 clear comfortably, this is too high |
| Deployment cap | 5 creatures | Worth testing 4 and 6 |
| Rally | 4 s double speed, 30 s cooldown, ~10% of outcome | Above 15% and absent players are punished |

### 2.3 Coverage and traits

| Value | Start | Kill criterion |
|---|---|---|
| Coverage tiers | I 25%, II 40%, III 60%, rounded up | Two carriers must fully cover one raider type. If three are needed, the four-type cap is wrong |
| Four-type cap per wave | 4 | Derived from ten trait slots; changes if the deployment cap moves |
| Plating | −40% damage taken | Below 30% and Vetch is a Taunt delivery vehicle again |
| Regrow | −50% regen time, 1% max HP/s after 3 s | Measured against regen-skip spend for Loam holders vs not |
| Litter | 1 / 2 / 3 samples on splice consumption | Above 70% of splices using a Litter parent means it is a tax, not a choice |
| Screen | 2 s target acquisition delay | If back-line compositions stay rare, it is too weak |
| Broodline Affinity | +5%, one per creature | The only permanent stat modifier in the game |

### 2.4 Progression

| Value | Start | Kill criterion |
|---|---|---|
| Campaign length | 65 waves, 8 chapters | Confirmed. Do not reopen without re-running the ratio table |
| Core milestone map | 12 tiers, waves 3–65 | Now also the difficulty dial — moving a milestone moves the Bastion |
| Fastest possible completion | ~9 months | Under 6 months and the payer ceiling collapses |
| Full Vault | ~21.5 months at Core rate | The Bastion must add nothing to this |
| Lineage depth | 5 generations | Storage, Lineage View layout, Affinity window |
| Raid immunity | 14 days, stepping down over a week | Watch the D14 retention cliff |

---

## 3. Instrument

### 3.1 The primary metric

**Species share of deployments at wave 40.** Wave 40 is the last two-lane wave and the point where composition stops being budget-driven.

- **Healthy:** no species below 10% of deployments
- **Failing:** any species below 10% — its utility trait is at fault, not its counter, because the counter is mandatory by construction
- **Diagnostic:** Ember and Hollow together above 50% means the utility traits collectively are too weak

**Instrument deployments, not collection.** A player who holds all six species and deploys four is the exact failure mode, and acquisition metrics will not show it.

### 3.2 Combat

| Metric | Why |
|---|---|
| Clear rate and attempts-to-clear at waves 20, 30, 45 | The three tightest ratios in the campaign. Wave 45 sits at exactly 1.00 |
| Composition at first clear of wave 45 | Should show two utility traits in the spare slots. If it does not, the ratio table is wrong |
| Regen skip shards as share of income | Doubles as the combat difficulty read |
| Share of splices with a Litter parent | Target 20–70% |
| Share of deployments with a ranged creature behind the front rank | Screen's read |
| Rally usage rate and its measured effect on outcome | Target ~10% |

### 3.3 Retention and first session

| Metric | Why |
|---|---|
| Session-one completion, with drop-off by beat | The FTUE either works or it does not, and beat-level data says where |
| Time to first splice | Target under 4 minutes |
| First-loss recovery rate | Whether the Wave Defeat screen actually teaches the counter |
| Founder naming rate | Custom metric. Whether the emotional hook lands |
| Lineage View revisits, week 1 | Custom metric. Whether the broodline idea is felt or just described |
| D1 / D3 / D7 / D14 | D14 specifically against the raid immunity step-down |
| Trial acceptance and conversion at expiry | The main monetization read |

### 3.4 Economy

| Metric | Why |
|---|---|
| Measured free-play income spread | Against the 6× guardrail |
| Actual Apex capture rate | The 30% assumption carries the entire top end |
| Splices per day by archetype | Against the 15–18 the charge model predicts |
| Cosmetic purchase rate per season by archetype | Against 4-of-5 for Core |

---

## 4. What Failure Means

Most of section 2 is tuning. Six of them are not — if these fail, the design is wrong rather than mistuned, and the fix is structural.

| If this happens | The problem is |
|---|---|
| Any species below 10% of wave-40 deployments | Utility traits. Roster collapse is the failure the whole trait system exists to prevent |
| Two carriers cannot fully cover one raider type | The proportional coverage numbers, and the four-type cap with them |
| Wave 45 unclearable for the median player after five attempts | The budget knee, not the Bastion. Move the knee before touching combat |
| Measured spread above 6× | Apex amortisation. Cap Apex yield per player per week rather than tuning capture rate |
| Regen skips above 30% of income | The combat curve is too hard. Do not reprice the skip |
| Players buying a counter, in any form, through any path | Stop. This is the one thing that cannot be tuned around |

The last row is the only one with no fallback. Everything else in this document is a number; that one is the design.

---

## 5. Sequencing

Three of these cannot be measured simultaneously and the order matters.

**First build — combat only.** Section 3.1 and 3.2, campaign waves 1 through 45, no economy, no raids, unlimited splice charges. Roster diversity and the wave-45 ratio are the two findings that invalidate the most downstream work, and both are measurable without a single economic number being correct.

**Second build — economy.** Sections 3.4 and the section 2.1 values. Charge regen and sample drop rates cannot be read while charges are unlimited.

**Third build — retention.** Section 3.3. Meaningless before the first two are stable, because early drop-off caused by a broken difficulty curve is indistinguishable from drop-off caused by a bad FTUE.

The nine unset values in section 1 are needed for the first build regardless of which section they belong to. Set them all before anything runs.

---

*This document supersedes the scattered open-question lists in the economy model §11, combat system §12, collectors §15 and the README's not-done section, for anything that is a number. Design questions in those documents that are not numbers remain where they are.*
