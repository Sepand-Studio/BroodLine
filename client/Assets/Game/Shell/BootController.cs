using System;
using System.Collections.Generic;
using System.Net.Http;
using Broodline.Model;
using Broodline.UI.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.Game.Shell
{
    /// The Boot scene's single entry point. `client_architecture` section 9:
    /// one persistent root scene holding the composition root, the network
    /// layer, the tab bar and the persistent top bar.
    ///
    /// What this does NOT yet do: task-13-brief.md's Step 6 sketch says this
    /// class "starts the OutboxPump (Task 18)" and "hands control to Ftue
    /// (Task 17) after ColdStart." Neither exists in this codebase - there is
    /// no `OutboxPump` class and no FTUE controller anywhere under
    /// `client/Assets` (Task 13 is the first client task of this phase).
    /// Wiring against a type that does not exist would not compile, so both
    /// are left as explicit TODOs rather than invented placeholders.
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
        TabBar _tabBar;

        async void Start()
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;

            ApplySafeArea(root);

            var tabBarSlot = root.Q<VisualElement>("tab-bar");
            _tabBar = new TabBar();
            tabBarSlot.Add(_tabBar);

            var screenHostElement = root.Q<VisualElement>("screen-host");
            var sheetLayer = root.Q<VisualElement>("sheet-layer");
            _screenHost = new ScreenHost(screenHostElement, sheetLayer, _tabBar);

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

            // TODO(Task 18): start the OutboxPump here once it exists.
            // TODO(Task 17): hand control to Ftue here once it exists.
        }

        void OnSnapshot(PlayerSnapshot snapshot)
        {
            var thresholds = snapshot.Tabs ?? new Dictionary<string, int>();
            var tabs = Progression.TabsFor(snapshot.HighestWaveCleared, thresholds);
            var active = tabs.Count > 0 ? tabs[0] : null;
            _tabBar.Render(tabs, active, OnTabSelected);
        }

        void OnTabSelected(string tab)
        {
            // No screens exist yet for any tab (they arrive in later tasks).
            // The shell's job here is only to prove the tab bar is wired to
            // real progression data, not to render a destination per tab.
        }

        static string LoadApiBaseUrl()
        {
            var asset = Resources.Load<TextAsset>("BroodlineConfig");
            if (asset == null) return null;
            var config = JsonUtility.FromJson<ClientConfig>(asset.text);
            return config?.apiBaseUrl;
        }

        /// client_architecture section 10: safe-area driven, no fixed pixel
        /// positions. `RuntimePanelUtils.ScreenToPanel` converts a
        /// screen-space point into the panel's own coordinate space, which is
        /// what actually varies under `PanelScaleMode.ScaleWithScreenSize` -
        /// so the inset is computed per device rather than assumed.
        static void ApplySafeArea(VisualElement root)
        {
            var panel = root.panel;
            if (panel == null) return; // not yet attached; the USS default (0) stands

            var safeArea = Screen.safeArea;
            float topInsetScreen = Screen.height - safeArea.yMax;
            float bottomInsetScreen = safeArea.yMin;

            float PanelY(float screenY) => RuntimePanelUtils.ScreenToPanel(panel, new Vector2(0f, screenY)).y;

            root.style.paddingTop = Mathf.Abs(PanelY(topInsetScreen) - PanelY(0f));
            root.style.paddingBottom = Mathf.Abs(PanelY(Screen.height) - PanelY(Screen.height - bottomInsetScreen));
        }
    }
}
