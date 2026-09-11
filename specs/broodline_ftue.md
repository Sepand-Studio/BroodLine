---
status: superseded
folder: 99-archive
superseded-by: broodline_build_order.md
note: >
  Era-2.
---

# Broodline — First-Time User Experience

*Design spec, the first session and the first fortnight*

---

## 1. Core Principle

Nine specs describe systems a player will meet over two years. This one covers the five minutes that decide whether they meet any of them.

**Rule: one new system per session, and the first sixty seconds are play.** No cinematic, no lore crawl, no tour of an empty base. A wave is already coming when the app opens.

### The advertising advantage

Kingshot draws consistent criticism for a mismatch between its marketing and its product — the promotional creatives show a tower-defense minigame, while the actual game is an idle 4X. Players arrive expecting one thing and find another, and the complaint is loud enough to appear in reviews and coverage.

**Broodline's core loop genuinely is tower defense.** The ad and the game can match. That's a real and unusual advantage in this category, and it should be protected: whatever the UA creatives show, session one must deliver it within thirty seconds. If marketing later drifts toward showing something the game isn't, the retention cost lands immediately in D1.

---

## 2. Session One, Beat by Beat

Target: **under six minutes**, ending on the game's most distinctive screen.

| # | Beat | Purpose |
|---|---|---|
| 1 | **Cold open.** A wave is incoming. Two creatures are handed to the player with one instruction: place them. | Play in under 20 seconds. Delivers the ad promise. |
| 2 | **First wave resolves.** Creatures act on their own via Instinct. Player watches, wins. | Teaches that placement decides outcomes, not tapping. |
| 3 | **Creature drop.** A third creature is awarded. | First acquisition. |
| 4 | **Name it.** Founder naming, one creature only. | The emotional anchor, placed before any complexity. |
| 5 | **Second wave.** Three creatures, one new enemy archetype with a visible armour type. | Introduces composition without explaining it. |
| 6 | **First splice.** Guided, using two *provided* creatures — never the named one. | The core loop. |
| 7 | **Mutation fires.** Scripted, guaranteed. | Player sees the best-case outcome once and knows it exists. |
| 8 | **Lineage View.** A two-generation tree with the named Founder at its root. | Ends the session on the thing nothing else does. |

Beat 8 is the whole argument for the session. A player closing the app after five minutes should be carrying one image: a family tree with a creature they named at the bottom of it.

---

## 3. The Consumption Problem

Splicing destroys both parents. That is the hardest thing in the game to teach and the easiest to get wrong.

**Rules for the tutorial splice:**

- The two parents are **provided specifically for it** and framed as sample stock, not as the player's creatures
- **The named Founder is never eligible.** It should be visibly locked out of the parent slots during the tutorial.
- Destruction is stated plainly before the confirm, in the same language the live game will use
- Immediately after, the Lineage View shows both consumed parents still present in the tree

That last beat is the actual lesson: **individuals are consumed, the record survives.** Teaching it in the first splice, with no stakes attached, means the player already understands the trade the first time it costs them something real.

Do not soften this. A tutorial that hides consumption produces a player who discovers it in week two by destroying something they cared about.

---

## 4. Founder Naming Cadence

Five Founders is five naming prompts, and five text-entry moments in one session is friction that will lose people.

- **Founder 1:** named in session one, beat 4
- **Founders 2–5:** awarded across days 1–3, each with an optional naming prompt and a sensible default
- All five renameable at any time from the Roster

One name in session one, spread the rest. The first one carries the emotional weight; the others are bonus.

---

## 5. Drip Schedule

| When | System introduced | Gated because |
|---|---|---|
| Session 1 | Combat, splicing, lineage | The core loop, nothing else |
| Session 2 | Splice Charges as a constraint, Roster, trait categories | Charges are meaningless until the player wants more splices |
| Session 2–3 | **Charge wall → free 24hr Double Regen trial** | The monetization spec's designed hook moment |
| Session 3 | Ark Home, first Gene Vault upgrade (near-instant) | Gives the base screen a reason to exist |
| Day 2 | World Map, Region Detail, first relocation | Needs a reason to move — introduce alongside a visibly better node |
| Day 2–3 | Harvesting, Common and Rich nodes | The economy behind the loop |
| Day 3–4 | Region defense waves | Ties the map back to combat |
| Day 5–7 | Collector dispatch, first cargo run | Safe: new-player raid immunity is active |
| Day 7 | Alliance prompt | Only once the player has something to contribute |
| **Day 14** | Raid exposure begins, immunity ends | Per the raiding spec |
| Week 3+ | Apex Veins, Stake mechanics, garrison | Requires an alliance and a mature roster |

The two most important entries are the day-2 map introduction and the day-14 raid exposure. The map must arrive attached to a *specific, visible* better node — abstract freedom to relocate teaches nothing. And raid exposure must be announced clearly when immunity lapses, not discovered by being raided.

---

## 6. What Stays Hidden

Deliberately absent from session one:

- The store. No offers, no first-purchase banner, no currency shop.
- Splice Roulette, Season Pass, Geneticist Tier
- The full Vault, the alliance tab, anything on the Map beyond the current region
- Trait tiers and probability tables — the tutorial splice shows outcomes, not odds

Bottom navigation should begin with **two tabs** and reveal the rest as systems unlock. A five-tab bar on a first launch is a wall of unexplained choices.

---

## 7. Monetization in the First Fortnight

The monetization spec's own guardrails apply hardest here.

- **No purchase prompt in session one.** Not the first-purchase 2x offer, not a starter pack. Nothing.
- **First designed monetization beat is the free trial**, at the first charge wall in session 2–3. It costs nothing and asks for nothing.
- **First purchase prompt** comes at the trial's expiry, framed as extending something the player has already used.
- Rewarded ads become available in session 2, presented as a free lever rather than a paywall alternative.

The pattern this preserves — let them feel the upgrade before asking for money — is described in the monetization spec as the single highest-leverage tactic available. Putting a store banner in session one spends that leverage for nothing.

---

## 8. What to Measure

Soft-launch instrumentation, in rough priority:

- **Completion rate of session one**, and drop-off by beat. Beat 6, the first splice, is the likeliest failure point.
- **D1, D3, D7 retention**, benchmarked against the category rather than absolutes
- **Time to first splice** — target under four minutes
- **Founder naming rate.** If a large share skip it, the emotional anchor isn't landing and beat 4 needs rework.
- **Lineage View revisits in week one.** This is the proxy for whether the game's distinctive idea is actually distinctive to players.
- **Trial acceptance and conversion at expiry**
- **Day-14 churn spike.** If raid exposure causes a visible cliff, the raiding spec's loss caps need tightening.

Founder naming rate and Lineage revisits are the two custom metrics worth building. Everything else is standard.

---

## 9. Open Questions

1. **Is six minutes too long?** Category norms trend shorter. The alternative is cutting the second wave, at the cost of introducing armour types later.
2. **Should mutation really be scripted in the tutorial?** It sets an expectation the 3% live rate won't meet. The counter-argument is that a player who has never seen a mutation doesn't know to want one.
3. **Does Founder naming need a skip path?** Almost certainly yes, with a good default — but a skip reduces the anchor's effect for the players most likely to churn.
4. **How is raid immunity expiry communicated?** Needs to be an event with a warning, ideally paired with an escort tutorial, not a silent flag flip.
5. **Two-tab or three-tab opening navigation?** Three lets the Map arrive without a layout shift on day 2.

---

*This completes the spec set: monetization, resource nodes, collector raiding, genetics, combat, alliance and territory, screen inventory, art direction, Gene Vault, and FTUE.*
