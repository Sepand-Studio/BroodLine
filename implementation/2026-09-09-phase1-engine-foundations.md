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
- **No `#if UNITY_*` regions in `engine/Runtime/`** (vendored `ThirdParty/` excepted). The csproj glob and the asmdef cover the same *files*, but a Unity-conditional region means they do not cover the same *code*: Unity compiles it and the .NET build excludes it, so the banned-API analyzer, the IL float scan and `dotnet build` itself are all blind to something that still ships in the player. Added in fix round 2 and enforced by `EnforcementTests.SimulationCore_HasNoUnityConditionalCompilation`, which makes "both compilers see the same thing" a checked premise rather than an assumption.

**This plan writes no gameplay.** No raiders, no traits, no counters, no tick phases. The toy simulation exists only to prove the harness catches drift.

---

### Task 0: Prerequisites

**Files:**
- Modify: `implementation/scripts/verify-prereqs.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: `verify-prereqs.sh` additionally asserting a .NET SDK.

- [x] **Step 1: Install the .NET SDK**

```bash
brew install --cask dotnet-sdk
```

- [x] **Step 2: Confirm it resolves**

```bash
dotnet --list-sdks
```

Expected: at least one SDK line. If `dotnet` is not on PATH afterwards, open a new shell — the cask installs to `/usr/local/share/dotnet` and adds a symlink that an existing shell will not see.

- [x] **Step 3: Add the check to the prerequisites script**

Insert before the Unity block in `implementation/scripts/verify-prereqs.sh`:

```bash
if dotnet --list-sdks >/dev/null 2>&1; then
  ok ".NET SDK $(dotnet --list-sdks | head -1 | awk '{print $1}')"
else
  bad ".NET SDK missing — brew install --cask dotnet-sdk"
fi
```

- [x] **Step 4: Run it**

```bash
./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: four `ok` lines and `exit=0`.

- [x] **Step 5: Commit**

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

- [x] **Step 1: Create the UPM manifest**

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

- [x] **Step 2: Create the assembly definition**

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

**Folded back (fix round 2, Finding 3):** nothing checked either field. `dotnet build` never reads an asmdef, so flipping `noEngineReferences` to `false` or adding an entry to `references` left the build green, every test passing and the cross-runtime gate printing `PASS` — while Unity happily linked `UnityEngine` into the simulation assembly and made `UnityEngine.Random`, `Time.deltaTime` and `Mathf` reachable from the tick loop. `tests/engine/EnforcementTests.EngineAsmdef_StillHasNoUnityReferencesAtAll` now parses this file and asserts both: `references` present and empty, `noEngineReferences` present and `true`. It asserts the keys are *present*, not merely not-false — an absent `noEngineReferences` defaults to `false` in Unity, which is the opposite of the promise.

- [x] **Step 3: Create the .NET project**

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

- [x] **Step 4: Add the proof type**

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

- [x] **Step 4b: Keep .NET's build output invisible to Unity**

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

- [x] **Step 5: Create the solution**

The .NET 10 SDK defaults `dotnet new sln` to the newer `.slnx` format; `--format sln` produces the classic `Broodline.sln` this plan refers to throughout.

```bash
cd "$(git rev-parse --show-toplevel)"
dotnet new sln --name Broodline --format sln
dotnet sln Broodline.sln add engine/Broodline.Sim.csproj
dotnet build Broodline.sln
```

Expected: `Build succeeded`, zero warnings, zero errors.

- [x] **Step 6: Point Unity at the same source**

Add to the `dependencies` object in `client/Packages/manifest.json`:

```json
    "com.sepandstudio.broodline.sim": "file:../../engine",
```

The path resolves relative to `client/Packages/`, so `../../engine` is the repository root's `engine/`.

- [x] **Step 7: Prove Unity compiles it too**

Close the Unity editor, then:

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: **`total=7 passed=7 failed=0`**, exit 0 — Phase 0's suite, unchanged. A compile error inside the package fails the whole run, so a green suite is the proof that Unity resolved and compiled `engine/`.

If Unity reports the package cannot be found, the relative path is wrong. If it reports `noEngineReferences` as unknown, the asmdef schema differs in this Unity version — read the error and fix the field rather than deleting it.

- [x] **Step 8: Commit**

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

- [x] **Step 1: Create the test project**

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

- [x] **Step 2: Write the failing test**

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

        // Compare the namespace with a real boundary. Prefix-matching FullName
        // would also skip a type named e.g. FixPointCSHelper sitting in our own
        // namespace — defeating the exclusion's whole point.
        static bool IsVendored(TypeDefinition t) =>
            t.Namespace == VendoredNamespace ||
            t.Namespace.StartsWith(VendoredNamespace + ".", StringComparison.Ordinal);

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
                        // Conversions matter as much as literals: casting on the IL
                        // stack — (int)((float)a / (float)b) — materialises no float
                        // local, field, parameter or literal and would evade everything
                        // else here.
                        if (i.OpCode == OpCodes.Ldc_R4 || i.OpCode == OpCodes.Ldc_R8 ||
                            i.OpCode == OpCodes.Conv_R4 || i.OpCode == OpCodes.Conv_R8 ||
                            i.OpCode == OpCodes.Conv_R_Un)
                            offenders.Add($"float op {type.FullName}.{m.Name} : {i.OpCode}");
                }
            }

            Assert.True(offenders.Count == 0,
                "floating point in the simulation core:\n  " + string.Join("\n  ", offenders));
        }

        // Must recurse. float[] has MetadataType.Array, not Single — and arrays are
        // the one collection type these constraints permit, so a bare outer-type
        // check is the likeliest evasion of all.
        static bool IsFloat(TypeReference t)
        {
            if (t == null) return false;
            if (t.MetadataType == MetadataType.Single || t.MetadataType == MetadataType.Double)
                return true;
            if (t is ArrayType a) return IsFloat(a.ElementType);
            if (t is ByReferenceType r) return IsFloat(r.ElementType);
            if (t is PointerType p) return IsFloat(p.ElementType);
            if (t is GenericInstanceType g)
            {
                foreach (var arg in g.GenericArguments)
                    if (IsFloat(arg)) return true;
            }
            return false;
        }
    }
}
```

- [x] **Step 3: Add the project and run — the test must PASS**

```bash
cd "$(git rev-parse --show-toplevel)"
dotnet sln Broodline.sln add tests/engine/Broodline.Sim.Tests.csproj
dotnet test Broodline.sln
```

Expected: **1 passed**. `SimVersion` holds only a string constant, so there is nothing to find yet.

A passing test proves nothing on its own here — Step 4 is what proves the scan works.

- [x] **Step 4: Prove the scan actually catches a violation**

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

Run **three** tripwires, not one — the first version of this scan passed all of these while being blind to every one:

| Tripwire | Must fail naming |
|---|---|
| `public static float[] T1 = new float[4];` | a `Single[]` field |
| `public static int T2(long a, long b) => (int)((float)a / (float)b);` | `conv.r4` |
| `public class FixPointCSHelper { public static float S() => 2.5f; }` | `FixPointCSHelper` — proving the vendored exclusion is namespace-bounded, not a prefix match |

- [x] **Step 5: Remove the tripwire and confirm green**

Delete the `Tripwire` property, then:

```bash
dotnet test Broodline.sln
```

Expected: **1 passed**.

**Folded back (fix round 2, Critical 1): the scan also inspects instruction OPERANDS, not only opcodes.**

As first written the body scan looked at `i.OpCode` and never at `i.Operand`, so float arriving through a call slipped straight past it:

```csharp
public int T() => (int)FixPointCS.Fixed64.ToDouble(Raw);
```

compiles to `call float64 …::ToDouble(int64)` followed by `conv.i4`. None of `Ldc_R4`, `Ldc_R8`, `Conv_R4`, `Conv_R8` or `Conv_R_Un` appears anywhere. This was verified, not reasoned about: that exact method was added to `SimVersion` and the original scan **passed**. `FixPointCS` ships `ToDouble`/`FromDouble`/`ToFloat`/`FromFloat`, and `System.BitConverter` is not on the banned list, so this is a reachable route rather than a hypothetical one — and it is precisely the route someone reaches for to work around `Mul`'s documented silent overflow, at which point `Fix64.cs`'s header promise ("no float or double may appear anywhere in this file — not in a signature, not in a body, not in a cast") would be false with nothing to catch it.

The scan now flags three operand kinds inside non-vendored types:

| Operand | Flagged when |
|---|---|
| `IMethodSignature` (covers `MethodReference`, `GenericInstanceMethod`, and `calli`'s `CallSite`) | the return type, any parameter type, or any generic argument is float |
| `FieldReference` | the field type is float |
| `TypeReference` (`newarr`, `box`, `ldtoken`, `castclass`) | the type is float |

The `IsVendored` guard already skips vendored types wholesale, so FixPointCS's own internal calls to its own float helpers stay exempt while a call **into** them from our code does not — the exclusion stays namespace-bounded exactly as Step 4's third tripwire requires.

Add a fourth tripwire to Step 4's table and watch it fail before removing it:

| Tripwire | Must fail naming |
|---|---|
| `public static int T4(long raw) => (int)FixPointCS.Fixed64.ToDouble(raw);` | `float via call Broodline.Sim.SimVersion.T4 -> System.Double FixPointCS.Fixed64::ToDouble(System.Int64)` |

- [x] **Step 6: Commit**

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

- [x] **Step 1: Write the banned list**

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
T:System.Threading.Tasks.Task`1;as above.
T:System.Threading.Tasks.ValueTask;as above.
T:System.Threading.Tasks.ValueTask`1;as above.
T:System.Threading.Tasks.Parallel;no parallelism inside a tick. Parallel.For/ForEach over a per-entity loop is the likeliest way it comes back.
T:System.Threading.Thread;no parallelism inside a tick.
T:System.Diagnostics.Stopwatch;ambient wall-clock by another name. Time in the core is tickIndex; a Stopwatch reading cannot be replayed.
T:System.Runtime.CompilerServices.RuntimeHelpers;GetHashCode here is the reference-identity hash — per-process and allocation-order dependent, the same hazard String.GetHashCode is banned for.
T:System.Collections.Hashtable;hash iteration order differs across runtimes, as Dictionary`2 does, and this one predates generics so it is easy to reach for by accident.
T:System.Collections.Concurrent.ConcurrentDictionary`2;hash iteration order, plus concurrency the tick loop must not have. Arity 1 does not exist; the arity-2 DocID is the one that binds.
```

