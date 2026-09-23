using System;
using Broodline.Sim.Combat;
using Broodline.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Broodline.Frontier.Tests
{
    public sealed class FrontierProofTests
    {
        [Test]
        public void NewlyCreatedActiveAndInactiveCreaturesApplyTheirFirstPoseWithoutErrors()
        {
            foreach (bool active in new[] { true, false })
            {
                var root = new GameObject("initialization regression");
                root.SetActive(active);
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
                {
                    try
                    {
                        var creature = art.Creature(root.transform, "vetch");
                        creature.Chilled = true;
                        Assert.DoesNotThrow(() => creature.Pose(0, true));
                        var renderer = creature.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        var properties = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(properties);
                        var tint = properties.GetColor(Shader.PropertyToID("_Tint"));
                        Assert.That(tint.r, Is.EqualTo(.65f).Within(.001f));
                        Assert.That(tint.g, Is.EqualTo(.85f).Within(.001f));
                        Assert.That(tint.b, Is.EqualTo(1f).Within(.001f));
                        LogAssert.NoUnexpectedReceived();
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
        }

        static void ValidMesh(Mesh mesh, bool checkWinding)
        {
            var vertices = mesh.vertices; var normals = mesh.normals; var indices = mesh.triangles;
            Assert.That(mesh.colors.Length, Is.EqualTo(vertices.Length));
            Assert.That(normals.Length, Is.EqualTo(vertices.Length));
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(float.IsNaN(vertices[i].sqrMagnitude) || float.IsInfinity(vertices[i].sqrMagnitude), Is.False);
                Assert.That(normals[i].magnitude, Is.EqualTo(1).Within(.001));
            }
            for (int i = 0; i < indices.Length; i += 3)
            {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                Assert.That(a, Is.InRange(0, vertices.Length - 1));
                Assert.That(b, Is.InRange(0, vertices.Length - 1));
                Assert.That(c, Is.InRange(0, vertices.Length - 1));
                var cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                Assert.That(cross.sqrMagnitude, Is.GreaterThan(1e-18f), "degenerate triangle " + i / 3);
                if (checkWinding) Assert.That(Vector3.Dot(cross, normals[a] + normals[b] + normals[c]), Is.GreaterThan(0), "inverted triangle " + i / 3);
            }
        }

        [Test]
        public void MeshPrimitivesHaveFiniteOutwardFaces()
        {
            var author = new FrontierMesh();
            author.Sphere(Vector3.zero, new Vector3(.7f, .4f, 1), Color.white);
            author.Box(new Vector3(3, 0, 0), new Vector3(1, 2, 3), Color.white, 31);
            author.Cone(new Vector3(6, 0, 0), new Vector3(6.2f, 1, .3f), .3f, .02f, Color.white);
            author.Wing(-1, Color.white); author.Wing(1, Color.white);
            var mesh = author.Finish("test");
            try { ValidMesh(mesh, true); } finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void ProofBodiesFitGeometryBudgetAndHaveValidBoneBindings()
        {
            var root = new GameObject("proof test");
            var shader = Shader.Find("Broodline/FrontierSurface");
            Assert.That(shader, Is.Not.Null);
            using (var art = new FrontierArt(shader))
            {
                try
                {
                    foreach (string species in new[] { "vetch", "ember", "pale", "courser", "lash", "skirmisher" })
                    {
                        var creature = art.Creature(root.transform, species, "cinder", "carapace");
                        var renderer = creature.GetComponentInChildren<SkinnedMeshRenderer>();
                        ValidMesh(renderer.sharedMesh, true);
                        int triangleCount = renderer.sharedMesh.triangles.Length / 3;
                        foreach (var filter in creature.GetComponentsInChildren<MeshFilter>()) triangleCount += filter.sharedMesh.triangles.Length / 3;
                        Assert.That(triangleCount, Is.LessThanOrEqualTo(10000), species);
                        Assert.That(renderer.sharedMesh.bindposes.Length, Is.EqualTo(creature.Bones.Length));
                        foreach (var weight in renderer.sharedMesh.boneWeights)
                        {
                            Assert.That(weight.boneIndex0, Is.InRange(0, creature.Bones.Length - 1));
                            Assert.That(weight.weight0, Is.EqualTo(1));
                        }
                        var vertices = renderer.sharedMesh.vertices;
                        var weights = renderer.sharedMesh.boneWeights;
                        var binds = renderer.sharedMesh.bindposes;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            int bone = weights[i].boneIndex0;
                            var bindVertex = renderer.transform.worldToLocalMatrix * creature.Bones[bone].localToWorldMatrix * binds[bone];
                            Assert.That(Vector3.Distance(bindVertex.MultiplyPoint3x4(vertices[i]), vertices[i]), Is.LessThan(.0001f), "rest pose changed a vertex");
                        }
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void TraitSlotsFollowTheirBonesAndRestPoseIsRepeatable()
        {
            var root = new GameObject("socket test");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            {
                try
                {
                    var creature = art.Creature(root.transform, "vetch", "cinder", "carapace");
                    Assert.That(creature.Dorsal.GetChild(0).name, Is.EqualTo("cinder"));
                    Assert.That(creature.Flank.GetChild(0).name, Is.EqualTo("carapace"));
                    Assert.That(creature.Dorsal.parent, Is.SameAs(creature.Bones[0]));
                    Assert.That(creature.Crown.parent, Is.SameAs(creature.Bones[1]));
                    var before = creature.Dorsal.position;
                    creature.Bones[0].localRotation = Quaternion.Euler(0, 0, 30);
                    Assert.That(Vector3.Distance(before, creature.Dorsal.position), Is.GreaterThan(.05f));
                    creature.Pose(0, true); var rotation = creature.Bones[0].localRotation;
                    creature.Pose(72, true); Assert.That(creature.Bones[0].localRotation, Is.EqualTo(rotation));
                    var renderer = creature.GetComponentInChildren<SkinnedMeshRenderer>();
                    var sameBody = art.Creature(root.transform, "vetch");
                    Assert.That(sameBody.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh, Is.SameAs(renderer.sharedMesh));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void RenderingSnapshotsDoNotChangeEitherWaveOutcome()
        {
            foreach (int id in new[] { 6, 7 })
            {
                var wave = WaveDef.ForId(id); var formation = FrontierFormation.Create(new[] { 0, 2, 4 });
                var direct = new SimRunner(wave, wave.Lane, formation, 6);
                while (direct.Step()) { }
                var observed = new SimRunner(wave, wave.Lane, formation, 6);
                var pair = new WavePair(observed); var clock = new WaveClock();
                var feedback = new FrontierBattleFeedback(observed); int cues = 0;
                int frames = 0;
                while (!clock.Terminated && frames++ < Stats.HardTickCap * 4)
                    clock.Advance(observed, frames % 3 == 0 ? .1 : 1.0 / 60, () => { pair.Advance(observed); feedback.Observe(observed, _ => cues++); });
                Assert.That(clock.Terminated, Is.True);
                Assert.That(cues, Is.GreaterThan(0));
                Assert.That(observed.Outcome.Hash, Is.EqualTo(direct.Outcome.Hash));
                Assert.That(pair.Current.Integrity, Is.EqualTo(observed.Integrity));
                Assert.That(pair.Current.Tick, Is.EqualTo(observed.Tick));
            }
        }

        [Test]
        public void FormationRejectsOverlapsAndUnknownPockets()
        {
            Assert.Throws<ArgumentException>(() => FrontierFormation.Create(new[] { 0, 0, 4 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => FrontierFormation.Create(new[] { 0, 2, 5 }));
            Assert.That(FrontierFormation.Create(new[] { 0, 2, 4 })[0].Trait1, Is.EqualTo(Trait.Taunt));
        }

        [Test]
        public void PlacementSwapsOccupantsAndInvalidInputDoesNotMutate()
        {
            var formation = new[] { 0, 2, 4 };
            Assert.That(FrontierFormation.Place(formation, 0, 2), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, formation);
            Assert.That(FrontierFormation.Place(formation, 0, 2), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => FrontierFormation.Place(formation, 0, 5));
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, formation);
            Assert.That(FrontierFormation.Place(formation, 2, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, formation);
        }

        [Test]
        public void FeedbackCapturesTerminalBreachOnlyOnce()
        {
            var wave = WaveDef.ForId(6);
            var runner = new SimRunner(wave, wave.Lane, Array.Empty<CreatureSpec>(), 6);
            var feedback = new FrontierBattleFeedback(runner); int breaches = 0, damage = 0;
            Action<FrontierCue> receive = cue => { if (cue.Kind == FrontierCueKind.Breach) breaches++; if (cue.Kind == FrontierCueKind.Damage) damage++; };
            while (!runner.Done) { runner.Step(); feedback.Observe(runner, receive); }
            Assert.That(breaches, Is.EqualTo(1)); Assert.That(damage, Is.EqualTo(0));
            feedback.Observe(runner, receive);
            Assert.That(breaches, Is.EqualTo(1));
        }

        [Test]
        public void BlinkBonesFollowHeadAndReducedMotionLeavesEyesOpen()
        {
            var root = new GameObject("blink test");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            {
                try
                {
                    var creature = art.Creature(root.transform, "vetch");
                    var left = creature.Bones[6]; var right = creature.Bones[7];
                    Assert.That(left.parent, Is.SameAs(creature.Bones[1]));
                    creature.Pose(3.18f); // middle of the deterministic blink interval
                    Assert.That(left.localScale.y, Is.LessThan(.1f));
                    Assert.That(right.localScale.y, Is.EqualTo(left.localScale.y));
                    creature.ReducedMotion = true; creature.Pose(3.18f);
                    Assert.That(left.localScale.y, Is.EqualTo(1));
                    creature.ReducedMotion = false; creature.Pose(3.18f, true);
                    Assert.That(left.localScale.y, Is.EqualTo(1));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
    }
}
