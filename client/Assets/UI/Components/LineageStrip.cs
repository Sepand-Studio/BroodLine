using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The pedigree, in one line: a node per generation on a rail, the
    /// current one marked, and a sentence under it.
    ///
    /// LINEAGE IS THE GAME'S DEPTH AXIS AND THIS IS THE ONLY PLACE IT IS
    /// VISIBLE AT A GLANCE. The handoff's overview calls it the depth engine
    /// - "lineage compounds across generations (G1 -> G7+)" - and its splice
    /// chamber spends a whole card on the strip plus the line that reads
    /// "Unbroken Vetch line since G1 - pedigree bonus +12% trait fidelity".
    /// The strip is what makes a G7 feel like a G7 rather than like a number
    /// on a chip.
    ///
    /// IT TAKES THE CHAIN, IT DOES NOT WALK ONE. A lineage is a tree and the
    /// server sends it (`LineageResponse`); picking the path through it that
    /// a player thinks of as "the line" is a decision, and it belongs to
    /// whoever has the tree. This draws the sequence it is handed.
    ///
    /// GENERATIONS ARE ON `t-num`, on the rule every other number in this
    /// vocabulary follows. The handoff sets these labels at 8.5px, which is
    /// below bible 10.6's 11px floor for any number a decision depends on -
    /// and a generation is the one number this whole strip exists to state.
    /// The floor wins; the 2.5px is recorded here rather than argued with
    /// silently, the way StatCell.uss records its own 1px.
    ///
    /// RE-BINDABLE, AND IT REBUILDS RATHER THAN RECYCLES. A chain changes
    /// length - the chamber's strip grows by one when a splice lands - so
    /// there is no fixed set of children to re-point, and a recycled row
    /// that kept a stale `--current` would put the mark on the wrong
    /// generation. Clearing is cheap here: a strip is five or six nodes.
    public sealed class LineageStrip : VisualElement
    {
        public const string UssClassName = "lineage-strip";
        public const string NodeUssClassName = "lineage-strip__node";
        public const string CurrentUssClassName = "lineage-strip__node--current";
        public const string GenUssClassName = "lineage-strip__gen";

        readonly VisualElement _track;
        readonly Label _note;

        public LineageStrip()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("LineageStrip").CloneTree(this);

            _track = this.Q<VisualElement>("track");
            _note = this.Q<Label>("note");
            Bind(null, null);
        }

        /// `nodes` is the chain oldest-first. `species` is one of bible 1.2's
        /// six and tints the node; anything else leaves it in the default
        /// --divider, on `CreatureSprites`' rule - null rather than a guess.
        /// `current` marks the generation the screen is about, which is
        /// usually the last but is not always: the lineage screen marks the
        /// creature a player tapped.
        ///
        /// A null or empty chain COLLAPSES THE WHOLE STRIP, because a rail
        /// with no nodes on it is a horizontal line across a card, which
        /// reads as a divider someone forgot to remove.
        public void Bind(IReadOnlyList<(int gen, string species, bool current)> nodes, string note)
        {
            _track.Clear();

            var count = nodes == null ? 0 : nodes.Count;
            style.display = count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            for (var i = 0; i < count; i++)
            {
                // THE RAIL GOES BETWEEN, SO THERE IS ONE FEWER OF IT THAN OF
                // NODES. Built as a sibling rather than as a border or a
                // background on the stop, because it has to STRETCH: the
                // handoff spaces its generations evenly across the card
                // whatever the chain's length, which is a flex-grow on the
                // thing between them and cannot be a fixed margin.
                if (i > 0) _track.Add(Rail());

                var node = nodes[i];
                _track.Add(Stop(node.gen, node.species, node.current));
            }

            _note.text = note ?? string.Empty;
            _note.style.display = string.IsNullOrEmpty(note) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        static VisualElement Rail()
        {
            var rail = new VisualElement();
            rail.AddToClassList("lineage-strip__rail");
            return rail;
        }

        VisualElement Stop(int generation, string species, bool current)
        {
            var stop = new VisualElement();
            stop.AddToClassList("lineage-strip__stop");

            var node = new VisualElement();
            node.AddToClassList(NodeUssClassName);
            if (current) node.AddToClassList(CurrentUssClassName);

            var key = (species ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length > 0) node.AddToClassList(NodeUssClassName + "--" + key);
            stop.Add(node);

            // The same `CreatureLabel.Generation` every other generation in
            // the app is written by, so "G4" cannot become "Gen 4" on one
            // screen.
            var label = new Label(CreatureLabel.Generation(generation));
            label.AddToClassList(GenUssClassName);
            label.AddToClassList("t-num");
            if (current) label.AddToClassList(GenUssClassName + "--current");
            stop.Add(label);

            return stop;
        }
    }
}
