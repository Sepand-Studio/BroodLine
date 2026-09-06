---
status: current
folder: 01-companions
verified-against: broodline_bible.md (2026-09-05)
note: >
  Defines the four utility traits — Carapace, Litter, Regrow, Screen — that
  bible §1.2 requires to "pull real weight" and never specifies. Each is given
  one pressure to answer, a tier curve, and a stacking rule, and the campaign
  and region-defence pools are audited for whether that pressure exists.
  Settles the keep-or-cut question at keep (register 6.13). Absolute values
  await the combat numbers table; ratios and shapes are the decisions here.
---

# Broodline — Trait Utility

*Design spec, the four traits that counter nothing. Companion to bible §1.2 and §4.4.*

---

## 1. The problem

Bible §1.2 states the risk plainly: eight counters across six species means Ember and Hollow carry two each and the other four carry one. Vetch, Skitter, Loam and Pale are only worth breeding if their second trait is worth having. If Carapace, Litter, Regrow and Screen are weak, those species become "the one trait I need in a body I don't want," and the roster collapses toward two species.

**Nothing in the spec set has ever said what the four do.** They are named in one table and referenced as a requirement in three documents. This is the last open design question in the set, and it decides whether the game has six species or two.

The alternative was to cut them and ship eight traits on six species. **Keep.** Cutting them makes four of six species single-purpose, removes the entire survivability layer, and leaves the second combat slot on those species empty or duplicated. The four are the design's only source of pressure that is not a lock, and a game whose every decision is "do I hold the key" is thinner than one that also asks "can I hold the line." Register 6.13.

---

## 2. The design rule

> **A counter decides whether the lock turns. A utility trait decides how long you last while turning it.**

Three constraints follow, and they are what keep the hard-counter model intact:

1. **No utility trait ever substitutes for a counter.** No amount of Carapace lets a defence without Pierce stop a Breaker. The no-damage-floor rule (§4.4) is untouched.
2. **No utility trait ever prevents a raider reaching the Ark by itself.** They extend creatures' lives and widen margins; they never end a threat.
3. **Utility is never required to turn a lock, but may be required to survive volume.** This is the one place a wave may demand utility — an attrition wave where every counter is present and the front line still wears through. Wave 50 is the campaign's example.

**No Aberrant trait counters a raider either** (§2.4), so Aberrants and the four utility traits share this category and this rule.

---

## 3. Where utility lives in a deployment

The budget argument is what makes these traits viable rather than a nice idea.

Five creatures, two combat traits each: **ten trait slots.** From wave 40 onward every wave sends the maximum four raider types (§4.5), so a correct deployment needs **four distinct counters**. Two of the six species carry two counters, so four counters can be covered by as few as two or three creatures.

That leaves **six or more slots with nothing required in them.** Utility traits are what occupies that space, and the space is guaranteed by the four-type cap rather than hoped for. A player is never choosing utility *instead of* a counter; they are choosing what fills the slots the counters did not need.

This also answers why the single-counter species stay in the roster. A player deploying Vetch for Taunt gets Carapace whether they planned it or not. The utility trait rides along with the counter, which is exactly why it must be worth having.

---

## 4. The four traits

Each answers a different **shape of pressure**. The shapes are chosen to be mutually non-substitutable: none of the four is a worse version of another, and the wave that Carapace answers is not the wave Screen answers.

| Trait | Species | Pressure it answers | Shape |
|---|---|---|---|
| **Carapace** | Vetch | Many small hits | Flat reduction, self |
| **Screen** | Pale | Few large hits | Percentage reduction, adjacent |
| **Litter** | Skitter | Lane spread — more ground than bodies | Extra bodies |
| **Regrow** | Loam | Attrition, and the cost of losing | Sustain, in and out of combat |

### 4.1 Carapace — flat reduction, self

Reduces every incoming hit by a flat amount.

| Tier | Reduction per hit |
|---|---|
| I | Small |
| II | Moderate |
| III | Large |

**Why flat.** Flat reduction is enormously strong against a stream of weak hits and nearly worthless against one heavy blow — a Skirmisher pack or a field of Brood splits versus a Breaker's siege strike. That gives Carapace a pressure profile without giving it a counter relationship: it makes a Vetch outlast a swarm, and does nothing about the armoured thing walking past it.

