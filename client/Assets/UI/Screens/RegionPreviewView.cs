using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The inspectable atlas destination for regions beyond the Ark.
    /// Claims remain on the separate, server-backed RegionView.
    public sealed class RegionPreviewView : VisualElement
    {
        public const string UssClassName = "region-preview";

        readonly ScreenScaffold _scaffold;
        readonly Label _band, _name, _status, _journey, _route, _neighbours, _lanes;
        readonly Button _action;
        RegionPreviewModel _model;
        Action _onStart;
        Func<DateTime> _now;
        Action _onTimerComplete;
        bool _completionSent;

        public RegionPreviewView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("RegionPreviewView").CloneTree(this);
            _scaffold = new ScreenScaffold("Region Detail", pushed: true, eyebrow: RegionPreviewScreen.Eyebrow);

            var hero = new VisualElement { name = "region-art" };
            hero.AddToClassList("region-preview__hero");
            var terrain = Resources.Load<Texture2D>("Art/map/frontier-atlas");
            if (terrain != null) hero.style.backgroundImage = new StyleBackground(terrain);
            var shade = new VisualElement(); shade.AddToClassList("region-preview__hero-shade");
            _band = new Label { name = "region-band" }; _band.AddToClassList("region-preview__band");
            _name = new Label { name = "region-name" }; _name.AddToClassList("region-preview__name");
            shade.Add(_band); shade.Add(_name); hero.Add(shade);
            _scaffold.Content.Add(hero);

            var journeyCard = new SectionCard("JOURNEY FROM THE ARK");
            journeyCard.AddToClassList("region-preview__journey-card");
            _status = new Label { name = "travel-status" }; _status.AddToClassList("region-preview__status");
            _journey = new Label { name = "travel-time" }; _journey.AddToClassList("region-preview__journey");
            _route = new Label { name = "travel-route" }; _route.AddToClassList("region-preview__route");
            journeyCard.Body.Add(_status); journeyCard.Body.Add(_journey); journeyCard.Body.Add(_route);
            _scaffold.Content.Add(journeyCard);

            var territory = new SectionCard("TERRITORY");
            _lanes = new Label { name = "region-lanes" }; _lanes.AddToClassList("region-preview__territory");
            _neighbours = new Label { name = "region-neighbours" }; _neighbours.AddToClassList("region-preview__territory");
            territory.Body.Add(_lanes); territory.Body.Add(_neighbours);
            _scaffold.Content.Add(territory);

            _action = new Button { name = "preview-relocation" };
            _action.AddToClassList("btn-primary");
            _action.clicked += () => _onStart?.Invoke();
            _scaffold.CtaRow.Add(_action);
            _scaffold.FooterNote = RegionPreviewScreen.PreviewNote;
            Add(_scaffold);

            schedule.Execute(Tick).Every(1000);
        }

        public void Bind(RegionPreviewModel model, Action onStart, Action onBack,
            Func<DateTime> now = null, Action onTimerComplete = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _onStart = onStart;
            _now = now ?? (() => DateTime.UtcNow);
            _onTimerComplete = onTimerComplete;
            _completionSent = false;
            _scaffold.OnBack = onBack;
            _band.text = model.Band;
            _name.text = model.Name;
            _status.text = model.Status;
            _route.text = model.Route;
            _lanes.text = MapScreen.Lanes(model.Lanes) + " · " + model.Hops + (model.Hops == 1 ? " hop" : " hops");
            _neighbours.text = "Borders  " + model.Neighbours;
            _action.text = model.Action;
            _action.SetEnabled(model.CanStart);
            Tick();
        }

        void Tick()
        {
            if (_model == null) return;
            if (!_model.Active || !_model.EndsAt.HasValue)
            {
                _journey.text = _model.Minutes < 0 ? "No route" : MapScreen.Travel(_model.Minutes) + " · " + _model.Origin + " to " + _model.Name;
                return;
            }
            var remaining = _model.EndsAt.Value - _now();
            if (remaining <= TimeSpan.Zero)
            {
                if (_completionSent) return;
                _completionSent = true;
                _onTimerComplete?.Invoke();
                return;
            }
            _journey.text = "Preview arrival in " + FormatRemaining(remaining);
        }

        static string FormatRemaining(TimeSpan remaining)
        {
            var total = (int)Math.Ceiling(remaining.TotalSeconds);
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
