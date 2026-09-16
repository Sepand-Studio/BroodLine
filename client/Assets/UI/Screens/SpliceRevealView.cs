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
    [UxmlElement]
    public partial class SpliceRevealView : VisualElement
    {
        public const string UssClassName = "splice-reveal-view";
        public const string MutatedUssClassName = "mutated";
        public const string ParentUssClassName = "splice-reveal-view__parent";

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

            _next.clicked += () => _onNext?.Invoke();
        }

        public void Bind(SpliceCommitResponse committed, CreatureDto parentA, CreatureDto parentB,
            bool mutated, Action next)
        {
            if (committed == null) throw new ArgumentNullException(nameof(committed));

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
            _parents.Clear();
            AddParent(parentA);
            AddParent(parentB);

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
