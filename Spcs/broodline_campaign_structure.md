# Broodline — Campaign Structure

*Design spec, the progression spine*

---

## 1. Why This Document Exists

The Gene Vault spec makes a promise the whole monetization structure rests on: **Core tiers require a campaign milestone, so progression cannot be bought past.** A player who spends heavily clears the shard and timer costs and then stops at the same wall as everyone else.

That promise has never been designed. Sixty-five waves is a constant in the reconciliation ledger, twelve Core tiers each need a milestone, the enemy archetype schedule assumes a specific pacing, and the economy model targets a nine-month floor on the fastest possible completion. All of it currently rests on a system that exists only as a number.

The campaign is also the only content in the game that is the same for every player. Everything else — your roster, your region, your alliance — diverges. This is the shared spine.

---

## 2. Shape

**Sixty-five waves across eight chapters.**

Chapters exist because a flat list of sixty-five is not a structure a player can hold. Each chapter is a themed run of six to nine waves with one new enemy archetype, one new mechanic, and a chapter-completion reward.

| Chapter | Waves | Introduces | Terrain family |
|---|---|---|---|
| 1 | 1–6 | Skitter, Bole. Placement and armor. | Defile |
| 2 | 7–14 | Gall, Rill. Enemies attack back; deaths spawn more. | Basin |
| 3 | 15–22 | Mantle. Kill priority. | Fork |
| 4 | 23–30 | Cairn, Flit. Emplacement damage; lane switching. | Terrace |
| 5 | 31–40 | Wither. Armor stripping. | Shelf |
| 6 | 41–50 | Nurse. Damage races. | Flood |
| 7 | 51–58 | Umbra. Sustained over burst. | Scree |
| 8 | 59–65 | Sire, Progenitor. Elites; mixed damage. | Delta |

Terrain families run easiest to hardest, in step with the region roster's eight families. By the time a player relocates to a Delta region in the Outer Reach, they have fought on Delta terrain in chapter eight and know what four lanes demand.

---

## 3. Campaign Terrain — A Resolution

The combat spec derives lane layout from "the region your Ark currently occupies." Applied to campaign waves, that makes campaign difficulty depend on where a player happens to be parked, and balance becomes impossible — the same wave is trivial from a Defile and brutal from a Delta.

**Recommendation: campaign waves have their own authored terrain**, drawn from the same eight families but fixed per wave.

This splits combat into two clean contexts:

| Context | Terrain source | Purpose |
|---|---|---|
| **Campaign** | Authored per wave | Balanced, teaches, gates progression |
| **Region defense** | Your current region | Makes relocation tactical |

Both use the identical engine. The distinction is only where the board comes from, and it preserves what the combat spec actually wanted — that relocating changes your battlefield — while keeping the progression spine tunable. It also lets the campaign teach each terrain family before the map charges you for not knowing it.

---

## 4. The Mutual Staircase

The Vault spec claims campaign gating stops money from skipping progression. The mechanism deserves stating plainly, because it isn't obvious and it's the most important structural fact in the game:

> Campaign waves are hard for under-powered rosters.
> Roster power comes from trait quality, which is capped by generation.
> Generation is capped by Splice Chamber tier.
> Splice Chamber cannot exceed Core tier.
> Core tier requires a campaign milestone.

Each rung requires the one below it. A player who buys unlimited Gene Shards on day one can max the Chamber to its Core cap, splice at speed, and then meets a wave their creatures cannot beat — because the generations they'd need are behind a Core tier that's behind a milestone that's behind the wave.

**This must be soft gating, not a stat check.** No wave should display "requires Generation 8." Waves are simply tuned so that a Gen 5 roster loses and a Gen 8 roster wins, with skill and composition moving the boundary by a generation either way. A visible requirement reads as a lockout and violates the Vault spec's guardrail; difficulty tuned against expected roster power reads as a challenge.

**Nine-month floor.** The fastest conceivable completion — unlimited spending, optimal play, no wasted splices — should land near nine months. If a spender clears sixty-five waves in three, the payer ceiling collapses and the Vault's anti-whale structure fails.

---

## 5. Milestone Map

Twelve Core tiers, twelve milestones.

| Core tier | Milestone wave | Notes |
|---|---|---|
| 1 | 3 | Near-instant build, per Vault §5 |
| 2 | 8 | |
| 3 | 14 | Chapter 2 complete |
| 4 | 20 | |
| 5 | 27 | |
| 6 | 33 | Halfway; timers reach 8hr |
| 7 | 39 | |
| 8 | 45 | **Second concurrent build slot** |
| 9 | 51 | |
| 10 | 56 | |
| 11 | 61 | |
| 12 | **65** | Progenitor. Final wave, final tier. |

