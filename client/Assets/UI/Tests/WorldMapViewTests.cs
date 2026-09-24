using System.Linq;
using Broodline.Model.Catalogs;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    public class WorldMapViewTests
    {
        [Test]
        public void Atlas_AndList_RepresentEveryRegion_AndMarkTheArk()
        {
            var view = new WorldMapView();
            view.Bind(MapScreen.Build("holdfast"), _ => { });

            var atlas = view.Q<VisualElement>("atlas");
            var viewport = view.Q<VisualElement>("atlas-viewport");
            Assert.AreSame(viewport, atlas.parent, "terrain, rings and markers must pan together inside one clipped viewport");
            Assert.IsNotNull(view.Q<Button>("zoom-out"));
            Assert.IsNotNull(view.Q<Button>("zoom-reset"));
            Assert.IsNotNull(view.Q<Button>("zoom-in"));
            Assert.AreEqual(30, atlas.Query<Button>(className: "world-map__marker").ToList().Count);
            Assert.IsTrue(atlas.Q<Button>("marker-holdfast").ClassListContains("world-map__marker--here"));
            Assert.IsTrue(atlas.Q<Button>("marker-tellin").ClassListContains("world-map__marker--gate"));
            Assert.IsFalse(atlas.Q<Button>("marker-holdfast").ClassListContains("world-map__marker--gate"));
            Assert.IsTrue(view.Q<Label>("map-legend").text.Contains("REACH GATE"));
            Assert.AreEqual(30, view.Query(className: WorldMapView.RowUssClassName).ToList().Count);
        }

        [Test]
        public void SelectingDistantRegionNumbersTheShortestRouteWithoutMovingTheArk()
        {
            var view = new WorldMapView();
            view.Bind(MapScreen.Build("holdfast"), _ => { });
            Assert.IsTrue(view.Select("sheerdown"));
            int minutes = RegionCatalog.TravelMinutes("holdfast", "sheerdown", out var route);

            Assert.AreEqual("A", view.Q<Button>("marker-holdfast").text);
            Assert.IsTrue(view.Q<Label>("route-summary").text.Contains(MapScreen.Travel(minutes).ToUpperInvariant()));
            Assert.IsTrue(view.Q<Label>("route-names").text.Contains("Sheerdown"));
            for (int step = 1; step < route.Count; step++)
            {
                var marker = view.Q<Button>("marker-" + route[step]);
                Assert.AreEqual(step.ToString(), marker.text);
                Assert.IsTrue(marker.ClassListContains("world-map__marker--route"));
            }
            Assert.IsFalse(view.Select("unknown"));
            Assert.AreEqual("Sheerdown", view.Q<Label>("selected-region").text);
        }
    }
}
