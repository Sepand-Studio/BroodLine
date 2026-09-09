# Phase 0 — Entity-Count Proof Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a per-creature rendering budget — triangles, bones, materials — under which wave 44's ~100 entities hold 60 fps and stay inside 600 MB on an A13 / 3 GB device, so the rig proof commission can be briefed with a number instead of a hope.

**Architecture:** A Unity 6 project containing one benchmark scene that spawns **procedurally generated synthetic skinned meshes** at wave 44's entity counts, sweeps triangle/bone/material parameters, and records frame-time percentiles and peak memory. Synthetic geometry rather than real art, so the proof runs before any asset exists and its output constrains the art brief rather than waiting on it. The same harness re-runs against real meshes later as validation.

**Tech Stack:** Unity **6000.6.0f1**, C#, URP, Unity Test Framework, IL2CPP / ARM64 / Metal, Git LFS, Xcode.

## Global Constraints

Copied verbatim from `specs/plans/broodline_client_architecture.md`:

- **Reference device: A13 / 3 GB** — iPhone 11, iPhone SE (2020), iPad 9th gen.
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

- [ ] **Step 1: Run the check**

```bash
./implementation/scripts/verify-prereqs.sh
```

It reports git-lfs, Xcode, and every installed Unity editor together with whether that editor carries the **iOS Build Support** module. An editor without the module passes a naive "is Unity installed" check and then fails at Build Settings, which is why the script looks for `PlaybackEngines/iOSSupport` specifically rather than for the editor alone.

- [ ] **Step 2: Install git-lfs if it is missing**

```bash
brew install git-lfs && git lfs install
```

`git lfs install` writes the global hooks; without it the filters in `.gitattributes` are inert and binaries commit as plain blobs.

- [ ] **Step 3: Install Unity if it is missing**

This is a GUI flow — the Hub's headless CLI needs an interactive sign-in, so it cannot be scripted from here.

1. Open **Unity Hub** and sign in
2. **Installs → Install Editor → Unity 6 LTS** (the newest `6000.x` marked LTS)
3. In the module list, tick **iOS Build Support**. Nothing in this plan works without it
4. Wait for the download — it is several gigabytes

- [ ] **Step 4: Re-run the check until it exits 0**

```bash
./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: three `ok` lines and `exit=0`.

- [ ] **Step 5: Pin the Unity version in this plan**

Determinism work later depends on knowing which editor produced a build. Record the installed version in the Tech Stack line at the top of this document, replacing `Unity 6 (version pinned at Task 0)` with the exact version string, for example `Unity 6000.0.32f1`.

- [ ] **Step 6: Commit**

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

- [ ] **Step 1: Confirm the toolchain is ready**

```bash
./implementation/scripts/verify-prereqs.sh
```

Expected: exit 0. Task 0 covers installation; this is the gate.

- [ ] **Step 2: Write `.gitattributes`**

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

- [ ] **Step 3: Verify the LFS routing before committing anything binary**

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

- [ ] **Step 4: Write `.gitignore`**

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

- [ ] **Step 5: Write `implementation/README.md`**

```markdown
# Implementation

Task plans and their outputs. Design and architecture live in `specs/`.

- `2026-09-08-phase0-entity-count-proof.md` — the render-budget proof that
  briefs the rig proof commission.
- `results/` — benchmark output, gitignored. Findings are promoted into the
  plan document itself.
