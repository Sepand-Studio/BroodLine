# Broodline — Region Roster

*Design spec, the map itself*

> **CURRENT — companion to the design bible.** This document is live and should
> be built from. It owns detail that `broodline_bible.md` deliberately does not
> duplicate. Where the two conflict, the bible is current and this document
> needs an edit.
>
> Rewritten against 1–3 lanes and six species. Replaces the version built on
> four-lane terrain families, elevation and water modifiers, and ten chassis.

---

## 1. What this document owns

Five systems resolve against regions. Bible §5.7 now carries the band topology, the gates and the inverse richness rule; what it does not carry is the thirty regions themselves.

| Wanted by | For |
|---|---|
| `broodline_base_stock.md` §4.2 | The per-region species weighting, deferred here as a content pass |
| `broodline_campaign_structure.md` §9 | The lane counts region defence resolves against |
| `broodline_collectors_raiding.md` §5 | The regions routes cross and the terrain an interception generates |
| Bible §6 | Which regions can be claimed and which never can |
| Bible §5.3 | Where nodes of each tier can appear |

**All thirty are authored below.** The previous version supplied nine and left twenty-one as an open question, and that question was the largest content gap in the set.

---

## 2. Topology

**Thirty regions in three concentric bands**, per bible §5.7.

| Band | Regions | Character | Claimable |
|---|---|---|---|
| **Inner Reach** | 8 | Settled, Warden-held. Low richness, forgiving ground | **Never** |
| **Mid Reach** | 12 | The working map. Where most players live | Yes |
| **Outer Reach** | 10 | Fracture-adjacent. High richness, punishing ground | Yes |

Each region borders two to four others within its band. **Band crossings happen only through gates** — four between Inner and Mid, four between Mid and Outer.

Gates are the most useful structural decision on the map. They give raiders predictable chokepoints to watch, they make the Route Plotter's direct-versus-allied choice meaningful rather than cosmetic, and they mean a convoy hauling Apex cargo out of the Outer Reach has a genuinely dangerous journey rather than an abstractly risky one.

| Boundary | Inner / Mid side | Mid / Outer side |
|---|---|---|
| Inner ↔ Mid | Tellin · Millgate · Windfell · Chalkrise | Greyspan · Fenwatch · Highmarl · Stonewake |
| Mid ↔ Outer | Coldharrow · Dunmarsh · Netherfold · Cadewater | Deepscree · The Spill · Threnody · Sheerdown |

**Each server instance runs its own copy of the same thirty regions**, with independent node rotation, territory and weekly tick timing.

---

## 3. Terrain families

Eight, and **every one is defined by lane arrangement alone.** Bible §4.2 cuts elevation, water and emplacement variety, so a family is four numbers: how many lanes, how they are arranged, how many pockets, and how long the lane is.

| Family | Lanes | Arrangement | Pockets | Lane length | Difficulty |
|---|---|---|---|---|---|
| **Defile** | 1 | Single approach | 6 | 24 | Easiest in the game |
| **Basin** | 1 | Single approach, sparse ground | 4 | 24 | |
| **Fork** | 2 | Converge for the last 6 tiles | 5 | 24 | |
| **Shelf** | 2 | Staggered — one lane 6 tiles shorter | 4 | 24 / 18 | |
| **Span** | 2 | Fully parallel, never converge | 5 | 24 | |
| **Scree** | 3 | Converge for the last 6 tiles | 5 | 24 | |
| **Weir** | 3 | Parallel but short | 6 | 18 | |
| **Delta** | 3 | Fully parallel | 4 | 24 | Hardest defence |

**The dial is how many counters a player must hold at once.** One lane means one raider stream and a defence can be built around two or three answers. Three parallel lanes means three streams arriving simultaneously, and per bible §4.5 they will not share a counter — a legal wave never sends two raiders answered by the same trait.

**Convergence is what separates Scree from Delta.** Three lanes that merge for the last six tiles let a Hollow at range 7 cover the merge point and contribute to all three; three parallel lanes do not. That is the whole difference and it is worth more than any terrain modifier the previous version proposed.

