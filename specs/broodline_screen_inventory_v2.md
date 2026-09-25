---
status: current
folder: 01-companions
verified-against: broodline_bible.md (2026-09-24)
note: >
  48 named screen and flow entries, build priority, rework column for the designed prototypes.
  Supersedes broodline_screen_inventory.md. Revised 2026-09-05 for register
  Part 6: Relocate Ark loses its risk column, Route Plotter gains a per-gate
  one, Roulette becomes a permanent Lab tab, the alliance Founder badge
  becomes Leader, and the Splice Census and Marks Shop are corrected. Store
  contents were reconciled to the September 24 product decision.
---

# Broodline — Screen Inventory

*Rebuilt against the design bible §1–10. Replaces the original inventory.*

---

## 1. How to read this

Twenty-one entries have design prototypes. This maps them against what the bible now requires, names what changed, and lists what was never designed. The counts below describe design coverage, not which screens are implemented in Unity.

**Status key:**
`✅` designed and current · `🔧` designed, needs rework · `🆕` not designed

**Total: 48 named screen and flow entries.** Twenty-one designed (six current, fifteen needing rework); twenty-seven have no design prototype. Counted from the rows in sections 3–9 on 2026-09-24.

**Revised 2026-09-24** against register Part 6 and the approved Store economy. Screens changed by the register pass carry a **`⚠`** marker.

---

## 2. Navigation

**Five tabs: Map · Ark · Splice · Lab · Allies.** No Store tab — the store is reached contextually from offers, currency taps and the event hub.

| Tab | Destination |
|---|---|
| **Map** | World Map |
| **Ark** | Gene Ark |
| **Splice** | Splice Chamber |
| **Lab** | Gene Lab |
| **Allies** | Alliance Hub |

**Persistent top bar:** Splice Charges with regen timer, Gene Shards, Geneticist Tier progress, mail badge, active event badge.

**Onboarding begins with two tabs** and reveals the rest as systems unlock (§9.7). A five-tab bar on first launch is a wall of unexplained choices.

Pushed sub-screens carry a back chevron and no bottom nav.

---

## 3. Splice hub — the core loop

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Splice Chamber** | 🔧 **Rebuild** | The prototype is a static file with no logic — nothing to port. Needs: three slots, the lock affordance on combat slot 1, coverage tiers on parent traits, mutation and Aberrant sub-roll as two separate numbers, generation and coverage ceiling, and the destruction notice above the CTA. CTA reads "Splice — consumes both parents." | §2.2, §2.6, §2.7 |
| **Splice Confirm** | 🆕 | Standard dialog naming both parents. Second dialog for Founders, naming the creature, buttons reading "Consume Ash" / "Keep Ash". Never suppressible. | §2.7 |
| **Splice Reveal** | ✅ | Consumption line already correct. Now confirms rather than discloses. | §2.1 |
| **Creature Roster** | 🔧 | Two trait pips, not four, plus an Instinct badge. Filter by species, generation, trait, coverage tier. Show garrison/escort/deployed state. | §1.1, §10.4 |
| **Creature Detail** | 🆕 | Species, two combat traits with coverage tier, Instinct, generation, coverage ceiling, Aberrant marker, current assignment. | §1.1, §2.5 |
| **Lineage View** | 🆕 **High priority** | Five-generation tree plus the **species composition strip** above it. Trait contributions per ancestor, mutation and Aberrant markers, Founder roots. Closes session one. | §3.5 |
| **Founder Naming** | 🆕 | First five creatures, one prompt in session one, rest across days 1–3, sensible defaults, renameable. | §9.4 |
| **Trait Codex** | 🆕 **Required** | Every trait by category, what it counters, what each coverage tier provides, plain description of each Instinct. Without it the counter system is learnable only by losing. | §4.7 |
| **Growth** | 🔧 | Prototype is static and implies feeding. Growth is cosmetic, advances on age, grants nothing. Remove feeding entirely. | §1.5 |

