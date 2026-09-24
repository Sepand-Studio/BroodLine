using NUnit.Framework;
using UnityEngine;

namespace Broodline.View.Tests
{
    public class BattleBackdropTests
    {
        [Test]
        public void PaintedGroundLoadsWithUpwardWindingAndStaysBehindTheLane()
        {
            var parent = new GameObject("battle-backdrop-test");
            try
            {
                var texture = Resources.Load<Texture2D>(BattleBackdrop.ResourcePath);
                Assert.IsNotNull(texture, "the imported battlefield texture must be in Resources");
                var backdrop = BattleBackdrop.Create(parent.transform, 24, 7);
                Assert.IsNotNull(backdrop);
                Assert.AreEqual(7, backdrop.gameObject.layer);
                var mesh = backdrop.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(4, mesh.vertexCount);
                Assert.AreEqual(6, mesh.triangles.Length);
                foreach (var vertex in mesh.vertices) Assert.AreEqual(BattleBackdrop.GroundY, vertex.y, .0001f);
                foreach (var normal in mesh.normals) Assert.Greater(normal.y, .99f);
                var positions = mesh.vertices;
                var indices = mesh.triangles;
                var outward = Vector3.Cross(positions[indices[1]] - positions[indices[0]],
                    positions[indices[2]] - positions[indices[0]]);
                Assert.Greater(outward.y, 0f);
                Assert.AreSame(texture, backdrop.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"));
            }
            finally { Object.DestroyImmediate(parent); }
        }
    }
}