**The cap that matters: reduction never removes more than 60% of any single hit.** Without it, tier-III Carapace zeroes out fodder damage entirely and becomes immunity to an entire raider type, which is a counter by another name.

Self only. Does not stack with a second Carapace on the same creature (impossible under two slots, but the engine should refuse it).

### 4.2 Screen — percentage reduction, adjacent

Reduces damage taken by creatures in **adjacent pockets** by a percentage. Not the carrier.

| Tier | Reduction | Range |
|---|---|---|
| I | Modest | Adjacent pockets |
| II | Moderate | Adjacent pockets |
| III | Moderate | Adjacent pockets, both sides of the lane |

**Why percentage, and why adjacent.** Percentage reduction scales with the size of the hit, so Screen is the mirror of Carapace: it matters most against Breakers and Bulwarks and least against fodder. And because it protects *others*, it makes a Pale worth deploying beside the front line rather than behind it — a positioning decision, which is what the Control role should produce.

**Screens do not stack.** A creature adjacent to two Screen carriers takes the higher reduction, not both. Stacking auras is how a defensive layer becomes an immunity layer.

Screen does not redirect targeting. Redirection is Taunt's job and Taunt is a counter; a utility trait must never do a counter's work, even partially.

### 4.3 Litter — extra bodies

The carrier deploys accompanied by **spawn**: small, short-lived bodies occupying the carrier's pocket.

| Tier | Spawn | Respawn |
|---|---|---|
| I | 1 | On a long cooldown |
| II | 2 | On a moderate cooldown |
| III | 3 | On a short cooldown |

Spawn carry **no traits, counter nothing, deal minimal damage, and cannot be placed.** They exist to absorb hits and occupy space. They do not enter regeneration when they fall and they grant nothing on death.

**Why this is not a fifth creature.** The five-creature cap (§4.3) exists so placement stays a thinking problem, not a body-count problem. Spawn are not placed, not chosen and not traited, so the thinking problem is unchanged. What they solve is the *three-lane* problem: five creatures across three lanes leaves at least one lane thin, and Litter is the only thing in the game that puts more bodies on the board.

That makes Litter's value rise with lane count, which ties it directly to the region roster's inverse rule — the Outer Reach is three-lane and rich, and Litter is what makes holding it survivable.

**Cap: spawn per lane never exceeds the tier-III count**, however many Litter carriers are deployed. Two tier-III Skitters in one lane give three spawn, not six.

### 4.4 Regrow — sustain, in and out of combat

Two effects, and the second is where most of its value is.

**In wave:** the carrier and creatures in adjacent pockets recover health slowly and continuously. Slow enough that it never out-heals real pressure; fast enough that a long wave ends with a front line that is worn rather than gone.

**After wave:** creatures deployed alongside a Regrow carrier leave regeneration **faster**, scaling with tier.

| Tier | In-wave regeneration | Post-wave regen reduction |
|---|---|---|
| I | Slow | Small |
| II | Moderate | Moderate |
| III | Moderate, wider radius | Large |

**The post-wave effect is the point.** Bible §4.11 makes failure cost regeneration timers and nothing else, and the economy's second-largest daily sink is regen skips. Regrow reduces the price of experimenting — a player running Loam retries more compositions per day than one who does not, and spends fewer shards doing it. That is real weight without a single point of combat power, and it makes Loam the species a player who likes to tinker builds around.

It also means Regrow is the one utility trait with an economy job, which is what bible §1.2 means when it calls these four "survivability and economy."

---

## 5. Does the pressure exist?

A trait with a defined job is still decorative if nothing in the game creates its pressure. The audit:

| Pressure | Trait | Where it exists today | Verdict |
|---|---|---|---|
| Many small hits | Carapace | Skirmisher-heavy waves throughout chapter 1; every Brood wave from 25; all high-volume late waves | **Abundant** |
| Few large hits | Screen | Breaker from wave 3; Bulwark from 41; Inner and Mid region defence pools | **Abundant** |
| Lane spread | Litter | Two lanes from wave 25, three from 41; the entire Outer Reach | **Adequate, late** |
| Attrition and retry cost | Regrow | Wave 50; the campaign retry loop generally; every region defence | **Thin in campaign, constant outside it** |

