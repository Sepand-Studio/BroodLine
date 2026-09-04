# Broodline — Enemy Archetypes

*Design spec, what attacks the Ark*

---

## 1. Why This Document Exists

The combat spec's fifth open question asks for enemy archetype count to be "scoped against budget early," and names it the biggest art and animation cost in the spec set. That scoping hasn't happened.

There's a design reason as well as a production one. The damage triangle is the mechanism that keeps a player holding more than five creatures, and it only works if the things being attacked are legible and varied. **Until enemies are defined, the triangle is an abstraction and the roster has nothing to be diverse against.**

---

## 2. What They Are

Enemies are **unbound splices** — genetic recombination that happened in the Fracture without a Warden directing it.

This costs nothing and buys a great deal. The player and the enemy are running the same process; one is controlled and one isn't. Every wave is an argument for what the Wardens do. It also makes visual kinship between player creatures and enemies a feature rather than a budget compromise, which matters directly in §3.

The one thing it must never become is a morality beat. Nothing here should suggest the player is fighting creatures that could have been theirs — that reads as cruelty in a game where players name and keep things, and the art direction's appeal-over-horror rule points the same way.

---

## 3. The Budget Answer

Twelve archetypes does not mean twelve models.

> **6 base bodies × 2 archetype variants = 12 archetypes.**

Each variant is a recolour, one geometry swap, and a different armor and behaviour package. **Animation is authored per body, not per archetype** — six animation sets, matching the rule the art direction already applies to player chassis.

| Line item | Count |
|---|---|
| Enemy body models | 6 |
| Animation sets | 6 |
| Geometry swap pieces | 6 |
| Archetypes shipped | 12 |

That halves the cost the combat spec was worried about, and it improves legibility rather than degrading it. A player who has learned the Crawler body reads its second variant instantly and only has to learn what changed.

**One exception is worth the money:** the tier-10 elite in §4 should be a distinct silhouette, not a variant. The campaign's final recurring threat carrying a reskin undercuts the moment.

---

## 4. The Roster

| Body | Archetype | Tier | Armor | Behaviour |
|---|---|---|---|---|
| **Crawler** | Skitter | 1 | Sealed | Fast, fragile, arrives in numbers |
| | Rill | 3 | Sealed | Faster; splits into two Skitters on death |
| **Hulk** | Bole | 1 | Plated | Slow, high HP, no special behaviour |
| | Cairn | 4 | Plated | Very high HP; damages emplacements it passes |
| **Spitter** | Gall | 2 | Warded | Attacks creatures from 3 tiles while advancing |
| | Wither | 5 | Warded | Longer range; applies armor shred to creatures |
| **Herald** | Mantle | 3 | Warded | Grants +30% armor to enemies within 2 tiles |
| | Nurse | 6 | Warded | Heals nearby enemies 2% max HP/sec |
| **Shade** | Flit | 4 | Sealed | Switches lanes once at mid-route |
| | Umbra | 7 | Sealed | Cycles 2s damage immunity |
| **Sire** | Sire | 8 | Plated | Elite. Spawns Skitters on a timer |
| | Progenitor | 10 | **Shifts** | Elite. Changes armor type at 66% and 33% HP |

Names sit in the same natural-history register as the chassis roster, and stay clear of the hive and brood-cult vocabulary the anti-Tyranid checklist rules out.

**Progenitor is the design keystone.** An enemy that changes armor type mid-fight is the only thing in the game that makes a *mixed-damage roster* mandatory rather than merely advisable. Everything else can be answered by bringing the right five creatures; Progenitor cannot. It should be the campaign's recurring wall and the anchor of high-richness region defense.

---

## 5. Why the Armor Mapping Is Legible

The armor assignments above aren't arbitrary. They give each damage type a clear job:

| Damage | Beats | Which means it handles |
|---|---|---|
| **Kinetic** | Sealed | Speed — Skitter, Rill, Flit, Umbra |
| **Corrosive** | Plated | Mass — Bole, Cairn, Sire |
| **Neural** | Warded | Support and ranged — Gall, Wither, Mantle, Nurse |

A player can hold that in their head after three waves without reading a table: *Kinetic for the fast ones, Corrosive for the big ones, Neural for the ones that help the others.* The combat spec asks that a bad matchup be a player mistake rather than a surprise, and a mapping this readable is what makes that fair.

The Warded group is deliberately the largest. Neural is otherwise the weakest-feeling damage type — support enemies are less viscerally satisfying to kill — and giving it four targets keeps it from becoming the slot players skip.

