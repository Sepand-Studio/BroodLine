---
status: current
folder: 00-bible
verified-against: broodline_bible.md (2026-09-05)
note: >
  The design, §1–10. The only document to build from. Where anything else
  disagrees with it, the bible wins.
---

# Broodline — Design Bible

*The current design. Replaces the nine original specs as they are absorbed.*

**Status:** Complete, §1–10.

Where this contradicts an original spec, this document is current. `broodline_reconciliation.md` records why each decision was made; this document records only what is true.

---

# §1 — The Creature Model

## 1.1 Anatomy

Every creature is four things: a **species**, two **combat traits**, one **Instinct**, and a **generation**.

| | |
|---|---|
| **Species** | The body. Sets silhouette, combat role and base stats. Six of them. A splice does not blend species — the child inherits one parent's body and the player chooses which. |
| **Combat traits** | Two slots. Where counter coverage lives. Drawn from a pool of twelve. |
| **Instinct** | One slot. Autonomous behavior. Six of them, untiered, inherited independently of the body. |
| **Generation** | An integer, `max(parent generations) + 1`. Gen 1 is base stock. Gates how high coverage can climb. |

Three slots rather than four is deliberate and load-bearing. Counters are hard — a creature without the answering trait does not beat its raider — so thin creatures are what force a wide roster, and a wide roster is what keeps splicing necessary. A third combat trait would let one creature answer most of a wave and would quietly starve the loop the whole game rests on.

The Instinct slot is safe precisely because Instinct counters nothing. It adds a breeding axis without adding counter coverage.

## 1.2 The six species

Each is distinguishable in pure silhouette at 40px. Shape carries role before any card is read.

| Species | Role | Traits | Colour | Silhouette |
|---|---|---|---|---|
| **Vetch** | Wall | Carapace, **Taunt** | `#6ba7c0` | Low dome, four stubby legs, no neck |
| **Ember** | Splash | **Cinder**, **Splash** | `#e5867a` | Tall narrow torso, head crest, two legs |
| **Skitter** | Swarm | **Sprint**, Litter | `#e8b34a` | Small body, six long thin legs, tiny head |
| **Hollow** | Sniper | **Reach**, **Pierce** | `#7a6ac0` | Tiny body, stilt legs, long forward neck |
| **Loam** | Support | Regrow, **Burrow** | `#7cc492` | Segmented ground-hugger, blunt snout, no legs |
| **Pale** | Control | Screen, **Chill** | `#c6cede` | Broad wing arc, small hanging body |

> **Pale moved, 2026-09-17:** `#a9b0c4` → `#c6cede`, in Phase 8 Task 6, to take
> the palette's worst colour-blind pair (Vetch/Pale) from ΔE 7.2 to 17.3. It is
> the only species colour this project has changed. `Tokens.uss` (`--slate`) and
> `PaletteContrast.Species` are the live values;
> `implementation/results/palette-decision.md` has the measurement, and §10.4
> carries the constraint the move puts on the art.

**Bold traits are counters.** Eight of the twelve answer a raider; the remaining four — Carapace, Litter, Regrow, Screen — are survivability and economy traits that counter nothing by design.

**On the uneven distribution.** Ember and Hollow each carry two counters; the other four carry one. This is unavoidable arithmetic — eight raiders need eight counters, and eight counters across six species means two species must double up.

It does not make any species skippable. Vetch is the only source of Taunt, so Vetch cannot be skipped; the same holds for Skitter, Loam and Pale. Ember and Hollow are more *efficient*, not others redundant, and the practical effect is that a new player prioritises them first — which is a clear early goal rather than a flaw.

One requirement follows: **the four utility traits must pull real weight.** If Carapace, Litter, Regrow and Screen are weak, the single-counter species become "the one trait I need in a body I don't want," and the roster collapses toward two species. These four are where survivability and economy live and they should be worth building around.

## 1.3 Coverage tiers

Traits scale I → II → III. **Tier decides how much a trait covers, never whether it works.**

Chill I slows one Courser; Chill III slows a pack. Reach I answers a flier in one lane; Reach III covers more sky. Splash I catches two of the eight Skirmishers a wave sends; Splash III catches five. Cinder I burns Brood's first generation of splits; Cinder III catches the second.

This is the single most important rule in the trait system. It keeps the lock binary and honest — any Chill stops a Courser — while giving tier real weight. Later waves pressure the player with **more**, never with resistance. No raider is ever immune to a tier-I answer.

Coverage rises by fusing samples (§1.6), bounded by generation (§2.5).

## 1.4 Instinct

Six behaviors. Untiered — a creature carries one or it doesn't.

| Instinct | Target rule | Trigger | Response |
|---|---|---|---|
| **Bloodscent** | Lowest current HP in range | — | — |
| **Vanguard** | Closest to the Ark | — | — |
| **Overwatch** | Furthest in range | — | +25% range, −20% attack speed |
| **Last Stand** | Nearest | Self below 25% HP | +50% attack speed |
| **Skittish** | Nearest | Self below 40% HP | Repositions to an adjacent free tile |
| **Pack Sense** | Nearest | Adjacent to same-species ally | +15% damage to both |

Behavior has no magnitude that can grow, which is why Instinct is untiered. A stronger Bloodscent would be a power stat; a more frequent one would be a hidden number. Both break the rule that every Instinct must be inferable from watching a single wave.

**Instinct inherits independently of the body.** A Vetch-bodied creature can carry Hollow's Instinct. This is what makes Instinct a breeding target rather than species flavour, and it gives a deep line a reason to exist beyond its generation count.

**Triggers must fire visibly** — an animation state change or a colour shift. The moment Last Stand kicks in should be legible from across the room.

**Where Instincts come from.** Gen-1 base stock rolls one Instinct, weighted so each species has a signature behavior it usually carries but can carry any of the six. This preserves species identity while keeping every Instinct reachable by breeding. *Weighting values to be set with the drop tables.*

## 1.5 Growth

Creatures mature through four visual stages, runt to apex, advancing on age.

**Growth changes how a creature looks and never whether it fights.** A creature is combat-ready from the moment it is revealed. There is no feeding mechanic, no growth timer gating deployment, and no stat that improves with maturity.

This is not a missed opportunity — it is a requirement. Under hard counters, a growth timer that benches a player's only Chill carrier costs them a wave they had the answer to, which reads as unfair because it is. Nothing may make a counter carrier unavailable when a wave demands it.

The visual payoff survives intact: same profile, mass moved outward, one rig and a proportion curve. See §10.2.

## 1.6 Aberrant traits

A small pool of traits belonging to no species, entering the game **only through mutation**.

- Off the coverage ladder entirely. Carried or not; no tiers, no samples, no fusing.
- **No Aberrant trait may ever counter a raider.** They are unbuyable and unplannable, which makes them pure upside — and would make them a hard wall if any were required. They join Carapace, Litter, Regrow and Screen in the category of traits that answer nothing.
- **Aberrant Instincts** are the strongest chase item in the game. A creature acting in a way no bred lineage can produce is more distinctive than one covering slightly more sky, and it is visible in play rather than in a stat readout.

Because money cannot buy access at all (§2.8), money cannot buy an Aberrant at any price. This is structural rather than a rule someone must remember when writing pack contents.

## 1.7 Samples

**A sample is not a trait you don't have yet. It is an upgrade to a trait you already carry.**

- **Access** — whether a creature can answer a raider — comes only from breeding: inherited from a parent, or produced by mutation.
- **Coverage** — how much that answer covers — comes from samples. Fuse three tier-I into a tier-II and apply it to a creature already carrying that trait.

Samples exist for the twelve species traits only. No Instinct samples, no Aberrant samples.

Fusing is permanent and instant — no timer, no builder. Sample inventory, fusing and the retired-creature archive live on the **Sample Store** screen; its capacity is governed by the Gene Vault facility.

**Retirement yields samples and pedigree, never traits.** Retiring a creature banks coverage and preserves its lineage record. The trait itself dies with the animal, which is the point the whole system is making.

---

# §2 — The Splice

## 2.1 What happens

The player selects two parents. Both are **consumed**.

That single decision does most of the economic work in the game. Consumption means the player always needs more base stock, which is what makes harvesting worth doing, which is what makes relocating the Ark worth doing, which is what makes Apex Veins worth contesting. Without it, a player eventually assembles a perfect roster and the map economy becomes decorative.

It also creates the stake the title points at: individual creatures don't survive, the **broodline** does.

## 2.2 Resolution

The child has three slots. **The pools are separate** — combat slots fill only from the parents' four combat traits, the Instinct slot only from their two Instincts.

