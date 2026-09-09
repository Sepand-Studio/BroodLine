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