```

- [ ] **Step 6: Commit**

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

- [ ] **Step 1: Create the Unity project**

Install Unity 6 LTS through Unity Hub with the **iOS Build Support** module. Then create a project:

- Template: **Universal 3D** (URP)
- Name: `client`
- Location: the repository root

- [ ] **Step 2: Set the three settings in the editor**

- `Edit → Project Settings → Editor → Asset Serialization → Mode: **Force Text**`
- `Edit → Project Settings → Editor → Version Control → Mode: **Visible Meta Files**`
- `File → Build Settings → Platform: **iOS** → Switch Platform`
- `Edit → Project Settings → Player → Other Settings`:
  - Scripting Backend: **IL2CPP**
  - Target Architectures: **ARM64**
  - `Edit → Project Settings → Player → Resolution and Presentation`: **Default Orientation: Portrait**, all other orientations unchecked

- [ ] **Step 3: Write the verification script**

```bash
#!/usr/bin/env bash
# implementation/scripts/verify-unity-settings.sh
# Asserts the Unity settings that are expensive to discover late.
set -euo pipefail
cd "$(dirname "$0")/../.."
fail=0
check () { # name file pattern
  if grep -qE "$3" "$2"; then
    printf '  ok    %s\n' "$1"
  else
    printf '  FAIL  %s  (expected /%s/ in %s)\n' "$1" "$3" "$2"; fail=1
  fi
}
E=client/ProjectSettings/EditorSettings.asset
P=client/ProjectSettings/ProjectSettings.asset
check "Asset Serialization: Force Text"  "$E" 'm_SerializationMode: 2'
check "Version Control: Visible Meta"    "$E" 'm_ExternalVersionControlSupport: Visible Meta Files'
check "Portrait default orientation"     "$P" 'defaultScreenOrientation: 0'
check "Portrait upside-down disabled"    "$P" 'allowedAutorotateToPortraitUpsideDown: 0'
check "Landscape left disabled"          "$P" 'allowedAutorotateToLandscapeLeft: 0'
check "Landscape right disabled"         "$P" 'allowedAutorotateToLandscapeRight: 0'
exit $fail
```

Note on `defaultScreenOrientation: 0` — Unity serialises Portrait as `0`. If your Unity version writes a different value, read the file after setting Portrait in the editor and use what it actually wrote. Assert the observed value, never a guessed one.

- [ ] **Step 4: Run it and confirm every line passes**

```bash
chmod +x implementation/scripts/verify-unity-settings.sh
./implementation/scripts/verify-unity-settings.sh
```

Expected: six `ok` lines, exit status 0. Any `FAIL` means the editor setting did not take — fix it in Unity and re-run rather than editing the `.asset` by hand.

- [ ] **Step 5: Commit**

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

- [ ] **Step 1: Create the assembly definitions**

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
  "optionalUnityReferences": ["TestAssemblies"],
  "includePlatforms": []
}
```

- [ ] **Step 2: Write the failing test**

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

- [ ] **Step 3: Run it and confirm it fails**

In Unity: `Window → General → Test Runner → EditMode → Run All`.

Expected: compile error — `SyntheticCreature` and `SyntheticCreatureSpec` do not exist. A compile failure is a valid failing state here; do not proceed until you have seen it.

- [ ] **Step 4: Write the implementation**

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

- [ ] **Step 5: Run the test and confirm it passes**

`Test Runner → EditMode → Run All`. Expected: `Build_ProducesRequestedTriangleBoneAndMaterialCounts` **PASS**.

If the triangle count is short, the submesh remainder distribution is wrong — the last submesh takes `spec.Triangles - cursor`, not `perSub`.

- [ ] **Step 6: Commit**

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

- [ ] **Step 1: Write the failing test**

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

- [ ] **Step 2: Run and confirm failure**

`Test Runner → EditMode → Run All`. Expected: compile error — `WaveBenchmark` and `BenchmarkResult` do not exist.

- [ ] **Step 3: Write `BenchmarkResult.cs`**

```csharp
using System.Globalization;

namespace Broodline.Benchmark
{
    public struct BenchmarkResult
    {
        public SyntheticCreatureSpec Spec;
        public int EntityCount;
        public double MedianMs;
        public double P95Ms;
        public long PeakMemoryBytes;

        public static string CsvHeader =>
            "triangles,bones,materials,entities,median_ms,p95_ms,peak_mb,holds_60,holds_30,under_600mb";

        public bool Holds60 => P95Ms <= 16.6;
        public bool Holds30 => P95Ms <= 33.3;
        public bool UnderMemoryCeiling => PeakMemoryBytes <= 600L * 1024 * 1024;

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                Spec.Triangles.ToString(c),
                Spec.Bones.ToString(c),
                Spec.Materials.ToString(c),
                EntityCount.ToString(c),
                MedianMs.ToString("F2", c),
                P95Ms.ToString("F2", c),
                (PeakMemoryBytes / (1024.0 * 1024.0)).ToString("F1", c),
                Holds60 ? "1" : "0",
                Holds30 ? "1" : "0",
                UnderMemoryCeiling ? "1" : "0");
        }
    }
}
```

Both thresholds come from the Global Constraints: 16.6 ms is the 60 fps frame budget, 33.3 ms the 30 fps fallback, 600 MB the memory ceiling on a 3 GB device.

- [ ] **Step 4: Write `WaveBenchmark.cs`**

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

- [ ] **Step 5: Run the tests and confirm they pass**

`Test Runner → EditMode → Run All`. Expected: all three **PASS**.

- [ ] **Step 6: Commit**

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

- [ ] **Step 1: Write `SweepRunner.cs`**

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

                foreach (var go in spawned) Destroy(go);
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

`Time.unscaledDeltaTime` with vSync off and `targetFrameRate` at 60 measures the real frame interval. Leave vSync on and every row reads 16.6 ms regardless of load, which is the classic way to get a meaningless benchmark.

`Profiler.GetTotalAllocatedMemoryLong()` already includes the managed heap, so do **not** add `GC.GetTotalMemory` to it — that double-counts and will make every row fail the 600 MB check for no reason.

