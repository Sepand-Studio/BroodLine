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
    public class DriftTests
    {
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

        [Test]
        public void EveryCommittedBodyMesh_MatchesItsRecipe()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var path = "Assets/Creatures/Resources/" + CreaturePaths.MeshDir + "/" + r.Id + ".asset";
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(committed, "no committed mesh at " + path + " - run generate-creatures.sh");
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(r)), Hash(committed),
                    r.Id + " drifted from its recipe - run generate-creatures.sh and commit");
            }
        }

        [Test]
        public void EveryCommittedPartMesh_MatchesItsRecipe()
        {
            foreach (var p in PartRecipes.All)
            {
                var path = "Assets/Creatures/Resources/" + CreaturePaths.MeshDir + "/part-" + p.Id + ".asset";
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(committed, "no committed mesh at " + path);
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(p)), Hash(committed), p.Id + " drifted");
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
    }
}
