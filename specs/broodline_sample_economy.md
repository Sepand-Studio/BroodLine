# Broodline — The Sample Economy

*Design spec, coverage supply and the Aberrant supply line*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Every number here is a starting point for soft-launch tuning. **The structures
> are decisions; the values are not.**

**On the file split.** The gap register queued this as a section inside `broodline_economy_model.md`. It is a separate document because that one owns Gene Shards and this one owns samples, and the last round of drift in this project came from two unrelated things sharing a name and a page. The economy model now points here.

---

## 1. What this document owns

Bible §7.6 states the structure of the sample economy and says outright that every number in it is open. Five things were waiting on those numbers: the campaign rewrite cannot set milestone rewards without drop rates, the Geneticist Tier 6 perk grants "a sample pull" that nothing defines, `broodline_combat_numbers.md` leaves the Aberrant sub-roll open at its §11, the Gene Vault capacity valve has two data points and no curve, and constraint 3 has never been audited.

It also corrects a figure that four documents inherited from each other and none of them checked. See §8.

**The one-line summary:** samples are abundant, the generation ceiling and roster churn are what actually gate coverage, and the Gene Vault capacity valve is a pressure mechanic rather than a scarcity one.

---

## 2. Denomination

**A sample is specific to one trait and one tier.** A tier-II Chill sample is a distinct object from a tier-I Chill sample and from a tier-II Reach sample.

- Twelve species traits × three tiers = **36 sample types**
- No Instinct samples, no Aberrant samples — bible §1.7 and §1.6
- The Sample Store shows them as stacks; **capacity counts individual samples, not stacks**

Counting individuals rather than stacks is what makes the valve bite. Counting stacks would mean a player holding thirty Chill samples occupies one slot, and the pressure to fuse evaporates.

**Applying a sample is instant, free, and irreversible.** A tier-II Chill sample applied to a creature already carrying Chill raises that creature's Chill to tier II. It cannot be applied to a creature that does not carry Chill — that would be access, which per bible §1.7 comes only from breeding.

---

## 3. Where samples come from

Daily rates by archetype, using the same three players as `broodline_economy_model.md` §3.

| Source | Casual | Core | Optimiser |
|---|---|---|---|
| Campaign and replay waves | 1.5 | 3.0 | 4.0 |
| Region defence | 0.5 | 1.0 | 1.5 |
| Node harvesting | 1.0 | 3.0 | 5.0 |
| Campaign milestones (amortised) | 0.2 | 0.4 | 0.6 |
| Splice Roulette (amortised) | — | 0.6 | 1.2 |
| Retirement (amortised) | 0.2 | 0.8 | 1.2 |
| **Daily total** | **~3.4** | **~8.8** | **~13.5** |

**Spread is 1 : 2.6 : 4.0.** That is deliberately tighter than the shard spread of 1 : 4.7 : 8.3, because shards buy throughput and samples buy coverage. A wide throughput gap is survivable; a wide coverage gap means the heaviest player's creatures answer four Coursers while the lightest player's answer one.

> **Guardrail: sample income spread never exceeds 5×.** It is the coverage-side equivalent of the economy model's 6× rule and it is the tighter of the two on purpose.

### 3.1 Tier mix by source

What tier a sample arrives at, by where it came from.

| Source | Tier I | Tier II | Tier III |
|---|---|---|---|
| Campaign and replay waves | 100% | — | — |
| Region defence | Mirrors the richest node in the region | | |
| Common Vein | 92% | 8% | — |
| Rich Deposit | 70% | 27% | 3% |
| Apex Vein | 40% | 45% | 15% |
| Campaign milestone | — | 100% | — |
| Splice Roulette / sample pull | 60% | 32% | 8% |
| Retirement | Returns exactly what was fused in | | |

**Waves drip tier I and nothing else.** That is what makes the guarantee at bible §8.2 real — a player at zero charges replaying cleared waves is always making progress, and the progress is the lowest-value kind, which is correct.