---

## 4. Ark hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Gene Ark** | ✅ | Pack/travel/unpack phases as one continuous transform. | §5.1 |
| **Campaign Select** | 🆕 | Wave list, completion state, replay rewards, **raider composition preview** — never hidden, per the guardrail. | §4.8, §4.12 |
| **Wave Defense** | 🔧 | Pockets beside the lane. Lane count 1–3 from the region. Remove armour-type displays. Rally control. Visible Instinct trigger states. | §4.2, §4.6 |
| **Post-Wave** | 🆕 | Rewards, sample drops, creature outcomes, regeneration assignments. | §4.11 |
| **Wave Defeat** | ✅ | Already names the raider that broke through and the answering trait — the single most important teaching screen in the game. Free retry, no paywall. | §4.11, §9.3 |
| **Replay Viewer** | 🆕 **Launch-critical** | Full simulation playback with per-creature behaviour visible. The difference between PvP that builds a community and PvP that bleeds one. | §4.10 |
| **Regeneration Tracker** | 🆕 | Creatures in regen, timers, skip cost. | §4.11 |

---

## 5. Map hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **World Map** | ✅ | Heat-map states, Ark markers, Collector routes, Apex pulses. | §5.7 |
| **Region Detail** | 🔧 | Currently a card on the map. Add **lane count** — relocation changes defensive terrain and hiding that makes the move a gamble. | §5.7, §4.2 |
| **Relocate Ark** | 🔧 ⚠ | **The route table and its intercept-risk column are removed.** The Ark is never a raid target; relocation takes the shortest path and varies only in time. The screen shows destination, lane count, travel time scaled by Drive, and the costs always shown — harvest forfeited, defenders stowed, lab timers unaffected. | §5.2, register 6.1 |
| **Apex Alert** | 🔧 | Add the **catalyst** to the yield description. Scout report sells detail and a head start, never access. | §5.3, §5.9 |
| **Collector Dispatch** | 🆕 | Collector class, cargo, escort slots, route options. Escorting creatures become unavailable. | §5.6 |
| **Route Plotter** | 🆕 ⚠ | **Collector routes only.** Direct, alliance corridor and night move, with **each gate on the route named, its controller shown, and its exposure window in minutes** — 20 normally, 10 through an allied-controlled gate, 0 on a night move. Exposure is fixed; nothing on this screen may imply a facility or purchase shortens it. | §5.6, register 6.1–6.2 |
| **Convoy Status** | 🆕 ⚠ | In-transit Collectors, ETA, and **which gate the convoy is approaching or inside**, with the window counting down. Immune everywhere else, and it should look immune. | §5.6, register 6.2 |
| **Collector Intercept** | ✅ | Time-boxed decision, ally escort. | §5.6 |
| **Transit Board** | 🆕 ⚠ | The raider's side. Convoys currently inside a gate window within three regions, showing **cargo band, escort count and minutes remaining only** — never a cargo figure, never escort traits. Daily raid counter and per-target cooldown state. | §5.6, Collectors §7 |

---

## 6. Lab hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Gene Lab** | 🔧 | Six facilities remapped: Splicing Chamber, Hatchery, Gene Vault, Harvest Array, Drive, Core. Remove Trait Archive and Alliance Hall. Core caps all others and shows its campaign milestone requirement. Next-tier effects in concrete numbers. | §7.2, §7.3 |
| **Splice Roulette** | 🆕 ⚠ | **A permanent tab in the Lab, not an event screen.** Featured pool of six traits with the rotation countdown, the shared probability table, sample inventory, 300-shard spin cost, and the **pity counter visible before the first spin**. Copy states plainly that Roulette raises coverage on traits already owned and cannot grant a new one. | §7.6, §8.4, register 6.8 |
| **Sample Store** | 🔧 | Renamed from Gene Vault. Twelve species traits only — no Instinct or Aberrant samples. Three tabs: Samples, Fuse, Archive. The 88% capacity threshold stays as designed. | §1.7, §7.6 |

