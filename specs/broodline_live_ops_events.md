---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_live_ops_events_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  The recurring calendar, reconciled to bible §2.2–2.3 (splice slots, mutation
  and the Aberrant sub-roll), §6.3 (weekly tick), §7.6 (Roulette yields
  samples), §8.3–8.4 and §8.6. Bible §8.4 is now the summary table and this is
  the design behind each row. Raises three §8.4 amendments (register 6.8–6.10).
---

# Broodline — Live-Ops Events

*Design spec, the recurring calendar. Companion to bible §8.4.*

---

## 1. What this document owns

Bible §8.4 is a five-row table and a rule about FOMO. This is the design behind each row: mechanics, cadence, rewards and the calendar they form. Where §8.4 and this document differ, this document is the later ruling and §8.4 has been amended to match.

Two events carry specific risk and get the most space. **Splice Roulette is a paid randomised mechanic** with the same regulatory surface as the splice screen. **Recipe Share is user-generated content** and depends on the moderation companion.

---

## 2. The weekly tick

Three systems already converge on one day: Rich Deposits rotate (§5.4), territory resolves (§6.3), and the weekly event launches. The bible makes this explicit in §6.3. This document treats it as the spine of the calendar.

**One day, one reason to log in, three things changing at once** — the map reshuffles, territory changes hands, the community goal resets. Everything else is scheduled around the tick, never independently of it.

---

## 3. Splice Census — weekly

*Renamed from "Gene Lab", which is now the facility (§7.1). An event and a building cannot share a name. Register 6.10.*

**Runs five days from the tick, then two days dark.**

A server-wide splice goal — a fixed target of completed splices for the whole server, with individual contribution tracked separately.

| Layer | Reward |
|---|---|
| **Personal** | Contribution tiers most active players reach. Charges, shards, samples. |
| **Community** | Server milestones at 40%, 70%, 100%. Rewards everyone who contributed at all. |

Personal tiers reward effort; community milestones make a solo player part of something without requiring an alliance, which is the §6.10 solo-viability principle applied to live-ops. A player with no alliance still gets a shared win once a week.

**The target scales with active server population**, recalculated at each tick. A fixed target that a mature server clears in two days and a young server never reaches is a broken event on both ends.

**The two dark days matter.** A permanently live community goal is ambient noise; the gap is what makes the tick land.

No alliance layer. It would deepen the social loop and disadvantage solo players in the one event designed to include them; the alliance already has Stake Assault and territory as its shared goals.

---

## 4. Splice Roulette — always on

**Permanently open. Featured sample pool rotates every two weeks.** Register 6.8.

Bible §8.4 scheduled Roulette as a bi-weekly event. An on/off gacha creates artificial scarcity around a paid randomised mechanic, which is exactly the wrong pressure to build, and a rotating pool delivers the same freshness without it. A spin pack that appears every other week is also a worse store than one that is always there.

**Structure** (bible §7.6, economy §7):

- Spin cost **300 Gene Shards**
- Dispenses **samples, never traits** — a spin raises coverage on traits the player already holds
- The **featured pool** is six of the twelve traits, rotating at every second tick; a spin yields a sample of a trait from the pool, weighted toward traits the player's roster carries (§7.6)

**Published odds:**

| Outcome | Rate |
|---|---|
| Tier-I sample | 82% |
| Tier-II sample | 15% |
| Tier-III sample | 3% |

**Pity: a tier-III sample is guaranteed within 30 spins, counter visible.** A gacha without a published floor is where regulators and players both lose patience, and the counter costs nothing to show. At 300 a spin, a guaranteed tier-III costs at most 9,000 shards — about two and a half days of core-player income, meaningful without being a wall.

**Roulette never grants access.** Samples for a trait the player does not hold are impossible by construction — the pool is filtered to traits already in the roster. There is no Aberrant outcome and no catalyst outcome at any price. State it on the screen: *"Roulette raises coverage on traits you own. It cannot give you a new one."*

**Odds are displayed before the first spin, not behind a link.** Same standard as the splice screen; the probability table is a shared component per the screen inventory.

Geneticist Tier 6's daily free sample pull (§8.2) is one Roulette spin from the current pool.

---

## 5. Apex Cup — monthly

**Runs the last seven days of each month.**

The game has no direct PvP combat; raids and Stake Assault are both asynchronous. The Cup is therefore a **shared gauntlet**: every player faces the identical escalating wave sequence with their five best creatures, ranked by waves survived, then by time.

- **Reuses the combat engine exactly.** No matchmaking, no new systems, no timezone advantage — everyone runs the same content whenever they like.
- **Ranks on genetics and composition**, not spending or reflexes. A player who spliced well wins.
- **Gauntlet waves obey §4.5** — never two raiders sharing a trait, never more than four types — and draw only from the eight raiders. The gauntlet may exceed the campaign's wave 60 in volume; it may never introduce a raider or demand a counter the campaign has not taught.
- **Brackets by Core tier**, in threes (1–3, 4–6, 7–9, 10–12), so a month-two player is never ranked against a month-twenty one.
- **Rewards are shards, Marks-shop-style cosmetics and leaderboard flair.** Never traits, creatures, catalysts or power.
- **Waves are authored monthly.** Authored is better content and leaks in advance; the leak is acceptable because the gauntlet ranks execution against a known sequence, the same way a speedrun does.

