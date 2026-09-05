# Broodline — Base Stock Supply

*Design spec, where creatures come from*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Every number here is a starting point for soft-launch tuning. **The structures
> are decisions; the values are not.**

---

## 1. Why this document exists

`broodline_sample_economy.md` §8 found that base stock, not Splice Charges, sets how often a player splices. That makes it the binding number in the economy: it sets mutation cadence, therefore Aberrant supply, therefore the rate at which coverage is created and destroyed. Every economic figure in the set has been resting on a working estimate of it.

It also carries the load for the design's second structural guarantee. Bible §4.4: *acquiring all six species yields all eight counters, and campaign milestones grant specific species so no player is locked out by luck.* Nothing has ever specified those milestones, so the guarantee has been an intention rather than a mechanism.

And it answers a problem the superseded chassis roster identified and nothing since has addressed: **bodies only ever leave a roster.** A splice consumes two creatures and the child takes one parent's body, so a player splicing toward Hollow holds nothing but Hollows within a couple of months — and then meets a wave that punishes them for it. Supply is what keeps all six circulating.

---

## 2. What a Gen-1 creature is

| Property | Value |
|---|---|
| **Species** | One of six, set by the source |
| **Combat traits** | **Always its species' pair**, both at coverage tier I |
| **Instinct** | One of six, rolled on the weights at §6 |
| **Generation** | 1 |
| **Coverage** | Tier I on both traits. Never higher |
| **Aberrant** | **Never.** Aberrants enter only through mutation, and mutation is a splice mechanic |

**The trait pair is not rolled.** A Gen-1 Vetch carries Carapace and Taunt. A Gen-1 Hollow carries Reach and Pierce. This is what makes bible §4.4's guarantee mechanically true rather than probabilistically likely — acquiring the species *is* acquiring its counters, with no roll in between.

It also gives base stock a clean job against hybrids. Base stock is broad and shallow: every counter, none of it deep. Hybrids are narrow and deep. The whole progression is trading the first for the second.

| Species | Combat traits | Counters carried |
|---|---|---|
| **Vetch** | Carapace, **Taunt** | Lash |
| **Ember** | **Cinder**, **Splash** | Brood, Skirmisher |
| **Skitter** | **Sprint**, Litter | Bulwark |
| **Hollow** | **Reach**, **Pierce** | Drift, Breaker |
| **Loam** | Regrow, **Burrow** | Delver |
| **Pale** | Screen, **Chill** | Courser |

---

## 3. Supply rates

Daily, by the three archetypes in `broodline_economy_model.md` §3.

| Source | Casual | Core | Optimiser | Species distribution |
|---|---|---|---|---|
| Campaign and replay waves | 2.0 | 4.0 | 5.0 | **Uniform** |
| Region defence | 0.5 | 1.0 | 1.5 | **Uniform** |
| Node harvesting | 1.0 | 4.0 | 8.0 | **Region-weighted** |
| Campaign milestones (amortised) | 0.3 | 0.5 | 0.7 | **Guaranteed specific** |
| Litter trait, if carried | — | 2.0 | 4.0 | **The carrier's species** |
| **Daily total** | **~3.8** | **~11.5** | **~19.2** |

Against `broodline_sample_economy.md` §8's splice cadence of 2 / 6 / 10 per day, consuming twice that:

| | In | Out | Net |
|---|---|---|---|
| Casual | 3.8 | 3.6 | +0.2 |
| Core | 11.5 | 12.0 | −0.5 |
| Optimiser | 19.2 | 20.0 | −0.8 |

**Every archetype runs at roughly break-even, and the two heavier ones run slightly negative.** That is correct and it is the pressure the whole map economy exists to create. A player who wants to splice more than they are supplied has exactly three levers: harvest more, relocate to better ground, or breed Litter into a line. All three are the game.

