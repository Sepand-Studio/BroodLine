# Broodline — Server Topology

*Technical spec, how many players share thirty regions*

> **CURRENT — technical spec.** `broodline_region_roster.md` §10 and
> `broodline_region_graph.md` §11 both close on the same open question: thirty
> regions across how many concurrent players. This answers it.

**The answer is not a product decision, it is derived.** Node depletion sets it, and the number falls out at **500 to 1,500 daily active per server** — §3. Getting there also surfaces a mechanic that has to exist and is not written down anywhere: **depletion must scale with harvester count**, or contested nodes are not contested — §2.

---

## 1. What has to be true

Five things the design already commits to, and a server population that breaks any of them is wrong.

| | Source |
|---|---|
| Rich Deposits are worth relocating for | Bible §5.8 — relocation is the map's central decision |
| Territory is contestable by more than one or two alliances | Bible §6.8 anti-monopoly |
| The Transit Board has targets within three regions | `broodline_collectors_raiding.md` §6 |
| The Inner Reach stays uncrowded enough to be a refuge | `broodline_region_roster.md` §4 |
| An individual alliance member is visible | Bible §6.2 — the 40 cap exists for this |

---

## 2. Depletion must scale with harvesters

Bible §5.3: a Rich Deposit "depletes in ~6 days of active harvesting." Alliance §6: allied members harvest **at full rate simultaneously, no yield splitting.**

**Those two together decide server population, and the interaction is not written down.**

If depletion is a six-day timer that runs regardless of who is on the node, then fifty harvesters extract fifty times the yield for the same six days, and there is no contest — a node is a free resource that happens to expire. If depletion instead tracks total extraction, then fifty harvesters exhaust it in three hours, and at any meaningful population Rich Deposits are gone before most players see them.

> **Depletion tracks total extraction. Six days is the figure for a single harvester.**
>
> `remaining_yield -= rate × harvester_count` per tick.
>
> **Floor 24 hours, ceiling the weekly tick.** A deposit never dies before a player who relocated for it can arrive, and never survives the rotation that clears it.

That is the only version where `broodline_region_roster.md` §4's inverse rule means anything — the best nodes sit on the worst ground *and* draw competition, and both halves of that need the second to be real.

**It also makes server population a derived quantity rather than a guess**, because it puts a hard ceiling on how many players a node can usefully support before it stops being worth travelling to.

**Adopted into bible §5.3**, with the two bounds. The bible previously set six days as a flat timer explicitly to sync with the weekly rotation; the ceiling preserves that sync without the timer.

---

## 3. The population figure

**Target: a Rich Deposit lasts two to four days at typical load.** Shorter and relocating for one is not worth ninety minutes of transit; longer and there is no pressure to move at all.

Against a six-day single-harvester capacity, that means **1.5 to 3 concurrent harvesters per Rich Deposit** on average.

**Rich Deposit supply per server**, from `broodline_region_roster.md` §5:

| Band | Regions | Rich each | Total |
|---|---|---|---|
| Inner | 8 | 0–1 | ~4 |
| Mid | 12 | 1–2 | ~18 |
| Outer | 10 | 2–3 | ~25 |
| | | | **~47** |

At two concurrent harvesters each, **~94 players actively working a Rich Deposit at any given moment.**

Harvesting is passive and continuous — an Ark parked in a region extracts whether the player is online or not, bounded by the 12-hour offline accrual cap. So "actively working" is closer to "parked on" than to "playing," and a reasonable share of a server's active roster is parked on a Rich Deposit at any time.

> **500 to 1,500 daily active players per server.**

**Cross-checks:**

| | |
|---|---|
| **Territory** | 22 claimable regions, 5 stakes per alliance. Room for **7 to 10 alliances** to hold meaningful territory. At 40 members that is 280–400 players in territorial alliances — a plausible share of 500–1,500 |
| **Transit Board** | Three-region radius, ±2 Core tier band. At the low end a player sees a handful of convoys; at the high end, dozens. Both work; below 500 the board goes empty |
| **Inner Reach** | 8 regions and no reason to be there past week two. It stays uncrowded by disinterest rather than by capacity |
| **Alliance visibility** | 7–10 alliances of 40 on a server of 1,000 means most active players are in one, which is what makes the social layer legible |

**The binding constraint is the low end, not the high end.** A server of 300 has an empty Transit Board, two or three alliances, and uncontested territory — which removes three systems at once. A server of 2,000 has fast-depleting Rich Deposits, which is a tuning problem rather than a structural one.

---

## 4. Server lifecycle

**New servers open on a population trigger, not a schedule.** When existing servers in a region cross their upper band, a new one opens. Opening servers on a calendar produces empty ones.

