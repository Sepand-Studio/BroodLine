using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The single most-repeated element in the app - screen_inventory_v2 11.
    /// Shows a trait, its coverage tier and what it counters (if anything).
    ///
    /// data_model 2 is explicit that a null coverage tier is an Aberrant, and
    /// that null and zero must not be conflated - "zero would sort and
    /// display as 'less than tier I'". `Bind` renders a null tier as the
    /// `aberrant` marker, never as a "tier 0" or an empty numeral.
    [UxmlElement]
    public partial class TraitPip : VisualElement
    {
        public const string UssClassName = "trait-pip";
        public const string AberrantUssClassName = "aberrant";
        public const string CountersUssClassName = "trait-pip--counters";

        readonly Label _label;

        public TraitPip()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("TraitPip");
            tree.CloneTree(this);

            _label = this.Q<Label>("label");
        }

        /// `tier` null means Aberrant (data_model 2) - Aberrants carry no
        /// coverage tier by construction, in either combat slot. `counters`
        /// is the single raider/name this trait answers, or empty/null for
        /// one of the four traits that "counter nothing by design" (bible
        /// 1.2) - Carapace, Litter, Regrow, Screen.
        public void Bind(string trait, int? tier, string counters)
        {
            var text = CreatureLabel.TraitWithTier(trait, tier);
            _label.text = text;

            EnableInClassList(AberrantUssClassName, tier == null);

            var hasCounter = !string.IsNullOrEmpty(counters);
            EnableInClassList(CountersUssClassName, hasCounter);

            tooltip = hasCounter
                ? text + " - counters " + counters + "."
                : text + " - counters nothing.";
        }
    }
}