**It also means the roster is a buffer, not a stockpile.** At the Hatchery floor of 20 a core player holds under two days of splice fodder. Bible §7.2's warning that roster capacity must never bind below what the counter system requires is what keeps that from becoming a lockout: six creatures hold all eight counters, five deploy, and the remaining slots are working capital.

### 3.1 Harvest scaling

Node-sourced base stock scales with the **Harvest Array**, at half the rate shards do.

| Array tier | Shard multiplier | Base stock multiplier |
|---|---|---|
| 1 | 1.00× | 1.00× |
| 4 | 1.35× | 1.18× |
| 8 | 2.00× | 1.50× |
| 12 | 3.00× | 2.00× |

Shards are throughput and can spread widely. Base stock is species, and species are counters, so its spread must stay tight — the same reasoning that keeps sample income inside 5× while shard income runs to 6×.

> **Guardrail: wave-completion base stock never scales with any facility, purchase, tier or event.** It is the floor under every player, and it is the only supply line that cannot be accelerated by anything. A player who does nothing but replay cleared waves still receives four creatures a day, uniformly across all six species.

---

## 4. Species distribution

### 4.1 Uniform sources

Wave completion and region defence roll **1/6 each, flat.** No weighting, no pity, no adjustment for what the player already holds.

Flat is deliberate over "weighted toward what you lack." A pity system here would quietly guarantee species, which sounds kinder and would make the campaign milestone guarantees at §5 redundant — and those are visible and legible where a hidden weight is not. The player should be able to see the mechanism that protects them.

### 4.2 Region weighting

Each of the thirty regions weights **two species**, roughly tripling their appearance rate in that region's node harvests.

| | Share of node-sourced base stock |
|---|---|
| The two weighted species | **30% each** |
| The other four | **10% each** |

This is the second reason to relocate, and it has nothing to do with yield. *"I need a Loam line and this region drops them"* is a roster decision on the map screen, and bible §5.7 already puts species weighting on the region tile for exactly this.

**Distribution rules**, carried from the superseded region roster §7 and still correct:

- Every species is weighted in **at least three regions**, with **at least one in each band**. No species is Outer-Reach-only, or a player can be locked out of a counter by not yet being ready for dangerous ground
- Thirty regions at two species each is sixty slots across six species — **ten regions per species**, which is comfortable
- Weighting affects **node harvests only**. Waves stay uniform

**Recommended band spread per species:** two or three Inner, four Mid, three or four Outer. The exact assignment is a content pass and belongs with the twenty-one unauthored regions.

### 4.3 Litter

`broodline_combat_numbers.md` §4.3: a Litter carrier produces one Gen-1 creature of **its own body species** every 8 / 6 / 4 hours by tier, capped by the Hatchery.

**Litter is bred, so it is not stuck on Skitter.** A player who breeds Litter onto a Vetch body farms Vetch. That makes it a genuine answer to the bodies-only-leave problem for whichever species a player most wants to keep circulating, and it is the most interesting thing about the trait.

**It is also the number most likely to be wrong.** Litter III at six a day against a core player's eleven-and-a-half is over half their supply from one trait slot. Two options if playtest confirms it:

- **Cap Litter output at a share of total supply** — messy, invisible, hard to explain
- **Move the tiers to 12 / 9 / 6 hours** — Litter III becomes four a day, a third of supply. Simpler and preferred

Recorded here rather than in the combat spec because it is an economy problem that only shows up from this side.

---

## 5. The guaranteed path

Bible §4.4's second structural guarantee, made mechanical.

### 5.1 The five Founders

Bible §9.4: five Founders across days 0–3, spread across species, with the sixth arriving as an early campaign milestone.

**Before the Founders, two creatures that are not Founders.** Bible §9.5 requires the tutorial splice parents to be provided specifically for it and framed as sample stock, with the named Founder visibly locked out of both slots. Those two are **a Vetch and an Ember**, handed over at wave 1 and consumed at beat 6 to produce Cinderplate — the Vetch-bodied hybrid that bible §10.9 makes the game's mascot.

