# Broodline — Screen Inventory

*Rebuilt against the design bible §1–10. Replaces the original inventory.*

---

## 1. How to read this

Twenty screens exist as design prototypes. This maps them against what the bible now requires, names what changed, and lists what was never designed.

**Status key:**
`✅` designed and current · `🔧` designed, needs rework · `🆕` not designed

**Total: 59 screens.** Eight are designed and current, thirteen are designed and need rework, thirty-eight have never been designed.

*An earlier header said 32 screens — twenty designed, eleven needing rework, twelve missing. The tables below always held more than that, and fourteen further screens have since been added from the raiding, live-ops, moderation, codex and alliance documents. The count here is the count in the tables. **Twenty-one designed screens against thirty-five undesigned ones is the real state of the design workstream**, and the earlier number made it look roughly half done when it is roughly a third.*

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
| **Trait Codex** | 🆕 **Required** | Browsable index over 34 entries — twelve traits, six Instincts, eight Aberrants, eight raiders. Filterable by kind, tier, discovered and owned state. **Opens on the threat board.** | §4.7 |
| **Threat Board** | 🆕 **Required** | Eight raiders and their answering traits side by side, with the species that carries each. The Codex's most-used view and the cheapest thing in it. Unlocks at wave 6 with the designed loss. | §4.7, §9.3 |
| **Codex Bottom Sheet** | 🆕 **Build first** | The Codex as a component, not a screen. Every trait pip in the app is tappable and opens the entry in place with no navigation loss. It appears over the splice screen, which is where a player most needs a definition without losing their place. | §4.7 |
| **Growth** | 🔧 | Prototype is static and implies feeding. Growth is cosmetic, advances on age, grants nothing. Remove feeding entirely. | §1.5 |

---

## 4. Ark hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Gene Ark** | ✅ | Pack/travel/unpack phases as one continuous transform. | §5.1 |
| **Campaign Select** | 🆕 | Wave list, completion state, replay rewards, **raider composition preview** — never hidden, per the guardrail. | §4.8, §4.12 |
| **Wave Defense** | 🔧 | Pockets beside the lane. Lane count 1–3 from the region. Remove armour-type displays. Rally control. Visible Instinct trigger states. | §4.2, §4.6 |
| **Post-Wave** | 🆕 | Rewards, sample drops, creature outcomes, regeneration assignments. | §4.11 |
| **Wave Defeat** | 🔧 | Names the raider and offers a free retry — correct. **Needs rework to distinguish three failures**: access (nobody carries it), coverage (the tier was too low), placement (the carrier was in the wrong lane). Naming the trait alone is right at wave 6 and misleading everywhere the player already holds it. | §4.11, §9.3 |
| **Replay Viewer** | 🆕 **Launch-critical** | Full simulation playback with per-creature behaviour visible. The difference between PvP that builds a community and PvP that bleeds one. | §4.10 |
| **Regeneration Tracker** | 🆕 | Creatures in regen, timers, skip cost. | §4.11 |
| **Raid Defence Alert** | 🆕 | The 90-second countdown. Open in time and the defender places escorts live; otherwise it auto-resolves and produces a replay. | Raiding §9 |
| **Region Defence** | 🆕 | Entry, composition preview scaled to region richness, what a loss costs. Distinct from Campaign Select — this is the rent, not the ladder. | §4.8, §7.2 |

---

## 5. Map hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **World Map** | ✅ | Heat-map states, Ark markers, Collector routes, Apex pulses. | §5.7 |
| **Region Detail** | 🔧 | Currently a card on the map. Add **lane count** — relocation changes defensive terrain and hiding that makes the move a gamble. | §5.7, §4.2 |
| **Relocate Ark** | ✅ | Three routes, costs always shown. | §5.2 |
| **Apex Alert** | 🔧 | Add the **catalyst** to the yield description. Scout report sells detail and a head start, never access. | §5.3, §5.9 |
| **Transit Board** | 🆕 | Raid target browse: cargo estimate, escort count, exposure time remaining. Never escort traits or generation. ±2 Core tier band enforced server-side, not in the filter. | Raiding §6 |
| **Raid Party Select** | 🆕 | Three-creature party, full loadout including Instinct. Warn when committing a creature carrying a counter held nowhere else. | Raiding §7 |
| **Collector Dispatch** | 🆕 | Collector class, cargo, escort slots, route options. Escorting creatures become unavailable. | §5.6 |
| **Route Plotter** | 🆕 | Direct vs allied-territory routes, interception risk per segment. | §5.6 |
| **Convoy Status** | 🆕 | In-transit collectors, ETA, exposure phase. | §5.6 |
| **Collector Intercept** | ✅ | Time-boxed decision, ally escort. | §5.6 |

