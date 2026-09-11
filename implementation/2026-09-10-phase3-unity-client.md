# Phase 3 — Unity Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wave 6 plays on a device, renders legibly, records its inputs as a replay, and that replay re-simulates bit-identically in xUnit.

**Architecture:** Three engine tasks come first, because the renderer cannot consume the engine as it stands — `Sim.Run()` is a closed loop that never lets `SimState` escape, and Rally does not exist. A `SimRunner` extracts the loop body so it can be stepped one tick at a time and read through `ReadOnlySpan<T>` accessors; Rally arrives as an expiry tick read at the Attack phase; the replay record is produced *by* the runner rather than observed from outside. Only then do two Unity assemblies appear — `Broodline.View`, which owns an accumulator and interpolates between two snapshots, and `Broodline.Game`, the composition root.

**Tech Stack:** C# `netstandard2.1` (engine), .NET 10 (tests), xUnit, Unity 6000.6.0f1 with IL2CPP, the Phase 1 core (`Fix64`, `Rng`, `Hash`, `Corpus`) and the Phase 2 combat engine.

## Global Constraints

Inherited from Phases 1 and 2 and enforced by existing tooling — the Cecil float scan, `BannedSymbols.txt`, and the empty-reference asmdef. Every one of these is already a build failure, not a convention:

- **No floating point anywhere in the core.** Fixed-point `Fix64` at Q32.32.
- **No `System.Math`, no `UnityEngine.Mathf`** in the core.
- **Fixed timestep, 30 Hz.** The simulation never sees a wall-clock delta.
- **Seeded PRNG, one stream per wave.** Never `System.Random`.
- **Deterministic iteration order.** No `Dictionary`, no `HashSet`, no LINQ.
- **No allocation in the tick loop.** Buffers are sized at wave load.
- **The core has no dependencies** — no Unity, no `System.IO`, nothing beyond primitives and arrays.
- `.meta` files are committed, **including a directory's own meta, which lives one level up**.

**These constraints bind `engine/` only.** `Broodline.View` is Unity code and uses `float` and `double` freely — the accumulator at Task 7 is a `double` on purpose. That is not a violation and not an oversight: the whole point of a fixed timestep is that variable-rate float maths lives on the render side of the boundary and reaches the simulation only as a decision to call `Step()` or not. The Cecil scan targets `Broodline.Sim` and nothing else.

From `specs/plans/broodline_phase3_unity_client.md`, and normative here:

- **Rally halves the attack interval *after* Instinct modifiers.** Integer division does not commute; the order is a rule, not a style (§5.1).
- **Rally halves the remaining attack cooldown** when it is applied (§5.2).
- **An invalid Rally is a no-op, not an error.** The replay records only what the simulation consumed.
- **A replay stores what `combat_engine` §3 specifies and validates it on load**, throwing rather than silently re-simulating something else (§6.1).
- **A dropped frame never drops a tick.** Catch-up is capped; a device that cannot keep up runs the wave slow.
- **Input is recorded at a tick index, never a timestamp.**
- **`View` never receives `SimState`.** Only `ReadOnlySpan<T>` accessors on `SimRunner`.

### Values, copied verbatim

| | |
|---|---|
| Tick rate | 30 Hz — `Stats.TicksPerSecond` |
| Rally | One use per wave, **4 seconds = 120 ticks**, doubled attack speed, one creature, no cooldown |
| Catch-up cap | **8 steps per frame** — 267 ms of simulation in one frame |
| Lane | 24 tiles, 5 pockets beside tiles 6, 10, 13, 17, 20 — `Lane.Defile()` |
| Hard tick cap | 5,400 ticks — `Stats.HardTickCap` |
| Stall detector | 300 ticks — `Stats.StallTicks` |
| Corpus | 500 scenarios — `Corpus.ScenarioCount` |
| | *(the golden hashes below are the POST-Rally values. Task 4 re-baselines them deliberately when `CreatureRallyUntil` joins the state vector; the pre-Rally pair was `4169973534116968225` / `434502781243215433`.)* |
| Golden A hash | `2495532238167386945` — wave 6, no Chill, `Loss` |
| Golden B hash | `13482666686023521257` — wave 6, Chill I, `Win` |

Creature attack intervals, in ticks — needed because Task 4's arithmetic turns on them:

| Species | Vetch | Ember | Skitter | Hollow | Loam | Pale |
|---|---|---|---|---|---|---|
| Interval | **45** | 51 | 12 | 75 | 36 | 33 |

**This plan writes no second raider, no second counter, no multi-lane geometry, no degradation ladder, no `Broodline.UI`, no server verification and no real art.** Those are named at the design doc §8.

---

## Two corrections this plan fed back into the design

Writing the tasks found two claims in the design that would not survive contact. Both were corrected in `specs/plans/broodline_phase3_unity_client.md` rather than carried as debt, because that document was still in an open PR when they were found. They are recorded here so the change is traceable rather than silent.

**Rally is not a State-phase countdown.** A countdown decremented at phase 2 and read at phase 5 is off by one against a 120-tick window, and it is not the codebase's idiom — `CreatureAcquireAt`, `CreatureNextAttackAt` and `CreatureBusyUntil` are all absolute expiry ticks read where they are used. Rally is `CreatureRallyUntil[]`, read at the Attack phase, and **no phase function is added**. Smaller than the design assumed, not larger.

**The accumulator is tested in EditMode, not PlayMode.** `WaveClock` has no `UnityEngine` dependency, so a scene buys nothing that feeding a large delta does not. Frame timings in those tests are multiples of a tick rather than milliseconds: `0.200` and `1.0/30.0` are both inexact in binary, and a test written against 200 ms asserts which side of a floating-point tie the division lands on rather than the behaviour.

---

## File structure

| File | Responsibility |
|---|---|
| `engine/Runtime/Combat/SimRunner.cs` | The tick loop as a steppable object; the read-only surface; Rally; the replay record |
| `engine/Runtime/Combat/Replay.cs` | The six-field record, its binary codec, and its load-time validation |
| `engine/Runtime/Combat/Sim.cs` | **Modified** — `Run()` becomes a thin loop over `SimRunner`; `Replay()` is added |
| `engine/Runtime/Combat/SimState.cs` | **Modified** — gains `CreatureRallyUntil[]` |
| `engine/Runtime/Combat/Attacks.cs` | **Modified** — Rally halves the interval, last |
| `engine/Runtime/Combat/WaveDef.cs` | **Modified** — gains `ForId(int)` |
| `engine/Runtime/Combat/Ids.cs` | **Modified** — gains `enum Terrain` |
| `tests/engine/corpus-baseline.txt` | 500 pinned scenario hashes. Tracked, and the regression net for Task 2 |
| `client/Assets/View/Broodline.View.asmdef` | The render assembly. References `Broodline.Sim` |
| `client/Assets/View/WaveClock.cs` | The accumulator, the catch-up cap, Rally capture at a tick boundary |
| `client/Assets/View/WaveSnapshot.cs` | The two interpolation buffers |
| `client/Assets/View/WaveView.cs` | Lane, pockets, bodies, interpolated transforms |
| `client/Assets/View/WaveHud.cs` | HP bars, Chill, Rally, integrity, breach, end panel, debug overlay |
| `client/Assets/View/SyntheticCreature.cs` | **Moved** from `Benchmark/` |
| `client/Assets/View/BoneAnimator.cs` | **Moved** from `Benchmark/` |
| `client/Assets/Game/Broodline.Game.asmdef` | Composition root |
| `client/Assets/Game/WaveRunner.cs` | Wires runner, clock, view and HUD; writes the replay artifact |
| `client/Assets/View/Tests/...` | EditMode tests for `WaveClock` and `WaveSnapshot` |
| `tests/engine/Combat/SimRunnerTests.cs` | Runner equivalence, the read-only surface |
| `tests/engine/Combat/RallyTests.cs` | The 28-tick case, the cooldown halving, the no-op cases |
| `tests/engine/Combat/ReplayTests.cs` | Round-trip, size, validation |
| `tests/engine/Combat/DeviceReplayTests.cs` | The done-when: re-simulate the device artifact |

---

### Task 0: Prerequisites

Phase 3 builds on the merged Phase 2. Confirm it is green before adding to it.

**Files:**
- Modify: none. This is a gate.

**Interfaces:**
- Consumes: the Phase 2 combat engine on `develop`.
- Produces: nothing.

- [x] **Step 1: Confirm the branch and the toolchain**

```bash
git rev-parse --abbrev-ref HEAD && ./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: `phase_3`, five `ok` lines, `exit=0`.

- [x] **Step 2: Confirm the suite is green and record the count**

```bash
dotnet test Broodline.sln --nologo
```

Expected: all pass. **Write the total down.** Recorded 2026-09-10: **111**. Later tasks add to it, and a drop means something was deleted rather than extended.

- [x] **Step 3: Confirm the cross-runtime gate passes**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first.

**If this fails, stop.** Task 2 rewrites the shape of the tick loop, and starting from a red gate means never knowing which change broke it.

---

### Task 1: Commit the corpus regression baseline

**Do this before touching `Sim.cs`. It is the only thing that will catch Task 2 getting the decomposition subtly wrong.**

`cross-runtime-diff.sh` generates both sides fresh and diffs them against each other, so it proves CoreCLR and IL2CPP *agree* — not that either still produces what it produced yesterday. `corpus-coreclr.txt` is gitignored. The only pinned behaviour in the repo is two golden hashes covering two scenarios. This task pins all 500.

**Files:**
- Create: `tests/engine/corpus-baseline.txt`
- Create: `tests/engine/CorpusBaselineTests.cs`
- Create: `implementation/scripts/emit-corpus-baseline.sh`
- Modify: `tests/engine/Broodline.Sim.Tests.csproj`

**Interfaces:**
- Consumes: `Corpus.RunScenario(int)` → `ulong`, `Corpus.ScenarioCount` = 500.
- Produces: `tests/engine/corpus-baseline.txt`, 500 lines of `index hash`, copied beside the test binary at build.

- [x] **Step 1: Make the baseline file visible to the test binary**

`tests/engine/Broodline.Sim.Tests.csproj` — add this `ItemGroup` before the closing `</Project>`:

```xml
  <ItemGroup>
    <Content Include="corpus-baseline.txt">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [x] **Step 2: Write the emitter**

`implementation/scripts/emit-corpus-baseline.sh`:

```bash
#!/usr/bin/env bash
# Regenerates tests/engine/corpus-baseline.txt from the CURRENT engine.
#
# This is a DELIBERATE re-baseline, not a routine step. Running it makes
# CorpusBaselineTests pass by definition, so it must only be run when the
# behaviour change is intended and understood - a balance change, or a state
# vector that genuinely grew. If a test went red and you are here looking for
# the button that makes it green, you are in the wrong place.
set -euo pipefail
cd "$(dirname "$0")/../.."
dotnet test Broodline.sln --nologo \
  --filter "FullyQualifiedName~CorpusBaselineTests.Emit" \
  -e BROODLINE_EMIT_BASELINE=1
echo "--- regenerated: tests/engine/corpus-baseline.txt ---"
git --no-pager diff --stat -- tests/engine/corpus-baseline.txt
```

```bash
chmod +x implementation/scripts/emit-corpus-baseline.sh
```

- [x] **Step 3: Write the test that reads it, and the emitter behind an env guard**

`tests/engine/CorpusBaselineTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Broodline.Sim;

namespace Broodline.Sim.Tests
{
    /// The corpus's regression net.
    ///
    /// cross-runtime-diff.sh generates BOTH sides fresh and diffs them, so it
    /// proves the two runtimes agree with each other - not that either still
    /// agrees with yesterday. A refactor that changed behaviour identically on
    /// both would pass it in silence. This file is the other half: 500 hashes
    /// pinned in a tracked file, covering species, trait and Instinct
    /// combinations no golden reaches.
    public class CorpusBaselineTests
    {
        // System.IO is banned in the ENGINE, not in the tests. This file is in
        // tests/ and reads a build artifact; nothing here ships.
        private static string BaselinePath =>
            Path.Combine(AppContext.BaseDirectory, "corpus-baseline.txt");

        private static string SourcePath()
        {
            // Walk up from bin/Debug/net10.0 to the project folder, so the
            // emitter writes the TRACKED file rather than the copied one.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Broodline.Sim.Tests.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "corpus-baseline.txt");
        }

        private static string Render()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
                sb.Append(i).Append(' ').Append(Corpus.RunScenario(i)).Append('\n');
            return sb.ToString();
        }

        [Fact]
        public void Emit()
        {
            // Only does anything under emit-corpus-baseline.sh. As an ordinary
            // test run it is a no-op, so `dotnet test` can never silently
            // rewrite the thing it is supposed to be checking against.
            if (Environment.GetEnvironmentVariable("BROODLINE_EMIT_BASELINE") != "1") return;
            File.WriteAllText(SourcePath(), Render());
        }

        [Fact]
        public void EveryScenarioMatchesTheCommittedBaseline()
        {
            Assert.True(File.Exists(BaselinePath),
                "corpus-baseline.txt was not copied to the output directory - check the csproj Content item.");

            var expected = File.ReadAllLines(BaselinePath);
            Assert.Equal(Corpus.ScenarioCount, expected.Length);

            var drifted = new List<string>();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                string actual = i + " " + Corpus.RunScenario(i);
                if (actual != expected[i]) drifted.Add("  line " + i + ": expected '" + expected[i] + "', got '" + actual + "'");
                if (drifted.Count == 10) break;
            }

            Assert.True(drifted.Count == 0,
                "The engine no longer reproduces the committed corpus.\n" +
                "If this is an INTENDED behaviour change, run\n" +
                "  ./implementation/scripts/emit-corpus-baseline.sh\n" +
                "and say in the commit message what changed and why.\n" +
                "First differences:\n" + string.Join("\n", drifted));
        }
    }
}
```

- [x] **Step 4: Generate the baseline**

```bash
touch tests/engine/corpus-baseline.txt
./implementation/scripts/emit-corpus-baseline.sh
wc -l tests/engine/corpus-baseline.txt
```

Expected: `500 tests/engine/corpus-baseline.txt`.

- [x] **Step 5: Prove the net actually catches drift**

Temporarily perturb one thing inside the tick loop. In `engine/Runtime/Combat/Attacks.cs`, change `Damage` to return `damage + 1` for `Species.Hollow`, then:

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~CorpusBaselineTests"
```

Expected: **FAIL**, naming specific line numbers. Revert the perturbation and confirm PASS.

**This step is the task.** A baseline that would not have caught the thing it exists to catch is worse than none, because it looks like protection.

- [x] **Step 6: Commit**

```bash
git add tests/engine/corpus-baseline.txt tests/engine/CorpusBaselineTests.cs \
        tests/engine/Broodline.Sim.Tests.csproj implementation/scripts/emit-corpus-baseline.sh
git commit -m "test: pin the corpus, so a refactor cannot change behaviour quietly

cross-runtime-diff.sh generates both sides fresh and diffs them against
each other. It proves CoreCLR and IL2CPP agree - not that either still
produces what it produced yesterday - and corpus-coreclr.txt is
gitignored, so nothing in the repo pinned it. The only pinned behaviour
was two golden hashes over two scenarios.

Task 2 rewrites the shape of the tick loop. This is what will catch it
getting that subtly wrong, across 500 scenarios whose species, traits and
Instincts no golden reaches. Proven to fail against a one-point damage
perturbation before being trusted.