- [ ] **Step 2: Build the scene**

- `File → New Scene`, save as `client/Assets/Scenes/Benchmark.unity`
- Add an empty GameObject named `SweepRunner`, attach the `SweepRunner` component
- Position the Main Camera to frame roughly a 12×12 metre area at origin — every entity must be **on screen and drawn**, or the benchmark measures culling instead of rendering
- `Edit → Project Settings → Graphics → Always Included Shaders`: add **Universal Render Pipeline/Lit**. `Shader.Find` only resolves shaders present in the build, and nothing in this scene references that shader at build time — without this the device run renders magenta and the numbers are meaningless
- Add the scene to `File → Build Settings → Scenes In Build` as index 0

- [ ] **Step 3: Run it in the editor as a smoke test**

Press Play. Expected: `[sweep]` lines in the Console, one per combination, ending in `[sweep] COMPLETE`.

**Editor numbers are not the result** — they measure your Mac. This step only proves the harness runs to completion without throwing.

- [ ] **Step 4: Build and run on the reference device**

Connect an **A13 / 3 GB device** — iPhone 11, iPhone SE (2020) or iPad 9th gen. `File → Build And Run`. Watch the Xcode console for `[sweep]` lines.

Take the device off charge and let it reach a steady thermal state before trusting the numbers. A benchmark run on a cold phone plugged into mains reports a device you do not ship to.

- [ ] **Step 5: Retrieve the CSV**

Xcode → `Window → Devices and Simulators` → select the device → select the app → **Download Container** → inspect `AppData/Documents/entity-budget.csv`.

Copy it to `implementation/results/entity-budget.csv` (gitignored).

- [ ] **Step 6: Record the budget in this plan document**

Find the highest triangle/bone/material combination whose row has `holds_60=1` **and** `under_600mb=1`. That is the per-creature budget.

Append to this file, filling in the measured values:

```markdown
## Phase 0 result — recorded <date>

**Device:** <model, iOS version>
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

- [ ] **Step 7: Commit**

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

- [ ] **Step 1: Add a budget subsection to §8**

Insert immediately before the `**Deliverables:**` line, substituting the measured numbers:

```markdown
**Per-asset budget**, measured rather than assumed — `implementation/2026-09-08-phase0-entity-count-proof.md`:

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

- [ ] **Step 2: Give the proof a duration, closing §9.4**

Replace open question 4 with:

```markdown
4. ~~**The proof has no stated duration.**~~ **Resolved — three weeks**, per
   `broodline_whats_left.md` §2, which already records "the rig proof runs
   three weeks" as a taken decision. A gate with no deadline is not a gate.
```

- [ ] **Step 3: Verify no other document contradicts the budget**

```bash
cd "$(git rev-parse --show-toplevel)"
grep -rn -iE "triangle|poly ?count|bone count|draw call" specs/*.md specs/plans/*.md | grep -v rig_proof
```

Expected: hits only in `broodline_client_architecture.md`, which is where the budget's rationale lives. Any other document naming a different number is a contradiction to reconcile before briefing.

- [ ] **Step 4: Commit**

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

## What this plan deliberately does not do

- **No simulation code.** The engine is Phase 1: the `netstandard2.1` library, the banned-API analyzer, the Cecil float scan, and the four determinism test layers. Entities here are render-only stand-ins with no tick loop.
- **No real art.** Synthetic meshes are the point — they let the proof run before the commission and let its output shape the brief.
- **No degradation ladder.** `client_architecture` §4's instancing and animation-rate rungs are Phase 3 work. This measures the *unoptimised* cost, which is the number the art budget must be set against.
- **This is not yet §4's gate.** `client_architecture` §4 requires the proof to run "with real creature meshes rather than capsules." Synthetic meshes cannot satisfy that — they establish the budget the commission is briefed with. **The gate itself closes when the rig proof delivers Vetch and Pale and this same harness re-runs against them**, with `SyntheticCreature.Build` swapped for the delivered prefabs and every threshold unchanged. That run is one task, and it belongs to whichever plan follows the commission. Until it happens, Phase 0 is not signed off.

## If the proof fails

Per `client_architecture` §4, a failure changes the renderer rather than the schedule:

- **Fails 60 fps, holds 30** — take the locked-30 option from `client_architecture` §3 and re-run the sweep against the 33.3 ms threshold for a larger budget.
- **Fails both** — the naive path is dead and crowd rendering is required before art begins: GPU instancing for identical raiders, or baked vertex-animation textures instead of skinned meshes. Re-run with those before briefing the commission.
- **Fails on memory only** — the triangle budget is fine and texture resolution is the problem, which is a `client_architecture` §6 packaging question rather than a rig question.
