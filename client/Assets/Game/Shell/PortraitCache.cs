using System;
using System.Collections.Generic;
using Broodline.Creatures;
using Broodline.Frontier;
using Broodline.UI.Components;
using Broodline.View;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broodline.Game.Shell
{
    /// One offscreen camera paints at most one queued card portrait per frame.
    /// The UI sees only ICreaturePortraitSource; geometry stays in Frontier.
    public sealed class PortraitCache : MonoBehaviour, ICreaturePortraitSource
    {
        public const int Size = 256;
        public const int Capacity = 64;
        const int StudioLayer = CreatureAssembler.StudioLayer;

        sealed class Entry
        {
            public Texture2D Texture;
            public LinkedListNode<string> Node;
        }

        sealed class Pending
        {
            public string Key, Species, First, Second;
            public float Growth;
            public readonly List<Action<Texture2D>> Callbacks = new List<Action<Texture2D>>();
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        readonly LinkedList<string> _recent = new LinkedList<string>();
        readonly Queue<Pending> _queue = new Queue<Pending>();
        readonly Dictionary<string, Pending> _pending = new Dictionary<string, Pending>();

        Camera _camera;
        RenderTexture _target;
        FrontierArt _art;

        public event Action<Texture2D> Evicted;

        public int Count => _entries.Count;
        public int Queued => _queue.Count;

        public static PortraitCache Create(Transform host)
        {
            var go = new GameObject("card-portrait-cache");
            go.transform.SetParent(host, false);
            go.transform.position = new Vector3(0f, -2000f, 0f);
            go.layer = StudioLayer;
            var cache = go.AddComponent<PortraitCache>();

            var camera = new GameObject("camera").AddComponent<Camera>();
            camera.transform.SetParent(go.transform, false);
            camera.gameObject.layer = StudioLayer;
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << StudioLayer;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.enabled = false;

            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Directional;
            light.cullingMask = 1 << StudioLayer;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            light.intensity = 1.1f;

            cache._target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = cache._target;
            cache._camera = camera;
            return cache;
        }

        public Texture2D Request(string species, string first, string second, float growth01,
            Action<Texture2D> ready)
        {
            species = Normalize(species);
            if (!FrontierVisuals.Has(species) || FrontierVisuals.For(species).Kind != FrontierKind.Companion) return null;
            first = Normalize(first); second = Normalize(second);
            var definition = FrontierVisuals.For(species);
            var growth = Mathf.Round(Mathf.Clamp01(growth01) * 4f) / 4f;
            var key = definition.PortraitKey(first, second, growth);
            if (_entries.TryGetValue(key, out var hit))
            {
                _recent.Remove(hit.Node);
                _recent.AddFirst(hit.Node);
                return hit.Texture;
            }
            if (!_pending.TryGetValue(key, out var job))
            {
                job = new Pending { Key = key, Species = species, First = first, Second = second, Growth = growth };
                _pending.Add(key, job);
                _queue.Enqueue(job);
            }
            if (ready != null) job.Callbacks.Add(ready);
            return null;
        }

        static string Normalize(string value)
        {
            value = value?.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(value) || value == "none" ? null : value;
        }

        void Update() => ProcessOne();

        /// Exposed so EditMode can prove one request paints one frame.
        public bool ProcessOne()
        {
            if (_queue.Count == 0) return false;
            var job = _queue.Dequeue();
            _pending.Remove(job.Key);
            Texture2D texture = null;
            try
            {
                texture = Paint(job);
                if (_entries.Count >= Capacity) EvictOldest();
                var node = _recent.AddFirst(job.Key);
                _entries.Add(job.Key, new Entry { Texture = texture, Node = node });
            }
            catch (Exception error)
            {
                Debug.LogWarning("[portrait-cache] " + job.Key + ": " + error.Message);
                if (texture != null) FrontierArt.Release(texture);
                texture = null;
            }
            foreach (var callback in job.Callbacks)
            {
                try { callback(texture); }
                catch (Exception error) { Debug.LogWarning("[portrait-cache] listener: " + error.Message); }
            }
            job.Callbacks.Clear();
            return true;
        }

        Texture2D Paint(Pending job)
        {
            if (_art == null) _art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));
            var creature = _art.Creature(transform, job.Species, job.First, job.Second);
            try
            {
                creature.Growth = job.Growth;
                creature.Pose(0f, true);
                CreatureAssembler.SetLayerRecursively(creature.gameObject, StudioLayer);
                var definition = FrontierVisuals.For(job.Species);
                FrontierPortraitCamera.Frame(_camera, transform, creature.PortraitBounds, creature.Rig, definition.Portrait);
                if (GraphicsSettings.currentRenderPipeline != null)
                    _camera.SubmitRenderRequest(new RenderPipeline.StandardRequest { destination = _target });
                else _camera.Render();

                var previous = RenderTexture.active;
                Texture2D image = null;
                try
                {
                    RenderTexture.active = _target;
                    image = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "portrait-" + job.Key };
                    image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    image.Apply(false, true);
                    return image;
                }
                catch
                {
                    if (image != null) FrontierArt.Release(image);
                    throw;
                }
                finally { RenderTexture.active = previous; }
            }
            finally
            {
                creature.gameObject.SetActive(false);
                FrontierArt.Release(creature.gameObject);
            }
        }

        void EvictOldest()
        {
            var key = _recent.Last.Value;
            _recent.RemoveLast();
            var entry = _entries[key];
            _entries.Remove(key);
            if (Evicted != null)
                foreach (Action<Texture2D> listener in Evicted.GetInvocationList())
                    try { listener(entry.Texture); }
                    catch (Exception error) { Debug.LogWarning("[portrait-cache] eviction listener: " + error.Message); }
            FrontierArt.Release(entry.Texture);
        }

        void OnDestroy()
        {
            foreach (var entry in _entries.Values) FrontierArt.Release(entry.Texture);
            _entries.Clear(); _recent.Clear(); _queue.Clear(); _pending.Clear();
            Evicted = null;
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); FrontierArt.Release(_target); }
            _art?.Dispose();
        }
    }
}
