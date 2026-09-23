using UnityEngine;

namespace Broodline.Frontier
{
    /// Presentation only. Simulation positions belong to the enclosing GameObject.
    public sealed class FrontierCreature : MonoBehaviour
    {
        public string SpeciesId { get; private set; }
        public Transform Dorsal { get; private set; }
        public Transform Flank { get; private set; }
        public Transform Crown { get; private set; }
        public Transform[] Bones { get; private set; }
        public Bounds PortraitBounds { get; internal set; }
        public bool Moving;
        public bool ReducedMotion;
        public bool Paused;
        public float Hurt;
        public float Growth;
        public bool Chilled;
        public float LookYaw;

        float _attackUntil;
        float _phase;
        float _hitUntil;
        float _presentationTime;
        float _greetingUntil;
        float _celebrationUntil;
        public FrontierRigDefinition Rig { get; private set; }
        FrontierBonePose[] _pose;
        SkinnedMeshRenderer _renderer;
        MaterialPropertyBlock _properties;
        int _tintId;

        public void Initialize(FrontierRigDefinition rig, Transform[] bones, SkinnedMeshRenderer renderer, float phase)
        {
            // Native objects are allocated explicitly on the main thread, also for inactive roots.
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _tintId = Shader.PropertyToID("_Tint");
            Rig = rig; SpeciesId = rig.Id; Bones = bones; _renderer = renderer; _phase = phase;
            _pose = new FrontierBonePose[bones.Length];
            Dorsal = Socket("sk_dorsal", rig.Dorsal);
            Flank = Socket("sk_flank", rig.Flank);
            Crown = Socket("sk_crown", rig.Crown);
        }

        Transform Socket(string name, FrontierSocketDefinition definition)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(Bones[definition.Bone], false);
            socket.localPosition = definition.Position - Rig.Bones[definition.Bone].Position;
            socket.localRotation = Quaternion.Euler(definition.Euler);
            socket.localScale = Vector3.one * definition.Scale;
            return socket;
        }

        public void Attack() => _attackUntil = _presentationTime + .28f;
        public void Hit() => _hitUntil = _presentationTime + .16f;
        public void Greet() => _greetingUntil = _presentationTime + 1.1f;
        public void Celebrate() => _celebrationUntil = _presentationTime + 1.6f;

        public void Pose(float time, bool rest = false)
        {
            if (Bones == null || _renderer == null || _properties == null) return;
            bool animate = !rest && !ReducedMotion;
            var state = new FrontierMotionState { Moving = Moving, ReducedMotion = ReducedMotion,
                Growth = Growth, Hurt = Hurt, LookYaw = LookYaw, AttackUntil = _attackUntil,
                HitUntil = _hitUntil, GreetingUntil = _greetingUntil, CelebrationUntil = _celebrationUntil };
            FrontierPose.Sample(Rig, state, time, _phase, _pose, rest);
            for (int i = 0; i < Bones.Length; i++)
            {
                Bones[i].localPosition = _pose[i].Position;
                Bones[i].localRotation = _pose[i].Rotation;
                Bones[i].localScale = _pose[i].Scale;
            }
            Color tint = Chilled ? new Color(.65f,.85f,1f) : Color.Lerp(Color.white, new Color(.7f,.75f,.8f), Mathf.Clamp01(Hurt)*.45f);
            if (animate && time < _hitUntil) tint = Color.Lerp(tint, new Color(1.3f,1.15f,.9f), .6f);
            _properties.SetColor(_tintId, tint);
            _renderer.SetPropertyBlock(_properties);
        }

        public void ResetReactions()
        { _attackUntil = _hitUntil = _greetingUntil = _celebrationUntil = 0; }

        public void AdvancePresentation(float deltaSeconds)
        {
            if (Paused) return;
            _presentationTime += Mathf.Max(0, deltaSeconds);
            Pose(_presentationTime);
        }

        void Update() => AdvancePresentation(Time.deltaTime);
    }
}
