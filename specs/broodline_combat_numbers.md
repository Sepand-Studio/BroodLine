# Broodline — Combat Numbers

*Design spec, the values under bible §1 and §4*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Every number here is a starting point for soft-launch tuning. **The structures
> are decisions; the values are not.** What must not move without a conversation
> is marked.

---

## 1. Why this document exists

Bible §1.3 explains coverage tiers with one worked example per trait and no numbers. Bible §4.4 lists eight raiders, a threat in five words each, and an answering trait. Bible §1.2 gives six species a role and a colour.

None of that can be built, tuned, or balanced. Four other documents are blocked behind it: the campaign wave tables, the region roster's difficulty by band, the Trait Codex's entries, and the raider roster. And the design's largest known risk — that four of twelve traits counter nothing and may therefore be worthless — cannot even be assessed until those four traits have effects.

This document supplies: six species stat profiles, a power-budget rule that keeps them equal, twelve traits with a concrete effect at each of three coverage tiers, six Instincts with numbers, eight raider profiles with the mechanic each one's counter actually answers, the generation-to-coverage table, and a wave budget formula.

---

## 2. The baseline

Everything is expressed against one unit, so that changing it moves everything together.

> **Baseline creature: 100 HP, 20 damage per hit, 1.0s interval, range 3.**

| Quantity | Value | Note |
|---|---|---|
| Lane length | **24 tiles**, spawn to Ark. Two families run 18 | Pockets sit beside tiles 6–20 |
| Pockets per lane | **4–6**, set by the region's terrain family | One creature per pocket. Families at `broodline_region_roster.md` §3 |
| Base raider walk speed | **0.6 tiles/sec** | 40 seconds to cross a full lane |
| Target wave length | **75–100 seconds** | Bible §4.1 targets ninety-second sessions |
| Deployment cap | **5 creatures** | Bible §4.3. Do not move without re-running §9 |
| Rally | 4s of doubled attack speed, once per wave | Bible §4.3 |

**Ark integrity.** A raider that reaches the Ark costs integrity rather than ending the wave outright. Integrity reaching zero loses it.

| | Integrity | Set by |
|---|---|---|
| Campaign wave | Authored, **2 early rising to 10** | The wave — schedule at `broodline_campaign_structure.md` §2 |
| Region defence | 10 + 2 per Core tier | Core facility — bible §7.2 |
| Raid defence | 6, fixed | Neither side's facilities |

Most raiders cost **2 integrity**. Skirmishers cost **1** — eight of them arriving is a real threat without being an instant loss, which is what lets a partial Splash answer still be a partial answer. Breakers and Bulwarks cost **4**.

This gives Core's integrity stat a job in PvE and only in PvE, per bible §7.2, and it means a wave is lost by degrees rather than by one leaked body.

---

## 3. The six species

**Stat profiles.** Damage is per hit; DPS is derived.

| Species | Role | HP | Damage | Interval | DPS | Range | Budget |
|---|---|---|---|---|---|---|---|
| **Vetch** | Wall | 260 | 14 | 1.5s | 9.3 | 2 | 3.38 |
| **Ember** | Splash | 130 | 36 | 1.7s | 21.2 | 3 | 3.42 |
| **Skitter** | Swarm | 80 | 11 | 0.4s | 27.5 | 2 | 3.40 |
| **Hollow** | Sniper | 60 | 55 | 2.5s | 22.0 | 7 | 3.40 |
| **Loam** | Support | 190 | 18 | 1.2s | 15.0 | 3 | 3.40 |
| **Pale** | Control | 120 | 21 | 1.1s | 19.1 | 5 | 3.41 |

### 3.1 The sidegrade envelope

Bible §4.12 promises that new species in later seasons are sidegrades and that a launch-era Vetch stays viable in year two. That promise needs a rule or ordinary power creep breaks it by the third season.

> **Budget = HP/100 + DPS/10 + (Range − 3) × 0.15**
>
> **Every species sums to 3.40, ±0.05.**

