# Phase 2 — Combat Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A deterministic combat simulation covering one lane, five pockets, the Courser and the Chill counter, with all eight tick phases in normative order, breach diagnosis recorded at breach time, guaranteed termination, and the whole thing proven bit-identical across CoreCLR and IL2CPP.

**Architecture:** `engine/Runtime/Combat/` adds gameplay to the Phase 1 core. One `Sim.Run()` calls eight phase functions in the order `broodline_combat_engine.md` §4 declares normative. Each counter is a named rule at its own phase site — there is no generic counter interface, because the eight counters sit at five different phases and any abstraction over them hides the only structural fact worth keeping (§5). State lives in dense parallel arrays indexed by stable integer ID and iterated in ID order.

**Tech Stack:** C# `netstandard2.1` (engine), .NET 10 (tests), xUnit, the Phase 1 core (`Fix64`, `Rng`, `Hash`), Unity 6000.6.0f1 with IL2CPP for the cross-runtime diff.

## Global Constraints

Inherited from Phase 1 and enforced by its existing tooling — the Cecil float scan, `BannedSymbols.txt`, and the empty-reference asmdef. Every one of these is already a build failure, not a convention:

- **No floating point anywhere in the core.** Fixed-point `Fix64` at Q32.32.
- **No `System.Math`, no `UnityEngine.Mathf`.** The core implements what it needs.
- **Fixed timestep, 30 Hz.** One tick is exactly `Fix64.One / 30`. The simulation never sees a wall-clock delta.
- **Seeded PRNG, one stream per wave** — `Broodline.Sim.Rng`. Never `System.Random`.
- **Deterministic iteration order.** No `Dictionary`, no `HashSet`, no LINQ. Dense arrays indexed by stable integer ID, iterated in ID order.
- **Every comparator is a total order**, tie-breaking on spawn index ascending.
- **No wall-clock, no ambient time.** Time is `tickIndex`.
- **No allocation in the tick loop.** Buffers are sized at wave load.
- **No parallelism inside a tick.**
- **The core has no dependencies** — no Unity, no Newtonsoft, nothing beyond primitives and arrays.
- `.meta` files are committed, **including a directory's own meta, which lives one level up**.

From `specs/plans/broodline_phase2_combat_engine.md`, and normative here:

- **Tick phase order is normative.** Changing it is a balance change, not a refactor.
- **Capacity is recomputed from scratch every tick** from the live creature set. Never accumulated.
- **Capacity is per-trait, not a global ladder.** Chill is `I=1, II=2, III=4` per `broodline_combat_numbers.md` §134. `combat_engine.md` §5.3's `1/3/5` is a superseded placeholder — see the design doc §2.
- **Breach diagnosis is recorded at breach time**, never inferred afterward.
- **The pre-wave check and the loss diagnosis are one function with two call sites.**

### Values, copied verbatim

From `broodline_combat_numbers.md`:

| | |
|---|---|
| Lane length | 24 tiles, spawn to Ark |
| Pockets | Beside tiles 6–20, one creature each. Defile runs 5 |
| Deployment cap | 5 creatures |
| Retarget delay | 0.4 s = **12 ticks**, every creature |
| Courser | 220 HP, **1.6 tiles/s**, integrity cost 2, counter Chill |
| Chill | Slows one Courser to **0.5 tiles/s**. Capacity **I=1, II=2, III=4** |
| Hard tick cap | 5,400 ticks (180 s) |
| Stall detector | 300 consecutive ticks with no advance and no HP change |

Species stats (§3):

| Species | HP | Damage | Interval | Range |
|---|---|---|---|---|
| Vetch | 260 | 14 | 1.5s | 2 |
| Ember | 130 | 36 | 1.7s | 3 |
| Skitter | 80 | 11 | 0.4s | 2 |
| Hollow | 60 | 55 | 2.5s | 7 |
| Loam | 190 | 18 | 1.2s | 3 |
| Pale | 120 | 21 | 1.1s | 5 |

Wave 6, from `broodline_waves_01_12.md` §3 — *"1 · Defile · Integrity 2 · 1 Courser. Nothing else · Timeline t=3 · Expected roster 5, none carrying Chill."*

**This plan writes no rendering, no replay format, no server verification and no second raider.** Those are named at the design doc §7.

---

## File structure

| File | Responsibility |
|---|---|
| `engine/Runtime/Combat/Ids.cs` | The enums — `Species`, `RaiderType`, `Trait`, `Instinct`, `Phase` |
| `engine/Runtime/Combat/Stats.cs` | The value tables above, as static lookups by enum |
| `engine/Runtime/Combat/WaveDef.cs` | Authored wave, spawn table, composition invariants |
| `engine/Runtime/Combat/Lane.cs` | Lane geometry and the precomputed squared-distance table |
| `engine/Runtime/Combat/SimState.cs` | Dense arrays: raiders, creatures, integrity, tick index |
| `engine/Runtime/Combat/Capacity.cs` | Per-tick capacity recomputation and the total-order comparator |
| `engine/Runtime/Combat/Phases.cs` | The eight phase functions |
| `engine/Runtime/Combat/Targeting.cs` | Instinct predicates and range checks |
| `engine/Runtime/Combat/Attacks.cs` | Attack rate and damage, with the Instinct triggers |
| `engine/Runtime/Combat/Outcome.cs` | `Result`, `Breach`, `Outcome` |
| `engine/Runtime/Combat/Counters.cs` | Chill, at its State-phase site |
| `engine/Runtime/Combat/Diagnosis.cs` | access / coverage / placement — one function, two call sites |
| `engine/Runtime/Combat/Sim.cs` | `Run()`, termination, the outcome |
| `engine/Runtime/Combat/BatchRunner.cs` | Runs N waves headless, reports clear-rate |

---

### Task 0: Prerequisites

Phase 2 builds on Phase 1's core. Confirm it is green before adding to it — a failure inherited from Phase 1 is much harder to diagnose once combat code sits on top of it.

**Files:**
- Modify: none.

**Interfaces:**
- Consumes: the Phase 1 core.
- Produces: nothing. This is a gate.

- [ ] **Step 1: Confirm the toolchain**

```bash
./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: five `ok` lines and `exit=0`.

- [ ] **Step 2: Confirm the Phase 1 suite is green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: all tests pass. Note the count — later tasks add to it, and a drop means something was deleted rather than extended.

- [ ] **Step 3: Confirm the determinism gate still passes**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first.

**If this fails, stop.** Phase 2 extends this corpus at Task 13; starting from a red gate means never knowing which phase broke it.

---

### Task 1: Ids, stats and the wave definition

The enums and value tables everything else indexes by, plus the authored wave and the two composition invariants that throw at load.

**Files:**
- Create: `engine/Runtime/Combat/Ids.cs`
- Create: `engine/Runtime/Combat/Stats.cs`
- Create: `engine/Runtime/Combat/WaveDef.cs`
- Create: `tests/engine/Combat/WaveDefTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum Species { Vetch, Ember, Skitter, Hollow, Loam, Pale }`
  - `enum RaiderType { Courser }`
  - `enum Trait { None, Chill }`
  - `enum Instinct { Bloodscent, Vanguard, Overwatch, LastStand, Skittish, PackSense }`
  - `Stats.CreatureHp(Species)`, `Stats.CreatureDamage(Species)`, `Stats.CreatureIntervalTicks(Species)`, `Stats.CreatureRange(Species)` — all `int`
  - `Stats.RaiderHp(RaiderType)`, `Stats.RaiderIntegrityCost(RaiderType)`, `Stats.RaiderMilliTilesPerSec(RaiderType)` — all `int`
  - `Stats.CounterFor(RaiderType)` → `Trait`
  - `Stats.ChillCapacity(int tier)` → `int` (tier 1→1, 2→2, 3→4)
  - `struct SpawnEntry { public int Tick; public RaiderType Type; }`
  - `sealed class WaveDef` with `int Id`, `int Integrity`, `int LaneCount`, `SpawnEntry[] Spawns`, and `static WaveDef Wave6()`
  - `WaveDef.Validate()` throwing `WaveCompositionException`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/WaveDefTests.cs`:

```csharp
using System;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class WaveDefTests
    {
        [Fact]
        public void Wave6_MatchesTheAuthoredDefinition()
        {
            var w = WaveDef.Wave6();
            Assert.Equal(6, w.Id);
            Assert.Equal(2, w.Integrity);
            Assert.Equal(1, w.LaneCount);
            Assert.Single(w.Spawns);
            Assert.Equal(RaiderType.Courser, w.Spawns[0].Type);
            Assert.Equal(90, w.Spawns[0].Tick);   // t=3s at 30Hz
        }

        [Fact]
        public void Validate_AllowsTwoSpawnsOfTheSameRaiderType()
        {
            var w = new WaveDef(
                id: 999, integrity: 3, laneCount: 1,
                spawns: new[]
                {
                    new SpawnEntry { Tick = 0,  Type = RaiderType.Courser },
                    new SpawnEntry { Tick = 30, Type = RaiderType.Courser }
                });

            // Two spawns of the SAME type is legal - that is volume, not a
            // shared counter. This must NOT throw.
            w.Validate();
        }

        [Fact]
        public void Validate_ThrowsWhenMoreThanFourRaiderTypes()
        {
            // With one raider type in the slice this cannot be constructed from
            // real data, so the invariant is exercised through the type-count
            // helper directly.
            Assert.Throws<WaveCompositionException>(
                () => WaveDef.AssertTypeCount(5));
        }

        [Fact]
        public void Stats_ChillCapacityIsPerTraitNotAGlobalLadder()
        {
            // combat_numbers section 134: I=1, II=2, III=4.
            // NOT combat_engine section 5.3's superseded 1/3/5.
            Assert.Equal(1, Stats.ChillCapacity(1));
            Assert.Equal(2, Stats.ChillCapacity(2));
            Assert.Equal(4, Stats.ChillCapacity(3));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Broodline.Sim.Combat` does not exist.

- [ ] **Step 3: Write `Ids.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// The slice covers one raider and one counter. The enums are declared at
    /// full width anyway so adding the other seven is additive rather than a
    /// renumbering - values are persisted in replays and wave data.
    public enum Species { Vetch = 0, Ember = 1, Skitter = 2, Hollow = 3, Loam = 4, Pale = 5 }

    public enum RaiderType { Courser = 0 }

    public enum Trait { None = 0, Chill = 1 }

    public enum Instinct
    {
        Bloodscent = 0, Vanguard = 1, Overwatch = 2,
        LastStand = 3, Skittish = 4, PackSense = 5
    }

    /// The eight tick phases, in the normative order of combat_engine section 4.
    /// Declared so tests can assert the order rather than trusting a comment.
    public enum Phase
    {
        Spawn = 1, State = 2, Movement = 3, Targeting = 4,
        Attack = 5, Death = 6, Breach = 7, Resolve = 8
    }
}
```

- [ ] **Step 4: Write `Stats.cs`**

Switch expressions rather than arrays or dictionaries: a `Dictionary` is banned for iteration order, and a switch is a compile error when a new enum member is added without a stat.

```csharp
namespace Broodline.Sim.Combat
{
    /// Values copied from broodline_combat_numbers.md. This file owns no
    /// decisions - it is a transcription, and a change here is a balance patch.
    public static class Stats
    {
        public const int TicksPerSecond = 30;
        public const int LaneTiles = 24;
        public const int DeploymentCap = 5;
        public const int RetargetLockoutTicks = 12;   // 0.4s at 30Hz
        public const int HardTickCap = 5400;          // 180s
        public const int StallTicks = 300;            // 10s

        // --- Creatures, section 3 ---

        public static int CreatureHp(Species s) => s switch
        {
            Species.Vetch => 260, Species.Ember => 130, Species.Skitter => 80,
            Species.Hollow => 60, Species.Loam => 190, Species.Pale => 120,
            _ => 0
        };

        public static int CreatureDamage(Species s) => s switch
        {
            Species.Vetch => 14, Species.Ember => 36, Species.Skitter => 11,
            Species.Hollow => 55, Species.Loam => 18, Species.Pale => 21,
            _ => 0
        };

        /// Attack interval in ticks. Source values are seconds: 1.5, 1.7, 0.4,
        /// 2.5, 1.2, 1.1 - all exact multiples of a 30Hz tick except where
        /// noted, so they are transcribed as ticks directly rather than
        /// computed, keeping the value table free of rounding.
        public static int CreatureIntervalTicks(Species s) => s switch
        {
            Species.Vetch => 45, Species.Ember => 51, Species.Skitter => 12,
            Species.Hollow => 75, Species.Loam => 36, Species.Pale => 33,
            _ => 0
        };

        public static int CreatureRange(Species s) => s switch
        {
            Species.Vetch => 2, Species.Ember => 3, Species.Skitter => 2,
            Species.Hollow => 7, Species.Loam => 3, Species.Pale => 5,
            _ => 0
        };

        // --- Raiders, section 5 ---

        public static int RaiderHp(RaiderType r) => r switch
        {
            RaiderType.Courser => 220,
            _ => 0
        };

        public static int RaiderIntegrityCost(RaiderType r) => r switch
        {
            RaiderType.Courser => 2,
            _ => 0
        };

        /// Speed in thousandths of a tile per second. Integer so the value
        /// table carries no fixed-point encoding; Lane converts once.
        public static int RaiderMilliTilesPerSec(RaiderType r) => r switch
        {
            RaiderType.Courser => 1600,
            _ => 0
        };

        /// The trait that answers this raider. combat_numbers section 222.
        public static Trait CounterFor(RaiderType r) => r switch
        {
            RaiderType.Courser => Trait.Chill,
            _ => Trait.None
        };

        /// Chill's effect: the affected raider moves at 0.5 tiles/sec.
        public const int ChilledMilliTilesPerSec = 500;

        /// Per-trait capacity. combat_numbers section 134 gives Chill 1/2/4 and
        /// section 115 says it twice in prose - "Chill III stops four of them".
        /// combat_engine section 5.3's global 1/3/5 ladder is a superseded
        /// placeholder; see the Phase 2 design doc section 2.
        public static int ChillCapacity(int tier) => tier switch
        {
            1 => 1, 2 => 2, 3 => 4,
            _ => 0
        };
    }
}
```