---

## 6. Introduction Schedule

The combat spec requires one new archetype at a time. Mapping to campaign progression:

| Introduced | Archetype | Teaches |
|---|---|---|
| Wave 1 | Skitter | Enemies path; creatures fire autonomously |
| Wave 2 | Bole | Armor types exist — this is the FTUE's beat 5 |
| Wave 5 | Gall | Enemies can attack back; placement has risk |
| Wave 9 | Rill | Death can create more enemies |
| Wave 14 | Mantle | Kill priority matters |
| Wave 20 | Cairn | Emplacements are damageable |
| Wave 26 | Flit | Lane assignment isn't permanent |
| Wave 33 | Wither | Your armor can be stripped |
| Wave 40 | Nurse | Damage races are losable |
| Wave 48 | Umbra | Sustained damage beats burst |
| Wave 55 | Sire | Elites; add-management |
| Wave 65 | Progenitor | Mixed damage is mandatory |

Bole at wave 2 is doing the FTUE's work — the spec's second beat calls for "one new enemy archetype with a visible armour type," and Bole is slow and obviously heavy, which teaches armor without a tooltip.

---

## 7. Region Defense Composition

Region defense waves scale with richness, per the combat spec. That's the pressure on "is this spot worth holding," and archetype composition is the honest way to express it.

| Region | Composition |
|---|---|
| **Common Vein only** | Tiers 1–3. Skitter, Bole, Gall, Rill, Mantle. |
| **Rich Deposit** | Tiers 1–6, adds Cairn, Wither, Nurse |
| **Apex Vein active** | Full pool including Sire; **Progenitor at Apex Vein regions specifically** |

Progenitor appearing only where an Apex Vein is active gives the map's most contested moment a distinct threat, and it means an alliance fighting over an Apex Vein is fighting the map as well as each other.

Region **terrain** determines lane count and emplacements per the combat spec; region **richness** determines who walks down them. Those are separate dials and they should stay separate — a poor region with four lanes should be tactically interesting and mechanically survivable.

---

## 8. Telegraphing

The combat spec promises armor mix is visible before deployment. Concretely, the pre-wave screen shows:

- **Armor composition** as proportions — "60% Sealed, 40% Plated" — not a creature-by-creature list
- **Archetype icons present**, with counts
- **Any behaviour flags** the player has already encountered: splits, heals, shields, ranged, shifts
- **Lane count and emplacement layout** for the current region

Undiscovered archetypes appear as an unknown marker rather than being hidden entirely. A player should know something new is coming without knowing what — surprise, not ambush. First encounters are the campaign's teaching moments and they need to land as events.

---

## 9. Raid Combat

Enemy archetypes **do not appear in raids.** Per the raiding spec, the attacker's three-creature party is the incoming wave; a raid is player creatures against player creatures.

Worth stating explicitly because it halves the balance surface. Archetypes only ever need tuning against PvE, and the 55% raid win-rate target is a separate problem with separate levers.

---

## 10. Guardrails

- Twelve archetypes from six bodies; animation authored per body
- Every archetype's armor type is visible before deployment
- No archetype has a hard counter — the triangle is ×1.5/×0.67, never immunity
- No enemy damages the Ark's stored resources, roster, or Vault; reaching the Ark loses the wave and nothing more
- Enemy design follows the anti-Tyranid checklist and the appeal-over-horror rule
- Seasonal archetypes reuse existing bodies unless they're elites
- Enemies never appear in PvP

---

## 11. Open Questions

1. **Is 65 waves the right campaign length at launch?** The introduction schedule assumes it. It also gates Gene Vault Core tiers via campaign milestones, so campaign length is load-bearing on the economy — a shorter campaign means the payer ceiling arrives sooner than the nine months the economy model targets.
2. **Should Progenitor's armor shifts be telegraphed mid-fight?** Visible shifts make it a legible puzzle; hidden ones make it a memory test. Leaning visible, with a clear colour change.
3. **Do enemies have Instinct-equivalent variety within an archetype?** Currently no — a Skitter always behaves like a Skitter. Adding variance would deepen the sim and multiply the balance surface.
4. **Six bodies may be too few by year two.** Seasonal content needs somewhere to go, and variant three and four on the same body will start to feel thin.
5. **How do multi-tile Swarm chassis interact with splitters?** A Rill splitting adjacent to a Skein produces a crowded board state nobody has looked at.

---

*Remaining undocumented: the region roster, mail and notification centre, player profile, and settings.*