| Species | HP term | DPS term | Range term | Total |
|---|---|---|---|---|
| Vetch | 2.60 | 0.93 | −0.15 | **3.38** |
| Ember | 1.30 | 2.12 | 0.00 | **3.42** |
| Skitter | 0.80 | 2.75 | −0.15 | **3.40** |
| Hollow | 0.60 | 2.20 | +0.60 | **3.40** |
| Loam | 1.90 | 1.50 | 0.00 | **3.40** |
| Pale | 1.20 | 1.91 | +0.30 | **3.41** |

A new species may redistribute freely inside the budget — that is how it becomes interesting — but the total is fixed. **Check every proposed species against this before art begins**, because a species that fails the envelope after modelling will ship anyway.

Two things the envelope deliberately does not price: the two traits a species carries, and its Instinct. Traits are where the real difference lives, and they are bred rather than granted, so pricing them into the body would double-count.

### 3.2 Reading the profiles

**Vetch is not a bad attacker, it is a wall with a stick.** At 260 HP it survives roughly three times what Hollow does, and its range of 2 means it must be placed in the pockets closest to the lane. That is the point: Vetch holds the front and Taunt makes raiders look at it.

**Hollow is the glass cannon and the reason pocket placement matters.** Range 7 covers most of a lane from one pocket; 60 HP means anything that reaches it kills it. Hollow wants a Vetch or a Screen carrier in front.

**Skitter's 0.4s interval is the whole species.** It is why Sprint lives here — shield-stripping is a function of hit count, not damage, and Skitter lands 2.5 hits a second.

**Ember's 1.7s interval is deliberately slow.** Splash multiplies damage across targets, so a fast splash creature would trivialise every swarm wave. The slow swing is what keeps Splash a partial answer at tier I.

**Loam is the highest-HP non-wall and the lowest-DPS non-support.** It exists to sit in a pocket and survive, which is what a Burrow carrier and a Regrow carrier both need to do.

---

## 4. The twelve traits

### 4.1 The grammar

Bible §1.3 is the rule this whole section implements: **tier decides how much a trait covers, never whether it works.** Any Chill stops a Courser. Chill III stops four of them.

Ten of the twelve scale on one of three axes. Naming the axis is what makes the Codex teachable:

| Axis | Traits | What tier buys |
|---|---|---|
| **Simultaneity** | Pierce, Taunt, Chill, Sprint, Burrow, Splash | How many raiders of that type the creature answers at once |
| **Depth** | Cinder, Reach | How far into the threat the answer reaches — split generations, lanes of sky |
| **Magnitude** | Carapace, Regrow, Screen, Litter | How much of the effect |

**Six of eight counters scale by simultaneity, which is exactly how bible §4.5 escalates waves — by volume.** Later waves send more Coursers, not tougher ones, and Chill II is the answer to two rather than a better answer to one. That correspondence is the single most important thing in this document and it should survive any tuning.

### 4.2 The eight counters

| Trait | Species | Answers | Tier I | Tier II | Tier III |
|---|---|---|---|---|---|
| **Splash** | Ember | Skirmisher | Hits **2** targets within 1 tile | **3** targets | **5** targets, radius 2 |
| **Pierce** | Hollow | Breaker | Ignores Plate on **1** Breaker at a time | **2** | **3** |
| **Taunt** | Vetch | Lash | Forces **1** Lash to target this creature | **2** | **4** |
| **Chill** | Pale | Courser | Slows **1** Courser to 0.5 t/s | **2** | **4** |
| **Cinder** | Ember | Brood | Burns the **first** split generation at birth | First, and **half** the second | **Both** generations |
| **Reach** | Hollow | Drift | Can target Drift in **1** lane | **2** lanes | **3** lanes |
| **Sprint** | Skitter | Bulwark | Degrades shield **100** per hit, **1** Bulwark at a time | **2** | **3** |
| **Burrow** | Loam | Delver | Forces **1** Delver to surface at the lane's midpoint | **2** | **3** |

**No counter has a damage component.** A Pierce carrier does not hit harder; it makes its existing damage land on a Breaker at all. This is what keeps counters from becoming a power ladder and what makes the four utility traits competitive for a slot.

