# Handoff: Broodline

Creature-breeding tower defense with a relocatable base and 4X territory control. Mobile, portrait.

## Overview

Broodline joins three systems at one point of tension: **the creatures you breed are the only thing defending a base that has to keep moving.**

- **Core loop** — splice two creatures into a hybrid → place hybrids to hold tower-defense waves → relocate the base onto richer ground → spend the yield on more splices.
- **Meta layer** — the base ("Mobile Gene Ark") relocates across a world map to claim resource nodes that deplete over time. Alliances hold territory. Resource collection uses visible Collector units that rival players can intercept.
- **Depth engine** — traits, not species. Six founder species carry 12 base traits; hybrids inherit from both parents with a mutation chance, and lineage compounds across generations (G1 → G7+).

This bundle contains **20 designed screens**, all interactive, plus three reference documents (index, character bible, pitch deck).

## About the design files

The files in `screens/` are **design references authored in HTML** — prototypes that show intended look, copy, and behavior. They are **not production code to copy**.

Your task is to **recreate these designs in the target codebase's environment** using its established patterns and libraries. If no environment exists yet, choose an appropriate stack for a live-service mobile game (Unity, React Native, Flutter, or native) and implement the designs there.

Two notes on the file format so you can read them efficiently:

- Each `.dc.html` file is a self-contained page. Open it directly in a browser — it renders and is fully clickable.
- Inside, markup sits between `<x-dc>` tags and the interaction logic is a `class Component extends DCLogic { … }` block with a `renderVals()` method that returns the values the markup binds to via `{{ name }}`. **Read `renderVals()` first** — it is the state model for that screen, and it is where all the derived logic lives (odds calculations, gating, label switching). `<sc-if>` is conditional render, `<sc-for>` is a list.
- `support.js` is the runtime that makes these files render. It is bundled only so the prototypes open offline. **Do not port it.**

## Fidelity

**High-fidelity.** Final colors, typography, spacing, copy, and interaction states. Recreate pixel-accurately using the codebase's own component library.

Two exceptions, both intentional:

1. **Creature and raider art is placeholder geometric silhouettes.** Every slot is sized and positioned for final art and can be swapped without layout changes. `screens/Character Bible.dc.html` is the art spec — see "Assets" below.
2. **All data is hardcoded sample data.** Numbers, names, and timers are illustrative. The economy is unbalanced by design; tuning is a separate workstream.

## Design tokens

### Color

| Token | Hex | Use |
|---|---|---|
| `ink` | `#3f3a52` | Primary text |
| `ink-deep` | `#2e2a3d` | Deck/dark surfaces |
| `paper` | `#f7f4fb` | Screen background |
| `surface` | `#ffffff` | Cards |
| `surface-sunk` | `#f8f6fc` | Inset rows, unselected options |
| `violet` | `#7a6ac0` | Primary brand, CTAs |
| `violet-deep` | `#6f5fbb` | CTA gradient end |
| `violet-shadow` | `#5b4d9e` | CTA drop edge |
| `violet-tint` | `#f1ecfa` | Secondary button, violet panels |
| `violet-text` | `#6b5fa8` | Violet-on-light text |
| `teal` | `#6ba7c0` | Vetch species, info |
| `teal-tint` | `#e7f3f9` | Teal panels |
| `green` | `#7cc492` | Loam species, success |
| `green-deep` | `#5aa87a` | Success CTA |
| `green-tint` | `#e8f5ec` | Success panels |
| `green-text` | `#43704f` | Success text |
| `amber` | `#e8b34a` | Apex/gold, warnings |
| `amber-bright` | `#ffb703` | Apex vein core |
| `amber-tint` | `#fdf1d8` / `#fdf6e6` | Warning panels |
| `amber-text` | `#7a5b12` / `#8e6d15` | Warning text |
| `coral` | `#e5867a` | Ember species, danger |
| `coral-deep` | `#d4776a` | Danger CTA |
| `coral-tint` | `#fbeae7` | Danger panels |
| `coral-text` | `#b3564a` / `#96463c` | Danger text |
| `slate` | `#c6cede` | Pale species, neutral — **moved from `#a9b0c4`**, Phase 8 Task 6 |
| `mute` | `#a29bb5` | Secondary text, inactive nav |
| `mute-soft` | `#b3adc2` | Tertiary text, placeholders |
| `divider` | `#f4f1fa` | Hairline dividers |
| `hairline` | `#ece7f6` | Unselected option outline |
| `ring` | `#8878cf` | Selected option 2px ring |

