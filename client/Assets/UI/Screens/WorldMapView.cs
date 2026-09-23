using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    public sealed class WorldMapView : VisualElement
    {
        public const string UssClassName = "world-map";
        public const string RowUssClassName = "world-map__region";

        readonly VisualElement _bands;

        public WorldMapView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("WorldMapView").CloneTree(this);
            _bands = this.Q<VisualElement>("bands");
            _bands.RemoveFromHierarchy();
            var scaffold = new ScreenScaffold(MapScreen.Title, eyebrow: MapScreen.Eyebrow);
            scaffold.Content.Add(_bands);
            Add(scaffold);
        }

        public void Bind(MapScreenModel m, Action<string> onPick)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _bands.Clear();
            foreach (var (heading, rows) in m.Bands)
            {
                var card = new SectionCard(heading);
                foreach (var r in rows)
                {
                    var id = r.Id;
                    var row = new OptionRow(r.Name, r.Detail, () => onPick?.Invoke(id)) { name = "region-" + r.Id };
                    row.AddToClassList(RowUssClassName);
                    row.Selected = r.Here;
                    card.Body.Add(row);
                }
                _bands.Add(card);
            }
        }
    }
}
