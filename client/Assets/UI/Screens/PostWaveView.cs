using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Post-Wave. Design section 5.1 adds this screen to `build_order`'s
    /// Phase 2 list deliberately - beat 3's creature drop happens here, so
    /// the beat set needs a surface that list does not name.
    ///
    /// It binds the SERVER's `WaveSubmitResponse` rather than the local
    /// `WaveReport`, and that is the whole point of the split:
    /// `client_architecture` section 7 makes the client a cache with an
    /// outbox and never a source of truth, so the reward and the verdict a
    /// player is shown are the ones the server returned from re-simulating
    /// the replay - not the ones the device computed.
    ///
    /// THE STAT ROW IS WHERE THAT RESPONSE STOPPED BEING HALF-READ. Every
    /// field of it arrived here from the first commit and only two were
    /// printed; `IntegrityRemaining` - the server's count of what survived,
    /// `Required.Always` on the wire, and the loss condition itself - reached
    /// this `Bind` and went nowhere. `PostWaveScreen.IntegrityStatLabel`'s
    /// comment has the consequence: two players clearing the same wave, one
    /// at 1 integrity and one untouched, read the identical screen.
    [UxmlElement]
    public partial class PostWaveView : VisualElement
    {
        public const string UssClassName = "post-wave-view";
        public const string GrantUssClassName = "post-wave-view__grant";

        readonly ScreenScaffold _scaffold;
        readonly Label _kicker;
        readonly Label _headline;
        readonly VisualElement _granted;
        readonly Button _next;
        readonly StatCell _reward;
        readonly StatCell _integrity;
        readonly StatCell _kept;

        Action _onNext;

        public PostWaveView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("PostWaveView");
            tree.CloneTree(this);

            _kicker = this.Q<Label>("kicker");
            _headline = this.Q<Label>("headline");
            _granted = this.Q<VisualElement>("granted");
            _next = this.Q<Button>("next");

            // BUILT ONCE, RE-VALUED PER BIND, which is the arrangement
            // `StatCell.Value` exists for ("the handoff's region card swaps
            // all three cells whenever a region is picked"). It is also what
            // keeps `reward` a stable handle: the cell is named, and
            // `WaveScreensTests` reads the reward line back through it the
            // way it used to read a Label named `reward`.
            //
            // THE VALUE LABEL'S `t-num` IS THE POINT OF USING THE COMPONENT
            // at all rather than marking a Label by hand - StatCell's own
            // class comment: "nothing makes a screen remember to put the
            // class on. Building the cell once is what makes it impossible to
            // forget." The reward and the integrity count are both numbers a
            // next decision depends on, which is bible 10.6's own test.
            _reward = new StatCell(PostWaveScreen.RewardStatLabel, null) { name = "reward" };
            _integrity = new StatCell(PostWaveScreen.IntegrityStatLabel, null) { name = "integrity" };

            // A THIRD CELL, BECAUSE THIS ROW AND THE DEFEAT SCREEN'S ARE THE
            // SAME ROW ON OPPOSITE ARMS OF ONE BRANCH. `Wave Defeat
            // .dc.html:43` draws three; a player who loses a wave and a player
            // who holds one should read the same shape. Its value is the size
            // of the deployment that came home - `WaveDefeatScreen
            // .KeptStatValue`'s comment has why that is not a survival count.
            _kept = new StatCell(PostWaveScreen.KeptStatLabel, null) { name = "kept" };

            var stats = this.Q<VisualElement>("stats");
            stats.Add(_reward);
            stats.Add(_integrity);
            stats.Add(_kept);

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: true, by the same inheritance `DeployView` took. The
            // handoff draws no post-wave screen - it ends at Wave Defense and
            // Wave Defeat - but its push table reaches Wave Defense from the
            // Gene Ark and Wave Defeat from Wave Defense "(on loss)", so the
            // screen on the other arm of that branch is pushed too. The
            // CHEVRON follows `Bind`'s `onBack` and not this flag; see below.
            //
            // `title: null` IS THE FOURTH HEADERLESS SCREEN AND IT IS PHASE 9
            // TASK 21e. The header's only content was the title "Post-Wave",
            // which is design section 5.1's section NAME and which the exit
            // gate's walk read as a screen name printed at a player. The note
            // where `PostWaveScreen.Title` used to be has the full ruling; the
            // short form is that `Headline` states the verdict directly
            // underneath, in the player's own words and at 28px, so a title
            // above it can only repeat it or say nothing - and `WaveDefeatView`,
            // the other arm of this same branch, has been headerless since
            // Task 18.
            //
            // `pushed: true` STAYS AND COSTS NOTHING. `ScreenScaffold.OnBack`
            // removes the chevron when the callback is null, which it is at
            // every call site (see `Bind`), and `WaveDefeatView` passes exactly
            // this pair for exactly this reason.
            _scaffold = new ScreenScaffold(title: null, pushed: true);

            // The UXML authors this screen's furniture as children of this
            // element and the scaffold's slots take them over here - a
            // re-parent rather than a second tree, so each element is
            // authored in exactly one place.
            var summary = new SectionCard { name = "summary" };
            summary.Body.Add(stats);

            // THE KICKER IS THE FIRST THING IN THE CONTENT COLUMN, because
            // `#header` is also where the scaffold's eyebrow lives and a
            // headerless screen that left it there would lose it silently.
            // `SpliceRevealView` made this move in Task 16b and its
            // constructor has the same note.
            _scaffold.Content.Add(_kicker);
            _scaffold.Content.Add(_headline);
            _scaffold.Content.Add(summary);
            _scaffold.Content.Add(_granted);
            _scaffold.CtaRow.Add(_next);
            Add(_scaffold);

            // Registered once, in the constructor, against a field the next
            // Bind can overwrite - so re-binding this same instance (a
            // recycled screen) cannot stack a second `onNext` behind the
            // first the way `clicked += next` inside Bind would.
            _next.clicked += () => _onNext?.Invoke();
        }

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron - `RosterView.Bind` has the full reasoning, which holds
        /// here: `Broodline.UI` cannot name `ScreenHost.Pop`, and
        /// `FtueDirector` reaches this screen through `ScreenFlow.ShowAsync`
        /// (`ScreenHost.Show`, back stack cleared) rather than a push, so
        /// nothing draws one today. On this screen that is also right on its
        /// own: back from a wave's summary is back to a wave that is over.
        /// `wave` and `deployed` ARE THE TWO FACTS THIS SCREEN STATES AND THE
        /// SERVER'S RESPONSE DOES NOT CARRY. `WaveSubmitResponse` is Result,
        /// IntegrityRemaining, Breaches, Reward and Granted
        /// (`Generated/Api/BroodlineApiClient.cs:2265-2281`) and carries no
        /// wave id at all, so the kicker's "WAVE 6 OF 12" has to come from the
        /// caller that started the wave. Both nullable for
        /// `DeployScreen.FoesStatValue`'s reason, and
        /// `WaveDefeatView.Bind` takes the same pair for the same reason.
        public void Bind(WaveSubmitResponse response, IReadOnlyList<CreatureDto> granted, Action next,
                         Action onBack = null, int? wave = null, int? deployed = null)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            _scaffold.OnBack = onBack;

            // THE KICKER IS THIS SCREEN'S OWN ELEMENT AND NO LONGER THE
            // SCAFFOLD'S EYEBROW - Phase 9 Task 21e took the page header with
            // the title, and the eyebrow slot is inside it. `Collapsing` is
            // the same null contract `ScreenScaffold.Eyebrow` kept: a caller
            // that does not know the wave says nothing rather than printing
            // "WAVE  OF 12" with a hole in it, and an empty Label still
            // carries --text-micro tracking and a margin, so it has to be
            // removed from the layout rather than blanked. `WaveDefeatView`
            // collapses its own relocated kicker the same way.
            Collapsing(_kicker, wave == null ? null : PostWaveScreen.Eyebrow(wave.Value));

            // The verdict is the SERVER's, and it is the one line on this
            // screen that changes between a win and anything else - which is
            // why there is no page header over it at all. The note where
            // `PostWaveScreen.Title` used to be has that argument in full.
            Collapsing(_headline, PostWaveScreen.Headline(response.Result));

            // AND ITS COLOUR, because this screen renders both verdicts. The
            // class used to be baked into the UXML as `t-success`, which meant
            // a non-win response - a documented path, see WaveScreens'
            // `Headline` - printed "Wave not cleared." in the green reserved
            // for a win. `WaveDefeatView` can hardcode `t-danger` because it
            // is a defeat-only screen; this one cannot hardcode either.
            var won = response.Result == PostWaveScreen.WinResult;
            _headline.EnableInClassList("t-success", won);
            _headline.EnableInClassList("t-danger", !won);

            _reward.Value = PostWaveScreen.RewardLine(response.Reward);
            _integrity.Value = PostWaveScreen.IntegrityStatValue(response.IntegrityRemaining);
            _kept.Value = PostWaveScreen.KeptStatValue(deployed);

            // ONE CARD PER CREATURE, named after its id - the same shape
            // `RosterView` uses, and the reason it is named rather than just
            // counted: a screen that shows the right NUMBER of arrivals and
            // the wrong creatures is beat 3 failing silently.
            _granted.Clear();
            if (granted != null)
            {
                foreach (var creature in granted)
                {
                    var card = new CreatureCard { name = creature.CreatureId.ToString() };
                    card.Bind(creature, null);
                    card.AddToClassList(GrantUssClassName);
                    _granted.Add(card);
                }
            }

            // NO EmptyState HERE, THOUGH TASK 11 STEP 3 ASKS FOR ONE ON EVERY
            // LIST, AND THE REASON IS THAT THIS IS NOT A LIST. It is a drop.
            // Design section 5.1 makes the creature drop BEAT 3 - one moment,
            // not a standing inventory - so most cleared waves grant nothing
            // and that is the ordinary case rather than a state owing the
            // player an explanation. `WaveDefeatScreen.Resupply` already
            // settled the same question in prose for the same shape on the
            // other arm of the branch: "an empty grant says nothing rather
            // than promising a resupply that is not there."
            //
            // WHAT THE ROW DID INSTEAD WAS DRAW ITS OWN ABSENCE. It carried
            // --green-tint and --space-3 of padding at width 100%, so a wave
            // that granted nothing drew a blank green block between the
            // reward and the CTA, and a wave that granted one creature drew
            // a single 160px card in a full-width green field. Both are in
            // the capture this task replaces. The tint is gone from the
            // stylesheet rather than toggled here, and the row collapses when
            // it is empty - the rule `ScreenScaffold.FooterNote` established
            // and `DeployView.Blocker` and `LineageView` follow.
            _granted.style.display = _granted.childCount == 0
                ? DisplayStyle.None : DisplayStyle.Flex;

            _next.text = PostWaveScreen.NextLabel;
            _onNext = next;
        }

        /// A Label that takes up no room when it has nothing to say.
        ///
        /// The headline is 28px, so an empty one is a blank line the height of
        /// the largest type in the app, plus its own bottom margin pushing
        /// everything under it down. Same defect as a blank tinted strip, one
        /// step quieter because there is no fill to see. The TEXT still
        /// round-trips to string.Empty, which is what `WaveScreensTests`
        /// reads.
        static void Collapsing(Label label, string text)
        {
            label.text = text ?? string.Empty;
            label.style.display = string.IsNullOrEmpty(text)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
