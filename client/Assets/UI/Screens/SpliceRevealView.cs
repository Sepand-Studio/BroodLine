using System;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Beat 7 - `broodline_splice_confirm_spec` section 5: the reveal "keeps
    /// its existing line - that parents are consumed and their traits live on
    /// in the pedigree - but it is now a confirmation of something already
    /// understood rather than a disclosure."
    ///
    /// WHY `mutated` IS A PARAMETER AND NOT READ OFF `committed`. The brief
    /// for this task binds a commit response carrying a mutation flag.
    /// **`SpliceCommitResponse` does not carry one, deliberately** -
    /// `services/api/src/routes/splice.ts` rolls `mutated`/`aberrant`,
    /// stores them, and withholds them from the response, because design
    /// section 10 defers Aberrant content: "reporting `mutated: true` for an
    /// event with no effect ... would show the player a mutation that did
    /// not happen."
    ///
    /// `GET /v1/lineage` DOES report it (`LineageNode.mutated`), and the
    /// director calls that route anyway for beat 8, so the flag reaching
    /// this screen still arrives through a server response and is never
    /// inferred on the client. Taking it as a parameter is what keeps that
    /// true while the commit route stays silent - and the day the flag lands
    /// on the commit response, only the caller changes.
    ///
    /// THE HERO CARD SITS IN AN `.elev-2` WRAPPER WEARING `reveal-flare`, as
    /// of Phase 8 Task 10. Both are existing vocabulary: `.elev-2` is
    /// Theme.uss's raised elevation, the only place in the app that earns the
    /// heavier of the handoff's two drops; `reveal-flare` is Motion.uss's
    /// opacity/scale transition on `--motion-screen`, written in Task 5
    /// against this exact screen and listed there as one of four classes
    /// that "match nothing today". This is the consumer it was waiting for.
    /// The MOTION is not visible in the capture corpus - a screenshot is one
    /// frame, and one frame of a 220ms transition is identical to no
    /// transition - so what this commit can claim is that the class is
    /// applied and the elevation renders, not that the flare reads well
    /// under a thumb. That is Task 16's pass.
    ///
    /// The `silhouette` slot inside the card is filled as of Task 13 (the
    /// interim species proxy) and re-filled as of Phase 9 Task 9 with the
    /// baked body and part sprites. Nothing on this screen does the filling:
    /// `CreatureCard.Bind` calls `CreatureSprites` and every consumer of the
    /// card got it at once, which is what "the atom of the interface" is
    /// supposed to buy.
    [UxmlElement]
    public partial class SpliceRevealView : VisualElement
    {
        public const string UssClassName = "splice-reveal-view";
        public const string MutatedUssClassName = "mutated";
        public const string ParentUssClassName = "splice-reveal-view__parent";

        /// Motion.uss's end state for `reveal-flare`. Named here because Bind
        /// is what toggles it, and an unreferenced transition class was how
        /// this animation came to exist without ever running.
        public const string FlareHiddenUssClassName = "reveal-flare--hidden";

        readonly ScreenScaffold _scaffold;
        readonly Label _headline;
        readonly VisualElement _child;
        readonly Label _childLine;
        readonly VisualElement _parents;
        readonly Label _consumption;
        readonly Button _next;

        Action _onNext;

        public SpliceRevealView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("SpliceRevealView");
            tree.CloneTree(this);

            _headline = this.Q<Label>("headline");
            _child = this.Q<VisualElement>("child");
            _childLine = this.Q<Label>("child-line");
            _parents = this.Q<VisualElement>("parents");
            _consumption = this.Q<Label>("consumption");
            _next = this.Q<Button>("next");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: true - the handoff's push table reaches Splice Reveal
            // from the Splice Chamber, "(after splice)". The CHEVRON follows
            // `Bind`'s `onBack`; see below.
            //
            // THE HEADER AND THE HEADLINE ARE TWO DIFFERENT SENTENCES, and
            // SpliceRevealScreen.Title says why: "Splice Reveal" names the
            // screen and never changes, "A mutation fired." is one of two
            // things that just happened.
            _scaffold = new ScreenScaffold(SpliceRevealScreen.Title, pushed: true);

            _scaffold.Content.Add(_headline);
            _scaffold.Content.Add(_child);
            _scaffold.Content.Add(_childLine);
            _scaffold.Content.Add(_parents);
            // LAST in the content region, directly above the CTA - the same
            // placement splice_confirm_spec 3 gives the destruction notice on
            // the chamber, and for the same reason: it is the sentence the
            // player is meant to have read when they tap.
            _scaffold.Content.Add(_consumption);
            _scaffold.CtaRow.Add(_next);
            Add(_scaffold);

            _next.clicked += () => _onNext?.Invoke();
        }

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron - `RosterView.Bind` has the full reasoning. Here it is
        /// also the weakest case for one: `FtueDirector` reaches this screen
        /// through `ScreenFlow.ShowAsync`, and going BACK from a reveal would
        /// be going back to a chamber whose two parents no longer exist.
        public void Bind(SpliceCommitResponse committed, CreatureDto parentA, CreatureDto parentB,
            bool mutated, Action next, Action onBack = null)
        {
            if (committed == null) throw new ArgumentNullException(nameof(committed));

            _scaffold.OnBack = onBack;

            _headline.text = SpliceRevealScreen.Headline(mutated);
            EnableInClassList(MutatedUssClassName, mutated);

            _child.Clear();
            if (committed.Child != null)
            {
                var card = new CreatureCard { name = committed.Child.CreatureId.ToString() };
                card.Bind(committed.Child, null);
                _child.Add(card);
            }
            _childLine.text = SpliceRevealScreen.ChildLine(committed.Child);

            // BOTH PARENTS ARE STILL DRAWN, on the screen that says they are
            // gone. splice_confirm_spec section 5 makes the reveal a
            // confirmation of a fact the player already read, and a screen
            // that quietly removed them would be teaching the opposite of
            // what the tree shows one tap later.
            //
            // AND A ROW THAT WAS HANDED NEITHER SAYS SO. `AddParent` skips a
            // null, so two nulls used to render as nothing at all - the same
            // screen, minus the lesson, with no sentence saying whether the
            // parents were consumed or simply never arrived.
            // EITHER PARENT MISSING IS THE SAME CASE AS BOTH, and the test
            // used to be `childCount == 0`, which only caught both. A splice
            // consumes two creatures, so one card on its own does not render
            // half the truth - it renders a different and wrong one, a child
            // with a single parent. Worse, `Consumption` returns empty as
            // soon as EITHER is null, so the one-parent case fell through
            // this guard AND that sentence: a lone card, no explanation,
            // on the screen bible 2.1 makes the first hour's teaching moment.
            _parents.Clear();
            if (parentA == null || parentB == null)
            {
                _parents.Add(new EmptyState(SpliceRevealScreen.NoParentsMessage, "warning"));
            }
            else
            {
                AddParent(parentA);
                AddParent(parentB);
            }

            // AND THE SENTENCE COLLAPSES WHEN THERE IS NONE, rather than
            // leaving a blank Label in the tree above the CTA.
            var consumption = SpliceRevealScreen.Consumption(parentA, parentB);
            _consumption.text = consumption;
            _consumption.style.display = string.IsNullOrEmpty(consumption)
                ? DisplayStyle.None : DisplayStyle.Flex;
            _next.text = SpliceRevealScreen.NextLabel;
            _onNext = next;

            // AND THE FLARE ACTUALLY RUNS. `reveal-flare` was applied to the
            // hero card and pinned by a test, but nothing ever added or
            // removed its `--hidden` pair - so there was no state change for
            // the transition to run against and the payoff of the whole first
            // hour snapped in with no motion at all. A USS transition needs
            // two computed styles: set the end state the element starts from,
            // then clear it once layout has resolved, which is the next frame.
            _child.AddToClassList(FlareHiddenUssClassName);
            _child.schedule.Execute(() => _child.RemoveFromClassList(FlareHiddenUssClassName));
        }

        void AddParent(CreatureDto parent)
        {
            if (parent == null) return;
            var card = new CreatureCard { name = parent.CreatureId.ToString() };
            card.Bind(parent, null);
            card.AddToClassList(ParentUssClassName);
            _parents.Add(card);
        }
    }
}