| Slot | Fills from | How |
|---|---|---|
| **Body** | Either parent's species | **Player chooses.** No randomness. |
| **Combat 1** | Any of the four parent combat traits | **Player locks it.** Guaranteed to carry. |
| **Combat 2** | The remaining three | Rolls, odds displayed |
| **Instinct** | The two parent Instincts | Rolls, modified by affinity and dominance |

Two controlled decisions, two rolls. Full determinism turns the game into a solved spreadsheet within a month of launch; full randomness makes a spent charge feel wasted and drives churn at exactly the wrong moment.

The lock matters more under hard counters than it would under a soft system. A player breeding toward Reach must be able to keep it through a generation — without the lock, breeding toward a counter would be luck rather than a plan.

One lock of three rather than two of four. With only three slots, locking two would make splices near-deterministic.

**Dominance.** Traits carry dominant or recessive expression, shown on the forecast. A recessive result carries the trait forward at lower coverage. This is safe because a downtier costs coverage and never access — the trait still works, it covers less, and re-fusing recovers it.

## 2.3 Mutation

Every splice carries a chance that a rolled slot produces a trait neither parent carried.

- **Base rate ~9%**, tuned at soft launch
- **An Aberrant sub-roll sits inside it.** Most mutations produce a species trait outside the parents' pool; a small fraction produce an Aberrant.
- Scales modestly with generation, giving late-game splices their own reason to exist
- The **Apex Vein catalyst** raises the Aberrant sub-roll specifically, not the base rate. This is what makes the Vein the route to an Aberrant rather than a generic splice booster.
- Mutation Surge events raise the sub-roll globally

Mutation is the game's slot machine, and it is important that it is the free one. The excitement lives in a mechanic every player has equal access to.

## 2.4 No failure state

The worst outcome is rolling traits the player already had. There is never a **SPLICE FAILED** screen. Losing two parents and a charge for a lateral result is disappointment enough; an explicit failure message converts disappointment into anger.

## 2.5 Generation and the coverage ceiling

Generation is `max(parent generations) + 1`, and it **gates how high coverage can climb.** A G3 creature holds tier II at most; tier III requires a deeper line.

Neither generation nor samples alone reaches the top. A deep line with no samples falls short; a sample stockpile on a G2 creature falls short. This is what keeps lineage mechanically meaningful rather than a bragging number, and it stops sample purchases from short-cutting generational investment.

The **Splicing Chamber** facility sets the maximum generation a creature can reach. A splice whose child would exceed the ceiling is blocked, with the upgrade surfaced directly in the message.

## 2.6 Odds are always visible

Non-negotiable. The probability table for the rolling slots, the mutation rate and the Aberrant sub-roll are all displayed **before the charge is spent.**

Hidden probabilities on a paid action is the mechanic regulators are currently most interested in, and concealing them buys nothing — players datamine it within a week and the goodwill loss is permanent.

## 2.7 Consumption is stated before it happens

Both parents are named above the CTA, the CTA itself states the cost, and a confirm step sits between the CTA and the reveal. Founders require a second dialog naming the creature.

Full copy and interaction states: `broodline_splice_confirm_spec.md`.

## 2.8 What money cannot buy

- **No purchase grants trait access.** Packs, pulls and Roulette spins grant samples, which are coverage. Every store description must reflect this.
- **No Aberrant is purchasable** at any price, because access itself is not purchasable.
- **No counter trait is ever purchase-only.**

Money buys rate. It never buys reach.

---

# §3 — Lineage

## 3.1 What the record holds

Every creature carries an ancestry record. The **Lineage View** displays a five-generation window of it — parents, grandparents, the traits each contributed, and where mutations entered.

Creatures that have left the roster persist here permanently. This is the memorial and the trophy case, and it is the single most shareable artifact the game produces.

## 3.2 Two ways a creature leaves the roster

Both are voluntary. Neither loses the record.

| | What it costs | What it returns |
|---|---|---|
| **Spliced** | The creature, as one of two parents | A child carrying its traits forward |
| **Retired** | The creature | Samples, and a freed roster slot |

**Nothing is ever lost involuntarily.** Combat never removes a creature — a creature at zero HP enters regeneration and returns. Raids take cargo, never roster. A player only ever loses a creature by choosing to.

That guarantee is what makes a destructive splice economy survivable. Permadeath in a game built on consuming creatures would make players hoard rather than splice, killing the loop everything rests on.

## 3.3 Founders

A player's first five creatures are flagged as **Founders** and named by the player. They appear at the root of every tree descended from them.

- **Founder 1** is named in the first session, immediately after the first creature is awarded
- **Founders 2–5** arrive across the first three days, each with an optional naming prompt and a sensible default
- All five are renameable at any time from the Roster

One name in the first session, the rest spread out. Five text-entry moments in one sitting is friction that loses people; the first name carries the emotional weight and the others are bonus.

**Founders can be consumed, but only deliberately.** Selecting a Founder as a splice parent triggers a second dialog naming the creature explicitly. The name is what makes a player stop and read — "Consume Ash" stops someone where "Consume Vetch Crawler" does not.

Blocking Founder consumption outright sounds kinder and is worse: it leaves five permanently dead roster slots by month six. One dialog is the cheaper fix, and the Founder's record survives in the tree either way. Only the individual is lost, which is the point the whole system makes.

## 3.4 Broodline Affinity

If three or more ancestors within the five-generation window carried the same trait, that trait gains **+12% inheritance probability** on the current creature.

Three rules hold it in place:

- **Inheritance only.** Affinity makes a trait more likely to carry. It never raises coverage, never adds damage, never touches combat values. It is a reliability bonus, not a power axis.
- **Hard-capped at one affinity per creature.** If it stacked, deep-lineage players would run away with the meta and every new player would be permanently behind — the failure mode that kills 4X games at the two-year mark.
- **It applies to Instinct as well as combat traits.** An Instinct running through a line becomes more likely to carry, which is what lets a player breed deliberately toward a behavior.

Affinity exists to make lineage mechanically real rather than decorative, and to reward breeding toward a plan instead of splicing opportunistically. Under hard counters it does useful work: a player who has bred a Chill line can rely on Chill surviving a generation.

## 3.5 The Lineage View

Two things, and the second is the one that gets used daily.

**The tree.** Five generations, each node showing species, generation, the traits it contributed, and a marker where a mutation or an Aberrant entered. Founders sit at the root with their given names.

**The species composition strip**, above the tree. *"This line carries Vetch, Hollow, Ember."*

The strip is the planning tool. Access comes only from breeding and species carry the counters, so the question a player actually asks is not "who are my ancestors" but "what can I breed for." Wanting Reach means needing a Hollow line, and the strip answers that in a glance where the tree takes eight nodes of reading.

The tree is the trophy. The strip is the tool. Both belong on the screen.

## 3.6 Why it ships early

The Lineage View is scheduled in the second build tier, ahead of the store and the alliance layer. Three reasons:

- It is the artifact players screenshot, and the only thing in the game no competitor has
- It is the closing beat of the first session — a player five minutes in should close the app carrying one image: a family tree with a creature they named at the bottom of it
- Under the trait model it is functional rather than commemorative, so delaying it delays a planning tool and not just a flourish

**Two custom metrics are worth building for it:** Founder naming rate, and Lineage View revisits in week one. If a large share of players skip naming, the emotional anchor is not landing. If nobody revisits the tree, the game's distinctive idea is not distinctive to players.

## 3.7 Sharing

A shared splice recipe carries its ancestry chain, so a player is sharing a **strategy** rather than a two-item combination. Recipe cards, lineage frames and Founder portraits are cosmetic, high-visibility, and attached to the screen players screenshot most.

---

# §4 — Combat

## 4.1 Format

Creatures are the towers. Raiders walk a fixed path toward the Gene Ark. If they reach it, the wave is lost.

The player's input is **placement and composition**, not moment-to-moment control. Once a wave starts, creatures act on their own according to their Instinct.

This is load-bearing. It makes combat playable one-handed in ninety seconds, it lets raid defences resolve while the player is asleep, and it means the breeding system rather than reflexes decides who wins. A player who spliced well beats a player who taps faster.

## 4.2 The battlefield

**Defenders go in pockets beside the lane, never on it.** Placement is about coverage, not mazing. This keeps combat legible on a phone and keeps composition, rather than layout puzzling, the deciding factor.

**The region sets the lane count, 1–3.** This is the game's cleanest difficulty dial: more lanes means more raiders arriving at once, which means more counters held simultaneously. It ties directly to region richness — an Apex Vein region is more dangerous to hold than a Common Vein one, so "is this spot worth it" gains a tactical answer alongside an economic one.

Relocating the Ark therefore changes defensive terrain as well as yield. Regions are hand-authored; there are roughly thirty and their tactical identity is worth the effort.

