# Broodline — Campaign Structure

*Design spec, the progression spine*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Rewritten against six species and eight raiders. Replaces the 65-wave version
> written against ten chassis and twelve archetypes.

---

## 1. What this document owns

The campaign is the only content in the game that is the same for every player. Everything else — roster, region, alliance — diverges. This is the shared spine, and three other documents now resolve against schedules only it can supply.

| Schedule | Wanted by |
|---|---|
| **Twelve Core milestone waves** | The anti-whale guardrail at bible §7.3. Core tiers cannot be bought past because each needs one |
| **Species-guarantee waves** | Constraint 2 at bible §4.4. `broodline_base_stock.md` §5 states the mechanism and leaves the wave numbers here |
| **The designed Courser loss** | Bible §9.3. The wave a player is meant to lose, and where Pale arrives |
| **Raider introduction order** | Bible §4.8's rule that one new raider arrives at a time |

It also owns the replay economy, which is what makes bible §8.2's guarantee real — at zero charges there is always something to do.

---

## 2. Shape

> **Sixty waves across eight chapters.**

Eight chapters because there are eight raiders and bible §4.8 introduces one at a time. Sixty waves because twelve Core milestones need spacing that is neither cramped nor a drought, and because the fastest possible completion has to land near nine months for the payer ceiling at `broodline_economy_model.md` §7 to hold.

| Chapter | Waves | Lanes | Raider introduced | Ark integrity | Core milestones |
|---|---|---|---|---|---|
| **1** | 1–5 | 1 | **Skirmisher** (w1) · **Lash** (w2) | 2 → 3 | 1 at w3 |
| **2** | 6–12 | 1 | **Courser** (w6 — *the designed loss*) | 2 → 4 | 2 at w8 · 3 at w12 |
| **3** | 13–20 | 1 → 2 (w16) | **Brood** (w14) | 4 → 6 | 4 at w17 · 5 at w20 |
| **4** | 21–28 | 2 | **Drift** (w22) | 6 | 6 at w25 |
| **5** | 29–36 | 2 | **Bulwark** (w30) | 6 | 7 at w33 |
| **6** | 37–44 | 2–3 | **Delver** (w38) | 8 | 8 at w41 |
| **7** | 45–52 | 3 | **Breaker** (w46) | 8 | 9 at w48 · 10 at w52 |
| **8** | 53–60 | 3 | — all eight in combination | 10 | 11 at w56 · 12 at w60 |

**Ark integrity rises with the chapters.** Per `broodline_combat_numbers.md` §2 a raider reaching the Ark costs integrity rather than ending the wave, and campaign waves author their own pool. Early waves at 2 are crisp puzzles where a single leak matters; late waves at 10 are attrition where the question is how much gets through. That progression is a difficulty dial that costs nothing to build and it is what lets chapter 1 be tight without being cruel.

**Lane count is the other dial**, rising 1 → 2 → 3 across the campaign. Bible §4.2 makes campaign terrain authored per wave rather than derived from the Ark's current region, so the campaign teaches each lane count before the map charges a player for not knowing it. By the time a player relocates to a three-lane Outer Reach region they have fought on three lanes for twenty waves.

### 2.1 Raider order

| Order | Raider | What it teaches | Counter | Held by |
|---|---|---|---|---|
| 1 | **Skirmisher** | Raiders path; creatures fire on their own; volume is a threat | Splash | Ember |
| 2 | **Lash** | Placement has risk. Something is shooting back, and it reaches past the front | Taunt | Vetch |
| 3 | **Courser** | **The counter system exists.** See §4 | Chill | Pale |
| 4 | **Brood** | A death can create more raiders | Cinder | Ember |
| 5 | **Drift** | Some raiders cannot be touched at all without the answer | Reach | Hollow |
| 6 | **Bulwark** | Hit count matters, not damage | Sprint | Skitter |
| 7 | **Delver** | The front line is not the whole board | Burrow | Loam |
| 8 | **Breaker** | The hardest lock in the game, taught last | Pierce | Hollow |

**Breaker is last on purpose.** It is the most punishing raider to meet without its answer — a wall that simply does not fall — and Hollow is Founder 1, so by wave 46 a player has carried Pierce for six weeks and only needs to work out that this is what it is for. Meeting Breaker at wave 6 instead would be the counter lesson taught as a brick wall rather than as a rule.

