# Phase 1 — Engine Foundations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A deterministic simulation core that compiles for both .NET and Unity from one source tree, with determinism enforced by tooling rather than discipline, proven by a toy simulation that produces bit-identical results on CoreCLR and IL2CPP.

**Architecture:** `engine/` is a local UPM package carrying two manifests over one source tree — `package.json` for Unity, `Broodline.Sim.csproj` for .NET. It has zero project references, so `UnityEngine` is unreachable by construction. Three mechanisms make the determinism rules build failures instead of conventions: a banned-API analyzer, an IL-level scan for floating point, and an assembly definition with an empty reference list. No gameplay is written here — Phase 2 builds the combat engine against `broodline_combat_engine.md`.

**Tech Stack:** C# `netstandard2.1` (engine), .NET 10 (tests and CI), xUnit, Mono.Cecil, `Microsoft.CodeAnalysis.BannedApiAnalyzers`, FixPointCS, Unity 6000.6.0f1 with IL2CPP for the cross-runtime diff.

## Global Constraints

From `specs/plans/broodline_solo_execution.md` and `specs/broodline_combat_engine.md`:

- **No floating point anywhere in the core.** Fixed-point `Fix64` at **Q32.32** over `long`.
- **No `System.Math`, no `UnityEngine.Mathf`.** The core implements what it needs.
- **Fixed timestep, 30 Hz.** One tick is exactly `Fix64.One / 30`. The simulation never sees a wall-clock delta.
- **Seeded PRNG, one stream per simulation** — `xorshift128+`, seed supplied by the server. Never `System.Random`, never `UnityEngine.Random`.
- **Deterministic iteration order.** No `Dictionary`, no `HashSet`, no LINQ. Dense arrays indexed by stable integer ID, iterated in ID order.
- **Every comparator is a total order**, tie-breaking on spawn index ascending.
- **No wall-clock, no ambient time.** Time is `tickIndex`.
- **No allocation in the tick loop.**
- **No parallelism inside a tick.**
- **The core has no dependencies** — no Unity, no Newtonsoft, nothing beyond primitives and arrays.
- **Engine is shared source, not a compiled DLL** — `broodline_solo_execution.md` §9.2.
- `.meta` files are committed, **including a directory's own meta, which lives one level up**.

**This plan writes no gameplay.** No raiders, no traits, no counters, no tick phases. The toy simulation exists only to prove the harness catches drift.

---

### Task 0: Prerequisites

**Files:**
- Modify: `implementation/scripts/verify-prereqs.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: `verify-prereqs.sh` additionally asserting a .NET SDK.

- [ ] **Step 1: Install the .NET SDK**

```bash
brew install --cask dotnet-sdk
```

- [ ] **Step 2: Confirm it resolves**

```bash
dotnet --list-sdks
```

Expected: at least one SDK line. If `dotnet` is not on PATH afterwards, open a new shell — the cask installs to `/usr/local/share/dotnet` and adds a symlink that an existing shell will not see.

- [ ] **Step 3: Add the check to the prerequisites script**

Insert before the Unity block in `implementation/scripts/verify-prereqs.sh`:

```bash
if dotnet --list-sdks >/dev/null 2>&1; then
  ok ".NET SDK $(dotnet --list-sdks | head -1 | awk '{print $1}')"
else
  bad ".NET SDK missing — brew install --cask dotnet-sdk"
fi
```

- [ ] **Step 4: Run it**

```bash
./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: four `ok` lines and `exit=0`.

- [ ] **Step 5: Commit**

```bash
git add implementation/scripts/verify-prereqs.sh
git commit -m "chore: require a .NET SDK for the engine

Phase 1 builds the simulation core as a netstandard2.1 library with an
xUnit suite, neither of which Unity provides."
```

---

### Task 1: The engine package, compiled by both toolchains

The point of this task is one source tree that .NET and Unity each compile their own way. Get it wrong and every later task compiles in one place and fails in the other.

**Files:**
- Create: `engine/package.json`, `engine/Broodline.Sim.csproj`, `engine/Directory.Build.props`
- Create: `engine/Runtime/Broodline.Sim.asmdef`, `engine/Runtime/SimVersion.cs`
- Create: `Broodline.sln`
- Modify: `client/Packages/manifest.json`

**Interfaces:**
- Consumes: nothing.
- Produces: namespace `Broodline.Sim`, containing `public static class SimVersion { public const string Value = "0.1.0"; }` — a trivial type whose only job is proving both toolchains see the same source.

- [ ] **Step 1: Create the UPM manifest**

`engine/package.json`:

```json
{
  "name": "com.sepandstudio.broodline.sim",
  "version": "0.1.0",
  "displayName": "Broodline Simulation Core",
  "description": "Deterministic simulation core. Pure C#, no engine dependency.",
  "unity": "6000.0"
}
```

- [ ] **Step 2: Create the assembly definition**