Each entry is `symbol;reason`, and the reason appears in the build error — which is the difference between a developer understanding the rule and working around it.

`BannedApiAnalyzers` matches on the exact DocID, and generic arity is part of it: an arity-0 entry for `Task` does not bind `Task<TResult>`, and the same applies to `ValueTask`/`ValueTask<TResult>`. Both arities are listed for each type for that reason, matching the existing arity-explicit notation used for `` Dictionary`2 `` and `` HashSet`1 `` above. `Parallel` is listed separately since it is not a `Task`/`ValueTask` arity variant — it is the static parallel-loop helper, and a per-entity `Parallel.For` is the most likely way parallelism gets reintroduced inside a tick.

**Folded back (fix round 2, Finding 5):** the last four entries close two half-bans. Ambient time was banned as `DateTime`/`DateTimeOffset` but not as `Stopwatch`, which is the same wall-clock under a different name. Identity hashing was banned as `String.GetHashCode` — randomised per process on CoreCLR, not on Mono — but `RuntimeHelpers.GetHashCode` is the reference-identity hash and carries the identical per-process hazard. `Hashtable` and `ConcurrentDictionary`2` were the two remaining order-dependent collections outside the `Dictionary`2`/`HashSet`1` pair. All four were verified to fire with their reasons by compiling each against the analyzer.

**Folded back (fix round 2, Finding 3): this list is now itself enforced.** Deleting the `AdditionalFiles` line, the analyzer `PackageReference`, or `RS0030` from `WarningsAsErrors` used to leave the build green, every test passing and the gate printing `PASS` — the banned list simply stopped being read. `tests/engine/EnforcementTests.EngineCsproj_StillWiresTheBannedApiAnalyzer` parses the csproj as XML (not as text — a commented-out line still contains the text) and asserts all three, and `BannedSymbols_StillListsTheSymbolsTheCoreCannotUse` asserts a representative slice of the entries above, each matched with its trailing `;` so an arity-0 entry cannot satisfy an assertion about an arity-1 type.

- [x] **Step 2: Wire the analyzer**

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

- [x] **Step 3: Confirm the build is still clean**

```bash
dotnet build Broodline.sln
```

Expected: `Build succeeded`, zero warnings.

- [x] **Step 4: Prove the ban actually fires**

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

- [x] **Step 5: Remove the tripwire, confirm green, commit**

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

- [x] **Step 1: Vendor FixPointCS**

Obtain the FixPointCS sources (MIT) and place the fixed-point implementation files plus the licence under `engine/Runtime/ThirdParty/FixPointCS/`. **Preserve the licence header in every file and keep `LICENSE` alongside them.** Record in your report which files and which revision you took.

Vendoring rather than a package reference is deliberate: the engine has no dependencies, and Unity consumes this as source.

Task 1 set `TreatWarningsAsErrors`, and third-party sources routinely carry warnings that our own code would not. If the build fails here, do **not** turn the flag off — suppress narrowly instead, scoped to the vendored directory:

```xml
  <ItemGroup>
    <Compile Update="Runtime/ThirdParty/**/*.cs">
      <NoWarn>$(NoWarn);CS0219;CS0414;CS1591</NoWarn>
    </Compile>
  </ItemGroup>
