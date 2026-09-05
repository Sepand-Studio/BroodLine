# Broodline — Wave Scaling Addenda

> **§5.3 and §5.4 are retracted.** The Bastion is not a purchasable module. It
> is free and equals Core tier, per `broodline_bastion_economy_check.md`. The
> mechanism in §5.1 — twelve levels, +20% damage each, damage only — and the
> ratio table in §6 both stand unchanged. §1 to §4 are unaffected.

*Closes the four open items in `broodline_wave_scaling_audit.md` §7 and specifies the Bastion module.*

Supersedes the audit's §5 and §6 where they differ — the lane schedule and the Bastion growth rate both moved once the schedule was aligned to chapter boundaries rather than assumed.

---

## 1. Campaign Length — 65 Waves, Confirmed

The constants ledger says 65, the milestone map is built for 65, and the nine-month completion floor and twelve Core tiers are both derived from it. A later note proposed 60 across seven chapters; that would require retuning the milestone map, the Bastion curve and every ratio below to save five waves.

**65 waves across eight chapters stands.** This closes one of the two items logged as pending sign-off.

---

## 2. Coverage Tiers, Restated as Proportions

Replaces bible §1.3's absolute counts. This is the single most important change in the set and it costs nothing.

| Tier | Covers |
|---|---|
| **I** | 25% of that raider type in the wave |
| **II** | 40% |
| **III** | 60% |

**Wave-1 behaviour is unchanged.** Bible §1.3 says Splash I catches two of the eight Skirmishers a wave sends and Splash III catches five. Two of eight is 25%; five of eight is 62.5%. The bible wrote proportions and called them counts.

Everything §1.3 promises survives intact:

- Tier decides how much a trait covers, never whether it works
- Any Chill stops a Courser — a tier-I answer is never nothing
- No raider is ever immune to a tier-I answer at any wave number
- Later waves pressure with more, never with resistance

And the four-type cap now falls out of the arithmetic instead of being asserted. Full coverage of one raider type needs 100%, so two carriers per type — a III and a II, or two IIIs. Four types is eight carriers, which is eight of the ten trait slots a five-creature deployment carries, leaving two for utility. That is exactly the composition §4.5 describes.

**Rounding:** coverage rounds up, always. A tier-I Splash against three Skirmishers covers one, not 0.75. This guarantees a tier-I answer is never zero at any wave size, which is the promise the whole system rests on.

---

## 3. Lane Schedule

Lanes follow chapter boundaries so that a lane increase always coincides with a new terrain family and a new archetype. The player is never asked to learn two things at once, and the wave after a lane increase is always the chapter wall.

| Chapters | Waves | Terrain | Lanes |
|---|---|---|---|
| 1–2 | 1–14 | Defile, Basin | **1** |
| 3–5 | 15–40 | Fork, Terrace, Shelf | **2** |
| 6–7 | 41–58 | Flood, Scree | **3** |
| 8 | 59–65 | Delta | **4** |

Four lanes in chapter 8 matches the campaign structure spec, which already promises that a player reaching a Delta region on the map has fought Delta terrain and knows what four lanes demand.

This corrects the audit, which assumed three lanes from wave 40. Wave 40 is the last two-lane wave; wave 41 is the first three-lane one.

---

## 4. Wave Duration Targets

Duration is set by lane count, not wave number. More lanes means a wider board and a longer traverse.

| Lanes | Target duration |
|---|---|
| 1 | 25 s |
| 2 | 45 s |
| 3 | 70 s |
| 4 | 95 s |

These are the figures the ratios in §6 are computed against. A ten-second change to a duration is roughly a 10–15% change in required DPS, which makes this the cheapest tuning lever in the game — worth remembering before anyone touches the budget formula.

**Risk:** 95 seconds is a long single wave for a mobile session, and chapter 8 is seven of them. If playtest says it drags, cut chapter 8 to three lanes rather than shortening the duration — the ratio is more sensitive to duration than to lane count.

---

## 5. The Bastion

*(Retracted. Kept for the record of what was considered.)* A seventh module in the Gene Lab, alongside Core, Harvest Array, Hatchery, Gene Vault, Splicing Chamber and Drive.

### 5.1 What it does

> **Twelve levels. +20% damage to every deployed creature per level above the first.**

Level 1 is 1.00×; level 12 is 7.43×. It applies to campaign waves and region defence. **Not to raids, in either direction** — amended by `broodline_four_decisions.md` §3, which keeps Core's combat effect confined to PvE per the Gene Vault guardrail.

**Damage only.** The Bastion never grants a trait, never raises coverage, never modifies hit points, range or attack speed. It is a single multiplier applied at damage resolution, which is what keeps it clear of the hard-counter guarantee: an unanswered raider stays unanswered at any multiplier, because Courser's threat is specified as damage that cannot kill it in time and scaling both sides preserves that exactly.

This is why the Bastion is safe and creature stat growth would not be. A creature that gets tougher and hits harder each generation eventually beats a Breaker without Pierce. A uniform output multiplier applied to a defence that scales alongside the offence never does.

### 5.2 Why it exists

Species stats §6 leaves the player's raw output flat across the entire campaign — generation, growth, traits and Instinct all grant nothing permanent, and Broodline Affinity's +5% is the only stat modifier in the game. Meanwhile wave volume grows to roughly 26× wave 1. Something has to scale and output is the only thing that can scale safely.

### 5.3 Why it is not attached to Core milestones directly

