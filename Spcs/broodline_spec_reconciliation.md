# Broodline — Spec Reconciliation

*Change control across fourteen documents*

---

## 1. Purpose

Five documents written after the original set — economy model, collectors and raiding, trait codex, chassis roster, enemy archetypes, region roster — resolved contradictions that the original nine still contain.

Anyone reading the genetics spec today learns that Frame determines footprint. It doesn't anymore. Anyone reading the monetization spec learns about the Breeder Tier, which is now the Geneticist Tier everywhere else.

**This document supersedes.** Where it conflicts with an original spec, this is correct. The originals keep the reasoning that produced each decision, which is worth preserving, so the recommendation is to leave them intact and read this alongside rather than editing nine files and losing the argument trail.

---

## 2. Resolved Contradictions

| # | Conflict | Original source | Resolution |
|---|---|---|---|
| 1 | Charge income assumed at 5/day | Genetics §11 Q1 | 25-min regen with cap 5 yields **15–18/day** at three sessions. Mutation fires every ~2.2 days, not six. |
| 2 | Mutation rate: per rolled slot or per splice | Genetics §5 | **3% per splice**, rolled once, mutating slot chosen after |
| 3 | Trait tiers variable or fixed | Genetics §3, §11 Q2 | **One fixed tier per trait.** No Rare Plated Hide. |
| 4 | Footprint owned by Frame | Combat §5 | **Footprint is a chassis property.** Frame keeps HP and armor type only. |
| 5 | Armor type source ambiguous | Combat §5 | **Frame sets armor type. Chassis never does.** |
| 6 | Collectors are Apex-only | Node §4 | **Collectors harvest anything outside the home region.** Apex additionally requires presence for the full window. |
| 7 | Two unrelated −40% constants | Alliance §6, Combat §9 | Distinct names required in implementation. Harvest penalty and cargo loss cap are tuned independently. |
| 8 | Store ladder non-monotonic | Monetization §4A | Corrected ladder in economy model §6. Value per dollar must rise with price. |
| 9 | Breeder vs Geneticist vocabulary | Monetization throughout | **Standardise on Geneticist Tier, Season Pass, Apex Cup.** Monetization spec is the only file affected. |
| 10 | Vault costs unspecified | Vault §3 | Full twelve-tier cost and timer table in economy model §5 |

**Downstream edit required by #4:** the Trait Codex's Heavy Frame entry currently reads "+80% HP, occupies two adjacent tiles." It becomes **+80% HP** with no footprint change, or it duplicates what the Tor chassis already is.

---

## 3. Scope Changes Needing Sign-Off

Two decisions materially change an original spec rather than clarifying it.

