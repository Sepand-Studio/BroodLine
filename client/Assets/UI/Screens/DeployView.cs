using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The deployment screen bound to `DeployScreenModel`
    /// (`client/Assets/UI/DeployScreen.cs`).
    ///
    /// `DeployScreen.Floor` and `.Cap` (mirrored from
    /// services/api/src/wave/issuance.ts) are the model's rule, not this
    /// view's. This view reads `CanDeploy`/`Blocker` only - it never counts
    /// `m.Slots` to decide whether Start should be enabled, because that
    /// would be a second copy of a rule the model already owns and could
    /// disagree with it (a duplicate id or a committed creature makes
    /// `CanDeploy` false at a count the floor/cap alone would allow).
    [UxmlElement]
    public partial class DeployView : VisualElement
    {
        public const string UssClassName = "deploy-view";
        public const string SlotUssClassName = "deploy-view__slot";

        readonly VisualElement _slots;
        readonly Label _blocker;
        readonly Button _start;

        Action _onStart;

        public DeployView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("DeployView");
            tree.CloneTree(this);

            _slots = this.Q<VisualElement>("slots");
            _blocker = this.Q<Label>("blocker");
            _start = this.Q<Button>("start");

            // Registered once, in the constructor, against a field the next
            // Bind can overwrite - so re-binding this same instance (a
            // recycled screen) cannot stack a second `onStart` behind the
            // first the way `clicked += onStart` inside Bind would.
            _start.clicked += () => _onStart?.Invoke();
        }

        public void Bind(DeployScreenModel m, Action onStart)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            _slots.Clear();
            foreach (var slot in m.Slots)
            {
                var card = new CreatureCard();
                // No `counters` map reaches this Bind - DeployScreen.Build's
                // signature carries none, and Broodline.UI cannot derive one
                // itself (bible's raider table lives in Broodline.Sim).
                card.Bind(slot.Creature, null);
                card.AddToClassList(SlotUssClassName);
                _slots.Add(card);
            }

            // The model's own sentence, verbatim - empty when `CanDeploy`.
            _blocker.text = m.Blocker ?? string.Empty;

            // The model's own label too - DeployScreen.Cta, not a literal
            // this view authors itself.
            _start.text = m.CtaLabel ?? string.Empty;
            _start.SetEnabled(m.CanDeploy);
            _onStart = onStart;
        }
    }
}
