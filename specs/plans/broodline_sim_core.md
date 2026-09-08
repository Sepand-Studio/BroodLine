# Broodline — Simulation Core

*Engineering spec. Step 1 of the build order, and the thing everything else waits on.*

---

## 1. Why This Document Exists

The bible commits, in three places, to auto-resolve being *a full simulation of the same engine, never a stat comparison rolled against a dice check*, and states that the replay is what makes losing tolerable. The architecture spec turns that commitment into an engineering constraint: **the same simulation must produce bit-identical results on an iPhone and in a Linux container.**

That constraint cannot be retrofitted. A simulation written with floats, variable timesteps and `Dictionary` iteration is not "fixed later" — it is rewritten. So this is written before the client, before the server, before any content.

The core also has a second job the design documents do not name. The bible's hard-counter model — eight raiders, eight answering traits, **no damage floor** — has never been expressed as a mechanism. "A defence without the answering trait does not work" is a design statement. §5 is what it means in code, and it is the highest-value half of this document.

**Engine: Unity 6, decided.** The core is a plain C# `netstandard2.1` library with no `UnityEngine` reference.

---

## 2. The Determinism Contract

Nine rules. Every one of them is a rule because breaking it produces a divergence that is invisible on the machine you are testing on.

**1. No floating point. Anywhere.**
All quantities are `Fix64`, a fixed-point type over `long` at Q32.32 — 32 integer bits, 32 fractional. Range ±2.1 billion, precision ~2.3×10⁻¹⁰. The core contains no `float`, no `double`, no `decimal`. Enforced by a Roslyn analyzer that fails the build on the token, not by code review.

**2. No `System.Math`, no `UnityEngine.Mathf`.**
`Fix64` provides its own `Sqrt` (integer Newton-Raphson, fixed iteration count), `Abs`, `Min`, `Max`, `Clamp`. Trig is a 4096-entry lookup table with linear interpolation in fixed point. Both are deterministic across platforms; the platform libraries are not.

**3. Fixed timestep, 30 Hz.**
One tick is exactly `Fix64.One / 30`. The simulation never sees a wall-clock delta. Rendering interpolates between the last two simulation states; the simulation does not know rendering exists.

**4. Seeded PRNG, one stream per simulation.**
`xorshift128+`, seeded by the server. Never `System.Random` (implementation-defined and version-dependent), never `UnityEngine.Random` (global mutable state). The RNG is passed explicitly into every function that needs it — no ambient access, so a stray call cannot silently consume a draw.

**5. Deterministic iteration order.**
No `Dictionary`, `HashSet`, or LINQ over unordered sources anywhere in the core. Entities live in dense arrays indexed by a stable integer ID and are iterated in ID order. **This is the divergence that always ships**, because .NET's hash iteration order is stable within one process and not across two runtimes.

**6. No wall-clock, no ambient time.**
Time is `tickIndex`. `DateTime.Now` does not appear in the core.

**7. No allocation in the tick loop.**
All entity storage is preallocated to the wave's maximum. Zero GC pressure means no GC-timing-dependent behaviour, and it also means the headless server can run a wave in a few milliseconds.

**8. No parallelism inside a tick.**
The core is single-threaded by construction. Parallelism happens *across* simulations on the server, never within one.

**9. The core has no dependencies.**
No Unity, no Newtonsoft, no BCL beyond primitives and arrays. If it needs something, it implements it.

### Assembly enforcement

The core ships as a separate compiled DLL, referenced by both the Unity client and the `sim` service. It is **not** source in `Assets/`. Unity assembly definitions cannot reference `UnityEngine` from it. This is what physically prevents a well-meaning gameplay engineer from reaching for `Time.deltaTime` at 11pm.

---

## 3. Geometry Without Vectors

The single biggest determinism simplification available, and it falls out of the bible's own decision to strip terrain modifiers.

Regions have **1–3 lanes, no elevation, no water, no emplacement variety**. That means:

