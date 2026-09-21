using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    public class MesherTests
    {
        public const int BodyTriangleBudget = 2500;
        public const int PartTriangleBudget = 400;

        static GeneratedMesh Mesh(BodyRecipe r) =>
            SurfaceNets.Build(r.Primitives, r.Blend, Sdf.BoundsOf(r.Primitives, r.Padding), r.Grid,
                              r.Bones.Select(b => b.Name).ToArray());

        [Test]
        public void EveryBody_IsInsideTheTriangleBudget_AndNotEmpty()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var m = Mesh(r);
                var tris = m.Triangles.Length / 3;
                Assert.Greater(tris, 200, r.Id + " meshed to almost nothing - check its bounds");
                Assert.LessOrEqual(tris, BodyTriangleBudget, r.Id + " is over budget at " + tris);
            }
        }

        [Test]
        public void EveryPart_IsInsideItsBudget()
        {
            foreach (var p in PartRecipes.All)
            {
                var m = SurfaceNets.Build(p.Primitives, p.Blend, Sdf.BoundsOf(p.Primitives, p.Padding), p.Grid, new[] { "root" });
                var tris = m.Triangles.Length / 3;
                Assert.Greater(tris, 20, p.Id + " meshed to almost nothing");
                Assert.LessOrEqual(tris, PartTriangleBudget, p.Id + " is over budget at " + tris);
            }
        }

        [Test]
        public void Weights_UseAtMostTwoInfluences_AndSumToOne()
        {
            // QualitySettings.asset: the Mobile tier caps skinning at two
            // influences. A vertex authored with four would look different on
            // the phone than in the Editor, so the mesher never writes them.
            var m = Mesh(SpeciesRecipes.For("vetch"));
            var boneCount = SpeciesRecipes.For("vetch").Bones.Length;
            foreach (var w in m.Weights)
            {
                Assert.AreEqual(0f, w.weight2, "third influence must be zero");
                Assert.AreEqual(0f, w.weight3, "fourth influence must be zero");
                Assert.AreEqual(1f, w.weight0 + w.weight1, 1e-4f);
                Assert.Less(w.boneIndex0, boneCount);
                Assert.Less(w.boneIndex1, boneCount);
            }
        }

        [Test]
        public void Normals_AreUnitLength_AndTrianglesIndexRealVertices()
        {
            var m = Mesh(SpeciesRecipes.For("vetch"));
            Assert.AreEqual(m.Vertices.Length, m.Normals.Length);
            foreach (var n in m.Normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            Assert.AreEqual(0, m.Triangles.Length % 3);
            Assert.IsTrue(m.Triangles.All(i => i >= 0 && i < m.Vertices.Length));
        }

        /// THE GATE THE TRIANGLE COUNTS CANNOT SEE. One vertex per cell means a
        /// feature thinner than a cell can put two surface sheets through one
        /// cell; the quads around both crossings then share that one vertex and
        /// the surface pinches into a fold. It is not a hole, it does not change
        /// the triangle count, and it survives every other test in this file -
        /// but it is not a closed surface either, and it renders as a crease.
        /// Found on the taunt at grids 15 and 12 while tuning; all four shipped
        /// meshes are clean, so writing it down costs nothing today and holds
        /// the nine parts and five species still to come.
        ///
        /// A closed, consistently wound triangle surface has every directed
        /// edge exactly once, with its opposite present - so every undirected
        /// edge is shared by exactly two triangles, facing opposite ways.
        [Test]
        public void EveryMesh_IsAClosedManifold()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
                AssertClosed(r.Id, Mesh(r));
            foreach (var p in PartRecipes.All)
                AssertClosed("part-" + p.Id, SurfaceNets.Build(
                    p.Primitives, p.Blend, Sdf.BoundsOf(p.Primitives, p.Padding), p.Grid, new[] { "root" }));
        }

        static void AssertClosed(string id, GeneratedMesh m)
        {
            var used = new System.Collections.Generic.Dictionary<(int, int), int>();
            for (int i = 0; i < m.Triangles.Length; i += 3)
            {
                int a = m.Triangles[i], b = m.Triangles[i + 1], c = m.Triangles[i + 2];
                Assert.IsTrue(a != b && b != c && c != a, id + " has a degenerate triangle at index " + i);
                foreach (var e in new[] { (a, b), (b, c), (c, a) })
                    used[e] = used.TryGetValue(e, out var k) ? k + 1 : 1;
            }
            int pinched = used.Count(kv => kv.Value > 1);
            int boundary = used.Count(kv => !used.ContainsKey((kv.Key.Item2, kv.Key.Item1)));
            Assert.AreEqual(0, pinched,
                id + " uses " + pinched + " directed edge(s) more than once - two surface sheets are " +
                "sharing a cell. Raise its Grid until the thin feature is at least two cells thick.");
            Assert.AreEqual(0, boundary,
                id + " has " + boundary + " boundary edge(s) - the surface is open, not closed.");
        }

        [Test]
        public void SmoothMin_IsMinAtZeroBlend_AndBelowMinOtherwise()
        {
            Assert.AreEqual(-1f, Sdf.SmoothMin(-1f, 2f, 0f));
            Assert.Less(Sdf.SmoothMin(0.1f, 0.1f, 0.5f), 0.1f, "two touching shapes blend into one");
        }
    }
}
