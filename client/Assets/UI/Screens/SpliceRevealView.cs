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
    /// The `silhouette` slot inside the card is still empty paint. Task 13's
    /// species proxy is what fills it.
    [UxmlElement]
    public partial class SpliceRevealView : VisualElement
    {
        public const string UssClassName = "splice-reveal-view";
        public const string MutatedUssClassName = "mutated";
        public const string ParentUssClassName = "splice-reveal-view__parent";

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
            _parents.Clear();
            AddParent(parentA);
            AddParent(parentB);
            if (_parents.childCount == 0)
            {
                _parents.Add(new EmptyState(SpliceRevealScreen.NoParentsMessage, "warning"));
            }

            _consumption.text = SpliceRevealScreen.Consumption(parentA, parentB);
            _next.text = SpliceRevealScreen.NextLabel;
            _onNext = next;
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