No elevation modifiers, no water tiles, no emplacement variety. Under hard counters those add noise to a decision already made at the splice screen.

## 4.3 Deployment

- **Five creatures maximum**, from a roster that may hold dozens
- Placement happens in a pre-wave phase with no timer — this is the thinking moment
- Creatures reposition between waves, never during
- **One Rally per wave**: a single tap granting a chosen creature roughly four seconds of doubled attack speed, on a thirty-second cooldown

Rally exists so an engaged player has something to do without making an absent player lose. It should be worth about 10% of a wave's outcome — enough to feel like agency, not enough that being away from the phone costs the fight.

## 4.4 The eight raiders

Each raider has exactly one answer. **A defence without the answering trait does not work.**

| Raider | Threat | Answered by |
|---|---|---|
| **Skirmisher** | Fast cheap fodder, arrives in eights | **Splash** · Ember |
| **Breaker** | Slow armoured siege, ignores taunts | **Pierce** · Hollow |
| **Lash** | Reaches past the front line to hit support and Collectors | **Taunt** · Vetch |
| **Courser** | Sprints the lane ignoring taunts; damage cannot kill it in time | **Chill** · Pale |
| **Brood** | Splits into three on death, then splits again | **Cinder** · Ember |
| **Drift** | Flier, crosses walls entirely | **Reach** · Hollow |
| **Bulwark** | Front shield, breaks only under rapid repeated hits | **Sprint** · Skitter |
| **Delver** | Burrows under the front line, surfaces mid-formation | **Burrow** · Loam |

There is **no damage floor**. Raw power does not substitute for the right trait — a rally with enough power on paper still fails against a Breaker wall if nothing in it carries Pierce. Composition beats numbers, and that is what sends players back to breed rather than to the store.

This model is only safe because access is guaranteed. Acquiring all six species yields all eight counters, campaign milestones grant specific species so no player is locked out by luck, and no counter is ever purchasable. If those guarantees weaken, the counter system becomes a paywall.

**Coverage decides scale, never whether the lock turns.** Chill I stops a Courser; Chill III stops a pack of them. A player with tier-I answers is always in the fight, just handling less of it.

## 4.5 Wave composition

Two rules govern every wave in the game.

**Never send two raiders answered by the same trait.** One perfect defender would clear the wave and the breeding pressure would evaporate. A legal wave of Courser, Drift and Brood demands Chill, Reach and Cinder simultaneously — three different species in the lineage.

**Maximum four raider types per wave.** Five deployed creatures at two combat traits each is ten trait slots, but no player reliably holds ten distinct counters in one deployment. Four is the ceiling the sample economy can supply; beyond it, waves demand roster breadth that nothing guarantees.

**Escalation is by volume, never by resistance.** Later waves send more raiders, across more lanes, in tighter simultaneity. No raider is ever immune to a tier-I answer. A wall that stops turning is a paywall wearing a difficulty curve.

## 4.6 Instinct on the battlefield

The six behaviours in §1.4 resolve here. Each is a target rule, sometimes a trigger, sometimes a response.

- **Every Instinct must be readable from watching one wave.** If a player cannot infer what it does by watching, it may as well be a hidden stat.
- **No Instinct is strictly best.** Bloodscent excels against swarms and struggles against a single armoured leader. Overwatch is superb across a long lane and wasted at a chokepoint.
- **Triggers fire visibly.** An animation state change or colour shift. The moment Last Stand kicks in should be legible from across the room.

Because Instinct counters nothing, it never compensates for a missing answer. It changes how well a correct composition performs, never whether an incorrect one can win.

## 4.7 The Trait Codex

Players must learn eight raider-to-counter associations, six Instinct behaviours, and what four utility traits do. There has to be somewhere to read that.

The Codex lists every discovered trait by category, what it counters, what coverage each tier provides, and a plain description of each Instinct's behaviour. Without it the splice screen's probability table is unreadable to a new player, and the counter system is learnable only by losing.

It is required, not optional. The counter model depends on the association forming, and the Codex is where a player checks it.

## 4.8 Wave types

| Type | Purpose |
|---|---|
| **Campaign** | The progression spine. Hand-authored, escalating, one new raider archetype introduced at a time. Yields samples, base stock and milestones. |
| **Replay** | Completed waves re-run for a reduced sample drip. This is the guarantee that a player at zero charges can still open the game and make progress, and it has to genuinely work. |
| **Region defence** | Periodic waves against local threats, scaling with region richness. Direct pressure on the "is this spot worth it" decision. |
| **Raid defence** | Resolved by the same engine, usually while the defender is offline. |

## 4.9 Raids

The engine runs with roles inverted. The defender's escort creatures are placed as towers along a short lane derived from the ambush terrain; the attacker's three-creature party is the incoming wave.

Attacking creatures use their full loadout including Instinct, so an attacking Skittish creature will retreat from its own raid — a real and interesting drawback.

Defenders benefit from terrain and emplacement; attackers benefit from choosing the moment. Neither side should win more than about 55% of the time at equal investment.

## 4.10 Auto-resolve and the replay

Auto-resolve must be a **full simulation of the same engine**, never a stat comparison rolled against a dice check.

Two reasons. If auto-resolve uses different maths, players will discover it and correctly conclude their placement and traits did not matter. And a simulation produces a **replay** — which is what makes losing tolerable. A defender who wakes to "you were raided, −40% cargo" is angry. A defender who can watch the fight, see their Skittish escort retreat at the wrong moment, and fix it, is engaged.

**Ship the replay viewer at launch.** It is the difference between PvP that builds a community and PvP that bleeds one.

## 4.11 Loss

- **No creature is ever lost in combat.** A creature reduced to zero HP enters regeneration — unavailable for 20–60 minutes depending on how the wave went.
- Regeneration is skippable with Gene Shards, a clean convenience sink that buys speed rather than power.
- A failed campaign wave costs only the regeneration timers. Retry immediately with a different roster.
- **Wave Defeat names the raider that broke through and the trait that would have answered it**, then offers a free retry. This is the screen that teaches the counter system, and there is no paywall on failure.

## 4.12 Guardrails

- Placement and composition decide outcomes; tapping speed contributes at the margin
- Auto-resolve uses the identical engine to live play
- No creature is lost involuntarily
- Raider composition is always visible before deployment
- Rally is capped so an absent player is not punished
- New species and traits in later seasons are sidegrades — a launch-era Vetch must still be viable in year two
- **The counter pool is closed.** A new raider must be answerable by an existing counter, or ship with its answer already in general supply.

---

# §5 — The Ark and the Map

## 5.1 The Mobile Gene Ark

The base is not fixed. It packs up, crosses the map, and unfolds somewhere richer.

Three phases, animated as one continuous transform rather than three assets:

1. **Deployed** — full footprint, antenna raised, facilities visible and producing
2. **Packing** — facilities fold inward, footprint contracts, antenna retracts. Production stops and harvest in progress is forfeited.
3. **Mobile** — compact travelling silhouette moving along the map route. **Cannot fight back in this state.**

Unpack is the reverse. The satisfying beat is the footprint expanding and facilities unfolding on arrival.

## 5.2 Relocation

Committing to a crossing is the game's recurring strategic decision. The screen's job is to make its costs legible before the player commits.

**The Ark is never a raid target, in any state.** Nothing aboard a relocating Ark is raidable: stored resources are not raidable, the roster is never at stake, and harvest in progress is forfeited on packing. Ark relocation takes the shortest path and its only variable is time. *(Amended 2026-09-05, register 6.1 — the earlier route table with intercept risk described Collector routes; see §5.6 and the Collectors & Raiding companion.)*

**Costs are always shown**, not discovered: harvest in progress is forfeited, defenders are stowed so there is no wave cover, and lab timers keep running unaffected.

Transit time scales with the **Drive** facility. A full Inner-to-Outer crossing is roughly three hours at base; the region roster gives per-hop times.

## 5.3 Resource nodes

| Tier | Rate | Lifespan | Character |
|---|---|---|---|
| **Common Vein** | **20/hour**, no cap | Never fully depletes; yield decays ~30% after a week of continuous harvesting | Safe fallback, low competition, **never claimable** |
| **Rich Deposit** | **60/hour**, `totalYield` **8640** (144 hours = six days) | Depletes in ~6 days of active harvesting | Worth relocating for, draws competition |
| **Apex Vein** | 8× base yield (design spec, not yet implemented) | Burns out in 48–72 hours | High-value, high-contest, alliance-scale |

**Rich Deposit duration is a consequence of the authored numbers: 8640 ÷ 60 = 144 hours = six days.** This duration syncs with the weekly rotation, producing a short window where a deposit runs dry a day before the map refreshes, which creates pressure to move rather than several days of dead ground.

