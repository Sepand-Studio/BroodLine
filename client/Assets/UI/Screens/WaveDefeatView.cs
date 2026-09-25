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

        /// The coral `HeroBand`. Named here rather than typed as a literal at
        /// the call site so the coupling between this screen and the rules in
        /// `WaveDefeatView.uss` that reach into `.hero-band__subject` is
        /// visible from both ends - `HeroBand.ElevationUssClassName`'s own
        /// convention.
        public const string BandUssClassName = "wave-defeat-view__band";

        readonly ScreenScaffold _scaffold;
        readonly HeroBand _band;
        readonly VisualElement _scene;
        readonly Label _kicker;
        readonly Label _headline;
        readonly Label _diagnosis;
        readonly VisualElement _diagnosisHeader;
        readonly SectionCard _diagnosisCard;
        readonly StatCell _leaked;
        readonly StatCell _integrity;
        readonly StatCell _kept;
        readonly Label _granted;
        readonly VisualElement _grants;
        readonly VisualElement _resupply;
        readonly Button _retry;
        readonly Button _roster;

        /// The counter's chip, REBUILT PER BIND rather than re-valued.
        /// `TraitChip` takes its trait, its tier and its species at
        /// construction and exposes no setter - that is its public shape and
        /// this task does not move it - so a new breach means a new chip. This
        /// screen binds once per defeat, so the cost is one element per wave
        /// lost, and it is the field the old one is removed through.
        TraitChip _counter;

        Action _onRetry;
        Action _onRoster;

        public WaveDefeatView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("WaveDefeatView");
            tree.CloneTree(this);

            _kicker = this.Q<Label>("kicker");
            _scene = this.Q<VisualElement>("breach-scene");
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
            //
            // `title: null`, WHICH HIDES THE PAGE HEADER, AND THE HANDOFF IS
            // WHY. `Wave Defeat.dc.html` has no header row at all: `:26` is
            // the status bar and `:31` is the band. `ScreenScaffold`'s
            // constructor treats a null title as "no page header", which is
            // the call `SpliceRevealView` already made on the same evidence in
            // Task 16b, and it takes the kicker slot with it - so the eyebrow
            // moves into the band, which is where `:40` draws it anyway.
            // `WaveDefeatScreen.Title` is kept and its note says why.
            //
            // `pushed: true` IS THEREFORE INERT, said plainly, exactly as it
            // is on Splice Reveal: the chevron lives in the row this hides.
            // The push relationship is still real and `Bind` still sets
            // `OnBack`; no caller passes one today.
            _scaffold = new ScreenScaffold(title: null, pushed: true);

            // ---------------------------------------------------- the band

            // `Wave Defeat.dc.html:31` IS A `HeroBand` IN CORAL, and that is
            // this task's one component decision. Its ramp
            // (`linear-gradient(170deg, #fbeee9, #f6e2e4)`), its
            // `border-radius: 26px` (--radius-band) and its
            // `box-shadow: 0 4px 16px` (the `.elev-2` half of the handoff's
            // 2:1 pair) are the band's three defining values, in a tint the
            // component had no way to draw until now. `HeroBand.Tint`'s
            // comment has the hook and the two cheaper hooks it rejected.
            //
            // `ring: false`, because there is no dashed ring on this screen -
            // `HeroBand`'s own reading is that the ring is an optional layer
            // and three of its six instances have none.
            //
            // CONTENT-SIZED: no `Fill` and no `Fix`. `:31` states no height
            // and no `flex`, unlike `Onboarding`'s floor of 300 and
            // `Splice Reveal`'s fixed 372 - this band is as tall as the four
            // things in it, which is the third of the three shapes
            // `HeroBand`'s class comment lists.
            _band = new HeroBand(ring: false, tint: HeroBand.Tint.Coral) { name = "band" };
            _band.AddToClassList(BandUssClassName);

            _leaked = new StatCell(WaveDefeatScreen.LeakedStatLabel, null) { name = "leaked" };
            _integrity = new StatCell(WaveDefeatScreen.IntegrityStatLabel, null) { name = "integrity" };
            _kept = new StatCell(WaveDefeatScreen.KeptStatLabel, null) { name = "kept" };

            var stats = this.Q<VisualElement>("stats");
            stats.Add(_leaked);
            stats.Add(_integrity);
            stats.Add(_kept);

            var verdict = this.Q<Label>("verdict");
            verdict.text = WaveDefeatScreen.Verdict;

            _band.Subject.Add(_kicker);
            _band.Subject.Add(verdict);
            _band.Subject.Add(_headline);
            _band.Subject.Add(stats);

            // THE TEACHING SENTENCE GETS THE CARD, which is the one place on
            // this screen where a component replaces a hand-built surface
            // rather than a bare row. The old `__diagnosis` rule was
            // --surface, --radius-card, a 1px --divider border and
            // --space-4 of padding - SectionCard's surface, written out
            // again, minus the elevation that lifts it off the paper.
            _diagnosisCard = new SectionCard { name = "diagnosis-card" };

            // THE HANDOFF'S TWO-SIDED CARD HEADER (`:61`), which
            // `SectionCard`'s own single Baloo heading cannot be - the same
            // row `DeployView.CardHeader` and `SpliceChamberView.CardHeader`
            // build, and the fourth instance in this phase. It should be a
            // component; recorded rather than made one here, because a fourth
            // caller is not this task's brief.
            var heading = this.Q<Label>("diagnosis-heading");
            heading.text = WaveDefeatScreen.DiagnosisHeading;

            _diagnosisHeader = new VisualElement { name = "diagnosis-header" };
            _diagnosisHeader.AddToClassList("wave-defeat-view__diagnosis-header");
            _diagnosisHeader.Add(heading);

            _diagnosisCard.Body.Add(_diagnosisHeader);
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
            _scaffold.Content.Add(_scene);
            _scaffold.Content.Add(_band);
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
        /// `wave` and `deployed` ARE THE TWO FACTS THIS SCREEN STATES AND
        /// `WaveReport` DOES NOT CARRY, and both are nullable for
        /// `DeployScreen.FoesStatValue`'s reason: a caller that does not know
        /// must not be made to claim wave zero or an empty deployment.
        /// `Model/WaveReport.cs:48-75` is the whole of what a report holds -
        /// Result, Ticks, IntegrityRemaining, ReplayBytes and Breaches - and
        /// widening it for two numbers the caller already has in hand would
        /// put a screen's chrome into the shared data layer.
        /// `FtueDirector.FightAsync` has both: `start.WaveId` and the
        /// deployment it sent.
        public void Bind(WaveReport report, IReadOnlyList<CreatureDto> granted, Action retry,
                         Action roster = null, Action onBack = null,
                         int? wave = null, int? deployed = null)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            _scaffold.OnBack = onBack;

            // THE KICKER IS IN THE BAND, NOT IN THE SCAFFOLD'S EYEBROW SLOT,
            // because this screen has no page header for that slot to sit
            // under - see the constructor. Collapsed when the caller does not
            // know the wave, on `ScreenScaffold.Eyebrow`'s own contract.
            Collapsing(_kicker, wave == null ? null : WaveDefeatScreen.Eyebrow(wave.Value));

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

            // THE THREE CELLS - `Wave Defeat.dc.html:44-55`. The breach LIST's
            // length rather than a second count, so the number cannot
            // disagree with the breach the headline names.
            _leaked.Value = WaveDefeatScreen.LeakedStatValue(report.Breaches);
            _integrity.Value = WaveDefeatScreen.IntegrityStatValue(report.IntegrityRemaining);
            _kept.Value = WaveDefeatScreen.KeptStatValue(deployed);

            // THE COUNTER, AS A CHIP, WHICH IS WHAT THE HANDOFF'S TEACHING ROW
            // (`:88-93`) DOES WITH THE ANSWER IT NAMES. The sentence beside it
            // says what went wrong; the chip says what to go and get.
            //
            // ITS TIER AND ITS SPECIES COME FROM THE CREATURE THAT JUST
            // ARRIVED, when one of them carries the trait. bible 9.3 is why
            // that is not a coincidence to be clever about: "the counter trait
            // is available immediately: a campaign reward, a species in the
            // next drop, something the player can act on within minutes", and
            // waves_01_12 section 3 makes wave 6's drop the Pale that carries
            // Chill. So the chip is tinted by the species the player now owns
            // and tiered at the coverage they now have, which is the whole
            // lesson in one element rather than a bare word.
            //
            // AND WHEN NOTHING GRANTED CARRIES IT, the chip still names the
            // trait with no tier - which `TraitChip` renders with its
            // `aberrant` outline, because a null tier is that component's
            // marker for "carries no coverage tier at all". That is the one
            // place this screen says something it does not mean, and it says
            // the smaller of two wrong things: the alternative is no chip at
            // all on a defeat whose answer the bundle knows.
            var counter = WaveDefeatScreen.Counter(first);
            _counter?.RemoveFromHierarchy();
            _counter = null;
            if (!string.IsNullOrEmpty(counter))
            {
                var carrier = WaveDefeatScreen.CarrierOf(granted, counter);
                _counter = new TraitChip(counter, WaveDefeatScreen.TierOf(carrier, counter),
                                         carrier == null ? null : carrier.Species) { name = "counter" };
                _diagnosisHeader.Add(_counter);
            }

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