Screen backdrop gradient (outside the phone): `linear-gradient(160deg, #e6e0f5, #d3e7ef)`.

### Typography

- **Display / numerals:** `Baloo 2`, weights 600–800. Titles, all large numbers, CTA labels.
- **Body / UI:** `Nunito`, weights 400–800. Everything else.
- Both from Google Fonts. Replace with the codebase's equivalents if it has a licensed pairing; keep the rounded-display + humanist-sans contrast.

| Role | Size | Weight | Notes |
|---|---|---|---|
| Screen title | 20–21px | 700 | Baloo 2 |
| Hero numeral | 24–31px | 700 | Baloo 2 |
| Section title | 19–26px | 700 | Baloo 2 |
| Card title | 12.5–13px | 800 | Nunito |
| Body | 11.5–12px | 700 | Nunito, line-height 1.4–1.45 |
| Secondary | 10.5–11px | 700 | Nunito, `mute` |
| Micro label | 9–10px | 800 | Nunito, `letter-spacing: .1em`, uppercase, `mute` |
| CTA | 16–17px | 700 | Baloo 2 |
| Tab label | 11.5px | 800 | Nunito |
| Nav label | 10px | 700/800 | Nunito |

**Deck only** (`Broodline Pitch.dc.html`, 1920×1080): title 76px, subtitle 46px, body 34px, small 28px, micro 24px; padding 100px top / 80px bottom / 100px sides. Defined as CSS custom properties in that file's `:root`.

### Spacing, radius, elevation

- Screen gutter: **12px**. Card padding: **12–16px** vertical, **14–18px** horizontal. Header padding: `8px 14px 10px`.
- Radius: pill `999px` · chip `5–7px` · icon tile `9–14px` · row `12–16px` · card `18–22px` · hero card `22–26px` · phone frame `34px`.
- Card shadow: `0 2px 8px rgba(63,58,82,.06)`. Raised card: `0 4px 16px rgba(63,58,82,.09)`. Phone frame: `0 18px 50px rgba(63,58,82,.28)`.
- **Selected option:** `inset 0 0 0 2px #8878cf` + white fill. **Unselected:** `inset 0 0 0 1px #ece7f6` + `#f7f5fb` fill.
- **Primary CTA:** `linear-gradient(180deg, #8878cf, #6f5fbb)`, `box-shadow: 0 3px 0 #5b4d9e, 0 6px 14px rgba(111,95,187,.28)`, radius 15px, padding 14px 0. Active state: `translateY(2px)` and shadow collapses to `0 1px 0 #5b4d9e` — this "physical button press" is used on every primary CTA and should be preserved.

### Layout frame

All screens are authored at **430 × 932** (iPhone 14 Pro logical size), portrait only, as a fixed-height column:

```
status bar (fixed)  →  header (fixed)  →  [tab bar]  →  content (flex: 1, scrolls)  →  CTA row  →  footer note  →  bottom nav (fixed)
```

Content is the only flexible region. Treat 430×932 as the reference and scale fluidly; nothing depends on that exact height.

## Navigation model

**Bottom nav — 5 tabs, present on hub screens only:** Map · Ark · Splice · Lab · Allies. Active tab: `#f1ecfa` pill, `#6b5fa8` icon and label at weight 800. Inactive: `#b3adc2` icon, `#a29bb5` label at weight 700. Icons are 19px, 1.9 stroke width, no fill.

Tab destinations: Map → World Map · Ark → Gene Ark · Splice → Splice Chamber · Lab → Gene Lab · Allies → Alliance Hub.

**Pushed sub-screens have no bottom nav** and carry a back chevron in a 34px rounded-square button at the header's left edge:

| Sub-screen | Pushed from |
|---|---|
| Relocate Ark | World Map |
| Apex Alert | World Map (or push notification) |
| Gene Vault | Gene Lab |
| Splice Reveal | Splice Chamber (after splice) |
| Hybrid Growth | Creature Roster |
| Creature Roster | Splice Chamber |
| Wave Defense | Gene Ark |
| Wave Defeat | Wave Defense (on loss) |
| Collector Intercept | World Map (or push notification) |
| Alliance Rally | Alliance Hub |
| World Chat | Alliance Hub |
| Breeder Profile | any header avatar |

## Screens

Grouped by system. For each: purpose, layout, and the state model to implement. Read the corresponding file's `renderVals()` for exact derivation logic.

---

### 1. World Map — `Splice World Map v2.dc.html`