Re-baselining is deliberate and scripted, and the emitter refuses to run
outside that script so an ordinary test run can never rewrite it."
```

---

### Task 2: `SimRunner` — decompose `Run()` without changing it

**Files:**
- Create: `engine/Runtime/Combat/SimRunner.cs`
- Create: `engine/Runtime/Combat/SimRunner.cs.meta`
- Modify: `engine/Runtime/Combat/Sim.cs`
- Modify: `tests/engine/Combat/CombatEnforcementTests.cs`
- Create: `tests/engine/Combat/SimRunnerTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Phases`, `Stats`, `Hash`, `Outcome`, `Breach`, `Result`, `WaveDef`, `Lane`, `CreatureSpec`.
- Produces:
  - `sealed class SimRunner`, constructor `(WaveDef, Lane, CreatureSpec[], ulong seed)`
  - `int Tick { get; }`, `Result Result { get; }`, `bool Done { get; }`
  - `bool Step()` — `false` once terminated
  - `Outcome Outcome { get; }` — valid once `Step()` has returned `false`
  - `Sim.Run(...)` keeps its exact existing signature and behaviour

**The ordering is the entire risk.** In the existing loop `FoldTick` runs *before* the termination check, and `s.Tick++` happens *after* it. Shifting either produces a whole-run hash offset by one tick on every scenario.

- [x] **Step 1: Write the failing test**

`tests/engine/Combat/SimRunnerTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimRunnerTests
    {
        [Fact]
        public void SteppedToCompletion_ReproducesGoldenA()
        {
            // Asserted against the PINNED LITERAL, not against Sim.Run - once
            // Run delegates to SimRunner, comparing the two proves nothing.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(4169973534116968225UL, r.Outcome.Hash);
            Assert.Equal(Result.Loss, r.Outcome.Result);
            Assert.Equal(1, r.Outcome.BreachCount);
            Assert.False(r.Outcome.Breaches[0].Access);
        }

        [Fact]
        public void SteppedToCompletion_ReproducesGoldenB()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(434502781243215433UL, r.Outcome.Hash);
            Assert.Equal(Result.Win, r.Outcome.Result);
            Assert.Equal(2, r.Outcome.IntegrityRemaining);
        }

        [Fact]
        public void TrueReturnsEqualOutcomeTicks()
        {
            // The loop increments Tick only on a step that does NOT terminate,
            // so the count of true returns is exactly Outcome.Ticks. This is
            // the invariant that catches an off-by-one in the extraction - the
            // single likeliest way to get this wrong.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            int steps = 0;
            while (r.Step()) steps++;

            Assert.Equal(r.Outcome.Ticks, steps);
        }

        [Fact]
        public void StepAfterTermination_IsFalseAndHarmless()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }
            ulong hash = r.Outcome.Hash;

            Assert.False(r.Step());
            Assert.False(r.Step());
            Assert.Equal(hash, r.Outcome.Hash);
        }

        [Fact]
        public void TickAdvancesOneAtATime()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            Assert.Equal(0, r.Tick);
            r.Step();
            Assert.Equal(1, r.Tick);
            r.Step();
            Assert.Equal(2, r.Tick);
        }
    }
}
```

- [x] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~SimRunnerTests"
```

Expected: compile failure — `SimRunner` does not exist.

- [x] **Step 3: Write `SimRunner.cs`**

`engine/Runtime/Combat/SimRunner.cs`:

```csharp
namespace Broodline.Sim.Combat
{
    /// The tick loop as an object that can be stepped, so a renderer can read
    /// the world between ticks and interpolate across it.
    ///
    /// This is a DECOMPOSITION of what Sim.Run already did, not a second
    /// implementation of it. Sim.Run is reimplemented as a thin loop over this
    /// class precisely so there is never a second tick loop to keep in sync -
    /// solo_execution section 9.3 rejects that shape by name.
    ///
    /// Two orderings inside Step are load-bearing and are the reason this class
    /// has a test asserting the count of true returns:
    ///   - FoldTick runs BEFORE the termination check, so the terminating tick
    ///     is folded into the hash like any other.
    ///   - Tick is incremented AFTER it, so a terminating tick does not advance
    ///     the counter and Outcome.Ticks is the number of COMPLETED ticks.
    public sealed class SimRunner
    {
        private readonly SimState _s;
        private readonly Breach[] _log;
        private readonly int[] _scratch;

        private Hash _hash;
        private int _breachCount;
        private int _stallTicks;
        private long _lastFingerprint = long.MinValue;

        private Result _result = Result.Running;
        private Outcome _outcome;
        private bool _done;

        public SimRunner(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            wave.Validate();

            _s = new SimState(wave, lane, deployment);
            _log = new Breach[wave.Spawns.Length];
            _scratch = new int[wave.Spawns.Length];

            _hash = Hash.Create();
            _hash.Add(wave.Id);
            _hash.Add(unchecked((long)seed));
        }

        public int Tick => _s.Tick;
        public Result Result => _result;
        public bool Done => _done;
        public Outcome Outcome => _outcome;

        /// Advances exactly one tick. Returns false once the wave has
        /// terminated, after which it is a harmless no-op.
        public bool Step()
        {
            if (_done) return false;

            // The hard cap is checked BEFORE the phases, exactly as the
            // original while-condition did.
            if (_s.Tick >= Stats.HardTickCap) { Finish(Result.Stalled); return false; }

            Phases.Spawn(_s);            // 1
            Phases.State(_s, _scratch);  // 2
            Phases.Movement(_s);         // 3
            Phases.Targeting(_s);        // 4
            Phases.Attack(_s);           // 5
            Phases.Death(_s);            // 6
            Phases.Breach(_s, _log, ref _breachCount);   // 7
            _result = Phases.Resolve(_s); // 8

            FoldTick(ref _hash, _s);

            if (_result != Result.Running) { Finish(_result); return false; }

            long fingerprint = Fingerprint(_s);
            if (fingerprint == _lastFingerprint)
            {
                _stallTicks++;
                if (_stallTicks >= Stats.StallTicks) { Finish(Result.Stalled); return false; }
            }
            else
            {
                _stallTicks = 0;
                _lastFingerprint = fingerprint;
            }

            _s.Tick++;
            return true;
        }

        private void Finish(Result result)
        {
            _result = result;
            _hash.Add((int)result);
            _hash.Add(_s.Integrity);

            _outcome = new Outcome
            {
                Result = result,
                Ticks = _s.Tick,
                IntegrityRemaining = _s.Integrity,
                Breaches = _log,
                BreachCount = _breachCount,
                Hash = _hash.Value
            };
            _done = true;
        }

        /// Folds the whole visible world into the run hash, every tick. A
        /// whole-run hash that only sampled the end state would let a
        /// mid-simulation divergence that self-corrects pass the gate.
        private static void FoldTick(ref Hash hash, SimState s)
        {
            hash.Add(s.Tick);
            hash.Add(s.Integrity);
            for (int r = 0; r < s.RaiderCount; r++)
            {
                hash.Add(s.RaiderHp[r]);
                hash.Add(s.RaiderProgress[r].Raw);
                hash.Add(s.RaiderAlive[r] ? 1 : 0);
                hash.Add(s.RaiderChilled[r] ? 1 : 0);
            }
            for (int c = 0; c < s.CreatureCount; c++)
            {
                hash.Add(s.CreatureHp[c]);
                hash.Add(s.CreatureTarget[c]);
                hash.Add(s.CreaturePocket[c]);
            }
        }

        /// Cheap "has anything moved or been hurt" summary for the stall
        /// detector. Deliberately not the run hash: it must not include the
        /// tick index, or nothing would ever compare equal.
        private static long Fingerprint(SimState s)
        {
            long f = s.Integrity;
            for (int r = 0; r < s.RaiderCount; r++)
                f = unchecked(f * 31 + s.RaiderProgress[r].Raw + s.RaiderHp[r]);
            for (int c = 0; c < s.CreatureCount; c++)
                f = unchecked(f * 31 + s.CreatureHp[c]);
            return f;
        }
    }
}
```

- [x] **Step 4: Create the `.meta` file**

Unity requires one per asset. **The existing metas in this repo are two lines** — `fileFormatVersion` and `guid`, nothing else — and Unity fills in the importer block on first load. Match that rather than writing the verbose form:

```bash
python3 - <<'PY'
import uuid, pathlib
p = pathlib.Path("engine/Runtime/Combat/SimRunner.cs.meta")
p.write_text("fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)
print(p.read_text())
PY
```

- [x] **Step 5: Reduce `Sim.Run` to a loop over it**

`engine/Runtime/Combat/Sim.cs` — replace the whole file body. `FoldTick` and `Fingerprint` move to `SimRunner` and are deleted here.

```csharp
namespace Broodline.Sim.Combat
{
    /// The simulation. Inputs in, result out - no ambient state, no
    /// wall-clock, no callbacks into the host. That is what lets the same code
    /// path serve live play, server verification and auto-resolve without
    /// branching (combat_engine section 1), and auto-resolve is the identical
    /// path with no player input rather than a stat roll.
    ///
    /// The loop itself lives in SimRunner so a renderer can step it. This
    /// entry point is preserved unchanged for every headless caller - the
    /// corpus, the batch runner, server verification - and there is exactly
    /// one tick loop underneath both.
    public static class Sim
    {
        public static Outcome Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            var runner = new SimRunner(wave, lane, deployment, seed);
            while (runner.Step()) { }
            return runner.Outcome;
        }
    }
}
```

- [x] **Step 5b: Repoint the tick-order enforcement scan**

**Folded back from execution: this step was missing and the suite went red at Step 7.**

`tests/engine/Combat/CombatEnforcementTests.cs` scans a source file for the eight `Phases.X(` calls in order. It reads `Sim.cs`, and the phases have just moved out of it — so the scan fails with *"Sim.Run no longer calls Phases.Spawn("* even though nothing about the order changed.

Point it at the file the loop now lives in. Rename the test to `TheTickLoop_StillCallsTheEightPhasesInNormativeOrder`, change the path to `SimRunner.cs`, and change the message from `"Sim.Run no longer calls "` to `"the tick loop no longer calls "`.

Then add the half that keeps it honest — a scan pointed at one file proves nothing if a second loop can exist in another:

```csharp
        [Fact]
        public void TheTickLoopLivesInExactlyOnePlace()
        {
            // Phase 3's whole claim about the decomposition: Sim.Run is a loop
            // over SimRunner, not a second implementation of it.
            // solo_execution section 9.3 rejects "a second model of the game
            // that has to stay in sync with the first" on cost grounds, and two
            // tick loops is exactly that shape.
            //
            // It is also what keeps the order scan above honest. A scan pointed
            // at one file proves nothing if a second loop can exist in another,
            // so this is the other half of that test rather than a separate
            // concern.
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Sim.cs");
            string source = File.ReadAllText(path);

            Assert.False(source.Contains("Phases."),
                "Sim.cs calls a phase function directly. The tick loop lives in " +
                "SimRunner; Sim.Run drives it. A second loop here would have to " +
                "be kept in step with that one forever, and the order-enforcement " +
                "scan only reads SimRunner.cs.");
        }
```

Re-prove the relocated scan the way Phase 2 proved the original — swap `Phases.Movement` and `Phases.Targeting` in `SimRunner.Step`, confirm **FAIL** with *"is out of normative order"*, and revert.

- [x] **Step 6: Run the new tests**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~SimRunnerTests"
```

Expected: PASS, five tests.

- [x] **Step 7: Run everything, including the baseline**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS, with the total at least what Task 0 Step 2 recorded, plus five.

**`CorpusBaselineTests` and `GoldenTests` must both be green with no re-baselining.** That is the acceptance criterion for this task and it is binary. If the baseline drifted, the decomposition changed behaviour — read the first differing line, and look at the two orderings called out at the top of `SimRunner`. **Do not run `emit-corpus-baseline.sh` to make this pass.**

- [x] **Step 8: Run the cross-runtime gate**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`.

- [x] **Step 9: Commit**

```bash
git add engine/Runtime/Combat/SimRunner.cs engine/Runtime/Combat/SimRunner.cs.meta \
        engine/Runtime/Combat/Sim.cs tests/engine/Combat/SimRunnerTests.cs
git commit -m "refactor: make the simulation steppable, without changing it

Sim.Run constructed SimState internally, ran every tick to completion and
returned only an Outcome. A renderer needs to read the world between
ticks to interpolate across them, and nothing let it.

The loop body moves to SimRunner and Sim.Run becomes four lines over it,
so there is one tick loop rather than two to keep in sync. Two orderings
carry the risk and are called out in the file: FoldTick runs before the
termination check, and Tick increments after it.

Proven by the two pinned goldens and by the 500-scenario baseline
reproducing byte-for-byte - no hash moved, which is the whole claim."
```

---

### Task 3: The read-only surface

`SimState` is a sealed class whose arrays are `readonly` *references* holding mutable contents, so handing `View` a `ref readonly SimState` would guarantee nothing — `View` could write `RaiderHp[0]` and the compiler would allow it. `client_architecture` §11's rule *"View renders simulation output and never derives it"* has to be structural, not remembered.

**Files:**
- Modify: `engine/Runtime/Combat/SimRunner.cs`
- Modify: `tests/engine/Combat/SimRunnerTests.cs`

**Interfaces:**
- Produces, all on `SimRunner`:
  - `int Integrity { get; }`, `int RaiderCount { get; }`, `int CreatureCount { get; }`
  - `ReadOnlySpan<RaiderType> RaiderType { get; }`
  - `ReadOnlySpan<int> RaiderHp { get; }`
  - `ReadOnlySpan<Fix64> RaiderProgress { get; }`
  - `ReadOnlySpan<bool> RaiderAlive { get; }`
  - `ReadOnlySpan<bool> RaiderChilled { get; }`
  - `ReadOnlySpan<Species> CreatureSpecies { get; }`
  - `ReadOnlySpan<int> CreatureHp { get; }`
  - `ReadOnlySpan<int> CreaturePocket { get; }`
  - `ReadOnlySpan<int> CreatureTarget { get; }`
  - `Lane Lane { get; }`, `int LaneTiles { get; }`

- [x] **Step 1: Write the failing test**

Append to `tests/engine/Combat/SimRunnerTests.cs`, inside the class:

```csharp
        [Fact]
        public void ReadOnlySurface_TracksTheLiveState()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

            Assert.Equal(2, r.Integrity);
            Assert.Equal(5, r.CreatureCount);
            Assert.Equal(24, r.LaneTiles);
            Assert.Equal(0, r.RaiderCount);          // the Courser spawns at t=90
            Assert.Equal(260, r.CreatureHp[0]);      // Vetch
            Assert.Equal(Species.Loam, r.CreatureSpecies[4]);

            // Step past the spawn tick and the raider surface populates.
            while (r.Tick < 91 && r.Step()) { }
            Assert.Equal(1, r.RaiderCount);
            Assert.True(r.RaiderAlive[0]);
            Assert.False(r.RaiderChilled[0]);        // no Chill in this deployment

            // Not an equality against 220. Spawn is phase 1 and Attack is phase
            // 5, so a creature can acquire and fire on the Courser's own spawn
            // tick - pinning "undamaged" here would be pinning phase ordering
            // in the wrong test.
            Assert.InRange(r.RaiderHp[0], 1, Stats.RaiderHp(RaiderType.Courser));
        }

        [Fact]
        public void SimRunner_ExposesNoPathToMutableState()
        {
            // The guarantee is structural, so it is asserted structurally: no
            // public member of SimRunner may hand out SimState or a raw array.
            // A ReadOnlySpan property cannot be written through; a T[] property
            // can, and that is the mistake this test exists to prevent.
            var t = typeof(SimRunner);
            foreach (var p in t.GetProperties())
            {
                Assert.False(p.PropertyType == typeof(SimState),
                    "SimRunner." + p.Name + " hands out SimState, which View could write through.");
                Assert.False(p.PropertyType.IsArray,
                    "SimRunner." + p.Name + " hands out a raw array. Use ReadOnlySpan<T>.");
            }
            foreach (var m in t.GetMethods())
            {
                Assert.False(m.ReturnType == typeof(SimState),
                    "SimRunner." + m.Name + " returns SimState.");
            }
        }
```