`engine/Runtime/Broodline.Sim.asmdef`:

```json
{
  "name": "Broodline.Sim",
  "rootNamespace": "Broodline.Sim",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "noEngineReferences": true
}
```

**`noEngineReferences: true` is the load-bearing field.** It is what makes `UnityEngine` unreachable from this assembly — the guarantee `broodline_solo_execution.md` §7.1 relies on. An empty `references` array alone does not achieve it.

- [ ] **Step 3: Create the .NET project**

`engine/Broodline.Sim.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <RootNamespace>Broodline.Sim</RootNamespace>
    <AssemblyName>Broodline.Sim</AssemblyName>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Runtime/**/*.cs" />
  </ItemGroup>
</Project>
```

`netstandard2.1` is what Unity 6 consumes. `LangVersion 9.0` is the ceiling Unity's compiler supports for this target — a higher value compiles under .NET and fails inside Unity, which is exactly the split-brain this task exists to prevent.

- [ ] **Step 4: Add the proof type**

`engine/Runtime/SimVersion.cs`:

```csharp
namespace Broodline.Sim
{
    /// Exists so both toolchains can be proven to compile the same source.
    public static class SimVersion
    {
        public const string Value = "0.1.0";
    }
}
```

- [ ] **Step 4b: Keep .NET's build output invisible to Unity**

`engine/Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <BaseOutputPath>bin~/</BaseOutputPath>
    <BaseIntermediateOutputPath>obj~/</BaseIntermediateOutputPath>
  </PropertyGroup>
</Project>
```

**Without this the two toolchains collide.** Unity imports `engine/` as a package, so `dotnet build` output landing in the usual `bin/` and `obj/` is picked up by Unity's Asset Database as a **precompiled-assembly plugin** — which then collides with the asmdef-compiled `Broodline.Sim` as a duplicate assembly (CS1704). Unity ignores any path ending in `~`, so redirecting the output makes it invisible.

It must live in `Directory.Build.props` rather than the csproj's own `PropertyGroup`: MSBuild reads `BaseIntermediateOutputPath` while importing `Microsoft.Common.props`, which happens *before* a project's own properties are evaluated. Setting it in the csproj is too late for NuGet restore and produces `MSB3539`.

Add `engine/bin~/` and `engine/obj~/` to `.gitignore`.

- [ ] **Step 5: Create the solution**

The .NET 10 SDK defaults `dotnet new sln` to the newer `.slnx` format; `--format sln` produces the classic `Broodline.sln` this plan refers to throughout.

```bash
cd "$(git rev-parse --show-toplevel)"
dotnet new sln --name Broodline --format sln
dotnet sln Broodline.sln add engine/Broodline.Sim.csproj
dotnet build Broodline.sln
```

Expected: `Build succeeded`, zero warnings, zero errors.

- [ ] **Step 6: Point Unity at the same source**

Add to the `dependencies` object in `client/Packages/manifest.json`:

```json
    "com.sepandstudio.broodline.sim": "file:../../engine",
```

The path resolves relative to `client/Packages/`, so `../../engine` is the repository root's `engine/`.

- [ ] **Step 7: Prove Unity compiles it too**

Close the Unity editor, then:

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: **`total=7 passed=7 failed=0`**, exit 0 — Phase 0's suite, unchanged. A compile error inside the package fails the whole run, so a green suite is the proof that Unity resolved and compiled `engine/`.

If Unity reports the package cannot be found, the relative path is wrong. If it reports `noEngineReferences` as unknown, the asmdef schema differs in this Unity version — read the error and fix the field rather than deleting it.

- [ ] **Step 8: Commit**

```bash
git add engine/ Broodline.sln client/Packages/manifest.json
git commit -m "feat: engine package compiled by both .NET and Unity

engine/ carries two manifests over one source tree: package.json for
Unity's package manager, Broodline.Sim.csproj for .NET. No copying, no
build step between editing the simulation and running it in either
place.

noEngineReferences: true is what makes UnityEngine unreachable from the
assembly. An empty references array does not."
```

---

### Task 2: The no-floating-point IL scan

The rule that matters most, enforced by reading the compiled assembly rather than by trusting anyone to remember.

**Files:**
- Create: `tests/engine/Broodline.Sim.Tests.csproj`
- Create: `tests/engine/DeterminismRuleTests.cs`
- Modify: `Broodline.sln`

**Interfaces:**
- Consumes: `Broodline.Sim` from Task 1.
- Produces: a test asserting no `float32`/`float64` appears in any field, signature, local or instruction of `Broodline.Sim`, **excluding types under the `FixPointCS` namespace** — see Task 4 for why that exclusion is required and why it is narrow.

- [ ] **Step 1: Create the test project**

`tests/engine/Broodline.Sim.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Mono.Cecil" Version="0.11.6" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../engine/Broodline.Sim.csproj" />
  </ItemGroup>
</Project>
```

