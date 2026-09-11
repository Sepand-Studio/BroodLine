---
status: current
folder: 03-technical
note: >
  Phase 3 design. The Unity client: a steppable simulation, Rally, the replay
  format, and a renderer for SimState. Precedes the implementation plan.
---

# Broodline — Phase 3 Design: The Unity Client

*The battlefield, and the three engine pieces it turned out to need*

> **CURRENT — design, not a plan.** This settles what Phase 3 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. Client layering, budgets and the render contract live in
> `broodline_client_architecture.md`; simulation rules live in
> `broodline_combat_engine.md`; values live in `broodline_combat_numbers.md`.
> This document owns none of them and cites all three.

---

## 1. Scope

`broodline_solo_execution.md` §8.2 scopes Phase 3 as *"Renderer for `SimState`,
interpolation, input capture, placeholder art, Codex bottom sheet,"* done when
*"a wave played on device replays bit-identically in xUnit."*

**The Codex sheet is cut.** It is the one item on that line that lives in
`Broodline.UI`, which `broodline_client_architecture.md` §1 forbids from
referencing `Broodline.Sim` at all. It is a second dependency chain — UI → Model
→ config bundle — and the config bundle does not exist until Phase 4's backend,
so building it now means hand-rolling a local config source and throwing it away.
Everything else on the line is one chain, and that chain is what the done-when
actually tests.

| | In scope | Deferred |
|---|---|---|
| **Engine** | `SimRunner.Step()`, Rally, the replay format | A second raider, counter or lane |
| **View** | Renderer for `SimState`, the accumulator and interpolation, placeholder bodies | VFX, damage numbers, Instinct trigger visuals |
| **Input** | Rally, captured at a tick boundary | Deployment UI, pause, speed control |
| **Readability** | HP bars, Chill state, **Rally state**, integrity, breach, outcome and diagnosis | Everything else on the Wave Defense screen |
| **UI assembly** | — | Codex sheet, Wave Defense chrome, the replay viewer screen |
| **Art** | Phase 0's synthetic bodies | Real meshes — blocked on the rig commission |
| **Performance** | The per-tick entity count, displayed | The degradation ladder (§8) |
| **Proof** | Device run → replay artifact → xUnit re-simulation → hash compare | Server verification |

**Deployment is programmatic.** The five creatures are supplied as an array, as
Phase 2's goldens already do. Rally is the only captured input, which is not a
simplification — `broodline_combat_engine.md` §8 makes it the *only* player input
during a wave. "Input capture" and "replay recording" are therefore very nearly
the same task, and §6 builds them as one.

**The slice is chosen so the render contract can be wrong in a visible way.**
Wave 6 is one Courser down one lane: almost nothing to draw. That is the point.
The risk this phase retires is not rendering volume — Phase 0 already measured
that — it is whether a wave driven by a variable frame rate, with a human tap in
it, consumes exactly the inputs its replay claims. One raider is enough surface
for that to fail informatively, and few enough that a failure is legible.

---

## 2. Three findings the design has to absorb

None of these appear in any document. All three were found by reading the Phase 2
branch against `broodline_client_architecture.md`, and each changes what the phase
is.

### 2.1 The renderer cannot consume the engine's public API

`Sim.Run()` constructs `SimState` internally, runs every tick to completion, and
returns only an `Outcome`. There is no `Step()`, and `SimState` never escapes.
**Interpolation needs to read state between ticks and nothing lets it.**

Separately, **Rally does not exist** — zero occurrences across `engine/` and
`tests/`. It is fully specified at `broodline_combat_engine.md` §8 but was not in
Phase 2's scope table, so it was never built. It is the whole of "input capture."

> **Consequence: Phase 3 opens with engine work.** Three tasks — make the
> simulation steppable, add Rally, implement the replay format — before a line of
> Unity. Only then does `Broodline.View` have something to render and something
> to record.

This is Phase 3's work rather than a reopening of Phase 2. Phase 2's design
scoped a headless combat slice and delivered exactly that; the steppable form is
a requirement of the renderer, and the renderer is this phase. `phase_2` merges
to `develop` unchanged before any of it starts.

