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
    [UxmlElement]
    public partial class RosterView : VisualElement
    {
        public const string UssClassName = "roster-view";
        public const string CardUssClassName = "roster-view__card";

        readonly Label _notice;
        readonly VisualElement _cards;

        public RosterView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("RosterView");
            tree.CloneTree(this);

            _notice = this.Q<Label>("notice");
            _cards = this.Q<VisualElement>("cards");
        }

        public void Bind(RosterScreen r, Action<CreatureDto> onSelect)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            // The model's own sentence, verbatim - empty only when `Known`
            // is genuinely the whole live roster.
            _notice.text = r.IncompleteNotice ?? string.Empty;

            _cards.Clear();
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
