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
        readonly VisualElement _hotspots;
        readonly Label _name, _subtitle;
        readonly Button _defend, _roster, _codex, _store;
        Action<string> _onPlot;

        public HomeBaseView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("HomeBaseView").CloneTree(this);

            var scaffold = new ScreenScaffold(null);
            var body = this.Q<VisualElement>("body");
            body.RemoveFromHierarchy();
            scaffold.Content.Add(body);
            Add(scaffold);

            _stage = this.Q<VisualElement>("stage");
            _hotspots = this.Q<VisualElement>("hotspots");
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

        /// Markers by fraction of the stage, so a resize never moves them off
        /// their plots: `left`/`top` are percentages and each marker is
        /// translated back by half its own size to sit centred on the point.
        public void PlaceHotspots(IReadOnlyList<HomeHotspot> hotspots)
        {
            _hotspots.Clear();
            if (hotspots == null) return;
            foreach (var h in hotspots)
            {
                var id = h.Id;
                var marker = new Button(() => _onPlot?.Invoke(id)) { name = "plot-" + h.Id, text = HomeScreen.HotspotLabel(h.Label, h.Tier) };
                marker.AddToClassList(HotspotUssClassName);
                marker.style.left = Length.Percent(Mathf.Clamp01(h.X01) * 100f);
                marker.style.top = Length.Percent(Mathf.Clamp01(h.Y01) * 100f);
                _hotspots.Add(marker);
            }
        }
    }
}
