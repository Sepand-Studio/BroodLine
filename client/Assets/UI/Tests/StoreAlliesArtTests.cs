using Broodline.Model.Catalogs;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    public sealed class StoreAlliesArtTests
    {
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
            Assert.AreEqual(StoreScreen.ChestProgress(0), view.Q<Label>("chest-progress").text);
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
    }
}
