---
status: current
folder: 01-companions
supersedes: 99-archive/broodline_region_roster_era2.md
verified-against: broodline_bible.md (2026-09-05)
note: >
  The thirty regions, reconciled to bible §4.2 (lane count 1–3 is the only
  tactical variable), §5.2–5.5 (relocation, nodes, harvesting), §6.8 (anti-
  monopoly) and the campaign's lane ladder. Terrain families, emplacements and
  chassis weighting are gone. All thirty regions are authored with adjacency.
  Travel times are soft-launch starting values scaled to the bible's ~3h
  crossing.
---

# Broodline — Region Roster

*Design spec, the map itself. Companion to bible §5 and §6.*

---

## 1. Why this document exists

Five systems resolve against regions: nodes spawn in them (§5.3–5.4), lane count comes from them (§4.2), alliances claim them (§6.3), Collectors route across them (§5.6), and region defence scales with their richness (§4.8). The bible says there are "roughly thirty" and that they are hand-authored. None of them are authored anywhere. This is the table.

---

## 2. What a region is

A region has exactly these properties. Nothing else varies.

| Property | Range | Set by |
|---|---|---|
| **Band** | Inner, Mid, Outer | This document |
| **Lane count** | 1–3 | This document. The only tactical variable (§4.2) |
| **Common Veins** | 1–3 | This document. Static, always present, never claimable (§5.4) |
| **Rich Deposit slots** | 0–3 | This document. Capacity, not occupancy — the weekly rotation decides which slots are live |
| **Apex eligible** | No / Rare / Yes | This document |
| **Adjacency** | 2–4 neighbours | This document |
| **Claimable** | Inner no; Mid and Outer yes | This document |
| **Controller** | Alliance or neutral | Weekly tick (§6.3) |
| **Live nodes, Arks present, alerts** | — | Runtime |

There are no terrain families, elevation bands, water tiles, emplacement counts or modifiers. The bible removed them: under hard counters they add noise to a decision already made at the splice screen (§4.2), and regions differ in look and lane arrangement only (§10.8). Visual identity is an art task; tactical identity is one integer.

---

## 3. Topology

**Thirty regions in three concentric bands.**

| Band | Regions | Character |
|---|---|---|
| **Inner Reach** | 8 | Settled. Low richness, mostly one lane. Unclaimable. |
| **Mid Reach** | 12 | The working map. Where most players live from week two. |
| **Outer Reach** | 10 | High richness, mostly three lanes. Where alliances fight. |

Bands solve the map-screen legibility problem §5.7 sets up: thirty regions with heat-map, banners, Arks, routes and alerts all live at once is illegible on a phone, but a map that zooms by band shows eight to twelve at a time.

**Each band is a ring.** Every region borders its two ring neighbours. Band crossings happen only through **gates** — four between Inner and Mid, four between Mid and Outer — so a gate region has three neighbours and every other region has two.

Gates are the structural decision that makes §5.2's route choice real. A direct crossing runs through a gate everyone can see; the alliance corridor runs through allied regions; a convoy hauling Apex cargo from the Outer Reach has a genuinely dangerous journey rather than an abstractly risky one.

**Each server runs its own copy of the same thirty regions**, with independent rotation, territory and tick timing. Layout is identical across servers so that shared knowledge ("Ironshaw is the western gate") transfers.

---

## 4. The inverse rule

> **Richness and lane count rise together.** The best nodes sit on the hardest ground.

This is the bible's §4.2 statement — an Apex region is more dangerous to hold than a Common one — made into an authoring rule. Without it the map has a correct answer: a rich one-lane region is simply the best place to be, everyone converges on it, and relocation stops being a decision. With it, every move is a trade. An Apex Vein on three lanes means better yield and a region defence that demands four counters held simultaneously, which is a different roster than the one that worked at home.

Applied per band:

| | Inner | Mid | Outer |
|---|---|---|---|
| **Lanes** | 1 (×4), 2 (×4) | 1 (×2), 2 (×7), 3 (×3) | 2 (×3), 3 (×7) |
| **Common Veins** | 2–3 | 2 | 1–2 |
| **Rich Deposit slots** | 0–1 | 1–2 | 2–3 |
| **Apex eligible** | No | Rare (3 regions) | Yes (all) |
| **Region defence pool** | Skirmisher, Breaker, Lash | + Courser, Brood, Drift | All eight |
| **Region defence types** | 1–2 | 2–3 | 3–4 |
| **Claimable** | No | Yes | Yes |

Within a band the rule holds too: the two-lane Inner regions are the ones with a Rich slot, and the three-lane Mid regions are the Apex-eligible ones.

**The region-defence pool follows the campaign.** Inner Reach sends only the chapter 1–2 raiders, because a player is there from day 3 (§9.6) and should not meet a raider in region defence before the campaign has taught it. Mid adds the chapter 3–5 raiders; Outer sends everything. Region defence waves obey §4.5 — never two raiders sharing a trait, never more than four types — and scale by volume with live richness.

