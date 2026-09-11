---
status: superseded
folder: 99-archive
superseded-by: broodline_monetization.md
note: >
  Era-2 monetization spec.
---

# Splice — Monetization Spec v1

*A dual-path (free + paid), live-ops-driven monetization system modeled on the proven Whiteout Survival / Kingshot playbook, adapted for a tower-defense/creature-breeding core loop.*

---

## 1. Core Currencies

| Currency | Type | Earned Free | Bought | Used For |
|---|---|---|---|---|
| **Splice Charges** | Soft, capped | Regen over time + daily login + rewarded ads | Packs, Breeder Pass | Combining two creatures into a hybrid |
| **Gene Shards** | Premium/hard | Slow drip from play (wave completion, achievements) | Direct purchase, packs | Cosmetic skins, chest unlocks, rare trait pulls, converting to Breeder XP |
| **Breeder XP** | Progression meter | Daily login streak, gem conversion, event participation | Accelerated via Gene Shard packs | Unlocks Breeder Tier (this game's VIP ladder) |

**Design rule:** every currency must have *both* a free (slow) and paid (fast) path. Never gate core play entirely behind payment — only gate *speed*.

---

## 2. Splice Charges — Regen & Caps

- Starting cap: **5 charges**
- Regen rate: **1 charge per 25 minutes** (tuned to refill roughly once between two average sessions)
- Cap increases with Breeder Tier (see below): +1 cap per 3 tiers, max cap of 10 at top tier
- Charges never fully block play — players can always watch/manage existing hybrids, replay completed levels for Gene Shard drip, or claim event rewards with 0 charges

**Free-to-play paths to more charges:**
- Daily login streak (resets on missed day): escalating charges on days 3/7/14/30
- Rewarded video ad: +1 charge, capped at 3/day
- Weekly "Gene Lab" event completion (see §5): bonus charges

---

## 3. Breeder Tier (VIP-equivalent ladder)

12 tiers, gated by cumulative **Breeder XP**.

- Breeder XP earned free via: daily login (up to 500 XP/day, streak-scaled), completing weekly events, first-time achievements
- Breeder XP earned paid via: every pack purchase includes XP; Gene Shards convertible to XP at a fixed rate (e.g., 2:1)
- Each tier unlocks **permanent** account-wide perks — not power spikes, but convenience/acceleration:
  - Tier 2: +1 splice charge cap
  - Tier 4: 10% faster charge regen
  - Tier 6: daily free rare-trait pull
  - Tier 8: unlock 2nd simultaneous splice queue
  - Tier 10: cosmetic hybrid aura unlocked
  - Tier 12: unique title + permanent 15% Gene Shard bonus on all purchases

This mirrors Whiteout's proven approach: VIP feels like *earned status*, not pay-to-win, because top perks are convenience and cosmetic, not raw combat power.

---

## 4. Store Structure

### A. Fixed Packs (impulse tier)
| Pack | Contents | Price |
|---|---|---|
| Starter Splice | 5 charges + 100 Gene Shards + 500 Breeder XP | $0.99 |
| Breeder Bundle | 15 charges + 300 Gene Shards + 1,500 Breeder XP + 1 rare trait pull | $4.99 |
| Lab Expansion | 40 charges + 1,000 Gene Shards + 5,000 Breeder XP + 3 rare trait pulls | $9.99 |
| Geneticist's Vault | 100 charges + 3,000 Gene Shards + 15,000 Breeder XP + 8 rare trait pulls + exclusive skin | $19.99 |
| Mythic Lab Access | Unlimited splice charges for 48 hrs + 5,000 Gene Shards | $14.99 |

First purchase of any pack: **2x contents, one-time only** — proven first-time conversion booster.

### B. Custom Chest (build-your-own — the highest-converting Whiteout mechanic)
Player picks 3 of 6 reward slots (charges / shards / XP / trait pulls / cosmetic / speed-up) to fill a chest at a given price point ($1.99 / $4.99 / $9.99 tiers). This beats fixed bundles because players stop feeling like they're paying for stuff they don't want.

### C. Breeder Pass (Battle Pass equivalent)
- Seasonal (4-week cycles), themed to a "Gene Lab season"
- **Free track:** modest charges, shards, cosmetic fragments
- **Paid track ($9.99/season):** 2-3x the free track's rewards, plus exclusive hybrid skins, a guaranteed rare-trait creature, and Breeder XP boosts
- Critical design point: paid track = *more of the same currency, faster*, not exclusive power — keeps it feeling fair.

---

## 5. Live-Ops Calendar (rotating named events — the Kingshot trick)

Static stores go stale. Kingshot ships major content every ~2 weeks; each event has its own themed offer.

| Event | Cadence | Mechanic | Monetization Hook |
|---|---|---|---|
| **Gene Lab** | Weekly | Community-wide splice goal (like a raid boss) | Limited-time charge/shard bundle tied to event |
| **Splice Roulette** | Bi-weekly | Gacha-style rare trait pull wheel | Discounted "extra spins" pack |
| **Breeder's Cup** | Monthly | Competitive PvP leaderboard using your best hybrids | Leaderboard-boost bundle (cosmetic flair + charges) |
| **Mutation Surge** | Seasonal (4x/yr) | Temporary new trait type available only during event | FOMO-driven limited trait pack |
| **Founder's Recipe Share** | Ongoing | Players share/rate best splice combos publicly | Cosmetic "recipe card" purchasable frame/skin |

Each event should launch with its own small themed offer (like Whiteout's Custom Chest) — this is what keeps ARPDAU high without the store ever feeling static.

---

## 6. Trial / Hook Mechanics (Whiteout's "2nd builder" trick)

- **First wall moment:** the first time a player runs out of charges (~session 2-3), grant a free **24-hour "Double Regen" trial** automatically, no purchase needed.
- When the trial ends, offer to extend it: soft-currency (Gene Shards) for 2 more days, or a small real-money pack for permanent Double Regen.
- This "let them feel the upgrade before asking for money" pattern converts far better than a cold paywall, and it's the single highest-leverage tactic in Whiteout's model.

---

## 7. Guardrails (keep it from feeling predatory)

- Charges never fully block progress — always something playable at 0 charges
- Rewarded ads always available as a free lever, capped daily to avoid ad fatigue
- No trait/creature is purely "pay-only" — every rare trait obtainable free, just slower
- Top Breeder Tier perks stay cosmetic/convenience, never raw win-more power
- Cap "FOMO" events at a reasonable frequency — burnout kills LTV faster than a missed sale

---

## 8. Why this works (evidence base)

- **Dual-path currency + VIP ladder:** Whiteout Survival — $3.4B lifetime revenue, zero ads, pure pay-to-progress model built almost entirely on this pattern.
- **Custom/build-your-own bundles:** Whiteout's Custom Chest Offer — personalization drives higher attach rate than fixed bundles.
- **Trial-then-paywall:** Whiteout's free 15-min 2nd-builder trial before offering a paid permanent unlock.
- **Rotating named live-ops events tied to fresh offers:** Kingshot shipped 5 major events in under 75 days, and its ARPDAU ($1.45) outperforms Whiteout's ($1.21) largely because of tighter event-offer synchronization.
- **Narrative wrapper reduces grind fatigue:** Whiteout frames scarcity as part of the survival story, which measurably improves session length and retention.

---

*Next steps: playtest charge regen timing (25 min may need tuning based on actual session data), validate pack price points against target market ARPU, and build the first Gene Lab event as a soft-launch test of the live-ops cadence.*