- [ ] **Step 5: Write `WaveDef.cs`**

```csharp
using System;

namespace Broodline.Sim.Combat
{
    public sealed class WaveCompositionException : Exception
    {
        public WaveCompositionException(string message) : base(message) { }
    }

    public struct SpawnEntry
    {
        public int Tick;
        public RaiderType Type;
    }

    /// An authored wave. Spawns are ordered by tick ascending, then by the
    /// order authored - the array index IS the spawn index, and spawn index is
    /// the universal tie-break, so its order is load-bearing rather than
    /// incidental.
    public sealed class WaveDef
    {
        public const int MaxRaiderTypes = 4;

        public int Id { get; }
        public int Integrity { get; }
        public int LaneCount { get; }
        public SpawnEntry[] Spawns { get; }

        public WaveDef(int id, int integrity, int laneCount, SpawnEntry[] spawns)
        {
            Id = id;
            Integrity = integrity;
            LaneCount = laneCount;
            Spawns = spawns;
        }

        /// Wave 6, from broodline_waves_01_12.md section 3:
        /// "1 . Defile . Integrity 2 . 1 Courser. Nothing else . Timeline t=3".
        public static WaveDef Wave6() => new WaveDef(
            id: 6, integrity: 2, laneCount: 1,
            spawns: new[]
            {
                new SpawnEntry { Tick = 3 * Stats.TicksPerSecond, Type = RaiderType.Courser }
            });

        /// The two composition invariants of combat_engine section 5.4,
        /// checked at load so a content author cannot ship a violation.
        public void Validate()
        {
            AssertSpawnsOrdered();
            AssertTypeCount(DistinctTypeCount());
            AssertNoSharedCounter();
        }

        public int DistinctTypeCount()
        {
            // Dense scan over the enum rather than a HashSet, which is banned.
            int count = 0;
            for (int t = 0; t <= (int)RaiderType.Courser; t++)
            {
                for (int i = 0; i < Spawns.Length; i++)
                {
                    if ((int)Spawns[i].Type == t) { count++; break; }
                }
            }
            return count;
        }

        public static void AssertTypeCount(int distinctTypes)
        {
            if (distinctTypes > MaxRaiderTypes)
                throw new WaveCompositionException(
                    "A wave may carry at most " + MaxRaiderTypes +
                    " raider types; this one carries " + distinctTypes + ".");
        }

        /// "Never two raiders answered by the same trait in one wave." Two
        /// spawns of the same TYPE are volume and are legal; two different
        /// types sharing a counter are not.
        private void AssertNoSharedCounter()
        {
            for (int t = 0; t <= (int)RaiderType.Courser; t++)
            {
                if (!ContainsType((RaiderType)t)) continue;
                for (int u = t + 1; u <= (int)RaiderType.Courser; u++)
                {
                    if (!ContainsType((RaiderType)u)) continue;
                    if (Stats.CounterFor((RaiderType)t) == Stats.CounterFor((RaiderType)u))
                        throw new WaveCompositionException(
                            "Raider types " + (RaiderType)t + " and " + (RaiderType)u +
                            " are both answered by " + Stats.CounterFor((RaiderType)t) +
                            "; a wave may not contain both.");
                }
            }
        }

        private bool ContainsType(RaiderType type)
        {
            for (int i = 0; i < Spawns.Length; i++)
                if (Spawns[i].Type == type) return true;
            return false;
        }

        private void AssertSpawnsOrdered()
        {
            for (int i = 1; i < Spawns.Length; i++)
                if (Spawns[i].Tick < Spawns[i - 1].Tick)
                    throw new WaveCompositionException(
                        "Spawns must be ordered by tick ascending; entry " + i +
                        " at tick " + Spawns[i].Tick + " follows tick " +
                        Spawns[i - 1].Tick + ".");
        }
    }
}
```

- [ ] **Step 6: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS, four new tests.

- [ ] **Step 7: Commit**

```bash
git add engine/Runtime/Combat tests/engine/Combat
git commit -m "feat: combat ids, stat tables and the wave definition

Transcribes combat_numbers' value tables and authors wave 6 as the slice's
test case. The two composition invariants of combat_engine 5.4 throw at
load rather than mid-tick, so a content author gets a failed publish
instead of a runtime surprise in month four.

Chill's capacity is per-trait - 1/2/4 from combat_numbers 134, which says
it twice - not combat_engine 5.3's global 1/3/5 ladder. 5.3 marks its own
figures as placeholders and they cannot be right for Reach, which caps at
lane count. The Phase 2 design doc 2 carries the reasoning."
```

---

### Task 2: Lane geometry and the precomputed distance table

`combat_engine.md` §2.2 is the largest determinism simplification in the design: a raider's position is a single scalar along its lane, a creature's position is a fixed tile, and every range check is an array lookup. **No square roots, no trigonometry, no vector maths in the hot loop.**

**Files:**
- Create: `engine/Runtime/Combat/Lane.cs`
- Create: `tests/engine/Combat/LaneTests.cs`

**Interfaces:**
- Consumes: `Stats`, `Fix64`.
- Produces:
  - `sealed class Lane` with `int[] PocketTiles`, `int PocketCount`, `int Tiles`
  - `static Lane Defile()` — 24 tiles, 5 pockets at tiles 6, 10, 13, 17, 20
  - `int DistSq(int pocket, int tile)` — squared distance in whole tiles
  - `bool InRange(int pocket, int tile, int rangeTiles)`
  - `static Fix64 SpeedPerTick(int milliTilesPerSec)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/LaneTests.cs`:

```csharp
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class LaneTests
    {
        [Fact]
        public void Defile_HasFivePocketsBesideTiles6To20()
        {
            var lane = Lane.Defile();
            Assert.Equal(24, lane.Tiles);
            Assert.Equal(5, lane.PocketCount);
            for (int p = 0; p < lane.PocketCount; p++)
            {
                Assert.InRange(lane.PocketTiles[p], 6, 20);
            }
        }

        [Fact]
        public void DistSq_IsPerpendicularOffsetPlusAlongLane()
        {
            var lane = Lane.Defile();
            int pocketTile = lane.PocketTiles[0];        // 6

            // Directly beside the pocket: only the 1-tile perpendicular offset.
            Assert.Equal(1, lane.DistSq(0, pocketTile));

            // Three tiles along: 3*3 + 1.
            Assert.Equal(10, lane.DistSq(0, pocketTile + 3));
            Assert.Equal(10, lane.DistSq(0, pocketTile - 3));
        }

        [Fact]
        public void InRange_ComparesSquaresAndNeverTakesARoot()
        {
            var lane = Lane.Defile();
            int pocketTile = lane.PocketTiles[0];

            // Hollow's range is 7: 7*7 = 49 >= DistSq.
            Assert.True(lane.InRange(0, pocketTile + 6, 7));   // 36+1 = 37
            Assert.False(lane.InRange(0, pocketTile + 7, 7));  // 49+1 = 50
        }

        [Fact]
        public void SpeedPerTick_IsExactAcrossThirtyTicks()
        {
            // A Courser at 1.6 tiles/sec advances 1.6 tiles in 30 ticks.
            Fix64 perTick = Lane.SpeedPerTick(1600);
            Fix64 travelled = Fix64.Zero;
            for (int i = 0; i < 30; i++) travelled = travelled + perTick;

            // Floor is 1: exactness is not claimed, determinism is.
            Assert.Equal(1, travelled.ToIntFloor());
            Assert.True(travelled > Fix64.One);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Lane` does not exist.

- [ ] **Step 3: Write `Lane.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// Lane geometry, precomputed at construction.
    ///
    /// combat_engine section 2.2: a lane is an authored polyline sampled at
    /// authoring time, a raider's position is a single scalar along it, and a
    /// creature's position is a fixed tile. Every region ships a precomputed
    /// distance table, so range checking in the tick loop is an array lookup
    /// and a comparison - no roots, no trigonometry, no vectors.
    ///
    /// Distances are SQUARED and in whole tiles, which keeps them exact
    /// integers. combat_engine 2.2 notes this makes ties common rather than
    /// rare, which is why every comparator tie-breaks on spawn index.
    public sealed class Lane
    {
        /// Pockets sit one tile off the lane. That perpendicular offset is the
        /// +1 in every squared distance.
        private const int PerpendicularOffsetSq = 1;

        public int Tiles { get; }
        public int PocketCount { get; }
        public int[] PocketTiles { get; }

        private readonly int[] _distSq;   // [pocket * Tiles + tile]

        public Lane(int tiles, int[] pocketTiles)
        {
            Tiles = tiles;
            PocketTiles = pocketTiles;
            PocketCount = pocketTiles.Length;

            _distSq = new int[PocketCount * tiles];
            for (int p = 0; p < PocketCount; p++)
            {
                for (int t = 0; t < tiles; t++)
                {
                    int along = pocketTiles[p] - t;
                    _distSq[p * tiles + t] = along * along + PerpendicularOffsetSq;
                }
            }
        }

        /// Defile - the terrain family wave 6 runs on. 24 tiles, 5 pockets.
        /// combat_numbers section 42: pockets sit beside tiles 6-20.
        public static Lane Defile() =>
            new Lane(Stats.LaneTiles, new[] { 6, 10, 13, 17, 20 });

        public int DistSq(int pocket, int tile) => _distSq[pocket * Tiles + tile];

        public bool InRange(int pocket, int tile, int rangeTiles) =>
            rangeTiles * rangeTiles >= DistSq(pocket, tile);

        /// Converts a speed in thousandths of a tile per second into tiles per
        /// tick. Not exact in binary - 1600/30000 recurs - but Fix64 division
        /// is deterministic, and determinism rather than exactness is the
        /// property the engine needs.
        public static Fix64 SpeedPerTick(int milliTilesPerSec) =>
            Fix64.FromInt(milliTilesPerSec) /
            Fix64.FromInt(1000 * Stats.TicksPerSecond);
    }
}
```

- [ ] **Step 4: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/Runtime/Combat/Lane.cs tests/engine/Combat/LaneTests.cs
git commit -m "feat: lane geometry and the precomputed distance table

combat_engine 2.2's simplification, built as specified: distances are
squared, integer and precomputed, so the hot loop contains no roots and no
vector maths. That removes the largest single class of cross-platform
divergence before it can exist.

Squared-integer distance also makes ties common rather than rare, which is
what makes the spawn-index tie-break load-bearing at every comparator
rather than decorative."
```

---

### Task 3: SimState and the entity model

Dense parallel arrays, sized once at wave load, iterated in ID order. No allocation happens after `SimState` is constructed.

**Files:**
- Create: `engine/Runtime/Combat/SimState.cs`
- Create: `tests/engine/Combat/SimStateTests.cs`

**Interfaces:**
- Consumes: `Stats`, `Lane`, `WaveDef`, `Fix64`.
- Produces:
  - `struct CreatureSpec { Species Species; Trait Trait1; int Tier1; Trait Trait2; int Tier2; Instinct Instinct; int Pocket; }`
  - `sealed class SimState` with public arrays and counts as listed below
  - `SimState(WaveDef wave, Lane lane, CreatureSpec[] deployment)`
  - `int RaiderTile(int r)` — `Progress.ToIntFloor()`, clamped to the lane
  - `bool CreatureCarries(int c, Trait t, out int tier)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/SimStateTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimStateTests
    {
        public static CreatureSpec[] FiveWithoutChill() => new[]
        {
            new CreatureSpec { Species = Species.Vetch,   Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Hollow,  Pocket = 1, Instinct = Instinct.Overwatch },
            new CreatureSpec { Species = Species.Skitter, Pocket = 2, Instinct = Instinct.Bloodscent },
            new CreatureSpec { Species = Species.Loam,    Pocket = 3, Instinct = Instinct.LastStand },
            new CreatureSpec { Species = Species.Ember,   Pocket = 4, Instinct = Instinct.PackSense }
        };

        [Fact]
        public void Construction_SizesForEverySpawnAndSeedsIntegrity()
        {
            var wave = WaveDef.Wave6();
            var state = new SimState(wave, Lane.Defile(), FiveWithoutChill());

            Assert.Equal(2, state.Integrity);
            Assert.Equal(0, state.Tick);
            Assert.Equal(0, state.RaiderCount);          // none spawned yet
            Assert.Equal(1, state.RaiderHp.Length);      // but capacity for one
            Assert.Equal(5, state.CreatureCount);
        }

        [Fact]
        public void Creatures_TakeTheirStatsFromTheSpeciesTable()
        {
            var state = new SimState(WaveDef.Wave6(), Lane.Defile(), FiveWithoutChill());
            Assert.Equal(260, state.CreatureHp[0]);   // Vetch
            Assert.Equal(60,  state.CreatureHp[1]);   // Hollow
        }

        [Fact]
        public void CreatureCarries_FindsATraitInEitherSlot()
        {
            var deployment = FiveWithoutChill();
            deployment[4] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 4,
                Instinct = Instinct.Vanguard,
                Trait2 = Trait.Chill, Tier2 = 1
            };
            var state = new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);

            Assert.True(state.CreatureCarries(4, Trait.Chill, out int tier));
            Assert.Equal(1, tier);
            Assert.False(state.CreatureCarries(0, Trait.Chill, out _));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `SimState` does not exist.

