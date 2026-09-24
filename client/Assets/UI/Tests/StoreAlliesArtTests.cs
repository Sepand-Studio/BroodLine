using System;
using System.Reflection;
using Broodline.Model.Catalogs;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    public sealed class StoreAlliesArtTests
    {
        [Test]
        public void StoreAndFacilityMarksImportAsTwoDimensionalUiSprites()
        {
            var paths = new[]
            {
                "Assets/UI/Art/icons/facilities/core.png",
                "Assets/UI/Art/icons/facilities/splicing.png",
                "Assets/UI/Art/icons/facilities/hatchery.png",
                "Assets/UI/Art/icons/facilities/vault.png",
                "Assets/UI/Art/icons/facilities/harvest.png",
                "Assets/UI/Art/icons/facilities/drive.png",
                "Assets/UI/Art/icons/store/pack-1.png",
                "Assets/UI/Art/icons/store/pack-5.png",
                "Assets/UI/Art/icons/store/pack-10.png",
                "Assets/UI/Art/icons/store/pack-15.png",
                "Assets/UI/Art/icons/store/pack-20.png",
                "Assets/UI/Art/icons/store/pack-50.png",
                "Assets/UI/Art/icons/store/pack-100.png",
            };

            foreach (var path in paths)
            {
                Assert.AreEqual(typeof(Texture2D), AssetDatabase.GetMainAssetTypeAtPath(path),
                    path + " must import as a 2D texture so UI Toolkit can use it in USS");
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    path + " must expose its UI sprite");
            }
        }

        [Test]
        public void EveryStorePackUsesItsOwnMarkAndPurchaseStaysAPreview()
        {
            var view = new StoreView();
            view.Bind(new StoreScreenModel(), () => { }, _ => { });
            foreach (var pack in StoreCatalog.Packs)
            {
                var row = view.Q<VisualElement>("pack-" + pack.Id);
                Assert.IsNotNull(row, pack.Id);
                Assert.IsTrue(row.Q<VisualElement>(className: "store__pack-icon")
                    .ClassListContains("icon--" + pack.Id), pack.Id);
                StringAssert.Contains("(preview)", row.Q<Button>("buy").text);
            }
            foreach (var pack in StoreCatalog.DirectShardPacks)
            {
                var row = view.Q<VisualElement>("pack-" + pack.Id);
                Assert.IsNotNull(row, pack.Id);
                Assert.IsTrue(row.Q<VisualElement>(className: "store__pack-icon")
                    .ClassListContains("icon--" + pack.Id), pack.Id);
                StringAssert.Contains("(preview)", row.Q<Button>("buy").text);
            }
            Assert.AreEqual(StoreScreen.ChestProgress(0), view.Q<Label>("chest-progress").text);
        }

        [Test]
        public void CustomChestShowsAllTiersAndDefaultsToStandard()
        {
            var view = new StoreView();
            view.Bind(new StoreScreenModel(), () => { }, _ => { });

            foreach (var tier in StoreCatalog.ChestTiers)
            {
                var button = view.Q<Button>("chest-tier-" + tier.Id);
                Assert.IsNotNull(button, tier.Id);
                StringAssert.Contains(tier.Label, button.text);
                StringAssert.Contains(tier.Price, button.text);
                Assert.AreEqual(tier.Id == "standard",
                    button.ClassListContains(StoreView.ChestTierActiveUssClassName), tier.Id);
            }

            Assert.AreEqual("15 charges", view.Q("chest-charges").Q<Label>("title").text);
            Assert.AreEqual("STANDARD CHEST · $4.99 · Choose three rewards",
                view.Q<Label>("chest-selection").text);
            Assert.AreEqual("$4.99 · Pick 3 more", view.Q<Button>("chest-buy").text);
        }

        [Test]
        public void CustomChestTierControlUpdatesVisibleAmountsAndPrice()
        {
            var view = new StoreView();
            view.Bind(new StoreScreenModel(), () => { }, _ => { });

            RaiseClicked(view.Q<Button>("chest-tier-large"));

            Assert.IsTrue(view.Q<Button>("chest-tier-large")
                .ClassListContains(StoreView.ChestTierActiveUssClassName));
            Assert.IsFalse(view.Q<Button>("chest-tier-standard")
                .ClassListContains(StoreView.ChestTierActiveUssClassName));
            Assert.AreEqual("30 charges", view.Q("chest-charges").Q<Label>("title").text);
            Assert.AreEqual("LARGE CHEST · $9.99 · Choose three rewards",
                view.Q<Label>("chest-selection").text);
            Assert.AreEqual("$9.99 · Pick 3 more", view.Q<Button>("chest-buy").text);
        }

        [Test]
        public void CustomChestRebindPreservesTheUsersConfiguration()
        {
            var model = new StoreScreenModel();
            var view = new StoreView();
            view.Bind(model, () => { }, _ => { });

            RaiseClicked(view.Q<Button>("chest-tier-large"));
            model.ChestSelection.Toggle("charges");
            model.ChestSelection.Toggle("shards");
            model.GiftAvailable = false;
            view.Bind(model, () => { }, _ => { });

            Assert.IsTrue(view.Q<Button>("chest-tier-large")
                .ClassListContains(StoreView.ChestTierActiveUssClassName));
            Assert.AreEqual("30 charges", view.Q("chest-charges").Q<Label>("title").text);
            Assert.AreEqual(
                "LARGE CHEST · $9.99 · 30 charges + 1,200 shards",
                view.Q<Label>("chest-selection").text);
            Assert.AreEqual("$9.99 · Pick 1 more", view.Q<Button>("chest-buy").text);
        }

        [Test]
        public void AllianceBannerHasAFullPennantAndKeepsThePreviewAction()
        {
            var view = new AlliesView();
            view.Bind(null, () => { });
            Assert.IsNotNull(view.Q<VisualElement>("alliance-pennant"));
            Assert.AreEqual(AlliesScreen.BannerKicker, view.Q<Label>("banner-kicker").text);
            StringAssert.Contains("(preview)", view.Q<Button>("create").text);
        }

        static void RaiseClicked(Button button)
        {
            Assert.IsNotNull(button);
            var clickable = button.clickable;
            Assert.IsNotNull(clickable);
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = typeof(Clickable).GetField("clicked", Flags);
            Assert.IsNotNull(field, "Clickable's backing action moved");
            var action = field.GetValue(clickable) as Action;
            Assert.IsNotNull(action, "the tier button has no click action");
            action();
        }
    }
}