**Pocket count is the second dial and it runs against lane count on purpose.** Delta gives three lanes and four pockets; Defile gives one lane and six. A player on Delta cannot cover everything and must decide what to give up.

**Weir is the odd one.** Three lanes, generous pockets, but an 18-tile lane means a Courser crosses in eleven seconds rather than fifteen and a Breaker in fifty rather than sixty-eight. Placement is easy and time is short. It punishes slow-clearing compositions rather than thin ones, which makes it the only family whose difficulty is about damage rate rather than counter breadth.

---

## 4. The inverse rule

> **Richness and defensibility run opposite. The best nodes sit on the worst ground.**

The single most important line in this document, carried forward intact.

Without it the map has a correct answer — a region that is both rich and defensible is simply the best place to be, everyone converges on it, and relocation stops being a decision. With it every move is a trade: an Apex Vein on a three-lane Delta means better yield and a harder region defence, and holding it demands a broader roster than the one that worked at home.

It is also what makes bible §4.2's terrain-follows-region connection pay off. Relocating changing the battlefield is not flavour; it is the cost accepted for the yield wanted.

| Band | Terrain skew | Common Veins | Rich Deposits | Apex |
|---|---|---|---|---|
| **Inner** | Defile, Basin, Fork | 2–3 | 0–1 | No |
| **Mid** | Fork, Shelf, Span, Basin | 2 | 1–2 | Rare |
| **Outer** | Scree, Weir, Delta, Span | 1–2 | 2–3 | Yes |

**Common Vein count falls as richness rises.** The safe fallback is thinnest exactly where the prizes are largest — another expression of the same rule, and it means the Outer Reach punishes a player who parks there and stops paying attention.

**The Inner Reach is permanently unclaimable.** Eight regions no alliance can ever control, open to everyone forever. Bible §6.8 guarantees Common Veins stay neutral; this goes further and guarantees a whole band. A new player, a solo player, or a player whose alliance just collapsed always has somewhere to stand that nobody can contest.

---

## 5. The thirty regions

Species columns are the two the region weights, tripling their share of node-sourced base stock per `broodline_base_stock.md` §4.2.

### Inner Reach — 8 regions, unclaimable

| Region | Family | Lanes | Pockets | Common | Rich | Species weighted |
|---|---|---|---|---|---|---|
| **Holdfast** | Defile | 1 | 6 | 3 | 0 | Vetch · Loam |
| **Ashfold** | Defile | 1 | 6 | 3 | 0 | Vetch · Ember |
| **Chalkrise** | Defile | 1 | 6 | 3 | 0 | Ember · Loam |
| **Quillmoor** | Basin | 1 | 4 | 2 | 1 | Hollow · Pale |
| **Barrowlight** | Basin | 1 | 4 | 3 | 0 | Loam · Skitter |
| **Windfell** | Basin | 1 | 4 | 2 | 1 | Hollow · Skitter |
| **Tellin** | Fork | 2 | 5 | 2 | 1 | Skitter · Ember |
| **Millgate** | Fork | 2 | 5 | 2 | 1 | Pale · Vetch |

**Holdfast is the starting region.** One lane, six pockets, three Common Veins, no competition, and nothing worth taking. It is the most defensible ground in the game and the poorest, which is exactly the lesson the map needs to teach first.

### Mid Reach — 12 regions

| Region | Family | Lanes | Pockets | Common | Rich | Apex | Species weighted |
|---|---|---|---|---|---|---|---|
| **Highmarl** | Basin | 1 | 4 | 2 | 1 | — | Hollow · Pale |
| **Stonewake** | Basin | 1 | 4 | 2 | 1 | — | Vetch · Ember |
| **Fenwatch** | Fork | 2 | 5 | 2 | 2 | Rare | Skitter · Vetch |
| **Sablewick** | Fork | 2 | 5 | 2 | 2 | — | Vetch · Ember |
| **Netherfold** | Fork | 2 | 5 | 2 | 2 | — | Pale · Hollow |
| **The Narrows** | Shelf | 2 | 4 | 2 | 1 | — | Pale · Loam |
| **Thornwyke** | Shelf | 2 | 4 | 2 | 1 | — | Loam · Pale |
| **Rookspire** | Shelf | 2 | 4 | 2 | 1 | — | Ember · Vetch |
| **Greyspan** | Span | 2 | 5 | 2 | 2 | Rare | Hollow · Skitter |
| **Coldharrow** | Span | 2 | 5 | 2 | 2 | — | Ember · Hollow |
| **Dunmarsh** | Span | 2 | 5 | 2 | 2 | Rare | Skitter · Loam |
| **Cadewater** | Span | 2 | 5 | 2 | 2 | Rare | Loam · Skitter |