---

## 7. Alliance hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Alliance Hub** | 🔧 ⚠ | Three tabs current. Remove any Alliance Hall reference. **The head-of-alliance role is Leader, not Founder** — badge, roster labels, permissions copy and application flow all change; "Founder" now refers only to a player's first five creatures. | §6.1, §6.2, register 6.12 |
| **Alliance Rally** | ✅ | The `hasPierce` check gating outcome independently of power is the design thesis in working form. Keep exactly as built. | §4.4, §6.4 |
| **Stake Management** | 🆕 | Five stake slots, Hold accrual, contest state, weekly tick countdown, decay warnings well in advance. | §6.3, §6.9 |
| **Garrison Assignment** | 🆕 | Contributed creatures, capacity, **contributor visibility**. Warn when garrisoning a creature carrying a counter the player holds nowhere else. | §6.5 |
| **Stake Assault** | 🆕 | Target selection, rate limit state, entry to the combat engine. | §6.4 |
| **Alliance Tech** | 🆕 | Three branches, treasury contribution, unlocked perks. Logistics only. | §6.7 |
| **Join / Apply** | 🆕 ⚠ | Alliance browse and application state. Join mode is set by the **Leader**. | §6.2, register 6.12 |
| **World Chat** | 🔧 ⚠ | Attached game objects, escort requests, recipe cards. **Add the moderation affordances**: long-press to report with context captured automatically, player block, officer mute. No UGC surface ships without filter, report and block. | §6, §3.7, moderation §6 |

---

## 8. Store and events

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Store** | 🔧 ⚠ | Three tabs current. Free daily gift leads the Packs tab. Five mixed-content bundles use **sample pulls**, never trait pulls: Starter Splice, Lab Bundle, Lab Expansion, the restored 48-hour Mythic Lab Access, and Geneticist's Vault. **Double Regen at $9.99 remains the anchor and only permanent purchase.** The $49.99 and $99.99 products are plain direct-shard rows; the $99.99 row is never surfaced in a banner or offer. Custom Chest has explicit Small/Standard/Large quantities and makes no savings claim without separate-item reference prices. | §8.3, September 24 Store decision |
| **Geneticist Profile** | 🔧 | Renamed from Breeder Profile. **Tier 6 perk becomes a sample pull.** Tier 12 title unchanged. | §7.5 |
| **Splice Census** | 🔧 ⚠ | Formerly "Gene Lab Event" — **renamed, because Gene Lab is the facility.** Server goal, personal contribution tiers, community milestones at 40/70/100%, five-day run then two days dark. | §8.4, register 6.10 |
| **Event Hub** | 🆕 | Live-ops calendar, active events, timers, themed offers. | §8.4 |
| **Apex Cup** | 🆕 ⚠ | Shared-gauntlet leaderboard, Core-tier bracket, rewards. Names are a UGC surface: **reportable rows and an anonymous-entry option.** | §8.4, moderation §3 |
| **Recipe Share** | 🆕 ⚠ | Browse and rate shared recipes with ancestry chains. **Structured data only — no title, no description, no comments.** Numeric rating, one per account, weighted by account age and splice count. Report control on every card. | §3.7, §8.4, moderation §4 |
| **Marks Shop** | 🆕 ⚠ | **One Marks currency**, not two. Escort-slot expansion, route obfuscation, regen skips, convoy skins and defence flair. No purchase path of any kind appears on this screen. | §8.2, register 6.4 |

---

