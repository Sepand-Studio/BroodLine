using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Model;
using Broodline.Model.Catalogs;
using Broodline.Model.Stub;
using Broodline.UI;
using Broodline.UI.Screens;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// THE TABS GO SOMEWHERE - Phase 10 Task 1.3. `BootController.OnTabSelected`
    /// was empty from Phase 7 to Phase 9 ("the largest limitation of this
    /// build"); this is what it calls now. One destination per
    /// `Progression.Order` entry: Map (the region list, live region marked),
    /// Ark (the home base), Splice (the roster in pick-parents mode, through
    /// the director's splice flow), Lab and Allies (stub-backed previews).
    /// The Store is not a tab (screen inventory §1) and is pushed from the
    /// shard pill and the home card.
    ///
    /// The router owns no state the server owns: balances stay on the
    /// snapshot, the region comes from `region/state`, and only the stub
    /// ledger's previews are written here.
    public sealed class HubRouter
    {
        readonly ScreenHost _host;
        readonly Func<PlayerSnapshot> _snapshot;
        readonly Func<Task<RegionStateResponse>> _regionState;
        readonly Func<int, Task<string>> _claim;
        readonly StubLedger _ledger;
        readonly Action<string> _notice;
        readonly Action<string> _setActiveTab;
        readonly HomeStage _home;
        readonly Func<DateTime> _now;

        string _regionId;

        /// Hooks the composition root fills in, because they run flows that
        /// live in the director (fights, splices) or need the sheet layer.
        public Func<Task> Defend;
        public Func<Task> SpliceFromRoster;
        public Action OpenCodex;

        public string Active { get; private set; }

        public HubRouter(
            ScreenHost host,
            Func<PlayerSnapshot> snapshot,
            Func<Task<RegionStateResponse>> regionState,
            Func<int, Task<string>> claim,
            StubLedger ledger,
            Action<string> notice,
            Action<string> setActiveTab,
            HomeStage home = null,
            Func<DateTime> now = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _regionState = regionState ?? throw new ArgumentNullException(nameof(regionState));
            _claim = claim;
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _notice = notice ?? (_ => { });
            _setActiveTab = setActiveTab ?? (_ => { });
            _home = home;
            _now = now ?? (() => DateTime.UtcNow);
        }

        /// The hub, as the director sees it: land on the Ark and then never
        /// return, because from here on the tabs drive the screen host.
        public async Task RunAsync()
        {
            await ShowAsync("Ark");
            await new TaskCompletionSource<bool>().Task;
        }

        public async Task ShowAsync(string tab)
        {
            Active = tab;
            _setActiveTab(tab);
            switch (tab)
            {
                case "Map": await ShowMapAsync(); break;
                case "Ark": ShowHome(); break;
                case "Splice":
                    if (SpliceFromRoster != null) await SpliceFromRoster();
                    else ShowHome();
                    // The flow returns when it is done or backed out of; the
                    // tab's own destination is then the roster's home.
                    if (Active == "Splice") ShowHome();
                    break;
                case "Lab": ShowLab(); break;
                case "Allies": ShowAllies(); break;
                default: ShowHome(); break;
            }
        }

        // ---------------------------------------------------------- Map

        async Task ShowMapAsync()
        {
            RegionStateResponse state = null;
            try { state = await _regionState(); }
            catch (Exception error) { Diagnostics.Defect("region/state did not arrive", error); }
            _regionId = state?.RegionId ?? _regionId;

            var view = new WorldMapView();
            view.Bind(MapScreen.Build(_regionId), onPick: id => _ = ShowRegionAsync(id, state));
            _host.Show(view);
        }

        async Task ShowRegionAsync(string id, RegionStateResponse state)
        {
            var here = RegionCatalog.Locate(_regionId);
            if (id != here.Id)
            {
                _notice(MapScreen.NotHere(RegionCatalog.Find(id)?.Name ?? id));
                return;
            }
            if (state == null)
            {
                try { state = await _regionState(); }
                catch (Exception error) { Diagnostics.Defect("region/state did not arrive", error); _notice(ServerError.UnexpectedProblem); return; }
            }
            var view = new RegionView();
            view.Bind(RegionScreen.Build(state), onClaim: slot => _ = ClaimAsync(slot));
            _host.Show(view);
        }

        async Task ClaimAsync(int slot)
        {
            if (_claim == null) return;
            var failure = await _claim(slot);
            if (failure != null) { _notice(failure); return; }
            await ShowMapAsync();
        }

        // ---------------------------------------------------------- Ark

        void ShowHome()
        {
            var view = new HomeBaseView();
            var model = new HomeScreenModel
            {
                RegionName = RegionCatalog.Locate(_regionId).Name,
                CoreTier = _ledger.FacilityTier(FacilityCatalog.CoreId),
                Hotspots = Hotspots(),
            };
            view.Bind(model,
                onDefend: () => { if (Defend != null) _ = Defend(); },
                onRoster: () => { if (SpliceFromRoster != null) _ = ShowAsync("Splice"); },
                onCodex: () => OpenCodex?.Invoke(),
                onStore: ShowStore,
                onPlot: plot => { _ = ShowAsync("Lab"); });
            if (_home != null) view.SetStage(_home.Show());
            _host.Show(view);
        }

        IReadOnlyList<HomeHotspot> Hotspots()
        {
            var list = new List<HomeHotspot>();
            var anchors = _home != null ? _home.PlotAnchors() : DefaultAnchors;
            foreach (var (id, x, y) in anchors)
            {
                var facility = FacilityCatalog.Find(id);
                if (facility == null) continue;
                list.Add(new HomeHotspot { Id = id, Label = facility.Name, Tier = _ledger.FacilityTier(id), X01 = x, Y01 = y });
            }
            return list;
        }

        /// Where the markers sit when there is no stage (tests, fixtures):
        /// the same layout the camera produces, by eye.
        public static readonly IReadOnlyList<(string Id, float X01, float Y01)> DefaultAnchors = new[]
        {
            ("core", .5f, .47f), ("splicing", .3f, .36f), ("hatchery", .72f, .4f),
            ("vault", .24f, .58f), ("harvest", .78f, .62f), ("drive", .5f, .74f),
        };

        // ---------------------------------------------------------- Store

        public void ShowStore()
        {
            var view = new StoreView();
            view.Bind(new StoreScreenModel { GiftAvailable = _ledger.DailyGiftAvailable() },
                onGift: () => { _ledger.ClaimDailyGift(); _notice(StoreScreen.GiftNotice); ShowStore(); },
                onBuy: id => { _ledger.RecordPreviewPurchase(id); _notice(StoreScreen.PreviewNotice); },
                onBack: () => _host.Pop());
            _host.Push(view);
        }

        // ---------------------------------------------------------- Lab

        void ShowLab()
        {
            var view = new LabView();
            view.Bind(LabScreen.Build(_ledger, _now()), onUpgrade: id =>
            {
                var tier = _ledger.FacilityTier(id);
                _ledger.RememberFacility(id);
                _ledger.StartUpgrade(id, FacilityCatalog.UpgradeTime(id, tier));
                _notice(LabScreen.PreviewNotice);
                ShowLab();
            });
            _host.Show(view);
        }

        // ---------------------------------------------------------- Allies

        void ShowAllies()
        {
            var view = new AlliesView();
            view.Bind(_ledger.AllianceName(), onCreate: () =>
            {
                _ledger.CreateAlliance(AlliesScreen.DefaultName);
                _notice(AlliesScreen.PreviewNotice);
                ShowAllies();
            });
            _host.Show(view);
        }
    }
}