---

## 6. Mutation Surge — six times a year

**Seven days, every second month.** Register 6.9.

Bible §2.3 and §8.4 are precise about what the Surge does: it **raises the Aberrant sub-roll globally**, not the base mutation rate. The older calendar had it tripling the base rate; that is wrong under the bible and is corrected here.

| | Normal | Surge week | With a catalyst |
|---|---|---|---|
| Base mutation rate | ~9% | ~9% | ~9% |
| Aberrant sub-roll | 5% | **25%** | 50% |

The Surge is the only event that changes the odds of something unbuyable, which is what makes it an event rather than a banner. **It raises the chance; it never grants the trait, and its offer never contains one** (§8.4). The offer is charges — more attempts at the better odds, the same line the model draws everywhere.

**Six a year rather than four.** The economy companion §8 puts an uncatalysed Aberrant at roughly one per thirteen days of active splicing. Six Surge weeks a year is about a fifth of the calendar at five times that rate — enough that the Aberrant chase has a rhythm players can plan around, not so much that the Surge stops being the week to save charges for. Quarterly was a thin supply line for the only route to Aberrants that does not run through an Apex Vein.

Surge weeks never fall in a Cup week except once a year, deliberately (§8).

---

## 7. Recipe Share — always on

Players publish a splice recipe: both parents' species and trait sets, the locked trait, the resulting child, and the **ancestry chain** (§2.7, §8.4). Others browse, rate and save.

**Recipes are information, not shortcuts.** Following one reproduces the *choices* — body, locked slot — and the same odds on the rolled slot and Instinct, not the same child. The one-lock-of-three structure (§2.2) guarantees divergence, which is what stops shared recipes solving the game in week two. A recipe is a strategy, never a deterministic path.

**Moderation.** Recipe titles and Founder names are free text; ratings can be manipulated. Both are covered by the moderation companion, which this event depends on and does not restate.

Monetization is cosmetic recipe card frames (§8.6). Nothing else.

---

## 8. The calendar

| Event | Cadence | Duration | Timed? |
|---|---|---|---|
| **Splice Census** | Weekly, from the tick | 5 days | Yes |
| **Splice Roulette** | Always on; pool rotates at every second tick | — | No |
| **Recipe Share** | Always on | — | No |
| **Apex Cup** | Monthly | Last 7 days | Yes |
| **Mutation Surge** | Six a year, every second month | 7 days | Yes |
| **Season Pass** | Four-week cycles, aligned to campaign seasons | Continuous | No |

**Overlap rule: never more than two timed events live at once**, always-on features excluded. This is the monetization addendum's cap, applied to events. The natural failure mode of a live-ops calendar is that something is always ending.

The Census and one of Cup or Surge is the normal ceiling. **Cup and Surge overlap once a year**, on purpose — elevated Aberrant odds during the ranked gauntlet is the year's peak week — and the Census pauses that week so the rule holds.

**Season Pass and campaign seasons share a clock.** Four weeks, same start day, so "this season" means one thing.

---

## 9. Offers

Every event offer obeys bible §8.6, which now carries the full never-sold list. Nothing is restated here. In practice each event sells:

| Event | Offer |
|---|---|
| Splice Census | Charge and shard bundle, ladder-compliant (6.5) |
| Splice Roulette | Discounted spin packs |
| Apex Cup | Cosmetic flair and charges |
| Mutation Surge | **Charges only** |
| Recipe Share | Recipe card frames |

---

## 10. Guardrails

- Roulette odds and pity counter visible before the first spin
- Roulette never yields a trait the player does not hold, an Aberrant, or a catalyst
- Mutation Surge changes the Aberrant sub-roll only — never the base rate, never content
- Apex Cup rewards are cosmetic and currency, never power; its waves obey §4.5 and the closed counter pool
- Census targets scale to server population; the two dark days are never filled
- Never more than two timed events live at once
- No event or facility shares a name
- Every event reward is reachable through free play

---

## 11. Bible amendments required

| § | Current | Ruling | Register |
|---|---|---|---|
| 8.4 | Splice Roulette bi-weekly | Always on, pool rotates every second tick | 6.8 |
| 8.4 | Mutation Surge seasonal | Six a year, seven days | 6.9 |
| 8.4 | Weekly event named "Gene Lab" | Renamed Splice Census; Gene Lab is the facility | 6.10 |

---

## 12. Open questions

1. **Is 3% the right tier-III Roulette rate?** It sets the shard price of a guaranteed tier-III at ≤9,000 and so moves the value of every shard; tune it with the economy model, never alone.
2. **Cup brackets by Core tier** rank a well-bred low-tier roster below a badly-bred high-tier one only if the gauntlet is volume-bound. Check at soft launch that bracket 1–3 winners are winning on composition.
3. **Surge at 25%.** Five times baseline is a guess. The check: an active player should see about one Aberrant per Surge week uncatalysed. If most see none, raise it; if most see two, lower it.
4. **Recipe rating manipulation** — deferred to the moderation companion, which needs a rule for it.
