using Broodline.Game.Shell;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    public sealed class HomeStageTests
    {
        [Test]
        public void PaintedValleyFillsTheCameraBehindTheArk()
        {
            var painting = Resources.Load<Texture2D>(HomeStage.BackdropResourcePath);
            Assert.IsNotNull(painting, "The Ark valley painting must be included in Resources.");

            var host = new GameObject("home-backdrop-test");
            try
            {
                var stage = HomeStage.Create(host.transform);
                var camera = stage.GetComponentInChildren<Camera>();
                var backdrop = camera.transform.Find("ark-valley-backdrop");
                Assert.IsNotNull(backdrop);
                Assert.AreEqual(CreatureAssembler.StudioLayer, backdrop.gameObject.layer);
                Assert.That(backdrop.localPosition.z, Is.GreaterThan(camera.nearClipPlane));
                Assert.That(backdrop.localPosition.z, Is.LessThan(camera.farClipPlane));

                var mesh = backdrop.GetComponent<MeshFilter>().sharedMesh;
                float halfHeight = backdrop.localPosition.z * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                Assert.That(mesh.bounds.extents.y, Is.GreaterThan(halfHeight));
                Assert.That(mesh.bounds.extents.x, Is.GreaterThan(halfHeight * HomeStage.Width / HomeStage.Height));
                var a = mesh.vertices[mesh.triangles[0]];
                var b = mesh.vertices[mesh.triangles[1]];
                var c = mesh.vertices[mesh.triangles[2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b - a, c - a), Vector3.back), Is.GreaterThan(0f),
                    "The painting must face the camera rather than be back-face culled.");
                Assert.AreSame(painting, backdrop.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void TheBaseFitsItsBudgetAndEveryPlotProjectsInsideTheFrame()
        {
            var host = new GameObject("home-stage-test");
            try
            {
                var stage = HomeStage.Create(host.transform);
                var texture = stage.Show();
                Assert.IsNotNull(texture);
                Assert.That(stage.TriangleCount(), Is.GreaterThan(0).And.LessThanOrEqualTo(30000));

                var anchors = stage.PlotAnchors();
                Assert.AreEqual(6, anchors.Count);
                foreach (var (id, x, y) in anchors)
                {
                    Assert.That(x, Is.InRange(0.05f, 0.95f), id + " x");
                    Assert.That(y, Is.InRange(0.05f, 0.95f), id + " y");
                }
                // The core is the Ark and sits above the drive plot on screen.
                float coreY = 0, driveY = 0;
                foreach (var (id, _, y) in anchors) { if (id == "core") coreY = y; if (id == "drive") driveY = y; }
                Assert.That(coreY, Is.LessThan(driveY));
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}
