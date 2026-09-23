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
        Vector3 _headRestPosition;
        int _limbEnd;
        SkinnedMeshRenderer _renderer;
        MaterialPropertyBlock _properties;
        int _tintId;

        public void Initialize(string id, Transform[] bones, SkinnedMeshRenderer renderer, float phase, int limbEnd)
        {
            // Called by FrontierArt on the main thread, including for inactive
            // objects where Awake has not run. Native rendering objects cannot
            // be constructed in a MonoBehaviour field initializer.
            if (_properties == null) _properties = new MaterialPropertyBlock();
            _tintId = Shader.PropertyToID("_Tint");
            SpeciesId = id; Bones = bones; _renderer = renderer; _phase = phase; _limbEnd = limbEnd;
            _headRestPosition = bones[1].localPosition;
            Dorsal = Socket("sk_dorsal", bones[0], id == "vetch" ? FrontierVetch.DorsalPosition : id == "pale" ? new Vector3(-.08f,.66f,0) : new Vector3(-.13f,.84f,0), Quaternion.identity);
            Flank = Socket("sk_flank", bones[0], id == "vetch" ? FrontierVetch.FlankPosition : id == "pale" ? new Vector3(0,.44f,-.23f) : new Vector3(-.07f,.47f,-.49f), Quaternion.Euler(-90,0,0));
            Crown = Socket("sk_crown", bones[1], id == "pale" ? new Vector3(.04f,.16f,0) : new Vector3(.06f,.18f,0), Quaternion.identity);
        }

        static Transform Socket(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false); socket.localPosition = position; socket.localRotation = rotation;
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
            float phase = time + _phase;
            float breath = animate ? Mathf.Sin(phase * 2.4f) * .018f : 0;
            float recoil = animate && time < _attackUntil ? Mathf.Sin(Mathf.Clamp01((_attackUntil-time)/.28f)*Mathf.PI) : 0;
            float greeting = animate && time < _greetingUntil ? Mathf.Sin(Mathf.Clamp01((_greetingUntil-time)/1.1f)*Mathf.PI*2) : 0;
            float celebration = animate && time < _celebrationUntil ? Mathf.Abs(Mathf.Sin(Mathf.Clamp01((_celebrationUntil-time)/1.6f)*Mathf.PI*3)) : 0;
            float idleTilt = animate && !Moving && Hurt < .6f ? Mathf.Sin(phase * (SpeciesId == "ember" ? 1.7f : .7f)) : 0;
            float growth = 1 + Mathf.Clamp01(Growth) * .14f;
            float hover = animate && SpeciesId == "pale" ? Mathf.Sin(phase * 1.5f) * .035f : 0;
            Bones[0].localPosition = new Vector3(0, hover + celebration * .07f, 0);
            Bones[0].localScale = new Vector3(growth, growth * (1 + breath - recoil*.055f), growth);
            Bones[0].localRotation = Quaternion.Euler(0, 0, -Mathf.Clamp01(Hurt)*9 + recoil*4);
            Bones[1].localPosition = _headRestPosition + Vector3.up * (greeting * .015f);
            Bones[1].localScale = Vector3.one * (1 + Mathf.Clamp01(Growth)*.16f);
            Bones[1].localRotation = Quaternion.Euler(idleTilt * 3 + greeting * 7,
                Mathf.Clamp(LookYaw, -24, 24) + idleTilt * (SpeciesId == "ember" ? 6 : 3), recoil*9 + greeting*5);
            for (int i = 2; i < _limbEnd; i++)
            {
                float swing = animate ? Mathf.Sin(phase * (SpeciesId == "pale" ? 2.2f : 9f) + (SpeciesId == "pale" ? 0 : (i%2)*Mathf.PI)) : 0;
                if (SpeciesId == "pale")
                    Bones[i].localRotation = Quaternion.Euler(swing * (i == 2 ? 9 : -9) + (i == 2 ? greeting*16 : 0), 0, 0);
                else if (SpeciesId == "ember" && i >= 4)
                    Bones[i].localRotation = Quaternion.Euler(greeting * (i == 4 ? 18 : -18), 0, (Moving ? -swing*12 : idleTilt*4) + celebration*12);
                else
                    Bones[i].localRotation = Quaternion.Euler(0, 0, Moving ? swing*18 : 0);
            }
            float blinkTime = Mathf.Repeat(phase + 1.7f, SpeciesId == "ember" ? 3.6f : SpeciesId == "pale" ? 5.6f : 4.8f);
            float blink = animate && blinkTime < .16f ? 1 - Mathf.Sin(blinkTime / .16f * Mathf.PI) * .96f : 1;
            for (int i = _limbEnd; i < Bones.Length; i++) Bones[i].localScale = new Vector3(1, blink, 1);
            Color tint = Chilled ? new Color(.65f,.85f,1f) : Color.Lerp(Color.white, new Color(.7f,.75f,.8f), Mathf.Clamp01(Hurt)*.45f);
            if (animate && time < _hitUntil) tint = Color.Lerp(tint, new Color(1.3f,1.15f,.9f), .6f);
            _properties.SetColor(_tintId, tint);
            _renderer.SetPropertyBlock(_properties);
        }

        public void AdvancePresentation(float deltaSeconds)
        {
            if (Paused) return;
            _presentationTime += Mathf.Max(0, deltaSeconds);
            Pose(_presentationTime);
        }

        void Update() => AdvancePresentation(Time.deltaTime);
    }
}
