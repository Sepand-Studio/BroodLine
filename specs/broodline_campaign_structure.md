---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_campaign_structure_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  The progression spine, reconciled to bible §1.2 (six species), §4.4 (eight
  raiders), §4.5 (wave rules), §7.3 (Core milestones) and §9.3–9.4 (first loss,
  Founders). Settles the wave count at 60 across seven chapters. Volume numbers
  are soft-launch starting values, not decisions.
---

# Broodline — Campaign Structure

*Design spec, the progression spine. Companion to bible §4.8 and §7.3.*

---

## 1. Why this document exists

Bible §7.3 makes the promise the whole monetization structure rests on: **every Core tier requires a campaign milestone, so progression cannot be bought past.** The bible states the promise and nothing designs the campaign that delivers it. This does.

The campaign is also the only content identical for every player. Roster, region and alliance all diverge; this is the shared spine, and it is where the eight raider-to-counter associations are taught one at a time.

---

## 2. Shape

**Sixty waves across seven chapters.**

Sixty rather than sixty-five: the older figure was set by a twelve-archetype enemy schedule that no longer exists. Eight raiders introduced one at a time across seven chapters, with twelve Core milestones at close-to-even spacing, lands naturally at sixty. Sixty-five would add a chapter with nothing new to introduce.

| Chapter | Waves | Introduces | Answered by | Lanes | Types per wave |
|---|---|---|---|---|---|
| 1 | 1–8 | **Skirmisher**, then **Breaker** at wave 3 | Splash · Ember, Pierce · Hollow | 1 | 1–2 |
| 2 | 9–16 | **Lash** | Taunt · Vetch | 1 | 2 |
| 3 | 17–24 | **Courser** | Chill · Pale | 1 | 2–3 |
| 4 | 25–32 | **Brood** | Cinder · Ember | 2 | 3 |
| 5 | 33–40 | **Drift** | Reach · Hollow | 2 | 3–4 |
| 6 | 41–50 | **Bulwark** | Sprint · Skitter | 3 | 4 |
| 7 | 51–60 | **Delver** | Burrow · Loam | 3 | 4 |

Each chapter is a themed run: one new raider, one lesson about it, and a chapter-completion reward. Chapter 1 introduces two raiders because the designed first loss (§9.3) needs an unanswerable raider inside session two or three, and Skirmisher alone cannot be unanswerable — every Founder set carries Splash.

**Three difficulty dials, applied in this order.** Chapters 1–3 escalate by raider count and tighter simultaneity on one lane. Chapters 4–5 add the second lane. Chapters 6–7 add the third. Types-per-wave rises alongside and reaches the §4.5 four-type cap at wave 40, after which every wave sends four types and difficulty is volume alone.

---

## 3. Campaign terrain

Bible §4.2 derives lane count from the region the Ark occupies. Applied to campaign waves that makes wave 30 trivial from a one-lane Common Vein and brutal from a three-lane Apex region, and balance becomes impossible.

**Campaign waves carry their own authored lane count**, fixed per wave, per the table above. Region defence uses the current region's lanes.

| Context | Lane source | Purpose |
|---|---|---|
| **Campaign** | Authored per wave | Balanced, teaches, gates progression |
| **Region defence** | Current region | Makes relocation tactical |

Same engine, same rules, different source for the board. There are no terrain families — the bible removed terrain modifiers, so the campaign teaches lane count and nothing else about the ground. The older "eight terrain families" framing is gone from here and needs to go from the region roster too.

---

## 4. Raider introduction order

The order is not arbitrary. Each raider teaches a rule the next one depends on.

| Raider | Lesson | Why here |
|---|---|---|
| Skirmisher | Placement. Volume. | Answered by every Founder set; safe to learn placement on |
| Breaker | **A defence without the answering trait does not work** | The designed first loss. Pierce is the one counter the Founders lack |
| Lash | The front line exists to protect what is behind it | Taunt is a positioning lesson; comes after placement is solid |
| Courser | Damage cannot substitute for the answer | Chill is the purest counter in the game; nothing kills a Courser in time |
| Brood | Kill order matters; tier decides coverage | Cinder I catches the first split, Cinder III the second — the first wave where sample fusing visibly changes the outcome |
| Drift | Walls are not enough | Reach on the second lane forces the first genuine breadth problem |
| Bulwark | Sustained pressure, not burst | Sprint's rapid hits are the only thing that degrades the shield; a Rally-timing lesson |
| Delver | Formation depth | Burrow is the last counter because it punishes the tidy front-line habit every earlier chapter built |

**Species pairing note.** Skirmisher and Brood are both answered by Ember; Breaker and Drift both by Hollow. This is legal under §4.5 — the rule forbids two raiders sharing a *trait*, not a species — and it rewards a player who has bred an Ember carrying both Splash and Cinder, or a Hollow carrying Reach and Pierce. That is the "efficient species" advantage §1.2 describes, and the campaign makes it visible by pairing those raiders from chapter 4 onward.