**Splash is the one to watch.** It is the only counter that also helps against everything else, because area damage is generically useful. Tier III at radius 2 hitting five targets is close to a general DPS multiplier on Ember's 36-damage swing. If Ember runs away with the meta in playtest, the lever is Splash III's radius, not Ember's stats.

### 4.3 The four that counter nothing

Bible §1.2 states the requirement plainly: *the four utility traits must pull real weight,* or the four single-counter species become "the one trait I need in a body I don't want" and the roster collapses toward Ember and Hollow.

They are given weight here by being pointed at the four things that actually hurt in this design.

| Trait | Species | Effect | Tier I | Tier II | Tier III |
|---|---|---|---|---|---|
| **Carapace** | Vetch | Incoming damage reduction | **−25%** | **−40%** | **−55%** |
| **Regrow** | Loam | Between-wave heal, and regeneration timer cut | Heals **30%** max HP · regen **−25%** | **60%** · **−50%** | **Full heal** · **−75%** |
| **Screen** | Pale | Damage reduction for creatures in adjacent pockets | **−30%**, one adjacent pocket | **−45%**, both adjacent | **−60%**, both adjacent, and intercepts Lash aimed at them |
| **Litter** | Skitter | Passive Gen-1 base stock, capped by the Hatchery | One every **8h** | **6h** | **4h** |

**Why each is worth a slot:**

**Regrow attacks the worst friction in the game.** Bible §4.11 puts a fallen creature in regeneration for 20–60 minutes, and bible §1.5 is explicit that nothing may make a counter carrier unavailable when a wave demands it. Regeneration is the closest the design comes to violating its own constraint. Regrow III cuts a 60-minute wait to 15 and fully heals between waves, which means a Regrow line is a line that is always available. That is not a nice-to-have under hard counters; for a player with exactly one Chill carrier it is close to mandatory.

**Screen protects the counter carriers.** Hollow at 60 HP carries two of the eight counters. A Pale with Screen III sitting between two Hollows cuts their incoming damage by 60% and pulls Lash fire off them entirely. The trait's value scales with how thin the creatures it protects are, and this design made creatures deliberately thin.

**Carapace is the reason Vetch can hold a front at all.** 260 HP at −55% is an effective 578 against most raider damage, which is what a wall needs to be when Lash is hitting it for 30 every two seconds.

**Litter feeds the consumption economy directly.** Splicing destroys both parents (bible §2.1), so base stock is the real constraint on the loop, not charges. A Litter III carrier produces six creatures a day. That is a farm, and it is the closest thing in the game to an engine — which is why it sits on a combat trait slot rather than being free.

**The honest tension: Litter does nothing in combat.** A creature carrying Litter and one counter is, in a wave, a creature with one trait. That is a real cost and it is the intended trade — the player is choosing throughput over board strength. It is also the utility trait most likely to be wrong, and the first thing to watch in playtest. If Litter carriers are never deployed, the trait has quietly become a non-combat trait occupying a combat slot, and it should either move to the Instinct axis or be replaced with a survivability effect.

### 4.4 Aberrant traits

Bible §1.6: outside the coverage ladder, mutation-only, and **none may ever counter a raider.**

**Eight at launch. Three combat, five Instinct.** The Instinct-side ones are the chase items, because a creature that behaves in a way no bred lineage can produce is more distinctive in play than one covering slightly more sky.

| Aberrant | Kind | Effect |
|---|---|---|
| **Kindle** | Combat | Killing a raider deals 15 damage to everything within 2 tiles |
| **Ossify** | Combat | Below 30% HP, becomes immune to damage for 3s. Once per wave |
| **Graft** | Combat | On this creature's death, the nearest ally heals to full |
| **Vigil** | Instinct | Does not act until three raiders are in range, then attacks at ×2 speed for 5s |
| **Mourn** | Instinct | +30% damage for the rest of the wave when an ally falls |
| **Contrary** | Instinct | Targets whatever the fewest other allies are targeting |
| **Tide** | Instinct | Alternates every 8s between +40% range and +40% attack speed |
| **Rooted** | Instinct | Never moves, never repositions, and cannot be targeted by Lash |

