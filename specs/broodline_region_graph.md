# Broodline — The Region Graph

*Content, the map's connectivity*

> **CURRENT — content, not design.** `broodline_region_roster.md` owns the thirty
> regions, their terrain, richness and species weighting. This document owns how
> they connect.

**One correction falls out of it.** Bible §5.2's travel times were written before adjacency existed and read as journey classes; with a graph they have to be per-segment, and a minimal Inner-to-Outer journey comes out at 125 minutes rather than 95 — §6.

---

## 1. Why this exists

The Route Plotter cannot be built without it. Neither can interception, which resolves per segment; neither can the Transit Board's three-region radius; neither can relocation, which needs a path rather than a distance.

`broodline_region_roster.md` §10 recorded adjacency as "a level-design artifact rather than a design decision," constrained by the eight gate pairs and by two-to-four borders per region. That is true, and it also means nobody was going to do it until someone did.

---

## 2. Three rings

Each band is a ring. Bands connect only through gates, and **no region borders a region two bands away** — an Inner-to-Outer journey always crosses the Mid Reach.

| Band | Regions | Structure |
|---|---|---|
| **Inner** | 8 | A ring of 8, plus two chords |
| **Mid** | 12 | A ring of 12, plus two chords |
| **Outer** | 7 + 3 | A ring of 7, plus a chain of 3 hanging off it |

**Forty-three edges total.** Ten inside the Inner Reach, fourteen inside the Mid, eleven inside the Outer, and eight gates.

---

## 3. The Inner Reach

Ring order, clockwise. Gates marked **G**.

> Holdfast — **Tellin G** — Ashfold — **Millgate G** — Quillmoor — **Windfell G** — Barrowlight — **Chalkrise G** — back to Holdfast

**Gates alternate with non-gates**, so no two gates are adjacent and every Inner region is at most one hop from one.

**Two chords:** Ashfold–Barrowlight and Quillmoor–Holdfast. They cut across the ring, so a convoy crossing the Inner Reach is never forced all the way around, and they give four regions a third border.

### Holdfast sits between two gates, and that is why it is the starting region

The most connected place in the safest band. A new player parks in a region with three Common Veins, no Rich Deposits, one lane and six pockets — and watches convoy traffic pass through in both directions from day one.

**Nothing about that exposes them.** The Inner Reach is unclaimable, Holdfast has nothing worth taking, and raid immunity runs fourteen days. What it does is make the map legible before the player has any reason to enter it: routes converge where they are standing, and other people's Collectors go past.

---

## 4. The Mid Reach

Twelve regions, and **the four Inner gates alternate with the four Outer gates** so that no single region is both.

> **Greyspan G↓** — Sablewick — **Coldharrow G↑** — **Fenwatch G↓** — Thornwyke — **Dunmarsh G↑** — **Highmarl G↓** — The Narrows — **Netherfold G↑** — **Stonewake G↓** — Rookspire — **Cadewater G↑** — back to Greyspan

**G↓** connects inward to the Inner Reach; **G↑** connects outward to the Outer Reach.

> **Crossing the Mid Reach always costs at least one intra-band segment.** No convoy goes Inner to Outer in two hops.

That is the rule the alternation exists to produce, and it is what gives the Mid Reach a purpose beyond being a corridor. **Four short crossings exist** — Coldharrow–Fenwatch, Dunmarsh–Highmarl, Netherfold–Stonewake, and Cadewater–Greyspan — where an outward gate sits directly beside an inward one. Those four are the map's main arteries, they are predictable, and they are where raiding concentrates.

**Two chords:** Sablewick–The Narrows and Thornwyke–Rookspire, cutting across the ring.

---

## 5. The Outer Reach

Not a ring. **A ring of seven with a chain of three hanging off it.**

**The ring**, with all four gates:

> **Deepscree G** — Blacksump — **The Spill G** — Kettlemoor — **Threnody G** — Saltwrack — **Sheerdown G** — back to Deepscree

**The deep chain**, hanging between the two non-gate ring regions:

> Blacksump — **The Gyre** — **Weltering** — **Rimfall** — Kettlemoor

### The three Delta regions are the three deepest

Not a coincidence. **The Gyre, Weltering and Rimfall are the map's only Delta regions** — three parallel lanes, four pockets, the hardest defensive ground in the game — and they carry one Common Vein, three Rich Deposits and Apex eligibility each.

