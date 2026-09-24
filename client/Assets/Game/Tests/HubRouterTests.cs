using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Game.Shell;
using Broodline.Model.Stub;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using Broodline.UI.Shell;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// Phase 10 Task 1.3: every tab has a destination, and it is a scaffolded
    /// screen in the host, with the tab bar told which one is active.
    public sealed class HubRouterTests
    {
        static (HubRouter router, VisualElement hostElement, ScreenHost host, System.Collections.Generic.List<string> active) Make(RegionStateResponse state)
        {
            var hostElement = new VisualElement();
            var host = new ScreenHost(hostElement, new VisualElement(), new TabBar());
            var active = new System.Collections.Generic.List<string>();
            var router = new HubRouter(
                host,
                () => null,
                () => Task.FromResult(state),
                claim: null,
                ledger: new StubLedger(new StubLedger.MemoryStore()),
                notice: _ => { },
                setActiveTab: active.Add);
            return (router, hostElement, host, active);
        }

        static RegionStateResponse State()
        {
            var state = new RegionStateResponse { RegionId = "verdant-shelf", Epoch = 1, Roster = new Roster { Count = 1, Cap = 20 } };
            state.Nodes.Add(new Nodes { Slot = 1, Type = "Shard", Accrued = 3, Remaining = 2, Grants = 0 });
            return state;
        }

        [Test]
        public async Task EveryTabShowsItsOwnScaffoldedScreen()
        {
            var (router, hostElement, _, active) = Make(State());

            await router.ShowAsync("Map");
            Assert.IsInstanceOf<WorldMapView>(hostElement.ElementAt(0));
            Assert.IsNotNull(hostElement.Q<ScreenScaffold>());

            await router.ShowAsync("Ark");
            Assert.IsInstanceOf<HomeBaseView>(hostElement.ElementAt(0));
            Assert.IsNotNull(hostElement.Q("plot-core"), "the six facilities are markers on the base");

            await router.ShowAsync("Lab");
            Assert.IsInstanceOf<LabView>(hostElement.ElementAt(0));
            Assert.IsNotNull(hostElement.Q("facility-core"));
            Assert.IsNotNull(hostElement.Q<Button>("lab-plot-core"), "the Lab uses the Ark's plot positions");

            await router.ShowAsync("Allies");
            Assert.IsInstanceOf<AlliesView>(hostElement.ElementAt(0));
            Assert.IsNotNull(hostElement.Q<Button>("create"));

            CollectionAssert.AreEqual(new[] { "Map", "Ark", "Lab", "Allies" }, active);
            Assert.AreEqual("Allies", router.Active);
        }

        [Test]
        public async Task TheStoreIsPushedOverTheCurrentScreenAndPopsBack()
        {
            var (router, hostElement, host, _) = Make(State());
            await router.ShowAsync("Ark");
            router.ShowStore();
            Assert.IsInstanceOf<StoreView>(hostElement.ElementAt(0));
            Assert.IsNotNull(hostElement.Q<Button>("gift"), "the free daily gift is first");
            host.Pop();
            Assert.IsInstanceOf<HomeBaseView>(hostElement.ElementAt(0));
        }

        [Test]
        public async Task TheMapLabelsHoldfastAsAPreviewOriginForTheUnmappedServerRegion()
        {
            var (router, hostElement, _, _) = Make(State());
            await router.ShowAsync("Map");
            var here = hostElement.Q<OptionRow>("region-holdfast");
            Assert.IsNotNull(here);
            Assert.IsTrue(here.Selected, "Holdfast is the authored preview origin until the API names the graph");
            Assert.IsFalse(hostElement.Q<OptionRow>("region-tellin").Selected);
            Assert.AreEqual("P", hostElement.Q<Button>("marker-holdfast").text);
            StringAssert.Contains("PREVIEW ORIGIN", hostElement.Q<Label>("ark-region").text);
        }

        [Test]
        public async Task TheArkNamesTheServerRegionWithoutBorrowingAnAuthoredOne()
        {
            var (router, hostElement, _, _) = Make(State());
            await router.ShowAsync("Map");
            await router.ShowAsync("Ark");

            StringAssert.Contains("Verdant Shelf", hostElement.Q<Label>("ark-subtitle").text);
            StringAssert.DoesNotContain("Holdfast", hostElement.Q<Label>("ark-subtitle").text);
        }

        [Test]
        public async Task ARegionStateFailureStillShowsTheMap()
        {
            var hostElement = new VisualElement();
            var host = new ScreenHost(hostElement, new VisualElement(), new TabBar());
            var router = new HubRouter(host, () => null,
                () => Task.FromException<RegionStateResponse>(new System.Exception("down")),
                null, new StubLedger(new StubLedger.MemoryStore()), _ => { }, _ => { });
            await router.ShowAsync("Map");
            Assert.IsInstanceOf<WorldMapView>(hostElement.ElementAt(0));
        }
    }
}
