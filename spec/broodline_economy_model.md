# Broodline — Economy Model

*Design spec, the numbers underneath every other document*

---

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Last brought into line with the bible: 4 Sep 2026.


## 1. Why This Document Exists

Every system in the design spends Gene Shards. Facility tiers, regeneration skips, timer skips, Roulette spins, Scout Reports, alliance treasury, cosmetics — all priced in a currency whose income rate is stated nowhere.

That means every cost in the spec set is currently unfalsifiable. "12 Core tiers on a steepening curve" cannot be evaluated as two years of progression or two months of it. This document fixes the faucets and the sinks to the same scale so the rest of the set can be checked against something.

**Founding principle, inherited from bible §8.1 and applied here as arithmetic:** money and time buy *rate*, never *reach*. Every number below is chosen so the gap between a free player and a paying one is measured in weeks of acceleration, not in content they can't access.

---

## 2. The Base Unit

Everything is expressed as a multiple of one number.

> **Common Vein base yield = 40 Gene Shards per hour**, at Harvest Array tier 1.

| Node | Multiplier | Rate at Array T1 | Constraint |
|---|---|---|---|
| **Common Vein** | 1× | 40/hr | Decays to ~28/hr after a week of continuous harvest |
| **Rich Deposit** | 3× | 120/hr | Depletes fully in ~6 days, syncing with the weekly rotation |
| **Apex Vein** | 8× | 320/hr | Requires Collector present, 48–72hr window, contested |

**Harvest Array scaling:** T1 = 1.00×, T4 = 1.35×, T8 = 2.00×, T12 = 3.00×. A maxed Array triples income, which is what makes it the raider's and the optimiser's first investment alike.

**Offline accrual cap: 12 hours.** Production banks while the player is away and stops at 12 hours' worth. Twelve is deliberate — it covers a full night's sleep without penalty, so the cap never punishes a normal schedule, but it does reward a second daily check-in. An 8-hour cap would quietly tax anyone who sleeps.

---

## 3. Faucets

| Source | Casual | Core | Optimiser |
|---|---|---|---|
| Node harvest | 480 | 2,600 | 3,900 |
| Daily login + streak | 60 | 120 | 120 |
| Campaign / replay drip | 120 | 250 | 300 |
| Events (weekly, amortised) | 100 | 400 | 500 |
| Raid cargo | — | 200 | 400 |
| Apex Vein (amortised) | — | — | 1,100 |
| **Daily total** | **~760** | **~3,570** | **~6,320** |

Archetypes: **Casual** is one session a day, Common Vein only, unaffiliated. **Core** is three sessions, a Rich Deposit, alliance membership. **Optimiser** is four-plus sessions with contested Apex access.

The spread is roughly **1 : 4.7 : 8.3**. That is wide, and the top end is entirely Apex-driven. Apex capture is modelled here at 30% of theoretical — the windows are short, the node is contested, and yield splits on the presence stat. **If real capture runs higher than 30%, the top archetype detaches from the rest of the economy.** Treat 6× as the ceiling on the free-play spread and tune Apex amortisation to hold it there; it is the only lever that moves this ratio without touching anything else.

---

## 4. Sinks

| Sink | Cost | Notes |
|---|---|---|
| **Gene Lab upgrades** | See §5 | The primary long-term sink, ~60% of lifetime spend |
| **Regeneration skip** | 1.5/remaining minute | 20-min skip = 30, 60-min = 90 |
| **Facility timer skip** | 25/remaining hour, min 50 | 72hr top-tier skip = 1,800 |
| **Splice Roulette spin** | 300 | Dispenses samples. Three tier-I samples fuse to a tier-II, so ~900 per uptier. |
| **Scout Report** | 200 | Apex early warning, per bible §5.9 |
| **Alliance treasury** | Player-capped 600/day | Prevents one whale funding a whole alliance's tech |
| **Cosmetics** | 1,500–6,000 | Skins, lineage frames, Founder portraits |
| **Shards → Geneticist XP** | 2:1 | Per `broodline_monetization.md` |

The daily-cost sinks — regen skips and treasury — should absorb roughly 25% of a core player's income, leaving 60% for the Gene Lab and 15% discretionary. If regen skips ever exceed that, players are losing too many waves and the combat difficulty curve is the actual problem, not the price.

---

## 5. The Facility Curve

Six facilities, not five. The previous version priced Core against five and applied a flat 35% to the rest; the reconciliation added a sixth and the flat rate was never right anyway, since a capacity valve and the spine of the whole progression should not cost the same.

### 5.1 Core

The spine. Every other facility is capped at Core's tier, and every Core tier additionally requires a campaign milestone — see §7.