**Node tier is the whole reason to move.** An Apex Vein is not eight times more samples; it is samples at a tier the map's safe ground never produces. Bible §5.8's pressure curve is expressed here as much as in shard yield.

### 3.2 Weighting

Bible §7.6: harvest drops are weighted toward traits the player's roster already carries, because samples for traits nobody holds are dead inventory.

> **Harvest drops: 75% roll from traits present in the roster, 25% roll uniformly across all twelve.**
>
> **Campaign, milestone, region defence and Roulette drops are unweighted.**

The 25% unweighted portion is not waste. It is the stockpile a player draws on the week they finally breed a Hollow line, and it is why acquiring a sixth species feels like unlocking something rather than starting from zero. Keeping campaign and event sources unweighted is what lets a player build deliberately toward something new, which bible §7.6 requires.

**Weighting reads from the roster, not from deployment.** A garrisoned or escorting creature still counts. Otherwise a player who garrisons their only Chill carrier stops receiving Chill samples, which is the wrong lesson at the wrong moment.

---

## 4. Fusing

**Three samples of a tier fuse into one of the next.** Permanent, instant, no timer, no builder, and **no shard cost.**

| To reach | Costs | In tier-I equivalents |
|---|---|---|
| Tier II | 3 × tier I | 3 |
| Tier III | 3 × tier II | 9 |

A shard cost on fusing was considered and rejected. It would make coverage progress stall for a shard-poor player who is otherwise doing everything right, and it would put a second currency between the player and the one action the Sample Store exists to encourage. Shards already have the largest sink in the game at the Gene Lab; they do not need this one.

**Fusing is the sink that makes capacity matter, and capacity is what makes fusing urgent.** Those two sentences are the entire design of this screen.

---

## 5. Capacity

Governed by the Gene Vault facility. **At 88% the display turns and warns; above 100%, new samples from waves and harvest are discarded.**

| Gene Vault tier | Capacity |
|---|---|
| 1 | 20 |
| 2 | 23 |
| 3 | 26 |
| 4 | 29 |
| 5 | 32 |
| 6 | 35 |
| 7 | 39 |
| 8 | 43 |
| 9 | 47 |
| 10 | 51 |
| 11 | 55 |
| 12 | 60 |

**Campaign milestone and Roulette samples are never discarded**, even above capacity. A discarded milestone reward is a reward the player earned and did not receive, which reads as a bug regardless of how well the valve is explained. Wave and harvest drops are the ambient flow and are the right thing to lose.

**The valve is safe because samples are coverage, never access.** A discard costs recoverable progress and can never strand a player without an answer to a raider. This was flagged as dangerous in an early reconciliation draft and the flag was withdrawn once the access/coverage split settled; the reasoning is worth keeping visible, because it is the only thing making a punitive-looking mechanic acceptable.

**At 20 capacity and 3.4 samples a day, a new player fills the Vault in six days.** That is the intended first encounter with the Sample Store — early enough to teach fusing, late enough that they know what a trait is.

---

## 6. Retirement

**Retiring a creature returns exactly the samples that were fused into it.** A creature carrying Chill III and Taunt I returns one Chill III sample and nothing for the Taunt.

Tier I is innate to the trait and returns nothing, because tier I was never bought — it came with access, and access came from breeding.

This makes retirement a genuine alternative to splicing rather than a consolation prize. Splicing a creature with two tier-III traits carries one of them forward and destroys the other's nine tier-I equivalents. Retiring it recovers both. **The choice is a child versus eighteen tier-I equivalents**, which is a real decision and exactly the kind the design wants at the roster screen.

---

## 7. What a splice costs in coverage

Not stated anywhere before now, and it is the largest sink in this document.

Per bible §2.2 a splice consumes two parents carrying four combat traits between them, and the child keeps two. **Coverage travels with the trait.** The two traits that do not carry take their fused coverage with them and it is gone.

- The **locked** combat trait carries at its parent's full coverage
- The **rolled** combat trait carries at full coverage if dominant, **one tier lower if recessive**
- The two traits that do not carry return nothing