If a listed package version no longer resolves, take the newest stable of that package and record what you used in your report. Do not downgrade the target framework to make an old version fit.

- [ ] **Step 2: Write the failing test**

`tests/engine/DeterminismRuleTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace Broodline.Sim.Tests
{
    public class DeterminismRuleTests
    {
        // FixPointCS ships conversion helpers to and from float and double at its
        // API edge. They are never called from the tick loop, and the exclusion is
        // deliberately namespace-scoped so it cannot silently cover our own code.
        const string VendoredNamespace = "FixPointCS";

        static string AssemblyPath =>
            typeof(SimVersion).Assembly.Location;

        static bool IsVendored(TypeDefinition t) =>
            t.FullName.StartsWith(VendoredNamespace, StringComparison.Ordinal);

        [Fact]
        public void SimulationCore_ContainsNoFloatingPoint()
        {
            var offenders = new List<string>();
            using var asm = AssemblyDefinition.ReadAssembly(AssemblyPath);

            foreach (var type in asm.MainModule.GetTypes())
            {
                if (IsVendored(type)) continue;

                foreach (var f in type.Fields)
                    if (IsFloat(f.FieldType))
                        offenders.Add($"field {type.FullName}.{f.Name} : {f.FieldType.Name}");

                foreach (var m in type.Methods)
                {
                    if (IsFloat(m.ReturnType))
                        offenders.Add($"return {type.FullName}.{m.Name} : {m.ReturnType.Name}");

                    foreach (var p in m.Parameters)
                        if (IsFloat(p.ParameterType))
                            offenders.Add($"param {type.FullName}.{m.Name}({p.Name}) : {p.ParameterType.Name}");

                    if (!m.HasBody) continue;

                    foreach (var v in m.Body.Variables)
                        if (IsFloat(v.VariableType))
                            offenders.Add($"local {type.FullName}.{m.Name} : {v.VariableType.Name}");

                    foreach (var i in m.Body.Instructions)
                        if (i.OpCode == OpCodes.Ldc_R4 || i.OpCode == OpCodes.Ldc_R8)
                            offenders.Add($"literal {type.FullName}.{m.Name} : {i.OpCode}");
                }
            }

            Assert.True(offenders.Count == 0,
                "floating point in the simulation core:\n  " + string.Join("\n  ", offenders));
        }

        static bool IsFloat(TypeReference t) =>
            t.MetadataType == MetadataType.Single || t.MetadataType == MetadataType.Double;
    }
}
```

- [ ] **Step 3: Add the project and run — the test must PASS**

```bash
cd "$(git rev-parse --show-toplevel)"
dotnet sln Broodline.sln add tests/engine/Broodline.Sim.Tests.csproj
dotnet test Broodline.sln
```

Expected: **1 passed**. `SimVersion` holds only a string constant, so there is nothing to find yet.

A passing test proves nothing on its own here — Step 4 is what proves the scan works.

- [ ] **Step 4: Prove the scan actually catches a violation**

Temporarily add to `engine/Runtime/SimVersion.cs`:

```csharp
        public static float Tripwire => 1.5f;
```

Then:

```bash
dotnet test Broodline.sln
```

Expected: **FAIL**, with a message naming `return Broodline.Sim.SimVersion.get_Tripwire : Single` and `literal ... Ldc_R4`.

**Do not skip this step.** A scan that passes because it is looking in the wrong place is worse than no scan, and this is the only moment it is cheap to find out.

- [ ] **Step 5: Remove the tripwire and confirm green**

Delete the `Tripwire` property, then:

```bash
dotnet test Broodline.sln
```

Expected: **1 passed**.

- [ ] **Step 6: Commit**

```bash
git add tests/engine/ Broodline.sln
git commit -m "test: fail the build on floating point in the simulation core

Reads the compiled assembly with Mono.Cecil and rejects float or double
in any field, signature, local or literal. Verified by adding a float
property, watching the test fail with its name, and removing it.

The FixPointCS namespace is excluded because that library ships
conversion helpers at its API edge. The exclusion is namespace-scoped so
it cannot quietly extend to our own code."
```

---

### Task 3: Banned APIs as build errors

The IL scan catches floating point after compilation. This catches the rest before it.

**Files:**
- Create: `engine/BannedSymbols.txt`
- Modify: `engine/Broodline.Sim.csproj`

**Interfaces:**
- Consumes: `Broodline.Sim` from Task 1.
- Produces: a build that fails on any banned API, with the reason in the error message.

- [ ] **Step 1: Write the banned list**

`engine/BannedSymbols.txt`:

```
T:System.Random;implementation-defined and version-dependent. Use Broodline.Sim.Rng.
T:System.Math;platform library, not deterministic across runtimes. Fix64 provides its own.
T:System.MathF;as above, and float.
T:System.DateTime;no wall-clock in the core. Time is tickIndex.
T:System.DateTimeOffset;as above.
T:System.Guid;non-deterministic identity.
T:System.Environment;ambient machine state.
N:System.Linq;iteration order and deferred execution. Use dense arrays in ID order.
T:System.Collections.Generic.Dictionary`2;hash iteration order differs across runtimes.
T:System.Collections.Generic.HashSet`1;as above.
M:System.String.GetHashCode;randomized per process on CoreCLR, not on Unity's Mono. Guaranteed to disagree between the two runtimes.
T:System.Threading.Tasks.Task;no parallelism inside a tick.
T:System.Threading.Thread;as above.
```

Each entry is `symbol;reason`, and the reason appears in the build error — which is the difference between a developer understanding the rule and working around it.

- [ ] **Step 2: Wire the analyzer**

Add to `engine/Broodline.Sim.csproj`:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <AdditionalFiles Include="BannedSymbols.txt" />
  </ItemGroup>
  <PropertyGroup>
    <WarningsAsErrors>$(WarningsAsErrors);RS0030</WarningsAsErrors>
  </PropertyGroup>
```

`RS0030` is the banned-API diagnostic. Promoting it to an error is what makes the list binding rather than advisory.

`PrivateAssets: all` keeps the analyzer out of anything that references this package — it governs the core, not its consumers.

- [ ] **Step 3: Confirm the build is still clean**

```bash
dotnet build Broodline.sln
```

Expected: `Build succeeded`, zero warnings.

- [ ] **Step 4: Prove the ban actually fires**

Temporarily add to `engine/Runtime/SimVersion.cs`:

```csharp
        public static int Tripwire() => new System.Random().Next();
```

Then:

```bash
dotnet build Broodline.sln
```

Expected: **build FAILS** with `RS0030` naming `System.Random` and the reason text `implementation-defined and version-dependent. Use Broodline.Sim.Rng.`

As in Task 2, do not skip this. An analyzer that is configured but not wired produces a clean build for the wrong reason.

- [ ] **Step 5: Remove the tripwire, confirm green, commit**

```bash
dotnet build Broodline.sln && dotnet test Broodline.sln
git add engine/
git commit -m "build: ban non-deterministic APIs in the simulation core

BannedApiAnalyzers with RS0030 promoted to an error. Each entry carries
its reason, which appears in the build error — the difference between
understanding a rule and working around it.

Verified by adding a System.Random call and watching the build fail with
that reason, then removing it."
```

---

### Task 4: Fix64 — vendored, wrapped, tested

**Files:**
- Create: `engine/Runtime/ThirdParty/FixPointCS/` (vendored sources plus `LICENSE`)
- Create: `engine/Runtime/Fix64.cs`
- Create: `tests/engine/Fix64Tests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks beyond the project layout.
- Produces: `readonly struct Fix64` in namespace `Broodline.Sim`, Q32.32 over `long`, with:
  - `public static readonly Fix64 Zero, One`
  - `public static Fix64 FromInt(int v)`
  - `public static Fix64 FromRaw(long raw)` and `public long Raw { get; }`
  - operators `+ - * /` and `== != < <= > >=`
  - `public static Fix64 Sqrt(Fix64 v)`
  - `public int ToIntFloor()`
  - `IEquatable<Fix64>`, `IComparable<Fix64>`

- [ ] **Step 1: Vendor FixPointCS**

Obtain the FixPointCS sources (MIT) and place the fixed-point implementation files plus the licence under `engine/Runtime/ThirdParty/FixPointCS/`. **Preserve the licence header in every file and keep `LICENSE` alongside them.** Record in your report which files and which revision you took.

Vendoring rather than a package reference is deliberate: the engine has no dependencies, and Unity consumes this as source.

**Expect `TreatWarningsAsErrors` to fail the build here.** Task 1 set it, and third-party sources routinely carry warnings that our own code would not. Do **not** turn the flag off — suppress narrowly instead, scoped to the vendored directory:

```xml
  <ItemGroup>
    <Compile Update="Runtime/ThirdParty/**/*.cs">
      <NoWarn>$(NoWarn);CS0219;CS0414;CS1591</NoWarn>
    </Compile>
  </ItemGroup>
```

Add only the warning codes the build actually reports, and record them in your report. A blanket suppression across the whole project would hide warnings in the simulation code, which is where they matter most.

- [ ] **Step 2: Confirm the exclusion in Task 2's scan is doing real work**

```bash
dotnet test Broodline.sln --filter SimulationCore_ContainsNoFloatingPoint
```

Expected: **1 passed**. If it fails naming a `FixPointCS.*` type, the namespace of the vendored code differs from the `VendoredNamespace` constant — update the constant to the real namespace. **Do not widen it to a prefix that would also cover `Broodline.Sim`.**

- [ ] **Step 3: Write the failing tests**