Two findings.

**Litter has no pressure before wave 25.** One-lane waves give it nothing to do, so a chapter 1–3 player who breeds Skitter for Sprint sees Litter do essentially nothing for three weeks. That is acceptable — Sprint is not needed until Bulwark at wave 41 either, so Skitter is a forward-looking species by construction — but the Trait Codex entry must say so plainly rather than let a new player conclude the trait is broken.

**Region defence is the standing utility pressure, not the campaign.** Campaign waves are authored, infrequent and retryable; region defence is recurring, automatic, and in the Outer Reach it is three lanes of four types on a schedule. Utility traits are what make a player's standing defence hold while they are not looking, which is a better home for them than authored campaign moments.

---

## 6. Campaign changes

The campaign companion authored **one** utility wave, wave 50, and flagged one-in-sixty as a token. Two more, one per remaining chapter:

| Wave | Chapter | Pressure | Intended answer |
|---|---|---|---|
| **45** | 6 | Three-lane spread: four types thin across three lanes, none individually threatening | **Litter** — the first wave with more ground than bodies |
| **50** | 6 | Attrition: Lash and Skirmisher sustained across three lanes | **Carapace or Regrow** — as authored |
| **56** | 7 | Concentrated heavy hits: Breaker and Bulwark on one lane, Delver and Drift on the others | **Screen** — the first wave where a percentage reduction beats a flat one |

Three utility waves in sixty, all in the last two chapters, after the player has met every counter. None of the three is unwinnable without utility; each is authored so that a roster carrying none of the four finishes with its front line gone and its Ark at a sliver, and a roster carrying one finishes comfortably. **That margin is the test**, and it is what soft launch measures.

---

## 7. Balance guardrails

- No utility trait ever answers a raider, at any tier
- Carapace never removes more than 60% of a single hit
- Screen auras never stack; highest applies
- Litter spawn per lane never exceeds three regardless of carrier count
- Regrow never out-heals sustained pressure in wave
- A wave winnable with four correct counters and no utility stays winnable; utility widens the margin, except on the three authored attrition waves
- No utility trait is ever a wave requirement in region defence, where the player is absent
- Utility traits follow every rule counters follow: bred not bought, one fixed tier each, coverage fused from samples, never purchasable

---

## 8. What this changes elsewhere

| Document | Change |
|---|---|
| **Bible §1.2** | Points here for what the four do |
| **Bible §4.4** | Gains the counter/utility distinction as a stated rule |
| **Campaign** | Waves 45 and 56 authored; open question 2 closed |
| **Trait Codex screen** | Utility traits need a distinct entry format — pressure answered, not raider countered — or players will look for the raider each one counters and conclude the Codex is broken |
| **Economy** | Regrow reduces regen-skip spend; the sink model's 25% daily-cost assumption should be checked against Loam adoption |
| **Combat numbers table** | Owes absolute values for all twelve tiers across the four traits |

---

## 9. Open questions

1. **Absolute values.** Everything here is a shape. The numbers need the combat table and then soft launch; the ratios between tiers matter more than the values.
2. **Is Litter's dead early game acceptable?** It is defensible and it is still three weeks of a trait doing nothing. The alternative — giving spawn a small out-of-combat job, such as a harvest trickle — would fix it and would also make Skitter the correct first splice for economy reasons, which is a different distortion. Leaning leave it, and say so in the Codex.
3. **Should Screen protect the carrier at tier III?** It would make Pale self-sufficient and blur the Carapace/Screen distinction. Leaning no.
4. **Utility trait sample weighting.** Economy §7 weights harvest drops toward traits the roster carries, and the four utility traits will lag the counters early because milestones grant counters first. Watch tier distribution at day 30; the economy companion already names the fix.
5. **Does region defence need an authored utility wave**, or is standing pressure enough? Region defence is generated, not authored, so this would mean a composition rule rather than a wave. Leaning enough.
