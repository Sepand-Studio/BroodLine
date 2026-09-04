# Broodline — Economy Model

*Design spec, the numbers underneath every other document*

---

## 1. Why This Document Exists

Nine specs spend Gene Shards. Vault tiers, regeneration skips, timer skips, Roulette spins, Scout Reports, alliance treasury, cosmetics — all priced in a currency whose income rate is stated nowhere.

That means every cost in the spec set is currently unfalsifiable. "12 Core tiers on a steepening curve" cannot be evaluated as two years of progression or two months of it. This document fixes the faucets and the sinks to the same scale so the rest of the set can be checked against something.

**Founding principle, inherited from the Vault spec and applied here as arithmetic:** money and time buy *rate*, never *reach*. Every number below is chosen so the gap between a free player and a paying one is measured in weeks of acceleration, not in content they can't access.

---

## 2. The Base Unit

Everything is expressed as a multiple of one number.

> **Common Vein base yield = 40 Gene Shards per hour**, at Harvest Array tier 1.

| Node | Multiplier | Rate at Array T1 | Constraint |
|---|---|---|---|
| **Common Vein** | 1× | 40/hr | Decays to ~28/hr after a week of continuous harvest |
| **Rich Deposit** | 3× | 120/hr | Depletes fully in 5–7 days |
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
| **Gene Vault upgrades** | See §5 | The primary long-term sink, ~60% of lifetime spend |
| **Regeneration skip** | 1.5/remaining minute | 20-min skip = 30, 60-min = 90 |
| **Vault timer skip** | 25/remaining hour, min 50 | 48hr top-tier skip = 1,200 |
| **Splice Roulette spin** | 300 | 3 fragments = 1 trait, so ~900 per trait |
| **Scout Report** | 200 | Apex early warning, per node spec §8 |
| **Alliance treasury** | Player-capped 600/day | Prevents one whale funding a whole alliance's tech |
| **Cosmetics** | 1,500–6,000 | Skins, lineage frames, Founder portraits |
| **Shards → Geneticist XP** | 2:1 | Per monetization spec §3 |

The daily-cost sinks — regen skips and treasury — should absorb roughly 25% of a core player's income, leaving 60% for the Vault and 15% discretionary. If regen skips ever exceed that, players are losing too many waves and the combat difficulty curve is the actual problem, not the price.

---

## 5. The Vault Curve

Core module, twelve tiers:

| Tier | Cost | Build timer |
|---|---|---|
| 1 | 150 | 2 min |
| 2 | 400 | 10 min |
| 3 | 900 | 45 min |
| 4 | 2,000 | 2 hr |
| 5 | 4,000 | 4 hr |
| 6 | 8,000 | 8 hr |
| 7 | 15,000 | 12 hr |
| 8 | 26,000 | 18 hr |
| 9 | 48,000 | 24 hr |
| 10 | 90,000 | 30 hr |
| 11 | 150,000 | 36 hr |
| 12 | 240,000 | 48 hr |

Core total: **~584,000**. The other four modules cost **35% of Core's price at the same tier**, so a fully maxed Vault is **~1.4M shards**.

**What that means in time:**

| | To Core T8 (2nd build slot) | To full Vault |
|---|---|---|
| Casual | ~10 months | Never — and that is fine |
| Core | ~2 months | ~21 months |
| Optimiser | ~5 weeks | ~12 months |

Twenty-one months for a core player answers the Vault spec's first open question: **12 tiers is the right depth.** The casual player never maxes out, but reaches the second build slot inside a year and is never locked out of content — only running at lower throughput, exactly as the guardrails require.

The early curve is deliberately shallow. Six tiers inside the first week is what makes the system feel alive to a new player; the weight sits in tiers 10–12, which is where the two-year players live.

---

## 6. Three Corrections to Existing Specs

**The charge model contradicts itself.** The genetics spec reasons from "five charges a day" and concludes a 3% mutation rate fires roughly every six days. But the monetization spec sets regen at 1 per 25 minutes with a cap of 5 — which refills from empty in 2h05m. A player logging in three times a day captures **15 charges**, not five, plus three from rewarded ads.

Real mutation cadence at 15–18 splices/day and 3%: **every 2.2 days.** That resolves the genetics spec's open question 1 — 3% is fine, and the worry was based on a charge figure that the regen rate doesn't produce.