- A lane is an authored **polyline path**, sampled at authoring time into a fixed array of points.
- A raider's position is a **single scalar**: distance travelled along its lane. Movement is `progress += speed * dt`. No vectors, no steering, no pathfinding at runtime.
- A creature's position is a **tile index**, fixed for the whole wave (creatures never reposition during a wave).
- Range checks compare a tile to a lane position. Since both are known at authoring time, **every region ships a precomputed distance table**: for each emplacement tile, the squared distance to each sample point on each lane, in `Fix64`.

The consequence: **the hot loop contains no square roots, no trigonometry, and no vector maths at all.** Range checking is an array lookup and a comparison. This removes the largest single class of cross-platform floating-point divergence before it can exist, and it makes a 90-second wave cost roughly 20 ms of server CPU.

Cost: lanes cannot be procedurally generated at runtime, and raid ambush terrain must be drawn from an authored set rather than synthesised. That is the right trade — thirty hand-authored regions plus a dozen authored ambush lanes is a content task, not an engineering risk.

---

## 4. Entities and Tick Order

### Entities

```
Creature   id, tileIndex, speciesId, traitA, traitB, coverageA, coverageB,
           instinctId, hp, maxHp, damage, range², attackInterval,
           attackCooldown, instinctState, rallyUntilTick

Raider     id, laneIndex, progress, speed, baseSpeed, hp, maxHp,
           archetypeId, statusFlags, splitGeneration, spawnTick

Wave       laneCount, spawnTable[], arkHp, tickIndex
```

All three live in preallocated dense arrays. Raider capacity is the wave's authored maximum plus the worst-case Brood split expansion, computed at authoring time and asserted.

### Tick order

Fixed and never reordered. Reordering these is a save-breaking change to every stored replay.

```
1.  Spawn      raiders due this tick, in spawn-table order
2.  Status     tick down Chill slows, Cinder burns, Taunt redirects, Sprint stacks
3.  Move       every raider, in ID order: progress += effectiveSpeed * dt
4.  Arrive     raiders past the end of the lane damage the Ark and despawn
5.  Instinct   every creature, in ID order: evaluate trigger, update instinctState
6.  Target     every creature, in ID order: apply target rule, select target
7.  Attack     every creature whose cooldown expired: resolve damage
8.  Counter    apply counter-trait effects (§5) at the point of contact
9.  Death      raiders at 0 HP: resolve Brood splits, then despawn.
               Creatures at 0 HP: mark for regeneration, remove from board
10. Rally      apply or expire the player's rally window
11. Terminate  check win, loss, and stall conditions (§7)
```

Two ordering notes that matter. **Movement precedes targeting**, so a creature always fires at where a raider is now, never where it was — consistent between client and server without extrapolation. **Death resolves after all attacks**, so two creatures firing at the same raider on the same tick both land; overkill is not refunded, and simultaneous kills are not order-dependent.

---

## 5. The Counter Model in Code

The most important section. The bible states the model in design terms; this is the mechanism.

### The principle

**Each raider carries a defensive mechanic that only its answering trait defeats.** It is never a damage multiplier. There is no damage floor because damage is not the axis — the raider is not "resistant," it is *doing something* that ordinary damage does not address, and the counter trait is what addresses it.

This is why "a rally with enough power on paper still fails against a Breaker wall if nothing carries Pierce" is literally true rather than a tuning claim.

### The eight mechanisms

