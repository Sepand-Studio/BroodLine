using NUnit.Framework;
using UnityEngine;
using Broodline.Benchmark;
using Broodline.View;

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
            EntityCount = 104, MainThreadP95Ms = 6.2, PresentWaitP95Ms = 10.4, RenderThreadP95Ms = 4.1, GpuP95Ms = 11.0,
            WallP95Ms = 16.1, PeakMemoryBytes = 400L * 1024 * 1024, TimingSamples = 300
        };
        Assert.AreEqual(
            BenchmarkResult.CsvHeader.Split(',').Length,
            r.ToCsvRow().Split(',').Length);
    }

    [Test]
    public void Holds60_UsesRealFrameBudgetNotHardcoded16Point6()
    {
        // One frame at 60 fps is 1000/60 = 16.667 ms. A 16.6 ceiling would make
        // a flawless 60 fps read as failure, which is the bug this fix removes.
        var atBudget = new BenchmarkResult
        {
            MainThreadP95Ms = 16.6, GpuP95Ms = 16.6, TimingSamples = 300
        };
        Assert.IsTrue(atBudget.Holds60, "16.6 ms is within the real 16.667 ms budget");

        var overBudget = new BenchmarkResult
        {
            MainThreadP95Ms = 16.7, GpuP95Ms = 16.7, TimingSamples = 300
        };
        Assert.IsFalse(overBudget.Holds60, "16.7 ms exceeds the real 16.667 ms budget");
    }

    [Test]
    public void Holds60_IsFalseWhenFrameTimingManagerReportedNothing()
    {
        // The dangerous case: no samples means both percentiles are 0, and
        // `0 <= 16.667` would otherwise mark every triangle count a pass. A
        // sweep that measured nothing must not read as a sweep that passed.
        var noTimings = new BenchmarkResult
        {
            MainThreadP95Ms = 0, GpuP95Ms = 0, TimingSamples = 0
        };
        Assert.IsFalse(noTimings.TimingValid, "zero samples is not a valid measurement");
        Assert.IsFalse(noTimings.Holds60, "a row with no timing must never claim 60 fps");
        Assert.IsFalse(noTimings.Holds30, "a row with no timing must never claim 30 fps");
    }

    [Test]
    public void Holds60_IsFalseWhenOnlyTheGpuHalfIsMissing()
    {
        // FrameTimingManager can hand back real CPU times and a flat 0 GPU time.
        // Judging on the CPU alone would understate the cost of exactly the
        // work this proof exists to bound.
        var noGpu = new BenchmarkResult
        {
            MainThreadP95Ms = 9.0, GpuP95Ms = 0, TimingSamples = 300
        };
        Assert.IsFalse(noGpu.TimingValid, "a 0 ms GPU frame is missing data, not fast work");
        Assert.IsFalse(noGpu.Holds60);
    }

    [Test]
    public void MainThreadTime_IsAlreadyExclusiveOfThePresentWait()
    {
        // Pins the relationship a device run cost us: main thread and present
        // wait PARTITION the frame, they do not nest. Subtracting one from the
        // other clamped every row of the iPad Air 4 sweep to 0.00 ms of CPU.
        // These are that run's own numbers at its two operating points.
        var atCap = new FrameSample { MainThreadMs = 6.03, PresentWaitMs = 12.52, WallMs = 16.75 };
        Assert.AreEqual(atCap.WallMs, atCap.MainThreadMs + atCap.PresentWaitMs, 2.0,
            "main + wait must account for the frame, which is what makes main the cost");

        var overBudget = new FrameSample { MainThreadMs = 7.05, PresentWaitMs = 27.47, WallMs = 33.50 };
        Assert.AreEqual(overBudget.WallMs, overBudget.MainThreadMs + overBudget.PresentWaitMs, 2.0,
            "the same partition holds when the frame misses 60 fps entirely");
    }

    [Test]
    public void CostP95_IsTheSlowestStageNotTheirSum()
    {
        // Main thread and GPU run concurrently, so the longer sets the frame
        // rate. The render thread is excluded even when it is the largest of
        // the three: its series carries a wait on the GPU rather than work.
        var r = new BenchmarkResult
        {
            MainThreadP95Ms = 4.0, RenderThreadP95Ms = 7.5,
            GpuP95Ms = 11.2, TimingSamples = 300
        };
        Assert.AreEqual(11.2, r.CostP95Ms, 0.001);
        Assert.IsTrue(r.Holds60, "11.2 ms is inside the 16.667 ms budget");
    }

    [Test]
    public void CostP95_IgnoresARenderThreadCarryingAWaitOnTheGpu()
    {
        // The 16000-triangle rows: render thread pinned at ~16.6 ms, the frame
        // interval itself, while the GPU reported 20 ms. Counting the render
        // thread would double-count GPU time that is already measured.
        var r = new BenchmarkResult
        {
            MainThreadP95Ms = 7.05, RenderThreadP95Ms = 16.68,
            GpuP95Ms = 20.06, TimingSamples = 300
        };
        Assert.AreEqual(20.06, r.CostP95Ms, 0.001, "the GPU is the binding stage here");
        Assert.IsFalse(r.Holds60);
    }
}
