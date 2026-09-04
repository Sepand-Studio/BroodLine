# Broodline — Screen Inventory

*Rebuilt against the design bible §1–10. Replaces the original inventory.*

---

## 1. How to read this

Twenty screens exist as design prototypes. This maps them against what the bible now requires, names what changed, and lists what was never designed.

**Status key:**
`✅` designed and current · `🔧` designed, needs rework · `🆕` not designed

**Total: 32 screens.** Twenty designed, of which eleven need rework; twelve missing.

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
| **Relocate Ark** | ✅ | Three routes, costs always shown. | §5.2 |
| **Apex Alert** | 🔧 | Add the **catalyst** to the yield description. Scout report sells detail and a head start, never access. | §5.3, §5.9 |
| **Collector Dispatch** | 🆕 | Collector class, cargo, escort slots, route options. Escorting creatures become unavailable. | §5.6 |
| **Route Plotter** | 🆕 | Direct vs allied-territory routes, interception risk per segment. | §5.6 |
| **Convoy Status** | 🆕 | In-transit collectors, ETA, exposure phase. | §5.6 |
| **Collector Intercept** | ✅ | Time-boxed decision, ally escort. | §5.6 |

---

## 6. Lab hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Gene Lab** | 🔧 | Six facilities remapped: Splicing Chamber, Hatchery, Gene Vault, Harvest Array, Drive, Core. Remove Trait Archive and Alliance Hall. Core caps all others and shows its campaign milestone requirement. Next-tier effects in concrete numbers. | §7.2, §7.3 |
| **Sample Store** | 🔧 | Renamed from Gene Vault. Twelve species traits only — no Instinct or Aberrant samples. Three tabs: Samples, Fuse, Archive. The 88% capacity threshold stays as designed. | §1.7, §7.6 |

---

## 7. Alliance hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Alliance Hub** | 🔧 | Three tabs current. Remove any Alliance Hall reference. | §6.2 |
| **Alliance Rally** | ✅ | The `hasPierce` check gating outcome independently of power is the design thesis in working form. Keep exactly as built. | §4.4, §6.4 |
| **Stake Management** | 🆕 | Five stake slots, Hold accrual, contest state, weekly tick countdown, decay warnings well in advance. | §6.3, §6.9 |
| **Garrison Assignment** | 🆕 | Contributed creatures, capacity, **contributor visibility**. Warn when garrisoning a creature carrying a counter the player holds nowhere else. | §6.5 |
| **Stake Assault** | 🆕 | Target selection, rate limit state, entry to the combat engine. | §6.4 |
| **Alliance Tech** | 🆕 | Three branches, treasury contribution, unlocked perks. Logistics only. | §6.7 |
| **Join / Apply** | 🆕 | Alliance browse and application state. | §6.2 |
| **World Chat** | ✅ | Attached game objects, escort requests, recipe cards. | §6, §3.7 |

---

## 8. Store and events

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Store** | 🔧 | Three tabs current. **Rewrite four pack descriptions** — sample pulls, never trait pulls. Free daily gift leads the Packs tab. | §8.3, §8.6 |
| **Geneticist Profile** | 🔧 | Renamed from Breeder Profile. **Tier 6 perk becomes a sample pull.** Tier 12 title unchanged. | §7.5 |
| **Splice Roulette** | 🆕 | Wheel, displayed odds, sample inventory, spin cost. Samples only. | §8.4 |
| **Gene Lab Event** | ✅ | Server goal, tasks, leaderboard, event offer. | §8.4 |
| **Event Hub** | 🆕 | Live-ops calendar, active events, timers, themed offers. | §8.4 |
| **Apex Cup** | 🆕 | Leaderboard, bracket, rewards. | §8.4 |
| **Recipe Share** | 🆕 | Browse and rate shared recipes with ancestry chains. | §3.7, §8.4 |
| **Marks Shop** | 🆕 | Raid and defence marks inventory and spend. | §5.6 |

---

## 9. Onboarding and system

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Onboarding** | 🔧 | Rebuild against the eight beats. Beat 5 teaches counters by letting one work. Beat 8 ends on Lineage View. **The designed first loss** at session 2–3 needs authoring as a deliberate beat. | §9.2, §9.3 |
| **Mail / Notifications** | 🆕 | Raid alerts, alliance notices, event announcements, reward grants. | — |
| **Settings / Account / Support** | 🆕 | Required for store submission. | — |

---

## 10. Build priority

**Tier 1 — the core loop is provable without any of the rest**
Splice Chamber · Splice Confirm · Splice Reveal · Creature Roster · Wave Defense · Gene Ark

**Tier 2 — the reason to keep playing**
Lineage View · Creature Detail · Trait Codex · Campaign Select · Post-Wave · Wave Defeat · World Map · Region Detail

**Tier 3 — the reason to spend**
Store · Geneticist Profile · Gene Lab · Sample Store · Splice Roulette · Event Hub

**Tier 4 — the reason to stay**
Alliance Hub · Chat · Stake Management · Garrison · Alliance Tech · Collector Dispatch · Route Plotter · Convoy Status · Replay Viewer

**Tier 5 — completeness**
Everything remaining, plus Mail and Settings.

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

---

## 12. Open

1. **Where the Trait Codex lives** — inside the Lab tab, or contextually from the splice screen and Wave Defeat. Contextual is more discoverable; standalone is more browsable. Leaning both: a tab entry plus deep links from any trait pip.
2. **Map legibility on a small phone** with heat-map, territory banners, Ark markers, Collector routes and alerts live at once. The hardest layout problem in the app.
3. **Whether Region Detail warrants its own screen** or stays a card on the map. Adding lane count may push it over.