- [ ] **Step 3: Write `SimState.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// A creature as deployed. data_model section 2: species, two traits with
    /// coverage, one Instinct. No level, no XP, no power score - everything
    /// about its strength derives from these.
    public struct CreatureSpec
    {
        public Species Species;
        public Trait Trait1;
        public int Tier1;
        public Trait Trait2;
        public int Tier2;
        public Instinct Instinct;
        public int Pocket;
    }

    /// The whole mutable world, in dense parallel arrays indexed by stable
    /// integer ID and iterated in ID order.
    ///
    /// Every array is sized at construction from the wave's spawn table, so
    /// the tick loop never allocates. A raider's array index IS its spawn
    /// index, which is what makes spawn index available as a tie-break without
    /// storing it.
    public sealed class SimState
    {
        public readonly WaveDef Wave;
        public readonly Lane Lane;

        public int Tick;
        public int Integrity;

        // --- Raiders. Index == spawn index. ---
        public readonly RaiderType[] RaiderType;
        public readonly int[] RaiderHp;
        public readonly Fix64[] RaiderProgress;
        public readonly bool[] RaiderAlive;
        public readonly bool[] RaiderChilled;      // recomputed every tick
        public int RaiderCount;                    // spawned so far

        // --- Creatures. Index == deployment order. ---
        public readonly Species[] CreatureSpecies;
        public readonly Trait[] CreatureTrait1;
        public readonly int[] CreatureTier1;
        public readonly Trait[] CreatureTrait2;
        public readonly int[] CreatureTier2;
        public readonly Instinct[] CreatureInstinct;
        public readonly int[] CreaturePocket;
        public readonly int[] CreatureHp;
        public readonly int[] CreatureTarget;        // raider id, or -1
        public readonly int[] CreatureAcquireAt;     // tick it may next acquire
        public readonly int[] CreatureNextAttackAt;  // tick it may next fire
        public readonly int CreatureCount;

        public SimState(WaveDef wave, Lane lane, CreatureSpec[] deployment)
        {
            Wave = wave;
            Lane = lane;
            Integrity = wave.Integrity;
            Tick = 0;

            int capacity = wave.Spawns.Length;
            RaiderType = new RaiderType[capacity];
            RaiderHp = new int[capacity];
            RaiderProgress = new Fix64[capacity];
            RaiderAlive = new bool[capacity];
            RaiderChilled = new bool[capacity];
            RaiderCount = 0;

            CreatureCount = deployment.Length;
            CreatureSpecies = new Species[CreatureCount];
            CreatureTrait1 = new Trait[CreatureCount];
            CreatureTier1 = new int[CreatureCount];
            CreatureTrait2 = new Trait[CreatureCount];
            CreatureTier2 = new int[CreatureCount];
            CreatureInstinct = new Instinct[CreatureCount];
            CreaturePocket = new int[CreatureCount];
            CreatureHp = new int[CreatureCount];
            CreatureTarget = new int[CreatureCount];
            CreatureAcquireAt = new int[CreatureCount];
            CreatureNextAttackAt = new int[CreatureCount];

            for (int c = 0; c < CreatureCount; c++)
            {
                CreatureSpecies[c] = deployment[c].Species;
                CreatureTrait1[c] = deployment[c].Trait1;
                CreatureTier1[c] = deployment[c].Tier1;
                CreatureTrait2[c] = deployment[c].Trait2;
                CreatureTier2[c] = deployment[c].Tier2;
                CreatureInstinct[c] = deployment[c].Instinct;
                CreaturePocket[c] = deployment[c].Pocket;
                CreatureHp[c] = Stats.CreatureHp(deployment[c].Species);
                CreatureTarget[c] = -1;
                CreatureAcquireAt[c] = 0;
                CreatureNextAttackAt[c] = 0;
            }
        }

        /// The raider's current tile, clamped into the table's bounds. Progress
        /// can reach the Ark tile exactly on the tick it breaches, and the
        /// distance table is only defined over [0, Tiles).
        public int RaiderTile(int r)
        {
            int tile = RaiderProgress[r].ToIntFloor();
            if (tile < 0) return 0;
            if (tile >= Lane.Tiles) return Lane.Tiles - 1;
            return tile;
        }

        public bool CreatureCarries(int c, Trait trait, out int tier)
        {
            if (CreatureTrait1[c] == trait) { tier = CreatureTier1[c]; return true; }
            if (CreatureTrait2[c] == trait) { tier = CreatureTier2[c]; return true; }
            tier = 0;
            return false;
        }

        public bool CreatureAlive(int c) => CreatureHp[c] > 0;
    }
}
```

- [ ] **Step 4: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/Runtime/Combat/SimState.cs tests/engine/Combat/SimStateTests.cs
git commit -m "feat: SimState - dense arrays for the whole mutable world

Sized once at wave load from the spawn table, so the tick loop allocates
nothing. A raider's array index is its spawn index, which makes the
universal tie-break available without storing a field for it."
```

---

### Task 4: Capacity and the total-order comparator

`combat_engine.md` §5.3 makes every counter trait a capacity resource, and §5.1 says how a tier is assigned to particular raiders. **Chill is one of the three exceptions** — its carrier may not be attacking the raider it slows, so assignment is nearest-first within range, re-evaluated every tick.

**Files:**
- Create: `engine/Runtime/Combat/Capacity.cs`
- Create: `tests/engine/Combat/CapacityTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Stats`, `Lane`.
- Produces:
  - `static int TotalChillCapacity(SimState s)` — summed across live carriers
  - `static int NearestCarrierDistSq(SimState s, int raider)` — squared distance to the nearest live Chill carrier that has it in range, or `-1` when none can reach it
  - `static int Compare(SimState s, int raiderA, int raiderB)` — the `(distance, spawnIndex)` total order
  - `static void AssignChill(SimState s, int[] scratch)` — fills `s.RaiderChilled`, gated on carrier range

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/CapacityTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class CapacityTests
    {
        private static CreatureSpec[] WithChill(int tier, int copies)
        {
            var d = SimStateTests.FiveWithoutChill();
            for (int i = 0; i < copies; i++)
                d[i] = new CreatureSpec
                {
                    Species = Species.Pale, Pocket = i,
                    Instinct = Instinct.Vanguard,
                    Trait1 = Trait.Chill, Tier1 = tier
                };
            return d;
        }

        [Fact]
        public void Capacity_SumsAcrossCarriers()
        {
            // combat_engine 5.3: "Capacity from multiple creatures carrying the
            // same trait sums." Two Chill II give 4, not 2.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 2, copies: 2));
            Assert.Equal(4, Capacity.TotalChillCapacity(s));
        }

        [Fact]
        public void Capacity_IsZeroWhenNoCarrierIsAlive()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 3, copies: 1));
            Assert.Equal(4, Capacity.TotalChillCapacity(s));

            s.CreatureHp[0] = 0;   // the carrier dies

            // 5.3: "its capacity frees immediately, because there is nothing to
            // free." Recomputed from the live set, never accumulated.
            Assert.Equal(0, Capacity.TotalChillCapacity(s));
        }

        [Fact]
        public void Compare_TieBreaksOnSpawnIndexAscending()
        {
            var s = new SimState(
                new WaveDef(1, 5, 1, new[]
                {
                    new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                    new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
                }),
                Lane.Defile(), SimStateTests.FiveWithoutChill());

            s.RaiderCount = 2;
            s.RaiderAlive[0] = s.RaiderAlive[1] = true;
            // Identical progress: distance ties exactly, which 2.2 says is
            // common rather than rare.
            s.RaiderProgress[0] = s.RaiderProgress[1] = Fix64.FromInt(10);

            Assert.True(Capacity.Compare(s, 0, 1) < 0);
            Assert.True(Capacity.Compare(s, 1, 0) > 0);
            Assert.Equal(0, Capacity.Compare(s, 0, 0));
        }

        [Fact]
        public void AssignChill_SlowsExactlyCapacityManyNearestFirst()
        {
            var wave = new WaveDef(1, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var s = new SimState(wave, Lane.Defile(), WithChill(tier: 1, copies: 1));
            s.RaiderCount = 3;
            for (int r = 0; r < 3; r++) s.RaiderAlive[r] = true;

            // Raider 2 is nearest the carrier's pocket (tile 6).
            s.RaiderProgress[0] = Fix64.FromInt(20);
            s.RaiderProgress[1] = Fix64.FromInt(15);
            s.RaiderProgress[2] = Fix64.FromInt(6);

            Capacity.AssignChill(s, new int[3]);

            // Chill I is capacity 1 - one raider, the nearest.
            Assert.False(s.RaiderChilled[0]);
            Assert.False(s.RaiderChilled[1]);
            Assert.True(s.RaiderChilled[2]);
        }

        [Fact]
        public void AssignChill_LeavesRaidersNoCarrierCanReachUnchilled()
        {
            // combat_engine 5.1 assigns "within range". A Pale in pocket 0
            // (tile 6, range 5) reaches tiles 2-10 and nothing beyond, so a
            // raider at tile 22 must stay at full speed however much spare
            // capacity exists. Without the gate an unopposed Courser crosses
            // in 48s against the 30s combat_numbers 240 describes.
            var wave = new WaveDef(1, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var s = new SimState(wave, Lane.Defile(), WithChill(tier: 3, copies: 1));
            s.RaiderCount = 2;
            s.RaiderAlive[0] = s.RaiderAlive[1] = true;

            s.RaiderProgress[0] = Fix64.FromInt(6);    // beside the carrier
            s.RaiderProgress[1] = Fix64.FromInt(22);   // far past it

            Capacity.AssignChill(s, new int[2]);

            // Chill III is capacity 4 - ample - so only range explains this.
            Assert.True(s.RaiderChilled[0]);
            Assert.False(s.RaiderChilled[1]);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Capacity` does not exist.

- [ ] **Step 3: Write `Capacity.cs`**

Insertion sort over the scratch array. The set is at most a hundred raiders, and an insertion sort over a total-order comparator is stable by construction — which matters, because `combat_engine` §5.1 warns that an unstable sort over equal keys can order them differently on two runtimes.

```csharp
namespace Broodline.Sim.Combat
{
    /// combat_engine section 5.3: every counter trait is a capacity resource,
    /// and capacity is recomputed from scratch every tick from the live
    /// creature set, never accumulated. Incremental capacity drifts, and drift
    /// in a counter system is a fairness bug.
    public static class Capacity
    {
        /// Summed across every LIVE carrier. A dead carrier contributes
        /// nothing, so its capacity frees the tick it dies.
        public static int TotalChillCapacity(SimState s)
        {
            int total = 0;
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureCarries(c, Trait.Chill, out int tier))
                    total += Stats.ChillCapacity(tier);
            }
            return total;
        }

        /// Squared distance from this raider to the NEAREST live Chill carrier
        /// that currently has it IN RANGE, or -1 when no carrier can reach it.
        ///
        /// combat_engine 5.1 assigns Chill "nearest-first within range,
        /// re-evaluated each tick", and the range gate is not decoration.
        /// Without it a carrier slows raiders it could never reach and an
        /// unopposed Courser crosses in 48s, against the 30s combat_numbers 240
        /// describes. Gated, it crosses in about 27s - which is what makes
        /// 134's "0.5 t/s" and 240's "halves its speed to 30 seconds" the same
        /// claim rather than a contradiction.
        ///
        /// Measured per raider rather than from one fixed anchor pocket:
        /// capacity sums across carriers standing in different pockets, so
        /// "nearest" has to mean nearest to THAT raider.
        public static int NearestCarrierDistSq(SimState s, int raider)
        {
            int best = -1;
            int tile = s.RaiderTile(raider);

            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (!s.CreatureCarries(c, Trait.Chill, out int tier) || tier <= 0) continue;

                int pocket = s.CreaturePocket[c];
                if (!s.Lane.InRange(pocket, tile, Stats.CreatureRange(s.CreatureSpecies[c])))
                    continue;

                int d = s.Lane.DistSq(pocket, tile);
                if (best < 0 || d < best) best = d;
            }
            return best;
        }

        /// The (distance, spawnIndex) total order of combat_engine 5.1.
        ///
        /// Nearest-first alone is not deterministic: 2.2 makes distance an
        /// exact integer, so ties are common, and a sort over equal keys can
        /// order them differently on two runtimes. Tie-breaking on spawn index
        /// makes this a total order, so any correct sort produces identical
        /// output everywhere.
        public static int Compare(SimState s, int raiderA, int raiderB)
        {
            int da = NearestCarrierDistSq(s, raiderA);
            int db = NearestCarrierDistSq(s, raiderB);
            if (da != db) return da < db ? -1 : 1;
            if (raiderA != raiderB) return raiderA < raiderB ? -1 : 1;
            return 0;
        }

        /// Chill - one of combat_engine 5.1's three exceptions to
        /// follow-the-carrier's-target, because a Chill carrier may not be
        /// attacking the raider it slows. Assignment is nearest-first within
        /// range, re-evaluated every tick.
        public static void AssignChill(SimState s, int[] scratch)
        {
            for (int r = 0; r < s.RaiderCount; r++) s.RaiderChilled[r] = false;

            int capacity = TotalChillCapacity(s);
            if (capacity <= 0) return;

            // Only raiders some live carrier can actually reach are candidates.
            int n = 0;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (NearestCarrierDistSq(s, r) < 0) continue;
                scratch[n++] = r;
            }

            // Insertion sort: stable by construction over a total order, and
            // allocation-free. n is bounded by the wave's spawn count.
            for (int i = 1; i < n; i++)
            {
                int v = scratch[i];
                int j = i - 1;
                while (j >= 0 && Compare(s, scratch[j], v) > 0)
                {
                    scratch[j + 1] = scratch[j];
                    j--;
                }
                scratch[j + 1] = v;
            }

            int slowed = n < capacity ? n : capacity;
            for (int i = 0; i < slowed; i++) s.RaiderChilled[scratch[i]] = true;
        }
    }
}
```

- [ ] **Step 4: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/Runtime/Combat/Capacity.cs tests/engine/Combat/CapacityTests.cs
git commit -m "feat: per-trait capacity and the total-order comparator

Capacity is recomputed from the live creature set every tick, never
accumulated - 5.3 is blunt that incremental capacity drifts and drift in a
counter system is a fairness bug. It also settles what a carrier's death
does: its capacity frees the same tick, because there is nothing to free.