---

## 5. The mutual staircase

The mechanism behind §7.3 deserves stating plainly. It is the most important structural fact in the game.

> Waves are lost on **volume**: more raiders than the player's coverage handles.
> Coverage is capped by **generation** (§2.5).
> Generation is capped by **Splicing Chamber** tier.
> Splicing Chamber cannot exceed **Core** tier (§7.3).
> Core requires a **campaign milestone**.

Each rung requires the one below it. A player who buys unlimited Gene Shards on day one maxes the Chamber to its Core cap, splices at speed, and meets a wave sending more Coursers than Chill II slows — because Chill III needs a generation behind a Chamber tier behind a Core tier behind a milestone behind that wave.

**This is soft gating and must stay soft.** Tier I always works; a wall that stops turning is a paywall wearing a difficulty curve (§4.5). What the staircase controls is *how many* raiders a defence handles, never *whether* it can. No wave displays a generation requirement. Waves are tuned so that a roster at the expected coverage wins and a roster a tier short loses on volume, with placement and Instinct moving the boundary.

**Nine-month floor.** The fastest conceivable completion — unlimited spending, optimal play, no wasted splices — should land near nine months. This is produced by the staircase in theory and by difficulty tuning in practice; it can only be validated in soft launch.

---

## 6. Milestone map

Twelve Core tiers, twelve milestones.

| Core tier | Wave | Notes |
|---|---|---|
| 1 | 2 | Near-instant build. Lands in session one, before the Gene Lab is introduced (§9.6 session 3) — the upgrade is waiting when the screen opens |
| 2 | 6 | |
| 3 | 10 | Chapter 2 |
| 4 | 15 | |
| 5 | 20 | |
| 6 | 25 | Chapter 4. Second lane. Timers cross into hours |
| 7 | 30 | Halfway |
| 8 | 35 | **Second concurrent build slot** (§7.4) |
| 9 | 40 | Four-type cap binds from here |
| 10 | 46 | |
| 11 | 53 | |
| 12 | 60 | Final wave, final tier |

Spacing is near-linear, widening by one wave per tier at the top. The steepening lives in the Gene Shard curve and the timers, not the wave gaps, so a player never meets a shard wall and a campaign drought in the same week.

Core 12 on wave 60 is deliberate. The last rung of the longest progression track sits behind the wave that demands all four types across three lanes at tier-III coverage — proof the player engaged with splicing rather than bought around it.

---

## 7. Species guarantee

Bible §9.4: five Founders across six species; the sixth arrives as an early milestone. Bible §1.2: acquiring all six yields all eight counters.

**The Founders are Vetch, Ember, Skitter, Loam and Pale. The missing species is Hollow.**

Hollow carries Pierce, and Pierce answers Breaker, and Breaker is the designed first loss at wave 3. **Wave 4 grants a Gen-1 Hollow.** The loss screen names the raider and the trait; the next wave hands over the species. "I need a Hollow line" is the first goal the game gives, and it is answered within minutes.

Hollow being the efficient two-counter species makes it the right one to withhold: the player wants it most, and it is the clearest early demonstration that species are the unit of acquisition.

**Re-supply.** Splicing consumes both parents, so a player can breed away their only Chill carrier. The guarantee has to hold against that. Each chapter-completion reward grants a Gen-1 creature of a **fixed species**, cycling so every species is guaranteed at least once by chapter 6, with Hollow and Ember — the two-counter species — appearing twice:

| Chapter complete | Guaranteed species |
|---|---|
| 1 (wave 8) | Vetch |
| 2 (wave 16) | Ember |
| 3 (wave 24) | Pale |
| 4 (wave 32) | Hollow |
| 5 (wave 40) | Skitter |
| 6 (wave 50) | Loam |
| 7 (wave 60) | Ember |

Alongside these, ordinary first-clear base stock is uniform random across the six.

---

## 8. Rewards

**First clear**

- Gene Shards, scaling with wave number
- Tier-I samples, **unweighted** — bible §7.6 keeps campaign drops unweighted so a player can build toward something new
- A Gen-1 base-stock creature at every fourth wave (4, 8, 12 …), uniform random except at the guaranteed waves above
- Core milestone at the twelve waves in §6
- Chapter completion: guaranteed-species creature, a larger shard grant, a cosmetic

**Replay**

- 20% of first-clear shard value
- The reduced sample drip §4.8 promises, so a player at zero charges always has a source
- Three replays per wave per day, then nothing until tomorrow
- No creature drop

The replay cap matters. Replay is the zero-charge guarantee, but uncapped it becomes a grind that competes with harvesting and makes the map optional. Three per wave across a growing library of cleared waves is generous without being the best way to earn.

