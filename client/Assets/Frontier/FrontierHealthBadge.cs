using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Frontier
{
    /// Retained UI: the text and fill change only when health or status changes.
    public sealed class FrontierHealthBadge : VisualElement
    {
        readonly Label _label;
        readonly VisualElement _fill;
        int _hp = int.MinValue, _maximum = -1;
        bool _chilled;
        public FrontierHealthBadge()
        {
            AddToClassList("health-badge"); pickingMode = PickingMode.Ignore;
            _label = new Label { enableRichText = false, pickingMode = PickingMode.Ignore }; Add(_label);
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.AddToClassList("health-track"); Add(track);
            _fill = new VisualElement { pickingMode = PickingMode.Ignore }; _fill.AddToClassList("health-fill"); track.Add(_fill);
        }
        public void SetHealth(int hp, int maximum, bool chilled = false)
        {
            if (hp == _hp && maximum == _maximum && chilled == _chilled) return;
            _hp = hp; _maximum = maximum; _chilled = chilled;
            _label.text = hp <= 0 ? "Resting" : (chilled ? "Chill · " : "") + hp;
            _fill.style.width = Length.Percent(maximum <= 0 ? 0 : Mathf.Clamp01(hp / (float)maximum) * 100);
            EnableInClassList("fallen", hp <= 0); EnableInClassList("chilled", chilled);
        }
    }
}