### 2.2 The corpus is a portability net, not a regression net

The decomposition in §3.1 rewrites the shape of the tick loop. The obvious
question is what catches it if it changes behaviour, and the obvious answer is
wrong.

| Mechanism | What it actually pins |
|---|---|
| `cross-runtime-diff.sh`, 500 scenarios | That CoreCLR and IL2CPP **agree with each other**. Both sides are generated fresh each run and diffed; `corpus-coreclr.txt` is gitignored and there is no committed baseline |
| `EnforcementTests` | That the scenario count is still 500 |
| `GoldenTests` | **Two** pinned literal hashes, marked *"never update these to match new output"* |

A refactor that changes behaviour *identically on both runtimes* passes the gate
in silence. The genuine regression net is two scenarios — wave 6, five creatures,
with and without Chill — while the 500 varied ones, spanning random species,
traits and Instincts, pin nothing at all.

> **Resolved: capture a 500-scenario CoreCLR corpus baseline and commit it,
> before `Sim.cs` is touched. The post-refactor run must reproduce it
> byte-for-byte.**

The emitter already exists, so this costs one task. It converts 500 varied
scenarios into a real regression net for the one refactor that most needs one,
and unlike the goldens it covers species and Instinct combinations no golden
reaches. It is also permanent: every later engine change inherits it.

**This is the acceptance criterion for §3.1 and it is binary.** The suite stays
green and the baseline reproduces exactly, or the decomposition is wrong.

### 2.3 `ref readonly SimState` protects nothing

`broodline_client_architecture.md` §2 has `View` read `ref readonly SimState`.
`SimState` is a **sealed class** whose arrays are `readonly` *references* holding
mutable contents, so a readonly reference to it is not a guarantee — `View` could
write `RaiderHp[0]` and the compiler would allow it. The rule at §11, *"`View`
renders simulation output and never derives it,"* would rest on discipline, which
is precisely what this project's tooling exists to replace.

> **Resolved: `SimRunner` exposes `ReadOnlySpan<T>` accessors and never hands out
> `SimState`.**

Zero-copy, genuinely immutable at the type level, unbanned, and native on
`netstandard2.1` at LangVersion 9. Structural rather than tested, which is the
same standard Phase 1 set with `noEngineReferences: true`.

**Owed edit:** `broodline_client_architecture.md` §2 should be corrected.
Tracked at §9 rather than done silently, because it is normative text in a
document this one does not own.

---

## 3. Architecture

Two new assemblies, and no more. `broodline_client_architecture.md` §1 names six;
`Broodline.UI`, `.Model` and `.Net` are not created, because an empty assembly
invites something to be put in it.

```
Broodline.Sim        the engine (UPM package)      — references nothing
      ▲
      │ ReadOnlySpan accessors, via SimRunner
Broodline.View       renders SimState              — references Sim
      ▲
      │
Broodline.Game       composition root, scene wiring
```

### 3.1 `SimRunner` — decomposing `Run()` without changing it

```csharp
public sealed class SimRunner
{
    public SimRunner(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed);

    public int Tick { get; }
    public int Integrity { get; }
    public ReadOnlySpan<int>   RaiderHp { get; }
    public ReadOnlySpan<Fix64> RaiderProgress { get; }
    public ReadOnlySpan<bool>  RaiderAlive { get; }
    public ReadOnlySpan<bool>  RaiderChilled { get; }
    public ReadOnlySpan<int>   CreatureHp { get; }
    public ReadOnlySpan<int>   CreatureTarget { get; }
    public ReadOnlySpan<int>   CreatureRallyTicks { get; }

    public bool    Step();        // false once terminated
    public Outcome Outcome { get; }   // valid once Step() has returned false
    public bool    TryRally(int creatureId);
    public Replay  Record { get; }
}
```

`Sim.Run()` survives with its exact signature, reimplemented as a thin loop:

```csharp
var r = new SimRunner(wave, lane, deployment, seed);
while (r.Step()) { }
return r.Outcome;
```