*Rooted was called Stillborn in an earlier draft. In a game about breeding animals, at a 12+ rating, that was a poor word in English and worse translated literally — see `broodline_localization.md` §3.1.*

None answers a raider. All are visible in a replay, which is what makes them worth screenshotting.

---

## 5. Instinct

Six behaviours, untiered, inherited independently of the body (bible §1.4). Numbers where the bible gives none.

| Instinct | Targets | Trigger | Response |
|---|---|---|---|
| **Bloodscent** | Lowest current HP in range | — | — |
| **Vanguard** | Closest to the Ark | — | — |
| **Overwatch** | Furthest in range | — | +25% range, −20% attack speed |
| **Last Stand** | Nearest | Self below 25% HP | +50% attack speed |
| **Skittish** | Nearest | Self below 40% HP | Repositions to an adjacent free pocket, 2s move, cannot act while moving |
| **Pack Sense** | Nearest | Adjacent to a same-species ally | +15% damage to both |

**Retarget delay is 0.4s** for every creature. This matters more than it looks: it is why eight Skirmishers beat a single-target defender who could out-DPS them on paper, and therefore why Splash is a counter rather than a convenience.

**None is strictly best, and the numbers are set to keep that true.** Overwatch trades 20% of its rate for range, which is superb down a long lane and wasted at a chokepoint. Bloodscent finishes wounded bodies and wastes swings on a single armoured leader. Skittish saves a creature and costs two seconds of fire — an attacking Skittish creature will retreat mid-raid, which is the drawback bible §4.9 calls interesting.

**Pack Sense at +15% is the only Instinct that rewards roster shape**, and it pulls against the counter system's demand for breadth. Both are correct. A player running three Skitters and a Hollow is making a real bet.

---

## 6. The eight raiders

Each has one mechanic, and its counter answers that mechanic rather than adding damage. Integrity cost is what reaching the Ark takes from §2.

| Raider | HP | Speed | Integrity | Mechanic | Answered by |
|---|---|---|---|---|---|
| **Skirmisher** | 40 | 0.9 | 1 | Arrives in **eights**, 1.5s apart. No attack. Retarget delay means single-target defenders lose bodies through the gaps | **Splash** |
| **Breaker** | 600 | 0.35 | 4 | **Plate** — every incoming hit is capped at 5 damage. Ignores Taunt | **Pierce** |
| **Lash** | 180 | 0.5 | 2 | Attacks the **furthest** defender in range 5 for 30 every 2s, reaching past the front line into support pockets | **Taunt** |
| **Courser** | 220 | **1.6** | 2 | Crosses a lane in 15 seconds. Ignores Taunt. Not killable in time by ordinary DPS | **Chill** |
| **Brood** | 150 | 0.55 | 2 | On death splits into **3** Broodlings at 50 HP; each splits into **3** Mites at 20 HP. One body becomes thirteen | **Cinder** |
| **Drift** | 130 | 0.7 | 2 | **Airborne.** Flies straight to the Ark ignoring lane geometry, and is untargetable without Reach | **Reach** |
| **Bulwark** | 300 | 0.4 | 4 | **Shield 400.** Damage does not pass it — it is not an HP pool. It degrades **only from hits by a creature carrying Sprint**, at 100 each | **Sprint** |
| **Delver** | 200 | 0.6 | 2 | **Submerged for the entire lane**, untargetable, surfacing at the Ark. Visible as a moving disturbance | **Burrow** |

### 6.1 Why each lock is honest

The design rule is that a wrong-trait defence *does not work*, not that it works badly. Checking each:

**Breaker.** At a 5-damage cap, a full deployment of five creatures at ~19 average DPS deals roughly 5 damage per hit across maybe 6 hits/sec = 30 DPS against 600 HP: 20 seconds, while the Breaker crosses in 68. So an unanswered Breaker *can* be ground down on a long lane — which is wrong. **Set the cap at 5 and Breaker HP at 600 only if lanes stay at 24 tiles.** The safer form, and the recommended one: **Plate caps incoming at 5 per hit and Breaker regenerates 15 HP/sec.** Now the cap is a genuine lock — no amount of unpierced damage outpaces regeneration — and Pierce turns it off. *This is the single most fragile number in the document.*

**Bulwark.** ~~400 shield at 25 per hit~~ — **revised, and the revision is structural.** Under the original rule an unaided Skitter stripped the shield in 6.4 seconds and killed the body in eleven, with no Sprint anywhere; raising the shield to 600 would have made that 9.6 seconds and changed nothing. The deeper problem was that **Sprint was the only counter whose species answered the raider without carrying the trait** — Sprint's mechanic is Skitter's base attack rate.

Now: **the shield blocks damage outright and degrades only from Sprint hits, at 100 each.** Four hits to break, on any body. That makes Bulwark and Breaker the matched pair their shared Hauler body implies — Plate caps damage and Pierce lifts the cap; Shield blocks damage and Sprint strips the shield — and it makes Sprint work on any species, which matters because traits are bred onto bodies. Found by authoring `broodline_waves_29_36.md` §5.

**Delver is the remaining raider worth re-checking on the same basis.** Its submersion looks like a sound binary. So did Bulwark's shield.

**Courser.** 15 seconds to cross, 220 HP, and a full deployment cannot reliably focus it in that window while also handling the rest of a wave. Chill halves its speed to 30 seconds, which is enough. Honest lock.

**Drift.** Untargetable without Reach is a hard binary, and correctly so. It is also the raider most likely to feel unfair, so bible §4.11's Wave Defeat screen naming the answering trait matters most here.

**Delver.** ~~Submerged for 70% of the lane~~ — **revised.** Surfacing at tile 17 left 11.7 seconds targetable against 200 HP, which five creatures kill without Burrow every time. It now travels submerged end to end and surfaces at the Ark, with Burrow forcing it up at the midpoint. Found by authoring `broodline_waves_37_44.md` §5.

**Three of eight locks were soft, and all three failed the same way.** Breaker, Bulwark and Delver were each specified as a *condition* — armoured, shielded, submerged — with a plausible number attached, and in all three the arithmetic let ordinary damage through the side. The five that were sound were written as rules: Courser is a rule about time, Brood about what happens on death, Drift about what can be targeted, and Skirmisher and Lash are soft by design.

> **A lock is only a lock if it is written as a rule about damage or targeting. A description of the raider's condition is not a rule.**

All eight now pass. Any seasonal raider should be checked against this before its stats are set.

**Brood.** Thirteen bodies from one is the volume threat. Cinder I catching the first generation reduces it to four bodies; Cinder III to one. Honest, and the tier scaling is genuinely felt.

**Lash and Skirmisher** are the two softest locks — both are answerable by enough raw output. That is acceptable for exactly two of eight, and they are deliberately the two that appear earliest in the campaign, where a player may not yet hold the answer.

**One raider carries two locks.** The Sunder at wave 60 is a Hauler at 1.5× scale wearing both Plate and Shield, answered by Pierce and Sprint together. It is a deliberate single exception, it appears in exactly one wave, and the guardrails keeping it singular are at `broodline_raider_roster.md` §7.

---

## 7. Generation and the coverage ceiling

Bible §2.5: generation gates how high coverage can climb, and samples fill toward it. Neither reaches the top alone.

| Generation | Coverage ceiling |
|---|---|
| G1 – G2 | Tier I |
| G3 – G4 | Tier II |
| G5 + | Tier III |

**Splicing Chamber sets the maximum generation a creature can reach**, so the facility gates the ceiling that gates the coverage:

Splicing Chamber costs 60% of Core at the same tier — the second most expensive facility in the game, because it gates the ceiling that gates everything.