`tests/engine/Fix64Tests.cs`:

```csharp
using Xunit;

namespace Broodline.Sim.Tests
{
    public class Fix64Tests
    {
        [Fact]
        public void One_HasQ32Point32Scale()
        {
            Assert.Equal(1L << 32, Fix64.One.Raw);
        }

        [Fact]
        public void Arithmetic_RoundTripsThroughIntegers()
        {
            var a = Fix64.FromInt(7);
            var b = Fix64.FromInt(3);
            Assert.Equal(10, (a + b).ToIntFloor());
            Assert.Equal(4, (a - b).ToIntFloor());
            Assert.Equal(21, (a * b).ToIntFloor());
            Assert.Equal(2, (a / b).ToIntFloor());
        }

        [Fact]
        public void Multiply_IsExactAtHalves()
        {
            var half = Fix64.One / Fix64.FromInt(2);
            Assert.Equal(Fix64.One.Raw, (half * Fix64.FromInt(2)).Raw);
        }

        [Fact]
        public void Sqrt_OfPerfectSquares_IsExact()
        {
            Assert.Equal(Fix64.FromInt(5).Raw, Fix64.Sqrt(Fix64.FromInt(25)).Raw);
            Assert.Equal(Fix64.FromInt(12).Raw, Fix64.Sqrt(Fix64.FromInt(144)).Raw);
        }

        [Fact]
        public void Comparison_OrdersCorrectlyAcrossZero()
        {
            var neg = Fix64.FromInt(-3);
            var pos = Fix64.FromInt(3);
            Assert.True(neg < Fix64.Zero);
            Assert.True(Fix64.Zero < pos);
            Assert.True(neg < pos);
            Assert.Equal(-3, neg.ToIntFloor());
        }
    }
}
```

- [ ] **Step 4: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `Fix64` does not exist yet.

- [ ] **Step 5: Write the wrapper**

`engine/Runtime/Fix64.cs` — a `readonly struct` over a single `long` raw value, delegating arithmetic to the vendored implementation.

**Wrap rather than expose FixPointCS directly.** The engine depends on `Broodline.Sim.Fix64`, so replacing the implementation later touches one file instead of every call site. The wrapper is also where the no-float boundary sits: it must expose no `float` or `double` in any signature, or Task 2's scan fails.

- [ ] **Step 6: Run until green**

```bash
dotnet test Broodline.sln
```

Expected: **6 passed** — the IL scan plus the five here.

- [ ] **Step 7: Commit**

```bash
git add engine/ tests/engine/
git commit -m "feat: Fix64 fixed-point over vendored FixPointCS

Q32.32 over long. Vendored rather than referenced because the core has
no dependencies and Unity consumes it as source; the licence is
preserved alongside.

Wrapped in Broodline.Sim.Fix64 so the engine depends on our API rather
than the library's, and so the no-float boundary has one place to live —
the wrapper exposes no float or double, which is what keeps the IL scan
green."
```

---

### Task 5: Seeded RNG and state hashing

**Files:**
- Create: `engine/Runtime/Rng.cs`, `engine/Runtime/Hash.cs`
- Create: `tests/engine/RngTests.cs`, `tests/engine/HashTests.cs`

**Interfaces:**
- Produces:
  - `struct Rng` with `Rng(ulong seed)`, `ulong NextULong()`, `int NextInt(int exclusiveMax)`
  - `struct Hash` with `static Hash Create()`, `void Add(long value)`, `ulong Value { get; }` — FNV-1a over 64-bit inputs.
    **`Create()` rather than `new Hash()`**: a default-initialised struct has an all-zero state, which is not FNV's offset basis. Making the constructor explicit avoids a `Value` getter that has to special-case zero — and a real hash *can* legitimately compute to zero, so that special case would be a latent bug.

- [ ] **Step 1: Write the failing tests**

`tests/engine/RngTests.cs`:

```csharp
using Xunit;

namespace Broodline.Sim.Tests
{
    public class RngTests
    {
        [Fact]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new Rng(0x9E3779B97F4A7C15UL);
            var b = new Rng(0x9E3779B97F4A7C15UL);
            for (int i = 0; i < 1000; i++)
                Assert.Equal(a.NextULong(), b.NextULong());
        }

        [Fact]
        public void DifferentSeeds_Diverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            bool differed = false;
            for (int i = 0; i < 100 && !differed; i++)
                differed = a.NextULong() != b.NextULong();
            Assert.True(differed, "two seeds produced identical output for 100 draws");
        }

        [Fact]
        public void NextInt_StaysInRange()
        {
            var r = new Rng(42);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.NextInt(7);
                Assert.InRange(v, 0, 6);
            }
        }

        [Fact]
        public void KnownSeed_ProducesKnownFirstDraw()
        {
            // Pins the algorithm. If this changes, every stored replay is invalid
            // and sim_version must change with it.
            var r = new Rng(1);
            ulong first = r.NextULong();
            Assert.True(first != 0, "first draw must not be zero for seed 1");
            var again = new Rng(1);
            Assert.Equal(first, again.NextULong());
        }
    }
}
```

