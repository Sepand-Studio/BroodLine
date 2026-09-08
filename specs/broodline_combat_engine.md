# Broodline — The Combat Engine

*Technical spec, the simulation everything runs on*

> **CURRENT — technical spec.** `broodline_combat_numbers.md` owns the values.
> This document owns how they resolve: tick order, targeting, how each counter
> is applied, what a replay stores, and where the simulation runs.

**Three things fall out of writing it.** Determinism is a hard requirement rather than a nice property, and it is what makes server verification cheap — §2. **There is no generic counter system**; the eight counters are eight different hooks at five different points in the tick, and unifying them produces something worse — §5. And the three-state loss diagnosis bible §4.11 now requires has to be computed *during* the simulation, not inferred from its result — §7.

---

## 1. What the engine has to serve

One simulation, four consumers, and they impose different requirements.

| Consumer | Runs | Requires |
|---|---|---|
| **Campaign and region defence** | Client, live | Responsiveness. No adversary, so no verification needed |
| **Raid attack** | Client live, server verified | **The defender is not present.** A modified client wins every raid unless the result is checked |
| **Raid auto-resolve** | Server | Bible §4.10 — a full simulation, never a stat roll |
| **Replay** | Client, from stored data | Bit-identical reproduction of a run that happened elsewhere |

**The last two are why determinism is not optional.** A replay that diverges is worse than no replay, and `broodline_collectors_raiding.md` makes the replay the thing that makes losing survivable — "here is exactly what happened" is the entire defence against a raid loss feeling arbitrary.

---

## 2. Determinism

> **Fixed-step, integer-deterministic, seeded. Same seed and same inputs produce the same result on every platform, every time.**

**Fixed step at 30Hz.** The fastest attack interval in the game is Skitter's 0.4s and the retarget delay is 0.4s, so 33ms granularity is ample. Variable timestep would make the simulation frame-rate dependent and non-reproducible.

**No floating point in simulation state.** Positions, health, shields, timers and damage are integers in fixed-point — positions in thousandths of a tile, health and damage in whole units, timers in ticks. Float arithmetic differs across CPU architectures and compiler flags, and a replay that plays back differently on an iPhone than it recorded on an iPad is a bug nobody can reproduce.

**One seeded RNG stream per wave**, advanced only by simulation events. Nothing in rendering, UI or animation may draw from it.

**What is actually random?** Almost nothing. Wave composition and spawn timing are authored, and Skittish repositions deterministically to the nearest free pocket away from the threat. The RNG covers: Instinct tie-breaks when two targets are equally valid, and the Aberrant Contrary's tie-break. **That is the whole list**, and it is worth noticing — a combat model built on counters rather than on damage rolls barely needs randomness at all.

### 2.1 Server verification is cheap because of this

The obvious way to stop raid cheating is to simulate raids on the server. That costs responsiveness — the attacker waits on a round trip for a ninety-second wave.

**Determinism gives a better answer.** The client simulates live and submits **seed, placement and Rally timestamp** — a few dozen bytes. The server re-runs the same simulation and compares the outcome. A mismatch is a rejected raid.

**The verification is one re-run of a ninety-second wave, and every raid gets one.** It can be batched; it is not sampled. That is affordable in a way that live server simulation of every raid is not, and it is entirely a consequence of the determinism the replay system already needed.

**Auto-resolve is the same code path with no player input**, per bible §4.10.

### 2.2 Geometry without vectors

The largest determinism simplification available, and it falls out of the bible's own decision to strip terrain modifiers.

Regions have **1–3 lanes, no elevation, no water, no emplacement variety.** That means:

- A lane is an authored **polyline path**, sampled at authoring time into a fixed array of points
- A raider's position is a **single scalar** — distance travelled along its lane. Movement is `progress += speed × dt`. No vectors, no steering, no pathfinding at runtime
- A creature's position is a **tile index**, fixed for the whole wave
- Range checks compare a tile to a lane position, and since both are known at authoring time, **every region ships a precomputed distance table**: for each emplacement tile, the squared distance to each sample point on each lane, in fixed point

**The hot loop therefore contains no square roots, no trigonometry and no vector maths at all.** Range checking is an array lookup and a comparison. This removes the largest single class of cross-platform divergence before it can exist, and it is why a ninety-second wave costs roughly 20 ms of server CPU.