| Raider | Defensive mechanic | Answering trait defeats it by |
|---|---|---|
| **Skirmisher** | Arrives in eights; single-target DPS cannot clear the volume before the lane empties | **Splash** — the attack hits N raiders instead of one |
| **Breaker** | Incoming damage reduced to 5%; ignores Taunt redirection | **Pierce** — damage reduction suppressed for N concurrent Breakers |
| **Lash** | Outranges the front line, targets the rearmost creature or the Collector | **Taunt** — forced to target the taunting creature instead, N concurrent |
| **Courser** | Moves at 4× base speed; reaches the Ark before any DPS output can kill it | **Chill** — speed reduced to 1× for N concurrent Coursers |
| **Brood** | On death, splits into three at 40% HP; those split again | **Cinder** — suppresses splitting to depth N (N=1 stops the first split) |
| **Drift** | Flying: **untargetable** by creatures without Reach | **Reach** — the creature can target fliers, in N lanes |
| **Bulwark** | Front shield blocks all damage; degrades only from hits within 0.4 s of each other | **Sprint** — attack interval floor removed, shield degrades, N concurrent |
| **Delver** | Submerged and **untargetable** until it surfaces mid-formation | **Burrow** — forced to surface at the front line, N concurrent |

Three of these (Drift, Delver, Bulwark) make the raider *unkillable* rather than merely hard, which is exactly the "no damage floor" guarantee expressed mechanically.

### Coverage is capacity

The bible: *coverage decides scale, never whether the lock turns.* In code, **every counter trait is a capacity resource.**

```
capacity[tier]:  I = 1,  II = 3,  III = 5      (starting values, tune in soft launch)
```

Unit of capacity by family:

| Family | Traits | One unit buys |
|---|---|---|
| **Concurrent** | Chill, Pierce, Taunt, Sprint, Burrow | One raider affected at a time; freed when that raider dies or leaves |
| **Per-attack** | Splash | One additional raider hit by each attack |
| **Depth** | Cinder | One generation of splits suppressed |
| **Spatial** | Reach | One lane in which fliers are targetable |

Capacity from multiple creatures carrying the same trait **sums**. Two creatures with Chill II give capacity 6 — six Coursers slowed. This is what makes "bring more of the answer" a valid response to a bigger wave, which is the design's stated escalation model: *later waves send more, never resistant.*

### Allocation must be deterministic

When six Coursers enter and Chill capacity is 5, which five get slowed? **Lowest raider ID first, evaluated in step 2 of the tick.** Not nearest, not most dangerous, not random. Nearest-first is more intuitive and requires a sort with a tie-break rule that will eventually differ between builds; ID order is stable, cheap, and — because raider IDs are assigned in spawn order — it reads on screen as "the ones that arrived first," which is close enough to intuitive.

Capacity is recomputed from scratch every tick from the live creature set, never incrementally accumulated. Incremental capacity drifts, and drift in a counter system is a fairness bug.

### The two wave-composition rules are core invariants, not content guidelines

The bible's rules — **never two raiders answered by the same trait in one wave**, and **maximum four raider types per wave** — are validated by the core at wave load and throw on violation. They are load-bearing for the counter model's fairness guarantee, and a content author will otherwise break them by accident in month four. The supersession register already notes that Sunder cannot share a wave with Breaker or Bulwark; that is this rule, and it belongs in an assertion rather than a reviewer's memory.

---

## 6. Instinct as a Behaviour Tree

Six behaviours, untiered, from the bible §1.4. Each is a **target rule**, an optional **trigger**, and an optional **response**.

```
Bloodscent   target: lowest current HP in range
Vanguard     target: closest to the Ark
Overwatch    target: furthest in range        response: +25% range, −20% attack speed
Last Stand   target: nearest    trigger: self HP < 25%   response: +50% attack speed
Skittish     target: nearest    trigger: self HP < 40%   response: reposition to adjacent free tile
Pack Sense   target: nearest    trigger: adjacent to same-species ally
                                          response: +15% damage to both
```

Implementation notes:

- Each is a **switch case, not a scripted tree**. Six behaviours do not need a behaviour-tree runtime, and a runtime is a determinism liability. If the count ever grows past twelve, revisit.
- **Every target rule needs an explicit tie-break**, and it is always lowest raider ID. Two raiders on equal HP within Bloodscent's range must resolve identically on both builds.
- **Skittish repositioning is the only mid-wave position change in the game**, and it violates the general "creatures never move during a wave" rule. Target tile is the lowest-index adjacent free tile. If none is free, the trigger fires visually and nothing moves — the Codex must state this, or it reads as a bug.
- `instinctState` is part of the simulation state hash, because the client shows a visible trigger state (animation change, colour shift) and a desync in *when* Last Stand fires is a desync a player will see.
- Instinct **counters nothing**. It never touches capacity, never grants targetability against Drift or Delver, never suppresses a split. This is a hard invariant: the moment an Instinct partially answers a raider, the counter model's supply guarantee stops holding.

---

## 7. Termination and the Soft-Lock Invariant

Three of the eight raiders required soft-lock fixes during design — Delver, Bulwark, and Breaker all had versions that could permanently block progress. That pattern is a property of untargetable and unkillable mechanics, and the core should refuse to let a new one ship.

**Every simulation terminates.** Enforced three ways:

1. **Win:** all raiders despawned and the spawn table is exhausted.
2. **Loss:** Ark HP reaches zero.
3. **Hard tick cap: 5,400 ticks (180 seconds).** A wave is designed for ~90. Hitting the cap is a **content bug**, not a gameplay outcome — the sim returns `Stalled`, the server awards a defender win, and it fires a high-priority telemetry event naming the wave.

Plus a stall detector: **if no raider has advanced and no HP has changed for 300 consecutive ticks, terminate as `Stalled` immediately** rather than burning to the cap. This catches the exact soft-lock shape — a submerged Delver with no Burrow present and nothing able to reach it — in ten seconds instead of three minutes.

`Stalled` must be impossible to reach in a shipped wave. It exists so that when it does happen, it is loud, safe for the player, and traceable to a wave ID.

---

## 8. Replays

A replay is inputs, not frames.

```json
{
  "simVersion": "1.4.0",
  "seed": "0x9E3779B97F4A7C15",
  "regionId": 17,
  "laneSet": "defile_a",
  "waveId": "c3_w22",
  "attacker": [ { creature records } ],
  "defender": [ { creature records, tileIndex } ],
  "inputs": [ { "tick": 412, "type": "rally", "creatureId": 3 } ],
  "outcomeHash": "sha256:..."
}
```

Roughly 2 KB. The viewer re-simulates. Consequences:

- **Every replay is a regression test.** The corpus is the determinism suite (§9), and it grows for free as players play.
- **`simVersion` is immutable per replay.** When the core changes, old replays must still play back correctly. Every released `sim` version stays deployed and replay requests route to the version they were recorded under. This is why `sim` is a separate deployable in the architecture spec — it is the only service that cannot roll forward.
- **`outcomeHash`** is a hash of the terminal state: Ark HP, per-creature final HP, tick count, raiders killed. It is what server-side validation compares.

Retention: keep everything. At 2 KB and ~20k raids a day, a year is about 15 GB, and the corpus is worth more than the storage.

---

## 9. Validation and the CI Gate

### Server-side wave validation

The client submits `{ waveId, seed, roster, placements, inputLog, claimedOutcomeHash }`. The `sim` service re-simulates and compares hashes. Mismatch means no reward and a telemetry event.

**Validate every wave. Do not sample.** At ~20 ms per wave and ~300k waves a day at 50k DAU, that is under two CPU-hours daily — cheaper than the fraud-analysis pipeline that sampling would require, and it makes the honest answer available for support tickets.

The seed comes from the server in the pre-wave call, so the client cannot search seeds for a favourable outcome.

### The determinism gate

A nightly job replays a corpus of several thousand stored replays on both an **iOS device-farm build** and the **Linux server build**, diffing outcome hashes and a per-100-tick intermediate state hash. Any drift fails the build.

The intermediate hashes matter more than the terminal one. A divergence that self-corrects by the end of the wave still means the player watched a different fight than the server recorded, and terminal-hash-only testing will miss it for months.

Seed the corpus before launch with 5,000 generated waves spanning every raider archetype, every Instinct, every coverage tier, and every legal wave composition.

