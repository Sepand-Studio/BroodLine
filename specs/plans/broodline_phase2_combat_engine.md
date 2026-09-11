---
status: current
folder: 03-technical
note: >
  Phase 2 design. The combat engine vertical slice: one lane, Courser, Chill.
  Precedes the implementation plan.
---

# Broodline — Phase 2 Design: The Combat Engine

*The vertical slice, and why it is a slice*

> **CURRENT — design, not a plan.** This settles what Phase 2 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. Values live in `broodline_combat_numbers.md`; rules live in
> `broodline_combat_engine.md`. This document owns neither and cites both.

---

## 1. Scope

`broodline_build_order.md` §3 scopes the first engine work as *"one lane, one
raider, five pockets, the counter check."* This design takes that literally.

| | In scope | Deferred |
|---|---|---|
| **Geometry** | One lane, 24 tiles, 5 pockets | Multi-lane, Reach's lane scaling |
| **Raider** | Courser | The other seven |
| **Counter** | Chill | The other seven |
| **Tick loop** | All eight phases, normative order | — |
| **Instincts** | All six predicates and their triggers (§6) | Contrary and the Aberrants |
| **Diagnosis** | access / coverage / placement | — |
| **Batch runner** | Minimal: run N waves, report clear-rate | Balance sweeps over trait sets |
| **Determinism** | Combat scenarios added to the corpus | Replay format |

**The slice is chosen so the loop can be wrong in a visible way.** Courser is
wave 6's designed loss — the beat that teaches the counter system — so it is on
the content critical path for the first hour. Chill exercises the State phase,
the concurrent-capacity family, and §5.1's *exception* path (nearest-first within
range, re-evaluated every tick), which is the subtler of the two assignment
rules. One raider and one counter is the smallest surface on which the
architecture can fail informatively.

**All six Instincts are in scope despite the single raider type.** They are
target-selection predicates over the live target set, not behaviour trees — six
comparators and three triggers — and targeting cannot be exercised honestly
without them. With several Coursers on the board, *lowest HP*, *closest to the
Ark* and *furthest in range* select genuinely different targets. The Aberrants
are deferred because Contrary alone needs the per-tick allied-target census.

**The other seven counters are additive, not a rewrite.** That is the test of
whether the structure at §3 is right.

---

## 2. A contradiction resolved before it is built

Two current documents disagree on Chill's capacity, and the engine cannot be
built against both.

| Source | Says |
|---|---|
| `broodline_combat_engine.md` §5.3 | A global ladder — `capacity[tier]: I = 1, II = 3, III = 5` |
| `broodline_combat_numbers.md` §134 | Chill-specific — `I = 1, II = 2, III = 4` |

`combat_numbers` §115 states it a second way in prose — *"Any Chill stops a
Courser. Chill III stops four of them"* — which agrees with its own table and not
with the ladder.

> **Resolved: capacity is per-trait, read from `combat_numbers`. There is no
> global ladder.**

`combat_engine.md`'s own ownership line disclaims values — *"Does not own: any
value (`broodline_combat_numbers.md`)"* — and its §11.6 already marks the
1/3/5 figures as placeholders, noting they cannot be right for Reach because
there are at most three lanes. Per-trait capacity is what that open question was
reaching for.

**Consequence for the engine:** capacity is a lookup keyed by `(trait, tier)`,
not an index into a shared array. This costs nothing now and avoids a data
migration when Reach caps at lane count.

**Owed edit:** `combat_engine.md` §5.3 should be corrected and §11.6 struck.
Tracked here rather than done silently, because §5.3 is normative text in a
current document.

---

## 3. Architecture

**One `Tick()` calling eight phase functions in the normative order, with each
counter as a named rule at its own phase site.**

`combat_engine.md` §5 pre-rejects the alternative: a unified counter interface
*"would have to be a switch statement wearing a coat, and it would obscure the
thing that actually matters — each counter is a specific rule at a specific
moment."* The eight counters sit at five different phases. Abstracting over them
hides the only structural fact worth preserving.

