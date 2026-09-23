using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Frontier
{
    /// Bounded pool; a burst of catch-up ticks never creates an unbounded UI tree.
    public sealed class FrontierFloatingCues
    {
        const int Capacity = 18;
        readonly Label[] _labels = new Label[Capacity];
        readonly Vector3[] _positions = new Vector3[Capacity];
        readonly float[] _ages = new float[Capacity];
        int _next;

        public FrontierFloatingCues(VisualElement parent)
        {
            for (int i = 0; i < Capacity; i++)
            {
                var label = new Label { enableRichText = false, pickingMode = PickingMode.Ignore };
                label.AddToClassList("floating-cue"); label.style.display = DisplayStyle.None;
                parent.Add(label); _labels[i] = label; _ages[i] = 2;
            }
        }

        public void Show(string text, Vector3 position, bool danger)
        {
            int index = _next; _next = (_next + 1) % Capacity;
            _labels[index].text = text; _labels[index].EnableInClassList("danger", danger);
            _labels[index].style.display = DisplayStyle.Flex;
            _positions[index] = position; _ages[index] = 0;
        }

        public void Advance(Camera camera, Vector2 size, float delta, bool reducedMotion)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_ages[i] >= 1) continue;
                _ages[i] = Mathf.Min(1, _ages[i] + delta / .9f);
                if (_ages[i] >= 1) { _labels[i].style.display = DisplayStyle.None; continue; }
                var p = camera.WorldToViewportPoint(_positions[i]);
                _labels[i].style.left = p.x * size.x - 38;
                _labels[i].style.top = (1 - p.y) * size.y - 30 - (reducedMotion ? 0 : _ages[i] * 25);
                _labels[i].style.opacity = reducedMotion ? 1 : Mathf.Clamp01((1 - _ages[i]) * 3);
            }
        }
    }
}