**Skirmisher and Lash are first for the opposite reason.** `broodline_combat_numbers.md` §6.1 names them the two softest locks — both are answerable by enough raw output. That is exactly what a player needs in the first hour, when they may hold the answer without knowing what it is.

---

## 3. Session one, wave by wave

Bible §9.2's eight beats resolve against waves 1 and 2. Stated here because the campaign owns the content those beats run on, and because bible §10.9 fixes the outcome of the tutorial splice.

| Beat | Wave | Content |
|---|---|---|
| 1 | **Wave 1** | Two creatures handed over — **a Vetch and an Ember**, unnamed sample stock. One instruction: place them |
| 2 | Wave 1 | Six Skirmishers. Ember's Splash catches them. The player watches and wins |
| 3 | — | **Founder 1 awarded: a Hollow** |
| 4 | — | Founder naming, one creature, with a skip path and a good default |
| 5 | **Wave 2** | Skirmishers and one Lash, three creatures deployed. Vetch's Taunt holds the Lash. Nobody is told about counters |
| 6 | — | **The tutorial splice.** The Vetch and the Ember, guided, with **Splash locked**. The Hollow is visibly locked out of both parent slots |
| 7 | — | Scripted mutation, guaranteed |
| 8 | — | Lineage View — a two-generation tree with the named Hollow beside it |

**Session one ends here, after wave 2**, and that is a scheduling requirement rather than a pacing preference. The tutorial splice consumes both parents, taking a three-creature roster to two; Founder 2 arrives on day 1. If wave 3 sat inside session one the player would fight it with fewer creatures than wave 2 — a difficulty spike that reads as the game punishing them for following the tutorial. **Wave 3 opens session two, with Founder 2 already in the roster.**

**The tutorial splice produces Cinderplate**, the Vetch-bodied hybrid at bible §10.9 that is the game's mascot and its store icon. Locking Splash rather than leaving it to the roll is what makes that deterministic: the player's first hybrid is always a wall that answers swarms, and it is always an animal they made.

**The two tutorial parents are not Founders.** Bible §9.5 requires the parents to be provided specifically for the splice and framed as sample stock, and the named Founder to be visibly locked out. Founder 1 is the Hollow awarded at beat 3 — the species with the most distinctive silhouette and two of the eight counters, so the creature a player names is also the one they will still be using in month three.

---

## 4. Wave 6 — the designed loss

Bible §9.3 requires that a player loses a wave for lack of the right trait: early, safely, with the reason named, and with the answer available within minutes. It does not say which wave. **Wave 6 is authored to be lost, and the raider is Courser.**

**Why Courser.** Per `broodline_base_stock.md` §5, the five Founders carry seven of the eight counters. The missing one is Chill, and Pale is deliberately the sixth species. Courser then does three things nothing else does: it ignores Taunt, so the Vetch wall does not save the player; it crosses a lane in fifteen seconds, so raw damage does not save them either; and its answer sits on the one species they do not have. The lesson lands as **"I need a Pale"** rather than "I got unlucky."

**How it is authored.**

- Wave 6 runs at **integrity 2** and sends **one Courser** behind a screen of Skirmishers
- One Courser reaching the Ark costs 2 integrity. The wave is lost
- **Wave Defeat names Courser and names Chill**, per bible §4.11. This is the screen the whole combat model rests on and this is the moment it earns its place
- **The Wave Defeat screen grants the Pale**, framed as a Warden resupply rather than a consolation
- **Free retry**, immediately, with the Pale in the roster

The player loses a wave, is told exactly why, is handed the answer, and wins the retry inside two minutes. That is the counter system taught in one beat, at a cost of nothing, at the moment bible §9.6 schedules it.

**No paywall on failure, here or anywhere.** A retry costs nothing. There is no attempt limit and no entry fee.

**If the wave is somehow won**, the Pale grant fires on clearing wave 6 instead. The beat is degraded, not broken — but the wave should be tuned so this effectively does not happen, because the lesson is the point and the win teaches nothing.

---

## 5. The mutual staircase

Rescued verbatim from the previous version, because it is the most important structural fact in the game and it is not obvious.

> Campaign waves are hard for under-powered rosters.
> Roster power comes from trait coverage, which is capped by generation.
> Generation is capped by Splicing Chamber tier.
> Splicing Chamber cannot exceed Core tier.
> Core tier requires a campaign milestone.

Each rung requires the one below it. A player who buys unlimited Gene Shards on day one can max the Splicing Chamber to its Core cap, splice at speed, and then meets a wave their creatures cannot beat — because the generations they would need sit behind a Core tier that sits behind a milestone that sits behind the wave.

