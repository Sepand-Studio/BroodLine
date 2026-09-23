using UnityEngine;

namespace Broodline.Frontier
{
    /// Keeps the live hero portrait and the card-sprite bake in the same view.
    public static class FrontierPortraitCamera
    {
        public static void Frame(Camera camera, Transform stage, Bounds bounds,
            FrontierRigDefinition rig, FrontierPortrait portrait)
        {
            bounds.Expand(rig.MotionAllowance * 2f);
            // Growth reserves space at framing time; animation never changes zoom.
            var x = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
            var z = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z));
            var radius = Mathf.Sqrt(x * x + z * z) * 1.25f;
            var height = bounds.extents.y * 1.25f;
            var direction = new Vector3(3.2f, 2.2f, -3.6f).normalized;
            var rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            // The hero turns through 360 degrees, so fit the swept horizontal
            // radius rather than only the current three-quarter projection.
            float Span(Vector3 axis) => radius * Mathf.Sqrt(axis.x * axis.x + axis.z * axis.z) +
                Mathf.Abs(axis.y) * height;
            camera.orthographicSize = Mathf.Max(.6f, Span(up), Span(right)) * (1f + portrait.Padding);
            var target = stage.position + new Vector3(0f, bounds.center.y, 0f);
            camera.transform.position = target + direction * 6f;
            camera.transform.LookAt(target);
        }
    }
}
