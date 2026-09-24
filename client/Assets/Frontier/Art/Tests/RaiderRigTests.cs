using Broodline.Frontier;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Broodline.Frontier.Art.Tests
{
    public sealed class RaiderRigTests
    {
        [Test]
        public void EveryAuthoredRaiderHasAUsableKitSocketAndFinitePose()
        {
            var root = new GameObject("raider art test");
            var baked = new Mesh();
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            try
            {
                foreach (var id in FrontierRaiders.Ids)
                {
                    var definition = FrontierVisuals.For(id);
                    Assert.That(definition.Kind, Is.EqualTo(FrontierKind.Raider), id);
                    var actor = art.Creature(root.transform, id);
                    Assert.That(actor.Kit, Is.Not.Null, id);
                    Assert.That(actor.Kit.parent, Is.SameAs(actor.Bones[actor.Rig.Kit.Value.Bone]), id);
                    var renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
                    Assert.That(renderer.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(10000), id);
                    actor.Moving = true;
                    actor.Pose(.4f);
                    renderer.BakeMesh(baked);
                    foreach (var v in baked.vertices)
                        Assert.That(float.IsNaN(v.sqrMagnitude) || float.IsInfinity(v.sqrMagnitude), Is.False, id);
                    Object.DestroyImmediate(actor.gameObject);
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(root); }
        }

        [Test]
        public void VariantPairsKeepTheirThreatSilhouettesSeparate()
        {
            var root = new GameObject("raider variant test");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            try
            {
                var skirmisher = art.Creature(root.transform, "skirmisher");
                var courser = art.Creature(root.transform, "courser");
                var breaker = art.Creature(root.transform, "breaker");
                var bulwark = art.Creature(root.transform, "bulwark");
                var sunder = art.Creature(root.transform, "sunder");
                Assert.That(courser.PortraitBounds.size.x, Is.GreaterThan(skirmisher.PortraitBounds.size.x * 1.5f));
                Assert.That(bulwark.PortraitBounds.max.x, Is.GreaterThan(breaker.PortraitBounds.max.x));
                Assert.That(sunder.PortraitBounds.size.x, Is.GreaterThan(breaker.PortraitBounds.size.x * 1.3f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
