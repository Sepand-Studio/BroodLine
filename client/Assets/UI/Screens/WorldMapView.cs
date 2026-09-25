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
        readonly Label _legend;
        readonly Label _selectedName, _selectedDetail, _routeSummary, _routeNames;
        readonly Button _selectedAction;
        readonly List<Button> _markers = new List<Button>();
        readonly List<(VisualElement Line, string A, string B)> _edges = new List<(VisualElement, string, string)>();
        Action<string> _onPick;
        MapScreenModel _model;
        string _selectedId;
        float _zoom = 1f;
        Vector2 _pan, _lastPointer, _tapStart;
        bool _dragging, _panned;

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
            var mapStatus = new VisualElement { name = "map-status" };
            mapStatus.AddToClassList("world-map__status");
            _selectedName = new Label { name = "selected-region" };
            _selectedName.AddToClassList("world-map__selected-name");
            mapStatus.Add(_arkLabel);
            mapStatus.Add(_selectedName);
            atlasCard.Body.Add(mapStatus);
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
            _legend = new Label { name = "map-legend" };
            _legend.AddToClassList("world-map__legend");
            atlasCard.Body.Add(_legend);
            // The 44px touch areas overlap on the dense inner ring. Capture
            // taps before a Button chooses its sibling by draw order, then
            // resolve the nearest visible marker center on release.
            _viewport.RegisterCallback<PointerDownEvent>(OnMapDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnMapMove);
            _viewport.RegisterCallback<PointerUpEvent>(OnMapUp);
            _viewport.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            _viewport.RegisterCallback<WheelEvent>(e => { SetZoom(_zoom + (e.delta.y < 0 ? .25f : -.25f)); e.StopPropagation(); });
            var selection = new VisualElement { name = "selection" };
            selection.AddToClassList("world-map__selection");
            _selectedDetail = new Label { name = "selected-detail" };
            _selectedDetail.AddToClassList("world-map__selected-detail");
            _routeSummary = new Label { name = "route-summary" };
            _routeSummary.AddToClassList("world-map__route-summary");
            _routeNames = new Label { name = "route-names" };
            _routeNames.AddToClassList("world-map__route-names");
            _selectedAction = new Button(() => _onPick?.Invoke(_selectedId)) { name = "selected-action" };
            _selectedAction.AddToClassList("btn-secondary");
            _selectedAction.AddToClassList("world-map__selected-action");
            selection.Add(_selectedDetail);
            selection.Add(_routeSummary);
            selection.Add(_routeNames);
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
            if (e.button != 0) return;
            _dragging = true;
            _tapStart = new Vector2(e.position.x, e.position.y);
            _lastPointer = _tapStart;
            _panned = false;
            _viewport.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        void OnMapMove(PointerMoveEvent e)
        {
            if (!_dragging || !_viewport.HasPointerCapture(e.pointerId)) return;
            var current = new Vector2(e.position.x, e.position.y);
            if (!_panned && (current - _tapStart).sqrMagnitude < 36f) return;
            _panned = true;
            _pan += current - _lastPointer;
            _lastPointer = current;
            ApplyView();
            e.StopPropagation();
        }

        void OnMapUp(PointerUpEvent e)
        {
            if (!_viewport.HasPointerCapture(e.pointerId)) return;
            if (!_panned) SelectNearest(new Vector2(e.position.x, e.position.y));
            _dragging = false;
            _viewport.ReleasePointer(e.pointerId);
            e.StopPropagation();
        }

        void SelectNearest(Vector2 point)
        {
            Button nearest = null;
            var best = float.PositiveInfinity;
            foreach (var marker in _markers)
            {
                var bounds = marker.worldBound;
                var center = new Vector2(bounds.center.x, bounds.center.y);
                var distance = (point - center).sqrMagnitude;
                var radius = bounds.width * .5f;
                if (distance > radius * radius || distance >= best) continue;
                nearest = marker;
                best = distance;
            }
            if (nearest != null) Select(nearest.name.Substring("marker-".Length));
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
            var limit = (_zoom - 1f) * 150f;
            _pan.x = Mathf.Clamp(_pan.x, -limit, limit);
            _pan.y = Mathf.Clamp(_pan.y, -limit, limit);
            _atlas.transform.scale = new Vector3(_zoom, _zoom, 1f);
            _atlas.transform.position = new Vector3(_pan.x, _pan.y, 0f);
        }

        public void Bind(MapScreenModel m, Action<string> onPick)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _model = m;
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
                    row.Selected = r.Id == m.CurrentRegionId;
                    card.Body.Add(row);
                }
                _bands.Add(card);
            }
        }

        void BuildAtlas(MapScreenModel model)
        {
            _atlas.Clear();
            _markers.Clear();
            _edges.Clear();
            var here = RegionCatalog.Find(model.CurrentRegionId);
            var preview = UsesPreviewOrigin(model);
            _arkLabel.text = (preview ? "PREVIEW ORIGIN  ·  " : "ARK POSITION  ·  ")
                + (here != null ? here.Name : "Unknown");
            _legend.text = preview
                ? "P  PREVIEW   □  REACH GATE\nI–III  LANES   1–N  ROUTE HOPS"
                : "A  ARK   □  REACH GATE\nI–III  LANES   1–N  ROUTE HOPS";

            foreach (var (name, diameter) in new[] { ("inner", 96), ("mid", 180), ("outer", 252) })
            {
                var ring = new VisualElement { name = "ring-" + name };
                ring.AddToClassList("world-map__ring");
                ring.style.width = diameter;
                ring.style.height = diameter;
                ring.style.left = (300 - diameter) * .5f;
                ring.style.top = (300 - diameter) * .5f;
                _atlas.Add(ring);
            }

            var plotted = new List<(MapRegionRow Region, Vector2 Position)>();
            var positions = new Dictionary<string, Vector2>();
            for (int band = 0; band < model.Bands.Count; band++)
            {
                var rows = model.Bands[band].Rows;
                float radius = band == 0 ? 48f : band == 1 ? 90f : 126f;
                for (int i = 0; i < rows.Count; i++)
                {
                    var region = rows[i];
                    float angle = (i / (float)rows.Count) * Mathf.PI * 2f - Mathf.PI * .5f;
                    var position = new Vector2(150f + Mathf.Cos(angle) * radius, 150f + Mathf.Sin(angle) * radius);
                    plotted.Add((region, position));
                    positions.Add(region.Id, position);
                }
            }

            // Draw the catalog's actual borders. The decorative reach rings
            // are not a promise of adjacency, especially on the Outer chain.
            foreach (var region in RegionCatalog.All)
            {
                foreach (var neighbour in region.Neighbours)
                {
                    if (string.CompareOrdinal(region.Id, neighbour) >= 0) continue;
                    if (!positions.TryGetValue(region.Id, out var from) ||
                        !positions.TryGetValue(neighbour, out var to)) continue;
                    var delta = to - from;
                    var length = delta.magnitude;
                    var line = new VisualElement { name = "edge-" + region.Id + "-" + neighbour };
                    line.AddToClassList("world-map__edge");
                    if (RegionCatalog.IsGate(region.Id, neighbour)) line.AddToClassList("world-map__edge--gate");
                    line.style.left = (from.x + to.x - length) * .5f;
                    line.style.top = (from.y + to.y) * .5f - 1f;
                    line.style.width = length;
                    line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                    _atlas.Add(line);
                    _edges.Add((line, region.Id, neighbour));
                }
            }

            foreach (var (region, position) in plotted)
            {
                string id = region.Id;
                var marker = new Button(() => Select(id))
                {
                    name = "marker-" + id,
                    text = region.Id == model.CurrentRegionId
                        ? preview ? "P" : "A"
                        : MapScreen.LaneGlyph(region.Lanes),
                    tooltip = region.Name + " · " + region.Detail
                };
                marker.AddToClassList("world-map__marker");
                if (region.Id == model.CurrentRegionId) marker.AddToClassList("world-map__marker--here");
                if (region.Gate) marker.AddToClassList("world-map__marker--gate");
                marker.style.left = position.x - 22f;
                marker.style.top = position.y - 22f;
                var face = new Label(marker.text) { name = "marker-face", pickingMode = PickingMode.Ignore };
                face.AddToClassList("world-map__marker-face");
                marker.Add(face);
                _atlas.Add(marker);
                _markers.Add(marker);
            }
            foreach (var band in model.Bands)
                foreach (var row in band.Rows)
                    if (row.Id == model.CurrentRegionId) { Select(row.Id); return; }
        }

        static bool UsesPreviewOrigin(MapScreenModel model)
        {
            foreach (var band in model.Bands)
                foreach (var row in band.Rows)
                    if (row.Here) return false;
            return true;
        }

        /// Select through the same path as a marker tap, also useful for returning from detail.
        public bool Select(string id)
        {
            if (_model == null || string.IsNullOrEmpty(id)) return false;
            foreach (var band in _model.Bands)
                foreach (var row in band.Rows)
                    if (row.Id == id) { SelectRegion(row); return true; }
            return false;
        }

        void SelectRegion(MapRegionRow region)
        {
            var preview = UsesPreviewOrigin(_model);
            var origin = region.Id == _model.CurrentRegionId;
            _selectedId = region.Id;
            _selectedName.text = region.Name;
            _selectedName.style.display = origin ? DisplayStyle.None : DisplayStyle.Flex;
            _selectedDetail.text = region.Detail;
            _selectedAction.text = region.Here ? "Open region" : "View region";
            int minutes = RegionCatalog.TravelMinutes(_model.CurrentRegionId, region.Id, out var route);
            if (route == null)
            {
                _routeSummary.text = preview ? "NO ROUTE FROM PREVIEW ORIGIN" : "NO ROUTE FROM THE ARK";
                _routeNames.text = "This region is beyond the known lanes.";
            }
            else if (origin)
            {
                _routeSummary.text = preview ? "PREVIEW ORIGIN" : "ARK POSITION";
                _routeNames.text = preview
                    ? "Illustrative routes begin here; the Ark remains in its server region."
                    : "Your journey begins here.";
            }
            else
            {
                int hops = route.Count - 1;
                _routeSummary.text = (preview ? "PREVIEW ROUTE  ·  " : "ROUTE  ·  ")
                    + hops + (hops == 1 ? " HOP  ·  " : " HOPS  ·  ") + MapScreen.Travel(minutes).ToUpperInvariant();
                var names = new string[route.Count];
                for (int i = 0; i < route.Count; i++) names[i] = RegionCatalog.Find(route[i]).Name;
                _routeNames.text = string.Join("  ›  ", names);
            }
            foreach (var marker in _markers)
            {
                string id = marker.name.Substring("marker-".Length);
                int step = route == null ? -1 : route.IndexOf(id);
                marker.text = id == _model.CurrentRegionId ? preview ? "P" : "A" : step >= 0 ? step.ToString() : MapScreen.LaneGlyph(RegionCatalog.Find(id).Lanes);
                marker.Q<Label>("marker-face").text = marker.text;
                marker.EnableInClassList("world-map__marker--route", step >= 0 && id != _model.CurrentRegionId);
                marker.EnableInClassList("world-map__marker--muted", step < 0);
                marker.EnableInClassList("world-map__marker--selected", marker.name == "marker-" + region.Id);
            }
            foreach (var (line, a, b) in _edges)
            {
                int from = route == null ? -1 : route.IndexOf(a);
                int to = route == null ? -1 : route.IndexOf(b);
                line.EnableInClassList("world-map__edge--route", from >= 0 && to >= 0 && Mathf.Abs(from - to) == 1);
            }
        }
    }
}
