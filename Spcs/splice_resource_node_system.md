# Splice — Resource Node System
*Design spec, expanding the Mobile Gene Ark relocation mechanic*

---

## 1. Core Concept

Resource nodes are what actually make the Mobile Gene Ark worth moving. Without spawn/depletion tied to the map, there's no reason to ever relocate. This system creates that pressure.

---

## 2. Node Types & Richness Tiers

| Tier | Richness | Spawn Frequency | Depletion Time | Notes |
|---|---|---|---|---|
| **Common Vein** | Base Gene Shard rate | Common, scattered everywhere | Never fully depletes, but yield decays ~30% after a week of continuous harvesting | Safe fallback, low competition |
| **Rich Deposit** | 3x base rate | Moderate, region-dependent | Depletes fully after ~5-7 days of active harvesting | Worth relocating for, draws competition |
| **Apex Vein** | 8x base rate + chance of rare trait fragments | Rare, spawns randomly on a rotating schedule | Depletes fast (~48-72 hrs) | High-value, high-contest — these are the ones alliances fight over |

Decay/depletion is the mechanic that actually drives movement — if nodes never ran dry, there'd be zero reason to ever leave a good spot.

---

## 3. Spawn & Rotation Logic

- **Common Veins** are static — always present in every region, mostly a baseline safety net
- **Rich Deposits** rotate weekly — a scheduled "map refresh" shifts a portion of Rich Deposits to new regions every 7 days, timed to the weekly live-ops cadence (Gene Lab event, etc.) so there's already a reason players check the map that day
- **Apex Veins** spawn semi-randomly, announced ~30 minutes before appearing (a "seismic activity detected in Region X" style alert) — this creates a rush moment, similar to a boss-spawn event, and rewards alliances that react fast

---

## 4. Harvesting Mechanic

Nodes aren't infinite-range — your Ark must be within its home region (or an adjacent one, at reduced rate) to harvest a node. Harvesting is passive once you're positioned correctly (ties back into the idle-style economy), but yield scales with:

- Lab tier (Gene Vault upgrades)
- Whether you or your alliance controls the region
- Node richness tier

Apex Veins require **active harvesting** — you must have a Collector unit physically present and can be intercepted (this is where it connects directly to the Harvester/Collector raiding piece).

---

## 5. Competition & Contested Nodes

Rich Deposits and Apex Veins can be shared or contested:

- If only one Ark is in range, they get full yield
- If multiple rival Arks are in range simultaneously, yield splits proportionally to a "presence" stat (time spent in-region + Lab tier), encouraging early arrival over last-minute swooping
- Alliance-controlled regions let allied members harvest cooperatively at full rate, non-allies get a harvesting penalty — this is what makes territory control matter economically, not just for bragging rights

---

## 6. Decay & the "Why Move" Pressure Curve

This is the actual retention hook:

- Sitting still is fine short-term but guaranteed to get worse — your Common Vein decays, your Rich Deposit eventually depletes entirely
- This creates a natural weekly rhythm: check the map, see what's rotated in, decide whether it's worth the transit risk to relocate
- **Design guardrail:** never let decay drop a stationary player below a livable baseline — this should feel like "you're leaving value on the table by not moving," not "you're being punished for standing still." Punishing inaction kills F2P retention; incentivizing action is the healthier framing.

---

## 7. Map Visualization

- Regions shown with a heat-map overlay (green = rich/available, red = contested/depleted, gold pulse = Apex Vein active)
- Tapping a region shows: current richness, who controls it, estimated travel time from your current Ark position, and any live alerts
- This is the screen players will check every login — effectively the daily "where do I go" decision hub, similar to how Kingshot players check event timers each day

---

## 8. Tie-Back to Monetization & Live-Ops

- **Scout reports** (a small paid or Gene-Shard-cost item) reveal Apex Vein spawns a few minutes earlier than the free alert — low-power, high-desirability purchase
- Weekly node rotation naturally syncs with the existing Gene Lab weekly event, so the store's featured offer that week can bundle harvesting boosts
- Apex Vein rushes are exactly the kind of shareable, exciting moment (like Kingshot's Fishing Event) that makes for good marketing clips and screenshots

---

*Next: this connects directly into the Harvester/Collector raiding piece, since Apex Veins require an active Collector unit that can be intercepted mid-transit.*
