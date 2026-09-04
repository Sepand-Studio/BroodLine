# Broodline — Live-Ops Events

*Design spec, the recurring calendar*

---

## 1. Why This Document Exists

The monetization spec describes five named events in a table, one line each, and identifies event-offer synchronisation as the reason Kingshot's ARPDAU outperforms Whiteout's. It is the revenue engine of the entire model and it has never been designed past five table rows.

Two of the five carry specific risk. **Splice Roulette is a gacha with fragment assembly** — the same regulatory surface as the splice screen, which received a full specification while Roulette received a sentence. **Founder's Recipe Share is user-generated content**, with no moderation policy anywhere in the set.

And one carries a contradiction that would break a core guardrail if shipped as written. See §6.

---

## 2. The Weekly Tick

Three systems already converge on one day: Rich Deposits rotate, alliance territory resolves, and Gene Lab launches. The node spec and alliance spec both cite this deliberately.

**Make it explicit and make it the week's event.** One day, one reason to log in, three things changing at once — the map reshuffles, territory changes hands, and the community goal resets. Scattering these across three days would triple the notifications and dilute all three.

Everything else in the calendar is scheduled around the Tick rather than independently.

---

## 3. Gene Lab

**Weekly. Runs five days from the Tick, then two days dark.**

A server-wide splice goal — a fixed target of completed splices for the whole server, with individual contribution tracked separately.

| Layer | Reward |
|---|---|
| **Personal** | Contribution tiers, reached by most active players. Charges, shards, fragments. |
| **Community** | Server milestones at 40%, 70%, 100%. Rewards everyone who contributed at all. |

The two-layer structure is what makes this worth building. Personal tiers reward effort; community milestones make a solo player part of something without requiring an alliance, which directly serves the solo-viability principle the alliance spec commits to. A player with no alliance still gets a shared win once a week.

**The target must scale with active server population**, recalculated weekly. A fixed target that a mature server clears in two days and a young server never reaches produces a broken event on both ends.

The two dark days matter. A permanently-live community goal stops being an event and becomes ambient noise; the gap is what makes the Tick land.

---

## 4. Splice Roulette

**Always available. Featured trait pool rotates every two weeks.**

The monetization spec schedules Roulette as a bi-weekly event. Recommend instead that it is **permanently open with a rotating featured pool** — an on/off gacha creates artificial scarcity around a paid randomised mechanic, which is the wrong pressure to build, and a rotating pool delivers the same freshness without it.

**Structure:**
- Spin cost: **300 Gene Shards**
- Dispenses **trait fragments**, never whole traits, per the genetics spec
- **Three fragments of a tier assemble into one trait of that tier**
- Fragments are category-specific — a Frame fragment assembles into a Frame trait

**Published odds:**

| Outcome | Rate |
|---|---|
| Common fragment | 60% |
| Refined fragment | 32% |
| Rare fragment | 8% |

**Roulette tops out at Rare. There are no Apex fragments, at any price, ever.** Apex traits are mutation-only per the genetics spec, and that guardrail is the entire reason the monetization model can claim money buys attempts rather than outcomes. Roulette is where that claim is most likely to be quietly broken, so it should be stated on the screen itself.

**Pity: a Rare fragment is guaranteed within 20 spins, with the counter visible.** A gacha without a published floor is where both regulators and players lose patience, and the counter costs nothing to display. In practice a Rare trait costs roughly 9,000 shards — about two and a half days of core-player income, which is meaningful without being a wall.

**Odds are displayed before the first spin, not behind a link.** Same standard as the splice screen. The probability table is a shared component per the screen inventory; build it once and use it in both places.

---

## 5. Apex Cup

**Monthly. Runs the last seven days of each month.**

The monetization spec calls this a "competitive PvP leaderboard using your best hybrids," but the game has no direct PvP combat — raids and Stake Assault are both asynchronous and both already specced.

**Recommendation: a shared gauntlet.** Every player faces the *identical* escalating wave sequence with their five best creatures. Ranking is by waves survived, then by time.

This is the right answer for several reasons at once. It needs no matchmaking and no new systems — it reuses the combat engine exactly. It has no timezone advantage, since everyone runs the same content whenever they like. And it ranks players on **genetics quality and composition**, not on spending, reflexes, or who was awake at 4am. A player who spliced well wins, which is the outcome the combat spec says the whole design exists to produce.

- **Brackets by Vault Core tier**, so a month-two player is never ranked against a month-twenty one
- Rewards are **shards, cosmetics, and leaderboard flair only** — never traits, never creatures, never power
- The gauntlet's wave sequence is authored fresh each month