State lives in dense arrays indexed by stable integer ID and iterated in ID
order, per the Phase 1 global constraints. No dictionaries, no LINQ, no
allocation in the tick loop.

### 3.1 Modules

Each has one purpose, a stated interface, and can be tested without the others.

| Module | Owns | Depends on |
|---|---|---|
| `WaveDef` | The authored wave and its spawn table. Validates the two composition invariants (§5.4) and **throws at load** | — |
| `Lane` | The polyline sampled at authoring time, and the precomputed tile→sample distance table (§2.2) | `Fix64` |
| `SimState` | Dense arrays — raiders, creatures, integrity, tick index. The whole mutable world | `Fix64` |
| `Capacity` | Per-tick recomputation from the live creature set; the `(distance, spawnIndex)` comparator | `SimState` |
| `Phases` | The eight phase functions, in normative order | the above |
| `Diagnosis` | access / coverage / placement, recorded at phase 7 | `SimState`, `Capacity` |
| `Sim` | `Run()` — the loop, termination, the outcome | all of the above |
| `BatchRunner` | Runs N waves headless and reports clear-rate | `Sim` |

**`Diagnosis` is one function with two call sites.** §7 requires the pre-wave
check and the loss diagnosis to be the same computation — one run against the
authored wave, one against a live breach. Building it as two functions is how
they drift.

### 3.2 Data flow

```
WaveDef + seed + deployment  →  Sim.Run()  →  Outcome
                                              { Win | Loss | Stalled,
                                                breaches[],
                                                wholeRunHash }
```

Inputs in, result out. No ambient state, no wall-clock, no callbacks into the
host. This is what makes the same code path serve live play, server
verification and auto-resolve without branching — `combat_engine.md` §1.

---

## 4. What the slice must get right

Three things, and they are the reason the slice exists.

**Tick order is normative.** Movement before targeting, targeting before attack,
death after attack. §4 is explicit that changing the order changes outcomes, so
the order is a behavioural contract and not an implementation detail.

**Capacity is recomputed from scratch every tick.** Never accumulated. §5.3 is
blunt about why: incremental capacity drifts, and drift in a counter system is a
fairness bug. It also settles what happens when a carrier dies mid-wave — its
capacity frees immediately because there is nothing to free.

**Every comparator is a total order.** §2.2 makes distance an exact integer, so
ties are common rather than rare, and an unstable sort over equal keys can order
them differently on two runtimes. `(distance, spawnIndex)` ascending, everywhere.

---

## 5. Error handling and termination

**Composition violations throw at wave load, not during a tick.** The two
invariants — never two raiders answered by the same trait, at most four raider
types — are validated when the wave is loaded. §5.4 wants this in an assertion
rather than a reviewer's memory.

**Every simulation terminates, three ways:**

| Outcome | Condition |
|---|---|
| `Win` | No raiders on the board and none pending |
| `Loss` | Integrity at or below zero. The sim stops that tick; remaining raiders are not resolved |
| `Stalled` | Hard cap at 5,400 ticks, or the stall detector: no raider advanced and no HP changed for 300 consecutive ticks |

**`Stalled` is a content bug, not a gameplay outcome.** It must be unreachable in
a shipped wave. When it happens it is loud, the defender wins, and it is
traceable to a wave ID. Three of the eight raiders had soft-lock versions during
design, so this is a real shape rather than a theoretical one.

---

## 6. Testing

Extends Phase 1's existing layers rather than inventing a parallel structure.

| Layer | Covers |
|---|---|
| **Unit** | Each phase function in isolation. The comparator's total-order property |
| **Golden** | Two pinned whole-run hashes — wave 6 without Chill, and with it (§6.1). Drift fails the build |
| **Fuzz** | Randomised deployments and wave compositions. Asserts termination always, and that `Stalled` never occurs on a valid wave |
| **Enforcement** | The tick order cannot be reordered without a test going red. Follows the pattern established by `EnforcementTests` |
| **Corpus** | Combat scenarios added to the existing set, so `cross-runtime-diff.sh` proves the tick loop bit-identical on CoreCLR and IL2CPP |

