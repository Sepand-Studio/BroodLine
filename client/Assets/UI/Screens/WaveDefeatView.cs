using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Wave Defeat. `screen_inventory_v2` section 10 calls it "the single most
    /// important teaching screen in the game", and bible 9.3 says why: a
    /// player who first meets an unanswerable raider at day ten reads it as
    /// the game breaking, and the same experience in session two reads as a
    /// rule being taught - but ONLY if the reason is named.
    ///
    /// So this screen names two things and offers one: the raider that broke
    /// through, the trait that would have answered it, and a free retry.
    /// Every sentence comes from `WaveDefeatScreen`; none is authored here.
    ///
    /// It binds a `WaveReport` - PLAIN DATA from `Broodline.Model`, built in
    /// `Broodline.Game` out of the engine's `Outcome`. `Broodline.UI`
    /// references no engine assembly and this screen is the reason that
    /// constraint has teeth: the counter it prints is the one the CONTENT
    /// BUNDLE authored, not one `Stats.CounterFor` derived, so retuning the
    /// answer to a raider does not need a client build.
    [UxmlElement]
    public partial class WaveDefeatView : VisualElement
    {
        public const string UssClassName = "wave-defeat-view";
        public const string GrantUssClassName = "wave-defeat-view__grant";

        readonly Label _headline;
        readonly Label _diagnosis;
        readonly Label _granted;
        readonly VisualElement _grants;
        readonly Button _retry;

        Action _onRetry;

        public WaveDefeatView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("WaveDefeatView");
            tree.CloneTree(this);

            _headline = this.Q<Label>("headline");
            _diagnosis = this.Q<Label>("diagnosis");
            _granted = this.Q<Label>("granted");
            _grants = this.Q<VisualElement>("grants");
            _retry = this.Q<Button>("retry");

            // Registered once against a field the next Bind overwrites, so a
            // re-bound instance cannot stack a second handler behind the
            // first - `DeployView` established this.
            _retry.clicked += () => _onRetry?.Invoke();
        }

        public void Bind(WaveReport report, IReadOnlyList<CreatureDto> granted, Action retry)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            // THE FIRST breach, not the worst or the last. It is the one that
            // started the defeat, and on the wave this screen was designed
            // for - wave 6, one Courser, integrity 2 - it is the only one.
            var first = report.Breaches != null && report.Breaches.Count > 0 ? report.Breaches[0] : null;

            _headline.text = WaveDefeatScreen.Headline(first);
            _diagnosis.text = WaveDefeatScreen.Diagnosis(first);
            _granted.text = WaveDefeatScreen.Resupply(granted);

            _grants.Clear();
            if (granted != null)
            {
                foreach (var creature in granted)
                {
                    var card = new CreatureCard { name = creature.CreatureId.ToString() };
                    // No `counters` map reaches this Bind, for the same reason
                    // it does not reach `DeployView`'s: nothing in this
                    // assembly can derive one, and the caller's signature
                    // carries none.
                    card.Bind(creature, null);
                    card.AddToClassList(GrantUssClassName);
                    _grants.Add(card);
                }
            }

            // bible 4.11: "Free retry. No paywall on failure, ever." There is
            // no state under which this button is disabled, which is the
            // point - so it is enabled unconditionally rather than from a
            // flag a future caller could pass false.
            _retry.text = WaveDefeatScreen.RetryLabel;
            _retry.SetEnabled(true);
            _onRetry = retry;
        }
    }
}
