---
status: superseded
folder: 99-archive
superseded-by: broodline_combat_numbers.md
note: >
  Ten chassis under the abandoned damage triangle. The design is six
  species.
---

# Broodline — Chassis Roster

*Design spec, the ten base bodies*

---

## 1. Why This Document Exists

Five specs lean on chassis. Genetics makes it the one player-controlled input in a splice. Combat calls it "the floor, traits are the delta" and gives an Instinct that rewards same-chassis adjacency. Art direction requires all ten distinguishable as flat black shapes at 40px. The Codex asks whether traits are chassis-restricted.

None of them says what the ten chassis are.

A chassis is **a stat profile, a footprint, and a silhouette.** It is not a trait, it never blends, and it is the only part of a creature the player fully controls.

---

## 2. The Five Roles

| Role | Job on the board | Reads as |
|---|---|---|
| **Bulwark** | Absorb and block. Placed at the lane mouth. | Low, wide, heavy |
| **Striker** | Single-target damage. Placed with firing angles. | Tall, forward-leaning |
| **Skirmisher** | High rate of fire against numbers. | Lean, low, quick |
| **Support** | Amplifies Field traits. Placed for coverage. | Rounded, static, sessile |
| **Swarm** | Multiple bodies in one deployment slot. | Small, repeated |

Two chassis per role. Two is the minimum that makes role a *category* rather than a synonym for a specific creature, and ten total is what the art direction's 40px silhouette test can realistically carry.

---

## 3. The Roster

Multipliers against a baseline of 1.0. Chassis sets the floor; Frame, Armament, and Field modify it.

| Chassis | Role | Footprint | HP | Damage | Range | Atk speed |
|---|---|---|---|---|---|---|
| **Tor** | Bulwark | 2 tiles | 2.2× | 0.5× | 0.7× | 0.8× |
| **Ridgeback** | Bulwark | 1 tile | 1.6× | 0.7× | 0.8× | 0.9× |
| **Pike** | Striker | 1 tile | 0.7× | 1.8× | 1.2× | 0.7× |
| **Kestrel** | Striker | 1 tile | 0.6× | 1.4× | 1.5× | 1.0× |
| **Cur** | Skirmisher | 1 tile | 0.8× | 0.6× | 0.8× | 1.8× |
| **Harrier** | Skirmisher | 1 tile | 0.7× | 0.8× | 1.0× | 1.4× |
| **Cradle** | Support | 1 tile | 1.0× | 0.3× | 0.6× | 0.8× |
| **Bellow** | Support | 1 tile | 0.9× | 0.4× | 1.3× | 0.8× |
| **Whelp** | Swarm | 2 tiles, 2 bodies | 0.5× each | 0.5× each | 0.9× | 1.2× |
| **Skein** | Swarm | 3 tiles, 3 bodies | 0.3× each | 0.35× each | 0.7× | 1.3× |

**Support specifics.** Cradle grants **+50% Field radius and effect**; Bellow grants **+25%** but keeps a usable range and contributes real damage. Cradle is the dedicated aura platform, Bellow is the compromise that doesn't waste a deployment slot on a board with few emplacements.

**Names.** All ten sit in the natural-history register the art direction commits to — a landform, a dog, a fish, two birds, a flock. Deliberately no military vocabulary, and deliberately nothing in the brood-cult or hive register that the anti-Tyranid checklist rules out.

---

## 4. Footprint Belongs to Chassis — A Correction

The combat spec assigns footprint to the **Frame** trait: "Some Frames occupy two adjacent emplacement tiles." The Trait Codex inherited this with the Heavy Frame entry.

**Recommendation: footprint is a chassis property. Frame traits never change it.**

Board space is the most spatially legible thing about a creature, and it should follow from identity rather than from a swappable trait. If Frame controls footprint, the same Tor occupies one or two tiles depending on a trait the player may reroll next week — placement intuition breaks, and the modular art rig has to support a chassis at two scales, which the art direction's budget does not cover.

**Consequence:** the Codex's Heavy Frame entry needs revising. Recommend it becomes +80% HP with no footprint change, keeping it a strong Rare Frame without duplicating what Tor already is.

---

## 5. Armor Is Not a Chassis Property

Chassis sets HP, damage, range, attack speed, and footprint. It does **not** set armor type.

Armor comes entirely from the Frame trait — Plated Hide, Fused Hide, Warded Pelt. This is a deliberate separation and it does real work: it means **any chassis can fill any position in the damage triangle.** A Tor with a Warded Pelt and a Tor with Plated Hide are the same body answering different threats.

If chassis carried armor type, the ten chassis would collapse into a lookup table against the three enemy damage types, and the roster decision the combat spec wants — keeping variety because the triangle demands it — would resolve into "own three tanks."

---

## 6. Swarm Chassis

Whelp and Skein are one creature with multiple bodies.

- **One deployment slot**, against the cap of five
- **Bodies occupy adjacent emplacement tiles** — two for Whelp, three for Skein. On a board with few emplacements this is a real cost.
- **All bodies share the creature's four traits.** One generation, one lineage entry, one Codex discovery.
- **Field auras project from one body only.** Without this rule a Skein triples any Field trait and Support chassis become pointless.
- Bodies fall individually. The creature enters regeneration when the last one falls.

