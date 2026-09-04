# Broodline — Screen Inventory
*Every screen implied by the five system specs, what each needs, and what's missing*

---

## 1. How to Use This

This is the working document to keep open while designing. It pulls every screen implied across the monetization, node, raiding, genetics, combat, and alliance specs into one place, states the data each needs, and — more usefully — names the screens that are assumed somewhere but defined nowhere.

**Total: 34 screens.** That is a lot for a launch scope. §5 orders them by priority so the first-session experience gets built first.

---

## 2. Navigation Architecture

Five-tab bottom navigation, with a persistent top bar.

| Tab | Contains |
|---|---|
| **Ark** | Home. Gene Vault, campaign entry, regeneration timers, daily claims |
| **Map** | World map, region detail, relocation, collectors, raids |
| **Lab** | Splice, roster, lineage, trait codex |
| **Alliance** | Hub, stakes, garrison, tech, chat |
| **Store** | Packs, custom chest, pass, roulette |

**Persistent top bar:** Splice Charges (with regen timer), Gene Shards, Geneticist Tier progress, mail badge, active event badge.

The Map tab is the daily-decision hub the node spec describes, and the Lab tab is where players spend the most total time. Those two carry the app.

---

## 3. Screen Inventory

### Ark Hub

| Screen | Data needed | Source |
|---|---|---|
| **Ark Home** | Current region, harvest rate, active timers, daily claim state, campaign progress | Node, Monetization |
| **Gene Vault** | Vault tier, upgrade costs, effects on yield/roster cap/charge regen | **Undefined — see §4** |
| **Campaign Select** | Wave list, completion state, replay rewards, enemy armor preview | Combat |
| **Regeneration Tracker** | Creatures in regen, timers, skip cost | Combat |
| **Daily Login** | Streak day, escalating rewards, next-tier preview | Monetization |

### Map Hub

| Screen | Data needed | Source |
|---|---|---|
| **World Map** | Heat-map overlay (green/red/gold pulse), Ark position, alliance territory, active alerts | Node |
| **Region Detail** | Richness, controller, travel time, node types present, terrain preview, live alerts | Node, Combat |
| **Relocation** | Route, transit time, risk, destination terrain | Node |
| **Collector Dispatch** | Collector class, cargo capacity, escort slots, route options | Raiding |
| **Route Plotter** | Direct vs. allied-territory routes, interception risk per segment | Raiding |
| **Convoy Status** | In-transit collectors, ETA, exposure phase | Raiding |
| **Raid Alert** | 90-second countdown, attacker info, defend/ignore, rally broadcast | Raiding |
| **Raid Party Select** | Three-creature selection, target route, cooldown state | Raiding |
| **Raid Result** | Cargo outcome, Marks earned, replay entry | Raiding, Combat |
| **Scout Report** | Apex Vein early warning, purchase state | Node, Monetization |

### Lab Hub

| Screen | Data needed | Source |
|---|---|---|
| **Splice** | Two parent slots, chassis toggle, 8-trait pool, 2 locked picks, **probability table**, mutation %, charge cost, child generation, destruction warning | Genetics |
| **Splice Confirm** | Standard confirm; **named dialog for Founders** | Genetics |
| **Roster** | Filter/sort by chassis, generation, trait, affinity; regen state; garrison/escort state | Genetics |
| **Creature Detail** | Chassis, four traits with tiers, generation, affinity, combat stats, current assignment | Genetics, Combat |
| **Lineage View** | Five-generation family tree, trait contributions, mutation markers, Founder roots | Genetics |
| **Founder Naming** | Name entry, first five creatures only | Genetics |
| **Trait Codex** | All discovered traits by category and tier, Instinct behavior descriptions | **Undefined — see §4** |
| **Splice Result** | Child reveal, traits gained, mutation callout | Genetics |

### Combat

| Screen | Data needed | Source |
|---|---|---|
| **Deployment** | Region terrain, emplacement tiles, 5-creature selection, enemy armor mix, terrain modifiers | Combat |
| **Live Combat** | Lanes, creature states, Rally control + cooldown, wave progress, Ark health | Combat |
| **Post-Wave** | Rewards, creature outcomes, regen assignments, retry | Combat |
| **Replay Viewer** | Full simulation playback, speed control, per-creature behavior visibility | Combat |

### Alliance Hub

| Screen | Data needed | Source |
|---|---|---|
| **Alliance Home** | Member list, roles, activity, treasury, active stakes | Alliance |
| **Stake Management** | Five stake slots, Hold accrual, contest state, weekly tick countdown | Alliance |
| **Garrison Assignment** | Contributed creatures, capacity, **contributor visibility** | Alliance |
| **Stake Assault** | Target selection, assault rate limit, entry into combat engine | Alliance, Combat |
| **Alliance Tech** | Three branches, contribution state, unlocked perks | Alliance |
| **Alliance Chat** | Messages, rally broadcasts, raid alerts | Alliance, Raiding |
| **Join / Apply** | Alliance browse, application state | Alliance |

