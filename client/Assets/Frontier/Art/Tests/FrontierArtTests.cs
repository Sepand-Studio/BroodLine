using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
        public void FounderPortraitFillsTheFrameWithoutClippingItsBounds()
        {
            var host = new GameObject("portrait-framing-host");
            var camera = new GameObject("portrait-camera").AddComponent<Camera>();
            try
            {
                camera.orthographic = true;
                camera.aspect = 1f;
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
                {
                    var creature = art.Creature(host.transform, "vetch");
                    var bounds = creature.PortraitBounds;
                    FrontierPortraitCamera.Frame(camera, host.transform, bounds, creature.Rig,
                        FrontierVisuals.For("vetch").Portrait);
                    float left = 1f, right = 0f, bottom = 1f, top = 0f;
                    for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var uv = camera.WorldToViewportPoint(host.transform.TransformPoint(corner));
                        left = Mathf.Min(left, uv.x); right = Mathf.Max(right, uv.x);
                        bottom = Mathf.Min(bottom, uv.y); top = Mathf.Max(top, uv.y);
                    }
                    Assert.That(right - left, Is.GreaterThan(.7f), "Vetch must read as a hero at phone size");
                    Assert.That(left, Is.GreaterThan(.03f));
                    Assert.That(right, Is.LessThan(.97f));
                    Assert.That(bottom, Is.GreaterThan(.03f));
                    Assert.That(top, Is.LessThan(.97f));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [Test]
        public void AuthoredCreatureUsesTheSharedRigAndKeepsBothTraitSockets()
        {
            var host = new GameObject("authored-host");
            var prefab = new GameObject("authored-vetch");
            Mesh mesh = null;
            Material material = null;
            try
            {
                var rig = FrontierRigDefinition.For("vetch");
                var bones = new Transform[rig.Bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    bones[i] = new GameObject(rig.Bones[i].Name).transform;
                    bones[i].SetParent(rig.Bones[i].Parent < 0 ? prefab.transform : bones[rig.Bones[i].Parent], false);
                    bones[i].localPosition = rig.LocalPosition(i);
                }
                var author = new FrontierMesh();
                FrontierVetch.Build(author);
                mesh = author.FinishRig("authored-test-body", rig);
                material = new Material(Shader.Find("Broodline/FrontierSurface"));
                var renderer = new GameObject("authored-body").AddComponent<SkinnedMeshRenderer>();
                renderer.transform.SetParent(prefab.transform, false);
                renderer.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.bones = bones;
                renderer.rootBone = bones[0];

                string requestedPath = null;
                using (var art = new FrontierArt(material.shader, path => { requestedPath = path; return prefab; }))
                {
                    var creature = art.Creature(host.transform, "vetch", "cinder", "carapace");
                    Assert.AreEqual("Frontier/Creatures/vetch", requestedPath);
                    Assert.AreSame(mesh, creature.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                    Assert.IsNotNull(creature.Dorsal.Find("cinder"));
                    Assert.IsNotNull(creature.Flank.Find("carapace"));
                    Assert.DoesNotThrow(() => creature.Pose(0f, true));
                }

                // The asset contract requires an origin root at unit scale.
                // An offset or scaled prefab must fall back before it shifts
                // a battle unit or makes the portrait camera frame the wrong bounds.
                prefab.transform.localPosition = new Vector3(.25f, 0f, 0f);
                using (var art = new FrontierArt(material.shader, _ => prefab))
                {
                    var fallback = art.Creature(host.transform, "vetch");
                    Assert.AreNotSame(mesh, fallback.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                }
                prefab.transform.localPosition = Vector3.zero;
                prefab.transform.localScale = Vector3.one;
                prefab.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
                using (var art = new FrontierArt(material.shader, _ => prefab))
                {
                    var fallback = art.Creature(host.transform, "vetch");
                    Assert.AreNotSame(mesh, fallback.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                }
                prefab.transform.localPosition = Vector3.zero;
                prefab.transform.localRotation = Quaternion.identity;
                prefab.transform.localScale = Vector3.one * 1.2f;
                using (var art = new FrontierArt(material.shader, _ => prefab))
                {
                    var fallback = art.Creature(host.transform, "vetch");
                    Assert.AreNotSame(mesh, fallback.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(prefab);
                if (mesh != null) Object.DestroyImmediate(mesh);
                if (material != null) Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void AuthoredPartsUseBothOrderedSocketsAndRemainOwnedByTheAsset()
        {
            var host = new GameObject("part-host");
            var prefab = new GameObject("authored-part");
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up * 2, Vector3.right },
                triangles = new[] { 0, 1, 2 } };
            mesh.RecalculateBounds();
            var material = new Material(Shader.Find("Broodline/FrontierSurface"));
            prefab.AddComponent<MeshFilter>().sharedMesh = mesh;
            prefab.AddComponent<MeshRenderer>().sharedMaterial = material;
            try
            {
                using (var art = new FrontierArt(material.shader, _ => null,
                    path => path == "Frontier/Parts/cinder" || path == "Frontier/Parts/carapace" ? prefab : null))
                {
                    var creature = art.Creature(host.transform, "vetch", " CINDER ", "CARAPACE");
                    Assert.AreSame(mesh, creature.Dorsal.Find("cinder").GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(mesh, creature.Flank.Find("carapace").GetComponent<MeshFilter>().sharedMesh);
                    Assert.Greater(creature.PortraitBounds.max.y, 2f, "authored parts participate in framing");
                    var reversed = art.Creature(host.transform, "vetch", "carapace", "cinder");
                    Assert.AreSame(mesh, reversed.Dorsal.Find("carapace").GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(mesh, reversed.Flank.Find("cinder").GetComponent<MeshFilter>().sharedMesh);
                }
                Assert.IsTrue(mesh != null, "disposing transient art must not destroy imported meshes");
                Assert.IsTrue(material != null, "disposing transient art must not destroy imported materials");
                prefab.transform.localScale = Vector3.one * 2;
                using (var art = new FrontierArt(material.shader, _ => null, _ => prefab))
                {
                    var fallback = art.Creature(host.transform, "vetch", "cinder");
                    Assert.AreNotSame(mesh, fallback.Dorsal.Find("cinder").GetComponent<MeshFilter>().sharedMesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(host); Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MissingAuthoredCreatureFallsBackToTheProceduralBody()
        {
            var host = new GameObject("fallback-host");
            try
            {
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface"), _ => null))
                {
                    var creature = art.Creature(host.transform, "vetch");
                    Assert.IsNotNull(creature.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
                    Assert.IsNotNull(creature.Dorsal);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void InstalledFounderMeshesAndTraitsLoadThroughTheLiveCreaturePath()
        {
            var host = new GameObject("installed-art-smoke");
            var species = new[] { "vetch", "ember", "skitter", "hollow", "loam", "pale" };
            var traits = new[] { "carapace", "taunt", "cinder", "splash", "sprint", "litter",
                "reach", "pierce", "regrow", "burrow", "screen", "chill" };
            try
            {
                var shader = Shader.Find("Broodline/FrontierSurface");
                Assert.IsNotNull(shader);
                using (var art = new FrontierArt(shader))
                {
                    for (int i = 0; i < species.Length; i++)
                    {
                        var id = species[i];
                        var asset = Resources.Load<GameObject>("Frontier/Creatures/" + id);
                        Assert.IsNotNull(asset, "missing installed body: " + id);
                        var creature = art.Creature(host.transform, id, "cinder", "carapace");
                        Assert.AreSame(asset.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                            creature.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh,
                            "body silently fell back: " + id);
                        Assert.IsNotNull(creature.Dorsal.Find("cinder"));
                        Assert.IsNotNull(creature.Flank.Find("carapace"));
                        Assert.DoesNotThrow(() => creature.Pose(.3f));
                    }
                    foreach (var trait in traits)
                    {
                        var asset = Resources.Load<GameObject>("Frontier/Parts/" + trait);
                        Assert.IsNotNull(asset, "missing installed part: " + trait);
                        var creature = art.Creature(host.transform, "vetch", trait, trait);
                        var sourceMesh = asset.GetComponent<MeshFilter>().sharedMesh;
                        Assert.AreSame(sourceMesh, creature.Dorsal.Find(trait).GetComponent<MeshFilter>().sharedMesh,
                            "dorsal part silently fell back: " + trait);
                        Assert.AreSame(sourceMesh, creature.Flank.Find(trait).GetComponent<MeshFilter>().sharedMesh,
                            "flank part silently fell back: " + trait);
                    }
                }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void EveryFounderTraitHasAVisiblePartInBothLiveSlots()
        {
            var root = new GameObject("all-trait-slots");
            var traits = new[] { "carapace", "taunt", "cinder", "splash", "sprint", "litter",
                "reach", "pierce", "regrow", "burrow", "screen", "chill" };
            try
            {
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
                {
                    foreach (var trait in traits)
                    {
                        var creature = art.Creature(root.transform, "vetch", trait, trait);
                        foreach (var socket in new[] { creature.Dorsal, creature.Flank })
                        {
                            var part = socket.Find(trait);
                            Assert.IsNotNull(part, trait + " is missing from " + socket.name);
                            var filter = part.GetComponent<MeshFilter>();
                            Assert.IsNotNull(filter);
                            ValidMesh(filter.sharedMesh, true);
                        }
                    }
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ProductionSnapshotIdsRemainCaseInsensitive()
        {
            var root = new GameObject("snapshot-id-regression");
            try
            {
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
                {
                    var creature = art.Creature(root.transform, " Ember ", " Cinder ", " CARAPACE ");
                    Assert.AreEqual("ember", creature.SpeciesId);
                    Assert.IsNotNull(creature.Dorsal.Find("cinder"));
                    Assert.IsNotNull(creature.Flank.Find("carapace"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NewlyCreatedActiveAndInactiveCreaturesApplyTheirFirstPoseWithoutErrors()
        {
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(type + ": " + message);
            };
            Application.logMessageReceived += capture;
            try
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
                        }
                        finally { Object.DestroyImmediate(root); }
                    }
                }
            }
            finally { Application.logMessageReceived -= capture; }

            Assert.IsEmpty(errors,
                "creating and posing an active or inactive creature logged an error:\n" +
                string.Join("\n", errors));
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
        public void PaintedBattlefieldOmitsOnlyTheFallbackGroundSlab()
        {
            var root = new GameObject("painted-ground-test");
            try
            {
                using (var art = new FrontierArt(Shader.Find("Broodline/FrontierSurface")))
                {
                    var pockets = new[] { 2, 6, 10, 14, 18 };
                    var fallback = art.Environment(root.transform, 24, pockets, false)
                        .GetComponent<MeshFilter>().sharedMesh;
                    var painted = art.Environment(root.transform, 24, pockets, false, paintedGround: true)
                        .GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(36, fallback.triangles.Length - painted.triangles.Length,
                        "the painted lane removes one box and keeps every authored detail");
                    ValidMesh(painted, true);
                }
            }
            finally { Object.DestroyImmediate(root); }
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
                    // EVERY DEFINED BODY, not a hard-coded six - Phase 10 Batch 2.
                    // The five new companions were outside this loop for a
                    // commit; a species the registry knows is a species this
                    // budget and binding check runs on.
                    foreach (var definition in FrontierVisuals.All)
                    {
                        string species = definition.Id;
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
                        // Weights sum to one and every index is a bone; a membrane
                        // blends shoulder and tip, so `weight0 == 1` is no longer the rule.
                        foreach (var weight in renderer.sharedMesh.boneWeights)
                        {
                            Assert.That(weight.boneIndex0, Is.InRange(0, creature.Bones.Length - 1));
                            Assert.That(weight.boneIndex1, Is.InRange(0, creature.Bones.Length - 1));
                            Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3, Is.EqualTo(1f).Within(.001f), species);
                        }
                        var vertices = renderer.sharedMesh.vertices;
                        var weights = renderer.sharedMesh.boneWeights;
                        var binds = renderer.sharedMesh.bindposes;
                        for (int i = 0; i < vertices.Length; i++)
                        {
                            Assert.That(portrait.Contains(vertices[i]), Is.True, "portrait clips body");
                            var skinned = Vector3.zero;
                            foreach (var (bone, w) in new[] { (weights[i].boneIndex0, weights[i].weight0), (weights[i].boneIndex1, weights[i].weight1) })
                            {
                                if (w <= 0) continue;
                                var bindVertex = renderer.transform.worldToLocalMatrix * creature.Bones[bone].localToWorldMatrix * binds[bone];
                                skinned += bindVertex.MultiplyPoint3x4(vertices[i]) * w;
                            }
                            Assert.That(Vector3.Distance(skinned, vertices[i]), Is.LessThan(.0001f), "rest pose changed a vertex");
                        }
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
                    foreach (string species in FrontierRigDefinition.Companions)
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
