using System;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The roster screen bound to `RosterScreen`
    /// (`client/Assets/UI/RosterScreen.cs`) - itself the model, per the
    /// task's own resolution: there is no separate `RosterScreenModel`.
    ///
    /// `IncompleteNotice` already tells the three states - failed load,
    /// never loaded, loaded-but-behind-the-map - apart (Phase 6 fix
    /// 7c95cd9). This view renders that sentence; it never infers
    /// completeness from `Known.Count` itself.
    ///
    /// THE NOTICE IS THE SCAFFOLD'S FOOTER NOTE AS OF PHASE 8 TASK 10, not a
    /// floating Label above the grid. It is a caveat about the list, which is
    /// the row's whole job, and it inherits the row's rule that an empty one
    /// COLLAPSES - so a complete roster no longer draws a blank strip where a
    /// warning would go. The text still round-trips to `string.Empty`, which
    /// is what `ScreenBindingTests` reads, through the element that now
    /// carries it.
    [UxmlElement]
    public partial class RosterView : VisualElement
    {
        public const string UssClassName = "roster-view";
        public const string CardUssClassName = "roster-view__card";

        readonly ScreenScaffold _scaffold;
        readonly VisualElement _cards;

        public RosterView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("RosterView");
            tree.CloneTree(this);

            _cards = this.Q<VisualElement>("cards");

            // THE FRAME, COMPOSED AND NOT INHERITED. ScreenScaffold's class
            // comment has the reason in full: `ScaffoldTests`' sweep asks each
            // screen for a DESCENDANT carrying `screen-scaffold`, and UQuery
            // never matches the element it is called on - so a screen that
            // derived from the scaffold would read as bare.
            //
            // pushed: true - the handoff's push table reaches Creature Roster
            // from the Splice Chamber, and the five tab destinations are Map,
            // Ark, Splice, Lab and Allies, none of which is this screen. The
            // CHEVRON is a separate question from the layout and it follows
            // `Bind`'s `onBack` - see below.
            _scaffold = new ScreenScaffold(RosterScreen.Title, pushed: true);
            _scaffold.Content.Add(_cards);
            Add(_scaffold);
        }

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron.
        ///
        /// A CALLER'S ACTION RATHER THAN `ScreenHost.Pop`, because
        /// `Broodline.UI.asmdef` references Model, Net and Generated.Api and
        /// NOT `Broodline.Game` - this assembly cannot name that type, which
        /// is the same one-way dependency `ScreenScaffold`'s "it does NOT own
        /// navigation" is written to. Optional because nothing pushes this
        /// screen today: `ScreenFlow.PushAsync` has no caller in the client at
        /// all, and `BootController.OnTabSelected` is still empty, so the
        /// roster is built only by tests and by this phase's capture harness.
        /// A chevron drawn anyway would be an affordance that does nothing,
        /// which `APushedScreenDrawsNoChevronUntilItIsGivenSomewhereToGo`
        /// exists to forbid.
        public void Bind(RosterScreen r, Action<CreatureDto> onSelect, Action onBack = null)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            _scaffold.OnBack = onBack;

            // The model's own sentence, verbatim - empty only when `Known`
            // is genuinely the whole live roster, and an empty one hides the
            // footer row rather than drawing it blank.
            _scaffold.FooterNote = r.IncompleteNotice;

            _cards.Clear();

            // AN EMPTY GRID SAYS WHICH KIND OF EMPTY IT IS, or says nothing.
            // `RosterScreen.EmptyMessage` claims the player owns nothing, and
            // that claim is only available when the cache knows it holds the
            // whole roster - `IncompleteNotice`'s own comment is that "you own
            // nothing" and "we could not find out what you own" must never
            // read the same. When the roster is incomplete the footer note
            // above already carries the second sentence, in full, including
            // what to do about it, so a second banner here would be the same
            // fact in two voices.
            if (r.Known.Count == 0)
            {
                if (r.IsComplete) _cards.Add(new EmptyState(RosterScreen.EmptyMessage, "ark"));
                return;
            }

            foreach (var creature in r.Known)
            {
                // Named after the creature id - the structural precondition
                // for `onSelect` wiring a headless test can prove, the same
                // way TabBarTests names each button after its own tab.
                var card = new CreatureCard { name = creature.CreatureId.ToString() };
                // No counters map reaches this Bind either - see DeployView.
                card.Bind(creature, null);
                card.AddToClassList(CardUssClassName);

                var selected = creature;
                card.RegisterCallback<ClickEvent>(_ => onSelect?.Invoke(selected));

                _cards.Add(card);
            }
        }
    }
}
