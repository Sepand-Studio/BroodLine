using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// Bounded, non-pickable combat labels positioned in the HUD's own space.
    public sealed class FloatingCuePool
    {
        public const int Capacity = 18;
        const float Lifetime = .9f;
        readonly VisualElement _layer;
        readonly Label[] _labels = new Label[Capacity];
        readonly Vector3[] _world = new Vector3[Capacity];
        readonly float[] _ages = new float[Capacity];
        int _next;

        public FloatingCuePool(VisualElement layer)
        {
            _layer = layer ?? throw new ArgumentNullException(nameof(layer));
            for (int i = 0; i < Capacity; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore, enableRichText = false };
                label.AddToClassList("wave-hud-view__cue");
                label.style.display = DisplayStyle.None;
                _layer.Add(label);
                _labels[i] = label;
                _ages[i] = Lifetime;
            }
        }

        public void Show(string text, Vector3 world, bool danger)
        {
            int i = _next; _next = (_next + 1) % Capacity;
            _labels[i].text = text;
            _labels[i].EnableInClassList("danger", danger);
            _labels[i].style.display = DisplayStyle.Flex;
            _labels[i].style.opacity = 1;
            _world[i] = world;
            _ages[i] = 0;
        }

        public void Advance(Camera camera, float delta, bool reducedMotion)
        {
            if (camera == null || _layer.panel == null) return;
            for (int i = 0; i < Capacity; i++)
            {
                if (_ages[i] >= Lifetime) continue;
                _ages[i] = Mathf.Min(Lifetime, _ages[i] + Mathf.Max(0, delta));
                if (_ages[i] >= Lifetime) { _labels[i].style.display = DisplayStyle.None; continue; }
                if (camera.WorldToViewportPoint(_world[i]).z <= 0)
                { _labels[i].style.display = DisplayStyle.None; continue; }
                var inPanel = RuntimePanelUtils.CameraTransformWorldToPanel(_layer.panel, _world[i], camera);
                var local = _layer.WorldToLocal(inPanel);
                float rise = reducedMotion ? 0 : _ages[i] / Lifetime * 24f;
                _labels[i].style.left = Mathf.Clamp(local.x - 28f, 0f, Mathf.Max(0f, _layer.contentRect.width - 56f));
                _labels[i].style.top = Mathf.Clamp(local.y - 30f - rise, 0f, Mathf.Max(0f, _layer.contentRect.height - 28f));
                _labels[i].style.opacity = reducedMotion ? 1f : Mathf.Clamp01((Lifetime - _ages[i]) * 3f);
            }
        }
    }
}