**This must be soft gating, not a stat check.** No wave displays "requires Generation 8." Waves are tuned so a G5 roster loses and a G8 roster wins, with skill and composition moving the boundary by a generation either way. A visible requirement reads as a lockout; difficulty tuned against expected roster power reads as a challenge.

**Nine-month floor.** The fastest conceivable completion — unlimited spending, optimal play, no wasted splices — should land near nine months. If a spender clears sixty waves in three, the payer ceiling collapses and the anti-whale structure fails.

**The staircase now has a second rung it did not have before.** `broodline_sample_economy.md` §8 established that base stock, not charges, gates splice cadence. So money buys charges a player cannot spend, and the real constraint on climbing the staircase is creature supply — which comes from waves, harvest and milestones, none of which are purchasable. The gate is stronger than it was when it rested on Core tiers alone.

---

## 6. Milestones and guarantees

Three separate reward tracks land on specific waves. Keeping them separate matters: a player should be able to see which is which.

### 6.1 Core milestones

Twelve, one per Core tier, at the waves in §2. Spacing is close to linear — roughly every five waves, tightening slightly at the start. **The steepening happens in the Gene Shard curve, not in the wave gaps**, so a player never faces a shard wall and a long campaign drought at the same moment.

**Core tier 12 landing on wave 60 is deliberate.** The last rung of the game's longest progression track sits behind the campaign's final wave, which by chapter 8 sends all eight raiders in combination — which is to say, behind proof that the player engaged with the breeding system rather than stacking one species.

### 6.2 Species guarantees

Constraint 2's mechanism, per `broodline_base_stock.md` §5.

| Species | Granted | When |
|---|---|---|
| Vetch, Ember | Tutorial sample stock, consumed at beat 6 | Wave 1 |
| **Hollow** | **Founder 1**, named | Wave 1, beat 3 |
| **Vetch** | Founder 2 | Day 1 |
| **Ember** | Founder 3 | Day 2 |
| **Skitter** | Founder 4 | Day 2 |
| **Loam** | Founder 5 | Day 3 |
| **Pale** | **Wave Defeat grant** | Wave 6 |

All six species inside the first week. All eight counters, at tier I, free, on a schedule with no roll in it.

**After that, the scarcity ratchet.** Each chapter completion grants one Gen-1 creature of the species the player currently holds fewest of — eight grants across the campaign, running for the life of the account through seasonal chapters. It is the anti-lockout mechanism working past onboarding, and it corrects a player who has spliced their way into a single-species roster before a wave does it for them.

### 6.3 First-clear rewards

| Reward | Value |
|---|---|
| Gene Shards | Scaling with wave number |
| Gen-1 creature | One, uniform across all six species |
| Samples | Two, tier I, unweighted |
| Chapter completion | A larger shard grant, a cosmetic, and the scarcity-ratchet creature |

---

## 7. Replay

Bible §8.2 guarantees that a player at zero charges always has something to do, and bible §4.8 says replay "has to genuinely work." Replay is what delivers both, and it is also the primary base stock source once the campaign runs out of first clears.

| | Value |
|---|---|
| Gene Shards | **20%** of first-clear value |
| Samples | One, tier I |
| Gen-1 creature | **One in three replays** |
| Cap | **Three replays per wave per day** |

**The creature drop is the important line.** `broodline_base_stock.md` §3 puts waves at four creatures a day for a core player — that has to come from somewhere once wave 60 is cleared. Twelve replays a day at one in three is exactly four, and six replays a day gets a casual player two. The numbers were set against each other and should move together.

**The cap matters as much as the reward.** Uncapped replay becomes a grind treadmill that competes with harvesting and makes the map economy optional. Three per wave, across a growing library of cleared waves, is generous without being the optimal way to earn — a player with forty cleared waves has a hundred and twenty replay slots a day and will never use them all, which is the correct shape.

**Replay never drops a species the player is short of preferentially.** It is uniform, like first clears. The ratchet at §6.2 is the visible mechanism and it should stay the only one.

---

## 8. Failure

**A failed wave costs regeneration timers and nothing else.** Retry immediately with a different roster.

Campaign is a puzzle, not a gamble. A player who loses learns which counter they were missing — the Wave Defeat screen tells them — and comes back with a different five. Attaching any resource cost to failure converts experimentation into risk and pushes players toward one safe roster, which is the opposite of what the counter system exists to produce.