## 9. Onboarding and system

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Onboarding** | 🔧 | Rebuild against the eight beats. Beat 5 teaches counters by letting one work. Beat 8 ends on Lineage View. **The designed first loss** at session 2–3 needs authoring as a deliberate beat. | §9.2, §9.3 |
| **Mail / Notifications** | 🆕 | Raid alerts, alliance notices, event announcements, reward grants. | — |
| **Settings / Account / Support** | 🆕 ⚠ | Required for store submission. Must carry the **published support contact**, the **block list**, and the age-gate result. | moderation §5–6 |
| **Age Gate** | 🆕 ⚠ | At account creation, before any name entry. Drives the under-13 restricted mode: curated Founder-name pool, generated display name, chat off, Recipe Share read-only. | moderation §5 |
| **Report / Block** | 🆕 ⚠ | Shared flow reachable from chat, recipe cards, profiles, alliances and leaderboard rows. Captures surrounding context automatically. | moderation §6 |

---

## 10. Build priority

**Tier 1 — the core loop is provable without any of the rest**
Splice Chamber · Splice Confirm · Splice Reveal · Creature Roster · Wave Defense · Gene Ark

**Tier 2 — the reason to keep playing**
Lineage View · Creature Detail · Trait Codex · Campaign Select · Post-Wave · Wave Defeat · World Map · Region Detail

**Tier 3 — the reason to spend**
Store · Geneticist Profile · Gene Lab · Sample Store · Splice Roulette · Event Hub · Founder Naming filter path

**Tier 4 — the reason to stay**
Alliance Hub · Chat · Stake Management · Garrison · Alliance Tech · Collector Dispatch · Route Plotter · Convoy Status · Transit Board · Replay Viewer

**Tier 5 — completeness**
Everything remaining, plus Mail and Settings.

**Three screens jump their tier for submission reasons.** **Age Gate**, **Report / Block** and the support contact in **Settings** are store-submission requirements, not features. They gate release regardless of build order, and the filter path on **Founder Naming** ships with them — a permanent, public, unfiltered name entered in session one is the surface most likely to fail review.

**Two placements are deliberate.** The **Trait Codex** sits in Tier 2 rather than Tier 5 because the counter system is the game's central mechanic and there is currently nowhere to learn it. The **Lineage View** sits in Tier 2 because it closes the first session and is the game's distinctive artifact — it should be visible early rather than treated as late-game.

The **Replay Viewer** is in Tier 4 but flagged launch-critical. It is the thing that makes a lost raid tolerable, and shipping raiding without it is the version of PvP that bleeds a community.

---

## 11. Shared components

Build once, reuse everywhere.

- **Creature card** — species silhouette, generation badge, **two trait pips with coverage tier**, Instinct badge, Aberrant marker, assignment state. Appears on at least nine screens. Worth disproportionate effort; it is the atom of the interface.
- **Trait pip** — icon, coverage tier, counter indicator. The single most-repeated element in the app.
- **Currency header** — charges with regen timer, shards, tier progress.
- **Probability table** — used on Splice and Roulette. Regulatory-relevant; build it once and correctly.
- **Region tile** — heat-map colouring, controller banner, lane count, alert state.
- **Timer chip** — regeneration, transit, charge regen, weekly tick, build timers.
- **Confirmation dialog** — two levels, standard and named. Never suppressible.
- **Gate chip** ⚠ — gate name, controller banner, exposure window in minutes. Used on Route Plotter, Convoy Status, Transit Board and Region Detail. New in the Part 6 revision.
- **Report control** ⚠ — one affordance, six surfaces, context captured automatically.

---

## 12. Open

1. **Where the Trait Codex lives** — inside the Lab tab, or contextually from the splice screen and Wave Defeat. Contextual is more discoverable; standalone is more browsable. Leaning both: a tab entry plus deep links from any trait pip.
2. **Map legibility on a small phone** with heat-map, territory banners, Ark markers, Collector routes and alerts live at once. The hardest layout problem in the app.
3. **Whether Region Detail warrants its own screen** or stays a card on the map. Adding lane count and the gate chip probably pushes it over.
4. ⚠ **Does Splice Roulette belong in the Lab tab or the Event Hub?** Now that it is always on, the Lab is the honest home — the Event Hub implies a countdown that no longer exists. Leaning Lab, with a deep link from the Event Hub while a new pool is fresh.
