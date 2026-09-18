using System;
using System.Collections.Generic;
using System.Globalization;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Beat 8 - the screen the whole first session exists to reach. bible
    /// 9.2: "A player closing the app after five minutes should be carrying
    /// one image: a family tree with a creature they named at the bottom of
    /// it."
    ///
    /// NOBODY IS MISSING FROM IT. `splice_confirm_spec` section 5 makes the
    /// consumed parents' presence the lesson - "individuals are consumed, the
    /// record survives" - so this view renders every node the server sent and
    /// filters none. A consumed parent is marked, not dropped; a tree that
    /// quietly omitted them would teach the opposite of what the confirm
    /// dialog just promised. The generation card's two stat cells state that
    /// same contrast as a number: how many the record holds, how many live.
    ///
    /// The server decides who is in the tree, who is consumed and what
    /// mutated. `LineageNode.pruned` exists on the wire and is NOT read here:
    /// `pruneLineage` runs server-side on the write that changes the depth,
    /// so a pruned ancestor is already absent from the response and a client
    /// filtering on the flag would be a second copy of a rule it cannot see
    /// the inputs to.
    ///
    /// `onBack` IS A CALLER'S ACTION AND CANNOT BE `ScreenHost.Pop` FROM
    /// HERE. The phase 8 plan writes the chevron as "onBack -> ScreenHost.Pop";
    /// `ScreenHost` is `Broodline.Game.Shell` and `Broodline.UI.asmdef`
    /// references `Broodline.Model`, `Broodline.Net` and `Generated.Api` and
    /// not `Broodline.Game`, so this assembly cannot name that type - the
    /// same one-way dependency `ScreenScaffold`'s "it does NOT own
    /// navigation" is written to. The view takes the action; wiring it to
    /// `Pop` is the director's. Optional rather than required because every
    /// caller in the tree today presents this screen with
    /// `ScreenFlow.ShowAsync`, which is `ScreenHost.Show` and clears the back
    /// stack - and `Pop` at depth zero is a documented no-op, so an unwired
    /// chevron and a wired one do the same nothing until a caller pushes.
    [UxmlElement]
    public partial class LineageView : VisualElement
    {
        public const string UssClassName = "lineage-view";
        public const string GenerationRowUssClassName = "generation-row";
        public const string NodeUssClassName = "node";
        public const string FounderUssClassName = "founder";
        public const string ConsumedUssClassName = "consumed";
        public const string MutatedUssClassName = "mutated";
        public const string HighlightUssClassName = "highlight";

        readonly VisualElement _generations;
        readonly Button _next;

        Action _onNext;
        Action _onBack;

        public LineageView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("LineageView");
            tree.CloneTree(this);

            _generations = this.Q<VisualElement>("generations");
            _next = this.Q<Button>("next");

            // Composed, not inherited - ScreenScaffold's class comment has
            // the reason and ScaffoldTests' sweep is what depends on it.
            // pushed: true per the handoff's push table; the action behind
            // the chevron is the caller's, per this class's comment.
            var scaffold = new ScreenScaffold(
                LineageScreen.Title, pushed: true, onBack: () => _onBack?.Invoke());
            scaffold.Content.Add(_generations);
            scaffold.CtaRow.Add(_next);
            Add(scaffold);

            _next.clicked += () => _onNext?.Invoke();
        }

        /// `highlight` is the creature the session is ABOUT - the named
        /// Founder at beat 8. It is a plain `Guid` rather than a nullable
        /// one: `Guid.Empty` matches no node the server can send, so "nothing
        /// highlighted" needs no second parameter to express.
        /// `next` IS REQUIRED, and that is the point of the signature.
        ///
        /// Beat 8 ends session one but must not be a dead end: `Ftue.Derive`
        /// answers `Lineage` on every launch between wave 2 and wave 6, so a
        /// tree with no way forward parks a returning player on the screen
        /// that closed their first session and never lets them reach wave 6 -
        /// the designed first loss design section 5 puts in this same phase.
        ///
        /// It was `Action next = null` for one commit, and an optional
        /// default is a weak guard for exactly that: a caller who forgets the
        /// argument compiles cleanly, gets a hidden button, and hangs the
        /// walk on a turn nothing can resume. A REQUIRED parameter makes the
        /// compiler the guard, and `BindStandalone` makes the no-continuation
        /// case a named, greppable decision instead of an omitted argument.
        ///
        /// (The alternative considered - having `ScreenFlow` assert in a
        /// debug build that a presented turn's resume was wired - cannot
        /// honestly be written: the flow hands the resume to `bind` and has
        /// no way to see whether the view kept it. All it could check is
        /// that `bind` ran, which it always does. A guard that cannot fire on
        /// the failure it names is worse than none.)
        ///
        /// `onBack` IS optional for the opposite reason and the difference is
        /// worth stating: an unwired `next` strands the walk, and an unwired
        /// chevron does what `ScreenHost.Pop` would have done at depth zero.
        public void Bind(LineageResponse lineage, Guid highlight, Action next, Action onBack = null)
        {
            if (next == null) throw new ArgumentNullException(nameof(next),
                "A directed lineage turn needs somewhere to go. Use BindStandalone for a tab destination.");
            Render(lineage, highlight, next, onBack);
        }

        /// The tree as a plain destination, with nothing waiting on it - a
        /// tab or a deep link rather than a beat. The continue button is
        /// hidden rather than dead, and choosing that is what calling this
        /// method means.
        public void BindStandalone(LineageResponse lineage, Guid highlight, Action onBack = null)
        {
            Render(lineage, highlight, next: null, onBack: onBack);
        }

        void Render(LineageResponse lineage, Guid highlight, Action next, Action onBack)
        {
            if (lineage == null) throw new ArgumentNullException(nameof(lineage));

            _next.text = LineageScreen.NextLabel;
            _next.style.display = next == null ? DisplayStyle.None : DisplayStyle.Flex;
            _onNext = next;
            _onBack = onBack;

            var nodes = lineage.Nodes;
            var generations = LineageScreen.Generations(nodes);

            _generations.Clear();

            // The empty tree, as a state the game meant rather than as a
            // screen that failed to load. It REPLACED a `notice` Label that
            // carried an --amber-tint fill and --space-2 of padding
            // unconditionally, so every populated capture of this screen
            // before Phase 8 Task 9 shows an empty amber strip under the
            // title - the banner rendering its own absence of text.
            if (generations.Count == 0)
            {
                _generations.Add(new EmptyState(LineageScreen.EmptyNotice, "splice"));
                return;
            }

            for (var i = 0; i < generations.Count; i++)
            {
                _generations.Add(CardFor(generations[i], nodes, highlight));
            }
        }

        /// One generation, as a card: the heading, the two counts, then the
        /// creatures.
        ///
        /// THE CARD CARRIES `generation-row` AND IS STILL NAMED
        /// `generation-&lt;badge&gt;`. Both are handles the tests and a deep link
        /// reach a generation by, and neither belongs to `SectionCard` - the
        /// component supplies the surface and the elevation and knows nothing
        /// about lineage.
        static VisualElement CardFor(int generation, ICollection<LineageNode> nodes, Guid highlight)
        {
            var card = new SectionCard(LineageScreen.GenerationHeading(generation))
            {
                name = "generation-" + CreatureLabel.Generation(generation),
            };
            card.AddToClassList(GenerationRowUssClassName);

            var creatures = new VisualElement { name = "creatures" };
            creatures.AddToClassList("generation-row__creatures");

            // Counted in the loop that renders, rather than in two more
            // passes over the same list: the filter is already here.
            var recorded = 0;
            var living = 0;
            foreach (var node in nodes)
            {
                if (node == null || node.Generation != generation) continue;
                recorded++;
                if (string.IsNullOrEmpty(node.ConsumedAt)) living++;
                creatures.Add(NodeFor(node, highlight));
            }

            // A ROW OF CELLS IS THE CONTAINER'S JOB. StatCell.uss's closing
            // note has the measurement: a component's stylesheet reaches the
            // component and its descendants and never its parent, so
            // `flex-direction: row` for this strip lives in LineageView.uss.
            var stats = new VisualElement { name = "stats" };
            stats.AddToClassList("generation-row__stats");
            stats.Add(new StatCell(LineageScreen.RecordedStatLabel,
                recorded.ToString(CultureInfo.InvariantCulture)));
            stats.Add(new StatCell(LineageScreen.LivingStatLabel,
                living.ToString(CultureInfo.InvariantCulture)));

            card.Body.Add(stats);
            card.Body.Add(creatures);
            return card;
        }

        static VisualElement NodeFor(LineageNode node, Guid highlight)
        {
            // Named after the creature id - the same convention
            // `RosterView`, `PostWaveView` and `SpliceChamberView` use, and
            // for the same reason: a tree with the right NUMBER of nodes and
            // the wrong creatures in it is beat 8 failing silently.
            var element = new VisualElement { name = node.CreatureId.ToString() };
            element.AddToClassList(NodeUssClassName);

            element.EnableInClassList(FounderUssClassName, node.IsFounder);
            // `consumedAt` is a TIMESTAMP, nullable on the wire - its
            // presence is the fact, and the client never parses it. Design
            // 3.2: a consumed parent is "dead but WHOLE", which is why it is
            // still here to be marked.
            element.EnableInClassList(ConsumedUssClassName, !string.IsNullOrEmpty(node.ConsumedAt));
            element.EnableInClassList(MutatedUssClassName, node.Mutated);
            element.EnableInClassList(HighlightUssClassName,
                highlight != Guid.Empty && node.CreatureId == highlight);

            element.Add(new Label { name = "label", text = LineageScreen.NodeLabel(node) });
            element.Add(new Label
            {
                name = "state",
                text = string.IsNullOrEmpty(node.ConsumedAt) ? string.Empty : LineageScreen.ConsumedLabel,
            });

            var traits = new VisualElement { name = "traits" };
            AddTrait(traits, node.Trait1, node.Tier1);
            AddTrait(traits, node.Trait2, node.Tier2);
            element.Add(traits);

            return element;
        }

        /// A lineage node's traits are nullable on the wire (a pruned or very
        /// old record can carry none), so an absent trait adds no pip rather
        /// than an empty one.
        static void AddTrait(VisualElement into, string trait, int? tier)
        {
            if (string.IsNullOrEmpty(trait)) return;
            var pip = new TraitPip { name = trait };
            // No `counters` reaches this Bind: the lineage response carries
            // no trait table, and `Broodline.UI` cannot derive one. The Codex
            // sheet is where a player reads what a trait answers.
            pip.Bind(trait, tier, null);
            into.Add(pip);
        }
    }
}