**Collectors as general-purpose harvesters (#6).** The node spec frames Collectors as an Apex Vein requirement. The raiding spec makes them the mechanism for harvesting any region you aren't sitting in. The reason is structural: Apex Veins are rare, so Apex-only Collectors means almost no convoys, no raid targets, and a Marks economy with nothing feeding it. The upside is that the map gains a second answer to "there's a better node over there" — relocate and commit, or dispatch and accept exposure. **This is the largest single change in the reconciliation and it should be explicitly accepted or rejected.**

**Campaign length of 65 waves.** The enemy archetype introduction schedule assumes it, and campaign milestones gate Gene Vault Core tiers, which is what makes the economy safe from spending. A shorter campaign moves the payer ceiling in from the nine months the economy model targets. **Campaign length is an economy decision, not a content decision.**

---

## 4. Open Questions Now Closed

| Question | Where asked | Answer |
|---|---|---|
| Is 3% the right mutation rate? | Genetics Q1 | Yes. The concern rested on a charge figure the regen rate doesn't produce. |
| Should Recessive results exist? | Genetics Q2 | No. Incompatible with fixed trait tiers. |
| Terrain visible before relocating? | Combat Q2 | Visible. A 95-minute Ark move is too expensive to gamble on. |
| Enemy archetype count? | Combat Q5 | 12 archetypes from 6 bodies, 6 animation sets. |
| Is 12 Core tiers the right depth? | Vault Q1 | Yes. ~21 months for a core player. |
| Where does the Trait Codex live? | Screens Q2 | Both. It's a component with a browsable index, not a screen. |
| Map legibility on a phone? | Screens Q4 | Partly solved. Three bands allow zoom to 8–12 regions at a time. |
| Canonical chassis colour? | Art Q3 | No. Silhouette carries recognition; colour stays trait-driven. |

---

## 5. Constants Ledger

Numbers that other numbers depend on. Changing any of these cascades, and the cascade is listed so nobody changes one in isolation.

| Constant | Value | What breaks if it moves |
|---|---|---|
| Common Vein yield | 40/hr | Every cost in the game. It's the scaling anchor. |
| Offline accrual cap | 12 hr | Session cadence; punishes sleep below 12 |
| Free-play income spread | ≤6× | Alliance viability for solo players |
| Splice charge regen / cap | 25 min / 5 | Mutation cadence; splice throughput; Roulette demand |
| Mutation rate | 3% per splice | Apex trait supply — the only entry point |
| Damage multipliers | ×1.5 / ×0.67 | Roster diversity; the whole reason to hold >5 creatures |
| Deployment cap | 5 creatures | Swarm chassis value; board legibility |
| Cargo loss cap | 40% | Day-14 churn |
| Attacker share of take | 50% | Raid income; economy inflation |
| Raids per player per day | **2** | **3 pushes a raider to ~600/day and breaks the 6× spread** |
| Non-ally harvest penalty | −40% | Map openness. Capped by alliance spec, never raisable. |
| Alliance stake cap | 5 | Anti-monopoly |
| Vault Core tiers | 12 | ~21-month progression arc |
| Campaign waves | 65 | Payer ceiling, archetype pacing, Core tier gating |
| Regions | 30 | Terrain variety, node rotation, gate topology |
| Chassis | 10 | 40px silhouette test; art budget |
| Traits | 56 | Probability table readability; Codex completion |
| Enemy archetypes | 12 (6 bodies) | Art budget; triangle legibility |
| Lineage depth | 5 generations | Storage; Lineage View layout; Affinity window |
| Broodline Affinity | +5%, one per creature | Late-game power spread |
| New-player raid immunity | 14 days | D14 retention cliff |

The three most fragile are **raids per day**, **cargo loss cap**, and **Common Vein yield**. The first two are the only numbers where a small change directly produces churn; the third invalidates every price in the set.

---

## 6. Still Open, Ranked

**1. 2D or 3D creatures.** Art direction Q1, and now the most consequential unresolved decision in the entire set. The scope has grown considerably since it was asked: ten modular chassis with four trait layers each, six enemy bodies with variants, thirty regions across eight terrain families. Nothing can be budgeted until this is settled, and the modular rig decision the art direction calls "the single most expensive mistake available" sits downstream of it.

**2. Environment art scope.** Art direction Q4, now quantified at thirty regions built from eight terrain families. Families make this tractable; it's still the largest line item after creatures.

**3. Five deployed creatures.** Combat Q1. Interacts with Swarm chassis, which occupy one slot and multiple tiles. Worth testing at four and six before the deployment UI is built.

**4. Behaviour preview animations.** Trait Codex Q1. Fourteen looping previews is the honest way to teach fourteen behaviour trees, and it's uncosted.

**5. Twenty-one unauthored regions.** Region roster Q1. The risk isn't the authoring, it's that players find the most defensible rich region within a week and it becomes the entire meta.

**6. Alliance member cap of 40.** Alliance Q1. Comparable titles run near 100. Unresolved and lower-stakes than the above.

---

## 7. Document Status

| Document | Status |
|---|---|
| Monetization | **Edits needed** — naming (#9), pack ladder (#8) |
| Resource nodes | **Edit needed** — Collector scope (#6), pending sign-off |
| Genetics | **Edits needed** — trait tiers (#3), mutation rate (#2); Q1 and Q2 closed |
| Combat | **Edits needed** — footprint (#4), armor source (#5); Q2 and Q5 closed |
| Alliance & territory | Current. Inner Reach unclaimable extends §8. |
| Screen inventory | Needs new screens: Transit Board, Marks Shop, Codex index, Calibration |
| Art direction | Q3 closed. Q1 and Q4 now blocking. |
| Gene Vault | Current. Q1 closed. Costs now specified externally. |
| FTUE | Current. |
| Economy model | Current |
| Collectors & raiding | Current, pending #6 sign-off |
| Trait codex | **Edit needed** — Heavy Frame footprint |
| Chassis roster | Current |
| Enemy archetypes | Current |
| Region roster | Current — 21 regions unauthored |

---

## 8. Screens Added Since the Inventory

Four screens now exist in specs that the screen inventory's thirty-four doesn't include:

- **Transit Board** — raid target browsing, cargo estimates, matchmaking band filter
- **Marks Shop** — Raid and Defense Marks inventory and spend
- **Codex Index** — browsable trait reference, plus a bottom-sheet component on every trait pip in the app
- **Vault Calibration** — post-tier-12 repeatable module refinement

Total is now **38**. The Codex bottom sheet is the one to build early — it appears over the splice screen, which is the highest-frequency screen in the game and the one where a player most needs a definition without losing their place.

---

*This document should be updated whenever a spec changes rather than allowing the set to drift again. The constants ledger in §5 is the part most worth keeping current.*
