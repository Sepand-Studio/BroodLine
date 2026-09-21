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

        [Test]
        public void SmoothMin_IsMinAtZeroBlend_AndBelowMinOtherwise()
        {
            Assert.AreEqual(-1f, Sdf.SmoothMin(-1f, 2f, 0f));
            Assert.Less(Sdf.SmoothMin(0.1f, 0.1f, 0.5f), 0.1f, "two touching shapes blend into one");
        }
    }
}