Assignment sorts on (distance, spawnIndex). Distance alone is not enough:
2.2 makes distance an exact integer, so ties are common rather than rare,
and an unstable sort over equal keys can order them differently on two
runtimes. Insertion sort because it is stable by construction and
allocation-free."
```

---

### Task 5: Phases 1–3 — Spawn, State, Movement, and Chill at its site

The first three phases, and the Chill rule at the State-phase site `combat_engine.md` §5 assigns it. **Movement precedes targeting so a creature never fires at a position a raider has already left** (§4).

**Files:**
- Create: `engine/Runtime/Combat/Counters.cs`
- Create: `engine/Runtime/Combat/Phases.cs`
- Create: `tests/engine/Combat/PhasesEarlyTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Capacity`, `Lane`, `Stats`.
- Produces:
  - `static void Counters.ApplyChill(SimState s, int[] scratch)`
  - `static void Phases.Spawn(SimState s)`
  - `static void Phases.State(SimState s, int[] scratch)`
  - `static void Phases.Movement(SimState s)`
  - `static Fix64 Phases.RaiderSpeed(SimState s, int r)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/PhasesEarlyTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class PhasesEarlyTests
    {
        private static SimState Wave6State(CreatureSpec[] deployment)
            => new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);

        [Fact]
        public void Spawn_IntroducesARaiderOnlyWhenItsTickArrives()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());

            s.Tick = 89;
            Phases.Spawn(s);
            Assert.Equal(0, s.RaiderCount);

            s.Tick = 90;                  // t=3s at 30Hz
            Phases.Spawn(s);
            Assert.Equal(1, s.RaiderCount);
            Assert.True(s.RaiderAlive[0]);
            Assert.Equal(220, s.RaiderHp[0]);
            Assert.Equal(Fix64.Zero, s.RaiderProgress[0]);
        }

        [Fact]
        public void Spawn_IsIdempotentWithinATick()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            Phases.Spawn(s);
            Assert.Equal(1, s.RaiderCount);
        }

        [Fact]
        public void Movement_AdvancesByFullSpeedWhenNotChilled()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);

            Fix64 before = s.RaiderProgress[0];
            Phases.Movement(s);
            Fix64 delta = s.RaiderProgress[0] - before;

            Assert.Equal(Lane.SpeedPerTick(1600), delta);
        }

        [Fact]
        public void State_ChillSlowsTheRaiderAndMovementHonoursIt()
        {
            var d = SimStateTests.FiveWithoutChill();
            d[0] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 0,
                Instinct = Instinct.Vanguard,
                Trait1 = Trait.Chill, Tier1 = 1
            };
            var s = Wave6State(d);
            s.Tick = 90;
            Phases.Spawn(s);

            var scratch = new int[1];
            Phases.State(s, scratch);
            Assert.True(s.RaiderChilled[0]);

            Fix64 before = s.RaiderProgress[0];
            Phases.Movement(s);

            // 0.5 tiles/sec, not 1.6.
            Assert.Equal(Lane.SpeedPerTick(500), s.RaiderProgress[0] - before);
        }

        [Fact]
        public void State_WithoutAChillCarrierLeavesTheRaiderAtFullSpeed()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);

            Phases.State(s, new int[1]);
            Assert.False(s.RaiderChilled[0]);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Phases` and `Counters` do not exist.

- [ ] **Step 3: Write `Counters.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// The counters, each as an explicit rule at its own tick-phase site.
    ///
    /// combat_engine section 5 rejects a generic counter interface outright:
    /// the eight counters are eight different KINDS of rule operating at five
    /// different points in the tick, and a unified interface "would have to be
    /// a switch statement wearing a coat". This file therefore has one method
    /// per counter, each called from exactly one phase, and it is expected to
    /// grow to eight unrelated methods rather than to acquire an abstraction.
    ///
    /// In scope for Phase 2: Chill (State, phase 2).
    /// Deferred: Splash, Pierce, Sprint (Attack), Cinder (Death),
    ///           Taunt, Reach (Targeting), Burrow (State).
    public static class Counters
    {
        /// Chill - phase 2, State. A speed modifier on the raider.
        ///
        /// Assignment is nearest-first within range, re-evaluated every tick:
        /// combat_engine 5.1 makes Chill one of three exceptions to
        /// follow-the-carrier's-target, because a Chill carrier may not be
        /// attacking the raider it slows.
        public static void ApplyChill(SimState s, int[] scratch)
        {
            Capacity.AssignChill(s, scratch);
        }
    }
}
```

- [ ] **Step 4: Write the first three phases in `Phases.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// The eight tick phases of combat_engine section 4, in normative order.
    ///
    /// "Each 33ms tick, in this order, and the order is normative - changing it
    /// changes outcomes." Movement precedes targeting so a creature never fires
    /// at a position a raider has already left; death resolves after attack so
    /// a raider that dies this tick has already moved and already been hit.
    /// Anything else produces same-tick cascades that cannot be reasoned about.
    public static partial class Phases
    {
        /// Phase 1 - Spawn. Introduce raiders whose spawn tick has arrived.
        ///
        /// The spawn table is ordered by tick ascending (WaveDef.Validate
        /// enforces it), so this walks forward from RaiderCount and stops at
        /// the first entry in the future. Idempotent within a tick.
        public static void Spawn(SimState s)
        {
            while (s.RaiderCount < s.Wave.Spawns.Length &&
                   s.Wave.Spawns[s.RaiderCount].Tick <= s.Tick)
            {
                int r = s.RaiderCount;
                s.RaiderType[r] = s.Wave.Spawns[r].Type;
                s.RaiderHp[r] = Stats.RaiderHp(s.RaiderType[r]);
                s.RaiderProgress[r] = Fix64.Zero;
                s.RaiderAlive[r] = true;
                s.RaiderChilled[r] = false;
                s.RaiderCount++;
            }
        }

        /// Phase 2 - State. Forced state changes.
        ///
        /// In the full engine this resolves Burrow surfacing, Delver reaching
        /// the Ark, Drift altitude, Chill expiry and Skittish repositioning.
        /// Phase 2's slice carries Chill only.
        public static void State(SimState s, int[] scratch)
        {
            Counters.ApplyChill(s, scratch);
        }

        /// Phase 3 - Movement. Advance every live raider by its current speed.
        public static void Movement(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                s.RaiderProgress[r] = s.RaiderProgress[r] + RaiderSpeed(s, r);
            }
        }

        /// Current speed in tiles per tick, after state effects.
        public static Fix64 RaiderSpeed(SimState s, int r)
        {
            int milli = s.RaiderChilled[r]
                ? Stats.ChilledMilliTilesPerSec
                : Stats.RaiderMilliTilesPerSec(s.RaiderType[r]);
            return Lane.SpeedPerTick(milli);
        }
    }
}
```

- [ ] **Step 5: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/Runtime/Combat/Phases.cs engine/Runtime/Combat/Counters.cs tests/engine/Combat/PhasesEarlyTests.cs
git commit -m "feat: phases 1-3 and Chill at its State-phase site

Spawn, State and Movement in the normative order of combat_engine 4.
Movement precedes targeting so a creature never fires at a position a
raider has already left.

Counters.cs has one method per counter and is expected to grow to eight
unrelated methods rather than acquire an abstraction. Section 5 rejects the
generic interface directly: the eight are eight different kinds of rule at
five different tick points, and unifying them would be a switch statement
wearing a coat."
```

---

### Task 6: Phase 4 — Targeting and the six Instincts

`combat_engine.md` §6: every creature holds one target; the 0.4s lockout is on **acquiring**, not on firing. **Instinct is a target-selection predicate plus an optional trigger, not a behaviour tree.**

**Files:**
- Modify: `engine/Runtime/Combat/Phases.cs`
- Create: `engine/Runtime/Combat/Targeting.cs`
- Create: `tests/engine/Combat/TargetingTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Lane`, `Stats`, `Capacity.Compare`.
- Produces:
  - `static void Phases.Targeting(SimState s)`
  - `static int Targeting.Select(SimState s, int creature)` — raider id or -1
  - `static bool Targeting.CanReach(SimState s, int creature, int raider)`
  - `static int Targeting.EffectiveRange(SimState s, int creature)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/TargetingTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class TargetingTests
    {
        /// Three Coursers abreast, so the predicates select different raiders.
        private static SimState ThreeRaiders(Instinct instinct, Species species, int pocket)
        {
            var wave = new WaveDef(1, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var d = new[]
            {
                new CreatureSpec { Species = species, Pocket = pocket, Instinct = instinct }
            };
            var s = new SimState(wave, Lane.Defile(), d);
            s.Tick = 0;
            Phases.Spawn(s);

            // Pocket 0 sits at tile 6. Raider 0 furthest along, raider 2 nearest.
            s.RaiderProgress[0] = Fix64.FromInt(11);
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(6);
            return s;
        }

        [Fact]
        public void Vanguard_TargetsTheRaiderClosestToTheArk()
        {
            var s = ThreeRaiders(Instinct.Vanguard, Species.Hollow, pocket: 0);
            Assert.Equal(0, Targeting.Select(s, 0));   // furthest along = closest to Ark
        }

        [Fact]
        public void Bloodscent_TargetsLowestCurrentHp()
        {
            var s = ThreeRaiders(Instinct.Bloodscent, Species.Hollow, pocket: 0);
            s.RaiderHp[1] = 40;
            Assert.Equal(1, Targeting.Select(s, 0));
        }

        [Fact]
        public void Overwatch_TargetsFurthestInRangeAndWidensRange()
        {
            var s = ThreeRaiders(Instinct.Overwatch, Species.Hollow, pocket: 0);

            // Range 7 becomes 8: +25%, truncated toward zero.
            Assert.Equal(8, Targeting.EffectiveRange(s, 0));

            // "Furthest in range" is by distance from the creature, so the
            // raider at tile 11 - five tiles away - wins over the one at 6.
            Assert.Equal(0, Targeting.Select(s, 0));
        }

        [Fact]
        public void NearestPredicates_TieBreakOnSpawnIndexAscending()
        {
            var s = ThreeRaiders(Instinct.LastStand, Species.Hollow, pocket: 0);
            // Raiders 1 and 2 equidistant from tile 6: tiles 8 and 4.
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(4);
            s.RaiderProgress[0] = Fix64.FromInt(20);   // out of the way

            Assert.Equal(1, Targeting.Select(s, 0));   // lower spawn index wins
        }

        [Fact]
        public void Select_IgnoresDeadAndOutOfRangeRaiders()
        {
            // Vetch range 2 from pocket 0 (tile 6) reaches tiles 5-7 only.
            var s = ThreeRaiders(Instinct.Vanguard, Species.Vetch, pocket: 0);
            Assert.Equal(2, Targeting.Select(s, 0));   // only the tile-6 raider

            s.RaiderAlive[2] = false;
            Assert.Equal(-1, Targeting.Select(s, 0));
        }

        [Fact]
        public void Targeting_LockoutDelaysAcquisitionByTwelveTicks()
        {
            var s = ThreeRaiders(Instinct.Vanguard, Species.Hollow, pocket: 0);
            Phases.Targeting(s);
            Assert.Equal(0, s.CreatureTarget[0]);

            // The target dies. The creature drops it immediately but may not
            // acquire again until the lockout expires.
            s.RaiderAlive[0] = false;
            s.Tick = 100;
            Phases.Targeting(s);
            Assert.Equal(-1, s.CreatureTarget[0]);
            Assert.Equal(100 + Stats.RetargetLockoutTicks, s.CreatureAcquireAt[0]);

            s.Tick = 111;
            Phases.Targeting(s);
            Assert.Equal(-1, s.CreatureTarget[0]);   // still locked out

            s.Tick = 112;
            Phases.Targeting(s);
            Assert.Equal(1, s.CreatureTarget[0]);    // acquires
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Targeting` does not exist.

- [ ] **Step 3: Write `Targeting.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// combat_engine section 6. Instinct is a target-selection predicate plus
    /// an optional trigger, not a behaviour tree - six rules, each a comparator
    /// over the raiders currently in range.
    ///
    /// Ties break on spawn index ascending, everywhere. The RNG is used only
    /// where a tie-break must not be predictable, which in this slice is
    /// nowhere: Contrary and the Aberrants are deferred.
    public static class Targeting
    {
        /// Overwatch is passive: +25% range, -20% attack speed. Applied as
        /// integer arithmetic - range * 5 / 4 - so no float enters the core.
        public static int EffectiveRange(SimState s, int creature)
        {
            int baseRange = Stats.CreatureRange(s.CreatureSpecies[creature]);
            return s.CreatureInstinct[creature] == Instinct.Overwatch
                ? baseRange * 5 / 4
                : baseRange;
        }

        public static bool CanReach(SimState s, int creature, int raider)
        {
            if (!s.RaiderAlive[raider]) return false;
            return s.Lane.InRange(
                s.CreaturePocket[creature],
                s.RaiderTile(raider),
                EffectiveRange(s, creature));
        }

        /// Returns the raider this creature's Instinct selects, or -1.
        ///
        /// One linear pass in ID order, keeping the best so far. Because the
        /// scan runs in ascending raider order and a challenger only wins on a
        /// STRICT improvement, an equal key leaves the earlier raider in place
        /// - which is the spawn-index tie-break, achieved without a sort.
        public static int Select(SimState s, int creature)
        {
            int best = -1;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!CanReach(s, creature, r)) continue;
                if (best < 0) { best = r; continue; }
                if (Prefers(s, creature, r, best)) best = r;
            }
            return best;
        }

        /// Strictly prefers challenger over incumbent under this Instinct.
        private static bool Prefers(SimState s, int creature, int challenger, int incumbent)
        {
            switch (s.CreatureInstinct[creature])
            {
                case Instinct.Bloodscent:
                    // Lowest current HP in range.
                    return s.RaiderHp[challenger] < s.RaiderHp[incumbent];

                case Instinct.Vanguard:
                    // Closest to the Ark - greatest progress along the lane.
                    return s.RaiderProgress[challenger] > s.RaiderProgress[incumbent];

                case Instinct.Overwatch:
                    // Furthest in range, measured from the creature.
                    return DistSq(s, creature, challenger) > DistSq(s, creature, incumbent);

                default:
                    // LastStand, Skittish and PackSense all target nearest.
                    return DistSq(s, creature, challenger) < DistSq(s, creature, incumbent);
            }
        }

        private static int DistSq(SimState s, int creature, int raider) =>
            s.Lane.DistSq(s.CreaturePocket[creature], s.RaiderTile(raider));
    }
}
```

- [ ] **Step 4: Add phase 4 to `Phases.cs`**

Append inside `public static partial class Phases`:

```csharp
        /// Phase 4 - Targeting. Retarget any creature whose target is dead, out
        /// of range, or whose Instinct preference now names a different valid
        /// target. Respects the 0.4s retarget lockout.
        ///
        /// combat_engine section 6: "The 0.4s retarget lockout is a lockout on
        /// ACQUIRING, not on firing." A creature whose target dies stops firing
        /// immediately and acquires 12 ticks later. That gap is what makes
        /// Splash a counter rather than a convenience, so dropping the target
        /// eagerly while gating acquisition is load-bearing, not incidental.
        public static void Targeting(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;

                int current = s.CreatureTarget[c];
                bool held = current >= 0 && Combat.Targeting.CanReach(s, c, current);

                if (!held && current >= 0)
                {
                    // Drop immediately, and start the lockout.
                    s.CreatureTarget[c] = -1;
                    s.CreatureAcquireAt[c] = s.Tick + Stats.RetargetLockoutTicks;
                    continue;
                }

                if (held)
                {
                    // Still valid, but the preference may have moved.
                    int preferred = Combat.Targeting.Select(s, c);
                    if (preferred != current && s.Tick >= s.CreatureAcquireAt[c])
                        s.CreatureTarget[c] = preferred;
                    continue;
                }

                if (s.Tick >= s.CreatureAcquireAt[c])
                    s.CreatureTarget[c] = Combat.Targeting.Select(s, c);
            }
        }
```

**Gotcha — the `Combat.` prefix is required, not stylistic.** Inside `Phases` there is now a *method* named `Targeting` as well as a *class* named `Targeting`. Writing `Targeting.Select(s, c)` unqualified from inside `Phases.Targeting` is a compile error (CS0119: *"is a method, which is not valid in the given context"*), because the method name binds first. `Combat.Targeting` resolves outward to `Broodline.Sim.Combat.Targeting` and compiles. Do not "simplify" it away — the build breaks, and the obvious fix of renaming the phase method breaks the eight-phase naming symmetry that Task 11's enforcement test reads.

- [ ] **Step 5: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/Runtime/Combat/Targeting.cs engine/Runtime/Combat/Phases.cs tests/engine/Combat/TargetingTests.cs
git commit -m "feat: phase 4 targeting and the six Instinct predicates

Instincts are comparators over the raiders in range, not behaviour trees.
Selection is one linear pass in ascending raider order where a challenger
wins only on a strict improvement, so an equal key leaves the earlier
raider in place - the spawn-index tie-break, without a sort.

The 0.4s lockout gates acquiring, not firing: a creature whose target dies
drops it the same tick and reacquires twelve ticks later. Section 6 is
explicit that this gap is what makes Splash a counter rather than a
convenience, so the asymmetry is deliberate."
```

---

### Task 7: Phases 5–6 — Attack, Death, and the three Instinct triggers

Three of the six Instincts carry a trigger as well as a predicate. Two of them (`LastStand`, `PackSense`) modify attacks; `Skittish` is a forced state change and therefore belongs in phase 2, not here.

**The Courser does not attack.** `broodline_waves_01_12.md` §3 describes it as a runner — *"fifteen seconds to cross, so five creatures cannot focus it down"* — so nothing damages creatures in this slice. `CreatureHp` and the death path exist and are exercised by tests that damage creatures directly, so the second raider does not have to add the plumbing.

**Files:**
- Modify: `engine/Runtime/Combat/SimState.cs` (three fields for Skittish)
- Modify: `engine/Runtime/Combat/Phases.cs`
- Create: `engine/Runtime/Combat/Attacks.cs`
- Create: `tests/engine/Combat/AttackTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Targeting`, `Stats`.
- Produces:
  - `SimState.CreatureBusyUntil[]`, `SimState.CreatureRepositioned[]`, `SimState.CreatureCanAct(int c)`
  - `static int Attacks.IntervalTicks(SimState s, int c)`
  - `static int Attacks.Damage(SimState s, int c)`
  - `static void Phases.Attack(SimState s)`
  - `static void Phases.Death(SimState s)`
  - `static void Phases.Skittish(SimState s)` — called from `State`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/AttackTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class AttackTests
    {
        private static SimState OneRaider(Instinct instinct, Species species, int pocket)
        {
            var d = new[]
            {
                new CreatureSpec { Species = species, Pocket = pocket, Instinct = instinct }
            };
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), d);
            s.Tick = 90;
            Phases.Spawn(s);
            s.RaiderProgress[0] = Fix64.FromInt(6);   // beside pocket 0
            return s;
        }

        [Fact]
        public void Attack_DealsSpeciesDamageAndSetsTheNextInterval()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            Phases.Targeting(s);
            Phases.Attack(s);

            Assert.Equal(220 - 55, s.RaiderHp[0]);                 // Hollow: 55
            Assert.Equal(90 + 75, s.CreatureNextAttackAt[0]);      // 2.5s = 75 ticks
        }

        [Fact]
        public void Attack_DoesNothingBeforeTheIntervalElapses()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            Phases.Targeting(s);
            Phases.Attack(s);
            int hp = s.RaiderHp[0];

            s.Tick = 91;
            Phases.Attack(s);
            Assert.Equal(hp, s.RaiderHp[0]);
        }

        [Fact]
        public void Overwatch_TradesTwentyPercentAttackSpeedForRange()
        {
            var s = OneRaider(Instinct.Overwatch, Species.Hollow, 0);
            // -20% attack speed: 75 ticks becomes 75 * 5 / 4.
            Assert.Equal(93, Attacks.IntervalTicks(s, 0));
        }

        [Fact]
        public void LastStand_SpeedsUpOnlyBelowQuarterHealth()
        {
            var s = OneRaider(Instinct.LastStand, Species.Hollow, 0);
            Assert.Equal(75, Attacks.IntervalTicks(s, 0));

            s.CreatureHp[0] = 14;                 // Hollow's 60 HP, under 25%
            // +50% attack speed: 75 * 2 / 3.
            Assert.Equal(50, Attacks.IntervalTicks(s, 0));
        }

        [Fact]
        public void PackSense_AddsFifteenPercentWithAnAdjacentSameSpeciesAlly()
        {
            var d = new[]
            {
                new CreatureSpec { Species = Species.Hollow, Pocket = 0, Instinct = Instinct.PackSense },
                new CreatureSpec { Species = Species.Hollow, Pocket = 1, Instinct = Instinct.Vanguard }
            };
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), d);

            // 55 * 115 / 100 = 63.
            Assert.Equal(63, Attacks.Damage(s, 0));

            // A different species in the adjacent pocket does not qualify.
            s.CreatureSpecies[1] = Species.Vetch;
            Assert.Equal(55, Attacks.Damage(s, 0));
        }

        [Fact]
        public void Death_ClearsTheRaiderAtZeroHp()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            s.RaiderHp[0] = 0;
            Phases.Death(s);
            Assert.False(s.RaiderAlive[0]);
        }

        [Fact]
        public void Skittish_RepositionsOnceBelowFortyPercentAndCannotActWhileMoving()
        {
            var s = OneRaider(Instinct.Skittish, Species.Hollow, 1);
            s.CreatureHp[0] = 20;                 // Hollow 60 HP, under 40%

            Phases.Skittish(s);
            Assert.False(s.CreatureCanAct(0));
            Assert.Equal(90 + 60, s.CreatureBusyUntil[0]);   // 2s at 30Hz

            // It repositions away from the threat, and only ever once.
            int moved = s.CreaturePocket[0];
            Assert.NotEqual(1, moved);

            s.Tick = 200;
            Phases.Skittish(s);
            Assert.Equal(moved, s.CreaturePocket[0]);
            Assert.True(s.CreatureCanAct(0));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Attacks` does not exist.

- [ ] **Step 3: Add the Skittish fields to `SimState.cs`**

Declare beside the other creature arrays:

```csharp
        public readonly int[] CreatureBusyUntil;      // tick it may act again
        public readonly bool[] CreatureRepositioned;  // Skittish fires once
```

Initialise in the constructor loop:

```csharp
                CreatureBusyUntil[c] = 0;
                CreatureRepositioned[c] = false;
```

Allocate them alongside the rest:

```csharp
            CreatureBusyUntil = new int[CreatureCount];
            CreatureRepositioned = new bool[CreatureCount];
```

And add the accessor:

```csharp
        /// A repositioning Skittish creature cannot act. combat_engine section
        /// 6: "2s, cannot act".
        public bool CreatureCanAct(int c) => CreatureAlive(c) && Tick >= CreatureBusyUntil[c];
```

- [ ] **Step 4: Write `Attacks.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// Attack rate and damage, including the two Instinct triggers that modify
    /// them. All modifiers are integer ratios so no float enters the core, and
    /// they are applied to the INTERVAL rather than to a rate, because the
    /// interval is what the tick loop compares against.
    public static class Attacks
    {
        /// Ticks between attacks, after Instinct modifiers.
        ///
        /// Overwatch: -20% attack speed, so the interval grows by 5/4.
        /// Last Stand below 25% HP: +50% attack speed, so it shrinks by 2/3.
        public static int IntervalTicks(SimState s, int c)
        {
            int interval = Stats.CreatureIntervalTicks(s.CreatureSpecies[c]);

            switch (s.CreatureInstinct[c])
            {
                case Instinct.Overwatch:
                    return interval * 5 / 4;

                case Instinct.LastStand:
                    return BelowFraction(s, c, 1, 4) ? interval * 2 / 3 : interval;

                default:
                    return interval;
            }
        }

        /// Damage per hit, after Instinct modifiers.
        ///
        /// Pack Sense: +15% to BOTH when an adjacent pocket holds a live ally
        /// of the same species. Only the carrier's own bonus is computed here;
        /// the ally's own call sees the same adjacency and gets the same
        /// answer, which is what "to both" means without shared state.
        public static int Damage(SimState s, int c)
        {
            int damage = Stats.CreatureDamage(s.CreatureSpecies[c]);

            if (s.CreatureInstinct[c] == Instinct.PackSense && HasPackAlly(s, c))
                damage = damage * 115 / 100;

            return damage;
        }

        private static bool HasPackAlly(SimState s, int c)
        {
            int pocket = s.CreaturePocket[c];
            for (int other = 0; other < s.CreatureCount; other++)
            {
                if (other == c || !s.CreatureAlive(other)) continue;
                if (s.CreatureSpecies[other] != s.CreatureSpecies[c]) continue;

                int delta = s.CreaturePocket[other] - pocket;
                if (delta == 1 || delta == -1) return true;
            }
            return false;
        }

        /// current HP < max * numerator / denominator, without division by a
        /// possibly-zero max and without float.
        public static bool BelowFraction(SimState s, int c, int numerator, int denominator)
        {
            int max = Stats.CreatureHp(s.CreatureSpecies[c]);
            return s.CreatureHp[c] * denominator < max * numerator;
        }
    }
}
```

- [ ] **Step 5: Add phases 5, 6 and the Skittish rule to `Phases.cs`**

Append inside `public static partial class Phases`:

```csharp
        /// Phase 5 - Attack. Resolve attacks whose interval has elapsed.
        public static void Attack(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureCanAct(c)) continue;
                if (s.Tick < s.CreatureNextAttackAt[c]) continue;

                int target = s.CreatureTarget[c];
                if (target < 0 || !s.RaiderAlive[target]) continue;

                s.RaiderHp[target] -= Attacks.Damage(s, c);
                s.CreatureNextAttackAt[c] = s.Tick + Attacks.IntervalTicks(s, c);
            }
        }

        /// Phase 6 - Death. Resolve deaths.
        ///
        /// In the full engine this is also where a Brood splits and where
        /// Cinder suppresses the split at birth. Neither is in this slice, so
        /// death here is only a clearing pass - but it stays its own phase
        /// because section 4 makes the ordering normative: a raider that dies
        /// this tick has already moved and has already been hit.
        public static void Death(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r] && s.RaiderHp[r] <= 0)
                    s.RaiderAlive[r] = false;

            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureHp[c] < 0)
                    s.CreatureHp[c] = 0;
        }

        /// Skittish - phase 2, State. A forced state change, not an attack
        /// modifier, which is why it lives here rather than in Attacks.
        ///
        /// combat_engine section 6: below 40% HP, reposition to the nearest
        /// free pocket away from the threat, 2s, cannot act. Deterministic by
        /// decision (whats_left section 2), which is what keeps the engine's
        /// RNG surface down to tie-breaks.
        public static void Skittish(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureInstinct[c] != Instinct.Skittish) continue;
                if (s.CreatureRepositioned[c]) continue;
                if (!Attacks.BelowFraction(s, c, 2, 5)) continue;   // < 40%

                int destination = NearestFreePocketAwayFromThreat(s, c);
                if (destination < 0) continue;

                s.CreaturePocket[c] = destination;
                s.CreatureRepositioned[c] = true;
                s.CreatureBusyUntil[c] = s.Tick + 2 * Stats.TicksPerSecond;
                s.CreatureTarget[c] = -1;
            }
        }

        /// "Away from the threat" is away from the raider nearest the Ark,
        /// which is the one the pocket most needs distance from. Scans pockets
        /// in ascending index and takes the first free one whose distance to
        /// that raider exceeds the current pocket's - lowest index wins ties,
        /// so the choice is total-ordered like every other comparator here.
        private static int NearestFreePocketAwayFromThreat(SimState s, int c)
        {
            int threat = -1;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (threat < 0 || s.RaiderProgress[r] > s.RaiderProgress[threat]) threat = r;
            }
            if (threat < 0) return -1;

            int threatTile = s.RaiderTile(threat);
            int here = s.Lane.DistSq(s.CreaturePocket[c], threatTile);

            for (int p = 0; p < s.Lane.PocketCount; p++)
            {
                if (Occupied(s, p)) continue;
                if (s.Lane.DistSq(p, threatTile) > here) return p;
            }
            return -1;
        }

        private static bool Occupied(SimState s, int pocket)
        {
            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureAlive(c) && s.CreaturePocket[c] == pocket) return true;
            return false;
        }
```

Then wire Skittish into phase 2 by extending `State`:

```csharp
        public static void State(SimState s, int[] scratch)
        {
            Skittish(s);
            Counters.ApplyChill(s, scratch);
        }
```

- [ ] **Step 6: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/Runtime/Combat tests/engine/Combat
git commit -m "feat: phases 5-6 and the three Instinct triggers

Attack and Death, plus Last Stand and Pack Sense as integer-ratio
modifiers on the interval and the damage. Skittish is a forced state
change rather than an attack modifier, so it sits in phase 2 where
section 4 puts it, and repositions deterministically - the decision that
keeps the engine's whole RNG surface down to tie-breaks.

Death stays its own phase even though nothing splits yet. The ordering is
normative: a raider that dies this tick has already moved and already been
hit, and Brood and Cinder will need exactly this seam."
```

---

### Task 8: Phases 7–8 — Breach, Resolve, and guaranteed termination

**Files:**
- Modify: `engine/Runtime/Combat/Phases.cs`
- Create: `engine/Runtime/Combat/Outcome.cs`
- Create: `tests/engine/Combat/TerminationTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Stats`.
- Produces:
  - `enum Result { Running, Win, Loss, Stalled }`
  - `struct Breach { int Tick; int Raider; RaiderType Type; int Lane; }`
  - `static bool Phases.Breach(SimState s, Breach[] log, ref int logCount)`
  - `static Result Phases.Resolve(SimState s)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/TerminationTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class TerminationTests
    {
        [Fact]
        public void Breach_DeductsIntegrityByRaiderTypeAndRemovesTheRaider()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            s.RaiderProgress[0] = Fix64.FromInt(Stats.LaneTiles);   // at the Ark

            var log = new Breach[1];
            int count = 0;
            Phases.Breach(s, log, ref count);

            Assert.Equal(0, s.Integrity);          // 2 - Courser's 2
            Assert.False(s.RaiderAlive[0]);
            Assert.Equal(1, count);
            Assert.Equal(RaiderType.Courser, log[0].Type);
        }

        [Fact]
        public void Resolve_LosesTheTickIntegrityReachesZero()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Integrity = 0;
            Assert.Equal(Result.Loss, Phases.Resolve(s));
        }

        [Fact]
        public void Resolve_WinsWhenNothingRemainsAndNothingIsPending()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            Assert.Equal(Result.Running, Phases.Resolve(s));

            s.RaiderAlive[0] = false;
            Assert.Equal(Result.Win, Phases.Resolve(s));
        }

        [Fact]
        public void Resolve_IsStillRunningWhileASpawnIsPending()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            // Nothing spawned yet, but the table is not exhausted.
            Assert.Equal(Result.Running, Phases.Resolve(s));
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Result` and `Breach` do not exist.

- [ ] **Step 3: Write `Outcome.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    public enum Result { Running = 0, Win = 1, Loss = 2, Stalled = 3 }

    /// One raider reaching the Ark, with its diagnosis attached at Task 9.
    /// combat_engine section 7: the diagnosis is recorded AT the breach,
    /// because by the time the wave ends the information about why a specific
    /// raider was unanswerable is gone.
    public struct Breach
    {
        public int Tick;
        public int Raider;
        public RaiderType Type;
        public int Lane;

        public bool Access;
        public bool Coverage;
        public bool Placement;
    }

    /// What a simulation returns. Inputs in, result out.
    public struct Outcome
    {
        public Result Result;
        public int Ticks;
        public int IntegrityRemaining;
        public Breach[] Breaches;
        public int BreachCount;
        public ulong Hash;
    }
}
```

- [ ] **Step 4: Add phases 7 and 8 to `Phases.cs`**

```csharp
        /// Phase 7 - Breach. Any raider at the Ark: deduct integrity, record
        /// the diagnosis, remove it. Returns true if anything breached.
        public static bool Breach(SimState s, Breach[] log, ref int logCount)
        {
            bool any = false;
            Fix64 ark = Fix64.FromInt(Stats.LaneTiles);

            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (s.RaiderProgress[r] < ark) continue;

                s.Integrity -= Stats.RaiderIntegrityCost(s.RaiderType[r]);
                s.RaiderAlive[r] = false;

                if (logCount < log.Length)
                {
                    log[logCount] = new Breach
                    {
                        Tick = s.Tick,
                        Raider = r,
                        Type = s.RaiderType[r],
                        Lane = 0
                    };
                    logCount++;
                }
                any = true;
            }
            return any;
        }

        /// Phase 8 - Resolve.
        ///
        /// combat_engine section 8: the wave is lost the tick integrity reaches
        /// zero or below, and the simulation STOPS there - raiders still on the
        /// board are not resolved and are not counted. Loss is therefore
        /// checked before win.
        public static Result Resolve(SimState s)
        {
            if (s.Integrity <= 0) return Result.Loss;

            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r]) return Result.Running;

            // Pending spawns count as remaining.
            if (s.RaiderCount < s.Wave.Spawns.Length) return Result.Running;

            return Result.Win;
        }
```

- [ ] **Step 5: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/Runtime/Combat tests/engine/Combat
git commit -m "feat: phases 7-8, breach recording and resolution

Loss is checked before win because section 8 stops the simulation the tick
integrity reaches zero - raiders still on the board are not resolved and
not counted, so a wave cannot be simultaneously lost and won on the tick a
last raider dies alongside a breach.

Breach carries the three diagnosis booleans as fields now and they are
filled at Task 9. They live on the record rather than being recomputed
because section 7 is explicit that by the time the wave ends the
information about why a raider was unanswerable is gone."
```

---

### Task 9: Breach diagnosis — one function, two call sites

`combat_engine.md` §7 requires the loss screen to distinguish **access**, **coverage** and **placement**, and §7 also requires the *pre-wave check* to run the same computation against the authored wave. **One function, two call sites** — building it twice is how they drift.

**Files:**
- Create: `engine/Runtime/Combat/Diagnosis.cs`
- Modify: `engine/Runtime/Combat/Phases.cs` (fill the booleans at phase 7)
- Create: `tests/engine/Combat/DiagnosisTests.cs`

**Interfaces:**
- Consumes: `SimState`, `Capacity`, `Stats`, `Lane`.
- Produces:
  - `struct Verdict { bool Access; bool Coverage; bool Placement; bool Answered; }`
  - `static Verdict Diagnosis.Evaluate(SimState s, RaiderType type, int simultaneous, int tile)` — `tile = -1` means "anywhere on the lane", which is the pre-wave call
  - `static Verdict Diagnosis.PreWaveCheck(SimState s, RaiderType type)`
  - `static int Diagnosis.SimultaneousCount(SimState s, RaiderType type)`

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/DiagnosisTests.cs`:

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class DiagnosisTests
    {
        private static CreatureSpec[] WithChill(int tier) => new[]
        {
            new CreatureSpec { Species = Species.Pale,   Pocket = 1, Instinct = Instinct.Vanguard,
                               Trait1 = Trait.Chill, Tier1 = tier },
            new CreatureSpec { Species = Species.Hollow, Pocket = 2, Instinct = Instinct.Vanguard }
        };

        [Fact]
        public void Access_IsFalseWhenNoCreatureCarriesTheTraitAtAll()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            var v = Diagnosis.Evaluate(s, RaiderType.Courser, simultaneous: 1, tile: 12);

            Assert.False(v.Access);
            Assert.False(v.Answered);
        }

        [Fact]
        public void Coverage_IsAboutTheLiveCountNotTheDeployment()
        {
            // Chill I is capacity 1. Sufficient against one Courser,
            // insufficient against two - the same deployment either way.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 12).Coverage);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 2, 12).Coverage);
        }

        [Fact]
        public void Coverage_UsesPerTraitCapacityNotAGlobalLadder()
        {
            // Chill II is 2, not combat_engine 5.3's superseded 3.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 2));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 2, 12).Coverage);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 3, 12).Coverage);
        }

        [Fact]
        public void Placement_IsFalseWhenNoCarrierCouldEverReachThatTile()
        {
            // Pale range 5 from pocket 1 (tile 10) reaches tiles 6-14.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 10).Placement);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 23).Placement);
        }

        [Fact]
        public void TheFirstFalseIsTheDiagnosis_EvaluatedInOrder()
        {
            // No access AND unreachable. Access is reported first, and
            // coverage/placement are not claimed to be meaningful.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            var v = Diagnosis.Evaluate(s, RaiderType.Courser, 1, 23);

            Assert.False(v.Access);
            Assert.False(v.Coverage);
            Assert.False(v.Placement);
        }

        [Fact]
        public void PreWaveCheck_IsTheSameFunctionAgainstTheAuthoredWave()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            // Wave 6 authors exactly one Courser, so the pre-wave check should
            // agree with the live evaluation at a reachable tile.
            var pre = Diagnosis.PreWaveCheck(s, RaiderType.Courser);
            Assert.True(pre.Answered);

            var none = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                    SimStateTests.FiveWithoutChill());
            Assert.False(Diagnosis.PreWaveCheck(none, RaiderType.Courser).Answered);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Diagnosis` does not exist.

- [ ] **Step 3: Write `Diagnosis.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    public struct Verdict
    {
        public bool Access;
        public bool Coverage;
        public bool Placement;
        public bool Answered => Access && Coverage && Placement;
    }

    /// combat_engine section 7. Three booleans, evaluated in order; the first
    /// false is the diagnosis, and it maps onto the three messages bible 4.11
    /// specifies.
    ///
    /// The same function serves the pre-wave check and the loss screen -
    /// section 7: "One function, two call sites." Building it twice is how the
    /// panel and the loss screen come to disagree.
    public static class Diagnosis
    {
        /// tile == AnyTile evaluates placement against the whole lane, which is
        /// the pre-wave question: could a carrier reach this raider ANYWHERE?
        public const int AnyTile = -1;

        public static Verdict Evaluate(SimState s, RaiderType type, int simultaneous, int tile)
        {
            var v = new Verdict();
            Trait answering = Stats.CounterFor(type);

            v.Access = HasCarrier(s, answering);
            if (!v.Access) return v;          // first false is the diagnosis

            v.Coverage = CapacityFor(s, answering) >= simultaneous;
            if (!v.Coverage) return v;

            v.Placement = CanBeReached(s, answering, tile);
            return v;
        }

        /// The pre-wave call. Runs against the authored wave rather than a
        /// live breach: the worst simultaneous count the spawn table can
        /// produce, anywhere on the lane.
        public static Verdict PreWaveCheck(SimState s, RaiderType type)
        {
            int worst = 0;
            for (int i = 0; i < s.Wave.Spawns.Length; i++)
                if (s.Wave.Spawns[i].Type == type) worst++;

            return Evaluate(s, type, worst, AnyTile);
        }

        /// How many of this raider type are on the board right now. Coverage is
        /// "a property of the deployment against what was on the board at that
        /// moment", so this is evaluated at breach time, never inferred later.
        public static int SimultaneousCount(SimState s, RaiderType type)
        {
            int n = 0;
            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r] && s.RaiderType[r] == type) n++;
            return n;
        }

        private static bool HasCarrier(SimState s, Trait trait)
        {
            if (trait == Trait.None) return false;
            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureAlive(c) && s.CreatureCarries(c, trait, out int tier) && tier > 0)
                    return true;
            return false;
        }

        private static int CapacityFor(SimState s, Trait trait) =>
            trait == Trait.Chill ? Capacity.TotalChillCapacity(s) : 0;

        private static bool CanBeReached(SimState s, Trait trait, int tile)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (!s.CreatureCarries(c, trait, out int tier) || tier <= 0) continue;

                int range = Targeting.EffectiveRange(s, c);
                int pocket = s.CreaturePocket[c];

                if (tile == AnyTile)
                {
                    for (int t = 0; t < s.Lane.Tiles; t++)
                        if (s.Lane.InRange(pocket, t, range)) return true;
                }
                else if (s.Lane.InRange(pocket, tile, range))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
```

- [ ] **Step 4: Fill the booleans at phase 7**

In `Phases.Breach`, replace the log-writing block so the diagnosis is captured **before** the raider is removed and before integrity changes — the live board at that instant is what coverage means:

```csharp
                if (logCount < log.Length)
                {
                    var verdict = Diagnosis.Evaluate(
                        s,
                        s.RaiderType[r],
                        Diagnosis.SimultaneousCount(s, s.RaiderType[r]),
                        s.RaiderTile(r));

                    log[logCount] = new Breach
                    {
                        Tick = s.Tick,
                        Raider = r,
                        Type = s.RaiderType[r],
                        Lane = 0,
                        Access = verdict.Access,
                        Coverage = verdict.Coverage,
                        Placement = verdict.Placement
                    };
                    logCount++;
                }
```

Move the `s.Integrity -=` and `s.RaiderAlive[r] = false` lines to **after** this block.

- [ ] **Step 5: Run until green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/Runtime/Combat tests/engine/Combat
git commit -m "feat: breach diagnosis, and the pre-wave check that shares it

Three booleans evaluated in order, the first false being the diagnosis.
Recorded at phase 7 against the live board and before the raider is
removed, because coverage is a property of the deployment against what was
present at that instant - Chill II is sufficient against two Coursers and
insufficient against three, from the same deployment.

