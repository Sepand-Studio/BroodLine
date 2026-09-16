using System;
using System.Collections.Generic;
using Broodline.Api;
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
    /// dialog just promised.
    ///
    /// The server decides who is in the tree, who is consumed and what
    /// mutated. `LineageNode.pruned` exists on the wire and is NOT read here:
    /// `pruneLineage` runs server-side on the write that changes the depth,
    /// so a pruned ancestor is already absent from the response and a client
    /// filtering on the flag would be a second copy of a rule it cannot see
    /// the inputs to.
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

        readonly Label _title;
        readonly Label _notice;
        readonly VisualElement _generations;

        public LineageView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("LineageView");
            tree.CloneTree(this);

            _title = this.Q<Label>("title");
            _notice = this.Q<Label>("notice");
            _generations = this.Q<VisualElement>("generations");
        }

        /// `highlight` is the creature the session is ABOUT - the named
        /// Founder at beat 8. It is a plain `Guid` rather than a nullable
        /// one: `Guid.Empty` matches no node the server can send, so "nothing
        /// highlighted" needs no second parameter to express.
        public void Bind(LineageResponse lineage, Guid highlight)
        {
            if (lineage == null) throw new ArgumentNullException(nameof(lineage));

            _title.text = LineageScreen.Title;

            var nodes = lineage.Nodes;
            var generations = LineageScreen.Generations(nodes);

            _notice.text = generations.Count == 0 ? LineageScreen.EmptyNotice : string.Empty;

            _generations.Clear();
            for (var i = 0; i < generations.Count; i++)
            {
                var generation = generations[i];
                var row = new VisualElement
                {
                    name = "generation-" + CreatureLabel.Generation(generation),
                };
                row.AddToClassList(GenerationRowUssClassName);

                foreach (var node in nodes)
                {
                    if (node == null || node.Generation != generation) continue;
                    row.Add(NodeFor(node, highlight));
                }

                _generations.Add(row);
            }
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
            var pip = new Components.TraitPip { name = trait };
            // No `counters` reaches this Bind: the lineage response carries
            // no trait table, and `Broodline.UI` cannot derive one. The Codex
            // sheet is where a player reads what a trait answers.
            pip.Bind(trait, tier, null);
            into.Add(pip);
        }
    }
}