**Common Vein count falls as richness rises.** The safe fallback is thinnest exactly where the prizes are largest, so the Outer Reach punishes a player who parks and stops paying attention, while §5.8's livable baseline still holds everywhere.

---

## 5. Inner Reach is unclaimable

Bible §6.8 guarantees Common Veins are never claimable. This goes further: **eight regions no alliance can ever control.** A new player, a solo player (§6.10) or a player whose alliance collapsed always has somewhere to stand that nobody contests, and the five-Stake cap already forces dominant alliances to choose. Nothing about §6 changes; the Inner Reach simply has no Stake slot.

---

## 6. The thirty regions

Names sit in a natural-history and landform register. None reuses a species, trait, raider or facility name.

### Inner Reach — ring of 8

| Region | Lanes | Common | Rich | Neighbours | Gate to |
|---|---|---|---|---|---|
| **Holdfast** | 1 | 3 | 0 | Quillmoor, Ferncross | — |
| **Quillmoor** | 2 | 2 | 1 | Holdfast, Ashfold | Greyspan |
| **Ashfold** | 1 | 3 | 0 | Quillmoor, Larkwell | — |
| **Larkwell** | 2 | 2 | 1 | Ashfold, Sedgecombe | Tarnbeck |
| **Sedgecombe** | 1 | 3 | 0 | Larkwell, Brackenhold | — |
| **Brackenhold** | 2 | 2 | 1 | Sedgecombe, Millstead | Dunmarrow |
| **Millstead** | 1 | 3 | 0 | Brackenhold, Ferncross | — |
| **Ferncross** | 2 | 2 | 1 | Millstead, Holdfast | Thornmere |

**Holdfast is the starting region.** One lane, three Common Veins, no Rich slot, no competition — the most defensible ground in the game and the poorest, which is the lesson the map teaches first. Both its neighbours are gate regions with a Rich slot, and §7 below guarantees one of them is live in any week.

### Mid Reach — ring of 12

| Region | Lanes | Common | Rich | Apex | Neighbours | Gate to |
|---|---|---|---|---|---|---|
| **Greyspan** | 2 | 2 | 1 | — | The Narrows, Slatefall | Quillmoor |
| **The Narrows** | 2 | 2 | 2 | — | Greyspan, Coldmere | Deepscree |
| **Coldmere** | 1 | 2 | 1 | — | The Narrows, Tarnbeck | — |
| **Tarnbeck** | 2 | 2 | 1 | — | Coldmere, Ironshaw | Larkwell |
| **Ironshaw** | 3 | 2 | 2 | Rare | Tarnbeck, Saltwend | Blackfen |
| **Saltwend** | 2 | 2 | 1 | — | Ironshaw, Dunmarrow | — |
| **Dunmarrow** | 2 | 2 | 1 | — | Saltwend, The Oxbow | Brackenhold |
| **The Oxbow** | 3 | 2 | 2 | Rare | Dunmarrow, Wrenfold | Stormreach |
| **Wrenfold** | 1 | 2 | 1 | — | The Oxbow, Thornmere | — |
| **Thornmere** | 2 | 2 | 1 | — | Wrenfold, Fenwatch | Ferncross |
| **Fenwatch** | 3 | 2 | 2 | Rare | Thornmere, Slatefall | Nightmarsh |
| **Slatefall** | 2 | 2 | 2 | — | Fenwatch, Greyspan | — |

Eight of the twelve are gates. That is deliberate: the Mid Reach is the transit band, and a Collector crossing from Outer to Inner passes through two of its gates in sequence. Coldmere and Wrenfold — the one-lane regions with no gate — are the Mid Reach's quiet corners, where a player who wants Mid-level yield without Mid-level traffic can sit.

### Outer Reach — ring of 10

| Region | Lanes | Common | Rich | Apex | Neighbours | Gate to |
|---|---|---|---|---|---|---|
| **Deepscree** | 3 | 1 | 3 | Yes | Rimfall, Riftmouth | The Narrows |
| **Rimfall** | 3 | 1 | 3 | Yes | Deepscree, The Spill | — |
| **The Spill** | 2 | 2 | 2 | Yes | Rimfall, Blackfen | — |
| **Blackfen** | 3 | 1 | 3 | Yes | The Spill, Glasswaste | Ironshaw |
| **Glasswaste** | 2 | 2 | 2 | Yes | Blackfen, Stormreach | — |
| **Stormreach** | 3 | 1 | 3 | Yes | Glasswaste, Cairnhead | The Oxbow |
| **Cairnhead** | 3 | 2 | 2 | Yes | Stormreach, Nightmarsh | — |
| **Nightmarsh** | 3 | 1 | 3 | Yes | Cairnhead, Brimscar | Fenwatch |
| **Brimscar** | 2 | 2 | 2 | Yes | Nightmarsh, Riftmouth | — |
| **Riftmouth** | 3 | 1 | 2 | Yes | Brimscar, Deepscree | — |

