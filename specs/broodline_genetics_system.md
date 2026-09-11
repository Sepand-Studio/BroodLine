---
status: superseded
folder: 99-archive
superseded-by: broodline_bible.md
note: >
  Era-2.
---

# Broodline — Genetics & Splicing System
*Design spec, the core loop*

---

## 1. Core Concept

Splicing combines two creatures into one hybrid. The two parents are **consumed** in the process.

That single decision does most of the economic work in the game. Consumption means the player always needs more base stock, which is what makes harvesting Gene Shards from nodes worth doing, which is what makes relocating the Ark worth doing, which is what makes Apex Veins worth contesting. Without consumption, a player eventually assembles a perfect roster and the entire map economy becomes decorative.

It also creates the emotional stake the title is pointing at: individual creatures don't survive, the **broodline** does. The lineage record persists after its members are gone. That's the game's one genuinely distinctive idea and everything below is built to support it.

---

## 2. Creature Anatomy

Every creature is three things:

**Chassis** — the base body. Determines combat role, silhouette, and base stats. Roughly 10 at launch, mapped to tower-defense roles (Bulwark, Striker, Skirmisher, Support, Swarm). Chassis does *not* blend during a splice; the child inherits one parent's chassis, and the player chooses which.

**Traits** — four slots. This is where all the variation and all the depth lives.

**Generation** — an integer, `max(parent generations) + 1`. Gen 1 creatures are base stock. Generation gates the *quality ceiling* of traits a creature can hold, so deep lineages are how you reach the top of the power curve.

Keeping chassis discrete and player-chosen is deliberate. If both chassis and traits rolled randomly, a splice would have too many failure axes and players would stop reading outcomes. One controlled decision plus one uncertain roll is a much cleaner tension.

---

## 3. Trait Categories

Four categories, one per slot. A creature holds exactly one of each — no doubling up, which prevents the obvious degenerate strategy of stacking four damage traits.

| Slot | Governs | Example |
|---|---|---|
| **Frame** | Survivability — HP, armor, size | Plated Hide, Regenerative |
| **Armament** | Offense — damage type, range, rate | Corrosive Spines, Longshot Glands |
| **Field** | Area effects — auras, slows, ally buffs | Chill Aura, Panic Pheromone |
| **Instinct** | Autonomous behavior — targeting, retreat, enrage | Bloodscent (targets lowest HP), Last Stand (enrages below 25%) |

**Instinct is the important one.** It's how a creature acts when you aren't watching, which matters enormously in a game with an idle economy and auto-resolving raid defenses. It's also the least common category in competing games, so it's where the strategy depth should live.

**Tiers**, sharing vocabulary with the node system:

| Tier | Source |
|---|---|
| Common | Base stock, always available |
| Refined | Rich Deposit harvests, campaign, splicing up |
| Rare | Apex Vein fragments, Splice Roulette, Geneticist Tier 6 daily pull, mutation |
| **Apex** | Mutation only, or seasonal Mutation Surge events |

Apex traits being **mutation-only** is the guardrail that matters. It means no amount of money buys an Apex trait directly — you buy more attempts, which is exactly the line the monetization spec draws. It also makes an Apex roll a genuine event worth screenshotting.

---

## 4. The Splice

Player selects two parents. Then:

1. **Choose the chassis** — either parent's. Free choice, no randomness.
2. **Prioritize two traits** — from the combined pool of both parents' eight traits, the player locks two that are guaranteed to carry forward.
3. **Two slots roll** — filled from the remaining pool, weighted by tier, with displayed probabilities.
4. **Generation increments.**
5. **Both parents are consumed.**

The two-guaranteed/two-rolled split is the central balance point. Full determinism turns the game into a solved spreadsheet within a month of launch. Full randomness makes a 25-minute charge feel wasted and drives churn at exactly the wrong moment. Half-and-half means every splice moves you forward while still holding something back.

**Show the odds before the charge is spent.** Non-negotiable. Hidden probabilities on a paid action is the mechanic regulators are currently most interested in, and concealing them buys you nothing — players datamine it within a week and the goodwill loss is permanent.

**No failure state.** The worst outcome is rolling traits you already had. There is never a "SPLICE FAILED" screen. Losing two parents and a charge for a lateral result is disappointment enough; adding an explicit failure message on top converts disappointment into anger.

---

## 5. Mutation

Every splice carries a small chance that a rolled slot produces a trait **neither parent had**.

- Base rate: ~3%, tuned at soft launch
- Scales modestly with generation — deep lineages mutate more, giving late-game splices their own reason to exist
- Mutation is the **only** entry point for Apex traits into the economy
- Rate is boosted globally during Mutation Surge events, which is what makes that seasonal event actually matter rather than being a reskinned banner

This is the game's slot machine, and it's important that it's the *free* one. The excitement lives in a mechanic every player has equal access to.

---

## 6. The Broodline

Every creature carries an ancestry record, five generations deep.

**The Lineage view** is a screen showing the family tree of any creature — parents, grandparents, the traits each contributed, and which mutations entered where. Consumed creatures persist here permanently. This is the memorial and the trophy case, and it's the single most shareable artifact the game produces.