| Tier | Cost | Timer | Cumulative |
|---|---|---|---|
| 1 | 500 | 5 min | 500 |
| 2 | 1,200 | 15 min | 1,700 |
| 3 | 2,600 | 45 min | 4,300 |
| 4 | 5,000 | 2 hr | 9,300 |
| 5 | 9,000 | 4 hr | 18,300 |
| 6 | 15,000 | 8 hr | 33,300 |
| 7 | 25,000 | 14 hr | 58,300 |
| 8 | 40,000 | 24 hr | 98,300 |
| 9 | 62,000 | 36 hr | 160,300 |
| 10 | 95,000 | 48 hr | 255,300 |
| 11 | 130,000 | 60 hr | 385,300 |
| 12 | 200,000 | 72 hr | **585,300** |

**The early curve is deliberately shallow.** Six tiers inside the first week is what makes the system feel alive to a new player. The weight sits in tiers 10 to 12, which is where the two-year players live.

**Core total: ~585,000 shards and ~269 hours of build timer.**

### 5.2 The other five

Priced as a share of Core at the same tier, and the shares differ because the jobs differ.

| Facility | Share of Core | Total | What each tier buys |
|---|---|---|---|
| **Splicing Chamber** | **60%** | ~351,000 | Maximum generation, which caps the coverage ceiling |
| **Harvest Array** | **50%** | ~293,000 | Shard yield ×1.0 → ×3.0; base stock ×1.0 → ×2.0 |
| **Hatchery** | **35%** | ~205,000 | Roster capacity 20 → 60 |
| **Drive** | **30%** | ~176,000 | Ark transit time ×1.0 → ×0.55, and the exposure window with it |
| **Gene Vault** | **20%** | ~117,000 | Sample capacity 20 → 60 |
| | **195%** | **~1,142,000** | |

**A fully built Gene Lab is ~1,727,000 shards.** Revised up from the ~1.6M the flat-rate estimate produced.

**Splicing Chamber is second most expensive because it gates the ceiling that gates everything.** Per `broodline_combat_numbers.md` §7 it sets maximum generation — G2 at tiers 1–2, G4 at 3–5, G6 at 6–8, G9 at 9–12 — and generation caps how high coverage can climb. It is the closest thing to a power purchase in the game, which is exactly why it should be expensive and why it is capped at Core's tier.

**Harvest Array is self-funding and priced accordingly.** Every tier pays for the next, so a shallow curve would let it run away. At 50% of Core it stays a decision rather than an obvious first purchase.

**Gene Vault is the cheapest by a wide margin.** It is a pressure valve, not power — capacity for coverage that is recoverable if discarded, per `broodline_sample_economy.md` §5. Charging power prices for a valve would make an inconvenience feel like a paywall.

**Non-Core build timers run at 50% of Core's at the same tier.** Total lab build time is roughly **940 hours**, about 39 days with one slot, less once the second unlocks at Core tier 8.

### 5.3 Timers versus shards

They bind at different points in the game and it is worth knowing which is which.

**Timers pace the early game.** Through Core tier 6 a player has the shards before they have the hours — the crossover sits around **Core tier 7**, where a 25,000-shard cost first outruns a 14-hour wait for a core player.

**Shards pace everything after.** By tier 10 the timer is 48 hours and the cost is 95,000, which is forty-four days of a core player's Gene Lab budget. From there the timer is noise.

That crossover is also where skip pricing stops mattering. At 25 shards per remaining hour a full 72-hour skip is 1,800 shards — against a 200,000-shard tier 12, it is rounding.

**What that means in time:**

| | To Core T8 (2nd build slot) | To a full Gene Lab |
|---|---|---|
| Casual | ~12 months | Never — and that is fine |
| Core | ~2.5 months | ~27 months |
| Optimiser | ~6 weeks | ~15 months |

Assumes 60% of the Gene Lab budget goes to Core early and the full budget across a lifetime. Revised from the flat-rate figures, which understated everything.

**Two and a bit years for a core player answers the original Vault spec's first open question: 12 tiers is the right depth.** The casual player never maxes out, reaches the second build slot inside a year, and is never locked out of content — only running at lower throughput, exactly as the guardrails require.

**These figures assume shards are the constraint. For a payer they are not.** See §7.

---

## 6. Two Corrections Carried Into the Bible

**The charge model corrected.** An early spec reasoned from "five charges a day." At a 25-minute regen with a cap of 5 — refilling from empty in 2h05m — a player logging in three times a day captures **15 charges**, plus three from rewarded ads. The real figure is 15–18.

Bible §2.3 sets mutation at **~9% per splice**, rolled once, with the mutating slot chosen after. One roll is easier to display honestly on the splice screen, and the probability table is regulator-facing.

**Charges are not the constraint on splicing — base stock is.** Fifteen splices a day would consume thirty creatures a day, which no supply model produces. A core player runs roughly **six splices a day** and banks the surplus charges, putting mutation at every 1.9 days. Worked through in `broodline_sample_economy.md` §8.

**The pack ladder is non-monotonic.** As specced, $4.99 gives 60 shards/dollar while $0.99 gives 101, and the $14.99 Mythic pack contains more shards than the $19.99 tier. Value per dollar must rise with price or players who do the arithmetic conclude the store is manipulative.

