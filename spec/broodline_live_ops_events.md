# Broodline — Live-Ops Events

*Design spec, the recurring calendar*

---

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Last brought into line with the bible: 4 Sep 2026.


## 1. Why This Document Exists

Bible §8.4 carries five named events as a five-row table. Event-offer synchronisation is the reason Kingshot's ARPDAU outperforms Whiteout's, which makes this the revenue engine of the whole model, and a table row is not a design.

Two of the five carry specific risk. **Splice Roulette is a paid randomised mechanic** — the same regulatory surface as the splice screen, which has a full specification while Roulette had a sentence. **Recipe Share is user-generated content**; its policy lives in `broodline_moderation_ugc.md`.

---

## 2. The Weekly Tick

Three systems already converge on one day: Rich Deposits rotate, alliance territory resolves, and Gene Lab launches. Bible §5.4 and §6.3 both cite this deliberately.

**Make it explicit and make it the week's event.** One day, one reason to log in, three things changing at once — the map reshuffles, territory changes hands, and the community goal resets. Scattering these across three days would triple the notifications and dilute all three.

Everything else in the calendar is scheduled around the Tick rather than independently.

---

## 3. Gene Lab

**Weekly. Runs five days from the Tick, then two days dark.**

A server-wide splice goal — a fixed target of completed splices for the whole server, with individual contribution tracked separately.

| Layer | Reward |
|---|---|
| **Personal** | Contribution tiers, reached by most active players. Charges, shards, samples. |
| **Community** | Server milestones at 40%, 70%, 100%. Rewards everyone who contributed at all. |

The two-layer structure is what makes this worth building. Personal tiers reward effort; community milestones make a solo player part of something without requiring an alliance, which directly serves the solo-viability principle at bible §6.10. A player with no alliance still gets a shared win once a week.

**The target must scale with active server population**, recalculated weekly. A fixed target that a mature server clears in two days and a young server never reaches produces a broken event on both ends.

### 3.1 The scaling formula

> **Target = 18 × A × D**
>
> where **A** is the count of accounts that performed at least one splice in the trailing seven days, and **D** is a difficulty factor of **1.00**.

Eighteen is the working figure: a core player splices about six times a day, per `broodline_sample_economy.md` §8, and the event runs five days. Eighteen is therefore about **three days of a core player's splicing**, which lands the 100% milestone somewhere on day four for a healthy server. Casual players contribute less and optimisers more, and the average across a real population is what **D** exists to correct once there is data.

**Floor and ceiling, both necessary:**

- **Floor: A is never counted below 200.** A server with sixty active players would otherwise face a target so low that 100% clears on day one and the event is over before most of them log in. Below the floor a young server has to stretch, which is the correct feeling.
- **Ceiling: A is capped at 5,000.** Past that the target stops rising, so the largest servers clear it comfortably. That is deliberate — a mature server should feel like it wins together, and a server-wide goal that punishes success would be a strange thing to build.

**A is measured on the trailing seven days, not live.** A live count would let the target move underneath players mid-event, which is the fastest way to make a collaborative goal feel rigged.

**D is the only tuning dial and it moves once a season at most.** Weekly adjustment would mean a target that responds to last week's performance, and players notice when clearing an event makes the next one harder.

The two dark days matter. A permanently-live community goal stops being an event and becomes ambient noise; the gap is what makes the Tick land.

---

## 4. Splice Roulette

**Always available. Featured sample pool rotates every two weeks**, per bible §8.4. An on/off gacha creates artificial scarcity around a paid randomised mechanic, which is the wrong pressure to build; a rotating pool delivers the same freshness without it.

**Structure:**
- Spin cost: **300 Gene Shards**
- Dispenses **samples**, never traits, per bible §1.7. A sample raises coverage on a trait a creature already carries; it can never grant access to one.
- Samples are trait-specific — a Chill sample fuses toward Chill coverage and nothing else
- **Three samples of a tier fuse into one of the next**, per bible §7.6

**Published odds:**

| Outcome | Rate |
|---|---|
| Tier-I sample | 60% |
| Tier-II sample | 32% |
| Tier-III sample | 8% |

**Roulette dispenses coverage and nothing else. No trait, no Instinct, no Aberrant, at any price, ever.** Access comes only from breeding and mutation, and that guardrail is the entire reason the model can claim money buys attempts rather than outcomes. Roulette is where the claim is most likely to be quietly broken, so it should be stated on the screen itself.