### Store & Events

| Screen | Data needed | Source |
|---|---|---|
| **Store Home** | Fixed packs, first-purchase 2x state, featured event offer | Monetization |
| **Custom Chest** | Six reward slots, pick-three, three price tiers | Monetization |
| **Season Pass** | Free and paid tracks, progress, reward preview | Monetization |
| **Geneticist Tier** | 12-tier ladder, XP progress, perk preview per tier | Monetization |
| **Splice Roulette** | Wheel, odds display, fragment inventory, spin cost | Monetization, Genetics |
| **Event Hub** | Live-ops calendar, active events, timers, themed offers | Monetization |
| **Apex Cup** | Leaderboard, bracket, rewards | Monetization |
| **Recipe Share** | Browse/rate shared splice recipes with **ancestry chains** | Monetization, Genetics |
| **Marks Shop** | Raid Marks and Defense Marks inventory and spend options | Raiding |

---

## 4. Gaps — Assumed But Never Specced

These are referenced across the specs as if they exist. They don't.

**Gene Vault upgrades.** The node spec says harvest yield scales with Lab tier, and the genetics spec says roster cap scales with Gene Vault tier. Nothing defines what the Vault is, how many tiers it has, what it costs, or what else it gates. This is a full progression system sitting in a blind spot — it's likely the main long-term Gene Shard sink and it has no document. **Highest-priority gap.**

**Trait Codex.** Players must learn 12–16 Instinct behaviors, plus Frame, Armament, and Field traits across four tiers. There is nowhere to read what any of them do. Without this, the splice screen's probability table is unreadable to a new player.

**Mail / notification centre.** Raid alerts, alliance notices, event announcements, and reward grants all need somewhere to land.

**Player profile.** Referenced implicitly by alliance applications and the Apex Cup leaderboard. Needs identity, stats, Founder display, Geneticist Tier.

**FTUE / onboarding.** No spec covers the first session — where the Ark, the map, the first splice, and Founder naming get introduced. This determines whether any of the rest matters.

**Settings, account, support.** Standard, unglamorous, required for store submission.

---

## 5. Design Priority

Build in this order. Each tier is playable before the next begins.

**Tier 1 — the core loop is provable without any of the rest**
1. Splice
2. Roster
3. Deployment
4. Live Combat
5. Ark Home

**Tier 2 — the reason to keep playing**
6. World Map
7. Region Detail
8. Creature Detail
9. Lineage View
10. Campaign Select
11. Post-Wave

**Tier 3 — the reason to spend**
12. Store Home
13. Custom Chest
14. Geneticist Tier
15. Season Pass
16. Splice Roulette

**Tier 4 — the reason to stay**
17. Alliance Home, Chat, Stake Management, Garrison
18. Collector Dispatch, Route Plotter, Raid Alert, Raid Result
19. Replay Viewer

**Tier 5 — completeness**
Everything remaining, plus the §4 gaps.

The Lineage View sits in Tier 2 deliberately. It's the game's distinctive artifact and the thing players screenshot, and it should be visible early rather than treated as a late-game feature.

---

## 6. Shared Components

Worth building once and reusing everywhere:

- **Creature card** — chassis silhouette, generation badge, four trait pips with tier colours, assignment state. Appears on at least nine screens.
- **Trait pip** — icon, category colour, tier indicator. The single most-repeated element in the app.
- **Currency header** — charges with regen timer, shards, tier progress.
- **Probability table** — used on Splice and Splice Roulette. Regulatory-relevant; build it once and correctly.
- **Region tile** — heat-map colouring, controller banner, alert state.
- **Timer chip** — regeneration, transit, charge regen, weekly tick.

Getting the creature card right is worth disproportionate effort. It's the atom of the entire interface.

---

## 7. Open Questions

1. **Is five bottom-nav tabs one too many?** Alliance could live inside Ark for players not in one, surfacing only on join.
2. **Where does the Trait Codex belong** — Lab tab, or contextually inside Splice? Contextual is more discoverable, standalone is more browsable.
3. **Does Combat need its own tab?** Currently entered from Ark and Map. Consistency may argue for a dedicated entry point.
4. **How much of the Map screen is legible on a small phone** with heat-map overlay, territory banners, Ark position, collector routes, and alerts all live at once? This is the hardest layout problem in the app.

---

*Next: the art direction one-pager — resolving the clinical-biotech versus organic-creature register, which affects nearly every screen above.*