| Splicing Chamber tier | Max generation | Effective coverage ceiling |
|---|---|---|
| 1–2 | G2 | Tier I |
| 3–5 | G4 | Tier II |
| 6–8 | G6 | Tier III |
| 9–12 | G9 | Tier III, with deeper lines for Affinity |

Above G6 the ceiling stops rising. Depth past that buys Broodline Affinity reliability (bible §3.4) and lineage, not coverage — which is what stops generation from becoming an uncapped power axis while keeping a deep line worth having.

**Sample cost to fill the ceiling.** Three samples fuse to the next tier (bible §7.6), so a tier-III trait costs nine tier-I samples. A creature carrying two combat traits at tier III therefore represents eighteen tier-I samples and a G5 line. That is the intended shape of a two-month project.

---

## 8. Wave budget

A wave is a threat budget spent on raiders, subject to two hard rules from bible §4.5.

> **Budget(w) = 100 × 1.04^(w−1) × L**
>
> where *w* is the wave number and *L* is the lane count.

Anchored so that **wave 1 at one lane funds the six Skirmishers bible §9.2 opens with, with room alongside.** Two earlier forms did not: a base of 40 did not fund the first wave at all, and a base of 60 with Skirmishers priced as a unit of eight left no granularity below wave 20 — a Lash cost 60 against a wave-2 budget of 62. Both were found by authoring waves 1–12 against them.

| Raider | Cost |
|---|---|
| **Skirmisher** | **10 each** |
| Lash | 60 |
| Drift | 65 |
| Delver | 75 |
| Courser | 85 |
| Brood | 90 |
| Bulwark | 120 |
| Breaker | 145 |

**Skirmishers are priced individually, not as a unit.** A quantum of 60 has no granularity at any budget below 200, which is every wave in the first two chapters.

The 1.04 growth rate puts wave 20 at roughly 2.1× wave 1 and wave 60 at three lanes at roughly 3,000.

**That number cannot be read as a wave.** Twenty Breakers is not a legal composition — it exceeds the four-type cap's practical shape and it puts more of one raider on the board than any coverage tier answers. **From wave 40 the four-type cap binds and the budget does not.** The arithmetic ceiling of a legal three-lane wave is **1,665** — three each of Breaker, Bulwark and Brood plus sixty Skirmishers. Wave 51 reaches 1,565 of that, and **nothing later in the campaign can exceed it by composition.** Chapter 8's escalation therefore comes from integrity, spawn ordering and the Sunder rather than from contents. Chapters 7 and 8 are authored against the type cap with budget checked afterward. **Growth is in count, never in raider stats.** A wave-60 Breaker has the same 600 HP as a wave-46 Breaker; there are simply more of them across more lanes.

**Campaign waves may deviate from the formula where an authored beat requires it.** Wave 6 does — see `broodline_campaign_structure.md` §4. Region defence follows the formula without exception.

**Two authored deviations are rules rather than exceptions.** Waves 1–5 are hand-set tutorial content and run far over; the formula should be treated as governing from wave 6. And **a wave carrying a Core milestone may run up to 15% over budget**, because a chapter's closing wave should feel like a climax and the formula does not otherwise allow one. Both come from `broodline_waves_01_12.md` §5.

**The budget is a ceiling on threat, not a target to hit.** A wave introducing something new should sit well under it — chapter 3's lane change runs at 68% and its Brood introduction at 90% — and only consolidation and milestone waves should approach or exceed it. Chapters 1 and 2 were authored as though budget were a target, which is why they look anomalous beside chapter 3; the correction is to how the formula is read rather than to the formula.

**Counts of the same raider do not add threat linearly.** Splash catches two, three or five targets per swing whether twelve or twenty Skirmishers are on the board, and Taunt pins one Lash whether there are two or four. Stacking a raider well past the coverage tier that answers it adds cost without adding difficulty — which is why a wave with a narrow palette has to overspend to feel like anything, and why the first two chapters run hot. Bible §4.5 is unambiguous that a wall which stops turning is a paywall wearing a difficulty curve.

**Two rules bound every legal wave:**

