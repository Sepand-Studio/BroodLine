using System;
using System.Collections.Generic;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// THE HOME SCREEN - Phase 10 Task 1.4, the Ark tab. A full-bleed picture
    /// of the base (a RenderTexture the Game layer paints, see HomeStage) with
    /// the facilities as tappable markers laid over it by normalized point,
    /// and under it the Ark's card: name, region, tier, and the four home
    /// actions. No scaffold header: like the founder screen, the subject is
    /// the header.
    public sealed class HomeBaseView : VisualElement
    {
        public const string UssClassName = "home-base";
        public const string HotspotUssClassName = "home-base__hotspot";

        readonly VisualElement _stage;
        readonly VisualElement _stageFrame;
        readonly VisualElement _hotspots;
        readonly Label _name, _subtitle;
        readonly Button _defend, _roster, _codex, _store;
        Action<string> _onPlot;
        IReadOnlyList<HomeHotspot> _placedHotspots;

        public HomeBaseView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("HomeBaseView").CloneTree(this);

            var scaffold = new ScreenScaffold(null);
            var body = this.Q<VisualElement>("body");
            body.RemoveFromHierarchy();
            scaffold.Content.Add(body);
            Add(scaffold);

            _stageFrame = this.Q<VisualElement>("stage-frame");
            _stage = this.Q<VisualElement>("stage");
            _hotspots = this.Q<VisualElement>("hotspots");
            _stageFrame.RegisterCallback<GeometryChangedEvent>(_ => LayoutHotspots());
            _name = this.Q<Label>("ark-name");
            _subtitle = this.Q<Label>("ark-subtitle");
            _defend = this.Q<Button>("defend");
            _roster = this.Q<Button>("roster");
            _codex = this.Q<Button>("codex");
            _store = this.Q<Button>("store");

            _defend.text = HomeScreen.DefendLabel;
            _roster.text = HomeScreen.RosterLabel;
            _codex.text = HomeScreen.CodexLabel;
            _store.text = HomeScreen.StoreLabel;
            this.Q<Label>("eyebrow").text = HomeScreen.Eyebrow;
        }

        public void Bind(HomeScreenModel m, Action onDefend, Action onRoster, Action onCodex, Action onStore, Action<string> onPlot)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _name.text = string.IsNullOrEmpty(m.ArkName) ? HomeScreen.DefaultArkName : m.ArkName;
            _subtitle.text = HomeScreen.Subtitle(m.RegionName, m.CoreTier);
            _defend.clicked += () => onDefend?.Invoke();
            _roster.clicked += () => onRoster?.Invoke();
            _codex.clicked += () => onCodex?.Invoke();
            _store.clicked += () => onStore?.Invoke();
            _onPlot = onPlot;
            PlaceHotspots(m.Hotspots);
        }

        /// The picture. Null clears it and the stage shows its own deep fill.
        public void SetStage(Texture texture)
        {
            if (texture == null) _stage.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            else if (texture is RenderTexture rt) _stage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _stage.style.backgroundImage = new StyleBackground((Texture2D)texture);
        }

        /// Marker positions are projected from the source image into the
        /// visible scale-and-crop frame after each resize. CSS centres each
        /// marker over that point with a half-size translation.
        public void PlaceHotspots(IReadOnlyList<HomeHotspot> hotspots)
        {
            _hotspots.Clear();
            _placedHotspots = hotspots;
            if (hotspots == null) return;
            foreach (var h in hotspots)
            {
                var id = h.Id;
                var marker = new Button(() => _onPlot?.Invoke(id)) { name = "plot-" + h.Id, text = HomeScreen.HotspotLabel(h.Label, h.Tier) };
                marker.AddToClassList(HotspotUssClassName);
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
                var marker = _hotspots.Q<Button>("plot-" + h.Id);
                if (marker == null) continue;
                var point = ArkStageProjection.Point(h.X01, h.Y01, width, height);
                marker.style.left = point.x;
                marker.style.top = point.y;
            }
        }
    }

    /// Maps a 720×1280 stage point into a centred scale-and-crop frame. Both
    /// home and Lab use this so their markers stay over the painted plot when
    /// a short phone crops the source vertically.
    public static class ArkStageProjection
    {
        public static Vector2 Point(float x01, float y01, float frameWidth, float frameHeight)
        {
            float scale = Mathf.Max(frameWidth / 720f, frameHeight / 1280f);
            return new Vector2((frameWidth - 720f * scale) * .5f + Mathf.Clamp01(x01) * 720f * scale,
                (frameHeight - 1280f * scale) * .5f + Mathf.Clamp01(y01) * 1280f * scale);
        }
    }
}
