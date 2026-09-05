# Broodline — The Terminal Sink

*Resolves economy model §8 and open question 5. Amends the Calibration recommendation rather than replacing it.*

---

## 1. Retraction First

I suggested last session that the terminal sink should be sample-side — fusing costs, catalysts, Roulette. That was wrong and the reason is worth recording, because it is a rule about what a terminal sink can be.

**Coverage saturates.** A maxed player holds tier III on every trait they need. Tier III is the ceiling, and generation caps the climb to it. Sample sinks therefore have a terminus, and a sink with a terminus is not a terminal sink — it is another finite track that runs out eighteen months later than the Vault does.

Anything that buys a bounded thing fails the same way. The terminal sink has to buy something with no ceiling, and the only things in the game with no ceiling are rate and vanity.

---

## 2. Calibration Is the Right Answer

Economy model §8 already proposes it: post-T12, each module accepts repeatable refinements at a flat 50,000 shards for +0.5% to its effect, uncapped. The reasoning for shipping it at launch is also correct — retrofitting a terminal sink into a live economy means inflating prices on players who already paid, or adding a currency, and both are worse.

The mechanism stays. Three things about it do not survive contact with the other specs.

---

## 3. Three Defects

### 3.1 Applied to the Harvest Array, it compounds and breaks the 6× spread

Calibrating income produces income, which buys Calibration. The economy model's own spread guardrail is the first casualty.

At flat 50,000 with 70% of post-max income going to Calibration:

| | 1 year | 3 years |
|---|---|---|
| Casual (760/day) | +1% | +2% |
| Core (3,570/day) | +9% | +27% |
| Optimiser (6,320/day) | +16% | +48% |

The free-play spread is already 1 : 4.7 : 8.3 and the model instructs holding it at 6×. A mechanism that adds 48% to the top and 2% to the bottom pushes the wrong direction, permanently, with no lever to pull it back.

### 3.2 Flat 50,000 does not hold

Open question 5 anticipates this and it is confirmed by the same table. A flat price against income that grows through Array scaling means the sink loosens every year — exactly the opposite of what a terminal sink is for.

### 3.3 Most modules cannot legally be Calibrated at all

"Each module accepts refinements" does not survive the guardrails:

| Module | Calibrating it | Verdict |
|---|---|---|
| **Harvest Array** | More income | Breaks the spread — §3.1 |
| **Core** | Core's effect is a cap on other modules | Meaningless; a cap on caps |
| **Drive** | Faster transit, smaller raid exposure window | Defensive advantage bought with shards. Already flagged as a risk in collectors §15 Q3 |
| **Splice Chamber** | Two effects. The generation cap is the coverage ceiling; charge regeneration is throughput | Cap **forbidden** — that is reach. Charge regen **legal** |
| **Habitat** | Roster capacity | **Legal** — the guardrail forbids roster slots *beyond* Habitat, which makes Habitat the sanctioned source |

Three of five modules fail outright, and a fourth is legal only in half of what it does. The recommendation as written implies five targets and has two.

---

## 4. The Fix

**Calibration applies to exactly two effects.**

| Target | Effect per Calibration |
|---|---|
| **Splice Chamber — charge regeneration** | −0.5% regeneration time |
| **Habitat — capacity** | +1 roster slot per 4 Calibrations |

Never the Harvest Array. Never the Core. Never the Drive. Never the Splice Chamber's generation cap, which stays Core-gated because it is the coverage ceiling and therefore reach.

Habitat is expressed in whole slots rather than a percentage because it has to be. Roster capacity tops out near 60, and +0.5% of 60 rounds to nothing — a percentage there is a purchase that visibly does zero. One slot per four Calibrations is the same rate expressed in a unit the player can see.

**Cost rises 3% per Calibration, from a 50,000 base.** Calibration 1 costs 50,000, Calibration 50 costs about 214,000, Calibration 100 about 940,000.

### Why the Splice Chamber is safe

Faster charge regeneration means more splices, and more splices means more mutation rolls — and mutation is the only entry point for Apex and Aberrant traits. That looks like buying power until constraint 8 is applied: **no Aberrant trait may ever counter a raider.** They are pure upside by construction, unbuyable and unplannable. More rolls buy chase items, cosmetic distinction and Instinct variety. They cannot buy a counter, at any spend, ever.

More splices also mean more sample consumption and more base stock demand, which sends the highest-invested players back to the map. That is the specific thing §8 says the game loses when the Vault maxes out.

### What the curve produces

At 60% of post-max income going to Calibration, per the budget split in `broodline_cosmetic_economy.md` §2:

| | 1 year | 3 years |
|---|---|---|
| Core | +6.5% (13 Calibrations) | +14.5% (29) |
| Optimiser | +10.0% (20) | +21.0% (42) |

The top-to-bottom gap narrows from 1.8× at flat pricing to 1.45×, and neither figure touches income, so the free-play spread guardrail is untouched by construction rather than by tuning.

In practical terms, three years buys a Core player about 29 refinements — enough to take charge regeneration from 25 minutes to 21, or add seven roster slots, or split the difference. Meaningful, visible in daily play, and nowhere near a power spike.

---

## 5. Calibration Has to Be Visible

+0.5% is not a purchase anyone makes for the +0.5%. It is a purchase people make because the total is legible to other people.

**Calibration count appears on the player profile, in the alliance roster, and in the replay header.** One number, cumulative across both targets, no tiers or titles. This costs nothing to build and it is the entire reason the sink will actually absorb income rather than sitting unbought while shards pile up.

This is also the honest framing: the terminal sink is a prestige track wearing a rate buff. Treating it as prestige from the start produces better decisions than discovering it in year two.

---

## 6. Remaining Open

**1. The alliance treasury cap.** A maxed player capped at 600/day to the treasury is the anti-monopoly rule doing its job, but it also blocks the most socially valuable thing a post-max player could spend on. Worth revisiting as a separate patron channel funding territory upkeep and alliance cosmetics rather than tech — but that is a new system and it should not be bundled into this decision.

**2. Whether 3% growth is right.** It holds for three years against current income projections. It has not been checked against Calibration's own effect on throughput, which is small but non-zero.

**3. Cosmetics as the second leg.** Seasons already ship cosmetics and they are genuinely unbounded. They are not modelled anywhere as a share of post-max spend, and they probably should be — a player with nothing to buy is the same problem whether the missing thing is a rate buff or a skin.

---

*Downstream: economy model §8 replaced by §4 above, §11 question 5 closed, guardrail list gains "Calibration never applies to income."*