- [x] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~SimRunnerTests"
```

Expected: compile failure — `SimRunner` has no `Integrity`.

- [x] **Step 3: Add the accessors**

In `engine/Runtime/Combat/SimRunner.cs`, add `using System;` at the top of the file and insert these members after the existing `public Outcome Outcome => _outcome;`:

```csharp
        // --- The read-only surface. ---
        //
        // client_architecture section 2 specifies "ref readonly SimState".
        // That guarantees nothing: SimState is a sealed CLASS whose arrays are
        // readonly REFERENCES holding mutable contents, so a readonly reference
        // to it still permits RaiderHp[0] = 0. ReadOnlySpan<T> cannot be
        // written through, so the rule "View renders and never derives" holds
        // by the type system rather than by anyone remembering it.
        //
        // Zero-copy: the implicit T[] to ReadOnlySpan<T> conversion wraps the
        // existing array. Nothing is allocated and nothing is copied.

        public int Integrity => _s.Integrity;
        public int RaiderCount => _s.RaiderCount;
        public int CreatureCount => _s.CreatureCount;
        public Lane Lane => _s.Lane;
        public int LaneTiles => _s.Lane.Tiles;

        public ReadOnlySpan<RaiderType> RaiderType => _s.RaiderType;
        public ReadOnlySpan<int> RaiderHp => _s.RaiderHp;
        public ReadOnlySpan<Fix64> RaiderProgress => _s.RaiderProgress;
        public ReadOnlySpan<bool> RaiderAlive => _s.RaiderAlive;
        public ReadOnlySpan<bool> RaiderChilled => _s.RaiderChilled;

        public ReadOnlySpan<Species> CreatureSpecies => _s.CreatureSpecies;
        public ReadOnlySpan<int> CreatureHp => _s.CreatureHp;
        public ReadOnlySpan<int> CreaturePocket => _s.CreaturePocket;
        public ReadOnlySpan<int> CreatureTarget => _s.CreatureTarget;
```

- [x] **Step 4: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS. No hash moves — this task adds accessors and touches no arithmetic.

- [x] **Step 5: Commit**

```bash
git add engine/Runtime/Combat/SimRunner.cs tests/engine/Combat/SimRunnerTests.cs
git commit -m "feat: a read-only surface View cannot write through

client_architecture section 2 has View read 'ref readonly SimState'.
That guarantees nothing - SimState is a sealed class whose readonly
arrays hold mutable contents, so a readonly reference to it still permits
RaiderHp[0] = 0, and the rule 'View renders and never derives' would rest
on discipline.

ReadOnlySpan<T> accessors instead, zero-copy, and a reflection test that
fails if any public member of SimRunner ever hands out SimState or a raw
array. Structural rather than remembered, which is the standard Phase 1
set with noEngineReferences: true.

Owed edit to client_architecture section 2, tracked in the Phase 3
design's section 9."
```

---

### Task 4: Rally

The only player input during a wave. `combat_engine` §8: one use, four seconds of doubled attack speed on one creature, no cooldown, *"a player input with a tick index."*

**Read the correction near the top of this document before starting.** Rally is an absolute expiry tick read at the Attack phase, not a countdown in the State phase. No phase function is added and the normative phase order is untouched.

**This task re-baselines the goldens and the corpus.** Folding `CreatureRallyUntil` into the run hash changes every hash, including runs where Rally is never used, because the state vector genuinely grew. That is expected and it is the *second* time the baseline earns its keep: Task 2 proved a refactor changed nothing, and this proves a change was confined to what it was supposed to change.

**Files:**
- Modify: `engine/Runtime/Combat/SimState.cs`
- Modify: `engine/Runtime/Combat/Attacks.cs`
- Modify: `engine/Runtime/Combat/SimRunner.cs`
- Modify: `tests/engine/Combat/GoldenTests.cs`
- Modify: `tests/engine/corpus-baseline.txt`
- Create: `tests/engine/Combat/RallyTests.cs`

**Interfaces:**
- Produces:
  - `SimState.CreatureRallyUntil` — `readonly int[]`, zeroed at construction
  - `SimRunner.RallyTicks` — `const int` = 120
  - `SimRunner.TryRally(int creatureId)` → `bool`
  - `SimRunner.RallyUsed { get; }` → `bool`
  - `SimRunner.CreatureRallyUntil { get; }` → `ReadOnlySpan<int>`

- [x] **Step 1: Write the failing test**

`tests/engine/Combat/RallyTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class RallyTests
    {
        private static SimRunner Fresh() =>
            new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                          GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

        [Fact]
        public void RallyHalvesTheIntervalAfterInstinctModifiers()
        {
            // The reason this is a rule and not a style: integer division does
            // not commute. Vetch is 45 ticks and is the STARTER species.
            //   Rally last : 45 * 5 / 4 = 56, then / 2 = 28
            //   Rally first: 45 / 2     = 22, then * 5 / 4 = 27
            // combat_numbers section 3 gives Vetch 1.5s = 45 ticks exactly, so
            // this is live for the most common creature in the game.
            var deployment = GoldenTests.DeploymentWithoutChill();
            deployment[0] = new CreatureSpec
            {
                Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Overwatch
            };

            var plain = new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);
            Assert.Equal(56, Attacks.IntervalTicks(plain, 0));      // Overwatch only

            plain.CreatureRallyUntil[0] = 1;                        // tick 0 < 1, so rallied
            Assert.Equal(28, Attacks.IntervalTicks(plain, 0));      // NOT 27
        }

        [Fact]
        public void RallyHalvesTheRemainingCooldown()
        {
            // NextAttackAt is an absolute tick already computed against the
            // un-halved interval. Left alone, Rally on a Hollow - 75 ticks -
            // does visibly nothing for up to 2.5s of its 4s window.
            var deployment = GoldenTests.DeploymentWithoutChill();
            deployment[0] = new CreatureSpec
            {
                Species = Species.Hollow, Pocket = 0, Instinct = Instinct.Vanguard
            };
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(), deployment, GoldenTests.Seed);

            // Step until the Hollow has actually fired once, so there IS a
            // cooldown to halve. Stepping to a fixed tick is not safe: a
            // creature whose NextAttackAt is still 0 has a negative "remaining"
            // and the assertion below would be checking nothing.
            while (r.Step() && r.CreatureNextAttackAt[0] <= r.Tick) { }
            Assert.True(r.CreatureNextAttackAt[0] > r.Tick,
                "the Hollow never attacked, so there is no cooldown to halve");

            int remaining = r.CreatureNextAttackAt[0] - r.Tick;
            Assert.True(r.TryRally(0));
            Assert.Equal(r.Tick + remaining / 2, r.CreatureNextAttackAt[0]);
        }

        [Fact]
        public void RallyLastsExactlyOneHundredAndTwentyTicks()
        {
            var r = Fresh();
            r.Step();                                     // Tick is now 1
            Assert.True(r.TryRally(0));
            Assert.Equal(r.Tick + 120, r.CreatureRallyUntil[0]);

            // Active on the tick it was granted and on the 120th, gone after.
            var plain = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                     GoldenTests.DeploymentWithoutChill());
            plain.CreatureRallyUntil[0] = 121;
            plain.Tick = 120;
            Assert.Equal(22, Attacks.IntervalTicks(plain, 0));   // Vetch 45 -> 22, rallied
            plain.Tick = 121;
            Assert.Equal(45, Attacks.IntervalTicks(plain, 0));   // expired
        }

        [Fact]
        public void RallyIsOneUsePerWave()
        {
            var r = Fresh();
            Assert.True(r.TryRally(0));
            Assert.True(r.RallyUsed);
            Assert.False(r.TryRally(1));
            Assert.False(r.TryRally(0));
        }

        [Fact]
        public void InvalidRallyIsANoOpAndNotAnError()
        {
            // The replay must record only what the simulation CONSUMED. An
            // input that was rejected but still written is exactly the shape of
            // a replay that does not reproduce.
            var r = Fresh();
            Assert.False(r.TryRally(-1));
            Assert.False(r.TryRally(99));
            Assert.False(r.RallyUsed);

            // A dead creature cannot be rallied, and the attempt is not spent.
            var r2 = Fresh();
            while (r2.Step()) { }
            Assert.False(r2.TryRally(0));      // terminated
        }

        [Fact]
        public void RallyChangesTheOutcomeHash()
        {
            // If it did not, Rally would be invisible to the determinism gate
            // and to server verification - an input the server could not check.
            var a = Fresh();
            while (a.Step()) { }

            var b = Fresh();
            b.Step();
            b.TryRally(0);
            while (b.Step()) { }

            Assert.NotEqual(a.Outcome.Hash, b.Outcome.Hash);
        }
    }
}
```

- [x] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~RallyTests"
```

Expected: compile failure — `CreatureRallyUntil` does not exist.

- [x] **Step 3: Add the state**

`engine/Runtime/Combat/SimState.cs` — add the field beside the other creature arrays, after `CreatureRepositioned`:

```csharp
        public readonly int[] CreatureRallyUntil;    // absolute tick; 0 == never
```

And in the constructor, beside the other `new`s:

```csharp
            CreatureRallyUntil = new int[CreatureCount];
```

The per-creature init loop needs nothing — `0` already means "never rallied", because `Tick < 0` is false for every tick.

- [x] **Step 4: Apply it in `Attacks.IntervalTicks`**

`engine/Runtime/Combat/Attacks.cs` — replace the whole `IntervalTicks` method. The existing version returns from inside the switch, which leaves nowhere to apply Rally afterwards.

```csharp
        /// Ticks between attacks, after Instinct modifiers and then Rally.
        ///
        /// Overwatch: -20% attack speed, so the interval grows by 5/4.
        /// Last Stand below 25% HP: +50% attack speed, so it shrinks by 2/3.
        /// Rally: doubled attack speed, so the interval halves.
        ///
        /// RALLY IS APPLIED LAST AND THE ORDER IS NORMATIVE. Integer division
        /// does not commute with the ratios above. Vetch is 45 ticks and is the
        /// starter species: under Overwatch, halving last gives 45*5/4 = 56 ->
        /// 28, and halving first gives 45/2 = 22 -> 27. The choice between 28
        /// and 27 is arbitrary; fixing it is not. Instinct describes the
        /// creature and Rally is a transient laid on top, so last is also the
        /// reading that matches the fiction.
        public static int IntervalTicks(SimState s, int c)
        {
            int interval = Stats.CreatureIntervalTicks(s.CreatureSpecies[c]);

            switch (s.CreatureInstinct[c])
            {
                case Instinct.Overwatch:
                    interval = interval * 5 / 4;
                    break;

                case Instinct.LastStand:
                    if (BelowFraction(s, c, 1, 4)) interval = interval * 2 / 3;
                    break;
            }

            if (s.Tick < s.CreatureRallyUntil[c]) interval /= 2;

            return interval;
        }
```

- [x] **Step 5: Add `TryRally` and its surface to `SimRunner`**

In `engine/Runtime/Combat/SimRunner.cs`, add the field beside the others:

```csharp
        private bool _rallyUsed;
```

Add to the read-only surface, beside the other creature accessors:

```csharp
        public ReadOnlySpan<int> CreatureRallyUntil => _s.CreatureRallyUntil;
        public ReadOnlySpan<int> CreatureNextAttackAt => _s.CreatureNextAttackAt;
        public bool RallyUsed => _rallyUsed;
```

And the method, after `Step()`:

```csharp
        /// combat_engine section 8: four seconds of doubled attack speed on one
        /// creature, one use per wave, no cooldown. 4s at 30Hz.
        public const int RallyTicks = 4 * Stats.TicksPerSecond;

        /// The only player input during a wave. Returns false - harmlessly -
        /// for every invalid case rather than throwing.
        ///
        /// An invalid Rally MUST be a no-op, because the replay records only
        /// what the simulation consumed. An input that was rejected but still
        /// written to the record is precisely the shape of a replay that does
        /// not reproduce, and it would surface as a rejected raid for an honest
        /// player rather than as a bug anyone could find.
        public bool TryRally(int creatureId)
        {
            if (_done) return false;
            if (_rallyUsed) return false;
            if (creatureId < 0 || creatureId >= _s.CreatureCount) return false;
            if (!_s.CreatureAlive(creatureId)) return false;

            _rallyUsed = true;
            _s.CreatureRallyUntil[creatureId] = _s.Tick + RallyTicks;

            // Halve the REMAINING cooldown too. NextAttackAt is an absolute
            // tick already computed against the un-halved interval, so without
            // this a Hollow's 75-tick interval swallows most of the 120-tick
            // window and the player's one input per wave looks dropped.
            int remaining = _s.CreatureNextAttackAt[creatureId] - _s.Tick;
            if (remaining > 0)
                _s.CreatureNextAttackAt[creatureId] = _s.Tick + remaining / 2;

            return true;
        }
```

- [x] **Step 6: Fold it into the run hash**

In `SimRunner.FoldTick`, add to the creature loop:

```csharp
                hash.Add(s.CreatureRallyUntil[c]);
```

**This changes every hash in the project.** It is deliberate: an input the hash cannot see is an input server verification cannot check.

- [x] **Step 7: Run and watch the goldens fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: `RallyTests` PASS; `GoldenTests` and `CorpusBaselineTests` **FAIL** on changed hashes. That is the correct result at this step — confirm the failures are *only* those hash assertions and nothing behavioural. `GoldenTests` must still report `Loss`/`Win`, one breach, `Access == false`, integrity 2.

- [x] **Step 8: Re-baseline deliberately, and record the new goldens**

Read the two new hashes out of the test output (**2026-09-10: A = 2495532238167386945, B = 13482666686023521257**) — `GoldenTests` writes them with `_out.WriteLine`:

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~GoldenTests" --logger "console;verbosity=detailed" | grep "Golden"
```

Replace `GoldenAHash` and `GoldenBHash` in `tests/engine/Combat/GoldenTests.cs` with the printed values, and update the pinning comment:

```csharp
        // Pinned 2026-09-10. Re-baselined when Rally joined the state vector -
        // CreatureRallyUntil is folded every tick, so every hash moved even on
        // runs where Rally is never used. A change here is a balance change or
        // a bug - never update these to match new output without knowing which.
```

Then regenerate the corpus baseline:

```bash
./implementation/scripts/emit-corpus-baseline.sh
```

- [x] **Step 9: Run everything green, then the cross-runtime gate**

```bash
dotnet test Broodline.sln --nologo && ./implementation/scripts/cross-runtime-diff.sh
```

Expected: all PASS; `PASS: 500 scenarios agree`.

- [x] **Step 10: Commit**

```bash
git add engine/Runtime/Combat/SimState.cs engine/Runtime/Combat/Attacks.cs \
        engine/Runtime/Combat/SimRunner.cs tests/engine/Combat/RallyTests.cs \
        tests/engine/Combat/GoldenTests.cs tests/engine/corpus-baseline.txt