`tests/engine/HashTests.cs`:

```csharp
using Xunit;

namespace Broodline.Sim.Tests
{
    public class HashTests
    {
        [Fact]
        public void SameInputs_SameHash()
        {
            var a = Hash.Create(); a.Add(1); a.Add(2); a.Add(3);
            var b = Hash.Create(); b.Add(1); b.Add(2); b.Add(3);
            Assert.Equal(a.Value, b.Value);
        }

        [Fact]
        public void OrderMatters()
        {
            var a = Hash.Create(); a.Add(1); a.Add(2);
            var b = Hash.Create(); b.Add(2); b.Add(1);
            Assert.NotEqual(a.Value, b.Value);
        }

        [Fact]
        public void EmptyHash_IsTheFnvOffsetBasis()
        {
            Assert.Equal(14695981039346656037UL, Hash.Create().Value);
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `Rng` and `Hash` do not exist.

- [ ] **Step 3: Implement both**

`Rng` is **xorshift128+**, seeded by splitting the 64-bit seed into two non-zero state words — a zero state is the algorithm's degenerate case and produces zeros forever. `NextInt` must avoid modulo bias.

`Hash` is FNV-1a with offset basis `14695981039346656037` and prime `1099511628211`, folding each `long` byte by byte.

Neither may allocate, use `System.Math`, or read wall-clock time. The banned-API analyzer will tell you if you try.

- [ ] **Step 4: Run until green**

```bash
dotnet test Broodline.sln
```

Expected: **13 passed**.

- [ ] **Step 5: Commit**

```bash
git add engine/ tests/engine/
git commit -m "feat: seeded xorshift128+ RNG and FNV-1a state hashing

One RNG stream per simulation, seeded by the server, passed explicitly
rather than held ambiently so a stray call cannot silently consume a
draw. Zero state is the algorithm's degenerate case and is guarded at
construction.

Hash is order-sensitive by design: it is how two runs of the same
simulation are compared, and a reordered tick is a different simulation."
```

---

### Task 6: The toy simulation and the four test layers

The smallest simulation that can drift, so the harness can be proven to catch drift before there is any gameplay to debug.

**Files:**
- Create: `engine/Runtime/ToySim.cs`
- Create: `tests/engine/ToySimTests.cs`, `tests/engine/FuzzTests.cs`

**Interfaces:**
- Produces:
  - `struct ToyInput { public int Tick; public int EntityId; }`
  - `static class ToySim` with `static ulong Run(ulong seed, int entityCount, ToyInput[] inputs, int ticks)` returning the terminal state hash, and `static ulong RunToTick(ulong seed, int entityCount, ToyInput[] inputs, int ticks, int checkpointEvery, System.Collections.Generic.List<ulong> checkpoints)`

- [ ] **Step 1: Write the failing tests**

`tests/engine/ToySimTests.cs`:

```csharp
using System.Collections.Generic;
using Xunit;

namespace Broodline.Sim.Tests
{
    public class ToySimTests
    {
        [Fact]
        public void SameSeedAndInputs_ProduceTheSameHash()
        {
            var inputs = new[] { new ToyInput { Tick = 5, EntityId = 1 } };
            Assert.Equal(ToySim.Run(7, 4, inputs, 90), ToySim.Run(7, 4, inputs, 90));
        }

        [Fact]
        public void DifferentSeed_ProducesADifferentHash()
        {
            var inputs = new ToyInput[0];
            Assert.NotEqual(ToySim.Run(1, 4, inputs, 90), ToySim.Run(2, 4, inputs, 90));
        }

        [Fact]
        public void InputChangesTheOutcome()
        {
            var none = new ToyInput[0];
            var one = new[] { new ToyInput { Tick = 3, EntityId = 0 } };
            Assert.NotEqual(ToySim.Run(9, 4, none, 60), ToySim.Run(9, 4, one, 60));
        }

        [Fact]
        public void CheckpointsAreStableAcrossRuns()
        {
            var inputs = new[] { new ToyInput { Tick = 10, EntityId = 2 } };
            var a = new List<ulong>();
            var b = new List<ulong>();
            ToySim.RunToTick(3, 5, inputs, 128, 64, a);
            ToySim.RunToTick(3, 5, inputs, 128, 64, b);
            Assert.Equal(a, b);
            Assert.NotEmpty(a);
        }

        [Fact]
        public void GoldenHash_IsPinned()
        {
            // A change here is a deliberate simulation change and invalidates every
            // stored replay. If this fails unexpectedly, something drifted.
            var inputs = new[] { new ToyInput { Tick = 5, EntityId = 1 } };
            ulong actual = ToySim.Run(7, 4, inputs, 90);
            Assert.Equal(GoldenValue, actual);
        }

