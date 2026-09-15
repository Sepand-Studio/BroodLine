# Phase 0 — Entity-Count Proof Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **STATUS — COMPLETE AND SIGNED OFF, 2026-09-14.** Every task in this plan is
> done (40/40), the budget is delivered and in the commission brief, and the
> gate is closed.
>
> **It closed by changing the floor, not by measuring the old one — and that
> distinction is the whole story.** This plan was written against an A13 / 3 GB
> reference device and measured on an A14 / 4 GB proxy, which is why the budget
> was published as PROVISIONAL behind a 30% margin. No A13 device was ever
> available. On 2026-09-14 the reference device was changed to **A14 / 4 GB**;
> `client_architecture` §3 and §12 carry the decision and its cost, which is a
> revenue cost, knowingly taken. **The iPad Air 4 stopped being a proxy and
> became the floor.** The measurement did not change. Its status did.
>
> | Was blocking | Resolution |
> |---|---|
> | **Reference device** — an A13 / 3 GB device to replace the A14 proxy measurement | **Discharged by the floor change.** The A14 run is now a floor run, so the 0.7 margin has nothing left to cover and is withdrawn. The budget is the measured **10000 triangles**, not 7000 |
> | **Real meshes** — `client_architecture` §4 requires the sweep run "with real creature meshes rather than capsules" | **Moved, not closed, and not waived.** It cannot be met until the rig proof delivers Vetch and Pale, so it now lives in `rig_proof` §8.3 as a re-run of this harness against the delivered prefabs. **It gates production species, not this phase** |
>
> **Phase 0's other half moved with it.** `solo_execution` §8.2 scheduled this
> proof alongside the rig proof, as "two proofs". The rig proof has never been
> commissioned and is blocked on procurement rather than on engineering, so it
> moved to the art track and keeps its gate there. **Phase 0 closes as an
> engineering gate and nothing downstream waits on it.**
>
> **How to read the task steps below.** They record what was done between
> 2026-09-08 and 2026-09-10 and they predate the floor change. Where a step
> quotes A13, a 30% margin, or a 7000-triangle budget, it is **accurate as
> history and superseded as fact** — it has been left intact rather than
> rewritten, because rewriting it would claim commits inserted text they did
> not. **The live numbers are in "Phase 0 result" at the end of this file and
> in `rig_proof` §8.3, and nowhere else.**

**Goal:** Produce a per-creature rendering budget — triangles, bones, materials — under which wave 44's ~100 entities hold 60 fps and stay inside 600 MB on an A14 / 4 GB device, so the rig proof commission can be briefed with a number instead of a hope.

**Architecture:** A Unity 6 project containing one benchmark scene that spawns **procedurally generated synthetic skinned meshes** at wave 44's entity counts, sweeps triangle/bone/material parameters, and records frame-time percentiles and peak memory. Synthetic geometry rather than real art, so the proof runs before any asset exists and its output constrains the art brief rather than waiting on it. The same harness re-runs against real meshes later as validation.

**Tech Stack:** Unity **6000.6.0f1**, C#, URP, Unity Test Framework, IL2CPP / ARM64 / Metal, Git LFS, Xcode.

## Global Constraints

Copied verbatim from `specs/plans/broodline_client_architecture.md`:

- **Reference device: A14 / 4 GB** — iPhone 12 and its variants, iPad Air 4, iPad 10th gen. *(Was A13 / 3 GB when this plan was written; changed 2026-09-14.)*
- **Frame rate: 60 target**, with a locked 30 fps fallback as the last degradation rung.
- **Memory: 600 MB peak.**
- **Wave 44 is the test case:** sixty Skirmishers, three Broods becoming thirty-nine, plus five creatures — close to a hundred entities.
- **Orientation: portrait only.** Layout is safe-area driven with no fixed pixel positions.
- **Asset Serialization: Force Text** and **Version Control: Visible Meta Files**, set before the first art commit.
- **Git LFS** for `*.fbx *.png *.tga *.psd *.jpg *.wav *.mp3 *.ogg`; **never** for `*.prefab *.asset *.anim *.controller *.unity`.
- Entities that are functionally identical are **instanced, not individually animated** (`specs/broodline_combat_engine.md` §9).

**This plan writes no simulation code.** The engine is Phase 1. Entities here are render-only stand-ins.

---

### Task 0: Prerequisites

The plan assumed a toolchain. Verify it before anything else — Task 2 is a hard stop without a Unity editor, and discovering that after Task 1 wastes the run.

**Files:**
- Create: `implementation/scripts/verify-prereqs.sh`

**Interfaces:**
- Consumes: nothing.
- Produces: `verify-prereqs.sh`, exit 0 when the toolchain is complete. Re-run after each install.

- [x] **Step 1: Run the check**

```bash
./implementation/scripts/verify-prereqs.sh
```

It reports git-lfs, Xcode, and every installed Unity editor together with whether that editor carries the **iOS Build Support** module. An editor without the module passes a naive "is Unity installed" check and then fails at Build Settings, which is why the script looks for `PlaybackEngines/iOSSupport` specifically rather than for the editor alone.

- [x] **Step 2: Install git-lfs if it is missing**

```bash
brew install git-lfs && git lfs install
```

`git lfs install` writes the global hooks; without it the filters in `.gitattributes` are inert and binaries commit as plain blobs.

- [x] **Step 3: Install Unity if it is missing**

This is a GUI flow — the Hub's headless CLI needs an interactive sign-in, so it cannot be scripted from here.

1. Open **Unity Hub** and sign in
2. **Installs → Install Editor → Unity 6 LTS** (the newest `6000.x` marked LTS)
3. In the module list, tick **iOS Build Support**. Nothing in this plan works without it
4. Wait for the download — it is several gigabytes

- [x] **Step 4: Re-run the check until it exits 0**

```bash
./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: three `ok` lines and `exit=0`.

- [x] **Step 5: Pin the Unity version in this plan**

Determinism work later depends on knowing which editor produced a build. Record the installed version in the Tech Stack line at the top of this document, replacing `Unity 6 (version pinned at Task 0)` with the exact version string, for example `Unity 6000.0.32f1`.

- [x] **Step 6: Commit**

```bash
git add implementation/scripts/verify-prereqs.sh implementation/
git commit -m "chore: prerequisites check for Phase 0

Verifies git-lfs, Xcode and Unity with the iOS Build Support module.
Checks for PlaybackEngines/iOSSupport rather than the editor alone,
because an editor without the module passes a naive check and then fails
at Build Settings."
```

---

### Task 1: Repository skeleton and Git LFS

Git LFS must be configured **before the first binary is committed** — retrofitting it means rewriting history.

**Files:**
- Create: `.gitattributes`
- Create: `.gitignore`
- Create: `implementation/README.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a repository where `client/Assets/Art/**` binaries route to LFS and Unity's generated files are ignored.

