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
        public bool Moving;
        public bool ReducedMotion;
        public float Hurt;
        public float Growth;
        public bool Chilled;
        public float LookYaw;

        float _attackUntil;
        float _phase;
        float _hitUntil;
        int _limbEnd;
        SkinnedMeshRenderer _renderer;
        readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        static readonly int Tint = Shader.PropertyToID("_Tint");

        public void Initialize(string id, Transform[] bones, SkinnedMeshRenderer renderer, float phase, int limbEnd)
        {
            SpeciesId = id; Bones = bones; _renderer = renderer; _phase = phase; _limbEnd = limbEnd;
            Dorsal = Socket("sk_dorsal", bones[0], id == "pale" ? new Vector3(-.08f,.66f,0) : new Vector3(-.13f,.84f,0), Quaternion.identity);
            Flank = Socket("sk_flank", bones[0], id == "pale" ? new Vector3(0,.44f,-.23f) : new Vector3(-.07f,.47f,-.49f), Quaternion.Euler(-90,0,0));
            Crown = Socket("sk_crown", bones[1], id == "pale" ? new Vector3(.04f,.16f,0) : new Vector3(.06f,.18f,0), Quaternion.identity);
        }

        static Transform Socket(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false); socket.localPosition = position; socket.localRotation = rotation;
            return socket;
        }

        public void Attack() => _attackUntil = Time.time + .28f;
        public void Hit() => _hitUntil = Time.time + .16f;

        public void Pose(float time, bool rest = false)
        {
            if (Bones == null) return;
            bool animate = !rest && !ReducedMotion;
            float phase = time + _phase;
            float breath = animate ? Mathf.Sin(phase * 2.4f) * .018f : 0;
            float recoil = animate && Time.time < _attackUntil ? Mathf.Sin(Mathf.Clamp01((_attackUntil-Time.time)/.28f)*Mathf.PI) : 0;
            float growth = 1 + Mathf.Clamp01(Growth) * .14f;
            Bones[0].localScale = new Vector3(growth, growth * (1 + breath - recoil*.055f), growth);
            Bones[0].localRotation = Quaternion.Euler(0, 0, -Mathf.Clamp01(Hurt)*9 + recoil*4);
            Bones[1].localScale = Vector3.one * (1 + Mathf.Clamp01(Growth)*.16f);
            Bones[1].localRotation = Quaternion.Euler(0, Mathf.Clamp(LookYaw, -24, 24) + (animate ? Mathf.Sin(phase*.8f)*3 : 0), recoil*9);
            for (int i = 2; i < _limbEnd; i++)
            {
                float swing = animate ? Mathf.Sin(phase * (SpeciesId == "pale" ? 2.2f : 9f) + (SpeciesId == "pale" ? 0 : (i%2)*Mathf.PI)) : 0;
                if (SpeciesId == "pale")
                    Bones[i].localRotation = Quaternion.Euler(swing * (i == 2 ? 9 : -9), 0, 0);
                else
                    Bones[i].localRotation = Quaternion.Euler(0, 0, Moving ? swing*18 : 0);
            }
            float blinkTime = Mathf.Repeat(phase + 1.7f, 4.8f);
            float blink = animate && blinkTime < .16f ? 1 - Mathf.Sin(blinkTime / .16f * Mathf.PI) * .96f : 1;
            for (int i = _limbEnd; i < Bones.Length; i++) Bones[i].localScale = new Vector3(1, blink, 1);
            Color tint = Chilled ? new Color(.65f,.85f,1f) : Color.Lerp(Color.white, new Color(.7f,.75f,.8f), Mathf.Clamp01(Hurt)*.45f);
            if (animate && Time.time < _hitUntil) tint = Color.Lerp(tint, new Color(1.3f,1.15f,.9f), .6f);
            _properties.SetColor(Tint, tint);
            _renderer.SetPropertyBlock(_properties);
        }

        void Update() => Pose(Time.time);
    }
}