**Founders.** A player's first five creatures are flagged as Founders and named by the player. They appear at the root of every tree descended from them. Costs nothing to implement, and it converts an arbitrary tutorial reward into something a player is still looking at in month six.

**Founders can be consumed, but only deliberately.** Selecting a Founder as a splice parent triggers a second confirmation dialog that names the creature explicitly — "Consume Ash permanently? This cannot be undone." Founders stay strategically usable, and nobody destroys one by tapping through a familiar screen at speed.

The alternative — blocking Founder consumption outright — sounds kinder but leaves five permanently dead roster slots by month six, which becomes its own slow-burn irritation. One dialog is the cheaper fix. The Founder's record survives in the lineage tree either way; only the individual is lost, which is the point the whole system is making.

**Broodline Affinity.** If three or more ancestors within the five-generation window carried the same trait, that trait gets a small bonus on the current creature — call it +5%, hard-capped at one affinity per creature.

The cap matters. Affinity exists to make lineage *mechanically* real rather than decorative, and to reward players who breed toward a plan instead of splicing opportunistically. It is not meant to be a power axis. If it stacks, deep-lineage players run away with the meta and every new player is permanently behind, which is the failure mode that kills 4X games at the two-year mark.

---

## 7. Where Base Stock Comes From

Consumption only works if replacement is reliable:

- **Wave completion** — the primary drip, Gen 1 commons, always flowing
- **Node harvesting** — Rich Deposits and Apex Veins yield Gen 1 creatures with Refined or Rare traits already slotted, which is the direct link between the map economy and the core loop
- **Campaign milestones** — guaranteed specific chassis, so no player is locked out of a role by bad luck
- **Splice Roulette** — the gacha wheel, offering trait fragments rather than whole creatures

**Roster cap** scales with Gene Vault tier. The cap should bind slightly — a player who never splices should feel gentle pressure from a full roster, which nudges them into the core loop without punishing them for it.

---

## 8. Splice Screen Requirements

The spec Claude Design needs. This is the most-visited screen in the game.

Must display:
- Two parent slots with chassis silhouette, generation number, and all four traits each
- Chassis toggle — clear which of the two is selected
- The combined eight-trait pool, with the two prioritized picks visually locked
- **Probability table for the two rolling slots**, per trait, per tier
- Mutation chance, stated as a number
- Charge cost and current charge count
- Generation of the resulting child
- A clear, unmissable indication that **both parents will be destroyed**

That last item needs real weight — a confirmation step, not a fine-print line. Accidentally consuming a treasured creature is the single most likely source of refund requests and one-star reviews.

**Two confirmation levels:**

| Parent selected | Confirmation |
|---|---|
| Standard creature | Single confirm, states both parents will be destroyed |
| **Founder** | Second dialog naming the creature — "Consume Ash permanently? This cannot be undone." |

The Founder dialog must use the player's chosen name, not the chassis name. The name is what makes the player stop and read.

Also needed, separately: **Roster screen** (filter and sort by chassis, generation, trait, affinity) and **Lineage view**. Neither is described anywhere in the existing docs.

---

## 9. Guardrails

- Every trait tier is reachable free; money buys attempts, never outcomes
- Apex traits are mutation-only, so they cannot be purchased at any price
- Odds are always visible before a charge is spent
- Broodline Affinity is hard-capped at one per creature
- No creature is ever lost involuntarily — raids take cargo, never roster
- New chassis introduced in seasons must be sidegrades, not upgrades; a Gen-1 launch chassis should still be viable in year two

---

## 10. Monetization & Live-Ops Hooks

Mostly already specced elsewhere; this is what genetics adds:

- **Splice Roulette** dispenses trait fragments — three fragments combine into one trait of that tier. Fragments make near-misses feel like progress rather than loss.
- **Founder's Recipe Share** becomes substantially better with lineage: a shared recipe carries a visible ancestry chain, so players are sharing a *strategy* rather than a two-item combo.
- **Geneticist Tier 8** (second splice queue) is the strongest convenience perk in the game once splicing is destructive, since throughput is the real constraint.
- **Lineage frames and Founder portraits** — cosmetic, high visibility, attached to the screen players screenshot most.

---

## 11. Open Questions

1. **Is 3% the right mutation rate?** At five charges a day, that's a mutation roughly every six days. Might be too rare to sustain the "any splice could be the one" feeling that carries the loop.
2. **Should Recessive results exist** — a trait carrying forward at a lower tier than the parent's? Adds texture and stakes, also adds frustration. Probably test rather than ship at launch.
3. **Five generations of lineage depth** is a storage and UI decision as much as a design one. Ten would be more impressive and much heavier.
4. **Sample extraction.** Should a player be able to spend Gene Shards to copy a creature rather than consume it? Protects attachment, but weakens the consumption economy that everything else depends on. Leaning no.

*Resolved: Founders can be consumed, gated behind a named confirmation dialog (§6, §8).*

---

*Next: combat spec — trait effects need concrete numbers, and Instinct traits in particular need defined behavior trees before any of the above can be balanced.*
