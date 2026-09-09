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