```

Add only the warning codes the build actually reports, and record them in your report. A blanket suppression across the whole project would hide warnings in the simulation code, which is where they matter most.

At the vendored revision pinned for this task (`a852f05b428a942f8dc274ee516a893ae224e0d4`), the build did not in fact fail: `dotnet build engine/Broodline.Sim.csproj -t:Rebuild -v normal` shows both vendored files compiling under `/warnaserror+` (BannedApiAnalyzers loaded, `/warnaserror+:NU1605,RS0030`) with 0 warnings and 0 errors, so no suppression block was added. The guidance above still stands for whichever future revision of the vendored sources first introduces one.

- [x] **Step 2: Confirm the exclusion in Task 2's scan is doing real work**

```bash
dotnet test Broodline.sln --filter SimulationCore_ContainsNoFloatingPoint
```

Expected: **1 passed**. If it fails naming a `FixPointCS.*` type, the namespace of the vendored code differs from the `VendoredNamespace` constant — update the constant to the real namespace. **Do not widen it to a prefix that would also cover `Broodline.Sim`.**

- [x] **Step 3: Write the failing tests**

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

**Fix round 1 extended this file.** The five tests above exercise only about half the mandated API surface — `FromRaw`, `==`, `!=`, `<=`, `>`, `>=`, `Equals(Fix64)`, `Equals(object)`, `GetHashCode`, and `CompareTo` were untested, so a transposed comparator (`>=` written as `a.Raw <= b.Raw`, say) would have left this suite green. Separately, `DivPrecise`'s overflow guard and `SqrtPrecise`'s negative-input path have real edge-case behaviour (saturation to `long.MaxValue` regardless of sign; silently returning `Zero`) that was neither documented nor pinned, and `Mul`/`DivPrecise` round in different directions (floor vs. truncate-toward-zero) with no test distinguishing them. The human ruling for that round was: document the contract on `Fix64` itself (see the type's header comment) and pin it with tests, but make no behavioural change to `Fix64` — the wrapper stays a thin delegation to the vendored implementation.

Eleven tests were added to `Fix64Tests.cs`, split into two clearly-labelled groups:

*Contract-pinning tests* (prefixed `Pinned_`; snapshot today's inherited behaviour on purpose — a failure means the behaviour changed, which is a decision to revisit, not a bug to chase):
- `Pinned_DivideByZero_PositiveDividend_SaturatesToLongMaxValue` and `Pinned_DivideByZero_NegativeDividend_AlsoSaturatesToLongMaxValue` — `FromInt(5) / Zero` and `FromInt(-5) / Zero` both give `Raw == long.MaxValue`; the sign of the dividend is never reapplied on the overflow path.
- `Pinned_Sqrt_OfNegative_ReturnsZeroRatherThanThrowing` — `Sqrt(FromInt(-4)) == Zero`, no exception.
- `Pinned_Multiply_FloorsTowardNegativeInfinity` — `FromRaw(-1) * FromRaw(3)` gives `Raw == -1` (floors), not `0` (which truncate-toward-zero would give).
- `Pinned_Divide_TruncatesTowardZero` — `FromInt(-1) / FromInt(3)` gives `Raw == -1431655765` (truncates toward zero), not `-1431655766` (which flooring, as `Mul` does, would give).

*Property tests* (assert invariants that must hold for any `Fix64` value — a failure here is a real defect): `ComparisonOperators_AgreeWithSignOfCompareTo`, `CompareTo_IsAntisymmetric`, and `CompareTo_IsTransitive` sweep all pairs/triples of a fixed `SampleRaws` array (`long.MinValue`, `long.MinValue + 1`, values either side of `±(1L << 32)`, `-1`, `0`, `1`, and `long.MaxValue - 1`, `long.MaxValue`) checking `<`, `<=`, `>`, `>=`, `==`, `!=` all agree with the sign of `CompareTo`, and that `CompareTo` is antisymmetric and transitive across that set — deliberately including `long.MinValue`, whose negation overflows, as the edge most likely to break a hand-rolled comparator. `FromRaw_RoundTripsThroughRaw` asserts `FromRaw(r).Raw == r` over the same set. `Equality_AgreesAcrossEqualsAndOperatorsAndHashCode` asserts `Equals(Fix64)`, `Equals(object)`, `==`/`!=` all agree over every sampled pair, and that equal values produce equal `GetHashCode()`. `Equals_Object_RejectsNullAndOtherTypes` covers `Equals(object)` against `null`, a string, and a boxed `int`.

All eleven values above were taken from an actual `dotnet test` run, not derived by hand and assumed — see Step 6 below.

**Fix rounds 2 and 3 corrected that contract, which was wrong about signs.** Round 1 wrote the third bullet of the header comment as a pair of universals — `(-a) / b == -(a / b)` always holds; `(-a) * b == -(a * b)` fails only when the exact product has a remainder below 2⁻³². Round 2 narrowed the division half to exclude saturation, and both claims were still false. The root cause neither round named: **`Fix64` exposes no unary minus.** Negation has to be written `Fix64.Zero - a`, which is `Fixed64.Sub`'s plain unchecked `0 - a.Raw`, so at `a.Raw == long.MinValue` it wraps and returns `a` itself — negation is the *identity* there. Every "negating an operand negates the result" identity therefore fails at that one value, for reasons having nothing to do with saturation or rounding. Round 3 rewrote the list to state that fact once, up front, and to let the identity claims reference it instead of each carrying its own exception list, then restated each identity with the condition under which it actually holds: division's when the division does not saturate, multiplication's when the exact product `a.Raw * b.Raw` is a whole multiple of 2³².

Round 3 also closed two gaps the contract had never covered:

- **`Mul` has no overflow guard at all.** Where `DivPrecise` at least saturates, `Mul` is plain unchecked `long` arithmetic and wraps silently to an unrelated value, with no saturation and no exception — `FromInt(int.MaxValue) * FromInt(int.MaxValue)` returns the real value `1` for a true product of about 4.6·10¹⁸. The contract had documented `Mul`'s rounding *direction* while documenting division's overflow behaviour, an asymmetry a reader would naturally misread as symmetry.
- **`DivPrecise`'s guard is not a "does it fit" test.** It is `(|a.Raw| >> 32) >= |b.Raw|`, which fires only once the exact quotient's raw magnitude reaches 2⁶⁴ — twice what a `long` holds. A quotient landing between `long.MaxValue` and 2⁶⁴ slips past it and is wrapped rather than saturated, often with the sign flipped: `FromRaw(long.MaxValue) / FromRaw(0xFFFFFFFF)` divides a positive by a positive and returns raw `-9223372034707292161`. This made round 1's first bullet ("any quotient whose magnitude overflows a `long` saturates") a third false universal, so round 3 corrected it too.

Five more `Pinned_` tests were added for these facts, each verified against the vendored source and against a measured run before being written down: `Pinned_Negation_IsIdentityAtLongMinValue`; `Pinned_DivisionSignIdentity_FailsAtLongMinValue_WithoutSaturating` (with a contrast at `long.MinValue + 1`, where the same divisor and the same non-saturating path make the identity hold); `Pinned_MultiplicationSignIdentity_FailsAtLongMinValue_WithNoRemainderDiscarded` (the product is exactly −1.5, so no remainder is discarded, and the identity fails anyway); `Pinned_Multiply_OverflowsSilentlyWithoutSaturating`; and `Pinned_Divide_QuotientAboveLongMaxValueWrapsInsteadOfSaturating`, which pins the guard's threshold to the raw unit — divisor raw 2³¹−1 saturates, divisor raw 2³¹ wraps to `-2`.

- [x] **Step 4: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `Fix64` does not exist yet.

- [x] **Step 5: Write the wrapper**

`engine/Runtime/Fix64.cs` — a `readonly struct` over a single `long` raw value, delegating arithmetic to the vendored implementation.

**Wrap rather than expose FixPointCS directly.** The engine depends on `Broodline.Sim.Fix64`, so replacing the implementation later touches one file instead of every call site. The wrapper is also where the no-float boundary sits: it must expose no `float` or `double` in any signature, or Task 2's scan fails.

- [x] **Step 6: Run until green**

```bash
dotnet test Broodline.sln
```

Expected at this task's original implementation: **7 passed** — the two pre-existing determinism tests (`SimulationCore_ContainsNoFloatingPoint` and `IsFloat_RecursesIntoArrayElementType`, the latter added during Task 2's fix round) plus the five here.

**Fix round 1 raised this to 18 passed** — the same two determinism tests plus sixteen in `Fix64Tests.cs` (the original five plus the eleven described in Step 3 above: five contract-pinning, six property). Confirmed by an actual `dotnet test Broodline.sln` run, not computed by hand.

**Fix round 3 raised this to 23 passed** — the same two determinism tests plus twenty-one in `Fix64Tests.cs` (round 1's sixteen plus the five described in Step 3 above). Round 2 changed only the header comment, so it left the count at 18. Taken from an actual `dotnet test Broodline.sln` run, not computed by hand.

- [x] **Step 7: Record the vendored provenance** *(added in fix round 2)*

**Folded back (fix round 2, Finding 8):** the vendored tree carried the licence and per-file headers but recorded nothing about where the code came from, so "is this current?", "has anyone edited it?" and "what would upgrading involve?" had no answer short of guesswork. `engine/Runtime/ThirdParty/FixPointCS/PROVENANCE.md` now names the upstream repository (`https://github.com/XMunkki/FixPointCS`), the pinned revision `a852f05b428a942f8dc274ee516a893ae224e0d4`, the three files taken (`Fixed64.cs`, `FixedUtil.cs`, `LICENSE.txt` — not the test suite, generators, `Fixed32.cs` or `F64.cs`), their SHA-256 checksums, and a copy-pasteable re-verification command. Byte-identity to upstream was verified rather than asserted: all three files were fetched at the pinned revision and compared with `cmp`, and all three match exactly. **The vendored sources themselves must not be edited** — that byte-identity is what makes an upgrade a clean replace-and-re-diff instead of an archaeology exercise, and it is why the wrapper documents the edge-case contract rather than patching it upstream-side.

- [x] **Step 8: Commit**

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

**Folded back (fix round 2, deferred ledger):** `Rng`'s doc comment attributed xorshift128+ to "Vigna & Blackman, 2014". The 2014 xorshift+ paper (*Further scramblings of Marsaglia's xorshift generators*, arXiv:1404.0390) is **Vigna's alone**; Blackman is his co-author on the later xoshiro/xoroshiro family, which is a different construction and not what this implements. Corrected in place. The citation is load-bearing rather than decorative: the algorithm is a pinned part of the replay format, and the reference is how a future reader checks the shift triple against its source.