Everything local to the old `Run` — the hash, the breach log, the scratch buffer,
the stall detector's counter and last fingerprint — becomes a field. Nothing else
moves.

**The ordering is the entire risk.** In the existing loop `FoldTick` runs *before*
the termination check, and `s.Tick++` happens *after* it. A decomposition that
looks equivalent but shifts either produces a whole-run hash offset by one tick,
on every scenario. §2.2's baseline is what catches it.

### 3.2 Data flow

```
WaveDef + seed + deployment → SimRunner ──Step()──→ Outcome
                                  │                 + Replay
                    ReadOnlySpan accessors
                                  ▼
                          View (accumulator,
                           two snapshots,
                           interpolation)
                                  ▲
                            Rally, at a tick index
```

The engine still never sees a wall-clock delta, never learns rendering exists,
and never calls back into the host. `broodline_combat_engine.md` §1's property —
one code path serving live play, verification and auto-resolve — is preserved,
and §6 extends it to replay.

---

## 4. The tick–render contract

`broodline_client_architecture.md` §2, built as written. This is the phase's real
risk surface; everything else is drawing.

- **`View` owns an accumulator.** Each frame it adds the frame delta and calls
  `Step()` as many times as the elapsed time allows, at exactly 30 Hz.
- **Two snapshots are retained**, previous and current. `View` copies only what
  interpolation needs — raider progress, HP, alive flags — rather than deep-copying
  world state each tick.
- **A dropped frame never drops a tick.** The catch-up loop runs the engine more
  times rather than skipping. A simulation that skipped ticks would produce a run
  the server cannot reproduce, and the player would lose the reward for a wave
  they won.
- **Catch-up is capped at 8 steps per frame.** Without a cap, a long stall
  spirals. With one, a device that cannot keep up runs the wave *slow* — every
  tick still executes, in order, at the same arithmetic. **Slow is recoverable;
  skipped is divergent.** Eight ticks is 267 ms of simulation in one frame, well
  past any stall the phase should tolerate silently; the number is a tuning value
  and the *cap* is the design decision.
- **Rally is captured at a tick boundary.** The tap sets a pending flag consumed
  by the next `Step()`, and is recorded at that tick index. Never a timestamp.

### 4.1 What is drawn, and the line under it

**Legible, not finished.** The done-when needs none of this — a bit-identical
replay does not care whether anything is readable — but a phase that ends without
anyone having *watched* wave 6 defers the first honest look at the game to a phase
that is authoring content against it.

| Drawn | Why it earns its place |
|---|---|
| Lane, five pockets, Ark | The board. Without it nothing else has a position |
| Creature and raider bodies | Phase 0's synthetic meshes, tinted by role |
| HP bars | The damage race is the whole of wave 6's tension |
| Chill, on the affected Courser | The counter is the thing the wave exists to teach. If it is not visible, the slice cannot be judged by eye |
| Rally, on the affected creature | It is the player's **only** input. A tap with no visible consequence is indistinguishable from a tap that was dropped — which is exactly the failure §4 is built to prevent |
| Integrity, and a breach at the Ark | The loss condition, and the moment it happens |
| End panel — result, ticks, integrity, the three-boolean diagnosis | `broodline_client_architecture.md` §9.1: the diagnosis *is* the actionable content, and it is what makes a loss legible rather than arbitrary |
| Debug overlay — tick, entity count, accumulator, catch-up steps, frame time | The contract at §4 is invisible by construction. This is how it is observed failing |

**The entity count is on screen although nothing degrades yet.** §8 defers the
ladder, but `broodline_client_architecture.md` §4 keys every rung off this number,
so surfacing it now means the ladder arrives with a value already proven to be
there and already deterministic.

Not drawn: VFX, damage numbers, Instinct trigger states, death animations, terrain
dressing, and every part of the Wave Defense screen that is not the battlefield
itself.

---

## 5. Rally

`broodline_combat_engine.md` §8: one use per wave, four seconds of doubled attack
speed on one creature, no cooldown, *"a player input with a tick index."* Four
seconds at 30 Hz is **120 ticks**.