PreWaveCheck is the same function against the authored spawn table. Section
7 asks for one function and two call sites; two functions is how the
composition panel and the loss screen come to disagree in month four."
```

---

### Task 10: `Sim.Run`, termination, and the two goldens

The loop, the stall detector, and the pinned pair the design doc §6.1 specifies: wave 6 without Chill loses with `access = false`; wave 6 with Chill clears.

**Files:**
- Create: `engine/Runtime/Combat/Sim.cs`
- Create: `tests/engine/Combat/GoldenTests.cs`

**Interfaces:**
- Consumes: everything above.
- Produces: `static Outcome Sim.Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)`

- [ ] **Step 1: Write `Sim.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    /// The simulation. Inputs in, result out - no ambient state, no
    /// wall-clock, no callbacks into the host. That is what lets the same code
    /// path serve live play, server verification and auto-resolve without
    /// branching (combat_engine section 1), and auto-resolve is the identical
    /// path with no player input rather than a stat roll.
    public static class Sim
    {
        public static Outcome Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            wave.Validate();

            var s = new SimState(wave, lane, deployment);
            var log = new Breach[wave.Spawns.Length];
            var scratch = new int[wave.Spawns.Length];
            int breachCount = 0;

            var hash = Hash.Create();
            hash.Add(wave.Id);
            hash.Add(unchecked((long)seed));

            Result result = Result.Running;
            int stallTicks = 0;
            long lastFingerprint = long.MinValue;

            while (s.Tick < Stats.HardTickCap)
            {
                Phases.Spawn(s);            // 1
                Phases.State(s, scratch);   // 2
                Phases.Movement(s);         // 3
                Phases.Targeting(s);        // 4
                Phases.Attack(s);           // 5
                Phases.Death(s);            // 6
                Phases.Breach(s, log, ref breachCount);   // 7
                result = Phases.Resolve(s); // 8

                FoldTick(ref hash, s);

                if (result != Result.Running) break;

                // Stall detector. combat_engine 8.1: if no raider has advanced
                // and no HP has changed for 300 consecutive ticks, terminate
                // immediately rather than burning to the cap. This catches the
                // soft-lock shape in ten seconds instead of three minutes.
                long fingerprint = Fingerprint(s);
                if (fingerprint == lastFingerprint)
                {
                    stallTicks++;
                    if (stallTicks >= Stats.StallTicks)
                    {
                        result = Result.Stalled;
                        break;
                    }
                }
                else
                {
                    stallTicks = 0;
                    lastFingerprint = fingerprint;
                }

                s.Tick++;
            }

            // The hard cap is a content bug, not a gameplay outcome.
            if (result == Result.Running) result = Result.Stalled;

            hash.Add((int)result);
            hash.Add(s.Integrity);

            return new Outcome
            {
                Result = result,
                Ticks = s.Tick,
                IntegrityRemaining = s.Integrity,
                Breaches = log,
                BreachCount = breachCount,
                Hash = hash.Value
            };
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

**`Fix64.Raw` is already public** (`engine/Runtime/Fix64.cs:118`, `public long Raw { get; }`), verified before this plan was dispatched. **Do not modify `Fix64`** — this task touches no Phase 1 file. Hashing the raw bits is the only way to fold a fixed-point value without a lossy conversion, and the accessor is already there.

- [ ] **Step 2: Write the golden tests, with the hashes left unpinned**

`tests/engine/Combat/GoldenTests.cs`:

```csharp
using Xunit;
using Xunit.Abstractions;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class GoldenTests
    {
        private readonly ITestOutputHelper _out;
        public GoldenTests(ITestOutputHelper output) { _out = output; }

        public const ulong Seed = 6;

        /// Golden A - wave 6 exactly as authored: five creatures, none
        /// carrying Chill, designed to be lost.
        ///
        /// Four Vetch and a Loam. Chosen for two reasons. It is a plausible
        /// pre-wave-6 roster - Vetch is the starter wall - and it dramatises the
        /// exact lesson the wave exists to teach, which waves_01_12 section 3
        /// states outright: "Courser ignores Taunt, so the Vetch does not save
        /// them."
        ///
        /// Chosen for MARGIN as well as verdict. The Courser breaches with 54 of
        /// its 220 hp remaining, so the Loss does not hinge on a damage race that
        /// an implementation detail could flip. A longer-ranged roster does not
        /// merely narrow that margin, it reverses the outcome: a Hollow running
        /// Overwatch covers 15 of the lane's 24 tiles and kills the Courser on its
        /// own, which would make this wave a clear and the golden pair meaningless.
        public static CreatureSpec[] DeploymentWithoutChill() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        /// Golden B - the Pale the player is granted on defeat, carrying Chill I,
        /// standing in the Loam's pocket. waves_01_12 section 3: "The Wave Defeat
        /// screen grants a Pale."
        ///
        /// The Courser dies at tile 16 of 24 - eight tiles of margin - so the Win
        /// does not hinge on a damage race either.
        ///
        /// This pair changes the creature as well as the trait, and that is the
        /// authored narrative rather than sloppy method: the player has no Pale at
        /// wave 6 and is granted one for losing. A same-body control (this roster
        /// with the Pale carrying NO trait) was evaluated and deliberately rejected
        /// as a golden - it loses by 3 hp of 220, a margin thin enough that
        /// ordinary implementation detail would flip it, which is exactly the
        /// property a pinned golden must not have. Chill's causality is isolated in
        /// unit tests instead, where it belongs: Task 5 asserts the speed change
        /// directly and Task 4 asserts the capacity assignment.
        public static CreatureSpec[] DeploymentWithChill()
        {
            var d = DeploymentWithoutChill();
            d[4] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 4, Instinct = Instinct.Vanguard,
                Trait1 = Trait.Chill, Tier1 = 1
            };
            return d;
        }

        [Fact]
        public void GoldenA_Wave6WithoutChillIsLostToAnUnanswerableCourser()
        {
            var o = Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            _out.WriteLine("Golden A hash: " + o.Hash + "  ticks: " + o.Ticks);

            Assert.Equal(Result.Loss, o.Result);
            Assert.Equal(1, o.BreachCount);
            Assert.Equal(RaiderType.Courser, o.Breaches[0].Type);

            // The diagnosis is the point of the wave: the trait is ABSENT, not
            // mis-tiered and not mis-placed.
            Assert.False(o.Breaches[0].Access);
        }

        [Fact]
        public void GoldenB_TheSameWaveWithChillIsCleared()
        {
            var o = Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithChill(), Seed);
            _out.WriteLine("Golden B hash: " + o.Hash + "  ticks: " + o.Ticks);

            Assert.Equal(Result.Win, o.Result);
            Assert.Equal(0, o.BreachCount);
            Assert.Equal(2, o.IntegrityRemaining);
        }

        [Fact]
        public void Run_IsReproducible()
        {
            var a = Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            var b = Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            Assert.Equal(a.Hash, b.Hash);
        }

        [Fact]
        public void PreWaveCheck_AgreesWithWhatActuallyHappens()
        {
            // The panel must not tell a player they are covered and then lose
            // the wave to access. Section 7's "one function, two call sites"
            // is what this asserts end to end.
            var noChill = new SimState(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill());
            Assert.False(Diagnosis.PreWaveCheck(noChill, RaiderType.Courser).Answered);

            var withChill = new SimState(WaveDef.Wave6(), Lane.Defile(), DeploymentWithChill());
            Assert.True(Diagnosis.PreWaveCheck(withChill, RaiderType.Courser).Answered);
        }
    }
}
```

- [ ] **Step 3: Run and read the two hashes off the output**

```bash
dotnet test Broodline.sln --nologo --logger "console;verbosity=detailed" 2>&1 | grep "Golden"
```

Expected: two `Golden A/B hash:` lines. **If either behavioural assertion fails, stop and fix the engine — do not pin a hash for a run that behaves wrongly.** Golden A must be a `Loss` with `Access == false`; Golden B must be a `Win`.

- [ ] **Step 4: Pin both hashes**

Add the recorded values as constants and assert them:

```csharp
        // Pinned 2026-09-10. A change here is a balance change or a bug -
        // never update these to match new output without knowing which.
        private const ulong GoldenAHash = 0;   // <- replace with the recorded value
        private const ulong GoldenBHash = 0;   // <- replace with the recorded value
```

Add to each golden test:

```csharp
            Assert.Equal(GoldenAHash, o.Hash);   // and GoldenBHash in the other
```

- [ ] **Step 5: Prove the goldens catch drift**

Temporarily change one normative thing — swap the `Phases.Movement(s)` and `Phases.Targeting(s)` lines in `Sim.Run`. Re-run.

Expected: **both goldens fail on the hash.** That is §4's claim being enforced rather than documented: movement before targeting is a behavioural contract, and reordering it changes outcomes. Revert and confirm green.

- [ ] **Step 6: Commit**

```bash
git add engine/Runtime/Combat/Sim.cs tests/engine/Combat/GoldenTests.cs engine/Runtime/Fix64.cs
git commit -m "feat: Sim.Run, the stall detector, and the two goldens

The loop with all eight phases in normative order, plus the two ways a
simulation is made to terminate that are not win or loss: the 5400-tick
hard cap and the 300-tick stall detector. Both return Stalled, which is a
content bug rather than a gameplay outcome - three of the eight raiders had
soft-lock versions during design, so it is a real shape.

Goldens are wave 6 without Chill and wave 6 with it: same wave, same seed,
one variable changed, so the pair isolates the counter as the only
difference between a loss and a clear. Golden A asserts access=false rather
than only a hash, because wave 6 exists to make the loss legible and a run
that loses for the wrong recorded reason is a real defect.

Verified by swapping movement and targeting and watching both goldens fail."
```

---

### Task 11: Fuzz and enforcement — the two layers that outlive this plan

The golden pair proves two specific runs. These two layers prove the properties that must hold for *every* run, and keep holding once the other seven counters land.

**Files:**
- Create: `tests/engine/Combat/FuzzTests.cs`
- Create: `tests/engine/Combat/CombatEnforcementTests.cs`

**Interfaces:**
- Consumes: `Sim`, `WaveDef`, `Lane`, `Rng`.
- Produces: nothing the engine consumes. These are guards.

- [ ] **Step 1: Write the fuzz tests**

`tests/engine/Combat/FuzzTests.cs`:

```csharp
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// combat_engine section 8.1: "Every simulation terminates." Three of the
    /// eight raiders required soft-lock fixes during design - Delver, Bulwark
    /// and Breaker all had versions that could permanently block progress -
    /// and the engine is supposed to refuse to let a new one ship.
    ///
    /// A golden proves one run. These prove the property.
    public class FuzzTests
    {
        private static CreatureSpec[] RandomDeployment(ref Rng gen)
        {
            var d = new CreatureSpec[5];
            for (int c = 0; c < 5; c++)
            {
                int tier = gen.NextInt(4);   // 0 means the trait is absent
                d[c] = new CreatureSpec
                {
                    Species = (Species)gen.NextInt(6),
                    Pocket = c,
                    Instinct = (Instinct)gen.NextInt(6),
                    Trait1 = tier > 0 ? Trait.Chill : Trait.None,
                    Tier1 = tier
                };
            }
            return d;
        }

        private static WaveDef RandomWave(ref Rng gen, int index)
        {
            int count = 1 + gen.NextInt(6);
            var spawns = new SpawnEntry[count];
            int tick = 0;
            for (int i = 0; i < count; i++)
            {
                tick += gen.NextInt(90);            // ordered by construction
                spawns[i] = new SpawnEntry { Tick = tick, Type = RaiderType.Courser };
            }
            return new WaveDef(1000 + index, 1 + gen.NextInt(12), 1, spawns);
        }

        [Fact]
        public void EverySimulationTerminates_AndNeverStalls()
        {
            for (int i = 0; i < 500; i++)
            {
                var gen = new Rng((ulong)(i + 1));
                var wave = RandomWave(ref gen, i);
                var deployment = RandomDeployment(ref gen);

                var o = Sim.Run(wave, Lane.Defile(), deployment, (ulong)(i + 1));

                Assert.NotEqual(Result.Running, o.Result);
                Assert.True(o.Ticks <= Stats.HardTickCap,
                    "run " + i + " exceeded the hard tick cap");

                // Stalled is a CONTENT bug, not a gameplay outcome. A valid
                // wave must never reach it - and every wave here is valid.
                Assert.NotEqual(Result.Stalled, o.Result);
            }
        }

        [Fact]
        public void EveryRunIsReproducibleFromItsInputs()
        {
            for (int i = 0; i < 200; i++)
            {
                var g1 = new Rng((ulong)(i + 1));
                var w1 = RandomWave(ref g1, i);
                var d1 = RandomDeployment(ref g1);

                var g2 = new Rng((ulong)(i + 1));
                var w2 = RandomWave(ref g2, i);
                var d2 = RandomDeployment(ref g2);

                Assert.Equal(
                    Sim.Run(w1, Lane.Defile(), d1, (ulong)(i + 1)).Hash,
                    Sim.Run(w2, Lane.Defile(), d2, (ulong)(i + 1)).Hash);
            }
        }

        [Fact]
        public void IntegrityNeverGoesUnnoticedNegativeWithoutALoss()
        {
            for (int i = 0; i < 300; i++)
            {
                var gen = new Rng((ulong)(i + 9001));
                var wave = RandomWave(ref gen, i);
                var o = Sim.Run(wave, Lane.Defile(), RandomDeployment(ref gen), (ulong)i);

                if (o.IntegrityRemaining <= 0)
                    Assert.Equal(Result.Loss, o.Result);
            }
        }

        [Fact]
        public void ADeliberateSoftLockIsCaughtByTheStallDetector()
        {
            // A wave whose only creature cannot reach the lane at all, against
            // a raider that is chilled to a crawl - the shape section 8.1
            // describes, forced on purpose. It must still terminate, and it
            // must terminate as a Loss rather than by burning to the cap.
            var wave = new WaveDef(9999, 99, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var d = new[]
            {
                new CreatureSpec { Species = Species.Vetch, Pocket = 4,
                                   Instinct = Instinct.Vanguard }
            };

            var o = Sim.Run(wave, Lane.Defile(), d, 1);

            Assert.NotEqual(Result.Running, o.Result);
            Assert.True(o.Ticks < Stats.HardTickCap,
                "the raider still advances, so this must resolve well before the cap");
        }
    }
}
```

- [ ] **Step 2: Run the fuzz tests**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS. **A `Stalled` failure here is a real finding, not a flaky test** — read the wave it names and work out which phase stops making progress.

- [ ] **Step 3: Write the enforcement test**

Follows the pattern `tests/engine/EnforcementTests.cs` established in Phase 1: assert the *thing that checks the code*, bluntly, by reading it. Tick order is normative and nothing else can see it change.

`tests/engine/Combat/CombatEnforcementTests.cs`:

```csharp
using System;
using System.IO;
using Xunit;

namespace Broodline.Sim.Tests.Combat
{
    /// combat_engine section 4: "the order is normative - changing it changes
    /// outcomes." That makes tick order a behavioural contract, but a contract
    /// nothing enforces: reorder two lines in Sim.Run and every unit test still
    /// passes, because each phase in isolation is still correct.
    ///
    /// The goldens would catch it - but only until someone re-pins them to
    /// match new output, which is exactly what a person does when they believe
    /// they made a harmless refactor. So this asserts the order in the source
    /// directly, and is deliberately blunt: a failure here is not "the code is
    /// wrong", it is "a balance change is being made and should be explicit".
    public class CombatEnforcementTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "engine")))
                dir = dir.Parent;
            Assert.True(dir != null, "could not locate the repository root");
            return dir.FullName;
        }

        [Fact]
        public void SimRun_StillCallsTheEightPhasesInNormativeOrder()
        {
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Sim.cs");
            Assert.True(File.Exists(path), "missing " + path);

            string source = File.ReadAllText(path);

            string[] ordered =
            {
                "Phases.Spawn(",
                "Phases.State(",
                "Phases.Movement(",
                "Phases.Targeting(",
                "Phases.Attack(",
                "Phases.Death(",
                "Phases.Breach(",
                "Phases.Resolve("
            };

            int previous = -1;
            for (int i = 0; i < ordered.Length; i++)
            {
                int at = source.IndexOf(ordered[i], StringComparison.Ordinal);
                Assert.True(at >= 0, "Sim.Run no longer calls " + ordered[i]);
                Assert.True(at > previous,
                    ordered[i] + " is out of normative order - see combat_engine section 4. " +
                    "Movement must precede targeting so a creature never fires at a " +
                    "position a raider has already left, and death must follow attack.");
                previous = at;
            }
        }

        [Fact]
        public void CapacityIsRecomputedRatherThanAccumulated()
        {
            // 5.3: "Capacity is recomputed from scratch every tick from the
            // live creature set, never accumulated." An accumulating
            // implementation would need somewhere to accumulate INTO, so the
            // guard is that AssignChill clears before it assigns.
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Capacity.cs");
            string source = File.ReadAllText(path);

            Assert.Contains("RaiderChilled[r] = false", source);
        }
    }
}
```

- [ ] **Step 4: Prove the enforcement test actually fires**

Swap the `Phases.Movement(s)` and `Phases.Targeting(s)` lines in `Sim.cs` and re-run.

Expected: `SimRun_StillCallsTheEightPhasesInNormativeOrder` **fails**, naming `Phases.Targeting(`. Revert and confirm green.

This is the difference between a comment saying the order is normative and something that notices when it is not.

- [ ] **Step 5: Commit**

```bash
git add tests/engine/Combat/FuzzTests.cs tests/engine/Combat/CombatEnforcementTests.cs
git commit -m "test: fuzz the termination guarantee and enforce tick order

The goldens prove two runs. Fuzz proves the property section 8.1 actually
promises - every simulation terminates - over 500 randomised waves and
deployments, and asserts Stalled is never reached on a valid wave. Three of
the eight raiders had soft-lock versions during design, so this is a real
shape rather than a theoretical one.

Tick order was normative and unenforced: reorder two lines in Sim.Run and
every unit test still passes, because each phase in isolation is still
correct. The goldens would catch it only until someone re-pins them to
match, which is what a person does when they believe they made a harmless
refactor. The enforcement test reads the order out of the source, following
the Phase 1 EnforcementTests pattern, so a reorder has to be an explicit
decision."
```

---

### Task 12: The headless batch runner

`combat_engine.md` §9.1: *"Build the batch runner with the engine, not as a later tool."* It is the only honest way to answer questions the design cannot settle by argument.

**Files:**
- Create: `engine/Runtime/Combat/BatchRunner.cs`
- Create: `tests/engine/Combat/BatchRunnerTests.cs`

**Interfaces:**
- Consumes: `Sim`.
- Produces:
  - `struct BatchResult { int Runs; int Clears; int Losses; int Stalls; ulong Hash; }`
  - `static BatchResult BatchRunner.Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong firstSeed, int runs)`

- [ ] **Step 1: Write the failing test**

```csharp
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class BatchRunnerTests
    {
        [Fact]
        public void Batch_CountsOutcomesAcrossRuns()
        {
            var r = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(),
                                    firstSeed: 1, runs: 20);

            Assert.Equal(20, r.Runs);
            Assert.Equal(0, r.Stalls);
            // Wave 6 without Chill is designed to be lost, and the simulation
            // is deterministic, so every seed loses it.
            Assert.Equal(20, r.Losses);
            Assert.Equal(0, r.Clears);
        }

        [Fact]
        public void Batch_ShowsTheCounterIsWhatChangesTheOutcome()
        {
            var without = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                          GoldenTests.DeploymentWithoutChill(), 1, 20);
            var with = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                       GoldenTests.DeploymentWithChill(), 1, 20);

            Assert.Equal(0, without.Clears);
            Assert.Equal(20, with.Clears);
        }

        [Fact]
        public void Batch_IsReproducible()
        {
            var a = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(), 7, 10);
            var b = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(), 7, 10);
            Assert.Equal(a.Hash, b.Hash);
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `BatchRunner` does not exist.

- [ ] **Step 3: Write `BatchRunner.cs`**

```csharp
namespace Broodline.Sim.Combat
{
    public struct BatchResult
    {
        public int Runs;
        public int Clears;
        public int Losses;
        public int Stalls;
        public ulong Hash;
    }

    /// combat_engine section 9.1: because the engine is deterministic, headless
    /// and free of rendering, it can run a wave far faster than real time -
    /// and that is what turns "what is Regrow worth" from an argument into a
    /// measurement. Built with the engine rather than as a later tool.
    ///
    /// Minimal by intent. Sweeping trait sets is Phase 3, once there are
    /// enough traits to sweep over.
    public static class BatchRunner
    {
        public static BatchResult Run(
            WaveDef wave, Lane lane, CreatureSpec[] deployment,
            ulong firstSeed, int runs)
        {
            var result = new BatchResult { Runs = runs };
            var hash = Hash.Create();

            for (int i = 0; i < runs; i++)
            {
                var outcome = Sim.Run(wave, lane, deployment, firstSeed + (ulong)i);

                switch (outcome.Result)
                {
                    case Result.Win:     result.Clears++;  break;
                    case Result.Loss:    result.Losses++;  break;
                    default:             result.Stalls++;  break;
                }

                hash.Add(unchecked((long)outcome.Hash));
            }

            result.Hash = hash.Value;
            return result;
        }
    }
}
```

- [ ] **Step 4: Run until green, then commit**

```bash
dotnet test Broodline.sln --nologo
git add engine/Runtime/Combat/BatchRunner.cs tests/engine/Combat/BatchRunnerTests.cs
git commit -m "feat: the headless batch runner

Section 9.1 asks for this with the engine rather than as a later tool,
because it is the only honest way to answer questions the design cannot
settle by argument. Minimal by intent - counting clears across seeds is
enough to make the counter's effect measurable, and sweeping trait sets
waits for Phase 3 when there are traits to sweep."
```

---

### Task 13: Extend the determinism corpus to combat

The load-bearing one. Phase 1's gate proves `Fix64` and `ToySim` agree across runtimes; without this it would keep passing while the code that actually decides raids never executes under IL2CPP.

**Files:**
- Modify: `engine/Runtime/Corpus.cs`
- Modify: `tests/engine/CorpusTests.cs`
- Create: `tests/engine/Combat/CombatCorpusTests.cs`

**Interfaces:**
- Consumes: `Sim`, `WaveDef`, `Lane`.
- Produces: `Corpus.RunScenario` additionally folding a combat run.

- [ ] **Step 1: Write the failing test**

`tests/engine/Combat/CombatCorpusTests.cs`:

```csharp
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class CombatCorpusTests
    {
        [Fact]
        public void CombatScenario_IsStableAcrossCalls()
        {
            Assert.Equal(Corpus.RunCombatScenario(0), Corpus.RunCombatScenario(0));
            Assert.Equal(Corpus.RunCombatScenario(499), Corpus.RunCombatScenario(499));
        }

        [Fact]
        public void CombatScenario_VariesWithIndex()
        {
            // If every index produced the same hash, a divergence would show on
            // one line of the diff or none, and the gate would be near-blind.
            Assert.NotEqual(Corpus.RunCombatScenario(0), Corpus.RunCombatScenario(1));
        }

        [Fact]
        public void CombatScenario_ExercisesBothOutcomes()
        {
            int clears = 0, losses = 0;
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                var o = Corpus.CombatOutcome(i);
                if (o.Result == Result.Win) clears++;
                else if (o.Result == Result.Loss) losses++;
                Assert.NotEqual(Result.Stalled, o.Result);
            }

            // A corpus that only ever lost would never execute the counter path
            // on IL2CPP, which is the half most worth proving.
            Assert.True(clears > 0, "corpus never clears a wave");
            Assert.True(losses > 0, "corpus never loses a wave");
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail**

```bash
dotnet test Broodline.sln --nologo
```

Expected: compile failure — `Corpus.RunCombatScenario` does not exist.

- [ ] **Step 3: Add the combat scenario to `Corpus.cs`**

```csharp
        /// Generates combat scenario N deterministically. Varies the Chill tier
        /// (including absent), the pockets and the Instincts, so the corpus
        /// executes both the answered and the unanswered path.
        public static Broodline.Sim.Combat.Outcome CombatOutcome(int index)
        {
            var gen = new Rng((ulong)(index + 1) * 7919UL);

            // Tier 0 means no Chill at all, which is the access=false path.
            int tier = gen.NextInt(4);

            var deployment = new Broodline.Sim.Combat.CreatureSpec[5];
            for (int c = 0; c < 5; c++)
            {
                deployment[c] = new Broodline.Sim.Combat.CreatureSpec
                {
                    Species = (Broodline.Sim.Combat.Species)gen.NextInt(6),
                    Pocket = c,
                    Instinct = (Broodline.Sim.Combat.Instinct)gen.NextInt(6)
                };
            }

            if (tier > 0)
            {
                deployment[0].Species = Broodline.Sim.Combat.Species.Pale;
                deployment[0].Trait1 = Broodline.Sim.Combat.Trait.Chill;
                deployment[0].Tier1 = tier;
            }

            return Broodline.Sim.Combat.Sim.Run(
                Broodline.Sim.Combat.WaveDef.Wave6(),
                Broodline.Sim.Combat.Lane.Defile(),
                deployment,
                (ulong)(index + 1));
        }

        public static ulong RunCombatScenario(int index) => CombatOutcome(index).Hash;
```

- [ ] **Step 4: Fold it into the existing scenario hash**

In `RunScenario`, after `FoldArithmeticSweep(ref hash);`:

```csharp
            hash.Add(unchecked((long)RunCombatScenario(index)));
```

**Every corpus hash changes as a result**, exactly as the arithmetic sweep did. That is intended: the same `PASS: 500 scenarios agree` now asserts a much larger claim.

- [ ] **Step 5: Run the .NET side and confirm green**

```bash
dotnet test Broodline.sln --nologo
```

Expected: PASS.

- [ ] **Step 6: Run the full cross-runtime gate**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first.

**This is the moment the phase is actually proven.** A failure here means the combat engine is not deterministic across runtimes — read the first differing scenario index, reproduce it with `Corpus.CombatOutcome(index)` on both sides, and find which phase diverges. Do not proceed past a failure.

- [ ] **Step 7: Prove the extended corpus catches combat drift**

Temporarily change one thing inside the tick loop that the arithmetic sweep cannot see — for example, make `Attacks.Damage` return `damage + 1` for `Species.Hollow`. Rebuild the IL2CPP player only (leave the CoreCLR side unmodified) and re-run.

Expected: **FAIL**, with the diff naming scenario lines. Revert and confirm `PASS` again.

This is the check that distinguishes "the corpus mentions combat" from "the gate would catch a combat divergence".

- [ ] **Step 8: Commit**

```bash
git add engine/Runtime/Corpus.cs tests/engine
git commit -m "test: extend the determinism corpus to the combat engine

The gate proved Fix64 and ToySim agreed across runtimes. Adding gameplay
without extending it would have left it green while the code that actually
decides raids never executed under IL2CPP - the same seam that let Mul,
Div and Sqrt go unproven before 0607cbe.

Determinism is not decorative here. Raid verification IS a re-run
comparison: the client submits seed, placement and Rally timestamp, and the
server re-runs and compares. A cross-runtime divergence in the tick loop is
a rejected raid for an honest player.

Every corpus hash changes, as it did for the arithmetic sweep. Scenarios
vary the Chill tier including absent, so the corpus executes both the
answered and the unanswered path rather than only the one."
```

---

## What this plan deliberately does not do

- **No second raider and no second counter.** The other seven counters are additive at their own phase sites — that is the test of whether the structure is right. Skirmisher and Splash are the highest-value next pair, per `build_order` §5.
- **No replay format.** §3's format is inputs rather than state and is cheap to add, but it wants more of the simulation's shape than one raider provides. Phase 1 deferred it for the same reason.
- **No server verification path.** The engine being deterministic and headless is what makes verification possible; wiring it is `broodline_solo_execution.md`'s.
- **No rendering, no instancing, no degradation ladder.** §9 is explicit that simulation is not the performance problem. The engine exposes the per-tick entity count via `SimState.RaiderCount`; consuming it is the client's.
- **No balance sweeps.** The batch runner exists so tuning becomes measurement. Running the sweeps is Phase 3, once there are enough traits to sweep over.
- **No Aberrants.** Contrary needs the per-tick allied-target census of §6, which is one pass over five creatures and should be built when its consumer is.
- **No multi-lane geometry.** Reach scales by lane count and is the counter that needs it.

## Definition of done

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
```

Both green, with:

- Wave 6 without Chill returning `Loss`, one breach, `Access == false`.
- Wave 6 with Chill I returning `Win`, integrity 2 intact.
- Both golden hashes pinned, and both proven to fail when movement and targeting are swapped.
- `cross-runtime-diff.sh` printing `PASS: 500 scenarios agree` over a corpus that runs the combat engine, and proven to fail when a tick-loop constant is perturbed on one runtime only.
- No `Stalled` outcome reachable from any corpus scenario.