### 6.1 The slice is already authored, and it is a matched pair

`broodline_waves_01_12.md` §3 authors wave 6 as *"1 · Defile · Integrity 2 ·
**1 Courser. Nothing else** · Expected roster 5, none carrying Chill."* That is
this slice's scope exactly — one lane, one raider, five pockets — and it was
authored to be so before the engine existed.

The authored payoff is **wave 8** — *"1 Courser · 4 Skirmishers … Teaches: the
answer works. Chill I stops the Courser."* But wave 8 brings Skirmishers, and
Skirmisher is a second raider outside this slice. So the clear-case golden is
**wave 6 run against a Chill-carrying deployment** rather than wave 8.

That is the better test anyway: same authored wave, same seed, one variable
changed. It isolates the counter as the only difference between a loss and a
clear, which wave 8 cannot do because its Skirmishers move too.

| Golden | Wave | Deployment | Expected | Asserts |
|---|---|---|---|---|
| **A** | 6, as authored | 5 creatures, **no Chill** | `Loss` — one breach, integrity 2 → 0 | Diagnosis records `access = false`. The Courser is unanswerable because the trait is absent, not mis-tiered or mis-placed |
| **B** | 6, same seed | Pale carrying **Chill I** | `Win` — Courser slowed to 0.5 t/s, killed before the Ark | Capacity assignment, the State phase, and the counter actually locking |

**Wave 8 is the regression target for Phase 3**, once Skirmisher and Splash
exist. It is the first authored wave that mixes a hard lock with volume, and it
should be pinned as a golden then rather than approximated now.

**A golden pair that spans loss and clear tests the diagnosis, not just the
hash.** Wave 6 exists precisely to make the loss legible — *"Wave Defeat names
Courser and names Chill"* — so a run that loses for the wrong recorded reason is
a real defect the golden should catch.

**The corpus extension is the load-bearing one.** Phase 1's gate currently
proves Fix64 and the toy simulation agree across runtimes. Adding gameplay
without extending it would leave the gate green while the code that actually
decides raids never executes under IL2CPP — the same seam commit `0607cbe`
closed for Fix64's arithmetic. Determinism is not decorative here: raid
verification *is* a re-run comparison, per §2.1.

---

## 7. What this design deliberately does not do

- **No replay format.** §3's format is a few hundred bytes of inputs and is
  cheap to add, but it wants more of the simulation's shape than one raider
  provides. Phase 1 deferred it for the same reason.
- **No server verification path.** The engine being deterministic and headless is
  what makes verification possible; wiring it is `broodline_solo_execution.md`'s.
- **No rendering, no instancing, no degradation ladder.** §9 is explicit that
  simulation is not the performance problem. The engine exposes a per-tick entity
  count so the render layer can degrade before it drops frames; consuming it is
  the client's job.
- **No balance tuning.** The batch runner exists so tuning becomes measurement
  rather than argument. Running the sweeps is Phase 3, once there are enough
  traits to sweep over.

---

## 8. Decisions owed

Neither blocks this slice; both are cheap now and expensive later.

| Decision | Why it matters | Owner |
|---|---|---|
| **Correct `combat_engine.md` §5.3, strike §11.6** | §2 above resolves it, but the normative text still says 1/3/5 | Design |
| **Sprint's 0.4s window** | §11.7 — *"needs a design decision, not an engineering one."* Out of this slice, blocks Bulwark whenever that lands | Design |

---

*Owns: the Phase 2 slice boundary, the module decomposition, and the per-trait
capacity resolution. Does not own: tick order, counter semantics or termination
rules (`broodline_combat_engine.md`), any value (`broodline_combat_numbers.md`),
or build sequencing (`broodline_build_order.md`).*