**A server is never closed while it has active players.** Territory, alliances and creature rosters are per-server and per-player respectively; closing a server would destroy the first and strand the second.

**Merging is the answer to decline, and it is expensive.** Two servers merging means two sets of territory claims resolving against one map, and there is no obvious rule that is not unfair to somebody. **Design it before it is needed** — a merge run in a hurry against a dying server is how live games lose their most invested players.

**The merge rule:** territory is cleared on merge and the first weekly tick after re-opens all claims. Cruel and legible, against the alternative of an arbitration nobody trusts. Announced two weeks ahead, with the tick countdown visible for the whole fortnight.

---

## 5. Regional assignment

`broodline_alliance_territory.md` §12 sets the weekly tick per server from three fixed slots — APAC, EMEA and Americas prime evening — chosen at creation and never changed.

> **Players are assigned a server by storefront region at signup**, and may not transfer.

**No transfers, and that is a harder rule than it looks.** A player who moves country, or who wants to play with a friend on another server, will ask. The answer is no, because transfers move a player between two live territory states and there is no honest way to do it — their alliance membership, their stakes and their raid history are all server-scoped.

**What is offered instead: a second account on another server.** Free, unlinked, starts at Holdfast.

---

## 6. The weekly tick

The heaviest scheduled job in the game, and it does four things at once, per bible §6.7.

| Order | |
|---|---|
| **1** | Resolve territory — Hold accrual, contested stakes, decay |
| **2** | Rotate Rich Deposits — deplete, respawn, redistribute |
| **3** | Roll Apex Vein spawns |
| **4** | Close the weekly event, grant rewards, open the next |

**Order matters.** Territory resolves before node rotation, so an alliance that wins a region gets the new nodes rather than the old ones. Rotating first would hand the previous holder a week's worth of fresh deposits on ground they just lost.

**The tick is not instantaneous and must not appear to be.** It touches every region and every alliance on the server. **Run it as a brief announced maintenance window** — five minutes, with a countdown visible for the preceding hour — rather than as a silent transaction that produces a map nobody watched change.

That window is also a design opportunity. One moment a week when the whole server is looking at the same thing is the closest this game gets to a shared event, and bible §6.7 already argues the synchronisation exists to give players one reason to log in rather than three scattered ones.

---

## 7. Static versus per-server state

Per `broodline_data_model.md` §7, most of the map is content rather than data.

| Static — ships with the build | Per-server — stored |
|---|---|
| Thirty region definitions | Node type, remaining yield, depletion timestamp |
| Terrain family, lane count, pockets | Controller and stake state |
| Adjacency and gate pairings | Ark presence |
| Species weighting | Hold accrual per alliance |
| Band membership | Tick hour |

**A server's entire map state is a few kilobytes.** Every server runs the identical thirty regions with the identical graph, which is what makes the map learnable across accounts and what keeps the Route Plotter's pathfinding a static computation.

---

## 8. Guardrails

- Depletion tracks total extraction, never a wall-clock timer, bounded by a 24-hour floor and the weekly tick
- Server population stays inside 500–1,500 daily active; a new server opens on the upper trigger
- No server closes while it has active players
- A merge clears all territory; claims re-open at the first tick after
- No player transfers between servers
- Tick hour is fixed at server creation and never changed
- The tick resolves territory before rotating nodes
- Every server runs the identical thirty regions and the identical graph
- The Inner Reach's controller field is null in schema, on every server, permanently

---

## 9. Open questions

1. **The 1.5-to-3 harvesters-per-deposit target is the number everything else derives from, and it is a judgement.** Two days feels short and four feels long, but neither has been played. It should be the first thing re-derived after soft launch, because every figure in §3 moves with it.
2. **What counts as a harvester for depletion?** An Ark parked in the region, or an Ark actively accruing? An offline player at their 12-hour cap is arguably not harvesting, and counting them would deplete nodes for absent players.
3. ~~**The merge rule at §4 is a starting point and it will be unpopular.**~~ **Resolved — clear all territory on merge; every claim re-opens at the first weekly tick after. Cruel and legible, against an arbitration nobody would trust.**
4. **Three tick slots against a global player base leaves gaps.** A player in India assigned to the EMEA slot gets a tick at roughly 22:30 local, which is late but workable. A player in Hawaii on the Americas slot gets one at 16:00, which is fine. The edges are survivable; a fourth slot would reduce the gaps and split the population further.
5. **Nothing here addresses what happens to a player whose server merges twice.** Rare, and worth thinking about once rather than twice.

---

*Owns: server population and its derivation, lifecycle, regional assignment, the tick job and the static/per-server split. Does not own: node yields (bible §5.3), territory rules (`broodline_alliance_territory.md`), or entity shapes (`broodline_data_model.md`).*