**Apex Veins yield high-tier species samples plus a catalyst** that raises the Aberrant sub-roll on the next splice. This is what makes an Apex Vein the route to an Aberrant — it grants the chance, never the trait, which is what keeps Aberrants unbuyable.

## 5.4 Spawn and rotation

- **Common Veins are static.** Always present in every region. This is the livable baseline and it can never be taken away from anyone.
- **Rich Deposits rotate weekly.** A scheduled map refresh shifts a portion to new regions every seven days, timed to the weekly event cadence and the territory tick so one day a week the whole map reshuffles. That single day is the week's event, and it gives players one reason to log in rather than three scattered ones.
- **Apex Veins spawn semi-randomly**, announced roughly thirty minutes ahead with a seismic alert. This creates a rush moment and rewards alliances that react fast.

## 5.5 Harvesting

Harvesting is passive once the Ark is positioned. The Ark must be in the node's region, or an adjacent one at a reduced rate.

Yield scales with:

- **Harvest Array** facility tier
- Whether the player or their alliance controls the region
- Node richness tier

**Apex Veins require active harvesting** — a Collector unit physically present, which can be intercepted in transit.

**Contested nodes.** If one Ark is in range it takes full yield. If rival Arks are present simultaneously, yield splits proportionally to a presence stat combining time in region and Harvest Array tier — which rewards early arrival over last-minute swooping. Allied Arks harvest the same region at full rate simultaneously, so clustering is a mechanical advantage.

## 5.6 Collectors

Collectors carry cargo from node to Ark along a visible route, and rival players can intercept them.

- Collector class determines cargo capacity and escort slots
- Routes trade directly against risk: direct is fast and exposed, allied territory is slower and safer
- **Escorts are creatures**, and a creature on escort duty is unavailable for defence
- Allies can ride escort, which is the moment alliances feel useful rather than administrative
- **Raids take cargo in transit. Never roster, never stored resources, never the Ark.**
- **Three Collector routes, none strictly better.** Direct is shortest and fully exposed; the alliance corridor is slower and halves exposure at allied-controlled gates; the night move costs 120 Gene Shards and is never exposed.
- **Exposure is a fixed window at each gate region a convoy passes through**, and nothing — facility tier, class, alliance tech or purchase — shortens it. Drive makes runs faster; it does not make them safer. *(Amended 2026-09-05, register 6.2.)*

New players have full raid immunity for their first fourteen days, then a **one-week step-down**: days 15–21 are raidable with the loss cap ramping toward the full 40% and the loss shield firing on a single loss, and day 22 is full exposure. The end of full immunity is announced as an event with warning, ideally paired with an escort tutorial — never a silent flag flip. *(Amended 2026-09-05, register 6.3.)*

## 5.7 The map screen

The daily decision hub, and the screen players check every login.

- Regions coloured by state: rich, common, drained, contested, Apex active
- The player's Ark, ally Arks and hostile Arks as distinct markers
- Collector routes as animated paths, with intercept-risk markers
- Apex veins as pulsing concentric rings
- Tapping a region shows richness, controller, travel time, node types present, lane count, and live alerts

Lane count belongs on this screen. Relocation changes defensive terrain as well as yield, and hiding that would make the move a gamble rather than a decision.

## 5.8 The pressure curve

Sitting still is fine short-term and guaranteed to get worse. Common Veins decay, Rich Deposits deplete, Apex Veins burn out. This produces the weekly rhythm: check the map, see what rotated in, decide whether it is worth the transit risk.

**The guardrail: decay must never drop a stationary player below a livable baseline.** This should feel like leaving value on the table by not moving, never like punishment for standing still. Punishing inaction kills free-to-play retention; incentivising action is the healthier framing, and Common Veins existing permanently everywhere is what makes it true.

## 5.9 Monetization touchpoints

**Safe.** Scout reports revealing Apex spawns a few minutes ahead of the free alert — low power, high desirability. Relocation speed-ups. Ark exterior cosmetics, which have genuine display value since other players see the Ark on the map.

**The scout report sells detail and a head start, never access.** Free alerts always fire thirty minutes ahead. The report resolves blurred intel rows into real values and adds five minutes; it never reveals something a non-paying player cannot eventually see.

---

# §6 — Alliance and Territory

## 6.1 Founding principle

**Control confers advantage, never exclusion.**

A player locked out of the map churns. A player operating at a disadvantage joins an alliance. Every number in this section is set to keep the second true and the first impossible.

Territory is the game's social gravity — the reason to be in an alliance rather than adjacent to one, and the only system that makes another player's login schedule matter to you. It also carries the highest toxicity risk in the design, which is why §6.8 is not trimming.

## 6.2 Structure

- **40 members maximum**
- **Roles:** Founder, up to five Officers, Members
- **Officer permissions:** place and withdraw Claim Stakes, assign garrison creatures, initiate rallies, spend the treasury
- **Joining:** open, application-gated, or invite-only, set by the Founder

Officers matter more here than in most games because garrison assignment costs individual members real resources. Handing that to five people is a trust decision, and the interface must make who assigned what fully visible.

**On the cap.** Forty is kept, and the debate about it is somewhat misplaced. An alliance of forty with twelve actives behaves like an alliance of twelve; the binding constraint is activity, not capacity. Design effort belongs in the decay rules at §6.9 rather than in tuning the ceiling.

## 6.3 Claiming

An alliance claims a region by placing a **Claim Stake**, which accumulates **Hold**.

