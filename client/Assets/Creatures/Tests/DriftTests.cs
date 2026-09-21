using System.Linq;
using Broodline.Creatures;
using Broodline.Creatures.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    /// The drift gate. A recipe edited without `Broodline > Generate Creatures`
    /// (or generate-creatures.sh) fails here, so the committed assets are
    /// always what the recipes say.
    ///
    /// THE HASH ALONE IS NOT THE GATE. It covers vertices and triangles, and
    /// those are all the geometry depends on - `Sdf` never reads
    /// `Primitive.Bone`, and `SurfaceNets` uses `boneNames` only inside
    /// `WeightsAt`. So retagging a primitive's bone, moving a `BoneDef`,
    /// editing a `SocketDef` or changing a colour all regenerate BYTE-IDENTICAL
    /// geometry and sail past a vertex hash while leaving the committed
    /// skinning, prefab or material stale. The tests below cover the recipe
    /// fields the later tasks actually consume: weights and bind poses (Task 8
    /// moves the bones), socket transforms (Task 10 mounts parts on them) and
    /// material colours (Tasks 11 and 12 render them).
    public class DriftTests
    {
        const string ResourceRoot = "Assets/Creatures/Resources/";

        static string MeshPath(string id) => ResourceRoot + CreaturePaths.MeshDir + "/" + id + ".asset";
        static string MaterialPath(string id) => ResourceRoot + CreaturePaths.MaterialDir + "/" + id + ".mat";

        static string Hash(GeneratedMesh m)
        {
            unchecked
            {
                long h = 17;
                foreach (var v in m.Vertices)
                    h = h * 31 + Mathf.RoundToInt(v.x * 1000f) * 7 + Mathf.RoundToInt(v.y * 1000f) * 13 + Mathf.RoundToInt(v.z * 1000f) * 17;
                foreach (var i in m.Triangles) h = h * 31 + i;
                return h.ToString("x");
            }
        }

        static string Hash(Mesh m)
        {
            var g = new GeneratedMesh { Vertices = m.vertices, Triangles = m.triangles };
            return Hash(g);
        }

        /// Bones nest (a leg under the root), so Transform.Find - which only
        /// looks at direct children - cannot find them all.
        static Transform Descendant(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        [Test]
        public void EveryCommittedBodyMesh_MatchesItsRecipe()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r.Id));
                Assert.IsNotNull(committed, "no committed mesh at " + MeshPath(r.Id) + " - run generate-creatures.sh");
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(r)), Hash(committed),
                    r.Id + " drifted from its recipe - run generate-creatures.sh and commit");
            }
        }

        [Test]
        public void EveryCommittedPartMesh_MatchesItsRecipe()
        {
            foreach (var p in PartRecipes.All)
            {
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath("part-" + p.Id));
                Assert.IsNotNull(committed, "no committed mesh at " + MeshPath("part-" + p.Id));
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(p)), Hash(committed), p.Id + " drifted");
            }
        }

        /// Recipe.cs: "A body stands on y = 0." Nothing asserted it, and Vetch
        /// did not - it meshed from -0.09985 to 0.93503, about 7% of its own
        /// length sunk through the plane the lane stands it on, because the
        /// dome bottomed below zero and the legs ended at y = 0 with a 0.11
        /// radius. Fixed by shifting the whole recipe up by 0.10 rather than
        /// by seating it in the generator, so the numbers in the recipe stay
        /// literally true and every assertion here can compare against them
        /// directly instead of against a hidden offset.
        ///
        /// The tolerance is 0.02 on a body about one unit long: an author who
        /// rounds their shift to two decimals is never more than 0.005 out, and
        /// 0.02 still catches the 0.10 sink that prompted this.
        [Test]
        public void EveryBody_RestsOnTheGroundPlane()
        {
            const float tolerance = 0.02f;
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                float lowest = CreatureGenerator.Regenerate(r).Vertices.Min(v => v.y);
                Assert.AreEqual(0f, lowest, tolerance,
                    r.Id + " does not stand on y = 0 - its mesh bottoms at " + lowest.ToString("F5") +
                    ". Shift every y in the recipe (primitives, bones AND sockets) by " +
                    (-lowest).ToString("F2") + " and regenerate.");

                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r.Id));
                Assert.IsNotNull(committed, "no committed mesh at " + MeshPath(r.Id));
                Assert.AreEqual(0f, committed.bounds.min.y, tolerance,
                    r.Id + "'s COMMITTED mesh bottoms at " + committed.bounds.min.y.ToString("F5"));
            }
        }

        /// Covers what the vertex hash cannot: retag a primitive's Bone and the
        /// geometry is identical but every weight's bone index moves.
        [Test]
        public void EveryCommittedBodyMesh_CarriesItsRecipesSkinning()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(r.Id));
                Assert.IsNotNull(committed, "no committed mesh at " + MeshPath(r.Id));

                var expected = CreatureGenerator.Regenerate(r).Weights;
                var actual = committed.boneWeights;
                Assert.AreEqual(expected.Length, actual.Length, r.Id + " committed weight count differs");
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i].boneIndex0, actual[i].boneIndex0, r.Id + " vertex " + i + " bone0 drifted");
                    Assert.AreEqual(expected[i].boneIndex1, actual[i].boneIndex1, r.Id + " vertex " + i + " bone1 drifted");
                    Assert.AreEqual(expected[i].weight0, actual[i].weight0, 1e-5f, r.Id + " vertex " + i + " weight0 drifted");
                    Assert.AreEqual(expected[i].weight1, actual[i].weight1, 1e-5f, r.Id + " vertex " + i + " weight1 drifted");
                }

                // A bind pose is the inverse of its bone's rest transform, so it
                // must send that bone's rest position to the origin. Move a
                // BoneDef.Position without regenerating and this is what breaks.
                var bind = committed.bindposes;
                Assert.AreEqual(r.Bones.Length, bind.Length, r.Id + " bind pose count differs from its bone count");
                for (int i = 0; i < bind.Length; i++)
                    Assert.AreEqual(0f, bind[i].MultiplyPoint3x4(r.Bones[i].Position).magnitude, 1e-4f,
                        r.Id + " bind pose " + i + " (" + r.Bones[i].Name + ") does not invert its bone's rest position - " +
                        "the recipe moved without a regenerate");
            }
        }

        [Test]
        public void EveryPrefab_Exists_AndCarriesItsSockets()
        {
            foreach (var r in SpeciesRecipes.All)
            {
                var prefab = Resources.Load<GameObject>(CreaturePaths.Body(r.Id));
                Assert.IsNotNull(prefab, "no prefab for " + r.Id);
                foreach (var s in r.Sockets)
                    Assert.IsNotNull(prefab.transform.Find(s.Name), r.Id + " prefab lacks socket " + s.Name);
                Assert.IsNotNull(prefab.GetComponentInChildren<SkinnedMeshRenderer>(), r.Id + " has no skinned renderer");
            }
            foreach (var p in PartRecipes.All)
                Assert.IsNotNull(Resources.Load<GameObject>(CreaturePaths.Part(p.Id)), "no prefab for part " + p.Id);
        }

        /// Task 10 mounts parts on these transforms and Task 8 moves these
        /// bones. Editing either in the recipe regenerates identical geometry,
        /// so only this test notices a prefab that was never rebuilt.
        [Test]
        public void EveryPrefab_MatchesItsRecipesBonesAndSockets()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var prefab = Resources.Load<GameObject>(
                    r.Raider ? CreaturePaths.Raider(r.Id) : CreaturePaths.Body(r.Id));
                Assert.IsNotNull(prefab, "no prefab for " + r.Id);

                foreach (var b in r.Bones)
                {
                    var t = Descendant(prefab, b.Name);
                    Assert.IsNotNull(t, r.Id + " prefab lacks bone " + b.Name);
                    var where = prefab.transform.InverseTransformPoint(t.position);
                    Assert.AreEqual(0f, (where - b.Position).magnitude, 1e-4f,
                        r.Id + " bone " + b.Name + " sits at " + where + ", recipe says " + b.Position +
                        " - run generate-creatures.sh and commit");
                    if (b.Parent != null)
                        Assert.AreEqual(b.Parent, t.parent.name,
                            r.Id + " bone " + b.Name + " is parented to " + t.parent.name + ", recipe says " + b.Parent);
                }

                foreach (var s in r.Sockets)
                {
                    var t = prefab.transform.Find(s.Name);
                    Assert.IsNotNull(t, r.Id + " prefab lacks socket " + s.Name);
                    Assert.AreEqual(0f, (t.localPosition - s.Position).magnitude, 1e-4f,
                        r.Id + " socket " + s.Name + " moved: prefab " + t.localPosition + " vs recipe " + s.Position);
                    Assert.AreEqual(0f, Quaternion.Angle(t.localRotation, Quaternion.Euler(s.Euler)), 1e-2f,
                        r.Id + " socket " + s.Name + " rotation drifted from Euler " + s.Euler);
                    Assert.AreEqual(s.Scale, t.localScale.x, 1e-4f,
                        r.Id + " socket " + s.Name + " scale drifted");
                }
            }
        }

        /// A colour is pure recipe data - it never touches a vertex - so the
        /// hash is blind to it and this is the only gate on it.
        [Test]
        public void EveryMaterial_CarriesItsRecipeColours()
        {
            foreach (var r in SpeciesRecipes.All)
            {
                var c = SpeciesColours.For(r.Id);
                AssertColours(r.Id, c.Base, c.Under);
            }
            foreach (var r in RaiderRecipes.All)
                AssertColours(r.Id, SpeciesColours.RaiderBase, SpeciesColours.RaiderUnder);
            foreach (var p in PartRecipes.All)
                AssertColours("part-" + p.Id, p.Base, p.Under);
        }

        static void AssertColours(string id, Color expectedBase, Color expectedUnder)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(id));
            Assert.IsNotNull(mat, "no committed material at " + MaterialPath(id) + " - run generate-creatures.sh");
            AssertColour(id, "_BaseColor", mat.GetColor("_BaseColor"), expectedBase);
            AssertColour(id, "_UnderColor", mat.GetColor("_UnderColor"), expectedUnder);
        }

        static void AssertColour(string id, string property, Color actual, Color expected)
        {
            const float tolerance = 1e-3f;
            bool same = Mathf.Abs(actual.r - expected.r) < tolerance
                     && Mathf.Abs(actual.g - expected.g) < tolerance
                     && Mathf.Abs(actual.b - expected.b) < tolerance;
            Assert.IsTrue(same, id + " material " + property + " is " + actual + ", recipe says " + expected +
                          " - run generate-creatures.sh and commit");
        }
    }
}