---

## 9. Failure

A failed wave costs regeneration timers and nothing else (bible §4.11). Retry immediately with a different five.

Campaign is a puzzle, not a gamble. A player who loses learns which counter they were missing and comes back. **No attempt limits, no energy, no entry fee.** Attaching any cost to failure converts experimentation into risk and pushes players toward one safe roster, which is the opposite of what hard counters exist to produce.

---

## 10. Wave 50

One wave in the campaign is authored so the intended answer is a **utility trait** rather than a counter.

Wave 50 is a three-lane attrition wave: Lash and Skirmisher across all lanes, sustained rather than spiking, with the Ark under pressure long enough that a front line without **Carapace** or **Regrow** wears through even with Taunt and Splash in hand. The counters are all present; what decides it is survivability.

This is the only wave that does this, and that fact is the campaign's contribution to the open trait-utility problem. Four traits counter nothing; if the campaign never rewards them, nobody learns to build for them, and the roster collapses toward Ember and Hollow. Wave 50 is where a player first discovers that Vetch's Carapace is worth having for its own sake. If playtest shows the wave is winnable without any utility trait, the utility traits are too weak — that is the test.

---

## 11. Volume tuning

Every number here is a soft-launch starting point, per bible §7.6.

**Wave budget** is the working measure: `B(w, L) = B₀ × g^(w−1) × L`, with `B₀ = 100`, `g = 1.04`, and raider costs starting at Skirmisher = 10. Budget sets how much a wave can send; the type rules in §4.5 and the lanes in §2 set what it sends.

Two things follow from the shape:

- **The budget stops being the binding constraint at wave 40.** From there, four types on three lanes is the ceiling composition, and the budget only scales the count of each. Tuning above wave 40 is about coverage tier demanded, not about what the wave contains.
- **Chapters 1–3 are budget-bound**; the interesting tuning is there, where one lane and few types mean the count of each raider is what the player feels.

The actual per-raider costs, per-wave counts and coverage-tier expectations belong in a combat numbers table, not here.

---

## 12. Campaign vs region defence

| | Campaign | Region defence |
|---|---|---|
| Lanes | Authored per wave | Current region's, 1–3 |
| Raiders | Fixed, scripted | Scales with region richness |
| Frequency | Player-initiated | Periodic, automatic |
| Rewards | Shards, samples, creatures, milestones | Shards, holds position |
| Failure | Regen timers only | Regen timers, harvest interruption |

Campaign is the ladder. Region defence is the rent. A player who only does campaign progresses but builds no economy; a player who only harvests builds an economy and hits a Core wall.

---

## 13. Seasonal extension

Waves 61+ arrive in four-week seasons (bible §8.3) under two rules from §4.12:

- **The counter pool is closed.** A new raider must be answered by one of the existing eight counters, or ship with its answer already in general supply. No seasonal wave may require a seasonal species or trait.
- **Core stays capped at 12.** Seasonal waves grant cosmetics, shards, samples and catalysts — never Gene Lab depth.

The trap: seasonal campaigns that require seasonal creatures turn a Season Pass into a progression requirement and make a lapsed month permanently costly. That is the mechanic that turns a live-service game into an obligation.

**After wave 60, before the first season.** A player who finishes has no milestone track left. Replay and region defence absorb shards; alliance content absorbs time. That is acceptable for the soft-launch window and not acceptable at scale — the first season should be live before any cohort reaches wave 60, which the nine-month floor gives comfortable room for.

---

## 14. Guardrails

- Every Core tier requires a campaign milestone; no purchase bypasses one
- Tier-I answers always work; difficulty is volume, never resistance
- No wave displays a generation or tier requirement
- No attempt limits, energy costs or entry fees
- Failure costs regeneration timers and nothing else
- All six species guaranteed by chapter 6, Hollow by wave 4
- Never two raiders answered by the same trait in one wave; never more than four types
- Replay always available and always capped
- Seasonal waves never require seasonal content
- Fastest possible completion stays near nine months

---

## 15. Open questions

1. **Founder set.** Vetch, Ember, Skitter, Loam, Pale, with Hollow withheld, is the pick. It makes Breaker the first loss and Hollow the first goal. The alternative — withhold Pale, make Courser the first loss — teaches the purer lesson but delays it to chapter 3, which is too late for §9.3.
2. **Is wave 50 enough?** One utility-trait wave in sixty is a token. If the utility traits get real weight, chapters 6 and 7 should each carry one.
3. **Base-stock cadence.** Every fourth wave is a guess. It should be set against the Hatchery floor of 20 and the splice-charge regen so a player is neither starved of parents nor drowning in Gen-1 stock.
4. **Post-60 window.** Confirm the first season ships before the first cohort's projected wave-60 date.
