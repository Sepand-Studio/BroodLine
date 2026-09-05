---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_economy_model_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  The numbers under every other document, re-derived against bible §5.3–5.5,
  §7.2–7.6, §8.2–8.3 and the promoted campaign, region and Collectors
  companions. Adds the sample economy §7.6 called for and the node-depletion
  model. Corrects the bible's pack ladder (register 6.5). Every value is a
  soft-launch starting point anchored to one number.
---

# Broodline — Economy Model

*Design spec, the numbers underneath every other document. Companion to bible §7 and §8.*

---

## 1. Why this document exists

Every cost in the spec set is unfalsifiable until income is stated. "Twelve Core tiers on a steepening curve" cannot be evaluated as two years of progression or two months of it. This fixes the faucets and sinks to one scale so everything else can be checked.

**Founding principle, bible §7.7 as arithmetic:** money and time buy rate, never reach. Every number below is chosen so the gap between a free player and a paying one is measured in months of acceleration, never in content.

Two currencies of progress run through the game and this document models both: **Gene Shards** (facilities, timers, convenience) and **samples** (coverage). The older model covered only shards.

---

## 2. The base unit

> **Common Vein base yield = 40 Gene Shards per hour**, at Harvest Array tier 1.

Every other number is a multiple of this. It is a scaling anchor, not a balance decision; move it and everything moves with it.

| Node (§5.3) | Multiplier | Rate at Array T1 | Constraint |
|---|---|---|---|
| **Common Vein** | 1× | 40/hr | Never depletes; a player's yield from it decays ~30% after a week of continuous harvest, resetting on relocation |
| **Rich Deposit** | 3× | 120/hr | Extraction pool, §3 |
| **Apex Vein** | 8× | 320/hr | Collector present; 48–72h window; presence split |

**Harvest Array scaling:** T1 = 1.00×, T4 = 1.35×, T8 = 2.00×, T12 = 3.00×. A maxed Array triples income, which is why it is the first investment for raider and optimiser alike.

**Adjacent harvesting** (§5.5) runs at 50% of the node's rate.

**Offline accrual cap: 12 hours.** Production banks while away and stops at twelve hours' worth. Twelve covers a night's sleep without penalty and still rewards a second daily check-in; eight would quietly tax anyone who sleeps.

---

## 3. Node depletion

**Depletion tracks total extraction, never wall-clock time.** A Rich Deposit is a pool of shards; it drains at the combined rate of every Ark harvesting it, Array multipliers included. Bible §5.3's "depletes in ~6 days of active harvesting" is this pool at design load.

| | Value |
|---|---|
| **Rich Deposit pool** | 55,000 shards |
| Design load | Three Arks at average Array 1.3× = 468/hr → **~5 days** |
| Solo Array-T1 Ark | 19 days — the weekly rotation moves it first |
| Three Array-T12 Arks | 51 hours |

