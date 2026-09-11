using UnityEngine;

namespace Broodline.View
{
    /// Drives a synthetic rig's bones every frame.
    ///
    /// Without this, bone count is free by construction. Unity recomputes a
    /// skinned mesh's bone matrices only when the bone transforms are dirty, so
    /// a rig that never moves costs the same at 48 bones as at 12 — which is
    /// exactly what the first iPad Air 4 sweep reported, and why its bone figure
    /// went into the rig proof brief labelled as a placeholder.
    ///
    /// A real creature is driven by an Animator writing local rotations every
    /// frame. This writes them directly: the same per-bone transform work and
    /// the same resulting hierarchy update, without an AnimationClip to author.
    /// What it does NOT reproduce is the Animator's own graph evaluation, so
    /// this measures the floor of a rig's per-frame cost rather than its total.
    public class BoneAnimator : MonoBehaviour
    {
        /// Kept small enough that a rig stays inside the bounds SyntheticCreature
        /// inflates for it, and large enough that every bone genuinely moves.
        public const float AmplitudeDegrees = 12f;

        Transform[] bones;
        float phase;

        public void Bind(Transform[] rig) => bones = rig;

        /// Entities animating in lockstep are not what a wave looks like, and
        /// identical transforms invite the engine to behave unrepresentatively.
        public void SetPhase(float value) => phase = value;

        void Update() => Tick(Time.time);

        /// Separated from Update so an edit-mode test can drive it directly.
        public void Tick(float time)
        {
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null) continue;
                float a = Mathf.Sin((time + phase) * 2f + i * 0.35f) * AmplitudeDegrees;
                b.localRotation = Quaternion.Euler(a, a * 0.25f, a * 0.5f);
            }
        }
    }
}
