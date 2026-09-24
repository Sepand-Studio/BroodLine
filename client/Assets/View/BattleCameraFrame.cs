using UnityEngine;

namespace Broodline.View
{
    /// Keeps the entire Frontier lane inside a narrow portrait frame. The
    /// camera is configured at run time as well as by WaveSceneBuilder, so an
    /// older checked-in Wave scene still gets the new composition.
    public static class BattleCameraFrame
    {
        public const float MinPortraitAspect = .45f;
        const float FieldOfView = 44f;
        const float SafeFraction = .84f;

        public static void Apply(Camera camera, int laneTiles)
        {
            if (camera == null) return;

            float length = laneTiles * WaveView.TileSize;
            var target = new Vector3(length * .5f, .7f, .5f);
            var offset = new Vector3(.16f, 1f, -.68f).normalized;
            var rotation = Quaternion.LookRotation(-offset, Vector3.right);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            float tanHalf = Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad);
            float aspect = Mathf.Min(MinPortraitAspect, Mathf.Max(.1f, camera.aspect));
            float distance = 0f;

            // Ground dressing, pocket bodies, Ark and raider heads. Fitting
            // corners instead of only the path protects narrow devices and
            // keeps the upper silhouettes clear of the HUD edge.
            foreach (float x in new[] { -2f, length + 3f })
            foreach (float y in new[] { -.5f, 3f })
            foreach (float z in new[] { -5f, 5f })
            {
                var relative = new Vector3(x, y, z) - target;
                float halfHeight = Mathf.Abs(Vector3.Dot(relative, up)) / (tanHalf * SafeFraction);
                float halfWidth = Mathf.Abs(Vector3.Dot(relative, right)) / (tanHalf * aspect * SafeFraction);
                distance = Mathf.Max(distance, Vector3.Dot(relative, offset) + Mathf.Max(halfHeight, halfWidth));
            }

            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = distance + 50f;
            camera.transform.SetPositionAndRotation(target + offset * distance, rotation);
        }
    }
}