- [x] **Step 1: Confirm the toolchain is ready**

```bash
./implementation/scripts/verify-prereqs.sh
```

Expected: exit 0. Task 0 covers installation; this is the gate.

- [x] **Step 2: Write `.gitattributes`**

The repository already has one line (`* text=auto eol=lf`) plus markdown and image rules. Replace the file with this, which keeps those rules and adds the LFS split:

```gitattributes
* text=auto eol=lf

*.md   text diff=markdown
*.json text
*.yml  text
*.yaml text

# Unity YAML — must stay diffable and mergeable. NEVER LFS.
*.prefab    text
*.unity     text
*.asset     text
*.anim      text
*.controller text
*.meta      text

# Binary art — LFS.
*.fbx filter=lfs diff=lfs merge=lfs -text
*.png filter=lfs diff=lfs merge=lfs -text
*.jpg filter=lfs diff=lfs merge=lfs -text
*.jpeg filter=lfs diff=lfs merge=lfs -text
*.tga filter=lfs diff=lfs merge=lfs -text
*.psd filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.mp3 filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.pdf filter=lfs diff=lfs merge=lfs -text
```

- [x] **Step 3: Verify the LFS routing before committing anything binary**

```bash
git check-attr filter -- client/Assets/Art/example.fbx
git check-attr filter -- client/Assets/Game/example.prefab
```

Expected, exactly:
```
client/Assets/Art/example.fbx: filter: lfs
client/Assets/Game/example.prefab: filter: unspecified
```

If `.prefab` reports `lfs`, the rules are wrong — fix before proceeding. A prefab in LFS is unmergeable and it is the mistake this step exists to catch.

- [x] **Step 4: Write `.gitignore`**

```gitignore
# Unity generated
client/[Ll]ibrary/
client/[Tt]emp/
client/[Oo]bj/
client/[Bb]uild/
client/[Bb]uilds/
client/[Ll]ogs/
client/[Uu]serSettings/
client/[Mm]emoryCaptures/
client/[Rr]ecordings/

# Unity regenerates these; the hand-maintained solution is at the repo root
client/*.csproj
client/*.sln
client/*.user

# Benchmark output
implementation/results/*.csv
implementation/results/*.json

.DS_Store
```

- [x] **Step 5: Write `implementation/README.md`**

```markdown
# Implementation

Task plans and their outputs. Design and architecture live in `specs/`.

- `2026-09-08-phase0-entity-count-proof.md` — the render-budget proof that
  briefs the rig proof commission.
- `results/` — benchmark output, gitignored. Findings are promoted into the
  plan document itself.
```

- [x] **Step 6: Commit**

```bash
mkdir -p implementation/results
git add .gitattributes .gitignore implementation/
git commit -m "chore: repository skeleton with Git LFS routing

LFS covers binary art only. Unity YAML — prefabs, scenes, assets,
animations, controllers, meta files — stays text so it stays mergeable.
Verified with git check-attr before any binary was added, because
retrofitting LFS means rewriting history."
```

---

### Task 2: Unity project with the settings that are expensive to change later

Three settings are painful to notice late: text serialization, visible meta files, and the iOS build target. All three are inspectable as text, so they can be asserted without opening Unity.

**Files:**
- Create: `client/` (Unity 6 project, 3D URP template)
- Modify: `client/ProjectSettings/EditorSettings.asset`
- Modify: `client/ProjectSettings/ProjectSettings.asset`
- Create: `implementation/scripts/verify-unity-settings.sh`

**Interfaces:**
- Consumes: Task 1's `.gitignore`, which keeps `client/Library/` out of git.
- Produces: a Unity project at `client/` whose settings are asserted by `verify-unity-settings.sh`, reused by CI in Phase 1.

- [x] **Step 1: Create the Unity project**

Install Unity 6 LTS through Unity Hub with the **iOS Build Support** module. Then create a project:

- Template: **Universal 3D** (URP)
- Name: `client`
- Location: the repository root

- [x] **Step 2: Set the three settings in the editor**

- `Edit → Project Settings → Editor → Asset Serialization → Mode: **Force Text**`
- `Edit → Project Settings → Editor → Version Control → Mode: **Visible Meta Files**`
- `File → Build Settings → Platform: **iOS** → Switch Platform`
- `Edit → Project Settings → Player → Other Settings`:
  - Scripting Backend: **IL2CPP**
  - Target Architectures: **ARM64**
  - `Edit → Project Settings → Player → Resolution and Presentation`: **Default Orientation: Portrait**, all other orientations unchecked

- [x] **Step 3: Verify with `implementation/scripts/verify-unity-settings.sh`**

The script exists. It asserts only values that live in **committed** files, which excludes two things worth knowing about:

- **The active build target is not checked.** It lives in `client/Library/`, which is gitignored, so no committed file records whether you switched to iOS. Switch it anyway — Task 5 cannot build without it.
- **The iOS scripting backend is not checked.** iOS supports only IL2CPP, so there is nothing to get wrong.

Two assertions are written the way they are for a reason, and both were wrong on the first attempt:

- Unity 6 omits `m_ExternalVersionControlSupport` when it holds the default, so the script asserts the observable consequence — that `.meta` files exist under `client/Assets` — rather than a key that is absent precisely when the setting is correct.
- Always Included Shaders are stored **by GUID, never by name**. The script greps for `933532a4fcc9baf4fa0491de14d08ed7`, URP's `Lit.shader`. A grep for the string "Universal Render Pipeline/Lit" matches nothing even when the shader is correctly added.

- [x] **Step 4: Run it and confirm every line passes**

```bash
chmod +x implementation/scripts/verify-unity-settings.sh
./implementation/scripts/verify-unity-settings.sh
```

Expected: six `ok` lines, exit status 0. Any `FAIL` means the editor setting did not take — fix it in Unity and re-run rather than editing the `.asset` by hand.

- [x] **Step 5: Commit**

```bash
git add client/ implementation/scripts/verify-unity-settings.sh
git commit -m "feat: Unity 6 project with text serialization and portrait iOS target

Force Text and Visible Meta Files are set before the first asset lands,
because both are painful to notice late and both are prerequisites for
the LFS split in .gitattributes. verify-unity-settings.sh asserts them
as text so CI can check them without opening Unity."
```

---

### Task 3: The synthetic creature generator

The proof needs meshes with controllable cost before any art exists. This generates a skinned mesh to an exact triangle, bone and material count.