        // Replaced with the observed value in Step 4.
        const ulong GoldenValue = 0UL;
    }
}
```

`tests/engine/FuzzTests.cs`:

```csharp
using Xunit;

namespace Broodline.Sim.Tests
{
    public class FuzzTests
    {
        [Fact]
        public void RandomScenarios_AreSelfConsistent()
        {
            // Catches intra-runtime nondeterminism — unordered iteration,
            // uninitialised memory — without needing a second platform.
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var gen = new Rng(seed);
                int entities = 1 + gen.NextInt(8);
                int ticks = 30 + gen.NextInt(120);
                int inputCount = gen.NextInt(6);

                var inputs = new ToyInput[inputCount];
                for (int i = 0; i < inputCount; i++)
                    inputs[i] = new ToyInput
                    {
                        Tick = gen.NextInt(ticks),
                        EntityId = gen.NextInt(entities)
                    };

                ulong first = ToySim.Run(seed, entities, inputs, ticks);
                ulong second = ToySim.Run(seed, entities, inputs, ticks);
                Assert.Equal(first, second);
            }
        }
    }
}
```

- [ ] **Step 2: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `ToySim` and `ToyInput` do not exist.

- [ ] **Step 3: Implement the toy simulation**

Requirements, all of which exist to exercise a rule from the Global Constraints:

- Entities are **dense arrays** — parallel `Fix64[]` for a health-like value and a position-like value, `int[]` for ids. No `Dictionary`, no `List` inside the tick.
- **Fixed timestep.** A tick advances position by a `Fix64` constant; nothing reads wall-clock time.
- **Inputs apply at their tick index**, matched by scanning an array sorted by `(Tick, EntityId)` — a total order, per the Global Constraints.
- **The RNG is passed explicitly** into the tick, never held statically.
- Each tick folds every entity's state into a `Hash` **in ascending id order**.
- **No allocation inside the tick loop.** Allocate the arrays once in `Run`.

Keep it under about 80 lines. It is a test fixture, not a game.

- [ ] **Step 4: Pin the golden hash**

Run the suite. `GoldenHash_IsPinned` fails and reports the actual value. Replace `GoldenValue` with it, and add above the constant:

```csharp
        // Observed on first implementation. Changing the simulation changes this
        // value; that is the point. An unexplained change is drift.
```

Re-run: **19 passed**.

- [ ] **Step 5: Prove the golden test actually catches drift**

Temporarily change a constant inside `ToySim` — a movement step, say. Run the suite.

Expected: `GoldenHash_IsPinned` **FAILS** with a different value; `RandomScenarios_AreSelfConsistent` still passes, because the change is deterministic.

That is the distinction the two layers exist to draw: the golden test catches *change*, the fuzz test catches *nondeterminism*. Revert the constant and confirm green.

- [ ] **Step 6: Commit**

```bash
git add engine/ tests/engine/
git commit -m "test: toy simulation with golden and fuzz layers

The smallest simulation that can drift, so the harness is proven before
there is gameplay to debug.

Golden pins a known hash and catches deliberate or accidental change.
Fuzz runs 200 random scenarios twice in-process and catches
nondeterminism — unordered iteration, uninitialised memory — without a
second platform. Verified by changing a movement constant and watching
golden fail while fuzz stayed green."
```

---

### Task 7: The cross-runtime CI gate

The reason the whole plan exists: proving CoreCLR and IL2CPP agree.

**Files:**
- Create: `client/Assets/Editor/DeterminismHarness.cs`
- Create: `implementation/scripts/cross-runtime-diff.sh`
- Create: `.github/workflows/determinism.yml`

**Interfaces:**
- Consumes: `ToySim.Run` from Task 6.
- Produces: a script exiting non-zero when the two runtimes disagree, and a workflow running it on a self-hosted macOS runner.

- [ ] **Step 1: Write the corpus runner as shared code**

`engine/Runtime/Corpus.cs` — a deterministic set of scenarios generated from a seed, so both runtimes run identical work without shipping a data file:

```csharp
namespace Broodline.Sim
{
    public static class Corpus
    {
        /// Generates scenario N deterministically and returns its terminal hash.
        /// Both runtimes must produce identical output for every index.
        public static ulong RunScenario(int index)
        {
            var gen = new Rng((ulong)(index + 1));
            int entities = 1 + gen.NextInt(8);
            int ticks = 30 + gen.NextInt(120);
            int inputCount = gen.NextInt(6);
            var inputs = new ToyInput[inputCount];
            for (int i = 0; i < inputCount; i++)
                inputs[i] = new ToyInput
                {
                    Tick = gen.NextInt(ticks),
                    EntityId = gen.NextInt(entities)
                };
            return ToySim.Run((ulong)(index + 1), entities, inputs, ticks);
        }
    }
}
```

Add a test asserting `RunScenario(0)` is stable across two calls, then commit this before continuing.

- [ ] **Step 2: Emit the CoreCLR side**

Add to `tests/engine/` a test that writes every scenario hash to a file when an environment variable is set, so the same suite serves both purposes:

```csharp
        [Fact]
        public void EmitCorpusHashes()
        {
            var path = System.Environment.GetEnvironmentVariable("BROODLINE_CORPUS_OUT");
            if (string.IsNullOrEmpty(path)) return;   // ordinary runs skip this

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 500; i++)
                sb.AppendLine(i + "," + Corpus.RunScenario(i));
            System.IO.File.WriteAllText(path, sb.ToString());
        }