Hold accrues from **member presence** (Arks in the region, weighted by time), **harvest volume** (cargo delivered from that region's nodes), and **garrison strength**.

**Control resolves on the weekly tick**, synced with the Rich Deposit rotation and the weekly event. One day a week the map reshuffles and territory changes hands at the same moment. That is one reason to log in rather than three scattered ones.

If two alliances hold Stakes in one region it is **Contested** — Hold accrues to both, control goes to the higher total at the tick, and **the losing Stake is refunded rather than destroyed.** Losing a contest should cost a week, not an investment.

## 6.4 Stake Assault

Pure accumulation would make territory a spreadsheet. Assault is the fight.

Any member may assault a rival Stake in a region where their own alliance also holds one. It resolves through the combat engine: the defending garrison are the towers, the assaulting player's deployment is the wave. The counter system applies unchanged — a garrison built around a Breaker wall holds against any rally carrying no Pierce, regardless of raw power.

- A successful assault removes a fixed chunk of accumulated Hold
- **Assaults are rate-limited per player per day**, so Hold cannot be zeroed by one motivated person with no sleep schedule
- Fallen garrison creatures enter regeneration normally and are never lost
- The defending alliance is notified and can reinforce

This is the intended peak-coordination moment: a contested Apex region, two alliances trading assaults across a week, garrisons rebuilt between them.

## 6.5 Garrisons

Garrison creatures are contributed by individual members and are **unavailable for anything else while garrisoned** — no raids, no escorts, no campaign, no wave defence.

That cost is the point. It makes territory a genuine investment rather than a passive bonus, and it creates the conversation alliances exist to have: who contributes what, and is this region worth it.

Under hard counters the cost bites harder than it would otherwise. Garrisoning a Chill carrier means having no answer to Coursers at home until it returns. That is a real decision, and it is the player's own — it never becomes an involuntary loss.

- Garrison capacity scales with Stake maturity; a fresh Stake defends thinly
- **Contributions are publicly visible within the alliance.** Alliances self-police contribution far better than any system, provided they can see who is carrying weight.
- Members may withdraw a garrisoned creature at any time, on a short cooldown

## 6.6 What control confers

| Benefit | Effect |
|---|---|
| **Cooperative harvest** | Allied members harvest the region's nodes at full rate simultaneously, no splitting |
| **Non-ally penalty** | Hostile Arks harvest at **−40%** — a penalty, never a lockout |
| **Interception advantage** | Hostile raiders are less effective on routes through the region |
| **Convoy staging** | Multi-member convoys depart from controlled regions only |
| **Regional banner** | Cosmetic display on the map |

**The −40% is the most important number in this section.** It must be painful enough that alliances want control and negotiate over it, and mild enough that an unaffiliated player can still work a contested region and progress. If it ever becomes an effective lockout, the map closes and new players leave.

## 6.7 Alliance tech

Members contribute Gene Shards to a treasury, unlocking account-wide perks. Three branches, all **logistics and convenience**:

- **Convoy** — better Collector classes, additional escort slots, faster transit
- **Extraction** — faster Hold accrual, increased garrison capacity
- **Lab** — reduced regeneration timers, faster charge regen

**No combat power, and no trait access or coverage.** This is what stops the top alliance compounding into unbeatability. A member of a brand-new alliance fields exactly as strong a creature as a veteran; what they lack is throughput, which effort closes. Alliance tech granting counter access would break the same guarantee a store purchase would.

## 6.8 Anti-monopoly guardrails

The load-bearing section.

- **Common Veins can never be claimed.** Neutral and fully harvestable by anyone, permanently. No player can ever be starved off the map.
- **Five concurrent Stakes per alliance, maximum.** A dominant alliance must choose which regions matter. Even at peak strength it cannot own the map.
- **The −40% penalty is capped** and can never be raised by tech, purchase or event.
- **No base attacks.** Nothing lets one player damage another's Ark, roster or stored resources. Raiding takes cargo in transit; assaults take Hold. Player reviews of comparable titles consistently name base-burning as the churn trigger, and this design simply does not contain the mechanic.
- **New alliance grace:** alliances under fourteen days old cannot have Stakes assaulted.

## 6.9 Inactivity and decay

Dead alliances squatting on rich regions is the slow failure that strangles a server in month eight.

- A Stake with no member presence in its region for **five days** begins losing Hold
- At zero Hold the Stake dissolves and the region returns to neutral
- Alliances below five active members across a fourteen-day window have their Stake cap reduced

Decay must be visible and warned well ahead. An alliance losing territory should see it coming for days, not discover it afterwards.

## 6.10 Solo players

A meaningful share of players will never join an alliance, and they remain a viable audience.

- Full access to Common Veins everywhere, permanently
- Rich Deposits and Apex Veins at −40% in controlled regions, full rate in neutral ones
- Ineligible for cooperative harvest, alliance tech, convoys and Stake mechanics
- Campaign, splicing, region defence and competitive events fully available

**Solo play is slower, not blocked.** Alliance membership should be attractive rather than compulsory, and the difference should read as opportunity cost rather than punishment.

## 6.11 Monetization

**Safe:** alliance banners, Stake skins, regional display flair — all highly visible on the screen everyone checks daily. Treasury-funded alliance-wide cosmetics, giving contribution a visible payoff.

**Never ship:**

- **Purchasable Hold**, or anything accelerating it with money. This converts territory from an effort contest into a spending contest and is the fastest available route to a pay-to-win reputation.
- **Additional Stake slots for money.** The cap of five is a competitive-health mechanism, not an inconvenience to be monetized.
- **Increases to the non-ally penalty.** It is a fixed constant.

Territory is where monetizing the wrong lever is least recoverable, because the damage lands on non-payers who then leave.

---

# §7 — Progression

## 7.1 Two ladders, two jobs

These must never blur. If they start granting overlapping perks, players stop being able to tell what they are progressing toward.

| | **Geneticist Tier** | **Gene Lab** |
|---|---|---|
| Earned by | XP — logins, events, purchases | Gene Shards plus time |
| Represents | Who you are | What you have built |
| Grants | Convenience, cosmetics, small accelerations | Capacity and throughput |
| Shape | One linear 12-rung ladder | Six parallel facilities |

Tier is a status ladder. The Gene Lab is a base.

## 7.2 The six facilities

The Gene Lab is an isometric base view. Not one number going up — six facilities, independently upgradeable, so a player is always choosing what to invest in rather than following a forced path.

| Facility | Governs |
|---|---|
| **Splicing Chamber** | Maximum creature generation, and therefore the coverage ceiling |
| **Hatchery** | Roster capacity — **floor of 20**, scaling upward |
| **Gene Vault** | Sample capacity |
| **Harvest Array** | Yield rate, and presence weight in contested regions |
| **Drive** | Relocation and Collector transit speed. **Never the exposure window** — see §7.7 |
| **Core** | Ark integrity in PvE region defence; caps every other facility |

Different players prioritise differently and that is the point. A raider invests in Drive and Harvest Array. A breeder pushes Hatchery and Splicing Chamber. Neither is wrong, and their Gene Lab screens should look visibly different by month three.

**Roster capacity never binds below what the counter system requires.** Holding all eight counters takes four creatures at best distribution, and a player also needs five deployed plus garrison and escort commitments. Twenty is the floor, not the ceiling — a cap that forces a player to drop a counter is a soft lockout.

**Core's integrity stat applies to region defence and nothing else.** Raids never target the Ark and §6.8 forbids base attacks, so this must not become a PvP stat.

## 7.3 Core as the spine

**No facility may exceed Core's tier.** Core is the expensive, slow, deliberate upgrade — the town-hall pattern, which works because it forces periodic hard commitment rather than continuous drip.

Twelve tiers. Each requires Gene Shards on a steepening curve, a build timer, and **a campaign milestone**.

That last requirement is the most important guardrail in this section. **Core progression cannot be bought past.** A player who spends heavily on day one accelerates through the shard and timer costs and then stops, blocked at the same campaign wall as everyone else.

This is what keeps money buying speed rather than skipping — the line the whole design draws, and the one a progression system crosses most easily.

## 7.4 Timers

Timers are the sink; that is the genre. But they are also the single most-cited complaint in reviews of comparable titles, so the position is deliberate.

- **One upgrade in progress at a time.** A second concurrent slot unlocks at Core tier 8 — earned, never purchased.
- **Maximum timer of 48 hours**, at the very top of the Core track. The genre norm of multi-day waits is not worth the retention cost.
- **Early tiers are near-instant** — the first six or so Core tiers complete in minutes to hours, so a new player feels the system moving.
- **Speed-ups are always purchasable and always earnable.**
- **Never block the whole game on a timer.** A player with an upgrade running can still harvest, splice, run campaign waves, raid and defend.

The failure mode to design against: a player opens the app, sees a three-day timer and nothing else to do, and closes it. Every timer should have at least three other things competing for the next sixty seconds.

## 7.5 Geneticist Tier

Twelve tiers gated by cumulative XP, earned free through daily logins, streaks, events and first-time achievements, and accelerated by purchase.

Every tier perk is **convenience or cosmetic, never raw combat power**:

| Tier | Perk |
|---|---|
| 2 | +1 Splice Charge cap |
| 4 | 10% faster charge regen |
| 6 | Daily free sample pull |
| 8 | Second simultaneous splice queue |
| 10 | Cosmetic creature aura |
| 12 | Unique title, permanent +15% Gene Shard bonus on purchases |

**Tier 6 grants a sample, never a trait.** The original spec described a daily rare-*trait* pull; because Tier progress is purchasable through shard-to-XP conversion, a tier perk granting trait access would be a purchase path to access. Samples are coverage, and coverage is buyable.

**Tier 8 is the strongest perk in the game** once splicing is destructive, because throughput is the real constraint.

## 7.6 The sample economy

New material — no source document covers this. Structure is settled below; **every number is a starting point for soft-launch tuning.**

### Sources

| Source | Yields |
|---|---|
| **Wave completion** | The primary drip. Low-tier samples, always flowing. |
| **Node harvesting** | The main volume. Rich Deposits and Apex Veins yield progressively higher tiers. |
| **Campaign milestones** | Fixed, specific samples, so no player stalls on bad luck |
| **Splice Roulette** | The gacha wheel. Samples, never traits. |
| **Retirement** | Banks the retired creature's coverage |

**Harvest drops are weighted toward traits the player's roster already carries.** Samples for traits nobody in the roster holds are dead inventory taking up capacity, and dead inventory makes the pressure valve feel arbitrary rather than meaningful. Campaign and event sources stay unweighted so a player can deliberately build toward something new.

### Fusing

**Three samples of a tier fuse into one of the next.** Permanent and instant — no timer, no builder. A tier-III trait therefore costs nine tier-I samples of equivalent value.

Fusing is the sink that makes capacity matter, and capacity is what makes fusing urgent.

### Capacity

Governed by the Gene Vault facility. **At 88% the display turns and warns; above it, new samples from waves are discarded.**

This is safe. Samples are coverage, never access, so a discard costs recoverable progress and can never strand a player without an answer to a raider. The valve creates real pressure to fuse without ever creating a lockout.

*Starting values: capacity around 20 at Gene Vault tier 1, scaling to roughly 60 at tier 12.*

### The catalyst

Apex Veins yield a **catalyst** alongside high-tier samples. A catalyst raises the Aberrant sub-roll on the next splice.

This is the only route to an Aberrant that a player can influence, and it is deliberately tied to the map's most contested content rather than the store. Catalysts are earned by participating in Apex windows — which is what makes those windows matter beyond their yield multiplier.

*Starting value: one catalyst per successful Apex Vein extraction.*

## 7.7 What progression must never do

- **No combat power.** No facility increases creature damage, health, or the five-creature deployment cap.
- **No PvP advantage.** Facility tier must not influence raid outcomes, interception or Stake Assault. A high-tier player has more throughput, not stronger creatures.
- **No trait access.** Every trait stays reachable at any facility level. The Splicing Chamber gates generation; generation gates coverage; neither gates access.
- **No hard lockouts.** A low-tier player harvests more slowly and holds fewer creatures. They are never excluded from content.

Money and time buy rate, never reach.

---

# §8 — Monetization

## 8.1 The rule

**Money buys rate. It never buys reach.**

Every currency has a free path and a paid path, and the paid path only ever makes the free one faster. Nothing in the game is reachable by payment alone.

This is not a values statement — it is what holds the counter system together. A player without the right trait loses to its raider regardless of spending, so the moment a trait becomes purchasable, the game becomes a paywall. §8.6 states the rule in the form the people writing pack copy actually need it.

## 8.2 Currencies

| Currency | Free path | Paid path | Spent on |
|---|---|---|---|
| **Splice Charges** | Regen over time, daily login, rewarded ads | Packs, Season Pass | Splicing |
| **Gene Shards** | Wave completion, achievements, events | Direct purchase | Facility upgrades, timer skips, cosmetics, Roulette |
| **Geneticist XP** | Daily login streak, events, achievements | Included in packs; shard conversion | The 12-tier ladder |
| **Marks** | Raiding and defending convoys, win or lose | **None. Never purchasable at any price.** | Marks Shop: raid convenience and cosmetics |

**Marks** exist so raid rewards live in a currency money cannot reach. The moment PvP currency has a dollar price, every raid becomes a spending comparison. *(Added 2026-09-05, register 6.4.)*

**Charges:** cap of 5, regenerating one per 25 minutes. Cap rises with Geneticist Tier to a maximum of 10.

**Charges never fully block play.** At zero charges a player can still replay completed waves for a sample drip, run region defence, manage their roster, dispatch Collectors, harvest, and claim event rewards. This guarantee has to genuinely work — it is what makes the charge system feel like pacing rather than a gate.

Free routes to more charges: escalating daily login streak rewards, a rewarded video for +1 charge capped at three per day, and weekly event completion.

## 8.3 Store structure

### Free daily gift

**The Packs tab opens with a free daily gift.** Generosity first is what makes the paid tiers underneath feel fair rather than grasping, and it gets non-payers opening the store, which is where conversion eventually happens.

### Fixed packs

| Pack | Contents | Price |
|---|---|---|
| Starter Splice | 5 charges · 100 shards · 500 XP | $0.99 |
| Lab Bundle | 15 charges · 600 shards · 1,500 XP · 1 **sample pull** | $4.99 |
| Lab Expansion | 40 charges · 1,400 shards · 5,000 XP · 3 **sample pulls** | $9.99 |
| Mythic Lab Access | Unlimited charges for 48 hours · 1,500 shards | $14.99 |
| Geneticist's Vault | 100 charges · 3,200 shards · 15,000 XP · 8 **sample pulls** · exclusive skin | $19.99 |

These are the five mixed-content bundles. Direct shard purchases are separate products:

| Product | Contents | Price |
|---|---|---|
| Vault of Shards | 9,000 shards | $49.99 |
| Reserve of Shards | 20,000 shards | $99.99 |

**Sample pulls, not trait pulls.** The original spec described these as rare trait pulls, which would have made counter access purchasable and broken the model on the store page. Pulls grant samples, which raise coverage on traits a player already holds.

First purchase of any pack grants **2× contents, one time only.**

### Custom Chest

Pick any three of six reward slots — charges, shards, XP, sample pulls, cosmetic fragments, speed-ups. The selected tier sets the price and the amount in every slot:

| Reward slot | Small · $1.99 | Standard · $4.99 | Large · $9.99 |
|---|---:|---:|---:|
| Charges | 6 | 15 | 30 |
| Shards | 250 | 600 | 1,200 |
| Geneticist XP | 1,000 | 2,500 | 5,000 |
| Sample pulls | 1 | 2 | 4 |
| Cosmetic fragments | 4 | 10 | 20 |
| Speed-ups | 2h | 6h | 12h |

Standard is the baseline reward set. Small is approximately 40% of Standard, with discrete rewards and timer rounding; Large is 200% of Standard. The store always shows the chosen tier, exact contents and price. **It makes no savings or discount claim:** separate-item reference prices do not exist, so a percentage would be invented. Add a live value strip only if a later economy revision establishes truthful reference prices.

This is the highest-converting mechanic available, because players stop feeling like they are paying for things they do not want.

### Season Pass

Four-week seasons. Free track carries modest charges, shards and cosmetic fragments. Paid track at $9.99 carries two to three times the free track's rewards, plus exclusive skins and XP boosts.

**The paid track is more of the same currency, faster — never exclusive power.** Any creature or trait on the paid track must be obtainable free, just slower, and the pass should say so plainly.

## 8.4 Live-ops calendar

Static stores go stale. Every event ships with its own themed offer.

| Event | Cadence | Mechanic | Offer |
|---|---|---|---|
| **Gene Lab** | Weekly | Server-wide splice goal with milestone thresholds | Limited-time bundle tied to the event |
| **Splice Roulette** | Bi-weekly | Sample wheel with displayed odds | Discounted extra spins |
| **Apex Cup** | Monthly | Competitive leaderboard using your best creatures | Cosmetic flair and charges |
| **Mutation Surge** | Seasonal | **Raises the Aberrant sub-roll globally** | Limited cosmetic and charge pack |
| **Recipe Share** | Ongoing | Players share and rate splice recipes with ancestry chains | Purchasable recipe card frames |

Mutation Surge is the one event that changes the odds of something unbuyable, which is what makes it matter rather than being a reskinned banner. **It raises the chance; it never grants the trait, and its offer never contains one.**

Cap FOMO events at a reasonable frequency. Burnout kills lifetime value faster than a missed sale.

## 8.5 The trial hook

The single highest-leverage tactic in the model, and it costs nothing.

1. **The first charge wall** — usually session two or three — automatically grants a **free 24-hour Double Regen trial.** No purchase, no prompt, no card.
2. **When it expires**, offer to extend: Gene Shards for two more days, or a small real-money pack for permanent Double Regen.

Let them feel the upgrade before asking for money. This converts far better than a cold paywall.

**No purchase prompt in the first session.** Not the first-purchase 2× offer, not a starter pack, nothing. Spending that leverage early spends it for nothing.

## 8.6 What a pack may contain

The practical form of §8.1. Three separate systems have already leaked trait access — store packs, alliance tech, and the Geneticist Tier 6 perk — none of them deliberately. Anyone writing an offer should read this table rather than reasoning from principles.

**May be sold:**

- Splice Charges, Gene Shards, Geneticist XP
- **Samples** and sample pulls — these are coverage
- Timer speed-ups and regeneration skips
- Cosmetics of every kind: skins, auras, banners, Stake flair, recipe frames, Ark exteriors
- Scout reports — detail and a head start, never access

**May never be sold, at any price, in any bundle, as any reward:**

- **Traits.** Not rare ones, not common ones, not "a creature with a guaranteed trait" where the trait is a counter.
- **Aberrant traits**, which follows from the above since access itself is unsellable.
- **Catalysts.** Aberrants are the one thing in the game money cannot reach even probabilistically, and that is worth more as a community and marketing asset than catalyst revenue would be. Catalysts come from Apex Veins and Mutation Surge only.
- **Hold**, or anything accelerating it.
- **Additional Stake slots.** The cap of five is a competitive-health mechanism, not an inconvenience.
- **Campaign milestone bypass** on Core tiers. This is the anti-whale guardrail; selling around it defeats the entire progression structure.
- **Additional concurrent upgrade slots.** Core tier 8 is the intended gate.
- **Increases to the non-ally harvest penalty.**

**The test for any new offer:** does it make something faster, or does it make something possible? Faster is sellable. Possible is not.

## 8.7 Guardrails

- Charges never fully block progress — there is always something playable at zero
- Rewarded ads are always available as a free lever, capped daily to avoid fatigue
- Every trait is obtainable free; money buys attempts, never outcomes
- Top Geneticist Tier perks stay convenience and cosmetic
- Odds are displayed before any charge or spin is spent
- No offer appears in the first session
- No purchase prompt sits in the splice confirmation path — selling a way to avoid loss at the moment of loss is the worst available moment to monetize

---

# §9 — First Session and First Fortnight

## 9.1 The rule

**One new system per session, and the first sixty seconds are play.**

No cinematic, no lore crawl, no tour of an empty base. A wave is already coming when the app opens.

Eight sections above describe systems a player will meet over two years. This one covers the five minutes that decide whether they meet any of them.

### The advertising advantage

Comparable titles draw consistent criticism for a mismatch between marketing and product — promotional creatives show a tower-defense minigame while the actual game is an idle 4X. Players arrive expecting one thing and find another, and the complaint appears in reviews and coverage.

**Broodline's core loop genuinely is tower defense.** The ad and the game can match, which is a real and unusual advantage in this category. It should be protected: whatever the creatives show, session one must deliver it within thirty seconds. If marketing later drifts toward showing something the game is not, the retention cost lands immediately in day-one numbers.

## 9.2 Session one

Target: **under six minutes**, ending on the game's most distinctive screen.

| # | Beat | Purpose |
|---|---|---|
| 1 | **Cold open.** A wave is incoming. Two creatures are handed over with one instruction: place them. | Play in under twenty seconds. Delivers the ad promise. |
| 2 | **The wave resolves.** Creatures act on their own via Instinct. The player watches and wins. | Teaches that placement decides outcomes, not tapping. |
| 3 | **Creature drop.** A third creature is awarded. | First acquisition. |
| 4 | **Name it.** Founder naming, one creature only. | The emotional anchor, placed before any complexity. |
| 5 | **Second wave.** Three creatures against a raider the player can already answer. | Introduces counters by letting them work, without explaining them. |
| 6 | **First splice.** Guided, using two *provided* creatures — never the named one. | The core loop. |
| 7 | **Mutation fires.** Scripted, guaranteed. | The player sees the best case once and knows it exists. |
| 8 | **Lineage View.** A two-generation tree with the named Founder at its root. | Ends on the thing nothing else does. |

Beat 8 is the whole argument for the session. A player closing the app after five minutes should be carrying one image: a family tree with a creature they named at the bottom of it.

**Beat 5 teaches by success, not explanation.** The raider is one the player's creatures can already counter, so the trait works and the wave is won. Nobody is told about the counter system. The lesson lands in session two.

## 9.3 The designed first loss

**Under hard counters, a player must lose a wave for lack of the right trait — early, safely, and with the reason named.**

This is the most important addition the counter model makes to onboarding. A player who first encounters an unanswerable raider at day ten, after investing real time, reads it as the game breaking. The same experience in session two or three, with two creatures at stake and a free retry, reads as a rule being taught.

Design it deliberately:

- A wave containing one raider the player's current roster cannot answer
- **Wave Defeat names the raider that broke through and the trait that would have answered it** — the screen exists for this moment
- The counter trait is available immediately: a campaign reward, a species in the next drop, something the player can act on within minutes
- **Free retry. No paywall on failure, ever.**

Get this right and the counter system is understood by day three. Get it wrong and it is discovered as frustration in week two.

## 9.4 Founder naming

Five Founders, five naming prompts, and five text-entry moments in one session is friction that loses people.

- **Founder 1** is named in session one, beat 4
- **Founders 2–5** arrive across days one to three, each with an optional prompt and a sensible default
- All five renameable at any time from the Roster

**Five Founders across six species.** The starting five should spread across species to cover the counters early waves demand; the sixth arrives as an early campaign milestone. That gives the player a legible first goal — a species they can see they are missing — and it starts delivering on the guarantee that acquiring all six yields all eight counters.

## 9.5 Teaching consumption

Splicing destroys both parents. That is the hardest thing in the game to teach and the easiest to get wrong.

- The two tutorial parents are **provided specifically for it** and framed as sample stock
- **The named Founder is visibly locked out** of both parent slots during the tutorial
- Destruction is stated plainly before the confirm, in the same language the live game uses
- **Immediately after, the Lineage View shows both consumed parents still present in the tree**

That last beat is the actual lesson: **individuals are consumed, the record survives.** Teaching it in the first splice, with nothing at stake, means the player already understands the trade the first time it costs them something real.

Do not soften this. A tutorial that hides consumption produces a player who discovers it in week two by destroying something they cared about.

## 9.6 Drip schedule

| When | Introduced | Gated because |
|---|---|---|
| Session 1 | Combat, splicing, lineage | The core loop, nothing else |
| Session 2 | Splice Charges as a constraint, Roster, trait basics | Charges mean nothing until the player wants more splices |
| Session 2–3 | **The designed first loss** (§9.3) | The counter lesson, taught safely |
| Session 2–3 | **Charge wall → free 24hr Double Regen trial** | The designed hook moment |
| Session 3 | Gene Lab, first facility upgrade (near-instant) | Gives the base screen a reason to exist |
| Day 2 | World Map, Region Detail, first relocation | Needs a reason to move — introduce alongside a visibly better node |
| Day 2–3 | Harvesting, Common and Rich nodes; **Sample Store** | The economy behind the loop |
| Day 3–4 | Region defence waves | Ties the map back to combat |
| Day 5–7 | Collector dispatch, first cargo run | Safe: raid immunity is active |
| Day 7 | Alliance prompt | Only once the player has something to contribute |
| **Day 14** | Full immunity ends; step-down week begins at a reduced loss cap | Announced as an event with warning |
| Day 22 | Full raid exposure | The step-down has made the first losses cheap and legible |
| Week 3+ | Apex Veins, catalysts, Stakes, garrisons | Requires an alliance and a mature roster |

The two most important entries are the day-two map introduction and the day-fourteen start of raid exposure. **The map must arrive attached to a specific, visible better node** — abstract freedom to relocate teaches nothing. And **raid exposure must be announced when immunity lapses**, not discovered by being raided.

## 9.7 What stays hidden

Deliberately absent from session one:

- The store. No offers, no first-purchase banner, no currency shop.
- Splice Roulette, Season Pass, Geneticist Tier
- The Sample Store, the alliance tab, anything on the map beyond the current region
- Coverage tiers and probability tables — the tutorial splice shows outcomes, not odds

**Bottom navigation begins with two tabs and reveals the rest as systems unlock.** A five-tab bar on first launch is a wall of unexplained choices.

## 9.8 What to measure

Soft-launch instrumentation, in rough priority:

- **Completion rate of session one**, and drop-off by beat. Beat 6, the first splice, is the likeliest failure point.
- **Day 1, 3 and 7 retention**, benchmarked against the category
- **Time to first splice** — target under four minutes
- **Founder naming rate.** If many skip it, the emotional anchor is not landing and beat 4 needs rework.
- **Lineage View revisits in week one.** The proxy for whether the game's distinctive idea is distinctive to players.
- **Recovery rate after the designed first loss.** If players who hit §9.3 do not retry and win within a session, the counter lesson is landing as frustration rather than instruction. This is the metric the whole combat model rests on.
- **Trial acceptance and conversion at expiry**
- **Day-14 churn spike.** A visible cliff means raid loss caps need tightening.

Founder naming rate, Lineage revisits and first-loss recovery are the three custom metrics worth building. Everything else is standard.

---

# §10 — Art Direction

## 10.1 The register

Warm, rounded, legible. Paper-white surfaces with violet as the brand and CTA colour, species colours carrying identity, and a rounded display face paired with a humanist sans.

An earlier direction proposed the opposite — a sterile clinical laboratory in charcoal and steel with creatures as the only warmth. It is a good idea and it is not what got built. The executed direction is coherent, tokenized and complete, and it wins on that basis.

Two requirements from the abandoned direction carry over, both functional rather than aesthetic. They are in §10.6.

**Amended 2026-09-23 (Phase 10, "The Living Frontier").** The register is now warm ivory and parchment surfaces (`#F5EBD9`, `#FFF8EB`) over a deep slate backdrop (`#263345`), violet (`#6B4EC2`) as brand and action, gold (`#E9B44C`) reserved for rewards and milestones, the six species colours unchanged. Creatures **have faces**: readable eyes, gaze, eyelids and small facial movement, with species-specific posture. This supersedes Phase 9's "personality lives in posture, gait and the trait parts, not in a face" ruling; silhouette and the two trait parts still carry combat identity, the face carries attachment. The world is a lush, unfamiliar frontier and the Ark a crafted travelling sanctuary — enamel, warm wood, brushed brass, glass, restrained violet energy. The reference is `specs/Designs/visual-upgrade-mockups-v1/` and the plan is `specs/plans/broodline_visual_upgrade_plan.md`; what is *not* adopted from the Kingshot reference — medieval humans, castles, rarity ladders — is recorded there.

## 10.2 Three shape rules

Hand these to the illustrator verbatim. No exceptions.

**1. Silhouette carries the role.** Wide and low is a wall. Tall and narrow is ranged. Spindly is fast. A player should name the role before reading the card. All six species must be distinguishable as flat black shapes at 40px; if two are confusable, one is wrong.

**2. Hybrids add parts, never average them.** A hybrid keeps one parent's body and bolts on the other's signature part. Muddy in-between blends kill both the read and the collecting.

**3. Growth exaggerates, never redesigns.** Runt to apex is the same creature with mass moved outward — bigger head, heavier limbs, same profile. Implemented as a proportion curve over one rig, never four separate assets. Players must keep recognising their own animal.

## 10.3 The pipeline

The trait model makes this tractable in a way it would not have been at four slots.

A creature is **one body plus two visible trait parts plus one Instinct cue.**

| Asset class | Count |
|---|---|
| Species bodies | 6 |
| Trait parts | 12 |
| Instinct cues | 6 — **contingent, see below** |
| **Total** | **24** |

**The six Instinct cues are not settled, and this section and §10.4 disagree about them.** §10.4 gives Instinct a card badge when static — *because* a third element on the body would muddy the silhouette — and behaviour plus a trigger state in combat. Neither of those channels is body geometry, so the count above may be six, one shared cue, or none. What decides it is whether Bloodscent, Vanguard and Overwatch can be identified in a busy wave: §1.4 gives those three no trigger, so they have no in-combat signal beyond who they choose to attack. That is a playtest question and it is recorded at `broodline_rig_proof.md` §9.3.

**`sk_crown` is authored on every body regardless of the answer.** A named transform costs nothing, and retrofitting an attachment point onto finished bodies is the expense this section exists to prevent. The rig proof mounts one cue there to prove the socket works — a socket that has never carried geometry is not standardised.

Those 24 assets assemble into roughly 2,400 distinct creatures. At four trait slots the same approach would have needed double the parts and produced silhouettes too busy to read at 40px — the reduction to two combat traits is the single largest cost saving in the design.

**Attachment points must be standardised before the first creature is modelled.** Retrofitting modularity onto hand-built creatures is the most expensive mistake available here.

**Animation is body-level, never trait-level**, or the budget is unbounded.

**This pipeline exists, as of Phase 9.** Six species bodies, three raider bodies
and twelve trait parts are generated from C# recipes in `Broodline.Creatures`
(`client/Assets/Creatures/`) into committed meshes, materials, prefabs and baked
sprites, assembled by slot per `broodline_rig_proof.md` §3.1. `sk_crown` is
authored on every body and carries nothing, as this section requires. The
governing design is `specs/plans/broodline_phase9_the_look.md` §3, whose §10
records where its own numbers were wrong.

## 10.4 Trait visibility

**The two combat traits are visible on the body.** Under hard counters a player scanning the roster must see at a glance who carries Chill and who carries Reach — that read decides waves. This is the single most important functional requirement in the art direction.

**Coverage tier is not shown on the body.** Tier lives on the card as pips. Three tiers, three pips. Showing tier physically would mean three variants of every trait part and would triple the pipeline for information a number conveys better.

The split is clean: **the body says which, the card says how much.**

**Instinct uses two channels depending on context.** On a static card it is a badge — the body is already carrying two trait parts and a third would muddy the silhouette. In live combat it is the behaviour itself, plus a visible trigger state: an animation change or colour shift when Last Stand fires or Skittish repositions. A player watching a wave should be able to infer an Instinct without reading anything.

**The trigger channel only covers half of them.** §1.4 gives Last Stand, Skittish and Pack Sense a trigger; Bloodscent, Vanguard and Overwatch have none. For those three the whole in-combat signal is targeting behaviour — who the creature picks — and Overwatch's is the subtlest, being a range and speed change rather than a choice of victim. Whether that is enough at a hundred entities is untested, and it is what decides §10.3's contingent cue count. If it is not enough, the answer is a second flat channel rather than geometry: colour never carries information alone here, so a shape-bearing badge, not a tint.

**Aberrant traits take an iridescent white-hot treatment** with subtle motion — the only animated treatment in the system. As a class rather than a tier, an Aberrant is a distinct marker rather than a fourth pip. Keeping it white rather than gold avoids colliding with the map's gold Apex Vein pulse.

**Colour never carries information alone.** Pip count works in greyscale; species colour is reinforced by silhouette.

### A constraint on the species art, measured 2026-09-17

**A Pale creature cannot be drawn as one flat colour.** Phase 8 tinted six
interim proxies and measured each species colour as a flat fill on the creature
card's `--surface-sunk` `#f8f6fc` silhouette slot:

| Species | Tint | Contrast vs the slot |
|---|---|---|
| Hollow | `#7a6ac0` | **4.21 : 1** |
| Vetch | `#6ba7c0` | 2.47 : 1 |
| Ember | `#e5867a` | 2.44 : 1 |
| Loam | `#7cc492` | 1.93 : 1 |
| Skitter | `#e8b34a` | 1.78 : 1 |
| **Pale** | `#c6cede` | **1.47 : 1 — effectively invisible** |

Read off the capture, the whole Pale creature moves no channel by more than 50 of
255. **This is not a defect in the proxies; it is a prediction about the real
art.** A flat one-colour silhouette has no internal value structure — Phase 9's
creatures will have outline, shading and a darker core, and Pale specifically
**will need its own value structure, an outline, or a darker slot behind it.**
Hue alone will not carry it on a near-white card.

**It is the direct cost of an improvement, and both calls were right.** §1.2's
move of Pale from `#a9b0c4` to `#c6cede` took the palette's worst colour-blind
pair from ΔE 7.2 to 17.3, and took Pale's contrast against the card from about
1.9:1 to 1.47:1 — lightening a colour on a near-white surface is the same
operation as hiding it. **Both decisions were correct against the measurements
available at the time**; contrast-against-a-card was not measurable when Pale
moved, because no species colour had been applied to a surface yet.
`implementation/results/species-collision.md` has the measurement.

## 10.5 Raiders

**Angular, dark, and never cute.** Threat must never read as collectible. Dark bodies, cool highlights, hot accent colour for the dangerous part.

Each of the eight must be recognisable at a glance, because recognition is the counter system's teaching surface. A player who cannot tell a Courser from a Breaker cannot learn which trait answers which — and the counter model depends entirely on that association forming.

**The answering trait should be visually implied where possible.** Drift flies, so Reach reads as something that reaches upward. Delver burrows, so Burrow reads as ground-level. Where shape can carry the association, the player learns it without a tutorial.

## 10.6 Interface

**Tabular figures.** The interface is dense with odds, timers, yields, countdowns and Hold accrual. Numbers must not jitter as they tick, and 1/l/I and 0/O must be unambiguous — misread stats erode trust. Verify the display face's numerals; if they fail, keep it for titles and CTAs and pair a tabular face for numerals only.

**Minimum 11pt for any number a decision depends on.**

Beyond that, the design tokens are authoritative: colour, spacing, radius, elevation, the physical button-press state on primary CTAs, and the named animation set. **Amended 2026-09-23:** the token authority is `client/Assets/UI/Shell/Tokens.uss`, seeded from the Phase 10 palette in §10.1 rather than the 2026-09 handoff README; `implementation/scripts/verify-uss-tokens.sh` is what makes that authority real. Spacing, radius, elevation and the press state carry over from the handoff unchanged.

## 10.7 What not to draw

**Damage is posture, not injury.** Wounded creatures show stress through stance, breathing and desaturation — never blood, gore or visible trauma. The rating and the audience require it.

**Appeal over horror.** These are creatures a player names and keeps in a family tree for two years. Strange and unsettling is fine. Repulsive is not.

**The adjacency checklist.** Games Workshop's Genestealers occupy neighbouring thematic ground — genetic hybrids, broods, cults of descent — and the IP is enforced aggressively. The name is clear; art is where an unnecessary association could form. Avoid chitinous carapace as a dominant surface, the purple-and-bone palette, elongated backswept skulls, six-limbed insectoid builds with scything upper limbs, and ribbed bio-ship architecture.

Lean toward **mammalian and reptilian mass** — fur, hide, keratin, muscle under skin. Warm-blooded rather than carapaced. This also serves the appeal requirement, so the constraint costs nothing.

## 10.8 Environments

Roughly thirty regions, each with visual identity but no tactical geometry variety — lane count is 1–3 and there are no terrain modifiers, so regions differ in look and lane arrangement rather than in bespoke layout.

That makes the line item substantially smaller than it would have been under a terrain-modifier model. Regions need to be memorable and distinguishable on the map, not individually engineered.

## 10.9 The mascot

**Cinderplate** — a G2 Vetch × Ember hybrid. Vetch's dome and legs unchanged, with Ember's crest row bolted on. Reads at 40px as a dome plus three spikes, and no other creature owns that pair.

It works as the mascot because it is the first hybrid every player makes in the tutorial. The store icon is an animal they already own.

---

*The bible is complete. Remaining project work: rebuild the screen inventory against these ten sections, deprecate the nine original specs, and tune the economy at soft launch.*
