using UnityEngine;

namespace Broodline.Frontier
{
    /// Keeps the live hero portrait and the card-sprite bake in the same view.
    /// Frame the visible three-quarter pose, leaving motion allowance, so a
    /// creature reads at phone size instead of occupying half a hero slot.
    public static class FrontierPortraitCamera
    {
        public static void Frame(Camera camera, Transform stage, Bounds bounds,
            FrontierRigDefinition rig, FrontierPortrait portrait)
        {
            bounds.Expand(rig.MotionAllowance * 2f);
            var direction = new Vector3(3.2f, 2.2f, -3.6f).normalized;
            var rotation = Quaternion.LookRotation(-direction, Vector3.up);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            // Project the expanded bounds into this fixed view. A square
            // texture needs half the horizontal span / aspect and half the
            // vertical span; the larger one determines orthographic size.
            var extent = bounds.extents;
            float Span(Vector3 axis) => Mathf.Abs(axis.x) * extent.x +
                Mathf.Abs(axis.y) * extent.y + Mathf.Abs(axis.z) * extent.z;
            camera.orthographicSize = Mathf.Max(.6f,
                Span(up), Span(right) / Mathf.Max(.01f, camera.aspect)) * (1f + portrait.Padding);
            var target = stage.TransformPoint(bounds.center);
            camera.transform.position = target + direction * 6f;
            camera.transform.LookAt(target);
        }
    }
}
