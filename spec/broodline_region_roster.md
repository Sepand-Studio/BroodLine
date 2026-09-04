# Broodline — Region Roster

*Design spec, the map itself*

---

## 1. Why This Document Exists

Five systems resolve against regions and none of them define one.

The node spec places veins in regions and scales yield by adjacency. The combat spec derives lane layout, emplacement count, and terrain modifiers from "the region your Ark currently occupies," and calls this the most valuable connection in the design. The alliance spec claims them. The raiding spec draws routes across them and reduces interception inside allied ones. The chassis spec weights base-stock drops by region.

All five are querying a table that doesn't exist.

---

## 2. Topology

**Thirty regions in three concentric bands.**

| Band | Regions | Character |
|---|---|---|
| **Inner Reach** | 8 | Settled, Warden-held. Low richness, forgiving terrain. |
| **Mid Reach** | 12 | The working map. Where most players live. |
| **Outer Reach** | 10 | Fracture-adjacent. High richness, punishing terrain. |

Bands solve the screen inventory's hardest layout problem. Thirty regions with heat-map overlay, territory banners, Ark position, collector routes, and alerts all live at once is illegible on a phone — but a map that zooms *by band* shows eight to twelve regions at a time, which is readable.

**Adjacency:** each region borders two to four others. Band crossings happen only through **gates** — four between Inner and Mid, four between Mid and Outer.

Gates are the most useful structural decision here. They give raiders predictable chokepoints to watch, they make the Route Plotter's direct-versus-allied choice meaningful rather than cosmetic, and they mean a convoy hauling Apex cargo from the Outer Reach has a genuinely dangerous journey rather than an abstractly risky one.

**Each server instance runs its own copy of the same thirty regions**, with independent node rotation, territory, and weekly tick timing.

---

## 3. Terrain Families

Eight tactical grammars. The combat spec is right that regions should be hand-authored — these are not generators. Each of the thirty is an authored instance within a family, and the family guarantees the tactical identity is legible before the player arrives.

| Family | Lanes | Emplacements | Signature |
|---|---|---|---|
| **Defile** | 1 | High | Single chokepoint. The easiest defense in the game. |
| **Fork** | 2 | Medium | Two lanes converging late |
| **Basin** | 2 | High | Open, generous placement. The neutral baseline. |
| **Terrace** | 2 | Medium | Elevation-heavy; range bonuses everywhere |
| **Shelf** | 2 | Medium | Split-level, lanes at different elevations |
| **Flood** | 3 | Low | Water slows enemies, but few places to stand |
| **Scree** | 3 | Low | Dead ground scattered through the field |
| **Delta** | 4 | Medium | Wide, late convergence. The hardest defense. |

---

## 4. The Inverse Rule

The single most important line in this document:

> **Richness and defensibility run opposite.** The best nodes sit on the worst ground.

Outer Reach regions skew Delta, Scree, and Flood. Inner Reach regions skew Defile, Terrace, and Basin.

Without this rule the map has a correct answer — a region that is both rich and defensible is simply the best place to be, everyone converges on it, and relocation stops being a decision. With it, every move is a trade: an Apex Vein on a four-lane Delta means better yield and a harder region defense, and holding it demands a different roster than the one that worked at home.

This is also what makes the combat spec's terrain-follows-region connection pay off. It isn't just flavour that relocating changes your battlefield — it's the cost you accept for the yield you wanted.

---

## 5. Band Profiles

| | Inner | Mid | Outer |
|---|---|---|---|
| **Common Veins** | 2–3 | 2 | 1–2 |
| **Rich Deposit slots** | 0–1 | 1–2 | 2–3 |
| **Apex eligible** | No | Rare | Yes |
| **Region defense** | Tiers 1–3 | Tiers 1–6 | Full pool, Progenitor at active Apex |
| **Terrain skew** | Defile, Terrace, Basin | Fork, Shelf, Basin | Delta, Scree, Flood |
| **Claimable** | No | Yes | Yes |

**Inner Reach is unclaimable.** Eight regions that no alliance can ever control, permanently open to everyone. The alliance spec guarantees Common Veins stay neutral; this goes further and guarantees a whole band. A new player, a solo player, or a player whose alliance just collapsed always has somewhere to stand that nobody can contest, and the five-stake cap already means dominant alliances must choose regardless.

Note that Common Vein count *falls* as richness rises. The safe fallback is thinnest exactly where the prizes are largest — another expression of §4, and it means the Outer Reach punishes a player who parks there and stops paying attention.

---

## 6. Sample Regions

Three per band, showing the schema. The remaining twenty-one are a content authoring pass against these rules.