The four Outer gates are the four richest regions. A convoy leaving any of them for home starts on the most contested ground in the game, and the two-lane Outer regions — The Spill, Glasswaste, Brimscar — are each two hops from a gate. Those are the Outer Reach's "hold and defend" positions: rich enough to be worth an alliance's Stake, far enough from the gates that the traffic is intentional.

**Totals:** 45 Rich Deposit slots (Inner 4, Mid 16, Outer 25), 13 Apex-eligible regions, 52 Common Veins.

---

## 7. Rotation and the tutorial guarantee

Bible §5.4: Rich Deposits rotate weekly; a portion shifts to new regions on the tick. Slots here are capacity. **How many are live in a given week is set by the economy model** against server population — that number, not the slot count, is what tunes competition.

One rule this document adds: **every rotation places at least one live Rich Deposit in Quillmoor or Ferncross.** Bible §9.6 requires the day-two map introduction to arrive attached to a specific, visibly better node; both are adjacent to Holdfast and both are gates, so the first relocation is a one-hop move that also shows the player a gate. A permanent Rich Deposit would be shared by every new player on the server under §5.5's presence split and yield nothing; a guaranteed-but-rotating one keeps the target fresh week to week.

---

## 8. Travel times

Bible §5.2 gives the direct crossing at ~3 hours with alliance corridor at ~4h20 and night move at ~5h10. Those are a full crossing. Per hop:

| Hop | Ark, direct | Collector, base class |
|---|---|---|
| Within a band (ring neighbour) | 45 min | 35 min |
| Through a gate | 1h 30m | 1h 10m |
| Inner to Outer (two gates, minimum path) | ~3h | ~2h 20m |

Route modifiers apply on top per §5.2: alliance corridor ×1.45, night move ×1.7 plus 120 Gene Shards. The **Drive** facility scales all Ark times and the Collector exposure window. Collector class scales Collector times; classes are specified in the Collectors and raiding companion, not here.

Ark relocation is slower than any Collector at every hop. Moving the Ark is the committed choice; sending a Collector is the flexible one.

**Adjacent harvesting.** Bible §5.5 allows harvesting a neighbouring region's nodes at a reduced rate. Adjacency here is ring adjacency plus gates — a Holdfast Ark can reach Quillmoor's Rich Deposit at the reduced rate without moving, which is the safe-but-slow option the day-two lesson contrasts against.

---

## 9. What regions do not do

Two era-2 mechanics are gone and it is worth saying why.

**No species weighting on drops.** The bible's §7.6 weights harvest drops toward traits the roster already carries; a second, region-based weighting would fight it, and chassis no longer exist. The second reason to move that this gave — "I need a Hollow line and The Narrows drops them" — is real and is lost. See open question 1.

**No terrain preview beyond lane count.** Bible §5.7 already puts lane count on the region tap. There is nothing else to preview.

**Raid interception does not use region lanes.** Bible §4.9 derives the ambush board from the route segment as a short lane. A convoy intercepted in a three-lane region fights on the raid board, not the region's. This settles the era-2 open question.

---

## 10. Region Detail

On tap (§5.7), in this order:

- Richness state and live node inventory
- Controller and banner, or *Unclaimable* for Inner Reach
- **Lane count** and the region-defence raider pool
- Travel time from the current Ark position, and from each dispatched Collector
- Neighbours, with gates marked
- Active alerts

---

## 11. Guardrails

- Inner Reach is permanently unclaimable
- Every region has at least one Common Vein; Common Veins are never claimable anywhere
- Lane count and Rich slot count rise together; no region is both rich and one-lane
- Every lane count occurs in at least two bands, so a player can find practice ground before the campaign demands it
- Region defence never sends a raider the band's pool excludes
- Lane count is always visible before committing to a move
- Every rotation leaves a live Rich Deposit adjacent to Holdfast
- No region is locked behind purchase, tier or alliance membership
- Gates are never closable by any player action

---

## 12. Open questions

1. **Sample affinity for the Outer Reach.** Each Apex-eligible region could carry one species whose samples its Rich and Apex drops favour. Samples are coverage, never access, so the anti-lockout guarantee is untouched, and it would give alliances a reason to contest *specific* regions rather than whichever is live. I would do it — but it stacks a second weighting on the §7.6 drop table and needs a bible amendment, so it is not in this document.
2. **Live Rich Deposits per rotation.** 45 slots; the economy model sets occupancy against the 500–1,500 DAU server target. Below about a third live, the Mid Reach feels empty; above two-thirds, rotation stops mattering.
3. **Eight Mid gates of twelve.** The transit band is very porous. If raiding proves too diffuse, close two Mid–Outer gates (Ironshaw and Fenwatch keep; Narrows and Oxbow lose) and re-check convoy route lengths.
4. **Should the Inner Reach ever open to claiming** in a late season? It would give veteran alliances a new front and break the guarantee new players depend on. Firmly no.