1. **Never two raiders answered by the same trait.** Skirmisher and Brood are both volume threats but answered differently, so both may appear; two Courser *types* cannot exist because there is only one.
2. **Maximum four raider types.** Five creatures at two combat traits is ten slots, but no player reliably holds ten distinct counters in one deployment. Four is what the sample economy can supply.

### 8.1 Auditing the four-type cap

Bible §4.5 asserts four is the ceiling. Checking it against the species roster:

A player needs four specific counters simultaneously. Best case, counters cluster: Ember carries Splash and Cinder, Hollow carries Pierce and Reach. So a wave of Skirmisher + Brood + Breaker + Drift is answerable by **two creatures**, leaving three deployment slots free.

Worst case, they scatter: Courser (Pale) + Lash (Vetch) + Bulwark (Skitter) + Delver (Loam) needs **four different species**, one counter each, leaving one slot free and no room for a utility trait carrier that isn't also a counter carrier.

**The worst case is tight but legal, and it is tight in the right way** — it is the wave that forces a player to own all six species rather than the two efficient ones. Wave authoring should reach the four-species case rarely and deliberately, not routinely.

**A five-type wave would be illegal even in the best case**, because the fifth type requires a fifth counter and at most two creatures can carry doubled-up counters. Four is correct.

---

## 9. What must not move

- **Deployment cap of 5.** The four-type audit at §8.1, the roster floor of 20, and board legibility on a phone all derive from it
- **Coverage tiers never gate access.** Any tier of the answering trait turns the lock
- **Counters carry no damage component.** The moment Pierce also hits harder, the utility traits are dead and the roster collapses
- **Wave escalation is by count only.** No raider ever gains HP, resistance, or immunity with wave number
- **The species budget of 3.40.** Check every seasonal species against §3.1 before art begins
- **No Aberrant counters a raider**
- **Exactly one dual-lock raider exists**, at wave 60, and it is a variant rather than a body

## 10. What to watch first in playtest

In order of how much rides on it:

1. **Litter, in both directions.** Whether carriers ever get deployed — if not, the trait is a non-combat effect occupying a combat slot — and whether Litter III at six creatures a day is half a core player's base stock supply from one trait
2. **Whether Breaker's Plate is a real lock**, per §6.1. It is the most fragile number here
3. **Whether Splash III is a general DPS multiplier** rather than a counter. Ember running away with the meta is the tell
4. **Whether an unaided Skitter trivialises early Bulwark waves**
5. **Whether the four-species worst case at §8.1 lands as a challenge or a wall** for a player at roster floor

---

## 11. Open questions

1. **Regrow's regeneration cut may be too strong.** At tier III it removes most of the cost of losing a creature, which is the only cost combat has. It may need a floor — no timer below 10 minutes regardless of Regrow.
2. **Should Litter output count against the Hatchery cap or pause at it?** Pausing is kinder; counting is what stops a Litter III player from never harvesting base stock again.
3. **Skirmisher's eight bodies arriving 1.5s apart** is a spawn pattern, not a stat, and it is doing most of the work in making Splash necessary. Worth testing at 1.0s and 2.0s before anything else in §6 is tuned.
4. ~~**Instinct source weighting.**~~ **Set** — signature 35%, the other five 13% each, with per-species signatures at `broodline_base_stock.md` §6.
5. ~~**Aberrant frequency.**~~ **Set** — a 5% sub-roll inside the 9% mutation rate, with catalysts raising it to 50% for three splices. `broodline_sample_economy.md` §9. A core player finds one roughly every five weeks; eight Aberrants is a multi-year set.
6. **Litter III may be too strong.** At one Gen-1 creature every four hours it produces six a day against a core player's twelve, which makes a single trait half of a player's entire base stock supply. This only became visible from the economy side — see `broodline_sample_economy.md` §8.

---

*Owns: species stats, trait coverage values, Instinct numbers, raider profiles, the generation ceiling, the wave budget. Does not own: sample drop rates and the Aberrant sub-roll (`broodline_economy_model.md`), the campaign wave tables (pending rewrite), or region lane counts (pending rewrite).*
