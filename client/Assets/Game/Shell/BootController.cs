using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Broodline.Model;
using Broodline.Net;
using Broodline.UI.Components;
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
        /// The shell's safe-area owners. Nothing READS this - the binders stay
        /// reachable through the GeometryChangedEvent closure BindSafeAreas
        /// registers on the root - and it is here so a reader can see at the
        /// field list that the shell has owners at all, and how many.
        IReadOnlyList<SafeAreaBinder> _safeAreas;
        OutboxClient _outbox;
        OutboxPump _pump;
        WaveHost _waves;
        PortraitStudio _studio;
        LaneStage _stage;
        FtueDirector _ftue;
        NoticeToast _toast;

        /// Gives the panel root AND the notice toast the safe-area inset, and
        /// hands back the binders so nothing re-derives them.
        ///
        /// TWO BINDERS, AND THE SECOND ONE IS THE WHOLE OF PHASE 9 TASK 21h's
        /// D2. On an iPhone 17 the notice toast drew with its first line behind
        /// the Dynamic Island: "did not finish within" was cut through by the
        /// black pill. One binder on the panel root is not enough, and the
        /// reason is a layout rule rather than a missing call.
        ///
        /// `#notice-layer` is a SIBLING of `#shell-root` (deliberately - Task
        /// 6 hides the shell while a wave is resident and a notice must still
        /// reach the player) and Shell.uss gives it `position: absolute` with
        /// `top: 0`. UI Toolkit offsets an absolutely positioned child from its
        /// parent's BORDER box, not its padding box, so the inset this method
        /// puts on the panel root reaches `#shell-root`, which is an in-flow
        /// child, and does not reach the notice layer or anything inside it.
        /// `.notice-toast` is itself `position: absolute` with `top:
        /// var(--space-6)`, so padding on the LAYER would not have reached it
        /// either - the padding has to go on the toast, whose own rows are
        /// in-flow children of it. THE DEVICE IS WHAT ESTABLISHED THIS: every
        /// other screen cleared the inset on the same frame the toast did not,
        /// which is only true if the root's padding is being applied and is not
        /// reaching the absolutely positioned layer.
        ///
        /// Applied now (in case a panel is already live) and re-applied on
        /// every layout change of the root - client_architecture section 10's
        /// "size- and aspect-tolerant by construction" needs the second half
        /// too: an iPad in Split View or Slide Over resizes the window with no
        /// rotation involved, so a one-shot apply at Start goes stale the first
        /// time that happens. See SafeAreaBinder.
        ///
        /// STATIC, PUBLIC AND SEAMED FOR `FtueDirector.StartLeavesTheWalkAlive`'s
        /// REASON: `Start()` is `async void` and nothing in EditMode can drive
        /// it, so a decision left inside it is a decision that is read and
        /// never executed by a test - on the startup path, against the user's
        /// standing rule. `bind` is what makes it drivable, the same
        /// arrangement `BroodlineClient.ColdStartAsync` uses for its
        /// `onRetry`/`wait`; production leaves it defaulted.
        ///
        /// THE TOAST'S BOTTOM INSET IS INERT AND THAT IS ACCEPTED.
        /// `SafeAreaBinder` sets paddingTop and paddingBottom together, so the
        /// toast becomes taller than its rows by the bottom inset. It is
        /// anchored by `top` alone, and the toast, its row container and the
        /// whole layer are all `PickingMode.Ignore`, so the extra box neither
        /// moves anything nor swallows a tap. Splitting the binder in two to
        /// avoid it would be a second safe-area mechanism for one element.
        public static IReadOnlyList<SafeAreaBinder> BindSafeAreas(
            VisualElement root, VisualElement toast, Func<VisualElement, SafeAreaBinder> bind = null)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (toast == null) throw new ArgumentNullException(nameof(toast));
            if (bind == null) bind = SafeAreaBinder.ForRuntimePanel;

            var bound = new List<SafeAreaBinder> { bind(root), bind(toast) };
            Action apply = () =>
            {
                for (var i = 0; i < bound.Count; i++) bound[i].ApplyIfChanged();
            };

            apply();
            root.RegisterCallback<GeometryChangedEvent>(_ => apply());
            return bound;
        }

        async void Start()
        {
            var document = GetComponent<UIDocument>();
            var root = document.rootVisualElement;

            var tabBarSlot = root.Q<VisualElement>("tab-bar");
            _tabBar = new TabBar();
            tabBarSlot.Add(_tabBar);

            var noticeLayer = root.Q<VisualElement>("notice-layer");
            _toast = new NoticeToast();
            noticeLayer.Add(_toast);

            // AFTER THE TOAST EXISTS, BECAUSE THE TOAST IS ONE OF THE TWO
            // THINGS THAT NEEDS THE INSET. See BindSafeAreas.
            _safeAreas = BindSafeAreas(root, _toast);

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
                // THE NOTICE IS WHY THIS PASSES A CALLBACK AT ALL. A packaged
                // player's first launch was measured timing out on this call
                // against a cold Cloud Run service, and `Retry` now spends up
                // to eleven seconds getting past that - eleven seconds in
                // which a tester on a fresh install has a blank shell and no
                // reason to believe anything is happening. One row, on the
                // first failed attempt, for the reason `FightAsync` gives.
                await _session.ColdStartAsync(
                    onRetry: (attempt, _) =>
                    {
                        if (attempt == 1) OnNotice(FtueNotice.ServerWakingUp);
                    });
            }
            catch (Exception e)
            {
                // This keeps a failed cold start from vanishing as an
                // unobserved exception out of this async void Start.
                //
                // AND IT IS SAID ON THE SCREEN, not only in a log nobody on a
                // device can read. The walk below reaches
                // `FtueNotice.ColdStartEmpty` when the snapshot is null - the
                // same event, said a second time on a screen with a button -
                // but only if the director gets that far, and a log line is
                // not something a tester can report.
                //
                // "NO ERROR SCREEN EXISTS YET" OPENED THIS COMMENT UNTIL
                // PHASE 9 TASK 21g. One does now (`InterruptedView`), and it
                // is deliberately NOT shown from here.
                //
                // NOT BECAUSE THE SHELL IS UNBUILT - AN EARLIER DRAFT OF THIS
                // PARAGRAPH SAID THAT AND IT IS FALSE. `_toast`, `_tabBar`,
                // `_screenHost` and `_screenFlow` are all constructed above,
                // so the screen machinery is ready right here. What is
                // unbuilt is the outbox, the pump, `WaveHost`, the studio,
                // the stage and the DIRECTOR.
                //
                // AND THE DIRECTOR IS THE REASON. `ScreenFlow.ShowAsync`
                // completes only when the bound `resume` fires, so showing
                // that screen here would park this `async void Start` on a
                // turn whose button has nothing to resume - the walk it would
                // be offering to retry does not exist yet. The walk below
                // puts the same screen up on this same failure a few lines
                // later, with something behind the button.
                //
                // THE TOAST BELOW IS DELIBERATELY UNGUARDED, unlike the one
                // in the second catch, and that asymmetry is not an
                // oversight to tidy up. That one is reached only when the
                // screen machinery itself has thrown, so asking it for a
                // toast can throw again; this one is reached when the NETWORK
                // failed, with `_toast` already built above and nothing
                // having touched it since.
                Debug.LogError("[BootController] cold start failed: " + e);
                OnNotice(FtueNotice.ColdStartFailed);
            }

            // The outbox, and the pump that drains it. `OutboxStore`'s path
            // is under `Application.persistentDataPath` because the queue
            // must survive a kill - that is the whole point of persisting
            // the key at action time (`OutboxClient`'s class comment).
            var store = new OutboxStore(Path.Combine(Application.persistentDataPath, "outbox.bin"));
            _outbox = new OutboxClient(_session.Api, store.Load(), store);
            _pump = gameObject.AddComponent<OutboxPump>();
            _pump.Configure(_outbox);
            _pump.OnNotice = OnNotice;

            // `traits` is read per run, never captured - `WaveHost`'s own
            // rule, because the snapshot it comes from is replaced wholesale
            // by every sync.
            var shellRoot = root.Q<VisualElement>("shell-root");
            _waves = new WaveHost(
                () => _session.Snapshot?.Traits,
                visible => shellRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None);

            // Task 11. Created before the director so it is already alive
            // for the three hero moments the director shows it around
            // (Tasks 14 and 16); the camera costs nothing until then - see
            // PortraitStudio.Create.
            _studio = PortraitStudio.Create(transform);

            // Task 17, and created here for Task 11's reason: the deploy
            // screen asks for a lane the moment the first beat reaches it,
            // and building the dressing then would allocate four trees, an
            // Ark and twenty-four dashes inside a `Bind`. Both rigs sit far
            // below the origin on the Studio layer and both keep their
            // cameras disabled until shown - see LaneStage.Create.
            _stage = LaneStage.Create(transform);

            _ftue = new FtueDirector(
                _session.Api,
                _outbox,
                _waves.RunAsync,
                _screenFlow,
                () => _session.Snapshot,
                () => _session.ColdStartAsync(),
                OnNotice,
                _studio,
                _stage);

            try
            {
                await _ftue.RunAsync();
            }
            catch (Exception e)
            {
                // Same reason the cold start is wrapped: this is an `async
                // void Start`, so anything that escapes here is an
                // unobserved exception with no stack anyone will see.
                //
                // WHAT REACHES THIS CATCH CHANGED IN PHASE 9 TASK 21G, and
                // what it can do about it did not. `RunAsync` now catches its
                // own throws and answers them with a screen carrying a live
                // control, so the only thing that still lands here is a throw
                // out of THAT - the recovery itself failing. At which point
                // there is no live control to offer: the mechanism that shows
                // screens is what just failed, and a second attempt at it
                // would be the same call.
                //
                // SO IT SAYS SOMETHING RATHER THAN ONLY LOGGING. A toast is
                // four seconds and a relaunch is genuinely the remedy, which
                // is what the sentence names. `Debug.LogError` alone is what
                // a tester holding a device cannot read, and that gap is the
                // one Task 4 closed everywhere else.
                //
                // THE TOAST IS GUARDED, AND THE REASON IS THE ONLY ROUTE THAT
                // GETS HERE. That route is a throw out of the recovery screen
                // - the screen machinery failing - and `OnNotice` turns round
                // and asks the same machinery for a toast. Unguarded, a
                // second throw here is precisely the unobserved exception out
                // of an `async void Start` that this catch exists to prevent,
                // and it would take the log line with it. The log runs FIRST
                // so the developer keeps the stack either way.
                Debug.LogError("[BootController] the first hour stopped: " + e);
                try
                {
                    OnNotice(FtueNotice.WalkUnrecoverable);
                }
                catch (Exception unsayable)
                {
                    Debug.LogError("[BootController] and it could not be said: " + unsayable);
                }
            }
        }

        /// Where a blocked beat's sentence goes: the toast, and the log so a
        /// capture still carries it. Phase 9 Task 4 closed the gap Phase 7
        /// Task 13 recorded here.
        void OnNotice(string notice)
        {
            if (string.IsNullOrEmpty(notice)) return;
            Debug.LogWarning("[Ftue] " + notice);
            _toast?.Show(notice);
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
        /// `RosterView` and `RegionView` are never built outside tests.
        ///
        /// THE SENTENCE THAT USED TO FOLLOW - "after `Beat.Done` the walk
        /// ends with no screen taking the shell" - WAS TRUE AND IS NOT ANY
        /// MORE, Phase 9 Task 21g. The walk cannot end without putting a
        /// screen up with a live control on it; `FtueDirector.RunAsync` has
        /// the whole reasoning. What that does NOT do is give the tabs
        /// anywhere to go, so this method is still empty and still the
        /// largest limitation of this build.
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