**It also makes distance an exact integer**, which matters at §5.1: raiders at equal progress in different lanes tie constantly, so the tie-break is load-bearing rather than decorative.

Cost: lanes cannot be procedurally generated at runtime, and raid ambush terrain must be drawn from an authored set rather than synthesised. That is the right trade — thirty hand-authored regions plus a dozen authored ambush lanes is a content task, not an engineering risk.

---

## 3. The replay format

A replay is **not** a recording of state. It is the inputs.

| Field | |
|---|---|
| Wave definition ID | Authored wave, or region-defence composition seed |
| RNG seed | One 64-bit value |
| Terrain | Family, lane count, pocket count, lane length |
| Deployment | Five creature snapshots: species, both combat traits with coverage tier, Instinct, HP, and assigned pocket |
| Rally | Tick index, and which creature. Or absent |
| Engine version | See below |

**A few hundred bytes.** Which matters, because bible §4.9 gives every raid a replay and there are two raids per player per day.

**Engine version is stored and it is load-bearing.** A balance patch that changes Chill's slow value invalidates every replay recorded before it. **Replays from a superseded engine version show their recorded outcome with a notice**, and do not re-simulate. The client never retains old engine versions. Discarding them is the alternative and it is worse — a player who lost a raid wants to see it more than they want it to be current.

---

## 4. Tick order

Each 33ms tick, in this order, and the order is normative — changing it changes outcomes.

| # | Phase | |
|---|---|---|
| **1** | **Spawn** | Introduce raiders whose spawn tick has arrived |
| **2** | **State** | Resolve forced state changes: Burrow surfacing, Delver reaching the Ark, Drift altitude, Chill expiry, Skittish repositioning |
| **3** | **Movement** | Advance every raider by its current speed. Movement precedes targeting so a creature never fires at a position a raider has already left |
| **4** | **Targeting** | Retarget any creature whose target is dead, out of range, or whose Instinct preference has changed. Respect the 0.4s retarget lockout |
| **5** | **Attack** | Resolve attacks whose interval has elapsed. Apply damage through the counter hooks at §5 |
| **6** | **Death** | Resolve deaths. Brood splits here. Cinder suppression applies here |
| **7** | **Breach** | Any raider at the Ark: deduct integrity, record the breach diagnosis at §7, remove it |
| **8** | **Resolve** | Check integrity ≤ 0 (loss) or no raiders remaining and none pending (win) |

**Movement before targeting, targeting before attack, death after attack.** A raider that dies this tick has already moved and has already been hit; a Brood killed at phase 6 splits at phase 6, and its children spawn at the parent's position and act from the next tick. Anything else produces same-tick cascades that are impossible to reason about.

---

## 5. There is no generic counter system

The natural instinct is to build one — eight counters, one interface, `applyCounter()`. **It produces something worse**, because the eight are eight different kinds of rule operating at five different points in the tick.

| Counter | Kind | Tick phase | Effect |
|---|---|---|---|
| **Splash** | On-hit target selection | 5 Attack | The hit resolves against N targets within the radius, not one |
| **Pierce** | Damage-modifier suppression | 5 Attack | Lifts Breaker's per-hit cap for damage from this carrier |
| **Sprint** | Damage permission | 5 Attack | Permits shield degradation, which is otherwise zero from any source |
| **Cinder** | On-death suppression | 6 Death | Suppresses the split at birth, by generation depth |
| **Taunt** | Target override on the raider | 4 Targeting | Forces the Lash's target regardless of its own rule |
| **Chill** | Speed modifier on the raider | 2 State | Halves Courser speed while applied |
| **Reach** | Targetability flag | 4 Targeting | Makes airborne raiders in this lane valid targets |
| **Burrow** | Forced state change | 2 State | Surfaces a submerged Delver at the lane midpoint |

**Three sit in Attack, two in State, one each in Targeting, Death and Targeting again.** A unified interface would have to be a switch statement wearing a coat, and it would obscure the thing that actually matters: **each counter is a specific rule at a specific moment, and moving it changes behaviour.**

**Build them as eight explicit rules at their eight sites.** It is more code and it is code that can be read.

### 5.1 Which raider does a coverage tier apply to?