```

- [ ] **Step 3: Emit the IL2CPP side from Unity**

`client/Assets/Editor/DeterminismHarness.cs` — a `[MenuItem("Broodline/Emit Corpus Hashes")]` plus a static method callable via `-executeMethod`, writing the identical 500 lines to a path from the command line. It calls the same `Corpus.RunScenario`, because Unity compiles the same source.

- [ ] **Step 4: Write the diff script**

`implementation/scripts/cross-runtime-diff.sh`:

```bash
#!/usr/bin/env bash
# Runs the corpus on CoreCLR and on Unity, and diffs the hashes.
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
OUT="implementation/results"
mkdir -p "$OUT"

echo "--- CoreCLR ---"
BROODLINE_CORPUS_OUT="$(pwd)/$OUT/corpus-coreclr.txt" \
  dotnet test Broodline.sln --filter EmitCorpusHashes >/dev/null || { echo "FAIL: dotnet"; exit 1; }

echo "--- Unity / IL2CPP ---"
"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod DeterminismHarness.EmitCorpus \
  -corpusOut "$(pwd)/$OUT/corpus-unity.txt" \
  -logFile "$(pwd)/$OUT/corpus-unity.log" || { echo "FAIL: unity (editor open?)"; exit 1; }

echo "--- diff ---"
if diff -u "$OUT/corpus-coreclr.txt" "$OUT/corpus-unity.txt" > "$OUT/corpus-diff.txt"; then
  echo "PASS: $(wc -l < "$OUT/corpus-coreclr.txt" | tr -d ' ') scenarios agree"
  exit 0
else
  echo "FAIL: runtimes disagree. First differences:"
  head -20 "$OUT/corpus-diff.txt"
  exit 1
fi
```

- [ ] **Step 5: Run it and see it pass**

```bash
chmod +x implementation/scripts/cross-runtime-diff.sh
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first.

**This step is the entire point of Phase 1.** If it fails, do not proceed — read the first differing scenario index and reproduce it in isolation.

- [ ] **Step 6: Prove the diff catches disagreement**

Temporarily make `DeterminismHarness` emit a wrong value for scenario 250 — add 1 to it. Re-run.

Expected: **FAIL**, with the diff naming line 251. Revert and confirm `PASS` again.

- [ ] **Step 7: Add the workflow**

`.github/workflows/determinism.yml`, running on `[self-hosted, macOS]` nightly and on pushes touching `engine/**` or `tests/engine/**`, executing `dotnet test` and then `cross-runtime-diff.sh`.

Note in the file that it requires a self-hosted runner: GitHub-hosted macOS bills at a 10× multiplier and has no Unity licence, per `broodline_solo_execution.md` §7.3.

- [ ] **Step 8: Commit**

```bash
git add engine/ tests/engine/ client/Assets/Editor/DeterminismHarness.cs implementation/scripts/cross-runtime-diff.sh .github/
git commit -m "ci: cross-runtime determinism gate

Runs 500 generated scenarios on CoreCLR and on Unity's IL2CPP and diffs
the hashes. This is the gate the whole phase exists for: the engine
compiles twice and must agree, and drift is a trust failure that
surfaces as players believing the game cheats.

The corpus is generated from seeds rather than stored, so both runtimes
run identical work with no data file to drift out of sync. Verified by
perturbing one scenario and watching the diff name it."
```

---

## What this plan deliberately does not do

- **No gameplay.** No raiders, traits, counters, tick phases, capacity or breach diagnosis. Phase 2 builds those against `broodline_combat_engine.md`, and doing it here would mean debugging game rules and harness defects at the same time.
- **No replay format.** It needs the real simulation's shape. `ToySim` proves hashing and determinism only.
- **No iOS device run.** The IL2CPP side runs as a macOS ARM64 build. `broodline_solo_execution.md` §7.3 is explicit that this is a strong proxy and not the iOS binary, closed by running the corpus on a physical device before releases.

## Definition of done

`./implementation/scripts/cross-runtime-diff.sh` prints `PASS: 500 scenarios agree` and exits 0, with `dotnet test Broodline.sln` green and the Unity suite unbroken.
