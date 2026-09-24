using NUnit.Framework;
using UnityEngine;

namespace Broodline.View.Tests
{
    public class BattleCameraFrameTests
    {
        [TestCase(430f / 932f)]
        [TestCase(390f / 844f)]
        [TestCase(360f / 640f)]
        public void PerspectiveFrame_KeepsLaneTerrainAndHeadsInsidePortrait(float aspect)
        {
            var go = new GameObject("battle-camera-check");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.aspect = aspect;
                BattleCameraFrame.Apply(camera, 24);
                Assert.IsFalse(camera.orthographic);

                foreach (float x in new[] { -2f, 27f })
                foreach (float y in new[] { -.5f, 3f })
                foreach (float z in new[] { -5f, 5f })
                {
                    var point = camera.WorldToViewportPoint(new Vector3(x, y, z));
                    Assert.Greater(point.z, camera.nearClipPlane);
                    Assert.That(point.x, Is.InRange(.07f, .93f));
                    Assert.That(point.y, Is.InRange(.07f, .93f));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
