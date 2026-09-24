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
        public const string ChestTierActiveUssClassName = "store__chest-tier--active";

        readonly VisualElement _tabs, _panels, _packs, _chest, _pass;
        readonly ScreenScaffold _scaffold;
        readonly Dictionary<string, OptionRow> _chestRows = new Dictionary<string, OptionRow>();
        readonly Dictionary<string, Button> _chestTierButtons = new Dictionary<string, Button>();
        ChestSelectionModel _chestSelection;
        Button _chestCta;
        Label _chestSummary, _chestProgress;

        public StoreView()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("StoreView").CloneTree(this);
            var body = this.Q<VisualElement>("body");
            body.RemoveFromHierarchy();
            // PUSHED, SO THE CHEVRON DRAWS. The router pushes the Store over
            // whatever tab is showing and the push hides the tab bar, so the
            // chevron is the only way out - the simulator walk of Task 1.8
            // found a Store with neither, which is a screen a player cannot
            // leave. ScreenScaffold draws the chevron only for a pushed
            // screen with a back action.
            _scaffold = new ScreenScaffold(StoreScreen.Title, pushed: true, eyebrow: StoreScreen.Eyebrow);
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
            gift.AddToClassList("store__gift");
            var giftBody = new VisualElement(); giftBody.AddToClassList("store__gift-body");
            var shard = new VisualElement();
            shard.AddToClassList("icon"); shard.AddToClassList("icon--shard"); shard.AddToClassList("store__gift-icon");
            giftBody.Add(shard);
            giftBody.Add(new Label(StoreScreen.GiftDetail) { name = "gift-detail" }.WithClass("store__gift-detail"));
            gift.Body.Add(giftBody);
            var claim = new Button(() => onGift?.Invoke()) { name = "gift", text = m.GiftAvailable ? StoreScreen.GiftClaim : StoreScreen.GiftClaimed };
            claim.AddToClassList("btn-reward");
            claim.SetEnabled(m.GiftAvailable);
            gift.Body.Add(claim);
            _packs.Add(gift);

            AddPackSection(StoreScreen.PacksTab, StoreCatalog.Packs, onBuy);
            AddPackSection("Gene shards", StoreCatalog.DirectShardPacks, onBuy);

            // Chest: pick three of six.
            _chest.Clear(); _chestRows.Clear(); _chestTierButtons.Clear();
            _chestSelection = m.ChestSelection;
            var chest = new SectionCard(StoreScreen.ChestHeading);
            var tiers = new VisualElement { name = "chest-tiers" };
            tiers.AddToClassList("store__chest-tiers");
            foreach (var tier in StoreCatalog.ChestTiers)
            {
                var tierId = tier.Id;
                var tierButton = new Button(() =>
                {
                    _chestSelection.SelectTier(tierId);
                    RefreshChest();
                }) { name = "chest-tier-" + tier.Id, text = tier.Label + "\n" + tier.Price };
                tierButton.AddToClassList("store__chest-tier");
                tiers.Add(tierButton);
                _chestTierButtons.Add(tier.Id, tierButton);
            }
            chest.Body.Add(tiers);
            var grid = new VisualElement(); grid.AddToClassList("store__chest-grid");
            foreach (var option in StoreCatalog.ChestOptions)
            {
                var id = option.Id; OptionRow row = null;
                row = new OptionRow(_chestSelection.TitleFor(option), option.Detail, () =>
                {
                    _chestSelection.Toggle(id);
                    RefreshChest();
                }) { name = "chest-" + option.Id };
                row.AddToClassList(ChestOptionUssClassName);
                var glyph = new VisualElement();
                glyph.AddToClassList("icon"); glyph.AddToClassList("icon--" + option.Icon); glyph.AddToClassList("store__chest-icon");
                row.Add(glyph);
                grid.Add(row);
                _chestRows.Add(option.Id, row);
            }
            chest.Body.Add(grid);
            _chestProgress = new Label { name = "chest-progress" };
            _chestProgress.AddToClassList("store__chest-progress");
            _chestProgress.AddToClassList("t-num");
            chest.Body.Add(_chestProgress);
            _chestSummary = new Label { name = "chest-selection" };
            _chestSummary.AddToClassList("store__chest-summary");
            chest.Body.Add(_chestSummary);
            chest.Body.Add(new Label(StoreScreen.ChestNote) { name = "chest-note" }.WithClass("t-secondary"));
            _chestCta = new Button(() => onBuy?.Invoke(_chestSelection.PurchaseId)) { name = "chest-buy" };
            _chestCta.AddToClassList("btn-primary");
            chest.Body.Add(_chestCta);
            _chest.Add(chest);
            RefreshChest();

            // Pass and the one permanent purchase.
            _pass.Clear();
            foreach (var (id, title, detail, price) in new[]
            {
                (StoreCatalog.SeasonPassId, StoreScreen.PassTitle, StoreScreen.PassDetail, StoreCatalog.SeasonPassPrice),
                (StoreCatalog.DoubleRegenId, StoreScreen.RegenTitle, StoreScreen.RegenDetail, StoreCatalog.DoubleRegenPrice),
            })
            {
                var card = new SectionCard(title);
                card.AddToClassList("store__pass-card");
                card.Body.Add(new Label(detail).WithClass("t-secondary"));
                var buy = new Button(() => onBuy?.Invoke(id)) { name = "buy-" + id, text = price + " · " + StoreScreen.Buy };
                buy.AddToClassList("btn-secondary");
                card.Body.Add(buy);
                _pass.Add(card);
            }
        }

        void AddPackSection(string title, IReadOnlyList<StorePack> packs, Action<string> onBuy)
        {
            var section = new SectionCard(title);
            foreach (var pack in packs)
            {
                var id = pack.Id;
                var row = new VisualElement { name = "pack-" + pack.Id };
                row.AddToClassList(PackRowUssClassName);
                var main = new VisualElement(); main.AddToClassList("store__pack-main");
                var crest = new VisualElement(); crest.AddToClassList("icon"); crest.AddToClassList("icon--" + pack.Id); crest.AddToClassList("store__pack-icon");
                var text = new VisualElement(); text.AddToClassList("store__pack-text");
                var packTitle = new Label(pack.Name); packTitle.AddToClassList("t-card-title");
                var detail = new Label(StoreScreen.Shards(pack.Shards) + " · " + pack.Note); detail.AddToClassList("t-secondary");
                text.Add(packTitle); text.Add(detail);
                main.Add(crest); main.Add(text);
                var purchase = new VisualElement(); purchase.AddToClassList("store__purchase");
                var price = new Label(pack.Price); price.AddToClassList("store__price");
                var buy = new Button(() => onBuy?.Invoke(id)) { name = "buy", text = StoreScreen.Buy };
                buy.AddToClassList("btn-secondary"); buy.AddToClassList("store__buy");
                purchase.Add(price); purchase.Add(buy);
                row.Add(main); row.Add(purchase);
                section.Body.Add(row);
            }
            _packs.Add(section);
        }

        void RefreshChest()
        {
            if (_chestSummary == null) return;
            _chestProgress.text = StoreScreen.ChestProgress(_chestSelection.Count);
            _chestSummary.text = _chestSelection.Summary;
            _chestCta.text = _chestSelection.Cta;
            _chestCta.SetEnabled(_chestSelection.Count >= StoreCatalog.ChestPicks);

            foreach (var tier in StoreCatalog.ChestTiers)
                _chestTierButtons[tier.Id].EnableInClassList(
                    ChestTierActiveUssClassName, tier.Id == _chestSelection.Tier.Id);
            foreach (var option in StoreCatalog.ChestOptions)
            {
                var row = _chestRows[option.Id];
                row.Q<Label>("title").text = _chestSelection.TitleFor(option);
                row.Selected = _chestSelection.IsPicked(option.Id);
            }
        }
    }

    static class LabelExtensions
    {
        public static Label WithClass(this Label label, string cls) { label.AddToClassList(cls); label.style.whiteSpace = WhiteSpace.Normal; return label; }
    }
}