**It lands in the State phase, beside Chill.** It is the same shape — a timed
state on a creature — and `Phases.State` is already where timed state is applied.
`SimState` gains `CreatureRallyTicks[]`, counted down there; the runner holds a
single `_rallyUsed` flag. It is folded into the run hash like everything else.

**An invalid Rally is a no-op, not an error.** Already used, unknown id, dead
creature: `TryRally` returns false and records nothing. The replay must contain
only what the simulation actually consumed, and a rejected input that still got
written is the exact shape of a replay that does not reproduce.

Two things §8 leaves open which the engine cannot be built without.

### 5.1 Rally halves the interval last, and the order is normative

`Attacks.IntervalTicks` applies Instinct modifiers as integer multiply-then-divide
— Overwatch `× 5/4`, Last Stand `× 2/3`. Integer division does not commute with a
further halving, so the order is observable in outcomes rather than in style.

Vetch's interval is **exactly 45 ticks**, and Vetch is the starter species:

| Order | Arithmetic | Result |
|---|---|---|
| Rally **last** | `45 × 5 / 4 = 56`, then `÷ 2` | **28 ticks** |
| Rally first | `45 ÷ 2 = 22`, then `× 5 / 4` | 27 ticks |

> **Resolved: Rally halves the interval after Instinct modifiers, always.**

The choice between 28 and 27 is arbitrary; fixing it is not. Instinct modifiers
describe the creature and Rally is a transient applied on top, so applying it last
is also the reading that matches the fiction.

### 5.2 Rally halves the remaining cooldown too

`CreatureNextAttackAt` is a stored absolute tick, computed when the previous
attack resolved and therefore against the un-halved interval. Left alone, Rally
changes nothing until the next attack lands on the old schedule.

For a Hollow — 75-tick interval — that is up to 2.5 seconds of a 4-second window
in which the player's one input per wave does visibly nothing.

> **Resolved: `TryRally` recomputes `CreatureNextAttackAt` by halving the
> remaining cooldown.**

Deterministic either way, one line at the Rally site, and it is what "doubled
attack speed" plainly means to the person pressing the button.

---

## 6. The replay format

`broodline_combat_engine.md` §3's six fields, **binary and fixed-layout,
little-endian, versioned**. Not JSON: *"a few hundred bytes"* is a design
constraint at two raids per player per day (bible §4.9), and JSON breaks it.

**The record is produced by the runner, not observed from outside.** `SimRunner`
appends the Rally as it consumes it. That kills the "the recording disagreed with
what was consumed" bug class structurally, rather than by testing for it
afterwards — and it is the same instinct as `Diagnosis` being one function with
two call sites.

**`Sim.Replay(record)` re-runs it.** Playing and replaying become one code path
differing only in where Rally comes from, which is what
`broodline_client_architecture.md` §9.1 already decided for the replay viewer:
*"Wave Defense gains one flag — input enabled or not — rather than a second
renderer."*

**No `System.IO` in the core.** `Serialize()` returns `byte[]` and `Deserialize()`
takes `ReadOnlySpan<byte>`; the host writes files. The engine's no-dependencies
rule is not negotiable for a convenience.

### 6.1 Two fields the engine cannot currently supply

§3 names two things Phase 2 has no representation for.

| §3 field | Engine today |
|---|---|
| Deployment carries **HP** per creature | `CreatureSpec` has no HP field. `SimState` derives it from `Stats.CreatureHp(species)` |
| Terrain carries **family** | `Lane` has no family. It is a static factory — `Lane.Defile()` |

Storing a value the simulation then ignores is worse than not storing it, because
the two can disagree and nothing says so.

> **Resolved: store what §3 specifies, and validate on load rather than trust.**

A replay whose stored HP disagrees with what `SimState` would construct **throws
at load**, not silently re-simulated into something else — the same treatment
`WaveDef.Validate()` already gives a composition violation, and for the same
reason. Terrain
family becomes an enum — one member, `Defile` — used as the lookup key, with lane
count, pocket count and lane length stored as check fields asserted against what
the lookup builds.