- [x] **Step 1: Write the failing tests**

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
        public void NextInt_NonPositiveBound_ReturnsZeroWithoutConsumingADraw()
        {
            // exclusiveMax <= 0: the range is empty, so NextInt must return 0
            // *and* must not advance the stream. Proven here by observing the
            // stream position, not just the return value: if a draw had been
            // consumed, r's next NextULong() would diverge from a fresh Rng's
            // first draw.
            var r = new Rng(42);
            Assert.Equal(0, r.NextInt(0));
            Assert.Equal(0, r.NextInt(-100));

            var fresh = new Rng(42);
            Assert.Equal(fresh.NextULong(), r.NextULong());
        }

        [Fact]
        public void NextInt_One_ReturnsZeroAndConsumesExactlyOneDraw()
        {
            // exclusiveMax == 1 is the one-element case, not the degenerate
            // one: it must return 0 (the only value in range) but -- unlike
            // exclusiveMax <= 0 -- it DOES consume exactly one draw. Task 7's
            // Corpus.RunScenario calls NextInt(entityCount) with counts
            // starting at 1, so every corpus stream's alignment depends on
            // this consuming exactly one draw, never zero.
            //
            // Proven by comparing against a fresh Rng with one draw skipped:
            // if NextInt(1) consumed zero draws, r's next draw would match
            // fresh's *first* draw instead; if it consumed two, it would match
            // fresh's *third*.
            var r = new Rng(42);
            Assert.Equal(0, r.NextInt(1));

            var fresh = new Rng(42);
            fresh.NextULong(); // skip the one draw NextInt(1) must have consumed
            Assert.Equal(fresh.NextULong(), r.NextULong());
        }

        [Fact]
        public void KnownSeed_ProducesKnownFirstDraw()
        {
            // Pins the algorithm, not just "seed 1 is reproducible" -- the literal
            // below is xorshift128+'s actual first draw for seed 1 under this
            // type's seed-splitting scheme (two chained SplitMix64 applications;
            // see the constructor's doc comment on Rng), taken verbatim from a
            // real run, never computed by hand. If this changes -- the shift
            // triple, the operation order, or the seed-splitting scheme -- every
            // stored replay is invalid and sim_version must change with it.
            var r = new Rng(1);
            ulong first = r.NextULong();
            Assert.Equal(10993463216891074725UL, first);
            var again = new Rng(1);
            Assert.Equal(first, again.NextULong());
        }
    }
}
```

**Correction folded in:** the brief's original version of this test asserted only `first != 0` and that two `Rng(1)` instances agree with each other — that passes for any PRNG and any seed-splitting scheme, so despite its own comment's claim it pinned nothing. The version above asserts the literal first draw instead. The constant was taken from an actual run, never computed by hand — Step 5 below is the run that proves it actually pins the algorithm.

**Fix round 1 correction folded in:** the version above adds `NextInt_NonPositiveBound_ReturnsZeroWithoutConsumingADraw` and `NextInt_One_ReturnsZeroAndConsumesExactlyOneDraw`. Neither existed before this fix round; only bound 7 was ever exercised, so nothing guarded `NextInt`'s two documented boundary behaviours. The second test is load-bearing: `NextInt`'s doc comment and Task 7's `Corpus.RunScenario` both depend on `NextInt(entityCount)` consuming exactly one draw when `entityCount == 1`, and `if (exclusiveMax <= 1) return 0;` is a natural-looking micro-optimisation that would pass every other test in this file while silently desynchronising every corpus stream one task downstream. Both tests observe stream *position* (by comparing against a fresh `Rng`'s subsequent draws), not just the return value, so a change to draw consumption is caught even though it produces no visible difference in the return value itself. Step 5 below extends the deliberate-break proof to cover both.

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

        [Fact]
        public void Add_PinsKnownFnv1aValues()
        {
            // Pins the algorithm Add() actually performs -- byte order, fold
            // order, and the prime constant -- not just the initial state
            // (EmptyHash_IsTheFnvOffsetBasis only covers that, before any byte
            // is folded). These literals are folds of Add(1), and of Add(1)
            // then Add(2), taken verbatim from a real run of this code, never
            // computed by hand. If either changes, the fold algorithm changed
            // and every previously-recorded hash comparison across runs is
            // invalid.
            var a = Hash.Create();
            a.Add(1);
            Assert.Equal(12161961113530546194UL, a.Value);

            var b = Hash.Create();
            b.Add(1);
            b.Add(2);
            Assert.Equal(17633220212635757292UL, b.Value);
        }
    }
}
```