**Mutation rate is ambiguous.** The genetics spec says a *rolled slot* may mutate, then states the rate per *splice*. With two rolling slots, 3% per slot is 5.9% per splice — nearly double. Recommend **3% per splice**, rolled once, with the mutating slot chosen after. One roll is easier to display honestly on the splice screen, and the probability table is regulator-facing.

**The pack ladder is non-monotonic.** As specced, $4.99 gives 60 shards/dollar while $0.99 gives 101, and the $14.99 Mythic pack contains more shards than the $19.99 tier. Value per dollar must rise with price or players who do the arithmetic conclude the store is manipulative.

| Price | Shards | Per dollar |
|---|---|---|
| $0.99 | 100 | 101 |
| $4.99 | 600 | 120 |
| $9.99 | 1,400 | 140 |
| $19.99 | 3,200 | 160 |
| $49.99 | 9,000 | 180 |
| $99.99 | 20,000 | 200 |

Charges, XP, and trait pulls layer on top and carry their own value; the shard component should be monotonic regardless.

---

## 7. The Payer Ceiling

At $99.99 for 20,000 shards, a fully maxed Vault costs about **$7,000** if bought outright. That number sounds alarming and isn't, because **Core tiers require campaign milestones** and campaign progression cannot be purchased.

This is the single load-bearing fact of the whole economy. A spender compresses the shard and timer cost to near zero and then stops at the same wall as everyone else. The design target: **the fastest possible campaign completion is ~9 months**, meaning no amount of money finishes the Vault faster than that.

If the campaign milestone gate is ever softened, weakened, or sold around, the entire structure collapses into pay-to-win in a single patch. It should be treated as immutable.

---

## 8. The Terminal Sink Problem

A core player maxes the Vault around month 21 and shard demand falls off a cliff. Every remaining sink is small and discretionary. Income keeps arriving with nothing to buy, and the map economy — harvesting, relocating, contesting Apex Veins — quietly stops mattering for the players with the most invested.

**Recommendation: Calibration.** Post-T12, each module accepts repeatable refinements at a flat **50,000 shards** for **+0.5%** to its effect, uncapped and unbounded. Diminishing in practice, infinite in principle, and it adds rate rather than reach — a Calibrated player harvests faster, never fields a stronger creature.

Ship this at launch even though nobody reaches it for eighteen months. Retrofitting a terminal sink into a live economy means either inflating costs on players who already paid the old prices, or introducing a new currency, and both are worse than building it now.

---

## 9. Naming Drift

The monetization spec uses **Breeder Tier, Breeder XP, Breeder Pass, Breeder's Cup**. Every later document uses **Geneticist Tier, Season Pass, Apex Cup**. These are the same systems under two vocabularies.

Standardise on the later set — *Geneticist* fits the Warden fiction and the clinical register the art direction commits to, where *Breeder* reads pastoral and slightly off-tone. Requires a pass over the monetization spec only.

---

## 10. Guardrails

- Free-play income spread never exceeds **6×** between the lightest and heaviest engaged player
- Offline accrual cap never drops below **12 hours**
- Shard value per dollar rises monotonically across the pack ladder
- The campaign milestone gate on Core tiers is never bypassable by purchase
- No sink introduced later may grant combat power, roster slots beyond Habitat, or trait access
- Every sink has a free path; money buys the same thing sooner

---

## 11. Open Questions

1. **Is 40/hr the right base?** It's a scaling anchor, not a balance decision — every other number moves with it. Worth setting against target session length in soft launch rather than in advance.
2. **Should Apex Vein yield be capped per player per week** rather than amortised? A hard cap holds the 6× spread more reliably than tuning capture rate, but it makes the rush moment feel less rewarding for the alliance that wins it.
3. **Does the 12-hour accrual cap need to scale with Vault tier?** Raising it late-game rewards investment; it also widens the spread at exactly the point where it's already widest.
4. **Regeneration skip pricing assumes players lose waves occasionally.** If the campaign is tuned easy, this sink is near-zero and the Vault absorbs a larger share than modelled.
5. **Calibration at 50,000 flat** may be too cheap by year three, when Optimiser income has tripled through Array scaling. A slowly rising cost is safer but harder to communicate.

---

*Remaining undocumented: the collector raiding spec — cited by four documents but absent from the set — plus the Trait Codex, mail centre, player profile, and settings.*
