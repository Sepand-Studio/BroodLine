---
status: superseded
folder: 99-archive
superseded-by: broodline_combat_engine.md
note: >
  Era-2. Names Pack Sense as same-chassis; the design is same-species.
---

# Broodline — Combat System
*Design spec, tower defense core*

---

## 1. Core Format

Your hybrids are the towers. Enemies path along lanes toward the Gene Ark. If they reach it, you lose the wave.

The player's input is **placement and composition**, not moment-to-moment control. Once a wave starts, creatures act autonomously according to their Instinct trait. This is deliberate and load-bearing: it makes combat playable one-handed in 90 seconds, it lets raid defenses resolve while the player is asleep, and it means the genetics system — not reflexes — is what determines whether you win. A player who spliced well should beat a player who taps faster.

---

## 2. The Battlefield

Lane layout is **determined by the region your Ark currently occupies.**

This is the most valuable connection in the whole design and it's nearly free. Relocating the Ark doesn't just change your yield, it changes your defensive terrain — a region with three approach lanes demands a different roster than one with a single chokepoint. Suddenly "is this node worth moving for" has a second axis, and players have a reason to keep a varied roster rather than five copies of the optimal creature.

Each region defines:
- **Lane count** (1–4) and their convergence pattern
- **Emplacement tiles** — where creatures can be placed, and how many exist
- **Terrain modifiers** — elevation tiles granting range, water tiles slowing enemies, dead ground where nothing can be placed

Regions should be hand-authored, not generated. There are maybe 30 of them and their tactical identity is worth the effort.

---

## 3. Deployment

- **Five creatures maximum** per defense, from a roster that may hold dozens
- Placement happens in a pre-wave phase with no timer — this is the thinking moment
- Creatures can be repositioned between waves, not during
- One **Rally** action per wave: a single tap that grants a chosen creature a short burst (roughly 4 seconds of doubled attack speed), on a 30-second cooldown

Rally exists so an engaged player has something to do without making an idle player lose. It should be worth roughly 10% of a wave's outcome — enough to feel like agency, not enough that being away from your phone costs you the fight.

---

## 4. Damage & Armor

Three damage types, three armor types, standard triangle:

| Damage | Strong against | Weak against |
|---|---|---|
| **Kinetic** | Sealed | Plated |
| **Corrosive** | Plated | Warded |
| **Neural** | Warded | Sealed |

Multipliers of ×1.5 and ×0.67 — noticeable enough to force composition decisions, gentle enough that a bad matchup isn't an auto-loss. Enemy waves telegraph their armor mix in the pre-wave screen, so bringing the wrong roster is a player mistake rather than a surprise.

This triangle is the main reason a player keeps more than five creatures. Without it, the roster collapses to whichever five are strongest and the entire genetics system loses its point.

---

## 5. Trait → Stat Mapping

Each of the four slots feeds combat directly:

**Frame** — HP, armor type, and footprint. Some Frames occupy two adjacent emplacement tiles, trading board space for durability. Footprint is an underused lever and it makes terrain matter more.

**Armament** — damage value, damage type, range, and attack interval. This slot alone determines whether a creature is a sniper, a shotgunner, or a slow heavy hitter.

**Field** — a persistent aura with a tile radius. Slows, damage amplification, ally regeneration, armor shred. Field creatures are placed for coverage rather than firing angle, which is a genuinely different placement puzzle.

**Instinct** — behavior. See below.

Chassis sets the baseline these modify: a Bulwark chassis with a glass-cannon Armament is still tankier than a Skirmisher with the same trait. Chassis is the floor, traits are the delta.

---

## 6. Instinct — Behavior Trees

The distinctive system, and the one that needs the most authoring work.

Every Instinct trait is a small behavior tree with three components: a **target rule**, a **threshold trigger**, and a **response**. The player never edits these — they acquire them through splicing, which is what makes Instinct traits worth chasing.

| Instinct | Target rule | Trigger | Response |
|---|---|---|---|
| **Bloodscent** | Lowest current HP in range | — | — |
| **Vanguard** | Closest to the Ark | — | — |
| **Patient** | Highest max HP | Fewer than 3 enemies in range | Holds fire, accumulates a damage bonus |
| **Last Stand** | Nearest | Self below 25% HP | +50% attack speed |
| **Skittish** | Nearest | Self below 40% HP | Repositions to an adjacent free tile |
| **Pack Sense** | Nearest | Adjacent to same-chassis ally | +15% damage to both |
| **Overwatch** | Furthest in range | — | +25% range, −20% attack speed |