**Featured pools are weighted, never exclusive.** A rotating pool raises the odds of samples for particular traits; it never gates any sample behind a window.

**Pity: a tier-III sample is guaranteed within 20 spins, with the counter visible.** A gacha without a published floor is where both regulators and players lose patience, and the counter costs nothing to display. In practice that is roughly 9,000 shards — about two and a half days of core-player income, meaningful without being a wall.

**Odds are displayed before the first spin, not behind a link.** Same standard as the splice screen. The probability table is a shared component per the screen inventory; build it once and use it in both places.

---

## 5. Apex Cup

**Monthly. Runs the last seven days of each month.**

Bible §8.4 calls this a "competitive leaderboard using your best creatures," but the game has no direct PvP combat — raids and Stake Assault are both asynchronous and both already specced.

**Recommendation: a shared gauntlet.** Every player faces the *identical* escalating wave sequence with their five best creatures. Ranking is by waves survived, then by time.

This is the right answer for several reasons at once. It needs no matchmaking and no new systems — it reuses the combat engine exactly. It has no timezone advantage, since everyone runs the same content whenever they like. And it ranks players on **genetics quality and composition**, not on spending, reflexes, or who was awake at 4am. A player who spliced well wins, which is the outcome bible §4.1 says the whole design exists to produce.

- **Brackets by Core tier**, so a month-two player is never ranked against a month-twenty one
- Rewards are **shards, cosmetics, and leaderboard flair only** — never traits, never creatures, never power
- The gauntlet's wave sequence is authored fresh each month

### 5.1 Authoring the gauntlet

**Twenty waves, one lane count, three attempts.**

| | |
|---|---|
| Length | **20 waves**, continuous — no retries within a run |
| Terrain | **One authored family for the whole gauntlet**, rotating monthly across the eight at `broodline_region_roster.md` §3 |
| Integrity | **6**, fixed, and it does not reset between waves |
| Roster | Five creatures, locked at the start of a run. Losses stay lost for the rest of the run |
| Attempts | **Three per month.** Best run counts |
| Budget | The standard formula, starting at wave 25 equivalent and running to wave 45 |

**Integrity carrying across waves is what makes this a gauntlet rather than twenty campaign waves.** A leak on wave 3 is still costing the player on wave 17, which turns the run into a resource-management problem rather than twenty independent puzzles. Six is tight enough that a sloppy opening ends a run.

**Losses persisting is the second half of that.** Five creatures at the start, and a creature that falls on wave 8 is gone for the remaining twelve. Roster *depth* stops mattering and roster *quality* starts to — which is the whole point of ranking on genetics.

**One terrain family per month, rotating.** Every player faces the same ground, so there is no terrain lottery, and the family that month determines what the winning composition looks like. A Delta month rewards breadth; a Defile month rewards depth. Over a year that gives eight distinct metagames out of content that already exists.

**Three attempts, best run counts.** One attempt makes a single misplacement worth a month; unlimited attempts makes the leaderboard a measure of free time. Three is enough to recover from a bad opening and not enough to brute-force.

**Two rules the gauntlet must not break.** Its waves obey the same composition rules as everything else — never two raiders answered by the same trait, never more than four types, no raider gaining stats with depth. And **the Sunder never appears**, per `broodline_raider_roster.md` §7: it exists at campaign wave 60 and nowhere else.

---

## 6. Mutation Surge — A Contradiction

**Seasonal, four times a year, seven days.**

The original monetization spec described Mutation Surge as a "temporary new trait type available only during event," with a "FOMO-driven limited trait pack."

**That directly violates two standing guardrails.** Bible §8.6 forbids selling traits at all, and §1.6 makes Aberrants unreachable by purchase at any price. An event-exclusive trait sold in a pack is both event-locked and purchasable — it fails on both counts.

**Resolution: Mutation Surge raises the Aberrant sub-roll globally. It introduces nothing.**

- The base mutation rate stays at ~9%. **The Aberrant sub-roll inside it rises from 5% to 20%** for seven days.
- No new traits, no event-exclusive traits, no trait packs, **and no catalysts** — catalysts come from Apex Vein extraction only, per bible §8.6
- Monetization hook is **splice charges**, not outcomes — more attempts at better odds, which is the line the model draws everywhere else

This is also the better event. It is the only week in the quarter when Aberrants enter the economy at real volume, and every player, paying or not, gets the same improved odds. The excitement stays in the free mechanic, which is the point.