| Region | Band | Family | Lanes | Emplace | Common | Rich | Apex | Chassis weight |
|---|---|---|---|---|---|---|---|---|
| **Holdfast** | Inner | Defile | 1 | 9 | 3 | 0 | — | Tor, Ridgeback |
| **Quillmoor** | Inner | Terrace | 2 | 7 | 2 | 1 | — | Kestrel, Bellow |
| **Ashfold** | Inner | Basin | 2 | 8 | 3 | 0 | — | Cur, Whelp |
| **Greyspan** | Mid | Fork | 2 | 6 | 2 | 2 | Rare | Pike, Harrier |
| **The Narrows** | Mid | Shelf | 2 | 6 | 2 | 1 | — | Cradle, Kestrel |
| **Fenwatch** | Mid | Flood | 3 | 4 | 2 | 2 | Rare | Skein, Cur |
| **Deepscree** | Outer | Scree | 3 | 4 | 1 | 3 | Yes | Tor, Pike |
| **Rimfall** | Outer | Delta | 4 | 5 | 1 | 3 | Yes | Harrier, Skein |
| **The Spill** | Outer | Flood | 3 | 3 | 2 | 2 | Yes | Whelp, Bellow |

Holdfast is the intended starting region — one lane, nine emplacements, three Common Veins, no competition. It is the most defensible ground in the game and the poorest, which is exactly the lesson the map needs to teach first.

---

## 7. Chassis Distribution

The chassis spec establishes that chassis can only *leave* a roster through splicing, so base-stock supply has to keep all ten circulating. Region weighting is the mechanism.

**Rules:**
- Every chassis is weighted in **at least three regions**, with at least one in each band. No chassis is Outer-Reach-only, or a player can be locked out of a role by not being ready for the Outer Reach.
- Each region weights **two chassis**, roughly tripling their appearance rate in that region's node harvests
- Weighting affects harvest drops only. Wave completion stays uniform across all ten, per the anti-lockout guarantee.

The payoff is a second reason to move that has nothing to do with yield. "I need Cradles and The Narrows drops them" is a relocation decision driven by roster composition rather than economics, and it gives the Inner Reach a purpose beyond being the beginner band.

---

## 8. Travel Times

| Route | Ark relocation | Collector (1.0× class) |
|---|---|---|
| Within band | 25 min | 18 min |
| Through a gate | 50 min | 35 min |
| Inner to Outer | 95 min | 70 min |

Collector times scale by class speed — 1.5× for Scout, 0.6× for Convoy Rig. A Convoy Rig running Inner to Outer is exposed for roughly seventy minutes of a two-hour route, which is the risk that justifies its twelve-thousand cargo.

Ark relocation is deliberately slower than any Collector. Moving the Ark is the committed choice; sending a Collector is the flexible one.

---

## 9. Region Detail Screen

The node spec promises a detail panel. Against this schema it needs:

- Current richness and node inventory, live
- Controller and alliance banner
- **Terrain preview** — family, lane count, emplacement count, modifiers present
- Travel time from current Ark position, and separately from each dispatched Collector
- Chassis weighting for the region
- Region defense tier
- Active alerts

Terrain preview settles the combat spec's second open question in favour of visibility. Hiding terrain makes relocation a gamble, and a ninety-five-minute commitment is too expensive to gamble on. Showing it is what turns §4's inverse rule from a hidden trap into a legible trade.

---

## 10. Guardrails

- Inner Reach is permanently unclaimable
- Common Veins exist in every region without exception, and are never claimable anywhere
- Richness and defensibility run opposite; no region is best at both
- Every chassis is obtainable in every band
- Terrain is always visible before committing to a move
- No region is ever locked behind purchase, tier, or alliance membership
- Gates are never closable by any player action

---

## 11. Open Questions

1. **Twenty-one regions still need authoring**, and they need to hold the §4 inverse rule under adversarial reading. Players will find the most defensible rich region within a week and it will become the whole meta.
2. **Are four gates per band boundary enough?** Fewer makes raiding sharper and route choice more meaningful; it also funnels every Outer convoy through predictable ground, which may cross from tense into miserable.
3. **Should the Inner Reach ever open to claiming** in a late-game season? It would give veteran alliances somewhere new to fight and it would break the guarantee new players depend on. Leaning firmly no.
4. **Does terrain vary between region defense and raid interception in the same region?** The raiding spec derives ambush terrain from the route segment, which may or may not be the region's authored layout.
5. **Thirty regions across how many concurrent servers?** Affects whether the Outer Reach feels contested or empty, and it's a live-ops decision more than a design one.

---

*Remaining undocumented: mail and notification centre, player profile, and settings.*