Design rules for authoring more:

- **Every Instinct must be readable from watching one wave.** If a player can't infer what it does by watching, it may as well be a hidden stat.
- **No Instinct should be strictly best.** Bloodscent is excellent against swarms and poor against a single armored leader. Overwatch is superb on elevation and wasted at a chokepoint.
- **Triggers should fire visibly** — an animation state change, a colour shift. The moment Last Stand kicks in should be legible from across the room.

Target Instinct count at launch: **12–16**. Fewer than that and the splice pool feels thin; more and none of them get learned.

---

## 7. Wave Structure

**Campaign waves** — the progression spine. Hand-authored, escalating, introducing one new enemy archetype at a time. Completing a wave for the first time yields Gene Shards, base-stock creatures, and campaign milestones.

**Replay** — completed waves are re-runnable for a reduced Gene Shard drip. This is the "always something to do at 0 charges" guarantee from the monetization spec, and it needs to genuinely work; a player with no charges must be able to open the game and make progress.

**Region defense** — periodic PvE waves against the current region's local threats. Scales with region richness, so an Apex Vein region is more dangerous to hold. Direct pressure on the "is this spot worth it" decision.

**Raid defense** — see below.

---

## 8. Raid Combat

Raids reuse this engine with the roles inverted.

- The **defender's** escort creatures are placed as towers along a short, simplified lane derived from the ambush terrain
- The **attacker's** three-creature raid party is the incoming wave
- Attacker creatures use their full trait loadout, including Instinct — an attacking Skittish creature will retreat from the fight, which is a real and interesting drawback

The asymmetry is intentional. Defenders benefit from terrain and emplacement; attackers benefit from choosing the moment. Neither side should win more than about 55% of the time at equal investment.

---

## 9. Auto-Resolve

Most raid defenses will resolve while the defender is offline. This must be a **full simulation of the same engine**, not a stat comparison rolled against a dice check.

Two reasons. First, if auto-resolve uses different math, players will discover it and correctly conclude that their placement and traits didn't matter. Second, a simulation produces a **replay** — and the replay is what makes losing tolerable. A defender who wakes to "you were raided, −40% cargo" is angry. A defender who can watch the fight, see that their Skittish escort retreated at the wrong moment, and fix it, is engaged.

Ship the replay viewer at launch. It's the difference between PvP that builds a community and PvP that bleeds one.

---

## 10. Loss & Regeneration

- Creatures are **never permanently lost in combat.** Ever. The only way a creature leaves the roster is voluntary splicing.
- A creature reduced to zero HP enters **regeneration** — unavailable for 20–60 minutes depending on how the wave went
- Regeneration timers are skippable with Gene Shards, which is a clean convenience sink that buys speed rather than power
- A failed campaign wave costs only the regeneration timers; retry immediately with a different roster

Permadeath in a game where creatures are built through a destructive splicing economy would be brutal, and it would make players hoard rather than splice — killing the loop the whole game rests on.

---

## 11. Guardrails

- Placement and composition decide outcomes; tapping speed contributes at the margin
- Auto-resolve uses the identical engine to live play
- No creature is ever lost involuntarily
- Enemy armor composition is always visible before deployment
- Rally is capped so an absent player isn't punished
- New chassis and traits in later seasons are sidegrades — a launch-era Bulwark must still be viable in year two

---

## 12. Open Questions

1. **Is five deployed creatures the right number?** Enough for composition decisions, few enough to read on a phone screen. Worth testing four and six.
2. **Should terrain be visible before relocating?** Showing it makes region choice tactical; hiding it makes relocation a gamble. Leaning visible — the node spec already promises a detailed region info panel.
3. **Rally on cooldown, or one per wave?** Cooldown rewards attention more, which cuts against the idle framing.
4. **How much does attacker Instinct matter in raids?** If attacking creatures behave too autonomously, raiding feels like it lacks agency. May need an attacker-side placement phase.
5. **Enemy archetype count at launch.** Probably 10–12, but this is the biggest art and animation cost in the spec and should be scoped against budget early.

---

*Next: alliance & territory control — the last system assumed by every other spec and defined by none.*