This keeps faith with a normative document, makes the redundancy self-checking
instead of dangerous, and means the HP field is already there when HP becomes
variable — carry-over damage, or a trait that modifies it.

---

## 7. Testing

Three layers. The first two extend existing structure; the third is new because
it proves something nothing existing proves.

| Layer | Covers | Runs |
|---|---|---|
| **xUnit, headless** | Runner equivalence against the committed baseline (§2.2). Rally semantics — the 28-tick case at §5.1, the cooldown halving at §5.2, the no-op cases. Replay round-trip: record → serialize → deserialize → re-run → identical `Outcome.Hash` | Existing CI |
| **Unity PlayMode** | The §4 contract. A synthetic 200 ms frame stall produces exactly 6 catch-up steps and no dropped tick; a 500 ms stall runs slow at the 8-step cap rather than skipping; a Rally tapped mid-frame records the **next** tick index, not the current render time | Editor, and CI where available |
| **Device round-trip** | **The done-when.** Wave 6 played on device writes `replay.bin` and its outcome hash; the artifact is pulled off device; an xUnit test re-runs it on CoreCLR and asserts an identical hash | Manual, per release |

**Layer 3 is a different proof from the corpus**, and the distinction is the
reason the phase exists. `cross-runtime-diff.sh` proves CoreCLR and IL2CPP agree
on generated scenarios run headlessly — that is **arithmetic portability**. Layer
3 proves a wave run through a renderer, at a variable frame rate, with a human
tap in it, consumed the inputs its replay claims — that is **frame-pacing
correctness**. Neither implies the other, and the second is what §4 can get wrong.

Phase 0's `CorpusPlayerHarness` is precedent for device-side artifact emission and
the mechanism should follow it rather than invent a second one.

---

## 8. What this design deliberately does not do

- **No second raider, counter or lane.** Engine scope stays Phase 2's. Wave 8
  remains the regression target for whichever phase adds Skirmisher and Splash.
- **No degradation ladder.** `broodline_client_architecture.md` §4's four rungs
  key off entity count, and wave 6 has one Courser — there is nothing to degrade.
  The per-tick entity count is *displayed* so the ladder has a number to be built
  against, which is the part that has to exist first.
- **No `Broodline.UI`.** No Codex sheet, no Wave Defense chrome, no replay viewer
  screen, no deployment UI.
- **No real art.** Phase 0's synthetic bodies are reused, at the same triangle,
  bone and material cost the commission was briefed with. **Phase 0's gate stays
  open regardless** — it requires real meshes from a commission not yet placed,
  and nothing in this phase changes that.
- **No server verification.** The replay format is what makes it possible; wiring
  it is Phase 5's, per `broodline_solo_execution.md` §8.2.
- **No Addressables, no persistence, no outbox.** Those are
  `broodline_client_architecture.md` §§6–8 and they arrive with the backend.

---

## 9. Decisions owed

Two edits to documents this one does not own, and one design question. None
blocks the phase; all three are cheap now.

| Decision | Why it matters | Owner |
|---|---|---|
| **Correct `client_architecture` §2's `ref readonly SimState`** | §2.3 resolves it for this phase, but the normative text still specifies a shape that guarantees nothing | Design |
| **Record Rally's two resolutions into `combat_engine.md` §8** | §5.1 and §5.2 are engine-observable rules, and §8 currently specifies neither. They belong in the document that owns counter and input semantics | Design |
| **Re-verify the iPad rules at `client_architecture` §10** | §12.3 asks for it before layout work, and this phase puts a build on a device for the first time since Phase 0. Cheap to answer here, expensive to discover during the UI phase | Design |

---

*Owns: the Phase 3 slice boundary, the steppable-simulation decomposition, Rally's
two arithmetic resolutions, the replay format's validate-don't-trust rule, the
corpus baseline requirement, and the assembly set created now. Does not own: the
render contract, budgets or layering (`broodline_client_architecture.md`), tick
order, counter semantics or input specification (`broodline_combat_engine.md`),
any value (`broodline_combat_numbers.md`), or phase sequencing
(`broodline_solo_execution.md`).*
