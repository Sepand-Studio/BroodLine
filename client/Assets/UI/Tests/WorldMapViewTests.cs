using System.Linq;
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
            Assert.AreEqual(30, view.Query(className: WorldMapView.RowUssClassName).ToList().Count);
        }
    }
}