git commit -m "feat: Rally, the only player input during a wave

Specified at combat_engine section 8 and never built - it was not in
Phase 2's scope table, so 'input capture' had nothing to capture.

Stored as an absolute expiry tick read at the Attack phase, not as a
countdown in the State phase as the design assumed. A countdown is off by
one against a 120-tick window because State runs at phase 2 and Attack
reads it at phase 5, and absolute expiry ticks are already the idiom here
- CreatureAcquireAt, CreatureNextAttackAt and CreatureBusyUntil all work
that way. No phase function added; the normative order is untouched.

Two resolutions combat_engine section 8 does not carry, both
outcome-visible. Rally halves the interval AFTER Instinct modifiers,
because integer division does not commute: Vetch under Overwatch is 28
ticks one way and 27 the other, and Vetch is the starter species. And it
halves the remaining cooldown, or Rally on a Hollow does nothing for 2.5s
of its 4s window.

Every hash moved. CreatureRallyUntil is folded each tick because an input
the hash cannot see is an input server verification cannot check. Goldens
and the corpus baseline re-based deliberately, with the verdicts, breach
count and diagnosis confirmed unchanged first."
```

---

### Task 5: The replay format

`combat_engine` §3's six fields, binary, fixed-layout, little-endian, versioned. **A few hundred bytes** is a design constraint at two raids per player per day (bible §4.9), not a description — Step 1 asserts it.

**Files:**
- Create: `engine/Runtime/Combat/Replay.cs` and its `.meta`
- Modify: `engine/Runtime/Combat/Ids.cs` — add `enum Terrain`
- Modify: `engine/Runtime/Combat/WaveDef.cs` — add `ForId`
- Modify: `engine/Runtime/Combat/SimRunner.cs` — produce the record
- Modify: `engine/Runtime/Combat/Sim.cs` — add `Replay`
- Create: `tests/engine/Combat/ReplayTests.cs`

**Interfaces:**
- Produces:
  - `enum Terrain { Defile = 0 }`
  - `sealed class Replay` with `int WaveId`, `ulong Seed`, `Terrain Terrain`, `int LaneCount`, `int PocketCount`, `int LaneTiles`, `CreatureSpec[] Deployment`, `int[] DeploymentHp`, `int RallyTick`, `int RallyCreature`, `string EngineVersion`
  - `byte[] Replay.Serialize()`
  - `static Replay Replay.Deserialize(ReadOnlySpan<byte>)` — throws `ReplayFormatException`
  - `void Replay.Validate()` — throws `ReplayFormatException`
  - `Lane Replay.BuildLane()`
  - `class ReplayFormatException : Exception`
  - `static WaveDef WaveDef.ForId(int id)` — throws `WaveCompositionException` on unknown
  - `SimRunner.Record { get; }` → `Replay`
  - `static Outcome Sim.Replay(Replay record)`

> **What shipped differs, and this list is kept as written for the record.** Three
> changes, each made for a reason found during execution or review:
>
> - `SimRunner.Record { get; }` **does not exist.** Handing out the live `Replay`
>   made the "TryRally is the only writer" claim false — its fields are public and
>   mutable, so a caller could stamp a rally onto a run that never had one and
>   serialize a structurally valid forgery. It shipped as `byte[]
>   SerializeRecord()` plus `Replay ReadRecord()`, which returns a deep copy.
>   Every `live.Record.…` in the code blocks below reads `live.SerializeRecord()`
>   or `live.ReadRecord().…` in the file that actually exists.
> - `Replay.Validate()` is now two methods. `ValidateFormat()` checks what no
>   engine version can disagree about and runs INSIDE `Deserialize`, so there is
>   no unvalidated `Replay` for a caller to forget about; `Validate()` checks the
>   engine version FIRST and then everything that is this engine's — enum widths,
>   the authored wave, the lane's geometry — because those bounds reject a
>   legitimate record from a LATER engine and reporting that as corruption sends
>   the reader after a forgery that is not there.
> - The deployment's field bounds live in `Deployments.Problem`, shared with the
>   `SimRunner` constructor. They were only in the codec, so the simulation would
>   run a wave with `Pocket = 178956971` to completion and write a record it could
>   not itself read back.

- [x] **Step 1: Write the failing test**

`tests/engine/Combat/ReplayTests.cs`:

```csharp
using System;
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class ReplayTests
    {
        private static SimRunner Fresh() =>
            new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                          GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

        [Fact]
        public void RoundTrip_WithoutRally_ReproducesTheOutcome()
        {
            var live = Fresh();
            while (live.Step()) { }

            var bytes = live.Record.Serialize();
            var replayed = Broodline.Sim.Combat.Sim.Replay(Replay.Deserialize(bytes));

            Assert.Equal(live.Outcome.Hash, replayed.Hash);
            Assert.Equal(live.Outcome.Result, replayed.Result);
            Assert.Equal(live.Outcome.Ticks, replayed.Ticks);
        }

        [Fact]
        public void RoundTrip_WithRally_ReproducesTheOutcome()
        {
            // The one that matters. A replay is the INPUTS, so if Rally is not
            // recorded at the tick it was consumed, this diverges.
            var live = Fresh();
            live.Step();
            live.Step();
            Assert.True(live.TryRally(2));
            while (live.Step()) { }

            Assert.Equal(2, live.Record.RallyTick);
            Assert.Equal(2, live.Record.RallyCreature);

            var bytes = live.Record.Serialize();
            var replayed = Broodline.Sim.Combat.Sim.Replay(Replay.Deserialize(bytes));

            Assert.Equal(live.Outcome.Hash, replayed.Hash);
        }

        [Fact]
        public void RejectedRallyIsNotRecorded()
        {
            var live = Fresh();
            Assert.False(live.TryRally(99));
            while (live.Step()) { }

            Assert.Equal(-1, live.Record.RallyTick);
            Assert.Equal(-1, live.Record.RallyCreature);
        }

        [Fact]
        public void ARecordIsAFewHundredBytes()
        {
            // combat_engine section 3 makes this a constraint, not a
            // description: bible section 4.9 gives every raid a replay and
            // there are two raids per player per day. Asserting it is what
            // stops someone reaching for JSON later.
            var live = Fresh();
            while (live.Step()) { }
            Assert.InRange(live.Record.Serialize().Length, 1, 512);
        }

        [Fact]
        public void DeserializeRejectsCorruption()
        {
            var live = Fresh();
            while (live.Step()) { }
            var bytes = live.Record.Serialize();

            Assert.Throws<ReplayFormatException>(() => Replay.Deserialize(new byte[] { 1, 2, 3 }));

            var badMagic = (byte[])bytes.Clone();
            badMagic[0] ^= 0xFF;
            Assert.Throws<ReplayFormatException>(() => Replay.Deserialize(badMagic));
        }

        [Fact]
        public void ValidateRejectsHpThatDisagreesWithTheStatTable()
        {
            // combat_engine section 3 stores HP per deployed creature; the
            // engine derives it from Stats. Storing a value the simulation then
            // ignores is worse than not storing it, because the two can
            // disagree and nothing says so. So it is checked on load.
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.Record.Serialize());

            record.DeploymentHp[0] = 9999;
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void ValidateRejectsGeometryThatDisagreesWithTheTerrain()
        {
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.Record.Serialize());

            record.PocketCount = 4;          // Defile has 5
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void EngineVersionIsStored()
        {
            var live = Fresh();
            while (live.Step()) { }
            Assert.Equal(SimVersion.Value, live.Record.EngineVersion);
        }
    }
}
```

- [x] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~ReplayTests"
```

Expected: compile failure — `Replay` does not exist.

- [x] **Step 3: Add `Terrain` to `Ids.cs`**

Append inside the namespace in `engine/Runtime/Combat/Ids.cs`:

```csharp
    /// The terrain family a lane is built from. combat_engine section 3 stores
    /// this in every replay, so the values are persisted and must never be
    /// renumbered - adding a family appends.
    ///
    /// One member for now. Lane geometry is otherwise reachable only through a
    /// static factory, which a replay cannot name.
    public enum Terrain { Defile = 0 }
```

- [x] **Step 4: Add `WaveDef.ForId`**

Append to the `WaveDef` class in `engine/Runtime/Combat/WaveDef.cs`:

```csharp
        /// Replays store a wave ID rather than the wave, so there has to be a
        /// lookup. Throwing on an unknown ID is the point: a replay naming a
        /// wave this engine does not have must fail loudly at load rather than
        /// re-simulate something else.
        public static WaveDef ForId(int id)
        {
            if (id == 6) return Wave6();
            throw new WaveCompositionException("no authored wave with id " + id);
        }
```

- [x] **Step 5: Write `Replay.cs`**

`engine/Runtime/Combat/Replay.cs`:

```csharp
using System;
using System.Text;

namespace Broodline.Sim.Combat
{
    public class ReplayFormatException : Exception
    {
        public ReplayFormatException(string message) : base(message) { }
    }

    /// combat_engine section 3: "A replay is NOT a recording of state. It is
    /// the inputs." Six fields, a few hundred bytes, and the engine version,
    /// which is load-bearing - section 9.4 of solo_execution renders a
    /// superseded replay's stored outcome with a notice rather than
    /// re-simulating it.
    ///
    /// Binary and fixed-layout rather than JSON, because "a few hundred bytes"
    /// is a constraint: bible section 4.9 gives every raid a replay and there
    /// are two raids per player per day.
    ///
    /// No System.IO. Serialize returns bytes and Deserialize takes them; the
    /// host writes files. The engine's no-dependencies rule is not traded for
    /// a convenience.
    public sealed class Replay
    {
        private const uint Magic = 0x50524C42;   // "BLRP" little-endian
        private const ushort FormatVersion = 1;

        public int WaveId;
        public ulong Seed;

        public Terrain Terrain;
        public int LaneCount;
        public int PocketCount;
        public int LaneTiles;

        public CreatureSpec[] Deployment;
        /// Per section 3's "five creature snapshots: ... HP". The engine
        /// derives HP from Stats, so this is redundant TODAY and checked
        /// against that derivation at Validate. It is stored anyway because
        /// section 3 says so and because carry-over damage would need it.
        public int[] DeploymentHp;

        public int RallyTick = -1;       // -1 == absent
        public int RallyCreature = -1;

        public string EngineVersion = SimVersion.Value;

        /// Rebuilds the lane the run used. The stored geometry is not trusted -
        /// it is checked against what the family produces, at Validate.
        public Lane BuildLane()
        {
            switch (Terrain)
            {
                case Terrain.Defile: return Lane.Defile();
                default: throw new ReplayFormatException("unknown terrain family " + (int)Terrain);
            }
        }

        /// Everything the record asserts about itself, checked against what
        /// this engine would build. A replay that disagrees is rejected rather
        /// than re-simulated into something else - the same treatment
        /// WaveDef.Validate gives a composition violation, and for the same
        /// reason.
        public void Validate()
        {
            if (Deployment == null || DeploymentHp == null ||
                Deployment.Length != DeploymentHp.Length)
                throw new ReplayFormatException("deployment and HP arrays disagree");

            if (Deployment.Length > Stats.DeploymentCap)
                throw new ReplayFormatException(
                    "deployment of " + Deployment.Length + " exceeds the cap of " + Stats.DeploymentCap);

            var lane = BuildLane();
            if (LaneTiles != lane.Tiles)
                throw new ReplayFormatException(
                    "lane length " + LaneTiles + " disagrees with " + Terrain + "'s " + lane.Tiles);
            if (PocketCount != lane.PocketCount)
                throw new ReplayFormatException(
                    "pocket count " + PocketCount + " disagrees with " + Terrain + "'s " + lane.PocketCount);
            if (LaneCount != WaveDef.ForId(WaveId).LaneCount)
                throw new ReplayFormatException("lane count disagrees with the authored wave");

            for (int c = 0; c < Deployment.Length; c++)
            {
                int expected = Stats.CreatureHp(Deployment[c].Species);
                if (DeploymentHp[c] != expected)
                    throw new ReplayFormatException(
                        "creature " + c + " stores HP " + DeploymentHp[c] +
                        " but " + Deployment[c].Species + " starts at " + expected);
            }

            if (RallyTick < -1) throw new ReplayFormatException("negative rally tick");
            if ((RallyTick < 0) != (RallyCreature < 0))
                throw new ReplayFormatException("rally tick and creature disagree about being absent");
            if (RallyCreature >= Deployment.Length)
                throw new ReplayFormatException("rally names creature " + RallyCreature + ", out of range");
        }

        public byte[] Serialize()
        {
            var version = Encoding.UTF8.GetBytes(EngineVersion ?? "");
            if (version.Length > 255) throw new ReplayFormatException("engine version string too long");

            int size = 4 + 2 + 1 + version.Length
                     + 4 + 8 + 4 + 4 + 4 + 4
                     + 4 + Deployment.Length * 8 * 4
                     + 4 + 4;

            var b = new byte[size];
            int i = 0;

            PutU32(b, ref i, Magic);
            PutU16(b, ref i, FormatVersion);
            b[i++] = (byte)version.Length;
            Buffer.BlockCopy(version, 0, b, i, version.Length); i += version.Length;

            PutI32(b, ref i, WaveId);
            PutU64(b, ref i, Seed);
            PutI32(b, ref i, (int)Terrain);
            PutI32(b, ref i, LaneCount);
            PutI32(b, ref i, PocketCount);
            PutI32(b, ref i, LaneTiles);

            PutI32(b, ref i, Deployment.Length);
            for (int c = 0; c < Deployment.Length; c++)
            {
                var d = Deployment[c];
                PutI32(b, ref i, (int)d.Species);
                PutI32(b, ref i, (int)d.Trait1);
                PutI32(b, ref i, d.Tier1);
                PutI32(b, ref i, (int)d.Trait2);
                PutI32(b, ref i, d.Tier2);
                PutI32(b, ref i, (int)d.Instinct);
                PutI32(b, ref i, d.Pocket);
                PutI32(b, ref i, DeploymentHp[c]);
            }

            PutI32(b, ref i, RallyTick);
            PutI32(b, ref i, RallyCreature);

            return b;
        }

        public static Replay Deserialize(ReadOnlySpan<byte> b)
        {
            int i = 0;
            if (b.Length < 11) throw new ReplayFormatException("too short to be a replay");
            if (GetU32(b, ref i) != Magic) throw new ReplayFormatException("bad magic");

            ushort format = GetU16(b, ref i);
            if (format != FormatVersion)
                throw new ReplayFormatException("format version " + format + ", expected " + FormatVersion);

            int versionLen = b[i++];
            if (i + versionLen > b.Length) throw new ReplayFormatException("truncated engine version");
            var r = new Replay { EngineVersion = Encoding.UTF8.GetString(b.Slice(i, versionLen)) };
            i += versionLen;

            r.WaveId      = GetI32(b, ref i);
            r.Seed        = GetU64(b, ref i);
            r.Terrain     = (Terrain)GetI32(b, ref i);
            r.LaneCount   = GetI32(b, ref i);
            r.PocketCount = GetI32(b, ref i);
            r.LaneTiles   = GetI32(b, ref i);

            int count = GetI32(b, ref i);
            if (count < 0 || count > Stats.DeploymentCap)
                throw new ReplayFormatException("deployment count " + count + " out of range");
            if (i + count * 8 * 4 + 8 > b.Length) throw new ReplayFormatException("truncated deployment");

            r.Deployment = new CreatureSpec[count];
            r.DeploymentHp = new int[count];
            for (int c = 0; c < count; c++)
            {
                r.Deployment[c] = new CreatureSpec
                {
                    Species  = (Species)GetI32(b, ref i),
                    Trait1   = (Trait)GetI32(b, ref i),
                    Tier1    = GetI32(b, ref i),
                    Trait2   = (Trait)GetI32(b, ref i),
                    Tier2    = GetI32(b, ref i),
                    Instinct = (Instinct)GetI32(b, ref i),
                    Pocket   = GetI32(b, ref i)
                };
                r.DeploymentHp[c] = GetI32(b, ref i);
            }

            r.RallyTick     = GetI32(b, ref i);
            r.RallyCreature = GetI32(b, ref i);

            return r;
        }

        // Explicit little-endian shifts rather than BitConverter, which is
        // host-endian. Nothing in the layout may depend on the machine.
        private static void PutU16(byte[] b, ref int i, ushort v)
        { b[i++] = (byte)v; b[i++] = (byte)(v >> 8); }

        private static void PutU32(byte[] b, ref int i, uint v)
        { b[i++] = (byte)v; b[i++] = (byte)(v >> 8); b[i++] = (byte)(v >> 16); b[i++] = (byte)(v >> 24); }

        private static void PutI32(byte[] b, ref int i, int v) => PutU32(b, ref i, unchecked((uint)v));

        private static void PutU64(byte[] b, ref int i, ulong v)
        { PutU32(b, ref i, (uint)v); PutU32(b, ref i, (uint)(v >> 32)); }

        private static ushort GetU16(ReadOnlySpan<byte> b, ref int i)
        { ushort v = (ushort)(b[i] | (b[i + 1] << 8)); i += 2; return v; }

        private static uint GetU32(ReadOnlySpan<byte> b, ref int i)
        {
            uint v = (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
            i += 4; return v;
        }

        private static int GetI32(ReadOnlySpan<byte> b, ref int i) => unchecked((int)GetU32(b, ref i));

        private static ulong GetU64(ReadOnlySpan<byte> b, ref int i)
        {
            ulong lo = GetU32(b, ref i);
            ulong hi = GetU32(b, ref i);
            return lo | (hi << 32);
        }
    }
}
```

