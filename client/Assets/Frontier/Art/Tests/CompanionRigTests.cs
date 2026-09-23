using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Broodline.Frontier.Art.Tests
{
    public sealed class CompanionRigTests
    {
        [Test]
        public void EveryTraitPairBakesAtBothGrowthEndpointsWithinBudget()
        {
            var root = new GameObject("companion combinations");
            var baked = new Mesh();
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            try
            {
                var traits = new[] { "", "cinder", "carapace", "chill", "taunt", "splash" };
                foreach (var species in FrontierRigDefinition.Companions)
                foreach (var dorsal in traits)
                foreach (var flank in traits)
                {
                    var actor = art.Creature(root.transform, species, dorsal, flank);
                    var renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                    int triangles = renderer.sharedMesh.triangles.Length / 3;
                    foreach (var part in actor.GetComponentsInChildren<MeshFilter>())
                        triangles += part.sharedMesh.triangles.Length / 3;
                    Assert.That(triangles, Is.LessThanOrEqualTo(10000), species + dorsal + flank);
                    foreach (float growth in new[] { 0f, 1f })
                    {
                        actor.Growth = growth;
                        actor.Moving = true;
                        actor.Pose(.22f);
                        renderer.BakeMesh(baked);
                        Assert.That(baked.vertexCount, Is.EqualTo(renderer.sharedMesh.vertexCount));
                        foreach (var v in baked.vertices)
                            Assert.That(float.IsNaN(v.sqrMagnitude) || float.IsInfinity(v.sqrMagnitude), Is.False);
                    }
                    Object.DestroyImmediate(actor.gameObject);
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(root); }
        }

        [Test]
        public void LoamCombatSocketsFollowTheMiddleSegmentIndependentlyOfRoot()
        {
            var root = new GameObject("loam socket regression");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            try
            {
                var actor = art.Creature(root.transform, "loam", "cinder", "splash");
                Assert.That(actor.Rig.Dorsal.Bone, Is.EqualTo(actor.Rig.Flank.Bone));
                Assert.That(actor.Rig.Dorsal.Bone, Is.Not.EqualTo(0));
                var middle = actor.Bones[actor.Rig.Dorsal.Bone];
                var rootPosition = actor.Bones[0].position;
                foreach (var socket in new[] { actor.Dorsal, actor.Flank })
                {
                    Assert.That(socket.parent, Is.SameAs(middle));
                    var before = socket.position;
                    middle.localPosition += Vector3.up * .2f;
                    Assert.That(Vector3.Distance(before, socket.position), Is.GreaterThan(.19f));
                }
                Assert.That(actor.Bones[0].position, Is.EqualTo(rootPosition));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RepeatedSpeciesSwitchingReusesAndDisposesNativeAssets()
        {
            var root = new GameObject("studio lifecycle");
            var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface"));
            var meshes = new Mesh[FrontierRigDefinition.Companions.Length];
            Material material = null;
            try
            {
                for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < meshes.Length; i++)
                {
                    var actor = art.Creature(root.transform, FrontierRigDefinition.Companions[i]);
                    var renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (pass == 0) meshes[i] = renderer.sharedMesh;
                    else Assert.That(renderer.sharedMesh, Is.SameAs(meshes[i]));
                    material = renderer.sharedMaterial;
                    Object.DestroyImmediate(actor.gameObject);
                }
            }
            finally { Object.DestroyImmediate(root); art.Dispose(); }
            foreach (var mesh in meshes) Assert.That(mesh == null, Is.True, "cached mesh leaked");
            Assert.That(material == null, Is.True, "shared material leaked");
        }
    }
}
