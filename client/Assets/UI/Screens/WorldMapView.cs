using System;
using Broodline.Model.Catalogs;
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
        readonly VisualElement _atlas;
        readonly Label _arkLabel;

        public WorldMapView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("WorldMapView").CloneTree(this);
            _bands = this.Q<VisualElement>("bands");
            _bands.RemoveFromHierarchy();
            var scaffold = new ScreenScaffold(MapScreen.Title, eyebrow: MapScreen.Eyebrow);
            var atlasCard = new SectionCard("THE THREE REACHES");
            _arkLabel = new Label { name = "ark-region" };
            _arkLabel.AddToClassList("world-map__ark-label");
            atlasCard.Body.Add(_arkLabel);
            _atlas = new VisualElement { name = "atlas" };
            _atlas.AddToClassList("world-map__atlas");
            atlasCard.Body.Add(_atlas);
            scaffold.Content.Add(atlasCard);
            scaffold.Content.Add(_bands);
            Add(scaffold);
        }

        public void Bind(MapScreenModel m, Action<string> onPick)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            BuildAtlas(m, onPick);
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

        void BuildAtlas(MapScreenModel model, Action<string> onPick)
        {
            _atlas.Clear();
            var here = RegionCatalog.Find(model.CurrentRegionId);
            _arkLabel.text = "ARK POSITION  ·  " + (here != null ? here.Name : "Unknown");

            foreach (var (name, diameter) in new[] { ("inner", 86), ("mid", 168), ("outer", 244) })
            {
                var ring = new VisualElement { name = "ring-" + name };
                ring.AddToClassList("world-map__ring");
                ring.style.width = diameter;
                ring.style.height = diameter;
                ring.style.left = (280 - diameter) * .5f;
                ring.style.top = (280 - diameter) * .5f;
                _atlas.Add(ring);
            }

            for (int band = 0; band < model.Bands.Count; band++)
            {
                var rows = model.Bands[band].Rows;
                float radius = band == 0 ? 43f : band == 1 ? 84f : 122f;
                for (int i = 0; i < rows.Count; i++)
                {
                    var region = rows[i];
                    float angle = (i / (float)rows.Count) * Mathf.PI * 2f - Mathf.PI * .5f;
                    string id = region.Id;
                    var marker = new Button(() => onPick?.Invoke(id))
                    {
                        name = "marker-" + id,
                        text = region.Here ? "A" : "•",
                        tooltip = region.Name + " · " + region.Detail
                    };
                    marker.AddToClassList("world-map__marker");
                    if (region.Here) marker.AddToClassList("world-map__marker--here");
                    marker.style.left = 140f + Mathf.Cos(angle) * radius - 16f;
                    marker.style.top = 140f + Mathf.Sin(angle) * radius - 16f;
                    _atlas.Add(marker);
                }
            }
        }
    }
}