Total output is roughly parity with a single-body chassis — a Skein's three bodies sum to 1.05× damage and 0.9× HP. What differs is the shape of it: spread across three targets, harder to focus down, and badly exposed to anything with area effect. That is a genuinely different tactical object rather than a stat variant, which is what justifies two of the ten slots.

---

## 7. Chassis in Splicing

Per the genetics spec, the child inherits **one parent's chassis, chosen by the player**. Chassis never blends, and there is no randomness in the choice.

This has a consequence the genetics spec doesn't state. **Chassis can only leave a player's roster, never enter it through splicing.** Every splice consumes two chassis and produces one. A player who splices exclusively toward Pike will, over months, hold nothing but Pikes — and then meets an enemy wave that punishes them for it.

So base stock supply has to keep all ten circulating:

| Source | Chassis behaviour |
|---|---|
| **Wave completion** | Uniform random across all ten |
| **Campaign milestones** | Guaranteed specific chassis, per genetics §7 — this is the anti-lockout path |
| **Node harvesting** | Weighted by region, so relocation shifts what you can acquire |
| **Splice Roulette** | Fragments only, no chassis |

Region-weighted chassis in node harvests is the strongest of these and it's nearly free. It gives the map another axis — "I need Cradles and this region drops them" — and it pushes back on the tendency to sit on one good node forever.

**Note a healthy tension:** Pack Sense grants +15% damage to adjacent same-chassis allies, rewarding clustering, while the damage triangle rewards diversity. Those pull against each other and both are correct. A player running four Curs and a Cradle is making a real bet.

---

## 8. Silhouette Requirements

The art direction requires all ten distinguishable as flat black shapes at 40px. That is a hard test and it constrains design more than any stat line here.

Mass distribution must differ, not just detail:

- **Tor** — low, wide, ground-hugging. Widest shape in the game.
- **Ridgeback** — compact, dense, dorsal ridge as the read
- **Pike** — long horizontal body, forward mass
- **Kestrel** — tall, narrow, elevated head
- **Cur** — low and lean, four-legged, quick stance
- **Harrier** — mid-height, wing or fin structure breaking the outline
- **Cradle** — rounded, sessile, no clear front
- **Bellow** — vertical with a flared upper structure
- **Whelp** — two small shapes, paired
- **Skein** — three small shapes, distributed

The two Swarm chassis pass the test almost for free, since body count is legible at any size. The two Bulwarks and two Strikers are where confusion is likeliest — Tor and Ridgeback in particular need obvious proportional difference, not just scale.

**Chassis colour identity** — the art direction's open question 3 — should resolve toward **no canonical chassis colour**. Trait-driven colour makes each creature individual, which matters more in a game where players name creatures and keep them in family trees. Silhouette is already carrying recognition; colour doesn't need to duplicate it.

---

## 9. The Sidegrade Envelope

The genetics spec commits to seasonal chassis being sidegrades: "a Gen-1 launch chassis should still be viable in year two." That promise needs a rule, or it will be broken by the third season through ordinary power creep.

**Rule:** every chassis sums to the same power budget under a fixed stat weighting. Two-tile chassis receive a **+40% budget** as compensation for board space. Multi-body chassis are budgeted on their combined totals.

A new seasonal chassis must fit an existing role's envelope, not exceed it. It may redistribute freely inside the budget — that's how it becomes interesting — but the total is fixed. Publish the weighting internally and check every new chassis against it before art begins, because a chassis that fails the envelope after modelling will ship anyway.

---

## 10. Guardrails

- Chassis never blends; the child takes one parent's, player-chosen
- Chassis sets HP, damage, range, attack speed, footprint — never armor type, never trait access
- Every chassis reachable free through campaign milestones
- All ten distinguishable as 40px silhouettes before texturing begins
- Animation is authored per chassis, never per trait, per the art direction's budget constraint
- Seasonal chassis fit the existing envelope; no chassis is ever strictly better than a launch chassis
- No chassis is ever sold. Money buys base stock volume, never a specific body.

---

## 11. Open Questions

1. **Are traits chassis-restricted?** The Codex asks this. Leaning no — universal traits keep the probability table simple and the combinatorics honest. But a Longshot Glands on a Tor is faintly absurd, and some restriction might improve legibility.
2. **Is two Bulwarks the right split?** Tor and Ridgeback are close in function and the silhouette test is hardest here. A third Striker or Support may be worth more than the second Bulwark.
3. **Do Swarm bodies count individually for Pack Sense?** A Skein adjacent to itself triggering same-chassis bonuses three times over is almost certainly wrong, but the rule needs stating.
4. **Region-weighted chassis drops need a table.** Thirty regions and ten chassis is a distribution nobody has authored, and it interacts with the node spec's weekly rotation.
5. **Does footprint interact with terrain modifiers?** A two-tile Tor spanning an elevation tile and a flat tile — does it get the range bonus? Needs a rule before the first multi-tile board is authored.

---

*Remaining undocumented: the enemy archetype roster, mail and notification centre, player profile, and settings.*