The pool is set so that a deposit under design load runs dry about a day before the weekly rotation (§5.3's stated intent), and so that a crowded deposit dies visibly fast. That is the pressure: a rich region with five Arks on it is a rich region for two days.

**Apex Vein:** no pool. It burns out on its 48–72h timer regardless of extraction, and yield splits on the presence stat. One **Apex extraction** — the unit the Collectors companion sizes the Deep Hauler against — is defined as **5,000 shards**, roughly sixteen Ark-hours at full rate.

**Common Vein decay is per player, not per node.** The node never depletes (§5.4); the individual's yield from it decays after a week of sitting still and resets on relocation. This keeps the §5.8 livable baseline intact for everyone else while still nudging the parked player.

### Server population

Live Rich Deposits per rotation is the occupancy knob on the region roster's 45 slots. At **30 live** (two-thirds), each holding 5–15 Arks across its life, and roughly 30% of daily actives seeking a Rich Deposit at any time, a server supports **500–1,500 DAU**. Below 500 the Mid Reach is empty and rotation does not matter; above 1,500 deposits die in a day and the Outer Reach becomes the only economy. This is the number the server topology has to hold, and occupancy is how live-ops tunes it without touching the map.

---

## 4. Shard faucets

Three archetypes. **Casual** is one session a day, Common Vein only, unaffiliated. **Core** is three sessions, a Rich Deposit most weeks, in an alliance. **Optimiser** is four-plus sessions with contested Apex access and a Deep Hauler.

| Source | Casual | Core | Optimiser |
|---|---|---|---|
| Node harvest | 480 | 2,600 | 3,900 |
| Daily login and streak | 60 | 120 | 120 |
| Campaign first-clears and replay | 120 | 250 | 300 |
| Events, weekly, amortised | 100 | 400 | 500 |
| Raid cargo, net of losses | — | 200 | 400 |
| Apex extraction, amortised | — | — | 1,100 |
| **Daily total** | **~760** | **~3,570** | **~6,320** |

Derivations: Casual banks the 12-hour cap once a day at 40/hr. Core runs a Rich Deposit ~20 hours a day at Array ~1.1× less the presence split. Optimiser adds Array ~1.6× and 1.5 Apex extractions a week. Raid figures come from the Collectors companion: a Hauler at 2,000 cargo, 40% taken, 50% to the attacker, two raids a day at 50%.

**The spread is 1 : 4.7 : 8.3**, and the top end is entirely Apex. Apex capture is modelled at 30% of theoretical. **If real capture runs higher, the top archetype detaches from the economy.** Treat **6×** as the ceiling on the free-play spread and hold it with Apex amortisation — it is the only lever that moves the ratio without touching anything else.

---

## 5. Shard sinks

| Sink | Cost | Notes |
|---|---|---|
| **Facility upgrades** | §6 | The long-term sink, ~60% of lifetime spend |
| **Regeneration skip** | 1.5 / remaining minute | 20-min skip = 30, 60-min = 90 (§4.11) |
| **Timer skip** | 25 / remaining hour, min 50 | 48h top-tier skip = 1,200 |
| **Splice Roulette spin** | 300 | Yields samples, never traits (§7.6). Odds displayed. |
| **Scout Report** | 200 | Detail and a head start, never access (§5.9) |
| **Night move** | 120 | Unexposed Collector route (§5.6) |
| **Alliance treasury** | Capped 600 / player / day | Stops one whale funding an alliance's tech |
| **Cosmetics** | 1,500–6,000 | Skins, lineage frames, Founder portraits, Ark exteriors |
| **Shards → Geneticist XP** | 2 : 1 | §8.2 |

Daily-cost sinks — regen skips, treasury, night moves — should absorb about 25% of a core player's income, leaving 60% for facilities and 15% discretionary. **If regen skips exceed that, players are losing too many waves and the difficulty curve is the problem, not the price.**

---

## 6. The facility curve

Bible §7.2: six facilities. §7.3: Core caps the rest, twelve tiers, steepening shards, a timer and a campaign milestone each. §7.4: first six or so tiers in minutes to hours, 48h maximum, second build slot at Core 8.

| Core tier | Shards | Timer | Campaign milestone |
|---|---|---|---|
| 1 | 150 | 2 min | wave 2 |
| 2 | 400 | 10 min | 6 |
| 3 | 900 | 45 min | 10 |
| 4 | 2,000 | 2 hr | 15 |
| 5 | 4,000 | 4 hr | 20 |
| 6 | 8,000 | 8 hr | 25 |
| 7 | 15,000 | 12 hr | 30 |
| 8 | 26,000 | 18 hr | 35 · second build slot |
| 9 | 48,000 | 24 hr | 40 |
| 10 | 90,000 | 30 hr | 46 |
| 11 | 150,000 | 36 hr | 53 |
| 12 | 240,000 | 48 hr | 60 |

Core total **~584,000**. The other five facilities cost **35% of Core at the same tier**, so a fully built Gene Lab is **~1.6M shards**.

**What that means in time**, at 60% of income to facilities:

| | To Core 8 | To Core 12 | To a full Gene Lab |
|---|---|---|---|
| Casual | ~11 months | ~5 years | Never — and that is fine |
| Core | ~2.5 months | ~9 months | ~25 months |
| Optimiser | ~6 weeks | ~5 months | ~14 months |

Two things this table shows. **The campaign, not shards, is what gates a core player's Core 12** — nine months of shards lands almost exactly on the campaign's nine-month floor, which means the two gates are tuned to bind together for the player the game is built around. And **the optimiser hits shard-Core-12 at five months and then waits four**, which is the anti-whale wall working on effort as well as money.

The early curve is shallow on purpose: six tiers in the first week is what makes the Gene Lab feel alive. The weight is in tiers 10–12, where the two-year players live.

---

## 7. The sample economy

Bible §7.6 settles the structure and defers every value. These are the starting values.

### Units

A **tier-I equivalent** (T1e) is the unit. Three T1 fuse to one T2; three T2 to one T3. So a T2 sample is 3 T1e and a T3 sample is 9 T1e. Raising a trait from tier I to II applies one T2 (3 T1e); from II to III applies one T3 (9 T1e). **Tier I to III costs 12 T1e per trait, 24 per creature.**

### Sources

| Source (§7.6) | Starting yield | Weighting |
|---|---|---|
| **Campaign first clear** | 2 × T1, per wave | Unweighted |
| **Replay** | 1 × T1 per replay, 3 per wave per day | Unweighted |
| **Common Vein** | 1 × T1 per 4 hours harvested | Toward traits the roster carries |
| **Rich Deposit** | 1 × T1 per 2 hours · 1 × T2 per 12 hours | Toward traits the roster carries |
| **Apex extraction** | 3 × T2 · 1 × T3 · **1 catalyst** | Toward traits the roster carries |
| **Core milestone** | 1 × T2 of a **named trait** | Fixed — the anti-bad-luck guarantee |
| **Splice Roulette** | 1 sample per spin: 82% T1 · 15% T2 · 3% T3 | Rotating pool, displayed |
| **Retirement** | One-third of fused value back as T1, rounded down | Trait dies with the animal |

Retirement worked example: a tier-III trait has 12 T1e invested; retiring returns 4 T1. Tier II returns 1. Tier I returns nothing — it was never fused.

**Milestone samples are named.** The twelve Core milestones each grant a T2 of a specific trait, sequenced to the campaign: Pierce at wave 2's milestone is too early (the player has no Hollow until wave 4), so the sequence is Splash, Pierce, Taunt, Chill, Cinder, Reach, Sprint, Burrow, then the four utility traits Carapace, Litter, Regrow, Screen at tiers 9–12. Every counter is guaranteed a tier-II by the time the campaign has introduced its raider.

### Daily sample income

| | Casual | Core | Optimiser |
|---|---|---|---|
| T1e per day | ~6 | ~18 | ~35 |
| Days to one tier-III trait (12 T1e) | ~2 | ~0.7 | ~0.35 |

This is fast, and it is meant to be, because **generation is the real ceiling** (§2.5). A G3 creature holds tier II at most; tier III needs a deeper line, and a deep line needs Splicing Chamber tiers behind Core tiers behind milestones. Sample income being generous keeps the pressure on lineage rather than on grinding; a player with samples and no generation is the intended mid-game state.

**The check that matters:** a core player should hold their first tier-III counter around **week three**, when Brood arrives at wave 25 and tier visibly changes the outcome for the first time. If it arrives in week one, the harvest rates are too high; if week five, the Chamber curve is too steep.

### Capacity

Gene Vault facility: **20 at tier 1, ~60 at tier 12**, roughly +3.5 per tier. Warning at 88%; above it, wave samples are discarded (§7.6). At ~18 T1e a day a core player fills a tier-1 Vault in about a day of not fusing, which is the pressure valve working — fuse or lose, never locked out.

### Catalysts

One per Apex extraction, and from Mutation Surge. Never sold (§8.6). An optimiser at 1.5 extractions a week earns ~6 catalysts a month; a core player in an Apex-capable alliance perhaps 1–2. This is deliberately the scarcest earnable thing in the game.

---

## 8. Charges and mutation

Bible §8.2: charge cap 5, one per 25 minutes, cap to 10 with Geneticist Tier. A player logging in three times a day captures **15 charges**, plus up to 3 from rewarded ads. Bible §2.3: base mutation ~9%, Aberrant sub-roll 5% of mutations rising to 50% with a catalyst.

At 15–18 splices a day and 9%: **a mutation every ~0.7 days**; an Aberrant, uncatalysed, every **~13 days**. With one catalyst spent a week, an optimiser sees an Aberrant roughly **weekly**. Those are the chase cadences the live-ops calendar should be built around.

The older model's 3% rate and "mutation every 2.2 days" are superseded by register 3.1.

---

## 9. The pack ladder

Bible §8.3's fixed packs carry a shard component that is **not monotonic**: $0.99 buys 101 shards per dollar, $4.99 buys 60, and the $14.99 Mythic pack carries more shards than the $19.99 tier. The older economy model corrected this and the correction never reached the bible. Players who do the arithmetic conclude the store is manipulative.

**Corrected shard components**, rising strictly with price:

| Pack | Price | Shards | Per dollar |
|---|---|---|---|
| Starter Splice | $0.99 | 100 | 101 |
| Lab Bundle | $4.99 | 600 | 120 |
| Lab Expansion | $9.99 | 1,400 | 140 |
| Mythic Lab Access | $14.99 | **1,500** | 100 — its value is 48h unlimited charges, not shards |
| Geneticist's Vault | $19.99 | 3,200 | 160 |

Charges, XP and sample pulls layer on top and carry their own value. The Mythic pack is allowed to break the shard ladder downward because it is not selling shards; it must never break it upward. **"Breeder Bundle" is renamed Lab Bundle** under the Geneticist vocabulary ruling (register 3.3). Both changes are applied to bible §8.3 and logged at register 6.5.

Direct shard purchases above the packs continue the ladder: $49.99 → 9,000 (180/$), $99.99 → 20,000 (200/$). Geneticist Tier 12's +15% applies on top.

---

## 10. The payer ceiling

At $99.99 for 20,000 shards, a full Gene Lab is about **$8,000** bought outright. That is not alarming, because Core needs milestones and milestones cannot be bought. A spender compresses shards and timers to nothing and then stands at the same campaign wall as everyone else: **nine months** to Core 12, no faster, at any spend.

Money therefore buys a core player about **sixteen months** on a full Gene Lab (25 → 9) and the optimiser about five. That is the intended shape. If the milestone gate is ever softened or sold around, this collapses to pay-to-win in one patch.

---

## 11. The terminal sink

A core player completes the Gene Lab around month 25 and shard demand falls off a cliff. Every remaining sink is small. Income keeps arriving, and the map — harvesting, relocating, contesting — quietly stops mattering for the players with the most invested.

**Calibration.** Post-tier-12, each facility accepts repeatable refinements at a flat **50,000 shards for +0.5%** to its effect, uncapped. Diminishing in practice, infinite in principle, and it adds rate rather than reach, so §7.7 holds: a Calibrated Array harvests faster; a Calibrated Chamber does not raise the generation ceiling. Calibration is excluded from Chamber and Hatchery for that reason.

Ship it at launch although nobody reaches it for two years. Retrofitting a terminal sink into a live economy means inflating costs on players who paid old prices or adding a currency, and both are worse. Needs a line in bible §7.3.

---

## 12. Guardrails

- Free-play income spread never exceeds 6× between lightest and heaviest engaged player
- Depletion tracks extraction, never wall-clock; Common Veins never deplete
- Offline accrual cap never drops below 12 hours
- Shard value per dollar rises monotonically across the ladder; a pack may break it downward only when it is not selling shards
- The campaign milestone gate on Core is never bypassable by purchase
- Samples are generous; generation is the ceiling. Never fix a coverage problem by cutting sample income
- Every Core milestone grants a named tier-II so no counter depends on luck
- Catalysts are never sold
- No sink introduced later grants combat power, roster beyond Hatchery, trait access, or generation
- Every sink has a free path; money buys the same thing sooner

---

## 13. Open questions

1. **Is 40/hr the right base?** A scaling anchor. Set against target session length in soft launch, not in advance.
2. **Apex capture at 30%.** If soft launch shows higher, cap Apex yield per player per week rather than tuning capture down — a cap holds the 6× spread more reliably, at the cost of the rush feeling less rewarding.
3. **Sample weighting toward the roster** (§7.6) plus named milestones may over-supply the counters and under-supply the four utility traits. Watch utility-trait tier distribution at day 30; if it lags the counters by more than a tier, un-weight Rich Deposit T2 drops.
4. **Rich Deposit pool at 55,000** assumes three Arks at design load. If servers run hot, lower occupancy first, then the pool; never raise the pool, which makes rotation irrelevant.
5. **Calibration at 50,000 flat** may be cheap by year three at tripled Array income. A slow ramp is safer and harder to communicate.