**They are also the furthest from any gate:**

| Region | Hops to the nearest gate |
|---|---|
| The Gyre | 2 |
| Rimfall | 2 |
| **Weltering** | **3 — the deepest region on the map** |

**This is `broodline_region_roster.md` §4's inverse rule expressed as topology rather than as a table.** The best nodes sit on the worst ground, and the worst ground is also the furthest from safety. A convoy hauling Apex cargo out of Weltering crosses three Outer segments before it even reaches a gate.

**Weltering is the map's prize and its trap.** Richest, hardest to defend, hardest to reach, hardest to get anything home from. An alliance that holds it has made a real commitment.

---

## 6. Gate pairings

Eight gates, fixed pairings. A gate is a single edge between two named regions.

| Inner | ↔ | Mid |
|---|---|---|
| Tellin | ↔ | Greyspan |
| Millgate | ↔ | Fenwatch |
| Windfell | ↔ | Highmarl |
| Chalkrise | ↔ | Stonewake |

| Mid | ↔ | Outer |
|---|---|---|
| Coldharrow | ↔ | Deepscree |
| Dunmarsh | ↔ | The Spill |
| Netherfold | ↔ | Threnody |
| Cadewater | ↔ | Sheerdown |

**Gates are never closable by any player action**, per `broodline_region_roster.md` §9. An alliance holding both ends of a gate controls a valuable corridor and cannot shut it.

---

## 7. Full adjacency

Thirty regions. Degree is in brackets.

### Inner Reach

| Region | Borders |
|---|---|
| **Holdfast** [3] | Tellin · Chalkrise · Quillmoor |
| **Tellin** [3] | Holdfast · Ashfold · **Greyspan** |
| **Ashfold** [3] | Tellin · Millgate · Barrowlight |
| **Millgate** [3] | Ashfold · Quillmoor · **Fenwatch** |
| **Quillmoor** [3] | Millgate · Windfell · Holdfast |
| **Windfell** [3] | Quillmoor · Barrowlight · **Highmarl** |
| **Barrowlight** [3] | Windfell · Chalkrise · Ashfold |
| **Chalkrise** [3] | Barrowlight · Holdfast · **Stonewake** |

### Mid Reach

| Region | Borders |
|---|---|
| **Greyspan** [3] | Cadewater · Sablewick · **Tellin** |
| **Sablewick** [3] | Greyspan · Coldharrow · The Narrows |
| **Coldharrow** [3] | Sablewick · Fenwatch · **Deepscree** |
| **Fenwatch** [3] | Coldharrow · Thornwyke · **Millgate** |
| **Thornwyke** [3] | Fenwatch · Dunmarsh · Rookspire |
| **Dunmarsh** [3] | Thornwyke · Highmarl · **The Spill** |
| **Highmarl** [3] | Dunmarsh · The Narrows · **Windfell** |
| **The Narrows** [3] | Highmarl · Netherfold · Sablewick |
| **Netherfold** [3] | The Narrows · Stonewake · **Threnody** |
| **Stonewake** [3] | Netherfold · Rookspire · **Chalkrise** |
| **Rookspire** [3] | Stonewake · Cadewater · Thornwyke |
| **Cadewater** [3] | Rookspire · Greyspan · **Sheerdown** |

### Outer Reach

| Region | Borders |
|---|---|
| **Deepscree** [3] | Sheerdown · Blacksump · **Coldharrow** |
| **Blacksump** [3] | Deepscree · The Spill · The Gyre |
| **The Spill** [3] | Blacksump · Kettlemoor · **Dunmarsh** |
| **Kettlemoor** [3] | The Spill · Threnody · Rimfall |
| **Threnody** [3] | Kettlemoor · Saltwrack · **Netherfold** |
| **Saltwrack** [2] | Threnody · Sheerdown |
| **Sheerdown** [3] | Saltwrack · Deepscree · **Cadewater** |
| **The Gyre** [2] | Blacksump · Weltering |
| **Weltering** [2] | The Gyre · Rimfall |
| **Rimfall** [2] | Weltering · Kettlemoor |

**Gate edges in bold.** Every region has two or three borders, which satisfies the two-to-four constraint with room to add a chord later without redesigning anything.

---

## 8. Travel times, reconciled

Bible §5.2 gives three figures: 25 minutes within a band, 50 through a gate, 95 from the Inner Reach to the Outer. **Those were written before adjacency existed**, and they read as journey classes rather than as per-segment costs.

