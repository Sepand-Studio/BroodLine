using System;
using System.Collections.Generic;
using Broodline.Model.Catalogs;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    public sealed class LabView : VisualElement
    {
        public const string UssClassName = "lab";
        public const string RowUssClassName = "lab__facility";

        readonly VisualElement _rows;
        readonly ScreenScaffold _scaffold;
        readonly Label _coreTier;
        readonly VisualElement _stageFrame, _stage, _hotspots;
        IReadOnlyList<HomeHotspot> _placedHotspots;
        Action<string> _onInspect;

        public LabView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("LabView").CloneTree(this);
            _rows = this.Q<VisualElement>("rows");
            _rows.RemoveFromHierarchy();
            _scaffold = new ScreenScaffold(LabScreen.Title, eyebrow: LabScreen.Eyebrow);
            var core = new SectionCard("ARK CORE");
            core.AddToClassList("lab__core-card");
            var coreLine = new VisualElement(); coreLine.AddToClassList("lab__core-line");
            var coreIcon = new VisualElement();
            coreIcon.AddToClassList("icon"); coreIcon.AddToClassList("icon--facility-core"); coreIcon.AddToClassList("lab__core-icon");
            var coreText = new VisualElement();
            _coreTier = new Label { name = "core-tier" };
            _coreTier.AddToClassList("lab__core-tier");
            coreText.Add(_coreTier);
            var coreDetail = new Label("The Core sets the ceiling for every facility.");
            coreDetail.AddToClassList("lab__core-detail");
            coreText.Add(coreDetail);
            coreLine.Add(coreIcon); coreLine.Add(coreText);
            core.Body.Add(coreLine);

            var plots = new SectionCard(LabScreen.PlotHeading);
            _stageFrame = new VisualElement { name = "lab-stage-frame" };
            _stageFrame.AddToClassList("lab__stage-frame");
            _stage = new VisualElement { name = "lab-stage" };
            _stage.AddToClassList("lab__stage");
            _hotspots = new VisualElement { name = "lab-hotspots" };
            _hotspots.AddToClassList("lab__hotspots");
            _stageFrame.Add(_stage); _stageFrame.Add(_hotspots);
            _stageFrame.RegisterCallback<GeometryChangedEvent>(_ => LayoutHotspots());
            plots.Body.Add(_stageFrame);
            plots.Body.Add(new Label(LabScreen.PlotHint).WithClass("lab__stage-hint"));
            _scaffold.Content.Add(plots);
            _scaffold.Content.Add(core);

            var card = new SectionCard("FACILITIES");
            card.Body.Add(_rows);
            _scaffold.Content.Add(card);
            _scaffold.FooterNote = LabScreen.Preview;
            Add(_scaffold);
        }

        public void Bind(LabScreenModel m, Action<string> onInspect,
            Func<DateTime> now = null, Action onTimerComplete = null,
            IReadOnlyList<HomeHotspot> hotspots = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _coreTier.text = "Tier " + m.CoreTier + " / " + FacilityCatalog.MaxTier;
            _onInspect = onInspect;
            PlaceHotspots(hotspots);
            now = now ?? (() => DateTime.UtcNow);
            _rows.Clear();
            foreach (var f in m.Rows)
            {
                var id = f.Id;
                var row = new OptionRow(f.Name + " · " + LabScreen.TierLabel(f.Tier),
                    f.Upgrading != null || !f.CanUpgrade ? f.Role : f.Detail,
                    () => _onInspect?.Invoke(id)) { name = "facility-" + f.Id };
                row.AddToClassList(RowUssClassName);
                if (f.Id == "core") row.AddToClassList("lab__facility--core");
                var glyph = new VisualElement { name = "glyph" };
                glyph.AddToClassList("icon"); glyph.AddToClassList("icon--" + f.Icon); glyph.AddToClassList("lab__glyph");
                glyph.pickingMode = PickingMode.Ignore;
                row.Add(glyph);
                if (f.Upgrading != null)
                {
                    var timer = new TimerChip { name = "timer" };
                    timer.Bind(f.Upgrading.Value);
                    timer.AddToClassList("lab__timer");
                    row.Add(timer);
                    var endsAt = f.UpgradeEndsAt ?? now() + f.Upgrading.Value;
                    var clock = now;
                    bool completed = false;
                    timer.schedule.Execute(() =>
                    {
                        var remaining = endsAt - clock();
                        timer.Bind(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
                        if (remaining > TimeSpan.Zero || completed) return;
                        completed = true;
                        onTimerComplete?.Invoke();
                    }).Every(1000);
                }
                else if (!string.IsNullOrEmpty(f.Blocker))
                {
                    var blocker = new Label(f.Blocker) { name = "blocker" };
                    blocker.AddToClassList("lab__blocker");
                    row.Add(blocker);
                }
                var inspect = new Label("DETAILS  ›") { name = "inspect" };
                inspect.AddToClassList("lab__inspect");
                inspect.pickingMode = PickingMode.Ignore;
                row.Add(inspect);
                _rows.Add(row);
            }
        }

        public void SetStage(Texture texture)
        {
            if (texture == null) _stage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            else if (texture is RenderTexture rt) _stage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _stage.style.backgroundImage = new StyleBackground((Texture2D)texture);
        }

        void PlaceHotspots(IReadOnlyList<HomeHotspot> hotspots)
        {
            _placedHotspots = hotspots;
            _hotspots.Clear();
            if (hotspots == null) return;
            foreach (var h in hotspots)
            {
                var id = h.Id;
                var marker = new Button(() => _onInspect?.Invoke(id))
                    { name = "lab-plot-" + id, tooltip = h.Label + " · " + LabScreen.TierLabel(h.Tier) };
                marker.AddToClassList("lab__hotspot");
                var facility = FacilityCatalog.Find(id);
                if (facility != null)
                {
                    var glyph = new VisualElement();
                    glyph.AddToClassList("icon");
                    glyph.AddToClassList("icon--" + facility.Icon);
                    glyph.AddToClassList("lab__hotspot-icon");
                    glyph.pickingMode = PickingMode.Ignore;
                    marker.Add(glyph);
                }
                _hotspots.Add(marker);
            }
            LayoutHotspots();
        }

        void LayoutHotspots()
        {
            if (_placedHotspots == null) return;
            float width = _stageFrame.resolvedStyle.width, height = _stageFrame.resolvedStyle.height;
            if (width <= 0f || height <= 0f) return;
            foreach (var h in _placedHotspots)
            {
                var marker = _hotspots.Q<Button>("lab-plot-" + h.Id);
                if (marker == null) continue;
                var point = ArkStageProjection.Point(h.X01, h.Y01, width, height);
                marker.style.left = point.x;
                marker.style.top = point.y;
            }
        }
    }
}