| Price | Shards | Per dollar |
|---|---|---|
| $0.99 | 100 | 101 |
| $4.99 | 600 | 120 |
| $9.99 | 1,400 | 140 |
| $19.99 | 3,200 | 160 |
| $49.99 | 9,000 | 180 |
| $99.99 | 20,000 | 200 |

Charges, XP, and sample pulls layer on top and carry their own value; the shard component should be monotonic regardless. Bible §8.3 now carries this ladder, extended to $49.99 and $99.99 tiers.

---

## 7. The Payer Ceiling

At $99.99 for 20,000 shards, a fully built Gene Lab costs about **$8,600** if bought outright. That number sounds alarming and isn't, because **Core tiers require campaign milestones** and campaign progression cannot be purchased.

This is the single load-bearing fact of the whole economy. A spender compresses the shard and timer cost to near zero and then stops at the same wall as everyone else. The design target: **the fastest possible campaign completion is ~9 months**, meaning no amount of money finishes the Gene Lab faster than that.

**The gate binds earlier than the shard figures suggest.** §5.3 puts an optimiser at Core tier 8 in about six weeks *if shards were the only constraint*. Core tier 8 requires campaign milestone 8, which `broodline_campaign_structure.md` §2 places at wave 41 — five months of campaign progression at the fastest plausible pace. **From roughly Core tier 5 onward a heavy spender is milestone-bound, not shard-bound**, and the gap widens with every tier.

That is the anti-whale structure doing its job, and it means the shard curve above is a floor on time rather than a prediction of it. For a casual or core player the shard figures are real; for a payer they stopped being the binding constraint months earlier.

If the campaign milestone gate is ever softened, weakened, or sold around, the entire structure collapses into pay-to-win in a single patch. It should be treated as immutable.

---

## 8. The Terminal Sink Problem

A core player maxes the Gene Lab around month 27 and shard demand falls off a cliff. Every remaining sink is small and discretionary. Income keeps arriving with nothing to buy, and the map economy — harvesting, relocating, contesting Apex Veins — quietly stops mattering for the players with the most invested.

**Recommendation: Calibration.** Post-T12, each facility accepts repeatable refinements at a flat **50,000 shards** for **+0.5%** to its effect, uncapped and unbounded. Diminishing in practice, infinite in principle, and it adds rate rather than reach — a Calibrated player harvests faster, never fields a stronger creature.

Ship this at launch even though nobody reaches it for eighteen months. Retrofitting a terminal sink into a live economy means either inflating costs on players who already paid the old prices, or introducing a new currency, and both are worse than building it now.

---

## 9. Naming — Resolved

Breeder Tier, Breeder XP, Breeder Pass and Breeder's Cup are retired. The current vocabulary is **Geneticist Tier, Geneticist XP, Season Pass, Apex Cup**, and *Vault* as infrastructure is now the **Gene Lab** with six named facilities. Full rename table in `broodline_supersession_map.md`.

---

## 10. Guardrails

- Free-play income spread never exceeds **6×** between the lightest and heaviest engaged player
- Offline accrual cap never drops below **12 hours**
- Shard value per dollar rises monotonically across the pack ladder
- The campaign milestone gate on Core tiers is never bypassable by purchase
- No sink introduced later may grant combat power, roster slots beyond the Hatchery, or trait access or coverage
- Every sink has a free path; money buys the same thing sooner

---

## 11. Open Questions

1. **Is 40/hr the right base?** It's a scaling anchor, not a balance decision — every other number moves with it. Worth setting against target session length in soft launch rather than in advance.
2. **Should Apex Vein yield be capped per player per week** rather than amortised? A hard cap holds the 6× spread more reliably than tuning capture rate, but it makes the rush moment feel less rewarding for the alliance that wins it.
3. **Does the 12-hour accrual cap need to scale with Core tier?** Raising it late-game rewards investment; it also widens the spread at exactly the point where it's already widest.
4. **Regeneration skip pricing assumes players lose waves occasionally.** If the campaign is tuned easy, this sink is near-zero and the Vault absorbs a larger share than modelled.
5. **Calibration at 50,000 flat** may be too cheap by year three, when Optimiser income has tripled through Array scaling. A slowly rising cost is safer but harder to communicate.
6. ~~**Sample drop rates, fusing costs, the Gene Vault capacity curve and catalyst frequency.**~~ **Written** — `broodline_sample_economy.md`, kept separate because this document owns Gene Shards and mixing the two currencies on one page is what produced the last round of drift.
7. ~~**Base-stock supply rates.**~~ **Written** — `broodline_base_stock.md`. Roughly 3.8, 11.5 and 19.2 creatures a day by archetype, all running near break-even against splice consumption.
8. **The five facility cost shares at §5.2 are the least evidenced numbers in this document.** They are reasoned from each facility's job rather than fitted to anything, and the two that matter most are Splicing Chamber at 60% — because it gates the coverage ceiling — and Gene Vault at 20% — because charging power prices for a pressure valve turns an inconvenience into a paywall.

---

*Owns Gene Shards. Samples are owned by `broodline_sample_economy.md`. Base-stock supply rates are still owed by neither.*
