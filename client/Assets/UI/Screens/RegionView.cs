using System;
using System.Globalization;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The map tab, bound to `RegionScreenModel`
    /// (`client/Assets/UI/RegionScreen.cs`).
    ///
    /// INTERIM, AND `RegionView.uss`'s HEADER IS THE RECORD OF WHY. It states
    /// what the handoff's Splice World Map v2 needs, what of it exists, what
    /// the plan that specified this task got wrong about that, and who owns
    /// the rest. The short version: this is a node LIST with Phase 8's
    /// foundation applied, deferred to Phase 10, and it is not the map.
    ///
    /// Every `NodeRow.CanClaim`/`.Blocker` is the model's own verdict -
    /// `map/claim.ts`'s roster-room check, mirrored there only so the screen
    /// can say so before the tap. This view renders that verdict; it does
    /// not recompute "does this grant fit the roster" from `RosterCount`/
    /// `RosterCap` itself.
    [UxmlElement]
    public partial class RegionView : VisualElement
    {
        public const string UssClassName = "region-view";

        /// One node's handle. Still called a ROW though the element under it
        /// is a `SectionCard` as of Phase 8 Task 12: five `ScreenBindingTests`
        /// assertions reach a node through this name, and renaming it would
        /// move all five for a word. It carries no rule in the stylesheet -
        /// the card is `SectionCard`'s.
        public const string NodeRowUssClassName = "node-row";

        public const string StatsUssClassName = "region-view__stats";
        public const string BlockerUssClassName = "region-view__blocker";
        public const string ClaimUssClassName = "region-view__claim";

        readonly ScreenScaffold _scaffold;
        readonly Label _roster;
        readonly VisualElement _nodes;

        public RegionView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("RegionView");
            tree.CloneTree(this);

            _roster = this.Q<Label>("roster");
            _nodes = this.Q<VisualElement>("nodes");

            // THE FRAME, COMPOSED AND NOT INHERITED. ScreenScaffold's class
            // comment has the reason in full: `ScaffoldTests`' sweep asks each
            // screen for a DESCENDANT carrying `screen-scaffold`, and UQuery
            // never matches the element it is called on - so a screen that
            // derived from the scaffold would read as bare. This is the last
            // of the ten the sweep was listing.
            //
            // pushed: false, AND IT IS THE ONE SCREEN THE HANDOFF ANSWERS
            // OUTRIGHT. Its navigation model names five tab destinations -
            // Map, Ark, Splice, Lab, Allies - and the table under them reads
            // "Map -> World Map", which is this screen. A tab destination is
            // top-level by definition, so there is no back stack to leave and
            // no `onBack` on `Bind`: `ATopLevelScreenHasNoChevronEvenWithABackAction`
            // would discard one anyway. The plan's step 1 asks for `onBack`
            // calling `ScreenHost.Pop`; `Broodline.UI.asmdef` references
            // Model, Net and Generated.Api and not `Broodline.Game`, so this
            // assembly cannot name that type at all - see RosterView.Bind.
            _scaffold = new ScreenScaffold(RegionScreen.Title);

            // THE ROSTER HEADLINE IS THE HEADER SLOT'S, AND IT IS THE ONLY
            // THING IN IT. The handoff puts three currency chips across the
            // top of this screen - Splice Charges 4/5, Gene Shards 1,240,
            // Breeder Tier 6 - and `CurrencyHeader` is the component that
            // draws them, which is what this task's step 1 asks for. It is
            // NOT here, because there is nothing to bind to it:
            // /v1/region/state carries no balances and `RegionScreenModel`
            // holds none, so the header would be `Bind`-ed with an empty
            // dictionary or not at all. `.currency-header` is --space-2 of
            // padding above and below an empty row either way, which is the
            // eighth instance of the blank strip this phase has spent four
            // tasks removing. The count/cap it would have sat beside is a
            // roster headline and not a currency, and `CurrencyHeader.Bind`
            // renders "key: value" from an int, so it cannot express a cap.
            _scaffold.HeaderSlot.Add(_roster);
            _scaffold.Content.Add(_nodes);
            Add(_scaffold);
        }

        public void Bind(RegionScreenModel m, Action<int> onClaim)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            // The model's own string either way - `RegionScreen.RosterHeadline`
            // holds both the `HasRosterCounts` gate and the formatting, on the
            // convention `UnlimitedLabel` and `ClaimCta` already follow.
            Roster(RegionScreen.RosterHeadline(m));

            _nodes.Clear();

            // AN EMPTY LIST, NOT AN UNCLAIMABLE ONE, and the difference is the
            // whole point of the state. This task's step 2 reads "EmptyState
            // when no nodes are claimable", which describes a worse screen:
            // when every node is spent there ARE rows, each carrying the
            // sentence that says why its Claim is off, and replacing them with
            // one centred message would delete the explanation on the only
            // screen that can give it. The empty state is for a list that
            // holds nothing.
            if (m.Nodes == null || m.Nodes.Count == 0)
            {
                _nodes.Add(new EmptyState(RegionScreen.EmptyMessage, "map"));
                return;
            }

            foreach (var node in m.Nodes)
            {
                _nodes.Add(CardFor(node, onClaim));
            }
        }

        /// The roster headline, shown only when it says something.
        ///
        /// THE EIGHTH BLANK STRIP OF THIS PHASE, and the first that is a bare
        /// header line rather than a tinted panel. The Label was added
        /// unconditionally with `margin-bottom: var(--space-3)` and emptied
        /// its text when `HasRosterCounts` was false, so a response that
        /// carried no roster headline drew 12px of nothing above the node
        /// list. Same rule as `DeployView.Blocker`, `FounderNamingView.
        /// Blocker` and `ScreenScaffold.FooterNote`; the text still
        /// round-trips to string.Empty, which is what `ScreenBindingTests`
        /// reads.
        void Roster(string headline)
        {
            _roster.text = headline ?? string.Empty;
            _roster.style.display = string.IsNullOrEmpty(headline)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// One node, as a card.
        ///
        /// THE TWO NUMBERS GAINED A WORD EACH, WHICH IS THE CHANGE THAT
        /// MATTERS HERE. The previous row rendered "Shard 12 3" - the type,
        /// then two bare integers with nothing on the screen saying which was
        /// shards waiting and which was harvests left. They are `StatCell`s
        /// now, so each carries its own label and its value carries `t-num`,
        /// which is the marker bible 10.6's tabular figures and 11px floor
        /// are both enforced through.
        static VisualElement CardFor(NodeRow node, Action<int> onClaim)
        {
            var card = new SectionCard(node.Type)
            {
                name = "node-" + node.Slot.ToString(CultureInfo.InvariantCulture),
            };
            card.AddToClassList(NodeRowUssClassName);

            var stats = new VisualElement();
            stats.AddToClassList(StatsUssClassName);
            stats.Add(new StatCell(
                RegionScreen.AccruedStatLabel, RegionScreen.StatValue(node.Accrued)));
            // The model's own text - `NodeRow.RemainingLabel` already chose
            // between the number and `RegionScreen.UnlimitedLabel`; this view
            // does not choose again.
            stats.Add(new StatCell(
                RegionScreen.RemainingStatLabel, node.RemainingLabel ?? string.Empty));
            // THE GRANT WAS ON NO SCREEN AT ALL. `Grants` is "creatures this
            // claim would grant" and it is the fact the roster refusal below
            // is entirely about - "Your roster has no room for 1 more" - yet
            // a granting node rendered identically to a shard node until that
            // refusal fired. Stated only when it is non-zero: `Grants` is 0
            // for every pure shard node, and a cell reading "0" on all of
            // them would be noise around the one that matters.
            if (node.Grants > 0)
            {
                stats.Add(new StatCell(
                    RegionScreen.GrantsStatLabel, RegionScreen.StatValue(node.Grants)));
            }
            card.Body.Add(stats);

            // The refusal, shown only when it says something, and directly
            // above the button it explains. Same rule as the roster headline:
            // `.panel-coral` is --coral-tint with --space-3 of padding, so an
            // empty one is a blank coral strip inside every claimable node's
            // card - which is the ORDINARY case, two of the three nodes in the
            // capture fixture and both of the two the server actually serves.
            var blocker = new Label { name = "blocker", text = node.Blocker ?? string.Empty };
            blocker.AddToClassList(BlockerUssClassName);
            blocker.AddToClassList("panel-coral");
            blocker.AddToClassList("t-danger");
            blocker.style.display = string.IsNullOrEmpty(node.Blocker)
                ? DisplayStyle.None : DisplayStyle.Flex;
            card.Body.Add(blocker);

            // The model's own label - `NodeRow.ClaimLabel`, not a literal this
            // view authors. Theme's base `Button` treatment and not
            // `.btn-primary`: see the stylesheet.
            var claim = new Button { name = "claim", text = node.ClaimLabel ?? string.Empty };
            claim.AddToClassList(ClaimUssClassName);
            claim.SetEnabled(node.CanClaim);
            var slot = node.Slot;
            claim.clicked += () => onClaim?.Invoke(slot);
            card.Body.Add(claim);

            return card;
        }
    }
}