**Fix round 1 correction folded in:** `Add_PinsKnownFnv1aValues` did not exist before this fix round. The doc comment on `Hash.Add` claims byte order is part of a "pinned" contract, but before this test, nothing pinned it: all three tests above pass unchanged under an LSB-first fold instead of the actual MSB-first one, and `EmptyHash_IsTheFnvOffsetBasis` only pins the *initial* state, before `Add` ever runs. The two literals above were obtained by running this code (via a deliberately-wrong placeholder assertion, then reading the real value off xUnit's failure diff, the same technique Step 5 below uses) — never computed by hand — and independently cross-checked against a from-scratch FNV-1a re-derivation, which agreed on both values. Step 5 below extends the deliberate-break proof to this pin too.

- [x] **Step 2: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `Rng` and `Hash` do not exist.

- [x] **Step 3: Implement both**

`Rng` is **xorshift128+**, seeded by splitting the 64-bit seed into two non-zero state words — a zero state is the algorithm's degenerate case and produces zeros forever. `NextInt` must avoid modulo bias.

`Hash` is FNV-1a with offset basis `14695981039346656037` and prime `1099511628211`, folding each `long` byte by byte.

Neither may allocate, use `System.Math`, or read wall-clock time. The banned-API analyzer will tell you if you try.

- [x] **Step 4: Run until green**

```bash
dotnet test Broodline.sln
```

Expected: **30 passed** (23 existing + 4 in `RngTests` + 3 in `HashTests`). The brief's original "13 passed" was computed before earlier tasks' review rounds added tests to the suite; take the real count from this run, never by hand.

**Fix round 1 raised this to 33 passed** — the same 23 pre-existing tests, plus 6 in `RngTests` (the original 4 plus `NextInt_NonPositiveBound_ReturnsZeroWithoutConsumingADraw` and `NextInt_One_ReturnsZeroAndConsumesExactlyOneDraw`), plus 4 in `HashTests` (the original 3 plus `Add_PinsKnownFnv1aValues`). Confirmed by an actual `dotnet test Broodline.sln` run, not computed by hand.

- [x] **Step 5: Prove the pin actually catches a change**

Every other determinism check in this plan carries a deliberate-break step — Task 2 Step 4, Task 3 Step 4, Task 6 Step 5, Task 7 Step 6 — and this is the one that was missing it. A pin nobody has watched fail is not proven to pin anything.

Temporarily perturb the seed-splitting in `Rng`'s constructor — for example, swap which `SplitMix64` output feeds which state word:

```csharp
_state1 = SplitMix64(ref z);
_state0 = SplitMix64(ref z);
```

Then:

```bash
dotnet test Broodline.sln
```

Expected: **FAIL** — `KnownSeed_ProducesKnownFirstDraw` fails with `Assert.Equal() Failure`, `Expected: 10993463216891074725`, and an `Actual:` that differs (the exact value depends on the perturbation chosen). The other 29 tests still pass; only the pin notices.

**Do not skip this step.** It is the only moment it is cheap to find out the pin is watching the seed-splitting scheme rather than passing by coincidence.

**Fix round 1 extended this step to the two pins it added** — the same principle applies: a pin nobody has watched fail is not proven to pin anything.

*Hash byte order:* temporarily changed `Add`'s fold loop from MSB-first (`for (int shift = 56; shift >= 0; shift -= 8)`) to LSB-first (`for (int shift = 0; shift <= 56; shift += 8)`), then ran `dotnet test Broodline.sln --filter "FullyQualifiedName~HashTests"`:

```
Broodline.Sim.Tests.HashTests.Add_PinsKnownFnv1aValues [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 12161961113530546194
  Actual:   9929646806074584996
Failed!  - Failed: 1, Passed: 3, Skipped: 0, Total: 4
```

Only the new pin fails — `SameInputs_SameHash`, `OrderMatters`, and `EmptyHash_IsTheFnvOffsetBasis` all still pass under the wrong byte order, exactly the gap this test closes. Restored the loop bounds and reconfirmed green.

*`NextInt(1)` draw consumption:* temporarily inserted `if (exclusiveMax <= 1) return 0;` at the top of `NextInt`, then ran `dotnet test Broodline.sln --filter "FullyQualifiedName~RngTests"`:

```
Broodline.Sim.Tests.RngTests.NextInt_One_ReturnsZeroAndConsumesExactlyOneDraw [FAIL]
  Assert.Equal() Failure: Values differ
  Expected: 13639555000553200875
  Actual:   12618900322348487378
Failed!  - Failed: 1, Passed: 5, Skipped: 0, Total: 6
```

Only the new consumption test fails — `NextInt_StaysInRange` and the other four `RngTests` all still pass under the short-circuit, since none of them observe stream position the way this one does. Restored the line and reconfirmed green.

- [x] **Step 6: Restore and confirm green**

Put the constructor back the way Step 3 left it (fix round 1 additionally restored `Hash.Add`'s loop bounds and removed the `NextInt` short-circuit — both diffed clean against the prior commit before proceeding), then:

```bash
dotnet test Broodline.sln
```

Expected: **30 passed** at the original implementation; **33 passed** after fix round 1 (see the corrected count above).

- [x] **Step 7: Commit**

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
  - `static class ToySim` with `static ulong Run(ulong seed, int entityCount, ToyInput[] inputs, int ticks)` returning the hash of the whole run's folded history — every entity's state, every tick — and `static ulong RunToTick(ulong seed, int entityCount, ToyInput[] inputs, int ticks, int checkpointEvery, System.Collections.Generic.List<ulong> checkpoints)`.
    **A whole-run hash, not just a terminal-state one**: it subsumes a terminal-state hash (identical runs still match) and additionally catches divergence that later reconverges. It also makes `RunToTick`'s checkpoints monotone — once two runs differ they differ forever — so a binary search over checkpoints locates the *first* divergent tick; per-tick-fresh snapshots can diverge and reconverge, which makes them invalid to bisect.

- [x] **Step 1: Write the failing tests**

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

            // Folded back (fix round 2, deferred ledger): stability alone is a
            // weak claim — RunToTick appending a CONSTANT every checkpointEvery
            // ticks satisfies every assertion above. 128 ticks at one checkpoint
            // per 64 gives exactly two, taken 64 ticks apart in a simulation
            // that mutates every entity every tick, so they are known distinct.
            // That is what makes "the two runs agree" evidence the checkpoints
            // track the run rather than evidence they are inert.
            Assert.Equal(2, a.Count);
            Assert.NotEqual(a[0], a[1]);
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

- [x] **Step 2: Run and watch them fail**

```bash
dotnet test Broodline.sln
```

Expected: compile error — `ToySim` and `ToyInput` do not exist.

- [x] **Step 3: Implement the toy simulation**

Requirements, all of which exist to exercise a rule from the Global Constraints:

- Entities are **dense arrays** — parallel `Fix64[]` for a health-like value and a position-like value, `int[]` for ids. No `Dictionary`, no `List` inside the tick.
- **Fixed timestep.** A tick advances position by a `Fix64` constant; nothing reads wall-clock time.
- **Inputs apply at their tick index**, matched by scanning an array sorted by `(Tick, EntityId)` — a total order, per the Global Constraints.
- **The RNG is passed explicitly** into the tick, never held statically.
- Each tick folds every entity's state into a `Hash` **in ascending id order**.
- **No allocation inside the tick loop.** Allocate the arrays once in `Run`.

Keep it under about 80 lines. It is a test fixture, not a game.

- [x] **Step 4: Pin the golden hash**

Run the suite. `GoldenHash_IsPinned` fails and reports the actual value. Replace `GoldenValue` with it, and add above the constant:

```csharp
        // Observed on first implementation. Changing the simulation changes this
        // value; that is the point. An unexplained change is drift.
```

Re-run: **39 passed** (33 baseline + 5 in `ToySimTests` + 1 in `FuzzTests`). The brief's original "19 passed" was computed before earlier tasks' review rounds added tests to the suite; take the real count from this run, never by hand.

- [x] **Step 5: Prove the golden test actually catches drift**

Temporarily change a constant inside `ToySim` — a movement step, say. Run the suite.

Expected: `GoldenHash_IsPinned` **FAILS** with a different value; `RandomScenarios_AreSelfConsistent` still passes, because the change is deterministic.

That is the distinction the two layers exist to draw: the golden test catches *change*, the fuzz test catches *nondeterminism*. Revert the constant and confirm green.

- [x] **Step 6: Commit**

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
- Create: `engine/Runtime/Corpus.cs`
- Create: `client/Assets/Editor/DeterminismHarness.cs`
- Create: `client/Assets/Determinism/CorpusPlayerHarness.cs`
- Create: `client/Assets/Determinism/CorpusHarness.unity`
- Create: `implementation/scripts/cross-runtime-diff.sh`
- Create: `.github/workflows/determinism.yml`

**Interfaces:**
- Consumes: `ToySim.Run` from Task 6.
- Produces: a script exiting non-zero when the two runtimes disagree, and a workflow running it on a self-hosted macOS runner.

- [x] **Step 1: Write the corpus runner as shared code**

`engine/Runtime/Corpus.cs` — a deterministic set of scenarios generated from a seed, so both runtimes run identical work without shipping a data file:

```csharp
namespace Broodline.Sim
{
    public static class Corpus
    {
        /// Generates scenario N deterministically and returns its whole-run hash.
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

**Folded back (fix round 2, Critical 2): the corpus also folds a fixed arithmetic sweep, because the toy simulation alone exercised almost nothing.**

`ToySim` reaches only `FromInt`, `+`, `-` and `Fix64.Zero`, and the vendored `Fixed64.Add`/`Sub` are literally `a + b` and `a - b`. So the gate as first written proved that **64-bit integer addition agrees across two runtimes** — arithmetic that could not plausibly have gone the other way — while every path carrying real cross-runtime risk never executed on IL2CPP at all:

- `DivPrecise` does variable-shift division including `u0 >> (64 - s)`, where `s = Nlz(|divisor|)` is `0` exactly when the divisor's magnitude sets the top bit. That is a shift of exactly 64, which **ECMA-335 leaves unspecified and C++ makes undefined** — and IL2CPP compiles to C++. The expression is masked to zero afterwards, but the shift still executes; if the two runtimes are ever going to disagree about `Fix64`, this is the likeliest single line.
- `SqrtPrecise` is a shift loop over `ulong`.
- `Mul` is unchecked wrapping `long` arithmetic with no overflow guard.

`RunScenario` therefore now returns a `Hash` folding the `ToySim` run hash **and** `FoldArithmeticSweep`, which applies `Mul`, `/`, `Sqrt` and `ToIntFloor` over `SweepRaws` — `Fix64Tests.SampleRaws` (`long.MinValue`, `long.MinValue + 1`, `±(1L<<40)`, `±(1L<<32)`, `±1`, `0`, `long.MaxValue - 1`, `long.MaxValue`) plus the pinned edges `0xFFFFFFFF`, `(1L<<31)-1` and `1L<<31` — every value and every ordered pair, in a fixed index order. Every one of those operations is total at those inputs (division by zero saturates, `Sqrt` of a negative returns zero, `Mul` wraps), which is exactly the documented `Fix64` contract; hashing the results is the point, since the gate's job is to prove the two runtimes agree about the edge behaviour, not that the edge behaviour is pleasant.

The sweep is deliberately identical for every scenario and folded **after** the simulation hash, so a single disagreement anywhere in it surfaces on all 500 lines of the diff rather than on one.

**`ToySim` was not changed** — `ToySimTests`'s golden hash stays valid. The corpus hashes did change, which is the evidence the sweep is actually folded in: scenario 0 moved from `8366189152614810321` to `15207835584096458000`, scenario 1 from `2288419299136412583` to `8418873356320103032`, and so on for all 500. The re-run gate then reported `PASS: 500 scenarios agree` at exit 0 with a byte-empty diff — a materially stronger claim than the same sentence made before.

`Corpus.ScenarioCount` was added in the same change as the single source of truth for the loop bound. Both emitters previously carried their own literal `500` held together by a "must match the CoreCLR emitter" comment that nothing checked; both now read this constant, `EnforcementTests.Corpus_StillSweepsTheFullScenarioCount` asserts it is 500, and the diff script asserts the line count it actually diffed equals 500.

- [x] **Step 2: Emit the CoreCLR side**

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

- [x] **Step 3: Build a real IL2CPP player, and emit the corpus from inside it**

**Corrected during implementation — `-executeMethod` alone was rejected.**
`-executeMethod` runs inside the Unity Editor, and the Editor executes
managed code under **Mono**, never IL2CPP. A harness that computed hashes
there would compare CoreCLR against Editor-Mono and print "500 scenarios
agree" having never executed a single IL2CPP-compiled instruction — the
exact blind spot this task exists to close, silently contradicting the
Architecture section's promise of bit-identical CoreCLR/IL2CPP results. So
the work is split across two files with two different jobs, and `-executeMethod`
is used only for the one thing it's actually fit for: driving a build.

- `client/Assets/Editor/DeterminismHarness.cs` —
  `DeterminismHarness.BuildMacIl2CppPlayer`, callable via `-executeMethod`.
  It never computes a hash. It builds a macOS ARM64, non-development,
  IL2CPP standalone player: reads the current scripting backend and
  architecture, sets `ScriptingImplementation.IL2CPP` and
  `OSArchitecture.ARM64` via `PlayerSettings.SetScriptingBackend`/
  `SetArchitecture(NamedBuildTarget.Standalone, ...)`, logs what it read back
  (not just what it set), builds via `BuildPipeline.BuildPlayer` with
  `BuildOptions.None` (non-development) and `extraScriptingDefines = {
  "BROODLINE_CORPUS_PLAYER" }` scoped to this build only, then restores the
  previous backend/architecture in a `finally` so a build that throws still
  leaves `ProjectSettings.asset`'s tracked values as it found them. It also
  logs which runtime artifacts landed in the bundle (`GameAssembly.dylib`
  present, no `MonoBleedingEdge/`) as evidence of which backend actually
  shipped.
- `client/Assets/Determinism/CorpusPlayerHarness.cs` — ships *inside* the
  player (deliberately not under `Editor/`, which player builds strip). A
  `[RuntimeInitializeOnLoadMethod(BeforeSplashScreen)]` reads `-corpusOut
  <path>` from the player's own command line, computes the identical 500
  hashes via `Corpus.RunScenario`, writes them, and quits with a status code
  (0 = emitted, 1 = emit failed, 2 = called without the flag — distinct
  codes so a caller can tell "ran and failed" from "called wrong"). Gated on
  the build-only `BROODLINE_CORPUS_PLAYER` define so the same
  `RuntimeInitializeOnLoadMethod` — which fires in *every* player built from
  this project — does not quit players it was never meant to touch.
- `client/Assets/Determinism/CorpusHarness.unity` — one deliberately empty
  scene (no camera, no light, no renderers). Tried first with zero scenes;
  Unity 6000.6 rejects that in batchmode (`BuildPlayerOptions.scenes = []`
  falls back to the currently-open, and therefore untitled, scene, and the
  build dies with "Cannot build untitled scene."). The corpus still runs at
  `BeforeSplashScreen`, before this scene loads — it exists only so the
  build succeeds, and reusing `SampleScene.unity` was rejected to avoid
  coupling the determinism gate to unrelated URP rendering assets.

**Folded back (fix round 1):** `DeterminismHarness.BuildMacIl2CppPlayer` now
refuses to hand back a Mono player rather than only logging which backend
shipped. Two independent checks guard this: the requested backend is read
back from `PlayerSettings` immediately after being set (throws before the
build starts if it isn't IL2CPP — catches `SetScriptingBackend` silently
no-oping, e.g. a future Unity upgrade changing what
`NamedBuildTarget.Standalone` means to that setter), and `ReportRuntimeArtifacts`
inspects the built bundle afterward (throws if `GameAssembly.dylib` is
missing or a `MonoBleedingEdge/` tree exists — catches the build pipeline
itself disagreeing with what `PlayerSettings` claimed). Verified against a
real Mono build (Task 7 fix-round-1 report), which also surfaced a real bug
in the second check: Unity 6000.6 ships `MonoBleedingEdge/` directly under
`Contents/`, not `Contents/Frameworks/` as originally checked, so that arm
of the check was always `False` regardless of the actual backend. Corrected
to `Contents/MonoBleedingEdge`. `GameAssembly.dylib`'s path was independently
confirmed correct against both a real IL2CPP build (Step 3 above) and this
same real Mono build, so the refusal was already correct either way on that
signal alone — this fixes the previously-dead second signal rather than
papering over it.

Both files compile against the same `Broodline.Sim` assembly CoreCLR also
runs (`Broodline.Sim.asmdef` is `autoReferenced`, so no new asmdef was
needed) — this is genuinely the same IL2CPP-compiled `Corpus.RunScenario`,
not a reimplementation.

- [x] **Step 4: Write the diff script**

**Corrected during implementation.** The version below replaces an earlier
draft that drove the Editor with `-executeMethod DeterminismHarness.EmitCorpus`
directly — invalid once Step 3 changed shape, since there is no such method
and emitting from the Editor is exactly what Step 3 rejected. This is the
script that was actually written, run, and watched fail on cue (Step 6):

`implementation/scripts/cross-runtime-diff.sh`:

```bash
#!/usr/bin/env bash
# Runs the 500-scenario corpus on CoreCLR and on a real IL2CPP player, and
# diffs the hashes. This is Phase 1's whole reason for existing: proving the
# engine compiles identically on both runtimes it actually ships on.
#
# IMPORTANT — this builds and runs a player, it does not use -executeMethod
# to compute hashes. -executeMethod runs inside the Unity Editor, and the
# Editor executes managed code under Mono, not IL2CPP. A gate built that way
# would compare CoreCLR against Editor-Mono and print "scenarios agree"
# having never executed a single IL2CPP-compiled instruction — see Task 7
# Step 3's report. So this script:
#   1. emits the CoreCLR side via `dotnet test` (tests/engine/CorpusTests.cs),
#   2. drives Unity via -executeMethod only to *build* a macOS ARM64,
#      non-development, IL2CPP standalone player (client/Assets/Editor/
#      DeterminismHarness.cs),
#   3. runs that built player as its own process, which emits its own side
#      (client/Assets/Determinism/CorpusPlayerHarness.cs),
#   4. diffs the two files it wrote.
#
# Timing: with a warm client/Library (Unity's own build cache, gitignored),
# the IL2CPP build is incremental and takes roughly 19-36 seconds. On a
# machine that has never built this player before — a fresh CI runner, or
# the first run on a new dev machine — the build is COLD: expect roughly
# 10 minutes, most of it one-time URP/Sentis shader-variant compilation that
# happens before IL2CPP codegen even starts. This is normal; it is not hung.
# Do not judge the timeout on the fast path alone.
#
# Streaming, not silent buffering: the Unity build is piped live to stdout
# (via `-logFile -`) rather than written to a log file that we only read
# after Unity exits. Several runs of this gate have previously been killed
# by a 600-second no-output watchdog while blocked on a silent multi-minute
# cold build — streaming avoids that, and it is also simply the right design
# for a CI log a human might be reading while it runs.
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(pwd)/client"
OUT="$(pwd)/implementation/results"
PLAYER_APP="$OUT/il2cpp-player/BroodlineCorpus.app"
PLAYER_BIN="$PLAYER_APP/Contents/MacOS/Broodline Bench"

CORPUS_CORECLR="$OUT/corpus-coreclr.txt"
CORPUS_IL2CPP="$OUT/corpus-il2cpp.txt"
CORPUS_DIFF="$OUT/corpus-diff.txt"
DOTNET_LOG="$OUT/corpus-dotnet-test.log"
BUILD_LOG="$OUT/il2cpp-build.log"
PLAYER_LOG="$OUT/il2cpp-player-run.log"

mkdir -p "$OUT"

# Every Unity batchmode invocation that builds this player reproducibly
# re-serialises these three files as a side effect of switching the active
# build target to StandaloneOSX and back — two URP assets pick up a
# shader-prefiltering field, and ProjectSettings.asset gains explicit
# (but equivalent-in-meaning) scriptingBackend/platformArchitecture keys.
# See Task 7 Step 3's report for the exact diffs. None of it is a real
# content change, but left in place it means "did this leave the tree
# dirty?" fails forever downstream (e.g. in CI). We restore exactly these
# three paths and NEVER `git checkout -- client/` or any other directory-wide
# revert: this branch takes concurrent commits from a human elsewhere under
# client/ (Benchmark work, at time of writing), and a blanket restore could
# silently discard their uncommitted work.
RESTORE_PATHS=(
  "client/ProjectSettings/ProjectSettings.asset"
  "client/Assets/Settings/PC_RPAsset.asset"
  "client/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"
)

# NOT restored, deliberately: the harness leaves Unity's active build target
# set to StandaloneOSX (EditorUserBuildSettings, which lives under
# client/Library/ — gitignored, not a git-cleanliness problem). Switching it
# back would just make the *next* run (or the next iOS build) pay a reimport
# cost with nothing gained, since nothing tracked by git reflects this value.

echo "--- preflight ---"
[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d "$PROJECT" ] || { echo "FAIL: no client/ project at $PROJECT"; exit 1; }
if pgrep -x Unity >/dev/null 2>&1; then
  echo "FAIL: a Unity editor is already running — close it first (a second instance cannot open the project)"
  exit 1
fi

# Snapshot which restore paths are ALREADY dirty before we touch anything.
# If one is, that is a human's in-progress edit, not our churn, and we have
# no way to tell which part of a later diff would be ours to discard — so we
# leave that specific file alone rather than guess (see git status/staging
# discipline: never touch a file you didn't dirty).
ALREADY_DIRTY=()
for p in "${RESTORE_PATHS[@]}"; do
  git diff --quiet HEAD -- "$p" 2>/dev/null || ALREADY_DIRTY+=("$p")
done

restore_known_churn() {
  for p in "${RESTORE_PATHS[@]}"; do
    dirty_before=0
    for d in "${ALREADY_DIRTY[@]:-}"; do
      [ "$d" = "$p" ] && dirty_before=1
    done
    if [ "$dirty_before" = 1 ]; then
      echo "NOTE: $p was already modified before this run — leaving it as-is, not restoring."
    else
      git checkout -- "$p" 2>/dev/null || true
    fi
  done
}

echo "--- CoreCLR ---"
rm -f "$CORPUS_CORECLR"
if ! BROODLINE_CORPUS_OUT="$CORPUS_CORECLR" \
    dotnet test Broodline.sln --filter EmitCorpusHashes >"$DOTNET_LOG" 2>&1; then
  echo "FAIL: dotnet test could not emit the CoreCLR corpus — see $DOTNET_LOG"
  tail -40 "$DOTNET_LOG"
  exit 1
fi
[ -s "$CORPUS_CORECLR" ] || { echo "FAIL: CoreCLR emitted no corpus file at $CORPUS_CORECLR"; exit 1; }

echo "--- Unity / IL2CPP: build the player (cold: ~10 min; incremental: ~19-36s) ---"
rm -f "$BUILD_LOG"
"$UNITY" -batchmode -quit -projectPath "$PROJECT" \
  -executeMethod DeterminismHarness.BuildMacIl2CppPlayer \
  -corpusPlayerOut "$PLAYER_APP" \
  -logFile - 2>&1 | tee "$BUILD_LOG"
build_code=${PIPESTATUS[0]}

# Restore the known churn right away, regardless of whether the build
# succeeded — the build-target switch that dirties these files happens near
# the start of DeterminismHarness.BuildMacIl2CppPlayer, before the actual
# BuildPipeline.BuildPlayer call, so a build that fails downstream still
# leaves the same files dirty.
restore_known_churn

if [ "$build_code" -ne 0 ] || ! grep -q "result=Succeeded" "$BUILD_LOG"; then
  echo "FAIL: IL2CPP player build failed (unity exit $build_code) — see $BUILD_LOG"
  exit 1
fi
[ -d "$PLAYER_APP" ] || { echo "FAIL: build reported success but $PLAYER_APP is missing"; exit 1; }

echo "--- Unity / IL2CPP: run the player ---"
rm -f "$CORPUS_IL2CPP" "$PLAYER_LOG"
"$PLAYER_BIN" -batchmode -nographics -logfile - -corpusOut "$CORPUS_IL2CPP" 2>&1 | tee "$PLAYER_LOG"
player_code=${PIPESTATUS[0]}
if [ "$player_code" -ne 0 ]; then
  echo "FAIL: IL2CPP player exited $player_code (0=wrote corpus, 1=emit failed, 2=called without -corpusOut) — see $PLAYER_LOG"
  exit 1
fi
[ -s "$CORPUS_IL2CPP" ] || { echo "FAIL: IL2CPP player emitted no corpus file at $CORPUS_IL2CPP"; exit 1; }

echo "--- diff ---"
if diff -u "$CORPUS_CORECLR" "$CORPUS_IL2CPP" >"$CORPUS_DIFF"; then
  echo "PASS: $(wc -l < "$CORPUS_CORECLR" | tr -d ' ') scenarios agree"
  exit 0
else
  echo "FAIL: runtimes disagree. First differences:"
  head -20 "$CORPUS_DIFF"
  exit 1
fi
```

**Three things a naive port of the old draft would have gotten wrong,**
found while building the real player rather than assuming one:

1. **Every Unity batch invocation dirties the tree beyond `ProjectSettings.asset`.**
   Switching the active build target to `StandaloneOSX` reproducibly
   re-serialises two URP assets under `client/Assets/Settings/`
   (`PC_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset` — a
   shader-prefiltering field appears) on top of the already-known
   `ProjectSettings.asset` churn (Step 3's report). Left unhandled, a CI
   runner's tree is dirty forever after the first run, and any downstream
   "is the tree clean" check fails permanently. The script restores exactly
   these three paths by name — never a blanket `git checkout -- client/` —
   because this branch takes concurrent commits from a human elsewhere under
   `client/`, and a directory-wide revert could silently discard their
   uncommitted work. It also snapshots whether any of the three was already
   dirty *before* the run and skips restoring that one file rather than
   guessing which part of its diff is the script's own churn.
2. **The harness leaves the active build target on `StandaloneOSX`.**
   That setting lives in `EditorUserBuildSettings`, persisted under
   `client/Library/` — gitignored — so it is not a tree-cleanliness problem.
   Deliberately not restored: switching it back would only make the next
   run (or the next iOS build) pay a reimport cost, for a value nothing
   tracked by git reflects either way.
3. **Cold vs. incremental build time differs by roughly 20x.** With a warm
   `client/Library` (this machine, mid-development) the IL2CPP build is
   19-36 seconds. On a machine that has never built this exact player
   before — a fresh CI runner, or a new contributor's first run — expect
   roughly **10 minutes**, most of it one-time URP/Sentis shader-variant
   compilation ahead of IL2CPP codegen itself. This sizes both the
   workflow's `timeout-minutes` (Step 7) and what a first-time local runner
   should expect before assuming it has hung.

**Folded back (fix round 1):** two gaps closed, because `grep -q
"result=Succeeded"` and the normal completion path alone were not enough:

1. **Backend/artifact assertions**, added right after the existing
   `result=Succeeded` check: `grep -q "building with backend=IL2CPP"`,
   `grep -q "GameAssembly.dylib present=True"`, and a negative check that
   `"MonoBleedingEdge/ present=True"` never appears. `result=Succeeded` alone
   proves *a* build succeeded, not which scripting backend it used — a
   silently-Mono build satisfies it too. `DeterminismHarness.cs` now refuses
   to return a Mono player on its own (Step 3 above), but that guard lives in
   Unity C# code a future edit could soften without this script noticing;
   these three assertions read the same evidence independently from the
   literal build log text, so softening the harness alone is not enough to
   make this gate pass on Mono. Proved for the right reason, not just wired:
   a real forced-Mono run was watched failing via the harness's own
   pre-build guard (message naming the backend, Unity exit 1); a second real
   forced-Mono run, with the harness's guards deliberately reverted to their
   pre-fix log-only behavior, was watched failing via these script-side
   assertions instead (the `backend=IL2CPP` check specifically); and a third
   real forced-Mono run, with only the pre-build guard neutralized, was
   watched failing via the harness's post-build artifact throw, with the
   corrected `MonoBleedingEdge` path now correctly reading `present=True`.
   See the fix-round-1 report for all three transcripts, and the final
   confirmation that a genuine IL2CPP pass still logs text these same
   assertions accept (`backend=IL2CPP`, `GameAssembly.dylib present=True`).
2. **A trap** (`EXIT`, `INT`, `TERM`), installed once `restore_known_churn`
   is defined: `trap restore_known_churn EXIT; trap 'exit 130' INT; trap
   'exit 143' TERM`. Without it, a kill between the backend being set inside
   Unity and the script's own explicit `restore_known_churn` call ever being
   reached — Ctrl-C, a CI job hitting `timeout-minutes`, an OOM — strands
   `client/ProjectSettings/ProjectSettings.asset` mid-build. The explicit
   call after the Unity step still runs first on the normal path, keeping
   the dirty window as short as possible; the trap is the safety net for
   every other way the script can leave. Verified in isolation (real
   transcripts in the fix-round-1 report): a bare `trap restore_known_churn
   EXIT` does not clobber an already-decided exit code even when
   `restore_known_churn`'s own last command fails, and `INT`/`TERM` signalled
   at the whole process group — how a terminal Ctrl-C and a CI cancellation
   actually deliver a signal to a running pipeline — interrupt promptly and
   restore before exiting 130/143 respectively. Also verified directly
   against this real script with the real trap installed: the normal PASS
   path still exits 0, and a real FAIL path (the forced-Mono runs above)
   still exits 1.

- [x] **Step 5: Run it and see it pass**

```bash
chmod +x implementation/scripts/cross-runtime-diff.sh
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first — the
script now checks this itself (`pgrep -x Unity`) and fails fast with a clear
message rather than letting a second Unity instance fail to open the project.

**This step is the entire point of Phase 1.** If it fails, do not proceed — read the first differing scenario index and reproduce it in isolation.

**Folded back (fix round 2, Finding 3): the script asserts the scenario count rather than printing it.** The PASS line used to read `PASS: $(wc -l < "$CORPUS_CORECLR") scenarios agree` — so reducing the corpus on both sides produced a cheerful `PASS: 5 scenarios agree` and exit 0, a gate agreeing loudly about almost nothing while the Definition of Done named 500. Both sides' line counts are now compared against `EXPECTED_SCENARIOS=500` *before* the diff, and a mismatch fails at exit 1.

**Folded back (fix round 2, Finding 6): the restore can no longer discard the human's uncommitted work.** `ALREADY_DIRTY` was snapshotted once at the top of the script, but the EXIT trap runs `git checkout -- "$p"` on every path out — so a file the human dirtied *after* that snapshot was silently reverted, and `client/ProjectSettings/ProjectSettings.asset` is squarely in their territory on a branch that has already had two commit collisions. Two changes: the snapshot is re-taken immediately before Unity is launched (closing the multi-minute `dotnet test` window), and `restore_known_churn` now re-runs the `git diff --quiet HEAD -- "$p"` dirtiness test itself and skips any path this run cannot account for. A `UNITY_RAN` flag makes the strongest case explicit — until Unity is actually invoked this script has touched none of `RESTORE_PATHS`, so a preflight failure or a failing `dotnet test` used to run `git checkout --` over three of the human's files having never started a build, and now restores nothing.

- [x] **Step 6: Prove the diff catches disagreement**

**Corrected during implementation:** the player-side emitter lives in
`CorpusPlayerHarness.cs`, not `DeterminismHarness.cs` (Step 3) — that is the
file to perturb. Temporarily make it emit a wrong value for scenario 250 —
add 1 to the hash for `i == 250` inside its emit loop. Rebuild and re-run.

Expected: **FAIL**, with the diff naming line 251 (the corpus file has one
`index,hash` line per scenario starting at index 0, so scenario 250 is line
251). Revert and confirm `PASS` again.

- [x] **Step 7: Add the workflow**

`.github/workflows/determinism.yml`, running on `[self-hosted, macOS]` nightly and on pushes touching `engine/**` or `tests/engine/**`, executing `dotnet test` and then `cross-runtime-diff.sh`.

Note in the file that it requires a self-hosted runner: GitHub-hosted macOS bills at a 10× multiplier and has no Unity licence, per `broodline_solo_execution.md` §7.3.

**Folded back:** `timeout-minutes: 45`, sized for a cold build (Step 4's
gotcha 3) plus checkout and `dotnet test` overhead, not the warm-cache fast
path. Whether a `[self-hosted, macOS]` runner is actually registered for
this repository cannot be verified from outside GitHub's settings — this
workflow may ship correct but un-runnable until one is registered, and that
should be stated plainly rather than implied otherwise.

**Folded back (fix round 1):** the push trigger's paths were extended beyond
`engine/**`/`tests/engine/**` to also watch the gate's own machinery:
`client/Assets/Editor/DeterminismHarness.cs` (which selects the backend —
exactly the failure mode Step 3's fix-round-1 note above addresses),
`client/Assets/Determinism/**`, `implementation/scripts/cross-runtime-diff.sh`
itself, this workflow file, `client/ProjectSettings/ProjectSettings.asset`
(whose `productName` the script's `PLAYER_BIN` path hardcodes as
`"Broodline Bench"`), and `Broodline.sln`. A change to any of these could
silently gut the gate and still land with a green board if nothing watched
for it.

**Folded back (fix round 2, Finding 7): a `pull_request` trigger, and two more paths.** The workflow had `push`, `schedule` and `workflow_dispatch` but no `pull_request` — a gate whose whole purpose is to block a bad merge never reported on the merge, only on the branch after the fact. `pull_request` now runs it against the merge commit, carrying the same path filter (spelled out twice rather than shared via a YAML anchor: anchors are not a documented GitHub Actions feature, and a silently-unparsed filter would run this 10-minute job on every PR or on none). One consequence worth knowing if this is ever made a **required** check: a required check with a path filter stays permanently "expected" on PRs that do not touch these paths — leave it advisory, or pair it with a path-filtered no-op job of the same name.

Two paths were added to both filters. `client/Packages/manifest.json` carries the `file:../../engine` line that is the actual wire making Unity compile the very same `Broodline.Sim` source CoreCLR runs — repoint or drop it and IL2CPP is silently compiling something else, or nothing, while the gate still reports on two files it believes are one. `client/ProjectSettings/ProjectVersion.txt` matters because `cross-runtime-diff.sh` hardcodes the editor path (`6000.6.0f1`): an editor upgrade lands there and the script then fails preflight at exit 127, which is worth learning from CI rather than from the next person to run it locally.

- [x] **Step 8: Commit**

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

**Folded back (fix round 2):** what that sentence now covers is materially wider than it was, because the final whole-branch review found the mechanisms sound individually but leaving a seam — *the static mechanisms are type-and-symbol shaped, the gate is execution shaped, and neither covered what the other missed.*

- The **500 scenarios** now exercise `Mul`, `/`, `Sqrt` and `ToIntFloor` across the wrap and saturation boundaries on both runtimes, not just `Fix64` add/sub (Task 7 Step 1).
- The **IL float scan** reads instruction operands, so float arriving through a call or a field no longer passes (Task 2 Step 5).
- The **`500` is asserted** from both directions — by the script against the lines it actually diffed, and by `EnforcementTests` against the constant both emitters loop to.
- The **enforcement is itself enforced**: `tests/engine/EnforcementTests.cs` fails if the analyzer reference, the `AdditionalFiles` include, `RS0030`-as-error, the asmdef's empty `references` or its `noEngineReferences: true` is removed — each of which previously left the whole board green.
- **`#if UNITY_*` in `engine/Runtime/` is banned and checked**, so "both compilers see the same code" is a premise rather than an assumption (Global Constraints).
- The gate **runs on pull requests**, and watches `manifest.json` and `ProjectVersion.txt`.
- The gate **cannot revert the human's uncommitted work** on any exit path.

The suite stands at **46 passed** after this round (41 before it, plus five in the new `tests/engine/EnforcementTests.cs`); taken from an actual `dotnet test Broodline.sln` run, not computed by hand.
