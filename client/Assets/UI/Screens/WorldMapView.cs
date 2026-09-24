using System;
using System.Collections.Generic;
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
        readonly VisualElement _viewport;
        readonly VisualElement _atlas;
        readonly Label _arkLabel;
        readonly Label _selectedName, _selectedDetail;
        readonly Button _selectedAction;
        readonly List<Button> _markers = new List<Button>();
        Action<string> _onPick;
        string _selectedId;
        float _zoom = 1f;
        Vector2 _pan, _lastPointer;
        bool _dragging;

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
            _viewport = new VisualElement { name = "atlas-viewport" };
            _viewport.AddToClassList("world-map__viewport");
            _atlas = new VisualElement { name = "atlas" };
            _atlas.AddToClassList("world-map__atlas");
            var terrain = Resources.Load<Texture2D>("Art/map/frontier-atlas");
            if (terrain != null) _atlas.style.backgroundImage = new StyleBackground(terrain);
            _viewport.Add(_atlas);
            atlasCard.Body.Add(_viewport);
            var mapControls = new VisualElement { name = "map-controls" };
            mapControls.AddToClassList("world-map__controls");
            MapControl(mapControls, "zoom-out", "−", () => SetZoom(_zoom - .25f));
            MapControl(mapControls, "zoom-reset", "Reset", ResetView);
            MapControl(mapControls, "zoom-in", "+", () => SetZoom(_zoom + .25f));
            atlasCard.Body.Add(mapControls);
            _viewport.RegisterCallback<PointerDownEvent>(OnMapDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnMapMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnMapUp);
            _viewport.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            _viewport.RegisterCallback<WheelEvent>(e => { SetZoom(_zoom + (e.delta.y < 0 ? .25f : -.25f)); e.StopPropagation(); });
            var selection = new VisualElement { name = "selection" };
            selection.AddToClassList("world-map__selection");
            _selectedName = new Label { name = "selected-region" };
            _selectedName.AddToClassList("world-map__selected-name");
            _selectedDetail = new Label { name = "selected-detail" };
            _selectedDetail.AddToClassList("world-map__selected-detail");
            _selectedAction = new Button(() => _onPick?.Invoke(_selectedId)) { name = "selected-action" };
            _selectedAction.AddToClassList("btn-secondary");
            _selectedAction.AddToClassList("world-map__selected-action");
            selection.Add(_selectedName);
            selection.Add(_selectedDetail);
            selection.Add(_selectedAction);
            atlasCard.Body.Add(selection);
            scaffold.Content.Add(atlasCard);
            scaffold.Content.Add(_bands);
            Add(scaffold);
        }

        static void MapControl(VisualElement parent, string name, string label, Action action)
        {
            var button = new Button(action) { name = name, text = label };
            button.AddToClassList("btn-secondary");
            button.AddToClassList("world-map__control");
            parent.Add(button);
        }

        void OnMapDown(PointerDownEvent e)
        {
            if (e.target is Button || e.button != 0) return;
            _dragging = true;
            _lastPointer = new Vector2(e.position.x, e.position.y);
            _viewport.CapturePointer(e.pointerId);
        }

        void OnMapMove(PointerMoveEvent e)
        {
            if (!_dragging || !_viewport.HasPointerCapture(e.pointerId)) return;
            var current = new Vector2(e.position.x, e.position.y);
            _pan += current - _lastPointer;
            _lastPointer = current;
            ApplyView();
        }

        void OnMapUp(PointerUpEvent e)
        {
            if (!_viewport.HasPointerCapture(e.pointerId)) return;
            _dragging = false;
            _viewport.ReleasePointer(e.pointerId);
        }

        void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, 1f, 2.25f);
            ApplyView();
        }

        void ResetView()
        {
            _zoom = 1f;
            _pan = Vector2.zero;
            ApplyView();
        }

        void ApplyView()
        {
            var limit = (_zoom - 1f) * 140f;
            _pan.x = Mathf.Clamp(_pan.x, -limit, limit);
            _pan.y = Mathf.Clamp(_pan.y, -limit, limit);
            _atlas.transform.scale = new Vector3(_zoom, _zoom, 1f);
            _atlas.transform.position = new Vector3(_pan.x, _pan.y, 0f);
        }

        public void Bind(MapScreenModel m, Action<string> onPick)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _onPick = onPick;
            BuildAtlas(m);
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

        void BuildAtlas(MapScreenModel model)
        {
            _atlas.Clear();
            _markers.Clear();
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
                    var marker = new Button(() => SelectRegion(region))
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
                    _markers.Add(marker);
                }
            }
            foreach (var band in model.Bands)
                foreach (var row in band.Rows)
                    if (row.Here) { SelectRegion(row); return; }
        }

        void SelectRegion(MapRegionRow region)
        {
            _selectedId = region.Id;
            _selectedName.text = region.Name;
            _selectedDetail.text = region.Detail;
            _selectedAction.text = region.Here ? "Open region" : "View region";
            foreach (var marker in _markers)
                marker.EnableInClassList("world-map__marker--selected", marker.name == "marker-" + region.Id);
        }
    }
}