---

## 6. Lab hub

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Gene Lab** | 🔧 | Six facilities remapped: Splicing Chamber, Hatchery, Gene Vault, Harvest Array, Drive, Core. Remove Trait Archive and Alliance Hall. Core caps all others and shows its campaign milestone requirement. Next-tier effects in concrete numbers. | §7.2, §7.3 |
| **Calibration** | 🆕 | Post-tier-12 repeatable refinement, flat cost per +0.5%. Ships at launch though nobody reaches it for eighteen months — retrofitting a terminal sink into a live economy is worse. | Economy §8 |
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
| **Marks Shop** | 🆕 | Raid and Defense Marks inventory and spend. Escort slot expansion, transit boosts, route obfuscation, regen skips, cosmetics. Marks are never purchasable. | Raiding §11 |
| **Convoy Staging** | 🆕 | The thirty-minute assembly window: contributors, committed cargo and escorts, share preview, departure countdown. Officer-initiated, from controlled territory only. | Alliance §7.1 |
| **Joint Raid Assembly** | 🆕 | Up to three alliance attackers committing parties against one Convoy Rig, with each party's escort share and Marks split shown before commit. | Alliance §7.1 |

---

## 9. Onboarding and system

| Screen | Status | Notes | Bible |
|---|---|---|---|
| **Onboarding** | 🔧 | Rebuild against the eight beats. Beat 5 teaches counters by letting one work. Beat 8 ends on Lineage View. **The designed first loss** at session 2–3 needs authoring as a deliberate beat. | §9.2, §9.3 |
| **Mail / Notifications** | 🆕 | Raid alerts, alliance notices, event announcements, reward grants. | — |
| **Settings / Account / Support** | 🆕 | Required for store submission. Published support address reachable from here and from the store listing. | Moderation §6 |
| **Account Creation / Age Gate** | 🆕 **Required** | Single date field on first launch, before the cold open. The only pre-play screen the design allows. Under-13 accounts take a restricted mode: no free-text naming, no chat, browse-only Recipe Share. | Moderation §5 |
| **Player Profile** | 🆕 | Identity, Founders, Geneticist Tier, Codex completion, defence record. Feeds alliance applications and the Apex Cup leaderboard. | §6.2, §8.4 |
| **Report** | 🆕 **Required** | In-context on every UGC surface. Captures surrounding context automatically — a report with no context cannot be actioned. | Moderation §6 |
| **Block List** | 🆕 **Required** | Unilateral, no justification. Hides messages and recipes, blocks alliance applications. | Moderation §6 |
| **Escort Tutorial** | 🆕 | Day 15, before raid exposure begins. Assign escorts, dispatch, watch the exposure window. | §5.6, Midgame §4 |

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

**Three placements are non-negotiable and sit outside the tiers.** The **Age Gate** ships first because everything downstream of account creation depends on knowing whether the account is restricted. **Report** and **Block** ship with the first UGC surface, whichever that is — App Review rejects an app carrying user content without filtering, reporting, blocking and a published contact address, and the rejection arrives at the end of the process rather than the start.

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

1. ~~**Where the Trait Codex lives.**~~ **Both.** A browsable index in the Lab tab, plus a bottom sheet opened from any trait pip anywhere in the app. It is a component with an index, not a screen.
2. ~~**Map legibility on a small phone**~~ — **partly answered.** The map's three concentric bands allow zooming to eight to twelve regions at a time, which is readable. Whether heat-map, banners, Ark markers, routes and alerts can all stay live at that zoom is still the hardest layout problem in the app.
3. **Whether Region Detail warrants its own screen** or stays a card on the map. Adding lane count may push it over.
4. **Where Region Defence is entered from** — the Map, the Ark, or a notification. It is periodic and automatic, which argues for the notification, but it needs a browsable home too.