### Fuzzing

A generator producing random-but-legal wave and roster combinations, run continuously, asserting: termination within the cap, no negative HP, no capacity over-allocation, no NaN-equivalent (`Fix64.MinValue` sentinel) propagation, and no wave that violates the two composition rules. This is how the *next* soft-lock gets found before a player finds it.

---

## 10. Public API

Small on purpose.

```csharp
public static class Simulator
{
    public static SimResult Run(SimInput input);                    // headless, to completion
    public static SimSession Begin(SimInput input);                 // stepped, for the client
}

public sealed class SimSession
{
    public void Step();                                             // exactly one tick
    public void Rally(int creatureId);                              // queued to next tick boundary
    public ref readonly SimState State { get; }                     // read-only view for rendering
    public SimResult? Result { get; }                               // non-null once terminated
    public ulong StateHash();                                       // for the CI gate
}
```

The client drives `Step()` from its own accumulator at 30 Hz and interpolates between states for rendering. The server calls `Run()`. **There is no third path** — auto-resolve and live play differ only in who calls the loop, which is the whole point.

---

## 11. What This Unblocks

| Depends on the core | Why |
|---|---|
| Wave Defense screen | It is the renderer for `SimState` |
| Replay Viewer | Launch-critical per the screen inventory; it is `Run()` plus the same renderer |
| Raid auto-resolve | The whole raiding loop is unplayable without it |
| Anti-cheat | Server validation is re-simulation |
| Campaign balance | 60 waves cannot be tuned without a headless batch runner |
| Trait utility balance | The most under-specified question in the design (Carapace, Litter, Regrow, Screen counter nothing). A headless batch runner answering "what is Regrow worth across 500 waves" is the only honest way to settle it. |

That last row is worth pulling out. Trait utility balance has been the top open design question for several sessions, and it is not answerable by argument. Once the core runs headless, it becomes a measurement: hold the wave set fixed, vary the utility trait, count clears. **Build the batch runner as part of the core, not as a later tool.**

---

## 12. Build Sequence

1. `Fix64`, its test suite, and the Roslyn analyzer banning `float`
2. Entity arrays, tick loop, termination, state hashing
3. Lane geometry and precomputed distance tables
4. Damage, targeting, the six Instincts
5. The eight counter mechanisms and the capacity allocator
6. Replay record and playback
7. Headless batch runner
8. CI determinism gate against a device-farm build
9. Unity integration: renderer, interpolation, trigger-state visuals

Steps 1–2 are one engineer for about a week and gate everything else. Step 8 must land before the first content authoring sprint, because it is what stops the corpus being built on a broken foundation.

---

## 13. Open Questions

1. **Capacity values 1 / 3 / 5.** Placeholder. The bible's examples imply Splash III catches five of eight Skirmishers and Cinder III catches the second split generation, which fits — but Reach III "covers more sky" across a maximum of three lanes makes 5 meaningless for Reach. Reach probably caps at lane count. Confirm before the Codex copy is written, since the Codex must state coverage in concrete terms.
2. **Does capacity from a dead creature free immediately?** Recommend yes — capacity is recomputed each tick from live creatures, so it is automatic. But it means losing your Chill carrier mid-wave releases every slowed Courser at once, which is dramatic and possibly too punishing. Watch it in soft launch.
3. **Sprint's 0.4 s window** is invented here to give Bulwark's shield a concrete degradation rule. Needs a design decision, not an engineering one.
4. **Rally at the tick boundary** means a player tapping at 33 ms granularity gets sub-tick variance in when it lands. Acceptable, but it must be the same on both builds — the input log records the tick, not the timestamp.
5. **Does Pack Sense check species or chassis?** The bible says same-species; an earlier spec said same-chassis. Species is current and the reconciliation supports it, but the string appears both ways in the set.

---

*Companion documents: the bible §4 defines what this computes; the architecture spec defines where it runs; the trait codex defines what the player is told it does.*