**No attempt limits, no energy cost, no entry fee, no paywall.** The only thing between a player and the next wave is whether their creatures can beat it.

---

## 9. Campaign versus region defence

Easily confused, and they do different jobs.

| | Campaign | Region defence |
|---|---|---|
| Terrain | **Authored per wave**, drawn from the eight families | The Ark's current region, exactly as authored |
| Lane count | Authored, 1–3 | The region's, 1–3 |
| Raiders | Fixed, scripted | Scales with region richness — `broodline_region_roster.md` §6 |
| Integrity | Authored, 2–10 | 10 + 2 per Core tier |
| Frequency | Player-initiated | Periodic, automatic |
| Rewards | Shards, creatures, samples, milestones | Shards, samples, holds your position |
| Failure | Regeneration timers | Regeneration timers, harvest interruption |

**Campaign is the ladder. Region defence is the rent.** A player who only runs campaign progresses but builds no economy; a player who only harvests builds an economy and hits a Core wall.

---

## 10. After wave 60

The previous version left this open and it is the sharpest question in the document. A player who finishes the campaign has no milestone track and no Core progression left.

Three things absorb it, and only the third is new:

- **Calibration.** Post-tier-12 repeatable facility refinement, per `broodline_economy_model.md` §8. Absorbs shards and grants rate, never reach. It ships at launch even though nobody reaches it for eighteen months
- **Seasonal chapters.** Waves 61 upward arrive in seasons. **Core stays capped at 12** — seasonal waves grant cosmetics, shards, samples, base stock and Calibration materials, never new facility depth
- **The scarcity ratchet continues.** Each seasonal chapter completion still grants the player's rarest species. It is the one guarantee that never expires

**Seasonal waves never require seasonal content.** No new wave may demand a species or trait introduced in the season that shipped it. That converts a Season Pass into a progression requirement and makes a lapsed month permanently costly, which is the mechanic that turns live-service games into obligations.

---

## 11. Guardrails

- Every Core tier requires a campaign milestone; no purchase bypasses one
- Difficulty gates by tuning, never by a displayed stat requirement
- No attempt limits, energy costs, or entry fees on any wave
- Failure costs regeneration timers and nothing else
- All six species guaranteed inside the first week; the scarcity ratchet runs for the life of the account
- One new raider at a time, in the §2.1 order
- Escalation is by count, lane and integrity — **never by raider stats.** A wave-60 Breaker has the same 600 HP as a wave-46 Breaker
- Replay always available and always capped
- Seasonal waves never require seasonal content
- Fastest possible completion stays near nine months

---

## 12. Open questions

1. **Is sixty the right count?** It is set by twelve milestones and eight raider introductions, not chosen independently. Fewer means tighter milestone spacing; more means longer droughts between Core tiers.
2. **How is the nine-month floor actually enforced?** The staircase produces it in theory. In practice it depends on difficulty tuning holding against optimal play, and that can only be validated in soft launch.
3. **The wave budget formula needs an anchor.** `broodline_combat_numbers.md` §8 gives Budget(w) = 40 × 1.045^w × L, which does not fund a single Skirmisher unit at wave 1. Recommend **60 × 1.04^(w−1) × L**, so wave 1 is exactly one Skirmisher unit and wave 60 at three lanes funds roughly sixteen Breakers. Campaign waves may deviate from the formula where a beat requires it — wave 6 does — but region defence should follow it.
4. **Does the Pale grant land better on defeat or on retry?** Granting it on the Wave Defeat screen is immediate and satisfies bible §9.3. Granting it on the retry screen separates the loss from the reward, which may teach the lesson more cleanly and risks the player quitting in between.
5. **Chapter 8 has no new raider.** It is the combination gauntlet, which is the right ending, but it is also eight waves with nothing new in them. It may need a signature encounter rather than a difficulty ramp.

---

*Chapters 1 and 2 are authored in `broodline_waves_01_12.md`; chapter 3 in `broodline_waves_13_20.md`.*

*Owns: wave count, chapter structure, raider introduction order, the twelve Core milestone waves, the species-guarantee schedule, the designed Courser loss, first-clear and replay rewards, and the seasonal extension rule. Does not own: raider stats or the wave budget formula (`broodline_combat_numbers.md`), creature supply rates (`broodline_base_stock.md`), or facility costs (`broodline_economy_model.md`).*