**Purpose.** Survey regions, read yield, select a target, relocate or dispatch a Collector.

**Layout.** Status bar → three currency chips in a row (Splice Charges `4/5`, Gene Shards `1,240`, Breeder Tier `6`) → map viewport (rounded 22px, ~400px tall) → Apex event banner → region detail card → bottom nav.

**Map.** Eight irregular polygon regions (R-01…R-08) drawn in SVG, filled by yield state, separated by 2.5px white strokes. Water background `#dceff6`-ish; land polygons per state:

| State | Fill | Meaning |
|---|---|---|
| Rich | `#8ed99b` | 3× base yield |
| Common | `#cadaa6` | 1× base |
| Drained | `#e8ddc6` | depleted |
| Contested | `#f2ab9f` | yield splits across all Arks present |
| Apex | `#f6c559` | 8× base, burns out in ~51h |

Overlays: your Ark as a violet hex pin with a white "YOUR ARK" label; ally Arks as green dots with alliance name; hostile Arks as coral dots; Collector routes as animated dashed violet paths (`stroke-dasharray: 8 6`, offset animating to `-60` over 2s linear infinite); Apex vein as concentric pulsing amber rings; intercept-risk markers as small coral warning badges. Selected region gets a dashed violet outline. Zoom +/− buttons top-right, yield legend bottom-left.

**Region detail card.** Region code + tier, name (Baloo 2, ~24px), yield multiplier, richness percentage bar, status line, then a 3-cell row (Control / Travel / Arks) and two CTAs: **Relocate Ark** (primary) and **Send Collector** (secondary).

**State.**
```
selectedRegion: 'a'…'h'      // tap a polygon to select
regions: { code, name, tier, yieldMult, richness%, control, travelTime, arksPresent, status }
```
Selecting a region swaps the entire detail card and the map's selection outline. Region data drives everything; no other state.

---

### 2. Gene Ark — `Gene Ark.dc.html`

**Purpose.** The base itself. Shows the pack-up / travel / unpack sequence and its costs.

**Layout.** Header → large Ark illustration stage → phase state → facility summary → CTA.

**Ark visual.** Isometric hex-prism base: top face `#e9e3f7` with `#9c8ccf` 3px stroke, left face `#b6a9dc`, right face `#cdc2e8`, a lighter hatch panel on top, and an antenna with a glowing tip. Idle animation: gentle vertical bob, `translateY(0 → -5px)`, 4s ease-in-out infinite.

**Pack-up concept.** Three phases the developer should animate as one continuous transform, not three separate assets:
1. **Deployed** — full footprint, antenna raised, facilities visible.
2. **Packing** — facilities fold inward toward the center, footprint contracts, antenna retracts. Production stops; in-progress harvest is forfeited.
3. **Mobile** — compact travelling silhouette, treads/skirt visible, moves along the map route. **Cannot fight back in this state.**

Unpack is the reverse. The satisfying beat is the footprint expanding and facilities unfolding on arrival.

**State.** `phase: 'deployed' | 'packing' | 'mobile' | 'unpacking'`, `currentRegion`, `harvestForfeited`.

---

### 3. Relocate Ark — `Relocate Ark.dc.html`

**Purpose.** Commit to a crossing. The screen's job is to make transit vulnerability legible.

**Layout.** Back chevron + route header + Apex countdown chip → route map (190px) → three route options → cost panel → CTA row (**Roll out** / **Stay**).

**Three routes** — each a real trade, none strictly better:

| Route | Time | Intercept | Cost | Tone |
|---|---|---|---|---|
| Direct crossing | 3h 05m | 62% | — | coral CTA, danger note |
| Alliance corridor | 4h 20m | 14% | — | violet CTA, success note |
| Night move | 5h 10m | 0% | 120 shards | violet CTA, neutral note |

Selecting a route redraws the SVG route path, moves the threat marker (opacity `1` / `0.35` / `0`), and swaps the risk figure, note background, note stroke color, note copy, CTA gradient, and footer text together. All of this is a single lookup table — see the `ROUTES` const.

**Cost panel — always shown.** `−340 forfeited` (harvest in progress), `no wave cover` (defenders stowed), `unaffected` (lab timers keep running).

**State.** `route: 'direct'|'corridor'|'night'`, `committed: boolean`.

---

### 4. Gene Lab — `Gene Lab.dc.html`

**Purpose.** Isometric base-building view with six upgradeable facilities.