Six of the eight scale by simultaneity — Splash, Pierce, Taunt, Chill, Sprint, Burrow — and "Pierce I bypasses Plate on one Breaker at a time" needs a rule for *which* one.

> **Assignment follows the carrier's current target, then nearest-first for the remainder, re-evaluated on retarget.**

A Pierce II carrier targeting Breaker A also suppresses the cap on the nearest other Breaker in range. When it retargets, both assignments re-evaluate.

**Chill, Taunt and Burrow are exceptions because their carriers may not be attacking the raider in question.** For those three, assignment is **nearest-first within range, re-evaluated each tick.**

> **The comparator is `(distance, spawnIndex)` ascending — a total order.**

Nearest-first alone is not deterministic. §2.2 makes distance an exact integer, so ties are common rather than rare, and a sort over equal keys can order them differently on two runtimes because the sort is not guaranteed stable. Tie-breaking on spawn index — the same rule §6 applies to targeting — makes the comparator a total order, so any correct sort produces identical output everywhere.

**Assignment must be visible.** A player who cannot see which Breaker their Pierce is suppressing cannot tell whether coverage or placement failed, which is exactly the distinction §7 exists to draw.

### 5.2 Depth-scaling counters

**Cinder** scales by split generation: tier I suppresses the first, tier II the first and half the second, tier III both. "Half the second" resolves as the first ceil(n/2) of each parent's children, deterministically ordered by spawn index — **not** a per-child coin flip.

**Reach** scales by lane count and is a targetability flag rather than a per-raider assignment. Reach II makes Drift targetable in two lanes; which two is set by the carrier's assigned lane plus the nearest adjacent lane with a Drift in it.

---

### 5.3 Coverage is capacity

The bible: *coverage decides scale, never whether the lock turns.* In code, **every counter trait is a capacity resource.**

```
capacity[tier]:  I = 1,  II = 3,  III = 5      (starting values, tuned in soft launch)
```

Unit of capacity by family:

| Family | Traits | One unit buys |
|---|---|---|
| **Concurrent** | Chill, Pierce, Taunt, Sprint, Burrow | One raider affected at a time; freed when that raider dies or leaves |
| **Per-attack** | Splash | One additional raider hit by each attack |
| **Depth** | Cinder | One generation of splits suppressed |
| **Spatial** | Reach | One lane in which fliers are targetable |

**Capacity from multiple creatures carrying the same trait sums.** Two creatures with Chill II give capacity 6 — six Coursers slowed. This is what makes "bring more of the answer" a valid response to a bigger wave, which is the design's escalation model: later waves send more, never resistant.

**Capacity is recomputed from scratch every tick from the live creature set, never accumulated incrementally.** Incremental capacity drifts, and drift in a counter system is a fairness bug. It also answers what happens when a carrier dies mid-wave: its capacity frees immediately, because there is nothing to free — every slowed Courser released at once, which is dramatic and worth watching in soft launch.

### 5.4 The two composition rules are engine invariants

The bible's rules — **never two raiders answered by the same trait in one wave**, and **a maximum of four raider types per wave** — are validated by the engine at wave load and throw on violation.

They are load-bearing for the counter model's fairness guarantee, and a content author will otherwise break them by accident in month four. The supersession register already notes that Sunder cannot share a wave with Breaker or Bulwark; that is this rule, and it belongs in an assertion rather than a reviewer's memory.

Catching them at config publish time turns a runtime throw into a failed publish — see `broodline_solo_execution.md` §5.2.

---

## 6. Targeting

**Every creature holds one target.** Retarget triggers: target dead, target out of range, or the Instinct's preference now names a different valid target.

**The 0.4s retarget lockout is a lockout on acquiring**, not on firing. A creature whose target dies stops firing immediately and acquires 12 ticks later. That gap is what makes Splash a counter rather than a convenience — `broodline_combat_numbers.md` §5 is explicit that eight Skirmishers beat a single-target defender who out-DPSes them on paper.

**Instinct is a target-selection predicate plus an optional trigger, not a behaviour tree.** Six rules:

| Instinct | Predicate | Trigger |
|---|---|---|
| Bloodscent | Lowest current HP in range | — |
| Vanguard | Closest to the Ark | — |
| Overwatch | Furthest in range | Passive: +25% range, −20% attack speed |
| Last Stand | Nearest | Self below 25% HP → +50% attack speed |
| Skittish | Nearest | Self below 40% HP → reposition to the nearest free pocket away from the threat, 2s, cannot act |
| Pack Sense | Nearest | Adjacent same-species ally → +15% damage to both |