**Files:**
- Create: `client/Assets/Benchmark/SyntheticCreature.cs`
- Create: `client/Assets/Benchmark/Broodline.Benchmark.asmdef`
- Create: `client/Assets/Benchmark/Tests/Broodline.Benchmark.Tests.asmdef`
- Test: `client/Assets/Benchmark/Tests/SyntheticCreatureTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `SyntheticCreature.Build(SyntheticCreatureSpec spec) -> GameObject`, where
  `SyntheticCreatureSpec` is a struct with `int Triangles, int Bones, int Materials`.
  The returned GameObject carries a `SkinnedMeshRenderer` whose mesh has exactly
  `spec.Triangles` triangles, `spec.Bones` bones, and `spec.Materials` submeshes.

- [x] **Step 1: Create the assembly definitions**

`client/Assets/Benchmark/Broodline.Benchmark.asmdef`:

```json
{
  "name": "Broodline.Benchmark",
  "rootNamespace": "Broodline.Benchmark",
  "references": [],
  "includePlatforms": [],
  "autoReferenced": true
}
```

`client/Assets/Benchmark/Tests/Broodline.Benchmark.Tests.asmdef`:

```json
{
  "name": "Broodline.Benchmark.Tests",
  "rootNamespace": "Broodline.Benchmark.Tests",
  "references": ["Broodline.Benchmark", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

Three fields here are load-bearing and a test assembly silently fails to compile without them:

- **`precompiledReferences: ["nunit.framework.dll"]` with `overrideReferences: true`** — this is how the assembly sees NUnit. `optionalUnityReferences: ["TestAssemblies"]` is the **legacy** form, removed years ago; it produces "Scripts have compiler errors" with no message naming the cause.
- **`includePlatforms: ["Editor"]`** — EditMode tests compile for the editor only.
- **`defineConstraints: ["UNITY_INCLUDE_TESTS"]`** — keeps the test assembly out of player builds.

- [x] **Step 2: Write the failing test**

`client/Assets/Benchmark/Tests/SyntheticCreatureTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Broodline.Benchmark;

public class SyntheticCreatureTests
{
    [Test]
    public void Build_ProducesRequestedTriangleBoneAndMaterialCounts()
    {
        var spec = new SyntheticCreatureSpec { Triangles = 1200, Bones = 24, Materials = 2 };

        var go = SyntheticCreature.Build(spec);
        var smr = go.GetComponent<SkinnedMeshRenderer>();

        Assert.IsNotNull(smr, "expected a SkinnedMeshRenderer");
        Assert.AreEqual(24, smr.bones.Length, "bone count");
        Assert.AreEqual(2, smr.sharedMesh.subMeshCount, "submesh count");

        int triangles = 0;
        for (int i = 0; i < smr.sharedMesh.subMeshCount; i++)
            triangles += (int)(smr.sharedMesh.GetIndexCount(i) / 3);
        Assert.AreEqual(1200, triangles, "triangle count");

        Object.DestroyImmediate(go);
    }
}
```

- [x] **Step 3: Run it and confirm it fails**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: non-zero exit with a compile error — `SyntheticCreature` and `SyntheticCreatureSpec` do not exist. A compile failure is a valid failing state here; do not proceed until you have seen it.

- [x] **Step 4: Write the implementation**

`client/Assets/Benchmark/SyntheticCreature.cs`:

```csharp
using UnityEngine;

namespace Broodline.Benchmark
{
    public struct SyntheticCreatureSpec
    {
        public int Triangles;
        public int Bones;
        public int Materials;
    }

    /// Builds a skinned mesh with an exact triangle, bone and material cost.
    /// Geometry is deliberately meaningless — only its cost matters.
    public static class SyntheticCreature
    {
        public static GameObject Build(SyntheticCreatureSpec spec)
        {
            var root = new GameObject("SyntheticCreature");
            var smr = root.AddComponent<SkinnedMeshRenderer>();

            var bones = new Transform[spec.Bones];
            var bindPoses = new Matrix4x4[spec.Bones];
            for (int i = 0; i < spec.Bones; i++)
            {
                var b = new GameObject("b" + i).transform;
                b.SetParent(i == 0 ? root.transform : bones[i - 1], false);
                b.localPosition = new Vector3(0f, i == 0 ? 0f : 0.1f, 0f);
                bones[i] = b;
                bindPoses[i] = b.worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            // Three vertices per triangle: no sharing, so the count is exact.
            int vertCount = spec.Triangles * 3;
            var verts = new Vector3[vertCount];
            var weights = new BoneWeight[vertCount];
            for (int v = 0; v < vertCount; v++)
            {
                int tri = v / 3;
                float t = tri / (float)Mathf.Max(1, spec.Triangles);
                verts[v] = new Vector3(
                    Mathf.Cos(t * Mathf.PI * 8f) * 0.3f + (v % 3) * 0.01f,
                    t * (0.1f * spec.Bones),
                    Mathf.Sin(t * Mathf.PI * 8f) * 0.3f);
                weights[v] = new BoneWeight
                {
                    boneIndex0 = Mathf.Min(spec.Bones - 1, tri * spec.Bones / Mathf.Max(1, spec.Triangles)),
                    weight0 = 1f
                };
            }

            var mesh = new Mesh { name = "SyntheticCreatureMesh" };
            if (vertCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            mesh.subMeshCount = spec.Materials;

            // Distribute triangles across submeshes; the remainder goes to the last.
            int perSub = spec.Triangles / spec.Materials;
            int cursor = 0;
            for (int s = 0; s < spec.Materials; s++)
            {
                int count = (s == spec.Materials - 1) ? spec.Triangles - cursor : perSub;
                var idx = new int[count * 3];
                for (int i = 0; i < count * 3; i++) idx[i] = (cursor * 3) + i;
                mesh.SetTriangles(idx, s, false);
                cursor += count;
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mats = new Material[spec.Materials];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            for (int m = 0; m < spec.Materials; m++) mats[m] = new Material(shader);

            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[0];
            smr.sharedMaterials = mats;
            smr.localBounds = mesh.bounds;
            return root;
        }
    }
}
```

- [x] **Step 5: Run the test and confirm it passes**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: exit 0, and `total=1 passed=1 failed=0`.

If the triangle count is short, the submesh remainder distribution is wrong — the last submesh takes `spec.Triangles - cursor`, not `perSub`.

- [x] **Step 6: Commit**

```bash
git add client/Assets/Benchmark/
git commit -m "feat: synthetic creature generator with exact render cost

Builds a skinned mesh to a requested triangle, bone and material count so
the entity-count proof can run before any art exists. Vertices are not
shared between triangles, which makes the count exact and the cost
pessimistic — the real budget can only be better."
```

---

### Task 4: The wave 44 benchmark scene

**Files:**
- Create: `client/Assets/Benchmark/WaveBenchmark.cs`
- Create: `client/Assets/Benchmark/BenchmarkResult.cs`
- Test: `client/Assets/Benchmark/Tests/WaveBenchmarkTests.cs`

**Interfaces:**
- Consumes: `SyntheticCreature.Build(SyntheticCreatureSpec)` from Task 3.
- Produces:
  - `WaveBenchmark.Spawn(SyntheticCreatureSpec spec, int count) -> GameObject[]`
  - `WaveBenchmark.Wave44Composition -> int` (constant, `104`)
  - `BenchmarkResult` with fields `double MedianMs, double P95Ms, long PeakMemoryBytes, int EntityCount, SyntheticCreatureSpec Spec`
  - `BenchmarkResult.ToCsvRow() -> string` and `BenchmarkResult.CsvHeader -> string`
  - `WaveBenchmark.Despawn(GameObject[] spawned) -> void` — destroys the entities **and the materials they own**

- [x] **Step 1: Write the failing test**

`client/Assets/Benchmark/Tests/WaveBenchmarkTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using Broodline.Benchmark;

public class WaveBenchmarkTests
{
    [Test]
    public void Wave44Composition_Is104Entities()
    {
        // 60 Skirmishers + 39 Brood children + 5 creatures — combat_engine section 9.
        Assert.AreEqual(104, WaveBenchmark.Wave44Composition);
    }

    [Test]
    public void Spawn_CreatesRequestedNumberOfEntities()
    {
        var spec = new SyntheticCreatureSpec { Triangles = 600, Bones = 16, Materials = 1 };
        var spawned = WaveBenchmark.Spawn(spec, 10);

        Assert.AreEqual(10, spawned.Length);
        foreach (var go in spawned)
        {
            Assert.IsNotNull(go.GetComponent<SkinnedMeshRenderer>());
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Spawn_WithUnevenMaterialSplit_StillProducesExactTriangleCount()
    {
        // 1000 across 3 submeshes is 333/333/334 — the remainder path.
        var spec = new SyntheticCreatureSpec { Triangles = 1000, Bones = 8, Materials = 3 };
        var spawned = WaveBenchmark.Spawn(spec, 1);
        var mesh = spawned[0].GetComponent<SkinnedMeshRenderer>().sharedMesh;

        int triangles = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
            triangles += (int)(mesh.GetIndexCount(i) / 3);

        Assert.AreEqual(1000, triangles, "remainder must land on the last submesh");
        WaveBenchmark.Despawn(spawned);
    }

    [Test]
    public void Despawn_DestroysEntitiesAndTheirMaterials()
    {
        var spec = new SyntheticCreatureSpec { Triangles = 300, Bones = 8, Materials = 2 };
        var spawned = WaveBenchmark.Spawn(spec, 3);
        var material = spawned[0].GetComponent<SkinnedMeshRenderer>().sharedMaterials[0];

        WaveBenchmark.Despawn(spawned);

        Assert.IsTrue(material == null, "materials must be destroyed, not merely orphaned");
        foreach (var go in spawned) Assert.IsTrue(go == null, "entities must be destroyed");
    }

    [Test]
    public void BenchmarkResult_CsvRowMatchesHeaderColumnCount()
    {
        var r = new BenchmarkResult
        {
            Spec = new SyntheticCreatureSpec { Triangles = 600, Bones = 16, Materials = 1 },
            EntityCount = 104, MedianMs = 12.5, P95Ms = 16.1, PeakMemoryBytes = 400L * 1024 * 1024
        };
        Assert.AreEqual(
            BenchmarkResult.CsvHeader.Split(',').Length,
            r.ToCsvRow().Split(',').Length);
    }
}
```

- [x] **Step 2: Run and confirm failure**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: non-zero exit, compile error — `WaveBenchmark` and `BenchmarkResult` do not exist.

- [x] **Step 3: Write `BenchmarkResult.cs`**

```csharp
using System.Globalization;

namespace Broodline.Benchmark
{
    public struct BenchmarkResult
    {
        public SyntheticCreatureSpec Spec;
        public int EntityCount;
        public double CpuP95Ms;      // FrameTimingManager, real CPU work
        public double GpuP95Ms;      // FrameTimingManager, real GPU work
        public double WallP95Ms;     // Time.unscaledDeltaTime, kept for reference
        public long PeakMemoryBytes;

        /// One frame at 60 fps is 1000/60 = 16.667 ms, not 16.6. A hardcoded 16.6
        /// makes a flawless 60 fps read as failure — which is exactly what the
        /// first device run reported.
        public const double Frame60Ms = 1000.0 / 60.0;
        public const double Frame30Ms = 1000.0 / 30.0;

        public static string CsvHeader =>
            "triangles,bones,materials,entities,cpu_p95_ms,gpu_p95_ms,wall_p95_ms,peak_mb,holds_60,holds_30,under_600mb";

        /// Judged on real CPU and GPU work, never on wall-clock frame interval.
        /// With a frame-rate cap or vsync, wall-clock reports the cap rather than
        /// the cost and is flat regardless of load.
        public bool Holds60 => System.Math.Max(CpuP95Ms, GpuP95Ms) <= Frame60Ms;
        public bool Holds30 => System.Math.Max(CpuP95Ms, GpuP95Ms) <= Frame30Ms;
        public bool UnderMemoryCeiling => PeakMemoryBytes <= 600L * 1024 * 1024;

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                Spec.Triangles.ToString(c),
                Spec.Bones.ToString(c),
                Spec.Materials.ToString(c),
                EntityCount.ToString(c),
                CpuP95Ms.ToString("F2", c),
                GpuP95Ms.ToString("F2", c),
                WallP95Ms.ToString("F2", c),
                (PeakMemoryBytes / (1024.0 * 1024.0)).ToString("F1", c),
                Holds60 ? "1" : "0",
                Holds30 ? "1" : "0",
                UnderMemoryCeiling ? "1" : "0");
        }
    }
}
```

**Why CPU and GPU rather than wall-clock.** The first device run on an iPhone 15 Pro returned a median of **16.67 ms at every one of thirty settings**, from 400 triangles to 6000 — perfectly flat across a 15× range, while peak memory rose sensibly from 176 to 304 MB. `Application.targetFrameRate = 60` makes Unity sleep to hit the target, so `Time.unscaledDeltaTime` reports the cap, not the cost. A benchmark whose job is finding a ceiling cannot be measured by the thing capping it.

`FrameTimingManager` reports actual CPU and GPU milliseconds per frame, unaffected by vsync or a frame-rate cap. It requires `PlayerSettings.enableFrameTimingStats = true`, set by `Phase0Setup`.

Thresholds are `1000.0/60.0` and `1000.0/30.0`, not 16.6 and 33.3. One frame at 60 fps is **16.667 ms**, so the old constant made a flawless 60 fps read as failure — every row of the first run said `holds_60=0` while the phone rendered perfectly.

- [x] **Step 4: Write `WaveBenchmark.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Benchmark
{
    public static class WaveBenchmark
    {
        /// 60 Skirmishers + 39 Brood children + 5 creatures.
        /// specs/broodline_combat_engine.md section 9.
        public const int Wave44Composition = 104;

        public static GameObject[] Spawn(SyntheticCreatureSpec spec, int count)
        {
            var made = new GameObject[count];
            int perRow = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var go = SyntheticCreature.Build(spec);
                go.name = "entity_" + i;
                go.transform.position = new Vector3(
                    (i % perRow) * 1.2f - perRow * 0.6f,
                    0f,
                    (i / perRow) * 1.2f);
                made[i] = go;
            }
            return made;
        }

        /// Destroys spawned entities AND the materials they own.
        /// `SyntheticCreature.Build` calls `new Material(shader)` per entity, and
        /// destroying a GameObject does not reclaim materials it created. Over a
        /// sweep of ~30 combinations at 104 entities this accumulates thousands of
        /// native Material allocations, which inflates the very peak-memory number
        /// the proof exists to measure.
        public static void Despawn(GameObject[] spawned)
        {
            if (spawned == null) return;
            foreach (var go in spawned)
            {
                if (go == null) continue;
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    foreach (var m in smr.sharedMaterials)
                        if (m != null) Object.DestroyImmediate(m);
                    if (smr.sharedMesh != null) Object.DestroyImmediate(smr.sharedMesh);
                }
                Object.DestroyImmediate(go);
            }
        }

        /// Frame times in milliseconds, sorted ascending, from a captured run.
        public static BenchmarkResult Summarise(
            List<double> frameMs, SyntheticCreatureSpec spec, int entityCount, long peakBytes)
        {
            frameMs.Sort();
            return new BenchmarkResult
            {
                Spec = spec,
                EntityCount = entityCount,
                MedianMs = Percentile(frameMs, 0.50),
                P95Ms = Percentile(frameMs, 0.95),
                PeakMemoryBytes = peakBytes
            };
        }

        static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            int idx = Mathf.Clamp(Mathf.CeilToInt(p * sorted.Count) - 1, 0, sorted.Count - 1);
            return sorted[idx];
        }
    }
}
```

- [x] **Step 5: Run the tests and confirm they pass**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: exit 0, `total=6 passed=6 failed=0` — the one test from Task 3 plus the five added here.

- [x] **Step 6: Commit**

```bash
git add client/Assets/Benchmark/
git commit -m "feat: wave 44 benchmark composition and result model

104 entities per combat_engine section 9. BenchmarkResult carries the
three Global Constraint thresholds — 16.6ms, 33.3ms, 600MB — as computed
properties, so a row is self-describing rather than needing the reader to
remember the budget."
```

---

### Task 5: The device sweep and the budget it produces

This is the task that produces the deliverable: the number for the commission.

**Files:**
- Create: `client/Assets/Benchmark/SweepRunner.cs`
- Create: `client/Assets/Scenes/Benchmark.unity`
- Modify: `implementation/2026-09-08-phase0-entity-count-proof.md` (record the result)

**Interfaces:**
- Consumes: `WaveBenchmark.Spawn`, `WaveBenchmark.Summarise`, `BenchmarkResult.ToCsvRow`.
- Produces: a CSV at `Application.persistentDataPath/entity-budget.csv`, one row per parameter combination.

- [x] **Step 1: Write `SweepRunner.cs`**

```csharp
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Broodline.Benchmark
{
    /// Attach to an empty GameObject in Scenes/Benchmark.unity and build to device.
    public class SweepRunner : MonoBehaviour
    {
        [SerializeField] int[] triangleSteps = { 400, 800, 1500, 3000, 6000 };
        [SerializeField] int[] boneSteps = { 12, 24, 48 };
        [SerializeField] int[] materialSteps = { 1, 2 };
        [SerializeField] int warmupFrames = 60;
        [SerializeField] int measureFrames = 300;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var sb = new StringBuilder();
            // Provenance first. A budget is only meaningful against the device it
            // was measured on, and these files will outlive the memory of which
            // phone produced them.
            sb.AppendLine("# device=" + SystemInfo.deviceModel);
            sb.AppendLine("# gpu=" + SystemInfo.graphicsDeviceName);
            sb.AppendLine("# memory_mb=" + SystemInfo.systemMemorySize);
            sb.AppendLine("# os=" + SystemInfo.operatingSystem);
            sb.AppendLine("# unity=" + Application.unityVersion);
            sb.AppendLine("# entities=" + WaveBenchmark.Wave44Composition);
            sb.AppendLine(BenchmarkResult.CsvHeader);

            foreach (var tris in triangleSteps)
            foreach (var bones in boneSteps)
            foreach (var mats in materialSteps)
            {
                var spec = new SyntheticCreatureSpec { Triangles = tris, Bones = bones, Materials = mats };
                var spawned = WaveBenchmark.Spawn(spec, WaveBenchmark.Wave44Composition);

                for (int i = 0; i < warmupFrames; i++) yield return null;

                var frames = new List<double>(measureFrames);
                long peak = 0;
                for (int i = 0; i < measureFrames; i++)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000.0);
                    long used = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    if (used > peak) peak = used;
                }

                var result = WaveBenchmark.Summarise(frames, spec, WaveBenchmark.Wave44Composition, peak);
                sb.AppendLine(result.ToCsvRow());
                Debug.Log("[sweep] " + result.ToCsvRow());

                WaveBenchmark.Despawn(spawned);
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
                yield return null;
            }

            var path = Path.Combine(Application.persistentDataPath, "entity-budget.csv");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[sweep] COMPLETE -> " + path);
        }
    }
}
```

~~`Time.unscaledDeltaTime` with vSync off and `targetFrameRate` at 60 measures the real frame interval.~~ **Superseded — this was wrong, and it cost two device runs.** `targetFrameRate` makes Unity sleep to hit the target whether or not vSync is off, so `unscaledDeltaTime` reports the cap and reads ~16.7 ms at every load. `cpuFrameTime` repeats the error because it includes the main thread's block on present. The frame's real cost is `max(cpuMainThreadFrameTime, gpuFrameTime)` — and `cpuMainThreadFrameTime` is already exclusive of `cpuMainThreadPresentWaitTime`, so the two must not be subtracted from one another. See the Phase 0 result below for the runs that established this.

`Profiler.GetTotalAllocatedMemoryLong()` already includes the managed heap, so do **not** add `GC.GetTotalMemory` to it — that double-counts and will make every row fail the 600 MB check for no reason.

- [x] **Step 2: Build the scene from a script, not by hand**

Create `client/Assets/Editor/BenchmarkSceneBuilder.cs` exposing `[MenuItem("Broodline/Build Benchmark Scene")]` and a static method callable from the CLI. It must:

- create a scene at `client/Assets/Scenes/Benchmark.unity`, creating the `Scenes` folder if absent
- add a GameObject named `SweepRunner` carrying the `SweepRunner` component
- add a camera positioned to frame roughly a 12×12 metre area at origin — **every entity must be on screen and drawn**, or the benchmark measures frustum culling instead of rendering
- add a directional light, so the URP/Lit material actually shades and the GPU cost is representative
- set the scene as index 0 in `EditorBuildSettings.scenes`

A scripted scene is reproducible and reviewable as text; a hand-built one is neither, and this scene is the measuring instrument.

URP/Lit is already in Always Included Shaders — `Phase0Setup.Apply` did it, and `verify-unity-settings.sh` asserts its GUID.

- [x] **Step 3: Run it in the editor as a smoke test**

Press Play. Expected: `[sweep]` lines in the Console, one per combination, ending in `[sweep] COMPLETE`.

**Editor numbers are not the result** — they measure your Mac. This step only proves the harness runs to completion without throwing.

- [x] **Step 4: Build and run on device — two tiers, and only one of them produces the budget**

**The Unity side is scripted.** Unity moved its build window between versions — `File → Build Settings` became **Build Profiles** — so menu instructions rot. `client/Assets/Editor/BenchmarkBuilder.cs` uses `BuildPipeline.BuildPlayer`, which is stable across both, and switches the active build target itself:

```bash
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod BenchmarkBuilder.BuildIOS \
  -logFile "$(pwd)/implementation/results/ios-build.log"
```

Or `Broodline → Build iOS Xcode Project` with the editor open. Output is `build/ios/Unity-iPhone.xcodeproj`, gitignored — an Xcode project is a build artifact and regenerating it is cheaper than storing it.

It builds with `BuildOptions.Development`, deliberately: that keeps managed stack traces so a throw on device is diagnosable, and it is what lets Xcode's **Download Container** reach the CSV at Step 5.

**Then, in Xcode:** open the project, set your signing team under Signing & Capabilities, select the device, Run. Watch the console for `[sweep]` lines ending in `[sweep] COMPLETE`.

**Not TestFlight.** It is for distributing to testers, and a TestFlight-installed app does not expose its container to Download Container — the sweep would run and the CSV would be unreachable.

| Tier | Device | What the run is worth |
|---|---|---|
| **Ceiling** | A17 / 8 GB, e.g. iPhone 15 Pro | Proves the harness end to end, upper bound only. **Never a budget** |
| **Floor** | **A14 / 4 GB** — iPhone 12 and its variants, iPad Air 4, iPad 10th gen | The reference device since 2026-09-14. **A run here produces the budget for production assets, with no margin applied** |
| ~~Proxy~~ | ~~A14 / 4 GB standing in for an A13 floor~~ | **Retired 2026-09-14.** The proxy tier existed only because the floor was A13 and no A13 device could be obtained. There is nothing left for it to stand in for |

### Why the iPad Air 4 is the worst-case A14, and why no margin is applied

**Rewritten 2026-09-14.** This section previously justified multiplying the measured triangle and bone counts by 0.7. That margin covered, in its own words, *"the generational GPU gap plus uncertainty in the fill-versus-vertex mix"* between an A14 proxy and an A13 floor. **With A14 as the floor there is no generational gap left to cover, so the margin is withdrawn rather than reduced.** What follows is why the remaining uncertainty also resolves in the budget's favour.

The iPad Air 4 is not the weakest A14 device. For this benchmark it is the hardest one:

| | iPhone 12 — the phone floor | iPad Air 4 — measured |
|---|---|---|
| SoC | A14 | A14, identical |
| Memory | 4 GB | 4 GB (3868 MB reported) |
| Pixels | 2532×1170 ≈ 2.96M | 2360×1640 ≈ 3.87M |

The iPad pushes **about 1.3× the pixels of an iPhone 12 on the same silicon**, and the measured ceiling is **GPU-bound**: 16000 triangles fails at 19.2–20.1 ms GPU p95 while the main thread sits at 7.3–7.7 ms. In the one dimension that actually binds, the device measured is harder than the phone it stands for. **A budget that passes here passes on every A14 phone.**

Memory runs conservative in the same direction — larger framebuffers mean the iPad's peak overstates a phone's, so a pass under 600 MB is a genuine pass.

**What no margin would ever have fixed, and what the headroom is actually for.** `BoneAnimator` writes local rotations directly; a real `Animator` also evaluates a graph, samples curves and blends clips. The meshes are synthetic and the shader is not a production shader. **None of that was measured at any tier, on any device, under any margin.** It is a gap in the harness, not a gap in the hardware, and shrinking the triangle number was never the right instrument for it. The **25% of the frame left unspent at 10000 triangles** — 12.58 ms GPU p95 against 16.667 — is what absorbs it. It is reserved, not spare, and must not be re-budgeted as capacity.

A budget derived from the ceiling looks authoritative and fails on the hardware the audience owns — worse than having no number, because it arrives after the art is paid for. That reasoning is why the ceiling tier still produces no budget, and it is unaffected by the floor change.

Take the device off charge and let it reach a steady thermal state before trusting anything. A cold phone on mains reports a device you do not ship to.

- [x] **Step 5: Retrieve the CSV**

Xcode → `Window → Devices and Simulators` → select the device → select the app → **Download Container** → inspect `AppData/Documents/entity-budget.csv`.

Copy it to `implementation/results/entity-budget.csv` (gitignored).

- [x] **Step 6: Record the budget in this plan document**

**A ceiling run records an upper bound and nothing else.** A proxy run produces a provisional budget after the 0.7 margin. Only a floor run produces a budget for production assets.

Find the highest triangle/bone/material combination whose row has `holds_60=1` **and** `under_600mb=1`. That is the per-creature budget.

Append to this file, filling in the measured values:

```markdown
## Phase 0 result — recorded <date>

**Device:** <model, iOS version — copy from the CSV's `# device=` line>
**Tier:** <floor (A14/4GB — budget is valid, no margin) | ceiling (budget NOT valid, upper bound only)>
**Per-creature budget at 104 entities, 60 fps, under 600 MB:**

| | Budget |
|---|---|
| Triangles | <value> |
| Bones | <value> |
| Materials | <value> |
| Measured p95 | <value> ms |
| Measured peak memory | <value> MB |

**Verdict:** <PASS — the modular pipeline is viable at this budget> or
<FAIL at 60 fps; holds 30 fps at <value>> or <FAIL both — the renderer
needs instancing or impostors before art begins>.
```

- [x] **Step 7: Commit**

```bash
git add client/Assets/Benchmark/ client/Assets/Scenes/Benchmark.unity implementation/
git commit -m "feat: entity-count sweep and the per-creature render budget

Sweeps triangle, bone and material counts at wave 44's 104 entities on
the A13 reference device and records the highest combination that holds
60 fps under 600 MB. vSync is off and targetFrameRate is 60 so
unscaledDeltaTime measures real frame cost rather than the vSync
interval.

The budget is an input to the rig proof commission, which currently has
no polygon or bone line in its deliverables."
```

---

### Task 6: Add the budget to the rig proof brief

The proof is only useful if it reaches the artist. `specs/broodline_rig_proof.md` §8 lists deliverables with no cost constraint, and §9.4 notes the proof has no stated duration.

**Files:**
- Modify: `specs/broodline_rig_proof.md` (§8 The commission)

**Interfaces:**
- Consumes: the budget table recorded in Task 5 Step 6.
- Produces: a commission brief an artist can hit or reject on cost grounds.

- [x] **Step 1: Add a budget subsection to §8**

> **The block below is what was inserted on 2026-09-10 and is NOT what `rig_proof` §8.3 says today.** The floor change of 2026-09-14 withdrew the margin and replaced the A13 re-measure instruction with the real-mesh re-run. It is reproduced unaltered because this is the record of what that commit did. **Read the live text in `specs/broodline_rig_proof.md` §8.3, never this copy.**

Insert immediately before the `**Deliverables:**` line, substituting the measured numbers:

```markdown
**Per-asset budget — PROVISIONAL.** Measured on an **A14 / 4 GB proxy** (iPad Air 4), not on the A13 / 3 GB reference device, then reduced by 30% to cover the gap. Source and reasoning: `implementation/2026-09-08-phase0-entity-count-proof.md`.

**This is sufficient for the rig proof and not for production.** §7 of this document makes the proof's two bodies throwaway — *"They will be remade"* — and §7 also states the proof is not a performance test. A provisional budget is therefore adequate to commission it. **Re-measure on an A13 / 3 GB device and replace these numbers before any of the remaining four species are modelled.**

| | Budget | Why |
|---|---|---|
| Triangles per body | <value> | Wave 44 puts 104 entities on screen at once |
| Bones per rig | <value> | Skinning is the dominant per-entity CPU cost |
| Materials per body | <value> | Each material is a draw call before batching |
| Trait part | Within the body budget, not additional | A body carries two parts and an Instinct cue |

Measured on an A13 / 3 GB device — iPhone 11, iPhone SE (2020), iPad 9th
gen — at 60 fps and under 600 MB. A body over budget is not a rejection
of the art; it is a request to hit the number, and it is far cheaper to
hear now than after six bodies are final.
```

- [x] **Step 2: Give the proof a duration, closing §9.4**

Replace open question 4 with:

```markdown
4. ~~**The proof has no stated duration.**~~ **Resolved — three weeks**, per
   `broodline_whats_left.md` §2, which already records "the rig proof runs
   three weeks" as a taken decision. A gate with no deadline is not a gate.
```

- [x] **Step 3: Verify no other document contradicts the budget**

```bash
cd "$(git rev-parse --show-toplevel)"
grep -rn -iE "triangle|poly ?count|bone count|draw call" specs/*.md specs/plans/*.md | grep -v rig_proof
```

Expected: hits only in `broodline_client_architecture.md`, which is where the budget's rationale lives. Any other document naming a different number is a contradiction to reconcile before briefing.

- [x] **Step 4: Commit**

```bash
git add specs/broodline_rig_proof.md
git commit -m "docs: give the rig proof commission a measured asset budget

Section 8's deliverables listed assets and renders with no cost
constraint, so an artist could deliver 48 correct renders of bodies too
expensive to ship. The budget comes from the Phase 0 entity-count proof
on the A13 reference device.

Closes open question 9.4 — the proof runs three weeks, which whats_left
section 2 already recorded as a taken decision."
```

---

## Phase 0 result — recorded 2026-09-10

**Device:** `iPad13,1` — iPad Air (4th generation), Apple A14 GPU, 3868 MB, iPadOS 27.0, Unity 6000.6.0f1.
**Tier:** **floor** (A14 / 4 GB), as of the 2026-09-14 reference-device change. **MEASURED — valid for production assets**, subject only to the real-mesh re-run booked in `rig_proof` §8.3. Recorded on 2026-09-10 as *proxy / PROVISIONAL*: **the measurement never changed, the floor did.**
**Source:** `implementation/results/entity-budget-ipadair4-run4-animated-bones.csv` — 40 combinations, 300 measured frames each, rigs animated every frame.

**Per-creature budget at 104 entities, 60 fps, under 600 MB:**

| | Measured | Budget |
|---|---|---|
| Triangles | 10000 | **10000** — no margin. The device measured *is* the floor |
| Bones | 80, **no ceiling found** | **not a constraint — see below** |
| Materials | 2 | **2** |
| p95 cost at (10000, 80, 2) | 11.64 ms (GPU-bound) | |
| **p95 cost, worst 10000-triangle row** — (10000, 12, 2) | **12.58 ms of a 16.667 ms frame** | **the binding measurement; ~25% of frame unspent** |
| p95 main thread at (10000, 80, 2) | 6.37 ms | |
| Peak memory | 394 MB of 600 | |

**Cite 12.58 ms, not 11.64 ms, when the question is whether the budget fits.** 11.64 is the row at the highest bone count; 12.58 is the worst row anywhere at 10000 triangles, and it is the one the budget has to survive.

**Verdict: PASS.** Every combination at or below 10000 triangles holds 60 fps under 600 MB at every bone count swept. Every combination at 16000 triangles fails: GPU p95 19.2–20.1 ms against a 16.667 ms frame, wall clock 33.4 ms as the device falls to 30 fps, 526 MB peak. The ceiling sits between 10000 and 16000 triangles and is **GPU-bound, not CPU-bound or bone-bound**.

### Bones are not the constraint, and now that is measured rather than assumed

Task 5 premised the bone axis on *"skinning is the dominant per-entity CPU cost."* With the rigs actually animated, that premise is false on this hardware. Main-thread p95 in milliseconds, triangle count held constant:

| triangles | 12 bones | 24 | 48 | 80 | 12 → 80 |
|---|---|---|---|---|---|
| 400 | 6.21 | 5.85 | 6.04 | 6.50 | +0.29 |
| 1500 | 5.94 | 6.26 | 6.04 | 6.33 | +0.39 |
| 6000 | 5.82 | 5.72 | 5.90 | 6.17 | +0.36 |
| 10000 | 5.69 | 5.69 | 5.88 | 6.34 | +0.65 |
| 16000 | 6.67 | 7.63 | 7.46 | 7.41 | +0.74 |

Nearly seven times the bones costs **0.3–0.7 ms of main thread across all 104 entities**, against a 16.667 ms frame. The effect is small but it is real: positive at every triangle count, which is what separates it from the previous run's exact zeros. Peak memory moves 318 → 323 MB over the same range.

**80 bones passed, so the bone ceiling was not found.** Applying a margin to an unfound ceiling would have invented a limit rather than recorded one, so the brief states that bones are unconstrained at the counts a creature rig plausibly needs, and gives the measured slope instead of a number. The 2026-09-14 withdrawal of the margin changes nothing here — there was never a bone number for it to reduce.

**Why it is cheap:** `gpuSkinning` is enabled, so per-vertex skinning is GPU work that scales with vertices rather than with rig complexity, and Unity's transform hierarchy update is jobified off the main thread. What remains on the main thread is the per-bone write itself.

**What this still does not measure:** `BoneAnimator` writes local rotations directly. A real `Animator` also evaluates a graph, samples curves and blends clips, none of which happen here. **This is the floor of a rig's per-frame cost, not its total.** A rig that is expensive to *evaluate* rather than expensive to *skin* is still unmeasured.

### The memory figure is pessimistic, and the binding constraint is not memory

`SyntheticCreature.Build` calls `new Mesh()` per entity, so the sweep holds **104 unique meshes**. Wave 44 does not. `broodline_raider_roster.md` §3 shares one Runner mesh across all sixty Skirmishers, and one Segment mesh across the Brood, its three Broodlings and their nine Mites at 0.55× and 0.3× scale — thirty-nine entities, one asset. With five creatures on top, the real wave draws its 104 entities from roughly **seven** unique meshes.

Taking the mesh-memory slope from this run's own rows at 12 bones and 1 material — 176.8 MB at 400 triangles to 389.2 MB at 10000 — about 212 MB of the peak is mesh data and about 168 MB is everything else. At seven unique meshes that term falls to roughly 14 MB:

| | Peak at 10000 triangles |
|---|---|
| 104 unique meshes (what the sweep measures) | ~390 MB |
| ~7 unique meshes (what wave 44 actually draws) | **~183 MB** |

So the 600 MB ceiling is not close, and a reader seeing "394 of 600" should not conclude that two thirds of the memory budget is spent. **GPU cost is unaffected** — 104 × 10000 triangles are rasterised whether or not the meshes are shared — which is why the triangle ceiling is where the constraint actually sits and why sharing does not buy a larger triangle budget.

This also means the sweep is a *conservative* memory test rather than a representative one. Left as is deliberately: a proof that overstates memory and gets the binding constraint right is the safe direction to be wrong in.

### The triangle figure held across the change

Run 2 measured a static rig and put the ceiling between 10000 and 16000 triangles. Run 4 animates every bone up to 80 and puts it in exactly the same place. The triangle budget is therefore robust to the defect that invalidated the bone figure — which is why the published number did not move when run 4 replaced run 2. It moved later, from 7000 to 10000, and for an unrelated reason: the floor changed and the margin was withdrawn. **The measurement behind it has been the same since run 2.**

### Runs that produced no budget, and why

Recorded so the same ground is not re-covered:

1. **iPhone 15 Pro, ceiling tier** (`entity-budget-iphone15pro.csv`) — every row 16.67 ms from 400 to 6000 triangles. `Time.unscaledDeltaTime` reports the frame-rate cap, not the cost.
2. **iPad Air 4, run 1** (`entity-budget-ipadair4-run1-capped-cpu.csv`) — GPU and memory valid, CPU flat at ~16.8 ms. `cpuFrameTime` includes the main thread's block on present, so it reported the same cap again.
3. **iPad Air 4, run 2** (`entity-budget-ipadair4-run2.csv`) — CPU clamped to 0.00 by subtracting `cpuMainThreadPresentWaitTime` from `cpuMainThreadFrameTime`; those fields partition the frame rather than nesting. The triangle and material budget was recovered from this run's raw component columns without a further device trip. Its bone figure was never valid: nothing animated the rigs.
4. **iPad Air 4, run 3** — suspended partway. iOS locked the screen on an unplugged device and the app stopped without failing. `Screen.sleepTimeout` now holds it awake, and the CSV is written after every combination so a stalled run is visible rather than indistinguishable from a slow one.
5. **iPad Air 4, run 4 — this one.** Combinations 1–16 (400 and 1500 triangles) ran on mains; the device was unplugged for the remaining 24, which include every 6000, 10000 and 16000 triangle row. **Both rows that set the ceiling — 10000 passing and 16000 failing — were measured on battery.** The mains rows are the light end and are not the binding constraint.

## What this plan deliberately does not do

- **No simulation code.** The engine is Phase 1: the `netstandard2.1` library, the banned-API analyzer, the Cecil float scan, and the four determinism test layers. Entities here are render-only stand-ins with no tick loop.
- **No real art.** Synthetic meshes are the point — they let the proof run before the commission and let its output shape the brief.
- **No degradation ladder.** `client_architecture` §4's instancing and animation-rate rungs are Phase 3 work. This measures the *unoptimised* cost, which is the number the art budget must be set against.
- **It does not run on real meshes, and that clause has moved rather than closed.** `client_architecture` §4 required the proof to run "with real creature meshes rather than capsules." Synthetic meshes cannot satisfy that — they establish the budget the commission is briefed with, which is the only order these two can happen in. **That run — `SyntheticCreature.Build` swapped for the delivered Vetch and Pale, every threshold unchanged — now lives in `rig_proof` §8.3 and gates production species, not this phase.** It is one task for whichever plan follows the commission. **Phase 0 is signed off without it, deliberately**: waiting would have blocked an engineering phase on a procurement item that has not been started.

## If the proof fails

Per `client_architecture` §4, a failure changes the renderer rather than the schedule:

- **Fails 60 fps, holds 30** — take the locked-30 option from `client_architecture` §3 and re-run the sweep against the 33.333 ms threshold for a larger budget.
- **Fails both** — the naive path is dead and crowd rendering is required before art begins: GPU instancing for identical raiders, or baked vertex-animation textures instead of skinned meshes. Re-run with those before briefing the commission.
- **Fails on memory only** — the triangle budget is fine and texture resolution is the problem, which is a `client_architecture` §6 packaging question rather than a rig question.