**Facilities.** Splicing Chamber, Gene Vault, Trait Archive, Defense Wall, Alliance Hall, Hatchery. Each has a level, an upgrade cost, an upgrade timer, and a tap target that opens its detail. Drawn as isometric hex-prism pads on an isometric ground grid, tinted per facility.

**State.** `facilities: [{ id, name, level, upgrading, timeRemaining, cost }]`, `selectedFacility`, `buildersAvailable`.

---

### 5. Gene Vault — `Gene Vault.dc.html`

**Purpose.** Trait fragment inventory, tier fusing, and the archive of retired creatures.

**Three tabs: Fragments · Fuse · Archive.**

**Capacity is the pressure valve.** Header pill and bar show `41 / 45`. Below 88% the bar is violet and reads HEALTHY; at or above 88% it turns coral, reads NEARLY FULL, and the note changes to warn that new fragments from waves are discarded. This threshold is the mechanic — implement it as a derived value, not a flag.

**Fragments tab.** One row per trait: icon tile, trait name, tier chip, a subtitle stating **what the trait beats** (not what it is), the held count, and a progress bar on an n/15 scale. Seven rows totalling 41: Carapace III ×14, Pierce II ×9, Cinder II ×7, Sprint II ×6, Burrow I ×3, Chill I ×2, Reach ×0 (locked row, dimmed to 0.72 opacity).

Scarcity is communicated through color: Chill at 2 is coral with "Only answer to Courser"; Reach at 0 is coral with "Drift is unanswerable without it".

**Fuse tab.** Three fragments → one of the next tier. Shows a 3-into-1 visual, the resulting tier, and a "ready to fuse" list where each entry is `held − 3` spare (Pierce 9 → 6 spare; Carapace 14 → 11 spare; Chill 2 → "needs 1 more"). Fusing is permanent and instant — no timer, no builder.

**Archive tab.** Retired creatures with the trait each left behind. Key reassurance for a breeding game: **retiring banks the traits and frees a roster slot — nothing bred is ever lost, only the animal.** Pedigree records survive retirement, so a G7 line still counts as G7.

**State.** `tab`, `fragments: [{ trait, tier, held, counters }]`, `capacity: { used, max }`, `fused`.

---

### 6. Splice Chamber — `Splice Chamber.dc.html`

**Purpose.** The core action. Pair two creatures, preview inherited traits, spend a charge.

**Layout.** Header with charge counter → two parent slots with a join affordance between them → trait forecast → mutation chance → **Splice** CTA.

**Forecast is the design point.** Show which traits are *likely* to carry before the charge is spent, so a pairing is a decision rather than a gamble. Traits inherit from both parents; tiers stack.

**State.** `parentA`, `parentB`, `forecast: [{ trait, tier, probability }]`, `mutationChance` (1 in 9), `chargesRemaining` (cap 5, timer regen).

---

### 7. Splice Reveal — `Splice Reveal.dc.html`

**Purpose.** The payoff moment. Mutation roll, trait cards, name the hybrid.

Reveal sequence: anticipation → trait cards resolving one at a time → mutation flourish if rolled → naming input. Mutation should feel meaningfully rarer than the base reveal — a distinct amber treatment, not just a bigger version of the normal one.

**State.** `phase: 'incubating'|'revealing'|'named'`, `result: { traits, mutation, generation }`, `name`.

---

### 8. Hybrid Growth — `Hybrid Growth.dc.html`

**Purpose.** Four growth stages from runt to apex, with feeding and lineage.

**Growth rule (must hold in art and code).** Growth **exaggerates, never redesigns** — the same creature with mass moved outward: bigger head, heavier limbs, same profile. Players must keep recognizing their own animal. Implement as a scale/proportion curve over one rig, not four separate assets.

**State.** `stage: 1..4`, `feedProgress`, `lineage: [ancestors]`.

---

### 9. Creature Roster — `Creature Roster.dc.html`

**Purpose.** The collection, filterable by generation and trait.

Grid of creature cards with generation badge, trait chips, and role. Filters across the top. **Active nav tab is Splice.**

**State.** `filter: { generation, trait, role }`, `creatures: []`, `selected`, `rosterCap` (18 of 20).

---

### 10. Wave Defense — `Wave Defense.dc.html`

**Purpose.** The actual combat. ~90 second sessions.

**Rule.** Waves walk a **fixed path** to the Ark. Defenders go in **pockets beside the lane, never on the road** — placement is about coverage, not mazing. This is deliberate: it keeps combat legible on a phone and keeps composition, not layout puzzling, the deciding factor.