---

## 7. Founder's Recipe Share

**Always on.**

Players publish a splice recipe: the two parents' trait sets, the species body chosen, the resulting child, and the **visible ancestry chain** bible §3.7 calls for. Others browse, rate, and save.

**Recipes are information, not shortcuts.** Following one produces the same probabilities, not the same child — one locked slot and two rolled slots, per bible §2.2, guarantees divergence. This is what stops shared recipes from solving the game in week two. A recipe is an aspiration and a strategy, never a deterministic path.

**Recipe Share is structured data only** — no title, no description, no comments, rating by numeric score with no written review. That removes the entire moderation surface from the most public, most persistent, server-wide UGC feature in the game. See `broodline_moderation_ugc.md` §4.

Monetization is cosmetic recipe card frames. That remains correct.

---

## 8. The Calendar

| Event | Cadence | Duration |
|---|---|---|
| Gene Lab | Weekly, from the Tick | 5 days |
| Splice Roulette | Always on; pool rotates biweekly | — |
| Recipe Share | Always on | — |
| Apex Cup | Monthly | Last 7 days |
| Mutation Surge | **6× a year**, every 8–9 weeks | 7 days |
| Season Pass | 4-week cycles | Continuous |

**Overlap rule: never more than two timed events active simultaneously**, excluding always-on features. Bible §8.4's own guardrail warns that FOMO burnout kills LTV faster than a missed sale, and the natural failure mode here is a calendar where something is always ending.

**Five of the six Surges avoid Apex Cup weeks.** Two events both about roster quality landing together wastes one of them.

The one intentional collision is **the sixth Surge landing inside an Apex Cup week**, once a year. Elevated Aberrant odds during the ranked gauntlet is the year's peak moment and worth scheduling on purpose — but exactly once, or the exception becomes the pattern.

---

## 9. Offer Guardrails

Each event ships a themed offer. The following may never appear in one:

- **Traits of any kind, Aberrant or species**, in any bundle, at any price
- **Catalysts**
- **Raid Marks or Defense Marks** — non-purchasable, per bible §8.6
- **Hold**, or anything accelerating it
- **Event-exclusive traits or species** — everything must remain reachable after the event ends
- **Creatures of a named species**, since every species carries a counter
- **Campaign milestone skips**
- Additional Stake slots, Vault build slots, or non-ally penalty increases

Safe: charges, shards, cosmetics, timer skips, Season Pass tiers, Geneticist XP.

---

## 10. Guardrails

- Roulette odds and pity counter visible before the first spin
- Roulette dispenses samples only — never traits, Instincts or Aberrants
- Mutation Surge changes odds only, never content
- Apex Cup rewards are cosmetic and currency, never power
- Gene Lab targets scale to server population
- Never more than two timed events live at once
- Every event reward is reachable through free play

---

## 11. Open Questions

1. **Is 8% the right tier-III rate?** It produces a tier-III sample per ~9,000 shards. Tuning it moves the entire value of Gene Shards, so it should not be adjusted independently of the economy model.
2. **Does Gene Lab need an alliance layer?** A third contribution tier at alliance level would deepen the social loop, and it would also disadvantage solo players in an event specifically designed to include them.
3. **Should the Apex Cup gauntlet use authored or generated waves?** Authored is better content and leaks in advance; generated is fair and blander.
4. ~~**Quarterly Mutation Surge is too rare.**~~ **Resolved: six a year, not four.** At a 5% base sub-roll a casual player who never contests an Apex Vein sees an Aberrant roughly every nine months, which is close to never for the game's most distinctive object. Six Surges — every eight or nine weeks rather than every thirteen — brings that under six months.

   **Surge frequency was the right lever rather than the base rate**, because raising the base rate makes Aberrants less rare for everyone including the players already finding them, while raising Surge frequency only helps the players currently shut out. It also keeps the Surge weeks feeling like weeks that matter.

   The calendar at §8 moves to six, spaced so that only one Surge a year overlaps an Apex Cup — deliberately, as the year's peak week.
5. **Recipe Share needs an anti-spam and rating-manipulation model** that no document covers. Structured-only publishing removes the text surface but not the vote surface.

---

*Owns: event mechanics, the Gene Lab scaling formula, Roulette odds and pity, the Apex Cup gauntlet format, the Mutation Surge cadence, and the calendar. Screens required: Event Hub, Splice Roulette, Gene Lab Event, Apex Cup, Recipe Share.*