- [x] **Step 6: Create its `.meta`**

```bash
python3 - <<'PY'
import uuid, pathlib
p = pathlib.Path("engine/Runtime/Combat/Replay.cs.meta")
p.write_text("fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)
PY
```

- [x] **Step 7: Have `SimRunner` produce the record**

In `engine/Runtime/Combat/SimRunner.cs`, add the field:

```csharp
        private readonly Replay _record;
```

Build it at the end of the constructor:

```csharp
            // The record is produced BY the runner rather than observed from
            // outside. That is what kills the "the recording disagreed with
            // what was consumed" bug class structurally: TryRally is the only
            // thing that writes the Rally fields, and it writes them at the
            // moment it accepts the input.
            var hp = new int[deployment.Length];
            for (int c = 0; c < deployment.Length; c++)
                hp[c] = Stats.CreatureHp(deployment[c].Species);

            _record = new Replay
            {
                WaveId = wave.Id,
                Seed = seed,
                Terrain = Terrain.Defile,
                LaneCount = wave.LaneCount,
                PocketCount = lane.PocketCount,
                LaneTiles = lane.Tiles,
                Deployment = (CreatureSpec[])deployment.Clone(),
                DeploymentHp = hp
            };
```

Expose it:

```csharp
        /// The inputs this run consumed. Complete from construction except for
        /// Rally, which TryRally appends when - and only when - it accepts one.
        public Replay Record => _record;
```

And in `TryRally`, immediately before `return true;`:

```csharp
            _record.RallyTick = _s.Tick;
            _record.RallyCreature = creatureId;
```

- [x] **Step 8: Add `Sim.Replay`**

Append to the `Sim` class in `engine/Runtime/Combat/Sim.cs`:

```csharp
        /// Re-runs a recorded replay. Playing and replaying are ONE code path
        /// differing only in where Rally comes from, which is what
        /// client_architecture section 9.1 already decided for the replay
        /// viewer: "Wave Defense gains one flag - input enabled or not -
        /// rather than a second renderer."
        ///
        /// This is also the server verification path. It is not wired to a
        /// server here; that is Phase 5's.
        public static Outcome Replay(Replay record)
        {
            record.Validate();

            var runner = new SimRunner(
                WaveDef.ForId(record.WaveId), record.BuildLane(),
                record.Deployment, record.Seed);

            while (true)
            {
                // Injected at the tick boundary the live run consumed it at -
                // the same place View calls TryRally from.
                if (record.RallyTick >= 0 && runner.Tick == record.RallyTick)
                    runner.TryRally(record.RallyCreature);

                if (!runner.Step()) break;
            }

            return runner.Outcome;
        }
```

- [x] **Step 9: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS, eight new tests. **No hash moves** — this task records inputs and does not change the simulation.

- [x] **Step 10: Run the cross-runtime gate and commit**

```bash
./implementation/scripts/cross-runtime-diff.sh
git add engine/Runtime/Combat/Replay.cs engine/Runtime/Combat/Replay.cs.meta \
        engine/Runtime/Combat/Ids.cs engine/Runtime/Combat/WaveDef.cs \
        engine/Runtime/Combat/SimRunner.cs engine/Runtime/Combat/Sim.cs \
        tests/engine/Combat/ReplayTests.cs
git commit -m "feat: the replay format, produced by the runner rather than observed

combat_engine section 3's six fields, binary and fixed-layout. Not JSON:
'a few hundred bytes' is a constraint at two raids per player per day, and
a test asserts it rather than trusting anyone to remember.

The record is built by SimRunner and TryRally is the only thing that
writes its Rally fields, at the moment it accepts the input. A recording
assembled by an observer can disagree with what the simulation consumed;
this one cannot.

Two section 3 fields had no engine representation - deployment HP, which
is derived from Stats, and terrain family, which was reachable only
through a static factory. Both are stored as specified and checked on
load, throwing the way WaveDef.Validate already does. Storing a value the
simulation then ignores is worse than not storing it.

Sim.Replay re-runs a record, so playing and replaying are one code path
differing only in where Rally comes from - client_architecture section
9.1's 'one flag, not a second renderer'. It is also the shape server
verification will take in Phase 5."
```

---

### Task 6: The Unity assemblies, and the placeholder body

Two assemblies and no more. `client_architecture` §1 names six; `Broodline.UI`, `.Model` and `.Net` are not created, because an empty assembly invites something to be put in it.

`SyntheticCreature` and `BoneAnimator` move out of `Broodline.Benchmark`. Pointing the renderer at a benchmark assembly inverts the dependency — production code would depend on a measuring harness.

**Files:**
- Create: `client/Assets/View.meta`, `client/Assets/View/Broodline.View.asmdef` (+ `.meta`)
- Create: `client/Assets/Game.meta`, `client/Assets/Game/Broodline.Game.asmdef` (+ `.meta`)
- Move: `client/Assets/Benchmark/SyntheticCreature.cs` (+ `.meta`) → `client/Assets/View/`
- Move: `client/Assets/Benchmark/BoneAnimator.cs` (+ `.meta`) → `client/Assets/View/`
- Modify: `client/Assets/Benchmark/Broodline.Benchmark.asmdef`
- Modify: `client/Assets/Benchmark/Tests/Broodline.Benchmark.Tests.asmdef`
- Modify: `SweepRunner.cs`, `BenchmarkResult.cs`, `WaveBenchmark.cs`, `Tests/SyntheticCreatureTests.cs`, `Tests/WaveBenchmarkTests.cs` — all under `client/Assets/Benchmark/`

**Interfaces:**
- Consumes: `Broodline.Sim` via the existing UPM reference — `client/Packages/manifest.json` already carries `"com.sepandstudio.broodline.sim": "file:../../engine"`.
- Produces: assemblies `Broodline.View` (references `Broodline.Sim`) and `Broodline.Game` (references both). `Broodline.Benchmark.SyntheticCreature` becomes `Broodline.View.SyntheticCreature`.

- [x] **Step 1: Move the two files, `.meta` included**

**Use `git mv` and move the `.meta` with the file.** A `.cs` that arrives without its `.meta` gets a fresh GUID from Unity, which silently breaks every reference to it.

```bash
mkdir -p client/Assets/View
git mv client/Assets/Benchmark/SyntheticCreature.cs      client/Assets/View/
git mv client/Assets/Benchmark/SyntheticCreature.cs.meta client/Assets/View/
git mv client/Assets/Benchmark/BoneAnimator.cs           client/Assets/View/
git mv client/Assets/Benchmark/BoneAnimator.cs.meta      client/Assets/View/
ls client/Assets/View/
```

Expected: four files.

- [x] **Step 2: Renamespace the two moved files**

In both `client/Assets/View/SyntheticCreature.cs` and `client/Assets/View/BoneAnimator.cs`, change:

```csharp
namespace Broodline.Benchmark
```

to:

```csharp
namespace Broodline.View
```

- [x] **Step 3: Write the two asmdefs**

`client/Assets/View/Broodline.View.asmdef`:

```json
{
  "name": "Broodline.View",
  "rootNamespace": "Broodline.View",
  "references": ["Broodline.Sim"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "noEngineReferences": false
}
```

`client/Assets/Game/Broodline.Game.asmdef`:

```json
{
  "name": "Broodline.Game",
  "rootNamespace": "Broodline.Game",
  "references": ["Broodline.Sim", "Broodline.View"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "noEngineReferences": false
}
```

- [x] **Step 4: Point the benchmark at its new home**

`client/Assets/Benchmark/Broodline.Benchmark.asmdef` — the `references` array is currently empty:

```json
{
  "name": "Broodline.Benchmark",
  "rootNamespace": "Broodline.Benchmark",
  "references": ["Broodline.View"],
  "includePlatforms": [],
  "autoReferenced": true
}
```

The test assembly reaches the moved types directly, so `client/Assets/Benchmark/Tests/Broodline.Benchmark.Tests.asmdef` needs the same addition — its `references` becomes:

```json
  "references": ["Broodline.Benchmark", "Broodline.View", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
```

Then add `using Broodline.View;` to exactly these five files, which are every remaining user of the two moved types:

- `client/Assets/Benchmark/SweepRunner.cs`
- `client/Assets/Benchmark/BenchmarkResult.cs`
- `client/Assets/Benchmark/WaveBenchmark.cs`
- `client/Assets/Benchmark/Tests/SyntheticCreatureTests.cs`
- `client/Assets/Benchmark/Tests/WaveBenchmarkTests.cs`

Confirm nothing else was missed:

```bash
grep -rln "SyntheticCreature\|BoneAnimator" client/Assets | grep "\.cs$"
```

Expected: the five above, plus the two moved files now under `client/Assets/View/`.

- [x] **Step 5: Generate the directory and asmdef `.meta` files**

Unity needs a `.meta` for every asset **and for every directory**, and a directory's meta lives one level up.

```bash
python3 - <<'PY'
import uuid, pathlib

def folder(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n" % uuid.uuid4().hex)

def asset(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)

folder("client/Assets/View.meta")
folder("client/Assets/Game.meta")
asset("client/Assets/View/Broodline.View.asmdef.meta")
asset("client/Assets/Game/Broodline.Game.asmdef.meta")
print("ok")
PY
```

- [x] **Step 6: Compile the Unity project**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: the existing benchmark tests still pass. A compile error here is a missing `using Broodline.View;` from Step 4.

- [x] **Step 7: Confirm the .NET side is untouched, then commit**

```bash
dotnet test Broodline.sln --nologo
git add client/Assets/View client/Assets/Game client/Assets/View.meta client/Assets/Game.meta \
        client/Assets/Benchmark
git commit -m "build: the View and Game assemblies, and a body to render

Two of client_architecture section 1's six. UI, Model and Net are not
created - an empty assembly invites something to be put in it, and the UI
boundary matters too much to stand up before there is anything to put
behind it.

SyntheticCreature and BoneAnimator move out of Broodline.Benchmark rather
than being referenced from it. Pointing the renderer at a measuring
harness inverts the dependency; the benchmark now references View, which
is the direction that makes sense. The bodies are already bone-driven and
already sized to the budget the art commission was briefed with, so the
renderer inherits a body whose cost is measured rather than guessed.

Phase 0's gate is unaffected and stays open - it needs real meshes from a
commission not yet placed."
```

---

### Task 7: The accumulator, and Rally at a tick boundary

**This is the phase's real risk surface. Everything after it is drawing.**

`WaveClock` has no `UnityEngine` dependency, so it is tested in **EditMode** rather than PlayMode — the design's §7 says PlayMode, and EditMode is strictly better here: same coverage, no scene, and a run measured in seconds. A real frame stall adds nothing that feeding a large delta does not already cover.