A recessive downtier is recoverable by re-fusing, which is why bible §2.2 can call it safe. The two non-carrying traits are not recoverable, which is why retirement exists.

> **Screen requirement.** The splice confirmation must state which coverage will not carry, by name and tier — "Chill III will not carry." A player who discovers after the fact that they destroyed nine tier-I equivalents will read it as the game hiding a cost, and bible §2.7 already commits to stating costs before they happen. This belongs in `broodline_splice_confirm_spec.md` §3 alongside the destruction notice.

---

## 8. A correction: splices per day

Four documents state that a three-session player captures 15–18 charges a day and therefore performs 15–18 splices. The charge arithmetic is right. **The conclusion is not, because a splice consumes two creatures.**

Fifteen splices a day requires **thirty base-stock creatures a day**, against a roster floor of 20. No supply model in the set produces that, and none plausibly could.

> **Charges are not the binding constraint on splicing. Base stock is.**

This is not a flaw — bible §2.1 says so explicitly: consumption "means the player always needs more base stock, which is what makes harvesting worth doing, which is what makes relocating the Ark worth doing." The arithmetic simply had not been run against the other side of it.

**Working figures**, pending the base-stock supply table:

| | Base stock/day | Splices/day |
|---|---|---|
| Casual | ~4 | ~2 |
| Core | ~12 | ~6 |
| Optimiser | ~20 | ~10 |

**Three downstream corrections follow:**

1. **Mutation cadence.** At 6 splices a day and a 9% rate, a core player sees a mutation every **1.9 days**, not every 0.7. Still the right cadence, and closer to the original design intent than the charge-derived figure was.
2. **Charge surplus is real and intended.** A core player banks charges they cannot spend, which is what makes the Geneticist Tier 8 second splice queue valuable only to players who have solved base stock — a good shape for the game's strongest perk.
3. **Litter is more important than it looks.** `broodline_combat_numbers.md` §4.3 gives Litter III one Gen-1 creature every four hours — six a day, against a core player's twelve. A single Litter III carrier is half a core player's entire base stock supply. **That may be too strong and is the first thing to check** alongside whether Litter carriers get deployed at all.

The economy model and monetization spec both carry the 15–18 splices figure and are corrected to point here.

---

## 9. The catalyst and the Aberrant sub-roll

The only supply line for the one thing money cannot buy.

| | Value |
|---|---|
| Base mutation rate | **9%** per splice (bible §2.3) |
| **Aberrant sub-roll** | **5%** of mutations |
| Catalyst | Raises the sub-roll to **50%** for the next **three** splices |
| Catalyst source | **One per successful Apex Vein extraction.** Nothing else |
| Mutation Surge | Raises the base sub-roll to **20%** for seven days |

**What that produces:**

| | Aberrant every |
|---|---|
| Casual, no catalysts | ~9 months |
| Core, occasional Apex participation | ~5 weeks |
| Optimiser, regular Apex participation | ~2 weeks |
| One catalyst, on its own | 13.5% chance across three splices |

A catalyst is a lottery ticket with roughly one-in-seven odds. That is the right weight for something an alliance contests a 48-hour window over — meaningful enough to fight for, uncertain enough that winning the Vein is not winning the Aberrant.

**The casual figure is the one to watch.** Nine months to a first Aberrant is close to never, and the Aberrant is the game's most distinctive object. The lever is Mutation Surge frequency rather than the base sub-roll: four weeks a year of 20% odds is a thin supply line, and `broodline_live_ops_events.md` §11 already flags it. **Recommend Mutation Surge moves from quarterly to six times a year** before the base rate is touched, because the base rate is what keeps Aberrants rare for everyone equally.

**Eight Aberrants exist** (`broodline_combat_numbers.md` §4.4). At a core player's five-week cadence with duplicates possible, a full set is a multi-year pursuit that no amount of money shortens. That is the correct shape for the game's completion goal.

---

## 10. What a sample pull is

Closes the last open question on the Geneticist Tier ladder.