### Outer Reach — 10 regions

| Region | Family | Lanes | Pockets | Common | Rich | Apex | Species weighted |
|---|---|---|---|---|---|---|---|
| **Threnody** | Span | 2 | 5 | 2 | 2 | Yes | Pale · Vetch |
| **Sheerdown** | Span | 2 | 5 | 2 | 2 | Yes | Loam · Vetch |
| **Deepscree** | Scree | 3 | 5 | 1 | 3 | Yes | Vetch · Hollow |
| **Blacksump** | Scree | 3 | 5 | 1 | 3 | Yes | Hollow · Loam |
| **Saltwrack** | Scree | 3 | 5 | 1 | 3 | Yes | Hollow · Ember |
| **The Spill** | Weir | 3 | 6 | 2 | 2 | Yes | Loam · Pale |
| **Kettlemoor** | Weir | 3 | 6 | 2 | 2 | Yes | Vetch · Skitter |
| **Rimfall** | Delta | 3 | 4 | 1 | 3 | Yes | Skitter · Ember |
| **Weltering** | Delta | 3 | 4 | 1 | 3 | Yes | Ember · Skitter |
| **The Gyre** | Delta | 3 | 4 | 1 | 3 | Yes | Pale · Loam |

**Names avoid three registers deliberately.** Nothing in the brood-cult or hive vocabulary the anti-Tyranid checklist at bible §10.7 rules out. Nothing that collides with a species, trait or raider name — a region called Emberfall or Farhollow would be unreadable in a sentence about species weighting. And nothing suggesting viscera, per the same section's appeal-over-horror rule.

### 5.1 Species distribution check

`broodline_base_stock.md` §4.2 requires every species weighted in at least three regions with at least one in each band.

| Species | Inner | Mid | Outer | Total |
|---|---|---|---|---|
| Vetch | 3 | 4 | 4 | **11** |
| Ember | 3 | 4 | 3 | **10** |
| Skitter | 3 | 4 | 3 | **10** |
| Hollow | 2 | 4 | 3 | **9** |
| Loam | 3 | 4 | 4 | **11** |
| Pale | 2 | 4 | 3 | **9** |
| | 16 | 24 | 20 | **60** |

Sixty slots across six species, range 9 to 11. **Every species appears in every band.** ✓

**Hollow and Pale are the two at nine, and both sit at two in the Inner Reach.** That is the right pair to be thinnest early: Hollow is Founder 1 so every player already holds one, and Pale arrives guaranteed at wave 6. Neither can be missed, so scarcity in the safe band creates a reason to move rather than a risk of lockout.

---

## 6. Region defence

Bible §4.8 lists region defence as a wave type and gives it no rules. It is the rent, against the campaign's ladder.

**Cadence.** A region defence wave fires roughly every **eight hours of active harvesting**, more often in richer regions:

| Richest node present | Interval |
|---|---|
| Common Vein only | 12 hours |
| Rich Deposit | 8 hours |
| Apex Vein active | 5 hours |

**Composition scales with richness.** This is the direct mechanical pressure on *is this spot worth it*:

| Richest node present | Raiders |
|---|---|
| **Common Vein only** | Skirmisher · Lash · Brood |
| **Rich Deposit** | adds Courser · Drift · Delver |
| **Apex Vein active** | Full pool, including Bulwark and Breaker |

**Terrain is the region's own**, per bible §4.2 — its family, lane count and pocket count exactly as authored above. This is the half of combat where relocating changes the battlefield.

**Integrity is 10 + 2 per Core tier**, per `broodline_combat_numbers.md` §2. This is Core's only mechanical job outside capping other facilities, and it applies here and nowhere else.

