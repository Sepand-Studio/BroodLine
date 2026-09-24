using System;
using System.Globalization;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The server-backed Ark region detail, bound to `RegionScreenModel`
    /// (`client/Assets/UI/RegionScreen.cs`).
    /// It pairs catalog geography with server-owned harvest nodes. Every
    /// `NodeRow.CanClaim`/`.Blocker` is the model's verdict; this view only
    /// renders it and never guesses whether a claim will fit the roster.
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
        readonly Label _regionBand, _regionName, _regionDetail;
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
            // The map is now the top-level tab. This detail is pushed from a
            // selected Ark marker and receives its back action from the Game
            // layer; UI does not depend on ScreenHost.
            _scaffold = new ScreenScaffold("Region Detail", pushed: true, eyebrow: "WORLD MAP · ARK TERRITORY");

            var hero = new VisualElement { name = "region-art" };
            hero.AddToClassList("region-view__hero");
            var terrain = Resources.Load<Texture2D>("Art/map/frontier-atlas");
            if (terrain != null) hero.style.backgroundImage = new StyleBackground(terrain);
            var shade = new VisualElement(); shade.AddToClassList("region-view__hero-shade");
            _regionBand = new Label { name = "region-band" }; _regionBand.AddToClassList("region-view__band");
            _regionName = new Label { name = "region-name" }; _regionName.AddToClassList("region-view__name");
            shade.Add(_regionBand); shade.Add(_regionName); hero.Add(shade);
            _scaffold.Content.Add(hero);

            var territory = new SectionCard("ARK TERRITORY");
            _regionDetail = new Label { name = "region-detail" };
            _regionDetail.AddToClassList("region-view__detail");
            territory.Body.Add(_regionDetail);
            _scaffold.Content.Add(territory);

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

        public void Bind(RegionScreenModel m, Action<int> onClaim, Action onBack = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            _scaffold.OnBack = onBack;
            _regionBand.text = m.Band;
            _regionName.text = m.Name;
            _regionDetail.text = "Your Ark is here · " + MapScreen.Lanes(m.Lanes) + "\nBorders  " + m.Neighbours;

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