With a graph, they have to be per-segment, and then the third is derived rather than stated.

> **Per segment, at Drive tier 1: 25 minutes within a band, 50 minutes across a gate.**
> Route multipliers apply to the journey: direct ×1.0, alliance corridor ×1.4, night move ×1.7.

| Journey | Segments | Minutes |
|---|---|---|
| Adjacent, same band | 1 | **25** |
| Across a gate | 1 | **50** |
| **Inner to Outer, minimal** | 3 — gate, Mid hop, gate | **125** |
| Holdfast to Deepscree, the nearest Outer region | 5 | **175** |
| **Holdfast to Weltering, the deepest** | 7 | **225** |

Shortest paths, computed against the adjacency at §7.

**A minimal Inner-to-Outer journey is 125 minutes, not 95.** The bible's figure should be corrected, and the reason it was wrong is that it assumed a direct crossing that the Mid-Reach alternation rule at §4 forbids.

### The original three-to-five-hour crossings were right about something

The bible originally set flat crossings of three to five hours and replaced them with the 25/50/95 distance model on the grounds that multi-hour moves are too long for a game whose sessions run ninety seconds.

**The graph produces multi-hour journeys again, as an emergent property of path length.** Holdfast to Weltering is 225 minutes — three hours and forty-five, inside the three-to-five-hour band the original flat model used.

**Both were right about different things.** The original was right that a journey to the deep Outer Reach should feel like a commitment measured in hours. The revision was right that moving one region over should be quick. A per-segment model gives both, and neither a flat constant nor a three-class table could.

**Nothing needs to change except the 95.** The per-segment figures stand, and the long journeys are now a consequence of the map rather than a rule imposed on it.

---

## 9. What the graph produces

**Four arteries.** Coldharrow–Fenwatch, Dunmarsh–Highmarl, Netherfold–Stonewake and Cadewater–Greyspan are the only places an outward gate sits beside an inward one. Almost every Inner-to-Outer convoy uses one, they are predictable, and that is where raiding will concentrate — which is what gates are for.

**One prize at the end of a dead end.** Weltering is three hops from any gate down a chain with no alternative route. Everything that comes out of it takes the same path, and everyone knows what that path is.

**A safe band that is genuinely safe.** Every Inner region has three borders, two chords cut the ring, and nothing there is worth taking. A new player can move around freely before they understand what movement costs.

**No degenerate shortcut.** The alternation rule means the fastest Inner-to-Outer route is 125 minutes and there is no faster one anywhere on the map. Nobody finds the good corner.

---

## 10. Guardrails

- Bands connect only through the eight named gates. No region borders a region two bands away
- Gates are never closable by any player action
- No Mid region is both an inward and an outward gate. Crossing the Mid Reach always costs at least one intra-band segment
- Every region has two to four borders
- The three Delta regions stay the three deepest. Richness, difficulty and distance move together
- Holdfast keeps three borders and stays adjacent to two gates

---

## 11. Open questions

1. **Saltwrack has only two borders and no chord**, making it the thinnest ring region. It is a Scree with Apex eligibility, so it may deserve a third — Saltwrack–The Gyre would connect the ring to the deep chain at a second point and give Weltering an alternative route out. That would also make Weltering less of a dead end, which is either a fix or a loss.
2. **Is one dead-end prize the right number?** Weltering works because it is singular. Two would dilute it; none would remove the map's most distinctive place.
3. **The four arteries may be too concentrated.** Almost all Inner-to-Outer traffic uses four edges, which makes raiding rich and makes those four edges the whole PvP map. Adding a fifth Mid chord between an inward and an outward gate would spread it.
4. **The graph has no asymmetry.** Every band is a regular ring with regular chords, which is clean and slightly lifeless. A few irregular edges would make the map feel authored rather than generated, at the cost of the guarantees at §10.
5. **Server population against thirty regions** is still open from the region roster. The graph makes it sharper: with four arteries and one dead end, a thinly populated server has a very empty Outer Reach and a very busy set of four edges.

---

*Owns: region adjacency, gate pairings, the ring and chain structure, and per-segment travel times. Does not own: region terrain, richness or species weighting (`broodline_region_roster.md`), route multipliers or interception (`broodline_collectors_raiding.md`), or Drive scaling (`broodline_economy_model.md`).*
