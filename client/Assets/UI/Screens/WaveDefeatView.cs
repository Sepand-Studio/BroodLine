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
    ///
    /// THE DIAGNOSIS STILL COMES FROM `sim`'s ECHO AND THE CLIENT'S OWN
    /// OUTCOME, AND `api` STILL NEVER PARSES A REPLAY. Nothing in this task
    /// touched that path and nothing about putting the sentence in a
    /// SectionCard invites it to move: `WaveSubmitResponse.breaches` is the
    /// server's echo of what `sim` re-simulated, `WaveReport.Breaches` is what
    /// the device's own run produced, and this view renders the report it is
    /// handed. The card is a surface. It reads no bytes.
    [UxmlElement]
    public partial class WaveDefeatView : VisualElement
    {
        public const string UssClassName = "wave-defeat-view";
        public const string GrantUssClassName = "wave-defeat-view__grant";

        readonly ScreenScaffold _scaffold;
        readonly Label _headline;
        readonly Label _diagnosis;
        readonly SectionCard _diagnosisCard;
        readonly Label _granted;
        readonly VisualElement _grants;
        readonly VisualElement _resupply;
        readonly Button _retry;
        readonly Button _roster;

        Action _onRetry;
        Action _onRoster;

        public WaveDefeatView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("WaveDefeatView");
            tree.CloneTree(this);

            _headline = this.Q<Label>("headline");
            _diagnosis = this.Q<Label>("diagnosis");
            _granted = this.Q<Label>("granted");
            _grants = this.Q<VisualElement>("grants");
            _resupply = this.Q<VisualElement>("resupply");
            _retry = this.Q<Button>("retry");
            _roster = this.Q<Button>("roster");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: true, and this one is in the handoff's push table by
            // name: "Wave Defeat | Wave Defense (on loss)". The CHEVRON
            // follows `Bind`'s `onBack` and not this flag; see below.
            _scaffold = new ScreenScaffold(WaveDefeatScreen.Title, pushed: true);

            // THE TEACHING SENTENCE GETS THE CARD, which is the one place on
            // this screen where a component replaces a hand-built surface
            // rather than a bare row. The old `__diagnosis` rule was
            // --surface, --radius-card, a 1px --divider border and
            // --space-4 of padding - SectionCard's surface, written out
            // again, minus the elevation that lifts it off the paper.
            _diagnosisCard = new SectionCard { name = "diagnosis-card" };
            _diagnosisCard.Body.Add(_diagnosis);

            // The UXML authors this screen's furniture as children of this
            // element and the scaffold's slots take them over here - a
            // re-parent rather than a second tree, so each element is
            // authored in exactly one place.
            //
            // BOTH CTAs ARE IN THE CTA ROW, WHICH STACKS THEM: the scaffold's
            // `__cta-row` is `flex-direction: column`, so the retry sits over
            // the roster in the handoff's order of weight rather than beside
            // it at half width.
            _scaffold.Content.Add(_headline);
            _scaffold.Content.Add(_diagnosisCard);
            _scaffold.Content.Add(_resupply);
            _scaffold.CtaRow.Add(_retry);
            _scaffold.CtaRow.Add(_roster);
            Add(_scaffold);

            // Registered once against fields the next Bind overwrites, so a
            // re-bound instance cannot stack a second handler behind the
            // first - `DeployView` established this.
            _retry.clicked += () => _onRetry?.Invoke();
            _roster.clicked += () => _onRoster?.Invoke();
        }

        /// `roster` is the secondary CTA and `onBack` the chevron, and NULL
        /// REMOVES EACH OF THEM RATHER THAN LEAVING IT INERT. Same rule
        /// `ScreenScaffold.OnBack` is written to and for the same reason: a
        /// player taps a control that does nothing, gets no error and no
        /// transition, and reads the app as broken rather than as busy.
        ///
        /// Nothing passes either today. `FtueDirector` reaches this screen
        /// through `ScreenFlow.ShowAsync` (`ScreenHost.Show`, back stack
        /// cleared) with `retry: resume` and nothing else, and
        /// `ScreenFlow.PushAsync` has no caller in the client at all - so the
        /// screen renders one CTA, which is what it rendered before this task.
        /// The second appears the moment a caller has somewhere to send it.
        public void Bind(WaveReport report, IReadOnlyList<CreatureDto> granted, Action retry,
                         Action roster = null, Action onBack = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            _scaffold.OnBack = onBack;

            // THE FIRST breach, not the worst or the last. It is the one that
            // started the defeat, and on the wave this screen was designed
            // for - wave 6, one Courser, integrity 2 - it is the only one.
            var first = report.Breaches != null && report.Breaches.Count > 0 ? report.Breaches[0] : null;

            Collapsing(_headline, WaveDefeatScreen.Headline(first));

            // THE DIAGNOSIS CARD DREW ITSELF EMPTY, AND IT IS THE FIFTH
            // INSTANCE OF THAT SHAPE THIS PHASE HAS FOUND. `Diagnosis`
            // returns the empty string whenever the bundle names no counter
            // for the raider - `WaveDefeatScreen.Headline`'s own comment
            // explains why that case drops the sentence rather than faking
            // one - and the panel around it existed unconditionally, so the
            // screen that had nothing to teach still drew a padded white card
            // with a border and nothing inside it. The four before it:
            // LineageView's notice and FounderNamingView's blocker (Task 9),
            // DeployView's blocker and SpliceChamberView's coverage warning
            // (Task 10). The card now collapses; the Label's TEXT still
            // round-trips to string.Empty, which is what
            // `WaveDefeat_ABreachTheBundleAnswersWithNothing` reads.
            _diagnosis.text = WaveDefeatScreen.Diagnosis(first) ?? string.Empty;
            Collapse(_diagnosisCard, _diagnosis.text);

            _granted.text = WaveDefeatScreen.Resupply(granted) ?? string.Empty;

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

            // THE RESUPPLY BLOCK COLLAPSES AS A UNIT AND TAKES NO EmptyState,
            // THOUGH TASK 11 STEP 3 ASKS FOR ONE ON EVERY LIST.
            // `WaveDefeatScreen.Resupply` already decided this in prose and
            // the decision is load-bearing: "an empty grant says nothing
            // rather than promising a resupply that is not there." An empty
            // state saying so out loud would be the screen announcing a gift
            // it did not receive, on the one screen the bible cares most
            // about not lying on. The sentence and the cards are one
            // announcement, so they appear and disappear together.
            _resupply.style.display = string.IsNullOrEmpty(_granted.text) && _grants.childCount == 0
                ? DisplayStyle.None : DisplayStyle.Flex;

            // bible 4.11: "Free retry. No paywall on failure, ever." There is
            // no state under which this button is disabled, which is the
            // point - so it is enabled unconditionally rather than from a
            // flag a future caller could pass false.
            _retry.text = WaveDefeatScreen.RetryLabel;
            _retry.SetEnabled(true);
            _onRetry = retry;

            _roster.text = WaveDefeatScreen.RosterLabel;
            _onRoster = roster;
            _roster.style.display = roster == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// A Label that takes up no room when it has nothing to say.
        ///
        /// The headline is 28px, so an empty one is a blank line the height of
        /// the largest type in the app. The TEXT still round-trips to
        /// string.Empty, which is what `WaveScreensTests` reads.
        static void Collapsing(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// The same rule applied to the surface AROUND a sentence rather than
        /// to the sentence. Hidden rather than removed, because the Label
        /// inside has to stay reachable: `Q<Label>("diagnosis").text` is how
        /// three tests read the empty case, and a removed subtree answers
        /// null.
        static void Collapse(VisualElement element, string text)
        {
            element.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
