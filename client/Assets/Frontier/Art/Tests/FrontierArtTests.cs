using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Broodline.Frontier.Art.Tests
{
    /// The art core's own gate: geometry, budgets, bindings, sockets, blink,
    /// pause and reduced motion. Moved out of FrontierProofTests in Phase 10
    /// Task 1.0 when the art core became `Broodline.Frontier.Art`, an assembly
    /// with no references, so production (View, Game) can build creatures
    /// from it without pulling in the proof app.
    public sealed class FrontierArtTests
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
            var vertices = mesh.vertices; var normals = mesh.normals; var indices = mesh.triangles; var surface = mesh.uv;
            Assert.That(mesh.colors.Length, Is.EqualTo(vertices.Length));
            Assert.That(normals.Length, Is.EqualTo(vertices.Length));
            Assert.That(surface.Length, Is.EqualTo(vertices.Length), "surface polish channel missing");
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(float.IsNaN(vertices[i].sqrMagnitude) || float.IsInfinity(vertices[i].sqrMagnitude), Is.False);
                Assert.That(normals[i].magnitude, Is.EqualTo(1).Within(.001));
                Assert.That(surface[i].x, Is.InRange(0f, 1f));
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
            author.Sphere(new Vector3(9,0,0), new Vector3(.8f,.5f,.6f), Color.white, upperOnly: true);
            author.ShellPlate(new Vector3(12,0,0), new Vector3(.8f,.5f,.6f),
                new[] { new Vector2(.8f,0), new Vector2(0,-.7f), new Vector2(-.8f,0), new Vector2(0,.7f) }, Color.white, 3);
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
                        var portrait = creature.PortraitBounds;
                        portrait.Expand(.001f); // float round-off at attachment corners
                        foreach (var filter in creature.GetComponentsInChildren<MeshFilter>())
                        {
                            ValidMesh(filter.sharedMesh, true);
                            triangleCount += filter.sharedMesh.triangles.Length / 3;
                            foreach (var vertex in filter.sharedMesh.vertices)
                                Assert.That(portrait.Contains(creature.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex))), Is.True, "portrait clips an attachment");
                        }
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
                            Assert.That(portrait.Contains(vertices[i]), Is.True, "portrait clips body");
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

        [Test]
        public void PausePreservesPoseAndResumesAtTheSamePresentationTime()
        {
            var root = new GameObject("pause regression");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            {
                try
                {
                    var paused = art.Creature(root.transform, "ember");
                    var control = art.Creature(root.transform, "ember");
                    foreach (var actor in new[] { paused, control })
                    { actor.Greet(); actor.Attack(); actor.Hit(); actor.AdvancePresentation(.09f); }
                    var before = paused.Bones[1].localRotation;
                    paused.Paused = true; paused.AdvancePresentation(10);
                    Assert.That(Quaternion.Angle(paused.Bones[1].localRotation, before), Is.LessThan(.001f));
                    paused.Paused = false;
                    paused.AdvancePresentation(.04f); control.AdvancePresentation(.04f);
                    for (int i = 0; i < paused.Bones.Length; i++)
                    {
                        Assert.That(Vector3.Distance(paused.Bones[i].localPosition, control.Bones[i].localPosition), Is.LessThan(.0001f));
                        Assert.That(Quaternion.Angle(paused.Bones[i].localRotation, control.Bones[i].localRotation), Is.LessThan(.001f));
                        Assert.That(Vector3.Distance(paused.Bones[i].localScale, control.Bones[i].localScale), Is.LessThan(.0001f));
                    }
                    var pausedTint = new MaterialPropertyBlock(); var controlTint = new MaterialPropertyBlock();
                    paused.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(pausedTint);
                    control.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(controlTint);
                    Assert.That(pausedTint.GetColor("_Tint"), Is.EqualTo(controlTint.GetColor("_Tint")));
                }
                finally { Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void ReducedMotionSuppressesGreetingAndCelebrationForEveryHeroSpecies()
        {
            var root = new GameObject("reduced personality motion");
            using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
            {
                try
                {
                    foreach (string species in new[] { "vetch", "ember", "pale" })
                    {
                        var actor = art.Creature(root.transform, species);
                        actor.ReducedMotion = true;
                        actor.Greet(); actor.Celebrate(); actor.Attack(); actor.Hit(); actor.AdvancePresentation(.22f);
                        Assert.That(actor.Bones[0].localPosition, Is.EqualTo(Vector3.zero), species);
                        foreach (var bone in actor.Bones)
                        {
                            Assert.That(Quaternion.Angle(bone.localRotation, Quaternion.identity), Is.LessThan(.001f), species);
                            Assert.That(bone.localScale, Is.EqualTo(Vector3.one), species);
                        }
                    }
                }
                finally { Object.DestroyImmediate(root); }
            }
        }
    }
}