| Founder | Species | Arrives | Counters it brings |
|---|---|---|---|
| 1 | **Hollow** | Session one, beat 3 — the named one | Reach, Pierce |
| 2 | **Vetch** | Day 1 | Taunt |
| 3 | **Ember** | Day 2 | Cinder, Splash |
| 4 | **Skitter** | Day 2 | Sprint |
| 5 | **Loam** | Day 3 | Burrow |

Five species, **seven of the eight counters.** The missing one is Chill.

**Founder 1 is a Hollow** because the creature a player names in the first five minutes should be one they still field in month three. Hollow has the most distinctive silhouette of the six and carries two of the eight counters, so it stays useful for the life of the account.

### 5.2 Pale, and the designed first loss

**Pale is deliberately the sixth species, and Courser is deliberately the designed first loss.**

Bible §9.3 requires that a player loses a wave for lack of the right trait — early, safely, with the reason named, and with the counter available within minutes. It does not say which raider. Courser is the right one and this is why:

- It ignores Taunt, so the Vetch wall does not save the player
- It crosses a lane in fifteen seconds, so raw damage does not save them either
- Its answer sits on the one species they do not have, which makes the lesson *"I need a Pale"* rather than *"I got unlucky"*
- Chill is the cleanest counter in the game to explain in a single line on the Wave Defeat screen

**The chain:** designed first loss at session two or three → Wave Defeat names Courser and Chill → the next campaign milestone grants a Pale → retry and win. That closes bible §9.3's requirement that the counter be available immediately, and it gives a new player a legible goal — a species they can see they are missing — which bible §9.4 asks for and never supplies.

**Pale is granted by the Wave Defeat screen itself**, framed as a Warden resupply rather than a consolation, with a free retry immediately after. The wave is **6**, authored at integrity 2 so a single Courser reaching the Ark loses it. Full beat at `broodline_campaign_structure.md` §4.

### 5.3 The scarcity ratchet

After all six species are held, **each chapter completion grants one Gen-1 creature of the species the player currently holds fewest of.**

This is the anti-lockout mechanism running for the rest of the game rather than only at onboarding. It costs one line of logic, it is invisible until it matters, and it means a player who has spliced their way into an all-Hollow roster is corrected by the campaign rather than by losing a wave they had no way to answer.

**Ties break toward the species carrying the counter the player has not deployed for longest.** If that is also tied, uniform random.

---

## 6. Instinct roll weights

Bible §1.4: Gen-1 base stock rolls one Instinct, weighted so each species has a signature behaviour it usually carries but can carry any of the six. The weights were left unset.

| Species | Signature Instinct | Why |
|---|---|---|
| **Vetch** | **Vanguard** | Targets closest to the Ark. A wall holds the front |
| **Ember** | **Bloodscent** | Targets lowest current HP. Splash finishing a wounded swarm |
| **Skitter** | **Pack Sense** | +15% when adjacent to same-species. Skitters cluster |
| **Hollow** | **Overwatch** | Furthest in range, +25% range. Range 7 wants more range |
| **Loam** | **Last Stand** | +50% attack speed below 25% HP. The high-HP body that keeps fighting |
| **Pale** | **Skittish** | Repositions below 40% HP. The broad wing arc that will not stand and take it |

> **Weights: 35% signature, 13% each for the other five.**

Thirty-five is deliberately soft. A hard signature — 70% or higher — would make Instinct read as species flavour, which bible §1.3 explicitly rejects: decoupling Instinct from the body is what makes it a breeding target rather than a stat that comes with the shape. At 35% a player sees their signature more often than not across a week of drops, and off-signature rolls are common enough to be a resource rather than an anomaly.

**Every Instinct is reachable on every species from base stock**, which matters because Broodline Affinity applies to Instinct (bible §3.4) and a player breeding toward Overwatch on a Vetch line needs a starting point that does not depend on a splice.