**Wave budget follows the formula without exception** — 60 × 1.04^(w−1) × L, where *w* is the region defence encounter count for that region and *L* its lane count. Campaign waves may deviate where an authored beat requires it; these may not.

**Losing costs regeneration timers and a two-hour harvest interruption.** Nothing else. No node loss, no cargo loss, no Hold loss. It is a setback, never a spiral.

**Region richness and region terrain are separate dials and must stay separate.** A poor region with three lanes should be tactically interesting and mechanically survivable; a rich region with one lane should not exist, per §4, but if authoring ever produces one it is the richness that moves.

---

## 7. Raid interception terrain

`broodline_collectors_raiding.md` §7 derives ambush terrain from the segment where interception occurred, and the previous version left open whether that matches the region's authored layout.

**It uses the region's lane count and nothing else.** A short **12-tile lane with 3 pockets**, at the region's lane count.

An ambush is not a defended position. The defender's escorts were moving, not emplaced, so they get less ground and less time than a region defence would give them — which is what makes escorting a real cost rather than a second home defence. The lane count carries across because it is the only thing about a region that should change how many answers a player needs at once.

---

## 8. Region Detail

What the screen needs, against this schema.

- **Current richness and node inventory**, live
- **Controller and alliance banner**
- **Terrain**: family name, lane count, pocket count, lane length
- **Species weighting** — the two species this region triples
- **Travel time** from the current Ark position, and separately from each dispatched Collector
- **Region defence tier**, derived from the richest node present
- Active alerts

**Terrain is always visible before committing to a move.** Hiding it makes relocation a gamble, and bible §5.2 puts an Inner-to-Outer crossing at ninety-five minutes — too expensive to gamble on. Showing it is what turns §4's inverse rule from a hidden trap into a legible trade.

**Species weighting belongs on this screen as much as richness does.** Bible §5.7 already puts it on the region tile. *"I need a Loam line and Sheerdown triples them"* is a relocation reason with nothing to do with yield, and it is the map's second axis.

---

## 9. Guardrails

- Inner Reach is permanently unclaimable — eight regions, forever
- Common Veins exist in every region without exception and are never claimable anywhere
- The non-ally harvest penalty never applies to Common Veins, in any region, under any controller
- Richness and defensibility run opposite; no region is best at both
- Every species is weighted in at least three regions, with at least one in each band
- Terrain is always visible before committing to a move
- No region is ever locked behind purchase, tier, or alliance membership
- Gates are never closable by any player action
- Region defence composition scales with richness; terrain scales with the region. The two dials stay separate

---

## 10. Open questions

1. **Are four gates per band boundary enough?** Fewer makes raiding sharper and route choice more meaningful; it also funnels every Outer convoy through predictable ground, which may cross from tense into miserable.
2. **Full adjacency has not been authored.** Each region borders two to four others, and which ones is a level-design artifact rather than a design decision — but the Route Plotter cannot be built without the graph, and the gate regions above constrain it.
3. **Should the Inner Reach ever open to claiming** in a late-game season? It would give veteran alliances somewhere new to fight and it would break the guarantee new players depend on. Leaning firmly no.
4. **Weir may be the wrong shape.** It is the only family whose difficulty is about damage rate rather than counter breadth, which makes it interesting and also makes it the one that could invalidate the design's central claim that composition beats numbers. Worth testing before the second Weir region is authored.
5. **Thirty regions across how many concurrent servers?** Affects whether the Outer Reach feels contested or empty. A live-ops decision more than a design one.
6. **Does the weekly Rich Deposit rotation move deposits between regions, or only reroll which slots are active?** Bible §5.4 says a portion shifts to new regions; the slot counts above are capacity, not guaranteed occupancy, and the distinction needs stating before the rotation is built.

---

*Owns: the thirty regions, the eight terrain families, band profiles, species weighting, gate placement, region defence cadence and composition, and raid interception terrain. Does not own: node yields or rotation (bible §5.3–5.4), travel times (bible §5.2), raider stats or the wave budget formula (`broodline_combat_numbers.md`), or base stock rates (`broodline_base_stock.md`).*
