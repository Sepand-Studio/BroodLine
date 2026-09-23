using System;
using System.Collections.Generic;
using Broodline.Model.Catalogs;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    public sealed class StoreView : VisualElement
    {
        public const string UssClassName = "store";
        public const string TabUssClassName = "store__tab";
        public const string TabActiveUssClassName = "store__tab--active";
        public const string PackRowUssClassName = "store__pack";
        public const string ChestOptionUssClassName = "store__chest-option";

        readonly VisualElement _tabs, _panels, _packs, _chest, _pass;
        readonly ScreenScaffold _scaffold;
        readonly HashSet<string> _picked = new HashSet<string>();
        Button _chestCta;

        public StoreView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("StoreView").CloneTree(this);
            var body = this.Q<VisualElement>("body");
            body.RemoveFromHierarchy();
            _scaffold = new ScreenScaffold(StoreScreen.Title, eyebrow: StoreScreen.Eyebrow);
            _scaffold.Content.Add(body);
            _scaffold.FooterNote = StoreScreen.Preview;
            Add(_scaffold);

            _tabs = this.Q<VisualElement>("tabs");
            _panels = this.Q<VisualElement>("panels");
            _packs = this.Q<VisualElement>("packs");
            _chest = this.Q<VisualElement>("chest");
            _pass = this.Q<VisualElement>("pass");

            foreach (var (name, label) in new[] { ("packs", StoreScreen.PacksTab), ("chest", StoreScreen.ChestTab), ("pass", StoreScreen.PassTab) })
            {
                var tab = new Button(() => Select(name)) { name = "tab-" + name, text = label };
                tab.AddToClassList(TabUssClassName);
                _tabs.Add(tab);
            }
            Select("packs");
        }

        public void Select(string panel)
        {
            foreach (var child in _panels.Children()) child.style.display = child.name == panel ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var tab in _tabs.Children()) tab.EnableInClassList(TabActiveUssClassName, tab.name == "tab-" + panel);
        }

        public void Bind(StoreScreenModel m, Action onGift, Action<string> onBuy, Action onBack = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            _scaffold.OnBack = onBack;

            // Packs: the gift first, then the ladder.
            _packs.Clear();
            var gift = new SectionCard(StoreScreen.GiftTitle);
            gift.Body.Add(new Label(StoreScreen.GiftDetail) { name = "gift-detail" });
            var claim = new Button(() => onGift?.Invoke()) { name = "gift", text = m.GiftAvailable ? StoreScreen.GiftClaim : StoreScreen.GiftClaimed };
            claim.AddToClassList("btn-reward");
            claim.SetEnabled(m.GiftAvailable);
            gift.Body.Add(claim);
            _packs.Add(gift);

            var ladder = new SectionCard(StoreScreen.PacksTab);
            foreach (var pack in StoreCatalog.Packs)
            {
                var id = pack.Id;
                var row = new VisualElement { name = "pack-" + pack.Id };
                row.AddToClassList(PackRowUssClassName);
                var text = new VisualElement(); text.AddToClassList("store__pack-text");
                var title = new Label(pack.Name); title.AddToClassList("t-card-title");
                var detail = new Label(StoreScreen.Shards(pack.Shards) + " · " + pack.Note); detail.AddToClassList("t-secondary");
                text.Add(title); text.Add(detail);
                var buy = new Button(() => onBuy?.Invoke(id)) { name = "buy", text = pack.Price };
                buy.AddToClassList("btn-secondary"); buy.AddToClassList("store__buy");
                row.Add(text); row.Add(buy);
                ladder.Body.Add(row);
            }
            _packs.Add(ladder);

            // Chest: pick three of six.
            _chest.Clear(); _picked.Clear();
            var chest = new SectionCard(StoreScreen.ChestHeading);
            var grid = new VisualElement(); grid.AddToClassList("store__chest-grid");
            foreach (var option in StoreCatalog.ChestOptions)
            {
                var id = option.Id; OptionRow row = null;
                row = new OptionRow(option.Title, option.Detail, () =>
                {
                    if (_picked.Contains(id)) _picked.Remove(id);
                    else if (_picked.Count < StoreCatalog.ChestPicks) _picked.Add(id);
                    row.Selected = _picked.Contains(id);
                    _chestCta.text = StoreScreen.ChestPick(_picked.Count);
                    _chestCta.SetEnabled(_picked.Count >= StoreCatalog.ChestPicks);
                }) { name = "chest-" + option.Id };
                row.AddToClassList(ChestOptionUssClassName);
                grid.Add(row);
            }
            chest.Body.Add(grid);
            chest.Body.Add(new Label(StoreScreen.ChestNote) { name = "chest-note" }.WithClass("t-secondary"));
            _chestCta = new Button(() => onBuy?.Invoke("chest")) { name = "chest-buy", text = StoreScreen.ChestPick(0) };
            _chestCta.AddToClassList("btn-primary");
            _chestCta.SetEnabled(false);
            chest.Body.Add(_chestCta);
            _chest.Add(chest);

            // Pass and the one permanent purchase.
            _pass.Clear();
            foreach (var (id, title, detail, price) in new[]
            {
                (StoreCatalog.SeasonPassId, StoreScreen.PassTitle, StoreScreen.PassDetail, StoreCatalog.SeasonPassPrice),
                (StoreCatalog.DoubleRegenId, StoreScreen.RegenTitle, StoreScreen.RegenDetail, StoreCatalog.DoubleRegenPrice),
            })
            {
                var card = new SectionCard(title);
                card.Body.Add(new Label(detail).WithClass("t-secondary"));
                var buy = new Button(() => onBuy?.Invoke(id)) { name = "buy-" + id, text = price + " · " + StoreScreen.Buy };
                buy.AddToClassList("btn-secondary");
                card.Body.Add(buy);
                _pass.Add(card);
            }
        }
    }

    static class LabelExtensions
    {
        public static Label WithClass(this Label label, string cls) { label.AddToClassList(cls); label.style.whiteSpace = WhiteSpace.Normal; return label; }
    }
}
