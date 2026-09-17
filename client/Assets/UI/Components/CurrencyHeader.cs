using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// screen_inventory_v2 11: "charges with regen timer, shards, tier
    /// progress." The regen timer itself is a `TimerChip`, composed beside
    /// this by the screen that owns both - `balances` is amounts only, one
    /// entry per currency, so this component never has to guess what a
    /// screen wants to pair it with.
    [UxmlElement]
    public partial class CurrencyHeader : VisualElement
    {
        public const string UssClassName = "currency-header";
        public const string ItemUssClassName = "currency-header__item";

        readonly VisualElement _items;

        public CurrencyHeader()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CurrencyHeader");
            tree.CloneTree(this);

            _items = this.Q<VisualElement>("items");
        }

        /// Replaces rather than accumulates, the same contract `TabBar.Render`
        /// documents: a currency dropping out of the set (or a re-bind of the
        /// same header for a different player) must not leave a stale label
        /// behind it.
        public void Bind(IReadOnlyDictionary<string, int> balances)
        {
            _items.Clear();
            if (balances == null) return;

            foreach (var balance in balances)
            {
                var label = new Label
                {
                    name = balance.Key,
                    text = balance.Key + ": " + balance.Value.ToString(CultureInfo.InvariantCulture),
                };
                label.AddToClassList(ItemUssClassName);
                _items.Add(label);
            }
        }
    }
}