**Files:**
- Create: `client/Assets/View/WaveClock.cs` (+ `.meta`)
- Create: `client/Assets/View/Tests.meta`, `client/Assets/View/Tests/Broodline.View.Tests.asmdef` (+ `.meta`)
- Create: `client/Assets/View/Tests/WaveClockTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `SimRunner.Step()`, `SimRunner.TryRally(int)`, `SimRunner.Tick`.
- Produces:
  - `sealed class WaveClock` with `const int MaxCatchUpSteps = 8`, `double Alpha { get; }`, `int StepsLastFrame { get; }`, `bool Terminated { get; }`
  - `void RequestRally(int creatureId)`
  - `void Advance(SimRunner runner, double deltaSeconds, System.Action onTick)`

- [x] **Step 1: Write the test assembly definition**

`client/Assets/View/Tests/Broodline.View.Tests.asmdef`:

```json
{
  "name": "Broodline.View.Tests",
  "rootNamespace": "Broodline.View.Tests",
  "references": ["Broodline.View", "Broodline.Sim", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [x] **Step 2: Write the failing test**

`client/Assets/View/Tests/WaveClockTests.cs`:

```csharp
using NUnit.Framework;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.View.Tests
{
    public class WaveClockTests
    {
        const double Frame60 = 1.0 / 60.0;
        const double Tick30  = 1.0 / 30.0;

        static SimRunner Runner() => new SimRunner(
            WaveDef.Wave6(), Lane.Defile(), Deployment(), 6UL);

        static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        [Test]
        public void SixtyHertzRenderingStepsTheSimEveryOtherFrame()
        {
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(1, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(1, clock.StepsLastFrame);

            Assert.AreEqual(2, runner.Tick);
        }

        [Test]
        public void ATwoHundredMillisecondStallCatchesUpAndDropsNoTick()
        {
            // The rule at client_architecture section 2: a dropped FRAME must
            // never drop a TICK. 200ms is six ticks.
            //
            // The half-tick is deliberate. 0.200 and 1.0/30.0 are both inexact
            // in binary, so 0.200 / (1.0/30.0) lands either side of 6 depending
            // on rounding, and a test written against the exact value would be
            // asserting a floating-point tie rather than the behaviour. The
            // extra half-tick puts it clear of the boundary.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 6.5, null);

            Assert.AreEqual(6, clock.StepsLastFrame);
            Assert.AreEqual(6, runner.Tick);
        }

        [Test]
        public void AStallBeyondTheCapRunsSlowRatherThanSkipping()
        {
            // 15 ticks owed, capped at 8. The remaining 7 stay in the
            // accumulator and are paid off by later frames. Every tick still
            // executes, in order - the wave runs SLOW, which is recoverable,
            // rather than SKIPPING, which is a run the server will not
            // reproduce.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 15.5, null);
            Assert.AreEqual(WaveClock.MaxCatchUpSteps, clock.StepsLastFrame);
            Assert.AreEqual(8, runner.Tick);

            clock.Advance(runner, 0.0, null);
            Assert.AreEqual(7, clock.StepsLastFrame);
            Assert.AreEqual(15, runner.Tick);
        }

        [Test]
        public void AlphaIsTheFractionOfATickElapsed()
        {
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 / 2, null);
            Assert.AreEqual(0.5, clock.Alpha, 1e-9);
            Assert.AreEqual(0, runner.Tick);
        }

        [Test]
        public void RallyIsConsumedAtATickBoundaryNotAtTheTapTime()
        {
            // client_architecture section 2: "A Rally tap records the tick
            // index, not a timestamp." The tap arrives mid-frame; it must be
            // consumed by the NEXT step and recorded at that step's tick.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 3.5, null);    // three steps, half left
            Assert.AreEqual(3, runner.Tick);

            clock.RequestRally(2);
            Assert.AreEqual(-1, runner.Record.RallyTick, "consumed before a tick boundary");

            clock.Advance(runner, Tick30 * 0.6, null);    // now over a boundary
            Assert.AreEqual(3, runner.Record.RallyTick);
            Assert.AreEqual(2, runner.Record.RallyCreature);
        }

        [Test]
        public void OnTickFiresOncePerSimulationStep()
        {
            var clock = new WaveClock();
            var runner = Runner();
            int fired = 0;

            clock.Advance(runner, Tick30 * 6.5, () => fired++);

            Assert.AreEqual(6, fired);
        }

        [Test]
        public void TerminationStopsTheClockWithoutBurningFrames()
        {
            var clock = new WaveClock();
            var runner = Runner();

            for (int i = 0; i < 500 && !clock.Terminated; i++)
                clock.Advance(runner, Tick30 * 6.5, null);

            Assert.IsTrue(clock.Terminated);
            Assert.IsTrue(runner.Done);
            Assert.AreEqual(Result.Loss, runner.Outcome.Result);

            clock.Advance(runner, Tick30 * 6.5, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
        }
    }
}
```

- [x] **Step 3: Run it and watch it fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: compile failure — `WaveClock` does not exist.

- [x] **Step 4: Write `WaveClock.cs`**

`client/Assets/View/WaveClock.cs`:

```csharp
using System;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// The seam between a fixed-timestep simulation and a variable frame rate.
    ///
    /// client_architecture section 2: View owns an accumulator, calls Step as
    /// many times as the elapsed time allows, and interpolates between the last
    /// two snapshots for display. The engine never sees a variable delta and
    /// never learns that rendering exists.
    ///
    /// The doubles here are deliberate and are not a determinism hazard. They
    /// never reach the simulation: the only thing that crosses the boundary is
    /// the DECISION to call Step, and Step takes no arguments. That is the
    /// entire point of a fixed timestep, and it is why the engine's no-float
    /// rule binds engine/ and not this file.
    public sealed class WaveClock
    {
        /// A dropped frame must never drop a tick, so a stall is paid off by
        /// stepping more times. Without a ceiling that spirals: a frame that
        /// took long enough schedules more work than the next frame has time
        /// for, forever. With one, a device that cannot keep up runs the wave
        /// SLOW - every tick still executes, in order, at the same arithmetic.
        /// Slow is recoverable; skipped is a run the server will not reproduce,
        /// and the player loses the reward for a wave they won.
        ///
        /// Eight ticks is 267ms of simulation in one frame, well past any stall
        /// worth absorbing silently.
        public const int MaxCatchUpSteps = 8;

        private const double TickSeconds = 1.0 / Stats.TicksPerSecond;

        private double _accumulator;
        private int _pendingRally = -1;

        /// How far between the previous tick and the current one the render
        /// should interpolate. 0 at a tick boundary, approaching 1.
        public double Alpha => _accumulator / TickSeconds;

        public int StepsLastFrame { get; private set; }
        public bool Terminated { get; private set; }

        /// Records a Rally tap. It is NOT applied here - it is held until the
        /// next tick boundary, so the simulation records a tick index rather
        /// than a moment in wall-clock time. The input log the server
        /// re-simulates from must be exactly what the local run consumed.
        public void RequestRally(int creatureId) => _pendingRally = creatureId;

        public void Advance(SimRunner runner, double deltaSeconds, Action onTick)
        {
            StepsLastFrame = 0;
            if (Terminated) return;

            _accumulator += deltaSeconds;

            while (_accumulator >= TickSeconds && StepsLastFrame < MaxCatchUpSteps)
            {
                if (_pendingRally >= 0)
                {
                    runner.TryRally(_pendingRally);
                    _pendingRally = -1;
                }

                if (!runner.Step())
                {
                    Terminated = true;
                    _accumulator = 0.0;
                    return;
                }

                _accumulator -= TickSeconds;
                StepsLastFrame++;
                onTick?.Invoke();
            }
        }
    }
}
```

- [x] **Step 5: Create the `.meta` files**

```bash
python3 - <<'PY'
import uuid, pathlib

def folder(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n" % uuid.uuid4().hex)

def script(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)

def asset(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)

folder("client/Assets/View/Tests.meta")
script("client/Assets/View/WaveClock.cs.meta")
script("client/Assets/View/Tests/WaveClockTests.cs.meta")
asset("client/Assets/View/Tests/Broodline.View.Tests.asmdef.meta")
print("ok")
PY
```

- [x] **Step 6: Run until green**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: seven new tests pass, benchmark tests still pass.

- [x] **Step 7: Commit**

```bash
git add client/Assets/View client/Assets/View.meta
git commit -m "feat: the accumulator, and Rally recorded at a tick boundary

The seam between a 30Hz simulation and a variable frame rate, and the
phase's real risk surface - everything after this is drawing.

A dropped frame never drops a tick: a stall is paid off by stepping more
times, capped at 8 per frame so it cannot spiral. Past the cap the wave
runs SLOW rather than skipping, because slow is recoverable and a skipped
tick is a run the server will not reproduce - the player loses the reward
for a wave they actually won.

A Rally tap is held until the next tick boundary rather than applied when
it arrives, so the record carries a tick index and not a moment in
wall-clock time. Tested against exactly that: a tap between boundaries
must not be visible in the record until the next step.

Tested in EditMode rather than PlayMode. WaveClock has no UnityEngine
dependency, so a scene buys nothing a large delta does not already cover,
and the run is seconds instead of minutes."
```

---

### Task 8: Rendering `SimState`

**Files:**
- Create: `client/Assets/View/WaveSnapshot.cs` (+ `.meta`)
- Create: `client/Assets/View/WaveView.cs` (+ `.meta`)
- Create: `client/Assets/View/Tests/WaveSnapshotTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `SimRunner`'s read-only surface, `WaveClock.Alpha`, `SyntheticCreature.Build`, `Lane.PocketTiles`.
- Produces:
  - `sealed class WaveSnapshot` with `void Capture(SimRunner)`, `float RaiderTile(int)`, `int RaiderHp(int)`, `bool RaiderAlive(int)`, `int CreatureHp(int)`, `int RaiderCount`
  - `sealed class WavePair` with `void Advance(SimRunner)`, `WaveSnapshot Previous`, `WaveSnapshot Current`
  - `sealed class WaveView : MonoBehaviour` with `void Build(SimRunner)`, `void Render(SimRunner, WavePair, double alpha)`

**`Fix64` has no float conversion, on purpose** — floats are banned in the engine. The conversion lives here, in View, and nowhere else: `raw / 4294967296.0`, which is 2^32, the Q32.32 scale.

- [x] **Step 1: Write the failing test**

`client/Assets/View/Tests/WaveSnapshotTests.cs`:

```csharp
using NUnit.Framework;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.View.Tests
{
    public class WaveSnapshotTests
    {
        static SimRunner Runner() => new SimRunner(
            WaveDef.Wave6(), Lane.Defile(), Deployment(), 6UL);

        static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        [Test]
        public void APairRetainsPreviousAndCurrent()
        {
            // Interpolation needs both. client_architecture section 2.
            var runner = Runner();
            var pair = new WavePair(runner);

            while (runner.Tick < 100) { runner.Step(); pair.Advance(runner); }
            float a = pair.Previous.RaiderTile(0);

            runner.Step(); pair.Advance(runner);
            float b = pair.Previous.RaiderTile(0);

            Assert.AreNotEqual(a, b, "Previous did not advance - the pair is not swapping");
            Assert.Greater(pair.Current.RaiderTile(0), pair.Previous.RaiderTile(0),
                "the Courser moves toward the Ark, so current must lead previous");
        }

        [Test]
        public void CaptureConvertsFixedPointWithoutLosingTheTile()
        {
            var runner = Runner();
            var pair = new WavePair(runner);
            while (runner.Tick < 120) { runner.Step(); pair.Advance(runner); }

            // The engine's floor of the same value is the authority.
            var raw = runner.RaiderProgress[0].Raw / 4294967296.0;
            Assert.AreEqual(raw, pair.Current.RaiderTile(0), 1e-4);
        }

        [Test]
        public void SnapshotsDoNotAliasEngineArrays()
        {
            // View copies what it needs. If a snapshot held the engine's array
            // instead, Previous and Current would be the same object and
            // interpolation would render nothing.
            var runner = Runner();
            var pair = new WavePair(runner);
            while (runner.Tick < 100) { runner.Step(); pair.Advance(runner); }

            float before = pair.Previous.RaiderTile(0);
            for (int i = 0; i < 10; i++) runner.Step();
            Assert.AreEqual(before, pair.Previous.RaiderTile(0),
                "the snapshot changed when the engine did - it is aliasing");
        }
    }
}
```

- [x] **Step 2: Run it and watch it fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: compile failure — `WaveSnapshot` does not exist.

- [x] **Step 3: Write `WaveSnapshot.cs`**

`client/Assets/View/WaveSnapshot.cs`:

```csharp
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// One tick's worth of the world, copied out for rendering.
    ///
    /// client_architecture section 2 requires two of these retained - previous
    /// and current - because interpolation needs both, and says View "copies
    /// the minimum it needs for interpolation rather than deep-copying world
    /// state each tick." This is that minimum: what moves and what is alive.
    ///
    /// Fix64 carries no float conversion because floats are banned in the
    /// engine. The conversion happens HERE and nowhere else: Q32.32 means the
    /// raw long is the value scaled by 2^32.
    public sealed class WaveSnapshot
    {
        private const double Q32 = 4294967296.0;   // 2^32

        private readonly float[] _raiderTile;
        private readonly int[] _raiderHp;
        private readonly bool[] _raiderAlive;
        private readonly bool[] _raiderChilled;
        private readonly int[] _creatureHp;

        public int RaiderCount { get; private set; }
        public int Integrity { get; private set; }
        public int Tick { get; private set; }

        public WaveSnapshot(int raiderCapacity, int creatureCount)
        {
            _raiderTile = new float[raiderCapacity];
            _raiderHp = new int[raiderCapacity];
            _raiderAlive = new bool[raiderCapacity];
            _raiderChilled = new bool[raiderCapacity];
            _creatureHp = new int[creatureCount];
        }

        public void Capture(SimRunner r)
        {
            RaiderCount = r.RaiderCount;
            Integrity = r.Integrity;
            Tick = r.Tick;

            for (int i = 0; i < r.RaiderCount; i++)
            {
                _raiderTile[i] = (float)(r.RaiderProgress[i].Raw / Q32);
                _raiderHp[i] = r.RaiderHp[i];
                _raiderAlive[i] = r.RaiderAlive[i];
                _raiderChilled[i] = r.RaiderChilled[i];
            }
            for (int c = 0; c < r.CreatureCount; c++)
                _creatureHp[c] = r.CreatureHp[c];
        }

        public float RaiderTile(int i) => _raiderTile[i];
        public int RaiderHp(int i) => _raiderHp[i];
        public bool RaiderAlive(int i) => _raiderAlive[i];
        public bool RaiderChilled(int i) => _raiderChilled[i];
        public int CreatureHp(int c) => _creatureHp[c];
    }

    /// The two retained snapshots, swapped rather than reallocated so the
    /// render path allocates nothing per tick.
    public sealed class WavePair
    {
        private WaveSnapshot _previous;
        private WaveSnapshot _current;

        public WavePair(SimRunner r)
        {
            int capacity = r.RaiderHp.Length;
            _previous = new WaveSnapshot(capacity, r.CreatureCount);
            _current = new WaveSnapshot(capacity, r.CreatureCount);
            _previous.Capture(r);
            _current.Capture(r);
        }

        public WaveSnapshot Previous => _previous;
        public WaveSnapshot Current => _current;

        /// Call once per simulation step, after Step returns.
        public void Advance(SimRunner r)
        {
            var swap = _previous;
            _previous = _current;
            _current = swap;
            _current.Capture(r);
        }
    }
}
```

- [x] **Step 4: Run the snapshot tests**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: three new tests pass.

- [x] **Step 5: Write `WaveView.cs`**

`client/Assets/View/WaveView.cs`:

```csharp
using UnityEngine;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// Renders SimState and never decides anything.
    ///
    /// client_architecture section 11: "View renders simulation output and
    /// never derives it. If the view needs a number that the engine does not
    /// expose, the engine gains an accessor." Everything drawn here is read
    /// straight off SimRunner's read-only surface or off a snapshot copied
    /// from it. Nothing is computed about the game.
    public sealed class WaveView : MonoBehaviour
    {
        /// One world unit per lane tile. The lane runs along +X from spawn at
        /// tile 0 to the Ark at tile Tiles; pockets sit one unit off in +Z,
        /// which is the perpendicular offset Lane bakes into its distance table.
        public const float TileSize = 1f;
        public const float PocketOffset = 1f;

        private static readonly SyntheticCreatureSpec BodySpec =
            new SyntheticCreatureSpec { Triangles = 7000, Bones = 24, Materials = 2 };

        private Transform[] _raiders;
        private Transform[] _creatures;
        private Transform _ark;

        public void Build(SimRunner r)
        {
            _ark = BuildMarker("Ark", new Color(0.85f, 0.78f, 0.45f),
                               new Vector3(r.LaneTiles * TileSize, 0f, 0f), 1.5f);

            for (int t = 0; t < r.LaneTiles; t++)
                BuildMarker("tile" + t, new Color(0.22f, 0.22f, 0.26f),
                            new Vector3(t * TileSize, -0.5f, 0f), 0.9f);

            _creatures = new Transform[r.CreatureCount];
            for (int c = 0; c < r.CreatureCount; c++)
            {
                var body = SyntheticCreature.Build(BodySpec);
                body.name = "creature" + c + "-" + r.CreatureSpecies[c];
                body.AddComponent<BoneAnimator>();
                body.transform.position = new Vector3(
                    r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
                _creatures[c] = body.transform;
            }

            _raiders = new Transform[r.RaiderHp.Length];
            for (int i = 0; i < _raiders.Length; i++)
            {
                var body = SyntheticCreature.Build(BodySpec);
                body.name = "raider" + i;
                body.AddComponent<BoneAnimator>();
                body.SetActive(false);
                _raiders[i] = body.transform;
            }
        }

        /// Interpolates between the two retained snapshots. alpha is 0 at a
        /// tick boundary and approaches 1 - WaveClock.Alpha.
        public void Render(SimRunner r, WavePair pair, double alpha)
        {
            float a = Mathf.Clamp01((float)alpha);

            for (int i = 0; i < _raiders.Length; i++)
            {
                bool live = i < pair.Current.RaiderCount && pair.Current.RaiderAlive(i);
                if (_raiders[i].gameObject.activeSelf != live)
                    _raiders[i].gameObject.SetActive(live);
                if (!live) continue;

                float tile = Mathf.Lerp(pair.Previous.RaiderTile(i), pair.Current.RaiderTile(i), a);
                _raiders[i].position = new Vector3(tile * TileSize, 0f, 0f);
            }

            for (int c = 0; c < _creatures.Length; c++)
            {
                bool alive = pair.Current.CreatureHp(c) > 0;
                if (_creatures[c].gameObject.activeSelf != alive)
                    _creatures[c].gameObject.SetActive(alive);
                if (!alive) continue;

                // Skittish repositions, so the pocket is read every frame
                // rather than cached at Build.
                _creatures[c].position = new Vector3(
                    r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
            }
        }

        private static Transform BuildMarker(string name, Color colour, Vector3 at, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = at;
            go.transform.localScale = Vector3.one * scale * 0.9f;
            go.GetComponent<Renderer>().material.color = colour;
            return go.transform;
        }
    }
}
```

- [x] **Step 6: Create the `.meta` files, compile, commit**

```bash
python3 - <<'PY'
import uuid, pathlib
for p in ["client/Assets/View/WaveSnapshot.cs.meta",
          "client/Assets/View/WaveView.cs.meta",
          "client/Assets/View/Tests/WaveSnapshotTests.cs.meta"]:
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)
print("ok")
PY
./implementation/scripts/run-unity-tests.sh EditMode
```

```bash
git add client/Assets/View
git commit -m "feat: render SimState, interpolated between two snapshots

Two snapshots retained and swapped rather than reallocated, carrying only
what interpolation needs - what moves and what is alive - rather than
deep-copying the world each tick.

Fix64 has no float conversion because floats are banned in the engine, so
the Q32.32 conversion lives here and nowhere else. A test pins it against
the engine's own value so the two cannot drift.

The view reads pockets every frame rather than caching them at build,
because Skittish repositions mid-wave. Nothing here computes anything
about the game: every number drawn is read off SimRunner's read-only
surface or off a snapshot copied from it."
```

---

### Task 9: Legibility and the debug overlay

The done-when needs none of this. A phase that ends without anyone having watched wave 6 defers the first honest look at the game to a phase that is authoring content against it.

**Files:**
- Create: `client/Assets/View/WaveHud.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `SimRunner`'s read-only surface, `WaveClock.StepsLastFrame`, `Outcome`, `Breach`.
- Produces: `sealed class WaveHud : MonoBehaviour` with `void Render(SimRunner, WaveClock, WavePair)`.

- [x] **Step 1: Write `WaveHud.cs`**

`client/Assets/View/WaveHud.cs`:

```csharp
using UnityEngine;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// Legible, not finished. IMGUI on purpose - this is a debug surface that
    /// the real Wave Defense screen replaces wholesale, and building it in UI
    /// Toolkit would invite it to be kept.
    ///
    /// The end panel shows the three-boolean breach diagnosis because
    /// client_architecture section 9.1 makes the diagnosis the actionable
    /// content: it names whether the trait was absent, present at insufficient
    /// coverage, or misplaced, which is what makes a loss legible rather than
    /// arbitrary. It is read off the Outcome, never recomputed.
    public sealed class WaveHud : MonoBehaviour
    {
        public SimRunner Runner;
        public WaveClock Clock;
        public WavePair Pair;
        public Camera View;

        private GUIStyle _label;

        private void OnGUI()
        {
            if (Runner == null) return;
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };

            DrawIntegrity();
            DrawBars();
            DrawDebug();
            if (Runner.Done) DrawOutcome();
        }

        private void DrawIntegrity()
        {
            // Integrity is the loss condition. combat_engine section 8 makes it
            // a pool, not a life count.
            GUI.Label(new Rect(12, 8, 420, 24),
                "<b>Integrity " + Runner.Integrity + "</b>   tick " + Runner.Tick, _label);
        }

        private void DrawBars()
        {
            for (int i = 0; i < Runner.RaiderCount; i++)
            {
                if (!Runner.RaiderAlive[i]) continue;
                int max = Stats.RaiderHp(Runner.RaiderType[i]);
                var at = WorldToScreen(new Vector3(Pair.Current.RaiderTile(i) * WaveView.TileSize, 1.4f, 0f));

                // Chill is the thing wave 6 exists to teach. If it is not
                // visible the slice cannot be judged by eye.
                Bar(at, Runner.RaiderHp[i], max,
                    Runner.RaiderChilled[i] ? new Color(0.45f, 0.75f, 1f) : new Color(0.85f, 0.35f, 0.3f),
                    Runner.RaiderChilled[i] ? "CHILLED" : null);
            }

            for (int c = 0; c < Runner.CreatureCount; c++)
            {
                if (Runner.CreatureHp[c] <= 0) continue;
                int max = Stats.CreatureHp(Runner.CreatureSpecies[c]);
                var at = WorldToScreen(new Vector3(
                    Runner.Lane.PocketTiles[Runner.CreaturePocket[c]] * WaveView.TileSize,
                    1.4f, WaveView.PocketOffset));

                // Rally is the player's ONLY input. A tap with no visible
                // consequence is indistinguishable from a tap that was dropped,
                // which is exactly the failure WaveClock exists to prevent.
                bool rallied = Runner.Tick < Runner.CreatureRallyUntil[c];
                Bar(at, Runner.CreatureHp[c], max,
                    rallied ? new Color(1f, 0.85f, 0.3f) : new Color(0.4f, 0.8f, 0.45f),
                    rallied ? "RALLY " + (Runner.CreatureRallyUntil[c] - Runner.Tick) : null);
            }
        }

        private void DrawDebug()
        {
            // The entity count is shown although nothing degrades yet.
            // client_architecture section 4 keys every rung of the degradation
            // ladder off it, so surfacing it now means the ladder arrives with
            // a number already proven to be there and already deterministic.
            int entities = Runner.RaiderCount + Runner.CreatureCount;
            GUI.Label(new Rect(12, Screen.height - 76, 460, 72),
                "entities " + entities +
                "\nalpha " + Clock.Alpha.ToString("F2") +
                "   catch-up " + Clock.StepsLastFrame +
                "\nframe " + (Time.deltaTime * 1000f).ToString("F1") + " ms", _label);
        }

        private void DrawOutcome()
        {
            var o = Runner.Outcome;
            string text = "<b>" + o.Result + "</b>\nticks " + o.Ticks +
                          "\nintegrity " + o.IntegrityRemaining;

            for (int b = 0; b < o.BreachCount; b++)
            {
                // Iterate to BreachCount, never Breaches.Length - the buffer is
                // the wave's spawn capacity and a zeroed trailing entry reads
                // as "the trait was absent".
                var br = o.Breaches[b];
                text += "\n\n<b>breach</b> " + br.Type + " at tick " + br.Tick +
                        "\n  access    " + br.Access +
                        "\n  coverage  " + br.Coverage +
                        "\n  placement " + br.Placement;
            }

            GUI.Box(new Rect(Screen.width / 2f - 160, 60, 320, 220), "");
            GUI.Label(new Rect(Screen.width / 2f - 144, 72, 300, 200), text, _label);
        }

        private void Bar(Vector2 at, int hp, int max, Color fill, string tag)
        {
            const float w = 52f, h = 6f;
            float frac = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

            var back = new Rect(at.x - w / 2f, at.y, w, h);
            GUI.DrawTexture(back, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                            new Color(0f, 0f, 0f, 0.55f), 0f, 0f);
            GUI.DrawTexture(new Rect(back.x, back.y, w * frac, h), Texture2D.whiteTexture,
                            ScaleMode.StretchToFill, false, 0f, fill, 0f, 0f);

            if (tag != null)
                GUI.Label(new Rect(at.x - w / 2f, at.y + h, 120f, 18f), tag, _label);
        }

        private Vector2 WorldToScreen(Vector3 world)
        {
            var p = (View != null ? View : Camera.main).WorldToScreenPoint(world);
            return new Vector2(p.x, Screen.height - p.y);
        }
    }
}
```

- [x] **Step 2: Create the `.meta`, compile, commit**

```bash
python3 - <<'PY'
import uuid, pathlib
pathlib.Path("client/Assets/View/WaveHud.cs.meta").write_text(
    "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)
PY
./implementation/scripts/run-unity-tests.sh EditMode
```

```bash
git add client/Assets/View
git commit -m "feat: enough legibility to judge wave 6 by eye

HP bars, Chill on the affected Courser, Rally on the affected creature,
integrity, and an end panel carrying the three-boolean breach diagnosis
read off the Outcome rather than recomputed.

Chill and Rally earn their place for different reasons. Chill is what the
wave exists to teach, so a slice where it is invisible cannot be judged.
Rally is the player's only input, and a tap with no visible consequence is
indistinguishable from a tap that was dropped - the exact failure the
accumulator exists to prevent.

The entity count is on screen although nothing degrades yet, because
client_architecture section 4 keys every rung of the ladder off it.

IMGUI on purpose. The real Wave Defense screen replaces this wholesale,
and building it in UI Toolkit would invite it to be kept."
```

---

### Task 10: The composition root and the device round-trip

**The done-when.** A wave played on device replays bit-identically in xUnit.

**Three things found in execution, recorded here rather than left to be rediscovered.**

**The camera must frame the lane along the TALL axis.** `Phase0Setup` locks the player to portrait and `client_architecture` §10 keeps it there, so the on-device aspect is about 0.46. An orthographic camera covers `orthographicSize × aspect × 2` horizontally, so laying a 24-tile lane across the screen would need `orthographicSize ≈ 26` and give 52 units of vertical coverage for a 2-unit-tall battlefield — a hairline. The camera looks straight down with **world +X mapped to screen-up** (`Quaternion.LookRotation(Vector3.down, Vector3.right)`), and `orthographicSize` sizes the lane's *length*. This is also what the shipped screen does: `screen_inventory` §4.2 puts "pockets beside the lane" in a portrait frame. `BenchmarkSceneBuilder` hit the same trap from the other side and documents it; the Editor's landscape Game view hides it completely.

**The device tests skip rather than fail when the artifact is absent.** The artifact cannot be produced by CI or by anyone without the physical device, so a hard failure would leave the suite permanently red for every other task. But a silent skip is the worse failure this repo already knows about — `run-unity-tests.sh` carries a comment calling a stale pass worse than a failure. So `DeviceArtifactFactAttribute` sets a `Skip` reason naming what is missing and how to produce it, and **the Definition of Done requires `Skipped: 0`**, which is what stops "skipped" being mistaken for "passed" at sign-off.

**The scene builder follows Phase 0's placement, not a new assembly.** See the Files block.

**Files:**
- Create: `client/Assets/Game/WaveRunner.cs` (+ `.meta`)
- Create: `client/Assets/Editor/WaveSceneBuilder.cs` (+ `.meta`) — **beside `BenchmarkSceneBuilder`, in the default `Assembly-CSharp-Editor` with no asmdef.** That is the pattern Phase 0 set and `Broodline.Game` is `autoReferenced`, so a `Broodline.Game.Editor` assembly buys nothing
- Create: `client/Assets/Scenes/Wave.unity` (+ `.meta`) — generated by the builder and **committed**, as `Benchmark.unity` already is: it diffs as text and can be regenerated, but the build does not depend on anyone remembering to run the generator
- Create: `client/Assets/Editor/WaveBuilder.cs` (+ `.meta`) — the iOS Xcode project for the wave scene, a near-copy of `BenchmarkBuilder` rather than a generalisation of it: the two differ only in the scene, but `BenchmarkBuilder` is the instrument behind a delivered render budget and refactoring it risks changing what Phase 0 measured with
- Create: `client/Assets/Editor/IosFileSharingPostProcess.cs` (+ `.meta`) — writes the two plist keys Unity exposes no setting for
- Create: `tests/engine/Combat/DeviceReplayTests.cs`
- Create: `tests/engine/Combat/EditorReplayTests.cs` — the same round-trip minus IL2CPP and the device, from a tracked artifact, so a device failure has one candidate cause instead of two
- Create: `implementation/results/device-replay.bin` — **tracked**, like the sweep CSVs

**Interfaces:**
- Consumes: everything above.
- Produces: `sealed class WaveRunner : MonoBehaviour`; a `replay.bin` artifact written to the app's Documents directory; `DeviceReplayTests` re-simulating it.

- [x] **Step 1: Write the composition root**

`client/Assets/Game/WaveRunner.cs`:

```csharp
using System;
using System.IO;
using UnityEngine;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.Game
{
    /// Wires the runner, the clock, the view and the HUD, and writes the replay
    /// artifact when the wave ends.
    ///
    /// Wave 6 as authored, with the golden-A deployment: four Vetch and a Loam,
    /// none carrying Chill. waves_01_12 section 3 designs this as a LOSS - the
    /// beat that teaches the counter system - so a Loss here is the wave
    /// working, not the build failing.
    ///
    /// Deployment is programmatic. Phase 3 captures one input, Rally, because
    /// combat_engine section 8 makes it the only player input during a wave.
    public sealed class WaveRunner : MonoBehaviour
    {
        public const ulong Seed = 6UL;

        private SimRunner _runner;
        private WaveClock _clock;
        private WavePair _pair;
        private WaveView _view;
        private WaveHud _hud;
        private bool _written;

        private static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        private void Start()
        {
            _runner = new SimRunner(WaveDef.Wave6(), Lane.Defile(), Deployment(), Seed);
            _clock = new WaveClock();
            _pair = new WavePair(_runner);

            _view = gameObject.AddComponent<WaveView>();
            _view.Build(_runner);

            _hud = gameObject.AddComponent<WaveHud>();
            _hud.Runner = _runner;
            _hud.Clock = _clock;
            _hud.Pair = _pair;
            _hud.View = Camera.main;
        }

        private void Update()
        {
            // A tap anywhere Rallies creature 0. Deliberately crude: choosing a
            // creature is a deployment-UI concern and this phase has no UI.
            if (Input.GetMouseButtonDown(0)) _clock.RequestRally(0);

            // Time.deltaTime, NOT a fixed value. Feeding the accumulator the
            // real frame delta is the whole point - it is what makes a stall on
            // a real device produce catch-up steps rather than a slower wave.
            _clock.Advance(_runner, Time.deltaTime, () => _pair.Advance(_runner));
            _view.Render(_runner, _pair, _clock.Alpha);

            if (_runner.Done && !_written) WriteArtifact();
        }

        private void WriteArtifact()
        {
            _written = true;

            // Application.persistentDataPath is the app's Documents directory
            // on iOS, which is what UIFileSharingEnabled exposes to devicectl.
            // The engine does no file I/O; it hands back bytes and this writes
            // them. Same split Phase 0's CorpusPlayerHarness uses.
            var dir = Application.persistentDataPath;
            File.WriteAllBytes(Path.Combine(dir, "replay.bin"), _runner.Record.Serialize());
            File.WriteAllText(Path.Combine(dir, "replay-outcome.txt"),
                _runner.Outcome.Hash + "\n" +
                (int)_runner.Outcome.Result + "\n" +
                _runner.Outcome.Ticks + "\n" +
                _runner.Outcome.IntegrityRemaining + "\n");

            Debug.Log("[broodline] wrote replay.bin to " + dir +
                      "  hash=" + _runner.Outcome.Hash +
                      "  result=" + _runner.Outcome.Result +
                      "  ticks=" + _runner.Outcome.Ticks);
        }
    }
}
```

- [x] **Step 2: Write the scene builder**

`client/Assets/Game/Editor/Broodline.Game.Editor.asmdef`:

```json
{
  "name": "Broodline.Game.Editor",
  "rootNamespace": "Broodline.Game.Editor",
  "references": ["Broodline.Game", "Broodline.View", "Broodline.Sim"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "autoReferenced": true
}
```

`client/Assets/Game/Editor/WaveSceneBuilder.cs`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Broodline.Game;

namespace Broodline.Game.Editor
{
    /// Builds the wave scene from code rather than committing a .unity file.
    /// Phase 0's BenchmarkSceneBuilder set the precedent: a generated scene
    /// diffs as source and cannot drift from what the code expects.
    public static class WaveSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Wave.unity";

        [MenuItem("Broodline/Build Wave Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.backgroundColor = new Color(0.07f, 0.07f, 0.09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Look down the lane, which runs +X from tile 0 to the Ark at 24.
            cameraGo.transform.position = new Vector3(12f, 6f, -12f);
            cameraGo.transform.rotation = Quaternion.Euler(25f, 0f, 0f);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("WaveRunner").AddComponent<WaveRunner>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[broodline] wrote " + ScenePath);
        }
    }
}
```

- [x] **Step 3: Create the `.meta` files and build the scene**

```bash
python3 - <<'PY'
import uuid, pathlib

def folder(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\n" % uuid.uuid4().hex)

def script(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)

def asset(p):
    pathlib.Path(p).write_text(
        "fileFormatVersion: 2\nguid: %s\n" % uuid.uuid4().hex)

folder("client/Assets/Game/Editor.meta")
script("client/Assets/Game/WaveRunner.cs.meta")
script("client/Assets/Game/Editor/WaveSceneBuilder.cs.meta")
asset("client/Assets/Game/Editor/Broodline.Game.Editor.asmdef.meta")
print("ok")
PY
```

Then in the Unity editor: **Broodline → Build Wave Scene**, and press Play.

> **Partly done, 2026-09-11 — left unticked on purpose.** The `.meta` files
> exist and the scene is generated and committed: it was built headlessly with
> `Unity -batchmode -quit -projectPath client -executeMethod WaveSceneBuilder.Build`,
> which reported `orthographicSize=12.96 laneLength=24` — the lane fits the tall
> axis with margin and leaves about 11.7 units across for a 3-unit pocket strip.
> **Nobody has pressed Play.** The rest of this step — watching the Courser
> advance, the bars move, and the end panel read `Loss` with `access False` — has
> not happened, and the step stays open until it does. A checkbox that quietly
> claims something was observed when it was not is worth less than no checkbox.

Expected: a lane of tiles, five bodies in pockets, one body entering at tick 90 and advancing, HP bars, and after roughly 20 seconds an end panel reading **Loss**, integrity 0, one breach with `access False`. **That is the wave working** — wave 6 is the designed loss.

- [x] **Step 4: Confirm Rally is visibly consumed**

Press Play again and click once while the Courser is on the board.

Expected: creature 0's bar turns amber, the `RALLY n` countdown appears and runs to zero over four seconds, and the console's final log line carries a **different hash** from the no-Rally run. A hash that did not change means the tap never reached the simulation.

- [x] **Step 5: Build and run on device, and pull the artifact**

**Corrected in execution — the plan had the wrong retrieval route.** It named `UIFileSharingEnabled` plus `devicectl` as the way off the device, on the assumption that Phase 0's `CorpusPlayerHarness` was precedent. It is not: `cross-runtime-diff.sh` builds a **macOS ARM64** player, so Phase 0 never pulled a file off an iOS device that way.

What Phase 0 *did* prove is recorded in `BenchmarkBuilder.cs`, which says a Development build **"is required for Xcode's Download Container to reach the CSV the sweep writes."** That is the route already known to work in this project, and it needs neither plist key.

```bash
# Generates the Xcode project. Development build, for the reason above.
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity   -batchmode -quit -projectPath "$(pwd)/client" -executeMethod WaveBuilder.BuildIOS -logFile -
```

Open `build/ios-wave/Unity-iPhone.xcodeproj`, run it on the device, play the wave, then **Xcode → Window → Devices and Simulators → the device → the app → ⚙ → Download Container**, and copy `AppData/Documents/replay.bin` out of the `.xcappdata` bundle.

`IosFileSharingPostProcess` additionally sets `UIFileSharingEnabled` and `LSSupportsOpeningDocumentsInPlace`, which unlock the command-line route below. That is a convenience worth having because this artifact is pulled once per engine change rather than once ever — but **try Download Container first**, since it is the one already proven here.

```bash
xcrun devicectl list devices
xcrun devicectl device info apps --device <DEVICE-UDID> | grep -i broodline
xcrun devicectl device copy from --device <DEVICE-UDID> \
  --domain-type appDataContainer --domain-identifier <BUNDLE-ID> \
  --source Documents/replay.bin --destination implementation/results/device-replay.bin
xcrun devicectl device copy from --device <DEVICE-UDID> \
  --domain-type appDataContainer --domain-identifier <BUNDLE-ID> \
  --source Documents/replay-outcome.txt --destination implementation/results/device-replay-outcome.txt
ls -l implementation/results/device-replay*
```

> **Recorded 2026-09-11: `devicectl` worked, and Download Container was never needed.**
>
> ```bash
> xcrun devicectl device copy from --device <UDID> \
>   --domain-type appDataContainer --domain-identifier com.sepandstudio.broodlinebench \
>   --source Documents --destination /tmp/wavepull
> ```
>
> **Copy the directory, not the file.** `--source Documents/replay.bin` fails silently — it reports success and writes nothing, which cost a poll loop that would have waited forever. `--source Documents` pulls both files and works first time.
>
> Two other things that cost a cycle each. The device must be **unlocked** or launch is refused outright (`FBSOpenApplicationErrorDomain error 7, Locked`). And Unity's generated Xcode project ships only a `GameAssembly` scheme, so `xcodebuild` has to be driven by `-target Unity-iPhone` with an explicit `CONFIGURATION_BUILD_DIR` rather than by `-scheme` with `-derivedDataPath`.
>
> The full sequence that worked, from a closed Editor and an unlocked connected device:
>
> ```bash
> Unity -batchmode -quit -projectPath client -executeMethod WaveBuilder.BuildIOS -logFile -
> xcodebuild -project build/ios-wave/Unity-iPhone.xcodeproj -target Unity-iPhone \
>   -configuration Debug -sdk iphoneos -allowProvisioningUpdates \
>   CONFIGURATION_BUILD_DIR="$PWD/build/ios-wave-out" build
> xcrun devicectl device install app --device <UDID> build/ios-wave-out/BroodlineBench.app
> xcrun devicectl device process launch --console --device <UDID> com.sepandstudio.broodlinebench
> ```

- [x] **Step 6: Track the artifact**

`.gitignore` ignores `implementation/results/*`. This artifact is evidence, like the sweep CSVs, and reproducing it needs a physical device:

```bash
git add -f implementation/results/device-replay.bin implementation/results/device-replay-outcome.txt
```

- [x] **Step 7: Write the test that re-simulates it**

`tests/engine/Combat/DeviceReplayTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// Phase 3's done-when: "a wave played on device replays bit-identically
    /// in xUnit."
    ///
    /// This proves something the corpus does not. cross-runtime-diff.sh proves
    /// CoreCLR and IL2CPP agree on generated scenarios run headlessly - that is
    /// arithmetic portability. This proves a wave run through a RENDERER, at a
    /// variable frame rate, with a human tap in it, consumed exactly the inputs
    /// its replay claims. Frame-pacing correctness, not arithmetic. Neither
    /// implies the other.
    public class DeviceReplayTests
    {
        private static string Artifact(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Broodline.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "implementation", "results", name);
        }

        [Fact]
        public void TheDeviceRunReSimulatesToTheSameHash()
        {
            string binPath = Artifact("device-replay.bin");
            string outPath = Artifact("device-replay-outcome.txt");

            Assert.True(File.Exists(binPath),
                "device-replay.bin is missing. It is captured by running the wave " +
                "on a device and pulling the artifact - Task 10 Step 5.");

            var record = Replay.Deserialize(File.ReadAllBytes(binPath));
            record.Validate();

            var lines = File.ReadAllLines(outPath);
            ulong deviceHash = ulong.Parse(lines[0]);
            var deviceResult = (Result)int.Parse(lines[1]);
            int deviceTicks = int.Parse(lines[2]);
            int deviceIntegrity = int.Parse(lines[3]);

            var replayed = Broodline.Sim.Combat.Sim.Replay(record);

            // The whole claim, in four asserts. A mismatch here means the
            // device consumed something the record does not describe - the
            // likeliest cause being a dropped tick or a Rally recorded against
            // wall-clock rather than a tick index.
            Assert.Equal(deviceHash, replayed.Hash);
            Assert.Equal(deviceResult, replayed.Result);
            Assert.Equal(deviceTicks, replayed.Ticks);
            Assert.Equal(deviceIntegrity, replayed.IntegrityRemaining);
        }

        [Fact]
        public void TheDeviceRunWasRecordedByThisEngineVersion()
        {
            // solo_execution section 9.4: a replay from a superseded engine
            // shows its recorded outcome and is not re-simulated. If this fails,
            // the test above is comparing across a balance change and its
            // verdict means nothing.
            var record = Replay.Deserialize(File.ReadAllBytes(Artifact("device-replay.bin")));
            Assert.Equal(SimVersion.Value, record.EngineVersion);
        }
    }
}
```

- [x] **Step 8: Run it**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~DeviceReplayTests"
```

Expected: PASS.

**If the hash differs, do not adjust the test.** Read the record — `Replay.Deserialize` then inspect `RallyTick` — and compare it against what the device logged. A Rally at a tick the device did not consume it at, or a tick count short of the device's, is the diagnosis. That divergence is the thing this whole phase exists to find.

- [x] **Step 9: Run everything and commit**

```bash
dotnet test Broodline.sln --nologo && ./implementation/scripts/run-unity-tests.sh EditMode
```

```bash
git add client/Assets/Game client/Assets/Game.meta tests/engine/Combat/DeviceReplayTests.cs
git add -f implementation/results/device-replay.bin implementation/results/device-replay-outcome.txt
git commit -m "feat: the device round-trip, and the phase's done-when

Wave 6 played on a real device, its inputs recorded, the artifact pulled
off and re-simulated on CoreCLR to the same hash.

This proves something the corpus does not. cross-runtime-diff.sh proves
CoreCLR and IL2CPP agree on generated scenarios run headlessly - that is
arithmetic portability. This proves a wave run through a renderer, at a
variable frame rate, with a human tap in it, consumed exactly the inputs
its replay claims. Frame-pacing correctness. Neither implies the other,
and only the second can be broken by an accumulator.

The artifact is tracked against the results gitignore, like the sweep
CSVs, and for the same reason: reproducing it needs a particular physical
device rather than a rerun.

The scene is generated from code rather than committed as a .unity file,
following Phase 0's BenchmarkSceneBuilder - it diffs as source and cannot
drift from what the code expects."
```

---

## What this plan deliberately does not do

- **No second raider, counter or lane.** Engine scope stays Phase 2's. Wave 8 remains the regression target for whichever phase adds Skirmisher and Splash.
- **No degradation ladder.** `client_architecture` §4's rungs key off entity count and wave 6 has one Courser — there is nothing to degrade. The count is displayed so the ladder arrives with a number already proven present and deterministic.
- **No `Broodline.UI`, `.Model` or `.Net`.** No Codex sheet, no Wave Defense chrome, no replay viewer screen, no deployment UI. The HUD at Task 9 is IMGUI precisely so nobody mistakes it for the screen.
- **No real art.** Phase 0's synthetic bodies at the same triangle, bone and material cost. **Phase 0's gate stays open** — it needs real meshes from a commission not yet placed, and nothing here changes that.
- **No server verification.** `Sim.Replay` is the shape it will take; wiring it is Phase 5's.
- **No Addressables, no persistence, no outbox.** `client_architecture` §§6–8, arriving with the backend.
- **No balance sweeps.** The batch runner is still minimal. Sweeping wants traits to sweep over.

## Definition of done

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
./implementation/scripts/run-unity-tests.sh EditMode
```

All three green, with:

- `SimRunner` stepped to completion reproducing both pinned golden hashes, and the count of `true` returns equal to `Outcome.Ticks`.
- `Sim.Run` reimplemented over `SimRunner`, with **one** tick loop in the codebase.
- The 500-scenario corpus baseline committed, proven to fail against a one-point damage perturbation, and reproducing after Task 2 with **no** re-baselining.
- No public member of `SimRunner` handing out `SimState` or a raw array.
- Rally halving the interval **after** Instinct modifiers — Vetch under Overwatch at **28** ticks, not 27 — and halving the remaining cooldown.
- An invalid Rally returning `false`, changing nothing, and appearing nowhere in the record.
- A replay round-tripping to an identical outcome hash, with and without Rally, in **under 512 bytes**.
- A replay whose stored HP or geometry disagrees with this engine throwing at load.
- A 200 ms stall producing exactly 6 catch-up steps and no dropped tick; a 500 ms stall capped at 8 and paid off by later frames.
- A Rally tapped between tick boundaries recorded at the **next** tick index.
- `implementation/results/device-replay.bin` tracked, and re-simulating on CoreCLR to the same hash, result, tick count and integrity the device reported.
- **`dotnet test` reporting `Skipped: 0`.** The two `DeviceReplayTests` skip themselves when the artifact is absent, so a green run with skips is a phase that has not been proven. Skipped is not passed.

## Edits owed elsewhere

Tracked here rather than made silently, because all three are normative text in documents this plan does not own.

| Document | Change | State |
|---|---|---|
| `specs/plans/broodline_phase3_unity_client.md` §5 | Rally is an absolute expiry tick read at the Attack phase, not a countdown in the State phase | **Applied** — the design was still in an open PR, so it was corrected rather than left wrong with a note beside it |
| `specs/plans/broodline_phase3_unity_client.md` §7 | The `WaveClock` contract is tested in EditMode, not PlayMode | **Applied**, same reason |
| `specs/plans/broodline_client_architecture.md` §2 | `ref readonly SimState` guarantees nothing — `SimState` is a sealed class whose readonly arrays hold mutable contents | **Owed.** Booked at the design's §9; Task 3 is what makes it concrete. Not applied here because `client_architecture` is a current document this phase does not own |
| `engine/Runtime/SimVersion.cs`, and a policy to go with it | **`SimVersion` is not bumped when engine behaviour changes.** It was still `0.1.0` after Task 4 moved every hash in the project, and nothing reads it except the replay's own `EngineVersion` field. That makes `solo_execution` §9.4 — *"a replay recorded under a superseded engine version renders its stored outcome and is not re-simulated"* — **inert**: a pre-Rally replay still claims `0.1.0` and would be silently re-simulated into a different outcome, which is exactly what the rule exists to prevent | **Owed, and it needs a decision rather than an edit.** Bumping the constant is trivial; deciding *what forces a bump* and *what enforces it* is not, and it belongs with whoever owns release cadence. Not bumped here, because the tracked Editor artifact was recorded under this engine and marking it superseded would falsely invalidate the evidence |
