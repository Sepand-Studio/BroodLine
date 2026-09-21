using UnityEngine;

namespace Broodline.Creatures
{
    /// Body-level, procedural, no clips - bible 10.3's "animation is
    /// body-level, never trait-level", and the Animator cost rig_proof.md
    /// section 8 never measured is not incurred. Breathe at idle, bob when
    /// moving, a flinch on demand, and damage as posture (bible 10.7): a
    /// droop and a desaturation, never injury.
    public sealed class CreatureMotion : MonoBehaviour
    {
        public const float BreathHz = 0.6f;
        public const float BreathAmount = 0.035f;
        public const float BobHz = 2.4f;
        public const float BobAmount = 0.06f;
        public const float FlinchSeconds = 0.25f;
        public const float DroopDegrees = 18f;

        public bool Moving;
        public float Hurt01;
        /// Growth, applied by CreatureAssembler; multiplies the root's scale.
        public float GrowthScale = 1f;

        Transform _root;
        Vector3 _restPosition;
        // Per-instance materials, not a MaterialPropertyBlock: a block overrides
        // only what the GPU sees and never touches Renderer.material's own
        // values, so a caller (or a test) reading .material.GetFloat back would
        // see the shared default forever. Damage-as-posture is read back
        // elsewhere (portrait, roster), so the instance has to actually hold it.
        Material[] _materials;
        float _flinchUntil = -1f;
        float _phase;

        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");

        void Awake()
        {
            _root = transform.Find("root") ?? transform;
            _restPosition = _root.localPosition;
            var renderers = GetComponentsInChildren<Renderer>();
            _materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) _materials[i] = renderers[i].material;
            _phase = Random.value * 6.28f;   // a wave is not a chorus line
        }

        public void Flinch() => _flinchUntil = Time.time + FlinchSeconds;

        void Update() => Tick(Time.time, Time.deltaTime);

        public void Tick(float time, float dt)
        {
            if (_root == null) Awake();
            float t = time + _phase;

            float breath = 1f + Mathf.Sin(t * BreathHz * 6.2832f) * BreathAmount * (1f - 0.5f * Hurt01);
            float squash = time < _flinchUntil ? 0.85f : 1f;
            _root.localScale = new Vector3(1f / Mathf.Sqrt(squash), breath * squash, 1f / Mathf.Sqrt(squash)) * GrowthScale;

            float bob = Moving ? Mathf.Abs(Mathf.Sin(t * BobHz * 3.1416f)) * BobAmount : 0f;
            _root.localPosition = _restPosition + Vector3.up * bob;

            _root.localRotation = Quaternion.Euler(Hurt01 * DroopDegrees, 0f, 0f);

            foreach (var m in _materials) m.SetFloat(DesaturateId, Hurt01);
        }
    }
}