Core tier N is granted *by* the wave that needed it: tier 8 arrives at wave 45, so wave 40 is fought at tier 7, and tier 12 arrives at wave 65, so the final wave is fought at tier 11. Any track handed out by milestones arrives one rung late, every time.

The Bastion resolves this the way every other Vault module already does: **the level is purchased with Gene Shards, and Core tier sets the cap.** A player sitting at Core 7 buys Bastion 7 and holds it *during* wave 40 rather than being handed it at wave 45.

The mutual staircase is preserved exactly. Bastion cannot exceed Core, Core cannot exceed the milestone, and the milestone is behind the wave. Money still buys rate and never reach.

### 5.4 Cost curve

Base 600, ×1.70 per level.

| Level | Multiplier | Cost | Cumulative |
|---|---|---|---|
| 1 | 1.00× | 600 | 600 |
| 2 | 1.20× | 1,020 | 1,620 |
| 3 | 1.44× | 1,734 | 3,354 |
| 4 | 1.73× | 2,948 | 6,302 |
| 5 | 2.07× | 5,011 | 11,313 |
| 6 | 2.49× | 8,519 | 19,832 |
| 7 | 2.99× | 14,483 | 34,315 |
| 8 | 3.58× | 24,620 | 58,935 |
| 9 | 4.30× | 41,855 | 100,790 |
| 10 | 5.16× | 71,153 | 171,942 |
| 11 | 6.19× | 120,960 | 292,902 |
| 12 | 7.43× | 205,631 | 498,533 |

**Against the economy model.** A Core player earns ~3,570/day, roughly 2.25M across the 21-month Core arc. The Bastion's full 499k is about 22% of that — a major sink without dominating the Array.

The shape is deliberate. Everything through level 7 costs 34,315 total, about 1.5% of lifetime income, so the Bastion is effectively free through the first five chapters. That matters because §6 shows the tightest ratios land at waves 20, 30 and 45 — all in the cheap half. **The audit's assumption that a player's Bastion level tracks their Core cap holds precisely where the campaign is hardest**, and stops holding only in chapter 8, where the ratios have headroom.

Levels 10 through 12 carry 73% of the total cost and are the endgame shard sink the Vault currently lacks after Core 12.

### 5.5 PvP

~~Bastion applies to raids on both sides. Raid matchmaking is banded at ±2 Core tier, so the widest legal mismatch is 1.44×.~~ **Superseded.** The Bastion does not apply to raids at all. Both sides fight at baseline stat lines and coverage does all the work — see `broodline_four_decisions.md` §3.

---

## 6. Result

Bastion level available *during* each wave, given the milestone map and the schedules above.

| Wave | Lanes | Bastion | Budget | Required DPS | Supply | Ratio |
|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 100 | 200 | 350 | 1.75 |
| 14 | 1 | 2 | 167 | 333 | 420 | 1.26 |
| 15 | 2 | 3 | 346 | 385 | 504 | 1.31 |
| 20 | 2 | 3 | 421 | 468 | 504 | **1.08** |
| 30 | 2 | 5 | 624 | 693 | 726 | **1.05** |
| 40 | 2 | 7 | 838 | 931 | 1,045 | 1.12 |
| 41 | 3 | 7 | 1,295 | 925 | 1,045 | 1.13 |
| 45 | 3 | 7 | 1,458 | 1,041 | 1,045 | **1.00** |
| 50 | 3 | 8 | 1,690 | 1,207 | 1,254 | 1.04 |
| 58 | 3 | 10 | 2,141 | 1,529 | 1,806 | 1.18 |
| 59 | 4 | 10 | 2,940 | 1,547 | 1,806 | 1.17 |
| 65 | 4 | 11 | 3,510 | 1,847 | 2,167 | 1.17 |

Budget retains the audit's knee: 1.04 per wave through wave 30, 1.03 from wave 31.

**Wave 45 is the hardest wave in the game** at a ratio of exactly 1.00, with waves 20 and 30 close behind. All three sit at the end of a two-lane run where the next Bastion level has not been earned. Wave 45 also carries the Core 8 milestone and the second concurrent build slot, so the game's tightest fight is the one that unlocks its biggest quality-of-life step — which is the right place for it.

**The margin at those three waves is the utility traits.** A ratio of 1.00 means a composition that does everything right barely clears. Plating III cutting incoming damage 40% keeps bodies firing longer; Screen III buys the back line acquisition-free seconds; Rally is specified at roughly 10% of a wave's outcome; Broodline Affinity adds 5%. A deployment using its two spare trait slots clears wave 45 with room. One that does not, loses.

Wave 1 at 1.75 is deliberate headroom for the FTUE.

---

## 7. Remaining Open

**1. Bastion competes with Harvest Array for the same early shards.** Both are the obvious first investment and the economy model's sink table does not yet include the Bastion. Needs a pass against §4 of the economy model before the curve is final.

**2. 95-second waves in chapter 8.** Flagged in §4. Watch in playtest; the fix is lane count, not duration.

**3. ~~The Bastion is a new screen.~~ Resolved — it is not.** With the Bastion free and tied to Core tier, the Gene Lab layout is unchanged and the value belongs as one line on the Core module readout.

**4. Coverage rounding at low counts.** §2 rounds up, which means a tier-I answer against a single raider covers it entirely. Correct for the guarantee, slightly generous in early waves — acceptable, but worth watching in the FTUE where waves are small.

---

*Downstream: bible §1.3 replaced by §2 above, campaign structure gains §3 and §4, Vault spec gains the Bastion, economy model §4 gains a sink, screen inventory gains a screen.*