---

## 6. Mutation Surge — A Contradiction

**Seasonal, four times a year, seven days.**

The monetization spec describes Mutation Surge as a "temporary new trait type available only during event," with a "FOMO-driven limited trait pack."

**That directly violates two standing guardrails.** The genetics spec states every trait tier is reachable free and that Apex traits are mutation-only, purchasable at no price. An event-exclusive trait sold in a pack is both event-locked and purchasable — it fails on both counts.

**Resolution: Mutation Surge boosts the global mutation rate. It introduces nothing.**

- Base rate rises from **3% to 9%** for seven days
- No new traits, no event-exclusive traits, no trait packs
- Monetization hook is **splice charges**, not outcomes — more attempts at a better rate, which is exactly the line the model draws everywhere else

This is also the better event. It is the only week in the quarter when Apex traits enter the economy at real volume — roughly nine mutations across a week of active splicing against three at baseline — and every player, paying or not, gets the same improved odds. The excitement stays in the free mechanic, which the genetics spec identifies as the point.

---

## 7. Founder's Recipe Share

**Always on.**

Players publish a splice recipe: the two parents' trait sets, the chassis chosen, the resulting child, and the **visible ancestry chain** the genetics spec calls for. Others browse, rate, and save.

**Recipes are information, not shortcuts.** Following one produces the same probabilities, not the same child — the two-guaranteed/two-rolled split guarantees divergence. This is what stops shared recipes from solving the game in week two. A recipe is an aspiration and a strategy, never a deterministic path.

**This is user-generated content and it has no moderation policy.** Recipe Share, alliance chat, and free-text Founder naming are three UGC surfaces across the spec set, and none of them is covered anywhere. That is an App Store submission requirement, not a design nicety.

Monetization is cosmetic recipe card frames, per the original spec. That remains correct.

---

## 8. The Calendar

| Event | Cadence | Duration |
|---|---|---|
| Gene Lab | Weekly, from the Tick | 5 days |
| Splice Roulette | Always on; pool rotates biweekly | — |
| Recipe Share | Always on | — |
| Apex Cup | Monthly | Last 7 days |
| Mutation Surge | Quarterly | 7 days |
| Season Pass | 4-week cycles | Continuous |

**Overlap rule: never more than two timed events active simultaneously**, excluding always-on features. The monetization spec's own guardrail warns that FOMO burnout kills LTV faster than a missed sale, and the natural failure mode here is a calendar where something is always ending.

The one intentional collision is **Mutation Surge landing inside an Apex Cup week**, once a year. Elevated mutation rates during the ranked gauntlet is the year's peak moment and worth scheduling on purpose.

---

## 9. Offer Guardrails

Each event ships a themed offer. The following may never appear in one:

- **Apex traits or Apex fragments**, in any bundle, at any price
- **Raid Marks or Defense Marks** — non-purchasable by the raiding spec
- **Hold**, or anything accelerating it
- **Event-exclusive traits or chassis** — everything must remain reachable after the event ends
- **Campaign milestone skips**
- Additional Stake slots, Vault build slots, or non-ally penalty increases

Safe: charges, shards, cosmetics, timer skips, Season Pass tiers, Geneticist XP.

---

## 10. Guardrails

- Roulette odds and pity counter visible before the first spin
- Roulette never dispenses Apex
- Mutation Surge changes rates only, never content
- Apex Cup rewards are cosmetic and currency, never power
- Gene Lab targets scale to server population
- Never more than two timed events live at once
- Every event reward is reachable through free play

---

## 11. Open Questions

1. **Is 8% the right Rare fragment rate?** It produces a Rare trait per ~9,000 shards. Tuning it moves the entire value of Gene Shards, so it should not be adjusted independently of the economy model.
2. **Does Gene Lab need an alliance layer?** A third contribution tier at alliance level would deepen the social loop, and it would also disadvantage solo players in an event specifically designed to include them.
3. **Should the Apex Cup gauntlet use authored or generated waves?** Authored is better content and leaks in advance; generated is fair and blander.
4. **Quarterly Mutation Surge may be too rare.** Four weeks a year of elevated rates is a thin supply line for the only Apex entry point in the game.
5. **Recipe Share needs an anti-spam and rating-manipulation model** that no document covers.

---

*Remaining undocumented: moderation and UGC policy, the D14–D90 retention arc, the monetization rewrite, and profile/mail/settings.*