**Ties break on spawn index, ascending.** Deterministic and cheap. The RNG is used only where a tie-break must not be predictable.

**Contrary, the Aberrant, requires an allied-target census.** It targets whatever the fewest allies target, so the engine maintains a per-tick count of how many creatures hold each raider as a target. That is one pass over five creatures and it should be built even if Contrary is the only consumer, because a general census is cheaper than a special case.

---

## 7. Breach diagnosis

Bible §4.11 requires the loss screen to distinguish **access**, **coverage** and **placement**. Those cannot be inferred from the final state — by the time the wave ends, the information about why a specific raider was unanswerable is gone.

> **Each breach records its diagnosis at tick phase 7, at the moment it happens.**

For every raider that reaches the Ark:

| Field | Determined by |
|---|---|
| `raider_type` | The raider |
| `answering_trait` | Lookup |
| `access` | Did any deployed creature carry the trait at any tier? |
| `coverage` | Was the trait's tier sufficient for the number of that raider type simultaneously present? |
| `placement` | Was a carrier assigned to a lane or position from which the assignment at §5.1 could have reached it? |
| `lane` | Which lane |

**Three booleans, evaluated in order.** The first false is the diagnosis, and it maps directly onto the three messages bible §4.11 specifies.

**Coverage is the subtle one.** "Sufficient tier" is not a property of the deployment, it is a property of the deployment *against what was on the board at that moment* — Chill II is sufficient against two Coursers and insufficient against three. It has to be evaluated at breach time against the live count.

**The same three fields drive the pre-wave check.** `broodline_raider_roster.md` §5 requires the composition panel to mark raiders the current deployment cannot handle, and §7's note that the check must run on coverage and lane assignment rather than presence is the same computation run against the authored wave rather than against a breach. **One function, two call sites.**

---

## 8. Integrity and resolution

**Integrity is a pool**, per `broodline_combat_numbers.md` §2. Each breach deducts by raider type — Skirmisher 1, most raiders 2, Breaker and Bulwark 4, the Sunder 6.

**The wave is lost the tick integrity reaches zero or below**, and the simulation stops there. Raiders still on the board are not resolved and are not counted.

**The wave is won when no raiders remain on the board and none are pending.** Brood children pending from a death this tick count as pending.

### 8.1 Every simulation terminates

Three of the eight raiders required soft-lock fixes during design — Delver, Bulwark and Breaker all had versions that could permanently block progress. That pattern is a property of untargetable and unkillable mechanics, and the engine should refuse to let a new one ship.

- **Win** — all raiders despawned and the spawn table exhausted
- **Loss** — integrity at or below zero
- **Hard tick cap: 5,400 ticks (180 seconds).** A wave is designed for ~90. Hitting the cap is a **content bug, not a gameplay outcome** — the engine returns `Stalled`, the server awards a defender win, and it fires a high-priority telemetry event naming the wave

Plus a stall detector: **if no raider has advanced and no HP has changed for 300 consecutive ticks, terminate as `Stalled` immediately** rather than burning to the cap. This catches the exact soft-lock shape — a submerged Delver with no Burrow present and nothing able to reach it — in ten seconds instead of three minutes.

`Stalled` must be impossible to reach in a shipped wave. It exists so that when it does happen, it is loud, safe for the player, and traceable to a wave ID.

**Rally** is one use per wave, four seconds of doubled attack speed on one creature, no cooldown. It is a player input with a tick index, and in a replay it is a stored timestamp.

---

## 9. Performance

`broodline_waves_37_44.md` §7 and both later wave documents flag the same worry: **wave 44 and five of chapter 8's waves put close to a hundred entities on the board.** Sixty Skirmishers plus three Broods becoming thirty-nine, plus five creatures.

**Simulation is not the problem.** A hundred entities at 30Hz with integer arithmetic and one targeting pass is trivial on any phone that can run the game at all.

**Rendering is the problem**, and it is `broodline_client_architecture.md`'s. But two simulation-side decisions help: entities that are functionally identical (Skirmishers, Mites) should be instanced rather than individually simulated for animation purposes, and the engine should expose a per-tick entity count so the render layer can degrade before it drops frames rather than after.