**Layout.** Wave counter and Ark health → lane playfield → defender tray → wave controls. **Active nav tab is Ark.**

**State.** `wave`, `arkHealth`, `placedDefenders: [{ creature, slot }]`, `availableSlots`, `enemies: [{ type, position, hp }]`, `phase: 'placing'|'running'|'won'|'lost'`.

---

### 11. Wave Defeat — `Wave Defeat.dc.html`

**Purpose.** Diagnose the loss, suggest a fix, offer a free retry.

Must state **which raider broke through and which trait would have answered it** — this is the screen that teaches the counter system. Free retry; no paywall on failure.

**State.** `failedWave`, `breachedBy`, `suggestedCounter`, `retriesUsed`.

---

### 12. Collector Intercept — `Collector Intercept.dc.html`

**Purpose.** A rival is intercepting your convoy. Escort, reroute, or accept the loss.

Time-boxed decision with a visible countdown. Allies can ride escort — this is the moment alliances feel useful.

**State.** `convoy: { cargo, route, eta }`, `raiders`, `timeRemaining`, `choice`.

---

### 13. Apex Alert — `Apex Alert.dc.html`

**Purpose.** An Apex vein is surfacing. Sell the scout report; frame the rush.

**Layout.** Seismic hero (expanding ripples at the surfacing point, animated seismograph trace confined to the card's right half, shaking "SEISMIC ACTIVITY" chip, countdown) → free intel row (8× yield, 3h05 travel, Sable-held) → **scout report card** → race timeline → alliance rush stack → CTAs.

**Scout report — the micro-spend.** Four intel rows render **blurred behind `filter: blur(4px)`** with a 25-shard price. On purchase they resolve to real values (surfacing point, rival ETA, trait odds, escort strength), the countdown gains 5 minutes, and the note becomes a tactical warning. Locked rows must read `scout to see` — never a placeholder that leaks the hidden value.

Free alerts always fire 30 minutes ahead; the report buys **detail and a 5-minute head start**, not access.

**State.** `scouted: boolean` → drives ~15 derived display values. See `renderVals()`.

---

### 14. Alliance Hub — `Alliance Hub.dc.html`

**Purpose.** Territory control, roster, and build-help.

**Three tabs: Territory · Members · Help.** CTA changes per tab (Mark R-06 for rush / Invite a breeder / Help all · 3).

- **Territory** — regional control map (Crux lilac `#a8ade4` / contested mint `#8ed99b` / Sable coral `#f2ab9f`) with member Ark pins, a standing row (4 regions, +34% co-op yield, 1 under threat), an Apex rush callout, and a per-region list showing holder, who's harvesting, yield delta and travel time.
- **Members** — roster ranked by weekly contribution with tier, region, last-seen.
- **Help** — build-help requests that cut timers per tap, a convoy escort request with a 40s window, and a weekly help meter toward the alliance chest.

**State.** `tab`, `alliance: { name, rank, memberCount, cap: 40 }`, `territories: []`, `members: []`, `helpRequests: []`, `helpGiven` / `helpReceived`.

---

### 15. Alliance Rally — `Alliance Rally.dc.html`

**Purpose.** Commit defenders to a timed Apex assault. **This screen carries the design thesis.**

**Layout.** Back + rally header + 04:12 countdown → target map with three inbound ally routes and the Apex objective → projected outcome → your unit picks → committed roster → CTA.

**The mechanic that matters.** Rally power alone does **not** decide the outcome. Garrison is 8,400 with a Breaker wall. If no **Pierce** unit is in the rally, the note reads *"Total power is enough on paper, but the Breaker wall will not break"* — the odds bar can show a win while the outcome copy says it fails. Composition beats raw numbers; that is what sends players back to breed rather than to the store.

**Honest cost.** Units sent on a rally cannot defend your own Ark until they return (6h, win or lose).

**State.** `sent: string[]` (unit ids), `joined: boolean`. Derived: `yours = Σ unit power`, `total = allies + (joined ? yours : 0)`, `win = total > 8400`, and a separate `hasPierce` check that gates the outcome note independently of `win`.

---

### 16. World Chat — `World Chat.dc.html`

**Purpose.** Alliance rally coordination, server trade, direct messages.

**Three channels: Alliance · World · Direct 2.**

Messages carry **attached game objects**, not just text: an Ark-in-transit chip, a live escort request with a 38s timer and a Ride button, an alliance invite card, a shareable splice recipe card with a Copy button. Own messages are violet-filled and right-aligned with a `14px 14px 4px 14px` bubble; others are white, left-aligned, `14px 14px 14px 4px`.

Quick-reply chips ("On my way", "Need escort", "Help me build") load the composer. **The composer and quick chips hide entirely on the Direct list** — there is no open thread to send to. Composer placeholder is channel-specific.

**State.** `channel`, `draft`, `sent`, `messages: [{ author, role, time, body, attachment }]`.

---

### 17. Onboarding — `Onboarding.dc.html`

**Purpose.** Five-step first-run flow.

| Step | Title | Teaches |
|---|---|---|
| 1 | Your Gene Ark | Relocation is the plan, not a setback |
| 2 | Splice two, get one | The core action (pulsing tap target on the join icon) |
| 3 | Your hybrid holds the lane | Defenders go beside the road, never on it |
| 4 | Richer ground is worth the move | Depletion and Apex windows |
| 5 | Nobody holds an Apex alone | Why alliances exist |

Each step swaps art, copy, and **note tone**: violet tip → green confirmation → amber warning. Progress pips at the top; a persistent horizontal 5-pip checklist (Ark · Splice · Defend · Claim · Ally) below the copy — completed pips green-ticked, current violet, upcoming grey. Rewards appear on steps 3 and 5. Back chevron dims on step 1.

**State.** `step: 0..4`. All content from a `STEPS[]` table.

---

### 18. Gene Lab Event — `Gene Lab Event.dc.html`

**Purpose.** Weekly server-wide event.

**Three tabs: Your tasks · Leaderboard · Event offer.**

- Server goal 1.48M / 2M splices, 74%, with claimed milestone markers at 40% and 70% and the next gold threshold at 92%. Milestone labels must sit directly under their ticks.
- **Tasks** — five with live progress; Claim buttons flip to Claimed.
- **Leaderboard** — alliance standings, your row highlighted at 4th, −460 behind.
- **Event offer** — Gene Lab Kit at $6.99 (62% off) with a points-math card showing how it moves you 4th → top three, plus a per-item cost breakdown justifying the $18.40 comparison.

**State.** `tab`, `claimed: string[]`, `serverProgress`, `tasks: []`, `standings: []`.

---

### 19. Store — `Store.dc.html`

**Purpose.** Monetization surface. **Everything sold is a speed-up — no trait, creature, or counter is purchase-only.**

**Three tabs: Custom chest · Packs · Breeder Pass.** An Ashfall-week countdown strip (05:12:44) sits above the tabs.

- **Custom Chest** (highest-converting mechanic) — pick any **3 of 6** rewards. Slots toggle; the counter and CTA track picks ("Pick 1 more" → "Buy chest · $4.99"); price tier is selectable ($1.99 / $4.99 / $9.99). A live value strip computes `You pay $X instead of $Y bought separately · SAVE Z%` from the actual picks, so personalization has a visible payoff. Reward values used: charges 4.2, shards 4.8, xp 2.6, pull 5.4, skin 3.9, speed 3.2.
- **Packs** — a **free daily gift card leads the tab** (generosity first is what makes the paid tiers feel fair), then Starter Splice $0.99 with a first-buy 2× banner and shimmer sweep, then four ladder packs at $4.99 / $9.99 / $19.99 / $14.99 with value badges (+35%, +80%, BEST VALUE, 48H ONLY) and a MOST POPULAR ribbon on Lab Expansion. Closes with a "what a pack buys you right now" card tying spend to the player's actual current blockers.
- **Breeder Pass** — Season 3, tier 14/40, free vs paid track side by side, next-tiers strip, and the fairness statement that every paid-track trait is obtainable free, just slower.

**State.** `tab`, `chosen: string[]` (max 3), `tier`, derived value/savings math.

---

### 20. Breeder Profile — `Breeder Profile.dc.html`

**Purpose.** Progression identity.

**Three tabs: Tier ladder · Record · Earn XP.**

- **Tier ladder** — 12 tiers; unlocked perks checked green, tier 6 highlighted as current, locked tiers show XP costs, tier 12 (Geneticist title, permanent +15% shards) in gold. Footer states every tier perk is **convenience or cosmetic, never raw combat power.**
- **Record** — 412 hybrids, 19 mutations, longest line G7, 218 waves; territory record; earned titles; recent splices.
- **Earn XP** — free daily cap 320/500, login streak (D3→D30 calendar), event tasks, rewarded video, and shard→XP conversion framed as a faster route, not the only route.

**State.** `tab`, `tier`, `xp`, `dailyXpEarned` / `dailyXpCap`, `streak`, `titles`.

---

## Interactions & behavior

**Universal patterns:**

- **Primary CTA press** — `translateY(2px)`, shadow collapses from `0 3px 0` to `0 1px 0`. On every primary button.
- **Option select** — white fill + `inset 0 0 0 2px #8878cf`. Unselected: `#f7f5fb` + `inset 0 0 0 1px #ece7f6`.
- **Tab switch** — active pill white with `#6b5fa8` weight-800 label; inactive transparent with `#a29bb5` weight-700. Tab bars sit in an `#ece7f6` track with 4px padding and 11px inner radius.
- **Locked/paid content** — `filter: blur(4px)` over real layout with a price chip, so the player sees the shape of what they'd buy.

**Named animations** (all CSS keyframes in the prototypes):

| Name | Definition | Used for |
|---|---|---|
| `glow` | opacity `.4 → 1 → .4`, 1.2–3.6s ease-in-out infinite | Apex cores, live dots, pulsing highlights |
| `dash` | `stroke-dashoffset: 0 → -60`, 2s linear infinite | Collector routes, rally paths |
| `bob` | `translateY(0 → -5px → 0)`, 4–4.4s ease-in-out infinite | Ark idle, hero creature |
| `orbit` | `rotate(0 → 360deg)`, 20–22s linear infinite | Dashed decorative rings |
| `ripple` | `scale(.6 → 2.1)` + opacity `.7 → 0`, 2.2s ease-out infinite | Apex surfacing, tap targets |
| `tapPing` | `scale(.7 → 1.7)` + opacity `.6 → 0`, 1.7s ease-out infinite | Onboarding tap prompts |
| `quake` | `translateX(0 → -1.5 → 1.5 → 0)`, .5s ease-in-out infinite | Seismic alert chip |
| `sweep` | `stroke-dashoffset → -220`, 3.4s linear infinite | Seismograph trace |
| `riseIn` | `translateY(8–12px)` + opacity `0 → 1`, .3–.4s ease-out | New message, step change |
| `shine` | `translateX(-120% → 320%) skewX(-18deg)`, 4.2s ease-in-out infinite | Store hero sweep |
| `pop` | `scale(1 → 1.06 → 1)`, 1.8s ease-in-out infinite | Urgency icons |
| `fill` | `width: 0 → target`, 1.2s ease-out once | Progress bar entry |

Stagger long infinite animations with `animation-delay` where two of the same run together (e.g. Apex ripples offset by ~0.9s).

## Game systems reference

### Six founder species

Each is distinguishable in **pure silhouette at 40px**. Shape carries role before any card is read.

| Species | Role | Traits | Color | Silhouette |
|---|---|---|---|---|
| **Vetch** | Wall | Carapace, Taunt | `#6ba7c0` | Low dome, four stubby legs, no neck |
| **Ember** | Splash | Cinder, Splash | `#e5867a` | Tall narrow torso, head crest, two legs |
| **Skitter** | Swarm | Sprint, Litter | `#e8b34a` | Small body, six long thin legs, tiny head |
| **Hollow** | Sniper | Reach, Pierce | `#7a6ac0` | Tiny body, stilt legs, long forward neck |
| **Loam** | Support | Regrow, Burrow | `#7cc492` | Segmented ground-hugger, blunt snout, no legs |
| **Pale** | Control | Screen, Chill | `#c6cede` | Broad wing arc, small hanging body |

> **Pale moved, 2026-09-17:** `#a9b0c4` → `#c6cede` (also the `slate` token
> above), in Phase 8 Task 6 — the palette's worst colour-blind pair, Vetch/Pale,
> went from ΔE 7.2 to 17.3. `Tokens.uss` is the live value.
> **Two notes for whoever draws these:** at `#c6cede` a Pale reads **1.47 : 1**
> against the card's `#f8f6fc` silhouette slot and is effectively invisible as a
> flat fill, so it needs value, outline or a darker slot (bible §10.4); and
> Hollow `#7a6ac0` and Skitter `#e8b34a` are *exactly* the `violet` and `amber`
> UI tokens, which is intended and recorded in
> `implementation/results/species-collision.md`.

### Eight raiders and their counters

| Raider | Threat | Countered by |
|---|---|---|
| **Skirmisher** | Fast cheap fodder, arrives in eights | Splash · Ember |
| **Breaker** | Slow armored siege, ignores taunts | Pierce · Hollow |
| **Lash** | Reaches past the front line to hit support and Collectors | Taunt · Vetch |
| **Courser** | Sprints the lane ignoring taunts; damage can't kill it in time | Chill · Pale |
| **Brood** | Splits into three on death, then splits again | Cinder · Ember |
| **Drift** | Flier, crosses walls entirely | Reach · Hollow |
| **Bulwark** | Front shield, only breaks under rapid repeated hits | Sprint · Skitter |
| **Delver** | Burrows under the front line, surfaces mid-formation | Burrow · Loam |

**Every founder counters at least one raider**, so no founder is an obvious skip. Carapace, Litter, Regrow and Screen counter nothing by design — they are survivability and economy traits.

**Wave design rule.** Never send two raiders answered by the same trait in one wave, or a single perfect defender clears it and the breeding pressure evaporates. A legal wave: Courser + Drift + Brood requires Chill, Reach and Cinder simultaneously — three different founders in the lineage.

### Hero creature

**Cinderplate** (G2, Vetch × Ember) — the mascot. Vetch's teal dome and legs **unchanged**, with Ember's coral crest row bolted on. Reads at 40px as "teal dome plus three coral spikes"; no other creature owns that pair. It's the mascot because it's the first hybrid every player makes in the tutorial, so the store icon is an animal they already own.

### Resource nodes

| Type | Yield | Decay |
|---|---|---|
| Common Vein | 1× | none |
| Rich Deposit | 3× | drains in ~4 days |
| Apex Vein | 8× | burns out in ~51h |

Contested regions split yield across every Ark present. Allied Arks harvest the same region at **full rate** — clustering is a mechanical advantage.

## Art direction rules (three rules, no exceptions)

From `screens/Character Bible.dc.html` — hand these to the illustrator verbatim:

1. **Silhouette carries the role.** Wide and low is a wall. Tall and narrow is ranged. Spindly is fast. Players should name the role before reading the card.
2. **Hybrids add parts, never average them.** A hybrid keeps one parent's body and bolts on the other's signature part. Muddy in-between blends kill both the read and the collecting.
3. **Growth exaggerates, never redesigns.** Runt to apex is the same creature with mass moved outward — bigger head, heavier limbs, same profile.

Raiders are **angular, dark, and never cute** (`#4b455f` bodies, `#6a6383` highlights, `#d4776a` hot accents), so threat never reads as collectible.

## Assets

- **Creature and raider art: placeholder.** Geometric SVG silhouettes throughout. Every slot is sized and positioned for final art — swapping in rendered assets requires no layout change. Use the Character Bible as the commission spec.
- **Icons: inline SVG, no icon font.** 19px at 1.9 stroke for nav, 11–18px at 2.2–2.6 stroke elsewhere; `fill="none"`, `stroke-linecap="round"`, `stroke-linejoin="round"`. Replace with the codebase's icon set — match the weight, not the exact paths.
- **Fonts:** Baloo 2 + Nunito, Google Fonts.
- **No emoji, no raster art, no third-party imagery.** Nothing in this bundle has licensing constraints.
- **`screens/shots/*.png`** — 6 captures at 2× used by the pitch deck. Reference only.

## Files

**Core loop:** `Splice Chamber` · `Splice Reveal` · `Hybrid Growth` · `Creature Roster` · `Wave Defense` · `Wave Defeat`

**Meta layer:** `Splice World Map v2` · `Gene Ark` · `Relocate Ark` · `Gene Lab` · `Gene Vault` · `Collector Intercept` · `Apex Alert` · `Alliance Hub` · `Alliance Rally` · `World Chat`

**Entry & live ops:** `Onboarding` · `Gene Lab Event` · `Store` · `Breeder Profile`

**Reference (not app screens):**
- `Broodline Index.dc.html` — clickable index of all 20 screens. **Start here.**
- `Character Bible.dc.html` — full cast, shape-language rules, counter matrix.
- `Broodline Pitch.dc.html` — 15-slide stakeholder deck (arrow keys to navigate).

**Runtime (do not port):** `support.js`, `deck-stage.js`, `image-slot.js`.

## Open questions for the team

Unresolved by design — flagged rather than guessed:

1. **Alliance size cap is 40.** Possibly too large for coordinated Apex rallies; 25 may produce tighter groups.
2. **Convoy interception** may need to be opt-in for the first few days, so new players aren't farmed before they understand escorts.
3. **Season length: 6 or 8 weeks.** Six suits the one-new-founder cadence but may strain live-ops.
4. **Economy is untuned.** Charge regeneration rate, fragment drop rates, and Vault capacity all need a balance pass against real session data.
