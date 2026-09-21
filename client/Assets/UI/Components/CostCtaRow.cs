using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The handoff's commit row: what it costs on the left, the button on
    /// the right, one line.
    ///
    /// THE COST AND THE BUTTON ARE ONE ELEMENT BECAUSE THEY ARE ONE
    /// SENTENCE. `Splice Chamber.dc.html` puts them in a single flex row -
    /// "Cost / 2" then "Begin Splice" - and that adjacency is the design:
    /// the price is read on the way to the tap, not in a footnote above it.
    /// Built separately, the two drift apart the first time a screen adds a
    /// warning between them, and the player taps a CTA whose cost is off
    /// screen.
    ///
    /// THE COST IS ON `t-num`, on the rule the rest of this vocabulary
    /// follows - it is the number the decision is literally about. The
    /// handoff sets it in Baloo 2 at 17px; this keeps the 17 and changes the
    /// face, for the ten digit widths Theme.uss's header measured.
    ///
    /// IT DOES NOT KNOW WHETHER THE PLAYER CAN AFFORD IT. `Enabled` is the
    /// caller's to set, from the balance the server sent. A row that read
    /// the cost string and compared it to something would be this assembly
    /// deciding an economy question, which `SpliceConfirmTests` exists to
    /// prevent.
    public sealed class CostCtaRow : VisualElement
    {
        public const string UssClassName = "cost-cta-row";

        /// "COST", uppercase in the markup because UI Toolkit has no
        /// `text-transform` - the handoff's `.lbl` class gets it from CSS,
        /// and `.t-micro` can only carry the size, weight, colour and
        /// tracking. Public so a screen can state a different eyebrow
        /// without a second copy of the styling.
        public const string CostEyebrow = "COST";

        readonly Button _cta;

        public CostCtaRow(string costIcon, string cost, string ctaLabel, Action onCta)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("CostCtaRow").CloneTree(this);

            this.Q<Label>("eyebrow").text = CostEyebrow;
            this.Q<Label>("cost").text = cost ?? string.Empty;

            var glyph = this.Q<VisualElement>("cost-icon");
            if (string.IsNullOrEmpty(costIcon)) glyph.RemoveFromHierarchy();
            else glyph.AddToClassList(costIcon);

            _cta = this.Q<Button>("cta");
            _cta.text = ctaLabel ?? string.Empty;

            // Registered, not provable - ComponentTests' class comment, point
            // 3: a dispatched click needs an attached Panel, which that
            // assembly has none of. `OptionRow` and `ConfirmDialog.Standard`
            // make the same trade. Subscribed once, here, so a re-bound
            // screen cannot stack a second handler behind the first.
            if (onCta != null) _cta.clicked += () => onCta();
        }

        /// The button's own enabled state, which Theme.uss's `Button:disabled`
        /// already draws - --hairline fill, --mute-soft label, no drop edge.
        /// Reads back off the control rather than a shadow field, on
        /// `OptionRow.Selected`'s rule: no second copy of the state to drift
        /// from what is rendered.
        public bool Enabled
        {
            get => _cta.enabledSelf;
            set => _cta.SetEnabled(value);
        }
    }
}