Core tier 12 landing on the Progenitor wave is deliberate. The last rung of the game's longest progression track is gated behind the one enemy that demands a mixed-damage roster — which is to say, behind proof that the player actually engaged with the genetics system rather than stacking one damage type.

Milestone spacing is close to linear. The steepening happens in the Gene Shard curve, not in the wave gaps, so a player never faces both a shard wall and a long campaign drought at the same moment.

---

## 6. Rewards

**First clear:**
- Gene Shards, scaling with wave number
- One Gen 1 base-stock creature, uniform random across the ten chassis
- A **guaranteed chassis** at ten specified waves (1, 2, 6, 10, 14, 22, 30, 40, 50, 59), delivering all ten chassis on a front-loaded schedule so role variety arrives early — this is the genetics spec's anti-lockout path
- A Core milestone at the twelve waves above
- Chapter completion: a larger shard grant and a cosmetic

**Replay:**
- **20% of first-clear shard value**
- **Three replays per wave per day**, then that wave yields nothing until tomorrow
- No creature drop

The replay cap matters. The monetization spec guarantees there is always something to do at zero charges, and replay is what delivers it — but uncapped replay becomes a grind treadmill that competes with harvesting and makes the map economy optional. Three per wave across a growing library of cleared waves is generous without being the optimal way to earn.

---

## 7. Failure

Per the combat spec, a failed wave costs only regeneration timers. Retry immediately with a different roster.

That is correct and it should stay. Campaign is a puzzle, not a gamble — the player who loses learns what armor mix they got wrong and comes back with a different five. Attaching any resource cost to failure converts experimentation into risk and pushes players toward one safe roster, which is the opposite of what the damage triangle exists to produce.

**No attempt limits, no energy cost, no entry fee.** The only thing standing between a player and the next wave is whether their creatures can beat it.

---

## 8. Campaign vs Region Defense

Easily confused, and they do different jobs:

| | Campaign | Region defense |
|---|---|---|
| Terrain | Authored per wave | Current region |
| Enemies | Fixed, scripted | Scales with region richness |
| Frequency | Player-initiated | Periodic, automatic |
| Rewards | Shards, creatures, milestones | Shards, holds your position |
| Failure | Regen timers only | Regen timers, harvest interruption |

Campaign is the ladder. Region defense is the rent. A player who only does campaign progresses but doesn't build an economy; a player who only harvests builds an economy and hits a Core wall.

---

## 9. Seasonal Extension

Waves 66+ arrive in seasons, and they inherit the sidegrade rule from the genetics and chassis specs.

- New waves never require chassis or traits introduced in that season
- Core tiers stay capped at 12 — seasonal waves grant cosmetics, shards, and Calibration materials, never new Vault depth
- New enemy archetypes reuse existing bodies unless they're elites

The trap to avoid: seasonal campaigns that require seasonal creatures. That converts a battle pass into a progression requirement and makes a lapsed month permanently costly, which is the mechanic that turns live-service games into obligations.

---

## 10. Guardrails

- Every Core tier requires a campaign milestone; no purchase bypasses one
- Difficulty gates by tuning, never by displayed stat requirements
- No attempt limits, energy costs, or entry fees on any wave
- Failure costs regeneration timers and nothing else
- All ten chassis guaranteed through chapter progression
- Replay always available and always capped
- Seasonal waves never require seasonal content
- Fastest possible completion stays near nine months

---

## 11. Open Questions

1. **Is sixty-five the right count?** It's set by the twelve-milestone requirement and the enemy introduction schedule, not chosen independently. Fewer waves means tighter milestone spacing; more means longer droughts between Core tiers.
2. **How is the nine-month floor actually enforced?** The mutual staircase produces it in theory. In practice it depends entirely on difficulty tuning holding against optimal play, and that can only be validated in soft launch.
3. **Should chapter terrain be fixed or varied within a chapter?** Fixed teaches a family thoroughly; varied is less monotonous across eight waves.
4. **Do campaign waves drop region-weighted chassis?** Currently uniform, per the anti-lockout guarantee. But it means campaign and harvesting have different acquisition logic, which players will notice.
5. **What happens after wave 65 before the first season?** A player who completes the campaign has no milestone track and no Core progression left. Calibration absorbs shards but grants no goal.

---

*Remaining undocumented: live-ops event specs, moderation and UGC policy, the D14–D90 retention arc, and the monetization rewrite.*