> **A sample pull is one Splice Roulette spin, granted rather than purchased.**
> Same odds table, same unweighted roll across all twelve traits, same visible pity counter.

Sixty percent tier I, 32% tier II, 8% tier III, with a tier-III sample guaranteed within twenty spins.

Defining it as the same object does three things. It means one probability-table component rather than two, and that component is regulator-facing so building it once and correctly matters. It means the pity counter carries across free and paid pulls, which is the honest way to run it. And it means a pack's "8 sample pulls" has a stated, checkable value rather than being a number next to a word.

**Where pulls come from:** Geneticist Tier 6 grants one daily; store packs grant 1, 3, 8, 20 and 50 by tier; the Custom Chest offers them as one of six slots; Splice Roulette sells them at 300 shards.

---

## 11. Audits

### Constraint 2 — species acquisition guarantees all eight counters

**Untouched by this document.** Samples never grant access (bible §1.7), so no sample rate can lock a player out of a counter or open one to them. The guarantee rests entirely on base-stock supply and campaign milestones, which is the base-stock table's problem.

### Constraint 3 — roster cap never binds below eight counters plus working depth

Holding all eight counters takes **six creatures**, not four: Ember carries Splash and Cinder, Hollow carries Pierce and Reach, and the remaining four counters sit one each on Vetch, Pale, Skitter and Loam. An earlier draft said four, which assumed a best-case distribution that only two species provide.

Six for counters, five deployed (overlapping), plus garrison, escort and splice fodder. **The floor of 20 clears it comfortably.** ✓

### Coverage saturation

Does the ladder run out? A core player earns roughly 14 tier-I equivalents a day. A roster of ten combat-relevant creatures at two traits each, all at tier III, costs 180. Thirteen days of total income.

**Churn is what stops that happening.** At six splices a day a core player destroys twelve creatures daily and loses the coverage on two traits each time. Demand outruns supply by a wide margin, and the ladder stays live.

**The failure mode is a player who stops splicing.** They would saturate in a fortnight and sample income would become meaningless. That is also a player who has stopped playing the core loop, so it is self-correcting — but it is worth instrumenting, because a saturated Sample Store is an early signal that a player has drifted into idle harvesting.

---

## 12. Guardrails

- Samples never grant trait access, at any tier, from any source
- No Instinct samples, no Aberrant samples
- Sample income spread never exceeds 5× between the lightest and heaviest engaged player
- Campaign milestone and Roulette samples are never discarded by the capacity valve
- Fusing never costs Gene Shards
- Catalysts come from Apex Vein extraction only and are never sold, at any price, in any bundle
- Odds and the pity counter are visible before any pull, free or paid
- A sample pull and a Roulette spin are the same object with the same table

---

## 13. Open questions

1. **Is 75/25 the right harvest weighting?** Too weighted and a player can never build toward a new species; too flat and most of the inventory is dead. It is the number most likely to be wrong and the easiest to tune.
2. **Should the capacity valve discard, or block?** Discarding is silent and cheap; blocking new drops until the player fuses is louder and arguably more honest. Discarding is specified above because blocking makes a player who has not opened the Sample Store in two days stop earning without knowing it.
3. **Litter III at six creatures a day** may be half a core player's base stock from one trait — see §8. This is a combat-numbers question that only became visible from the economy side.
4. **Mutation Surge at four weeks a year** is a thin Aberrant supply line for casual players. Six times a year is the recommendation and it is a live-ops decision, not an economy one.
5. **Does retirement need a cooldown?** As specified, a player can fuse coverage onto a creature and retire it to get the samples back with no loss, which makes the Sample Store a free storage locker for coverage. Probably harmless; possibly an exploit that makes roster capacity meaningless.

---

*Owns: sample sources and rates, tier mix, weighting, fusing, the capacity curve, retirement yield, splice coverage loss, catalyst frequency, the Aberrant sub-roll, and what a sample pull is. Does not own: base-stock supply rates or Instinct source weights (still owed), Gene Shard income (`broodline_economy_model.md`), or Roulette pricing and event cadence (`broodline_live_ops_events.md`).*
