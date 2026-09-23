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
    /// stack - so today nobody passes one, AND NO CHEVRON IS DRAWN. That is
    /// the honest rendering rather than a gap: `Pop` at depth zero is a
    /// documented no-op, so a chevron wired to it there would be a control a
    /// player taps to no effect, which reads as a broken app rather than as a
    /// screen with no parent. The affordance appears the moment a caller
    /// pushes this screen and hands over somewhere to go.
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

        /// The words that keep Founder, Mutated and Consumed off colour
        /// alone - bible 10.4. `MarksFor` has the whole account.
        public const string MarksUssClassName = "node__marks";
        public const string MarkUssClassName = "node__mark";

        /// The column of trait chips inside a node. Named here rather than
        /// typed as a literal in the sheet so the coupling between the C#
        /// that builds the column and the USS that spaces it is visible from
        /// both ends - `SectionCard.ElevationUssClassName`'s convention, and
        /// the same one `MarksUssClassName` above already follows.
        ///
        /// THE COLUMN IS THE CONTAINER'S JOB AND IT CANNOT BE THE CHIP'S.
        /// `StatCell.uss`'s closing note is the measurement: a component's
        /// stylesheet reaches the component and its descendants and never its
        /// parent, so nothing `TraitChip` carries can space the stack holding
        /// it. `SpliceChamberView.uss:141` owns the identical rule for the
        /// identical reason.
        public const string TraitsUssClassName = "node__traits";

        readonly ScreenScaffold _scaffold;
        readonly VisualElement _generations;
        readonly Button _next;

        Action _onNext;

        public LineageView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("LineageView");
            tree.CloneTree(this);

            _generations = this.Q<VisualElement>("generations");
            _next = this.Q<Button>("next");

            // Composed, not inherited - ScreenScaffold's class comment has
            // the reason and ScaffoldTests' sweep is what depends on it.
            //
            // pushed: true per the handoff's push table, and NO ACTION YET -
            // so no chevron is drawn until Render is handed one. A closure
            // over a field here would defeat that: it is never null, so the
            // scaffold would draw a chevron on a screen with nowhere to go
            // and a player would tap it to no effect at all. ScreenScaffold's
            // `OnBack` carries the whole argument.
            // THE KICKER IS PASSED AT CONSTRUCTION, NOT AT BIND, because it
            // never changes: it says where in the app this screen is, and
            // this screen is always the pedigree. `RosterView` sets its own
            // in `Bind` because the roster's is a live count; `SpliceChamber
            // View` passes a constant here, as this does.
            _scaffold = new ScreenScaffold(
                LineageScreen.Title, pushed: true, eyebrow: LineageScreen.Eyebrow);
            _scaffold.Content.Add(_generations);
            _scaffold.CtaRow.Add(_next);
            Add(_scaffold);

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
        /// worth stating: a missing `next` strands the walk, and a missing
        /// `onBack` draws no chevron at all - so it cannot strand anything.
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
            _scaffold.OnBack = onBack;

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

            // THE SECOND CHANNEL, AND IT IS NOT DECORATION. See MarksFor.
            var marks = MarksFor(node);
            if (marks != null) element.Add(marks);

            var traits = new VisualElement { name = "traits" };
            traits.AddToClassList(TraitsUssClassName);
            AddTrait(traits, node.Trait1, node.Tier1, node.Species);
            AddTrait(traits, node.Trait2, node.Tier2, node.Species);
            element.Add(traits);

            return element;
        }

        /// What is true about this creature, in words - or nothing at all.
        ///
        /// bible 10.4: "Colour never carries information alone." This screen
        /// was the one place in the app that broke it. A lineage node draws
        /// NO CREATURE - it is a label, a state line and two pips - so there
        /// is no silhouette here to reinforce anything, and Founder and
        /// Mutated were each carried by a 3px coloured border and by nothing
        /// else. `Consumed` was already a word, which is the pattern the
        /// other two now follow; `LineageScreen.ConsumedLabel`'s own comment
        /// had said so all along - "the screen says so in words as well as in
        /// a class name".
        ///
        /// It is also what closes Task 6's deferred palette collision without
        /// moving a colour. The amber rail IS Skitter and the violet rail IS
        /// Hollow, at dE 0.0, so on a screen where colour was the only
        /// channel the rails read as species marks. With the words present,
        /// amber can go on meaning Founder. Rendered evidence, and the three
        /// costed alternatives that were rejected, are in
        /// implementation/results/species-collision.md.
        ///
        /// RETURNS NULL RATHER THAN AN EMPTY ROW, AND THAT IS THE POINT OF
        /// THE SIGNATURE. Phase 8 found eight banners drawn unconditionally
        /// that merely emptied their text - a blank amber strip on this very
        /// screen was one of them, removed in Task 9. The `state` Label this
        /// replaces was the same shape: constructed for every node and given
        /// `string.Empty` for the living ones. Measured, it collapsed to zero
        /// height and was not a defect, but building the ninth one next to
        /// where the eighth was diagnosed is not a thing to do. A node with
        /// nothing to say gets no element.
        static VisualElement MarksFor(LineageNode node)
        {
            var marks = new VisualElement { name = "marks" };
            marks.AddToClassList(MarksUssClassName);

            // Ordered as the creature's own history reads: what it was born
            // as, what happened at its splice, what happened to it in the
            // end. A node can carry more than one - a consumed founder is an
            // ordinary thing - so the row wraps rather than choosing.
            AddMark(marks, node.IsFounder, FounderUssClassName, LineageScreen.FounderLabel);
            AddMark(marks, node.Mutated, MutatedUssClassName, LineageScreen.MutatedLabel);
            // `consumedAt` is a TIMESTAMP, nullable on the wire - its
            // presence is the fact, and the client never parses it.
            AddMark(marks, !string.IsNullOrEmpty(node.ConsumedAt),
                    ConsumedUssClassName, LineageScreen.ConsumedLabel);

            return marks.childCount == 0 ? null : marks;
        }

        /// One word, or nothing. Named after itself so a test and a deep link
        /// can reach a single mark, the same way every node is named after
        /// its creature id.
        static void AddMark(VisualElement into, bool applies, string modifier, string text)
        {
            if (!applies) return;
            var mark = new Label { name = modifier, text = text };
            mark.AddToClassList(MarkUssClassName);
            mark.AddToClassList(MarkUssClassName + "--" + modifier);
            into.Add(mark);
        }

        /// A lineage node's traits are nullable on the wire (a pruned or very
        /// old record can carry none), so an absent trait adds no chip rather
        /// than an empty one.
        ///
        /// =================================================================
        /// A `TraitChip`, NOT A `TraitPip`, AS OF PHASE 9 TASK 19 - AND THE
        /// SENTENCE IN `CreatureCard`'s CLASS COMMENT THAT SAID OTHERWISE IS
        /// REFUTED BY THE LINE THIS ONE REPLACES.
        ///
        /// Task 16 swapped the roster card's marks from pips to chips on a
        /// measurement - "EVERY `CreatureCard.Bind` IN THE APP PASSES
        /// `counters: null`, so the pip's dot has never once gone green in a
        /// shipped screen" - and exempted this screen in the same breath:
        /// "`TraitPip` SURVIVES AND IS STILL THE RIGHT ANSWER WHERE A COUNTER
        /// IS ACTUALLY KNOWN - `LineageView` builds one per trait and that is
        /// untouched" (`CreatureCard.cs:37-40`). A counter is NOT known here.
        /// The line removed from this method passed `counters: null` too, and
        /// its own comment said why it always would: "the lineage response
        /// carries no trait table, and `Broodline.UI` cannot derive one."
        /// So the exemption rested on a premise this file already contained
        /// the refutation of, and the swap's own argument applies verbatim:
        /// nothing is lost, six species tints are gained.
        ///
        /// IT LEFT THIS SCREEN THE LAST ONE IN THE APP DRAWING A PIP, which
        /// is the tell this task exists to remove - `new TraitPip` has no
        /// other caller outside `ComponentTests`. A player reaching the tree
        /// from the roster or the chamber has seen the same creature's traits
        /// as species-tinted chips twice already.
        ///
        /// AND IT UNCOVERS THE PIPS ON THE ONE NODE THE SCREEN IS ABOUT.
        /// `.trait-pip`'s fill is `--violet-tint` (`TraitPip.uss:11`) and so
        /// is `.node.highlight`'s (`LineageView.uss`), so on the highlighted
        /// node - the named Founder, the one image bible 9.2 says a player
        /// carries away - both pills were drawn in the page's own colour and
        /// had no edge at all. Measured on `LineageView.png` before this
        /// change: the two pills on `Ash (G1)` are `#f1ecfa` on `#f1ecfa`.
        /// A species tint collides with the highlight for Hollow alone, one
        /// species in six instead of six in six.
        ///
        /// THE TIER IS REAL HERE, unlike on the Codex sheet - `LineageNode`
        /// carries `tier1`/`tier2` on the wire - so a null one IS an Aberrant
        /// in data_model 2's sense and the chip is right to mark it.
        /// =================================================================
        static void AddTrait(VisualElement into, string trait, int? tier, string species)
        {
            // `IsAbsentTrait` RATHER THAN `IsNullOrEmpty` - Phase 9 Task 21e.
            // This screened for a missing STRING and not for the wire's own
            // word for a missing TRAIT, so a node with one combat trait drew
            // a chip reading "None" beside it - the same defect the exit
            // gate's walk found on the post-wave grant card, in a second
            // place, from a second hand-rolled definition of "absent". There
            // is one definition now and `CreatureLabel.IsAbsentTrait` holds
            // it. Null and empty are still absences; that half is unchanged.
            if (CreatureLabel.IsAbsentTrait(trait)) return;
            // `species` is the NODE's, which is the question a tree asks:
            // whose trait is this. `TraitChip`'s class comment draws the line
            // - "a pip says what a trait COUNTERS ... a chip says whose trait
            // it is" - and a family tree is nothing but the second question.
            into.Add(new TraitChip(trait, tier, species) { name = trait });
        }
    }
}
