using NUnit.Framework;
using UnityEngine;
using Broodline.Benchmark;
using Broodline.View;

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

    [Test]
    public void Build_AttachesAnAnimatorBoundToEveryBone()
    {
        // A rig nothing drives is a rig whose bone count costs nothing, which
        // is how the first iPad Air 4 sweep reported 12 and 48 bones as
        // identical at every triangle count.
        var spec = new SyntheticCreatureSpec { Triangles = 300, Bones = 16, Materials = 1 };
        var go = SyntheticCreature.Build(spec);

        var animator = go.GetComponent<BoneAnimator>();
        Assert.IsNotNull(animator, "a synthetic creature must carry something that moves its rig");

        var smr = go.GetComponent<SkinnedMeshRenderer>();
        var before = new Quaternion[smr.bones.Length];
        for (int i = 0; i < smr.bones.Length; i++) before[i] = smr.bones[i].localRotation;

        animator.Tick(1.0f);

        int moved = 0;
        for (int i = 0; i < smr.bones.Length; i++)
            if (Quaternion.Angle(before[i], smr.bones[i].localRotation) > 0.01f) moved++;

        Assert.AreEqual(smr.bones.Length, moved, "every bone must be driven, not just the root");
        Object.DestroyImmediate(go);
    }

    [Test]
    public void Build_InflatesBoundsSoAnAnimatedRigIsNotCulled()
    {
        // Bounds come from the rest pose. If they are left tight, an animated
        // renderer leaves them and is culled mid-frustum, silently removing
        // entities from a benchmark that exists to count them.
        var spec = new SyntheticCreatureSpec { Triangles = 300, Bones = 16, Materials = 1 };
        var go = SyntheticCreature.Build(spec);
        var smr = go.GetComponent<SkinnedMeshRenderer>();

        var rest = smr.sharedMesh.bounds;
        Assert.Greater(smr.localBounds.extents.magnitude, rest.extents.magnitude,
            "localBounds must exceed the rest pose the mesh was built in");
        Object.DestroyImmediate(go);
    }
}
