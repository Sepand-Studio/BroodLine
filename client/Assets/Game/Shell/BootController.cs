using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Broodline.Model;
using Broodline.Net;
using Broodline.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// The Boot scene's single entry point. `client_architecture` section 9:
    /// one persistent root scene holding the composition root, the network
    /// layer, the tab bar and the persistent top bar.
    ///
    /// task-13-brief.md's Step 6 sketch says this class "starts the
    /// OutboxPump (Task 18)" and "hands control to Ftue (Task 17) after
    /// ColdStart". Task 13 left both as TODOs because neither type existed
    /// yet. **Both exist now and both are wired here** - `OutboxPump`
    /// arrived with Task 18 and was never constructed by anything until
    /// this commit, so until now the outbox only ever flushed as a side
    /// effect of a caller's own `SubmitWaveAsync`/`SpliceCommitAsync` and a
    /// queued entry sat there until the next action of the same kind.
    ///
    /// COMPOSITION ORDER MATTERS AND IS NOT ALPHABETICAL. The pump is
    /// configured BEFORE the director runs, so a submission the director
    /// queues while offline is already covered by the foreground and
    /// reachability triggers rather than waiting for the next wave.
    [RequireComponent(typeof(UIDocument))]
    public sealed class BootController : MonoBehaviour
    {
        [Serializable]
        class ClientConfig
        {
            public string apiBaseUrl;
        }

        Session _session;
        ScreenHost _screenHost;
        ScreenFlow _screenFlow;
        TabBar _tabBar;
        SafeAreaBinder _safeArea;
        OutboxClient _outbox;
        OutboxPump _pump;
        WaveHost _waves;
        FtueDirector _ftue;

        async void Start()
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;

            // Applied now (in case a panel is already live) and re-applied on
            // every layout change - client_architecture section 10's
            // "size- and aspect-tolerant by construction" needs the second
            // half too: an iPad in Split View or Slide Over resizes the
            // window with no rotation involved, so a one-shot apply at Start
            // goes stale the first time that happens. See SafeAreaBinder.
            _safeArea = SafeAreaBinder.ForRuntimePanel(root);
            _safeArea.ApplyIfChanged();
            root.RegisterCallback<GeometryChangedEvent>(_ => _safeArea.ApplyIfChanged());

            var tabBarSlot = root.Q<VisualElement>("tab-bar");
            _tabBar = new TabBar();
            tabBarSlot.Add(_tabBar);

            var screenHostElement = root.Q<VisualElement>("screen-host");
            var sheetLayer = root.Q<VisualElement>("sheet-layer");
            _screenHost = new ScreenHost(screenHostElement, sheetLayer, _tabBar);
            _screenFlow = new ScreenFlow(_screenHost);

            var http = new HttpClient();
            _session = new Session(http, new SnapshotStore(), new AuthStore(), OnSnapshot);
            var apiBaseUrl = LoadApiBaseUrl();
            if (!string.IsNullOrEmpty(apiBaseUrl)) _session.Api.BaseUrl = apiBaseUrl;

            try
            {
                await _session.ColdStartAsync();
            }
            catch (Exception e)
            {
                // No error screen exists yet - that is later-task work. This
                // keeps a failed cold start from vanishing as an unobserved
                // exception out of this async void Start.
                Debug.LogError("[BootController] cold start failed: " + e);
            }

            // The outbox, and the pump that drains it. `OutboxStore`'s path
            // is under `Application.persistentDataPath` because the queue
            // must survive a kill - that is the whole point of persisting
            // the key at action time (`OutboxClient`'s class comment).
            var store = new OutboxStore(Path.Combine(Application.persistentDataPath, "outbox.bin"));
            _outbox = new OutboxClient(_session.Api, store.Load(), store);
            _pump = gameObject.AddComponent<OutboxPump>();
            _pump.Configure(_outbox);

            // `traits` is read per run, never captured - `WaveHost`'s own
            // rule, because the snapshot it comes from is replaced wholesale
            // by every sync.
            _waves = new WaveHost(() => _session.Snapshot?.Traits);

            _ftue = new FtueDirector(
                _session.Api,
                _outbox,
                _waves.RunAsync,
                _screenFlow,
                () => _session.Snapshot,
                () => _session.ColdStartAsync(),
                OnNotice);

            try
            {
                await _ftue.RunAsync();
            }
            catch (Exception e)
            {
                // Same reason the cold start is wrapped: this is an `async
                // void Start`, so anything that escapes here is an
                // unobserved exception with no stack anyone will see.
                Debug.LogError("[BootController] the first hour stopped: " + e);
            }
        }

        /// Where a blocked beat's sentence goes.
        ///
        /// THERE IS STILL NO NOTICE SURFACE. Task 13 recorded the same gap
        /// for the cold-start failure above, and `OutboxPump.Notices` holds
        /// the outbox's expiry notices in a list nothing renders. Logging is
        /// not a substitute for a toast; it is what keeps the sentence from
        /// being silently discarded until one exists, and it is named as a
        /// gap here rather than hidden behind a comment-free `Debug.Log`.
        void OnNotice(string notice)
        {
            if (string.IsNullOrEmpty(notice)) return;
            Debug.LogWarning("[Ftue] " + notice);
        }

        void OnSnapshot(PlayerSnapshot snapshot)
        {
            var thresholds = snapshot.Tabs ?? new Dictionary<string, int>();
            var tabs = Progression.TabsFor(snapshot.HighestWaveCleared, thresholds);
            var active = tabs.Count > 0 ? tabs[0] : null;
            _tabBar.Render(tabs, active, OnTabSelected);
        }

        /// DELIBERATELY EMPTY, and it is the largest limitation of this build.
        ///
        /// PHASE 7 SHIPS FTUE-ONLY NAVIGATION. The tab bar above renders real
        /// progression data and every tab is tappable; a tap does nothing,
        /// because nothing routes a tab to a screen. `FtueDirector` is the
        /// ONLY production file in the client that constructs a screen, so
        /// `RosterView` and `RegionView` are never built outside tests, and
        /// after `Beat.Done` the walk ends with no screen taking the shell.
        ///
        /// An earlier version of this comment read "no screens exist yet for
        /// any tab (they arrive in later tasks)". They arrived, in Tasks
        /// 15-17; the comment did not notice. Wiring the tabs is new feature
        /// work and is named as inherited in
        /// `implementation/2026-09-15-phase7-followups.md`, section 13.
        void OnTabSelected(string tab)
        {
        }

        static string LoadApiBaseUrl()
        {
            var asset = Resources.Load<TextAsset>("BroodlineConfig");
            if (asset == null) return null;
            var config = JsonUtility.FromJson<ClientConfig>(asset.text);
            return config?.apiBaseUrl;
        }
    }
}