**This needs its own proof against the engine**, running in parallel with the rig proof. It is the second thing in Phase 1 that can invalidate an assumption, and wave 44 is the test case — it exists, it is authored, and it is the worst case in the campaign. Scheduled at `broodline_client_architecture.md` §4, which owns the budget it is measured against.

### 9.1 The headless batch runner is part of the engine

Because the engine is deterministic, headless and free of rendering, it can run a wave far faster than real time. **Build the batch runner with the engine, not as a later tool.**

It is the only honest way to answer questions the design cannot settle by argument. **Trait utility balance is the standing example** — Carapace, Litter, Regrow and Screen counter nothing, and "what is Regrow worth" becomes a measurement the moment the engine runs headless: hold the wave set fixed, vary the trait, count clears across five hundred waves.

It is also how sixty authored waves get tuned without playing them sixty times, and it is what feeds `broodline_playtest_tuning_sheet.md` its inputs.

---

## 10. Guardrails

- Fixed step at 30Hz. No variable timestep, ever
- No floating point in simulation state
- One seeded RNG stream per wave, advanced only by simulation events
- Tick phase order is normative. Changing it is a balance change
- Every counter is an explicit rule at its own site. No generic counter interface
- No square roots, no trigonometry and no vector maths in the hot loop — distance is a precomputed table lookup
- Every comparator is a total order, tie-breaking on spawn index ascending
- Capacity is recomputed from the live creature set each tick, never accumulated
- The two wave-composition rules throw at wave load
- Every simulation terminates — hard cap at 5,400 ticks, stall detector at 300
- Breach diagnosis is recorded at breach time, never inferred afterward
- The pre-wave check and the loss diagnosis use the same function
- Replays store inputs, never state
- Engine version is stored with every replay, and superseded replays are marked rather than discarded
- Auto-resolve is the identical code path with no player input — never a stat roll

---

## 11. Open questions

1. ~~**Does the client simulate raids and get verified, or does the server simulate and the client play back?**~~ **Settled: client simulates, server verifies** — `broodline_data_model.md` §8. The raid alert window is ninety seconds and the defender may be joining live; a round trip per wave against that is the wrong trade, and determinism makes verification a single re-run either way.
2. ~~**How often is raid verification actually run?**~~ **Resolved — every raid. Determinism makes it a single re-run of a ninety-second wave, which is cheap enough, and sampling lets cheating through during exactly the window where a game's PvP reputation is set.**
3. ~~**What happens to an in-flight replay when a balance patch lands?**~~ **Resolved — recorded outcome with a notice. The client never retains old engine versions.**
4. ~~**Skittish's reposition is the only RNG the player sees affecting an outcome.**~~ **Resolved — nearest free pocket away from the threat, deterministic. That reduces the RNG surface to tie-breaks only.**
5. ~~**The retarget lockout at 0.4s is doing more balance work than any other number in this document.**~~ **Moved — the retarget lockout is now a first-class tunable in `broodline_combat_numbers.md` §5.**

6. **Capacity values 1 / 3 / 5 are placeholders**, inherited with the capacity model at §5.3. The bible's examples fit — Splash III catches five of eight Skirmishers, Cinder III catches the second split generation — but **Reach scales by lane and there are at most three lanes**, so III = 5 is meaningless for it. Reach probably caps at lane count. Confirm before the Codex copy is written, since `broodline_trait_codex.md` must state coverage in concrete terms.
7. **Sprint's 0.4 s window** — the rule that gives Bulwark's shield a concrete degradation threshold — was invented in the merged document rather than derived from the design. **Needs a design decision, not an engineering one.**

---

*Owns: tick order, determinism requirements, lane geometry, replay format, targeting resolution, counter application sites and the capacity model, wave-composition invariants, breach diagnosis, termination, the batch runner, and where simulation runs. Does not own: any value (`broodline_combat_numbers.md`), wave contents (the seven wave documents), rendering or the client (`broodline_client_architecture.md`), or where the engine is deployed (`broodline_solo_execution.md`).*

*Absorbed `broodline_sim_core.md` — geometry without vectors, the capacity model, the composition invariants, termination and the batch runner — which is ❌ superseded as a result.*
