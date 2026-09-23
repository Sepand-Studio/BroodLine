using Broodline.Game.Shell;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    public sealed class HomeStageTests
    {
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