**Instinct weights are identical across all sources.** A milestone Vetch and a harvested Vetch roll the same table. Weighting milestone grants toward signatures would make the guaranteed path also the least interesting one.

---

## 7. Audits

### Constraint 2 — species acquisition guarantees all eight counters

| Species | Counters | Guaranteed by |
|---|---|---|
| Vetch | Taunt | Founder 1 |
| Ember | Cinder, Splash | Founder 2 |
| Hollow | Reach, Pierce | Founder 3 |
| Skitter | Sprint | Founder 4 |
| Loam | Burrow | Founder 5 |
| Pale | Chill | Campaign milestone, §5.2 |

**Eight counters, all six species, none of it luck-dependent.** ✓

Every counter arrives inside the first week, at tier I, on a creature that came free. The chain from there — coverage by fusing, depth by generation — is entirely optional and entirely paid for in time.

### The bodies-only-leave problem

A core player splices six times a day, consuming twelve creatures and producing six. Each child takes one parent's body, so **species diversity drains at roughly six body-exits a day** as a player optimises toward whatever they are breeding.

Against that: four uniform wave drops a day is 0.67 of each species daily, or four to five of each per week. Region-weighted harvest adds four more a day concentrated on two species. And the §5.3 ratchet catches the tail.

**Uniform wave drops alone are sufficient to keep all six species present in a roster indefinitely.** ✓ That is the reason they must never scale with anything.

### Roster capacity

Six creatures hold all eight counters — only Ember and Hollow carry two each, so the other four counters need four separate bodies. Five deploy, overlapping with those six. Garrison and escort commitments come out of the remainder, as does splice fodder.

At the Hatchery floor of **20**, a core player has six committed to counters and fourteen working. That is under two days of splice fodder, which is tight and correct. **It is not a lockout**, because the six counter carriers are never the ones consumed — a player splicing their last Pale is making a choice the confirmation screen names. ✓

---

## 8. Guardrails

- Wave-completion base stock is uniform across all six species and never scales with any facility, purchase, tier or event
- Gen-1 creatures always carry their species' trait pair, both at tier I. The pair is never rolled
- Gen-1 creatures never carry an Aberrant
- Every species is weighted in at least three regions, with at least one in each band
- All six species are guaranteed inside the first week through Founders and one campaign milestone
- The scarcity ratchet runs for the life of the account, not only during onboarding
- Instinct weights are identical across every source
- No species is ever sold, in any pack, at any price — bible §8.6

---

## 9. Open questions

1. **Litter III at six creatures a day** is over half a core player's supply from one trait slot. §4.3 recommends moving the tiers to 12 / 9 / 6 hours if playtest confirms it. This is the first thing to check.
2. **Is 35% the right signature weight?** Too low and species identity dissolves; too high and Instinct stops being worth breeding for. It interacts with Broodline Affinity's +12% and the two should be tuned together.
3. **Does the scarcity ratchet need to be visible?** Silent is elegant and means a player never knows they were helped. Visible — *"the campaign has granted you a Pale, your rarest species"* — teaches the guarantee, which has retention value the silent version does not.
4. **Should region weighting apply to region defence drops** as well as node harvests? Currently no, on the grounds that region defence is the rent and should pay a flat rate. Arguable either way.
5. **A player who never joins an alliance and never leaves the Inner Reach** gets uniform waves plus low-yield harvest, at roughly the casual rate. That is slow, not blocked, per bible §6.10 — but it has never been checked against the campaign's difficulty curve, and it should be before soft launch.

---

*Owns: what a Gen-1 creature is, supply rates and sources, species distribution, the Founder and milestone guarantees, the scarcity ratchet, and Instinct roll weights. Does not own: sample supply (`broodline_sample_economy.md`), Gene Shard income (`broodline_economy_model.md`), the campaign wave numbers the §5.2 chain resolves against (pending rewrite), or the per-region species assignment (pending the region rewrite).*
