using System;
using UnityEngine;

namespace Broodline.View
{
    /// Short, pooled ground pulses. They are presentation only and never own a sim entity.
    public sealed class BattleVfx : IDisposable
    {
        public const int Capacity = 16;
        const float Lifetime = .42f;
        readonly GameObject[] _objects = new GameObject[Capacity];
        readonly MeshRenderer[] _renderers = new MeshRenderer[Capacity];
        readonly MaterialPropertyBlock[] _blocks = new MaterialPropertyBlock[Capacity];
        readonly float[] _ages = new float[Capacity];
        readonly float[] _sizes = new float[Capacity];
        readonly Color[] _colors = new Color[Capacity];
        readonly Mesh _ring;
        readonly Material _material;
        int _next;

        public BattleVfx(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            _ring = Ring();
            _material = new Material(RuntimeShaders.Require(RuntimeShaders.Unlit)) { name = "Battle pulse" };
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("battle-pulse-" + i);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = _ring;
                _renderers[i] = go.AddComponent<MeshRenderer>();
                _renderers[i].sharedMaterial = _material;
                _blocks[i] = new MaterialPropertyBlock();
                _objects[i] = go;
                _ages[i] = Lifetime;
                go.SetActive(false);
            }
        }

        public void Show(Vector3 world, Color color, float size)
        {
            int i = _next; _next = (_next + 1) % Capacity;
            _ages[i] = 0;
            _sizes[i] = size;
            _colors[i] = color;
            var go = _objects[i];
            go.transform.position = new Vector3(world.x, .07f, world.z);
            go.transform.localScale = Vector3.one * Mathf.Max(.05f, size * .35f);
            _blocks[i].SetColor("_BaseColor", color);
            _renderers[i].SetPropertyBlock(_blocks[i]);
            go.SetActive(true);
        }

        public void Advance(float delta, bool reducedMotion)
        {
            if (delta <= 0) return;
            for (int i = 0; i < Capacity; i++)
            {
                if (_ages[i] >= Lifetime) continue;
                _ages[i] = Mathf.Min(Lifetime, _ages[i] + delta);
                if (_ages[i] >= Lifetime) { _objects[i].SetActive(false); continue; }
                float t = _ages[i] / Lifetime;
                _objects[i].transform.localScale = Vector3.one * _sizes[i] * (reducedMotion ? .72f : .35f + t * .8f);
                _blocks[i].SetColor("_BaseColor", Color.Lerp(_colors[i], _colors[i] * .25f, t));
                _renderers[i].SetPropertyBlock(_blocks[i]);
            }
        }

        static Mesh Ring()
        {
            const int segments = 16;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vertices[i * 2] = new Vector3(Mathf.Cos(a) * .72f, 0, Mathf.Sin(a) * .72f);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                int b = i * 6, n = (i + 1) % segments * 2, c = i * 2;
                triangles[b] = c; triangles[b + 1] = n; triangles[b + 2] = c + 1;
                triangles[b + 3] = c + 1; triangles[b + 4] = n; triangles[b + 5] = n + 1;
            }
            var mesh = new Mesh { name = "Battle pulse ring", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        public void Dispose()
        {
            foreach (var go in _objects) if (go != null) Destroy(go);
            Destroy(_ring); Destroy(_material);
        }

        static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
