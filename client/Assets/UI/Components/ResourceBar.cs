using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// THE TOP BAR'S RESOURCE ROW - Phase 10 Task 1.2. Replaces `CurrencyHeader`,
    /// which was built in Phase 7, bound by one fixture, and never placed on a
    /// production screen. This one is: `BootController` puts it in Shell.uxml's
    /// `#top-bar` and re-binds it from every `PlayerSnapshot`.
    ///
    /// ONE PILL PER BALANCE THE SERVER SENT, in the dictionary's own order, each
    /// named by its key so a screen or a test can `Q(key)` the value. The glyph
    /// is chosen by key (`IconFor`); a key with no glyph draws a bare value
    /// rather than the wrong picture. Only the shard pill carries a "+" - Gene
    /// Shards are the one purchasable balance - and it raises `OnOpenStore`
    /// rather than navigating, because this assembly cannot see the router.
    /// Marks are never purchasable (bible 8.2) and never get a "+", which is a
    /// rule this class enforces by omission, not a convention a screen keeps.
    public sealed class ResourceBar : VisualElement
    {
        public const string UssClassName = "resource-bar";
        public const string PillUssClassName = "resource-bar__pill";
        public const string ValueUssClassName = "resource-bar__value";
        public const string DiscUssClassName = "resource-bar__disc";
        public const string GlyphUssClassName = "resource-bar__glyph";
        public const string BuyUssClassName = "resource-bar__buy";
        public const string TimerUssClassName = "resource-bar__timer";

        /// The one key whose pill offers a purchase.
        public const string PurchasableKey = "shards";

        readonly VisualElement _pills;

        /// The shard pill's "+" was tapped. The Game layer routes it to the
        /// Store; this component only reports it.
        public event Action OnOpenStore;

        public ResourceBar()
        {
            AddToClassList(UssClassName);
            var tree = Resources.Load<VisualTreeAsset>("ResourceBar");
            tree.CloneTree(this);
            _pills = this.Q<VisualElement>("pills");
        }

        /// Which `icons.uss` glyph a balance key wears. Both the server's
        /// `splice_charges` and the older fixtures' `charges` reach the same
        /// bolt, so a rename on the wire does not blank the picture.
        public static string IconFor(string key)
        {
            switch ((key ?? string.Empty).ToLowerInvariant())
            {
                case "splice_charges": case "charges": return "charge";
                case "shards": return "shard";
                case "tier": case "xp": return "tier";
                case "marks": return "mark";
                default: return null;
            }
        }

        public void Bind(IReadOnlyDictionary<string, int> balances) => Bind(balances, null);

        /// `nextCharge` is the time until the next Splice Charge regenerates; it
        /// is shown as a `TimerChip` inside the charges pill when given and the
        /// pill exists, and ignored otherwise.
        public void Bind(IReadOnlyDictionary<string, int> balances, TimeSpan? nextCharge)
        {
            _pills.Clear();
            if (balances == null) return;

            foreach (var balance in balances)
            {
                var pill = new VisualElement { name = "pill-" + balance.Key };
                pill.AddToClassList(PillUssClassName);

                var icon = IconFor(balance.Key);
                if (icon != null)
                {
                    var disc = new VisualElement();
                    disc.AddToClassList(DiscUssClassName);
                    disc.AddToClassList(DiscUssClassName + "--" + icon);
                    var glyph = new VisualElement();
                    glyph.AddToClassList("icon");
                    glyph.AddToClassList("icon--" + icon);
                    glyph.AddToClassList(GlyphUssClassName);
                    disc.Add(glyph);
                    pill.Add(disc);
                }

                var value = new Label
                {
                    name = balance.Key,
                    text = balance.Value.ToString(CultureInfo.InvariantCulture),
                };
                value.AddToClassList(ValueUssClassName);
                value.AddToClassList("t-num");
                pill.Add(value);

                if (nextCharge.HasValue && icon == "charge")
                {
                    var timer = new TimerChip { name = "next-charge" };
                    timer.AddToClassList(TimerUssClassName);
                    timer.Bind(nextCharge.Value);
                    pill.Add(timer);
                }

                if (balance.Key == PurchasableKey)
                {
                    var buy = new Button(() => OnOpenStore?.Invoke()) { name = "buy", text = "+" };
                    buy.AddToClassList(BuyUssClassName);
                    pill.Add(buy);
                }

                _pills.Add(pill);
            }
        }
    }
}
