using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Sim.Combat;
using Broodline.UI.Screens;
using Broodline.View;

namespace Broodline.Game
{
    /// Wires the runner, the clock, the view and the HUD for ONE played wave.
    ///
    /// It no longer decides which wave that is. `WaveHost` loads this scene
    /// additively over the shell and calls `Configure` with the wave, the
    /// deployment and the seed the player actually chose - design section 4:
    /// "Wave Defense loads additively and unloads on exit."
    ///
    /// EXCEPT ON ONE PATH, WHICH IS TRACKED AND MUST NOT BREAK. Playing
    /// `Assets/Scenes/Wave.unity` on its own is the Editor half of the replay
    /// round-trip capture (`EditorReplayTests`, Task 10 of the Phase 3 plan):
    /// wave 6 as authored with the golden-A deployment, played at a real
    /// variable frame rate with a real human tap in it, written out as
    /// `replay.bin` and `replay-outcome.txt`. `standaloneCapture` is what
    /// keeps that working, and `WaveSceneBuilder` leaves it TRUE in the scene
    /// asset - so the procedure is unchanged: open the scene, press Play.
    ///
    /// waves_01_12 section 3 designs wave 6 as a LOSS - the beat that teaches
    /// the counter system - so a Loss on that path is the wave working, not
    /// the build failing.
    public sealed class WaveRunner : MonoBehaviour
    {
        /// The tracked capture's inputs, named rather than inlined, because
        /// `EditorReplayTests.TheEditorRunIsWave6AsAuthored` asserts the
        /// RECORD carries exactly these and has no other way to know them.
        public const ulong Seed = 6UL;
        public const int CaptureWaveId = 6;

        /// The SCENE's answer to "is anything hosting this?" - true in
        /// `Wave.unity`, which `WaveSceneBuilder` writes and never flips.
        ///
        /// It gates two things, and both belong to the standalone path: this
        /// component deploying wave 6 by itself, and writing the capture
        /// artifacts. `Configure` deliberately does NOT touch it, because the
        /// standalone path configures itself through that same method - a
        /// `Configure` that cleared this would stop the artifacts being
        /// written, and nobody would see it until the next capture session.
        [SerializeField] bool standaloneCapture = true;

        /// Set by `WaveHost` around a hosted run.
        ///
        /// STATIC, AND SET BEFORE THE LOAD, because "before play" has to mean
        /// something. An additive `LoadSceneAsync` integrates the scene at the
        /// end of a frame and runs `Awake` there; by the time the host holds a
        /// reference to this component, that has already happened. A flag the
        /// host writes afterwards would be racing the very thing it is meant
        /// to gate. This one is written before the scene exists, so there is
        /// nothing to race.
        public static bool Hosted;

        /// Cleared every time the player loop starts.
        ///
        /// THIS PROJECT DISABLES DOMAIN RELOAD. `ProjectSettings/EditorSettings
        /// .asset` carries `m_EnterPlayModeOptionsEnabled: 1` and
        /// `m_EnterPlayModeOptions: 3` - `DisableDomainReload |
        /// DisableSceneReload` - so statics are NOT reset when Play Mode
        /// starts. `WaveHost.RunAsync` clears `Hosted` in a `finally`, and a
        /// `finally` does not run when a person stops Play Mode mid-wave: the
        /// domain simply keeps `Hosted == true`.
        ///
        /// What that costs is the exact failure this whole flag exists to
        /// prevent. The next time someone opens `Wave.unity` and presses Play
        /// to take the tracked capture, `StandaloneCapture` is false, `Update`
        /// returns every frame, and they watch an empty battlefield that
        /// writes nothing and reports nothing. No test can catch it either,
        /// because every test resets the flag itself.
        ///
        /// `SubsystemRegistration` is the earliest runtime hook and runs
        /// before any scene loads, which is before any `Update` could read it.
        /// `Determinism/CorpusPlayerHarness.cs` uses the same attribute.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHosted() => Hosted = false;

        /// Whether this run owns its own deployment and the capture
        /// artifacts.
        ///
        /// Two facts, ANDed, and neither is latched: `standaloneCapture` is
        /// the SCENE's answer and `Hosted` is the RUN's. Read live rather
        /// than resolved once in `Awake`, for two reasons - there is then no
        /// window between scene integration and `Configure` in which this can
        /// read wrong, and `Awake` does not run at all in an EditMode test,
        /// so a latched version of this rule would be one no test could
        /// reach. This is the gate on a procedure nobody exercises until the
        /// next capture session; it is worth being able to assert.
        public bool StandaloneCapture => standaloneCapture && !Hosted;

        SimRunner _runner;
        WaveClock _clock;
        WavePair _pair;
        WaveView _view;
        WaveHudView _hud;
        SafeAreaBinder _safeArea;
        bool _written;
        bool _inputEnabled = true;

        TaskCompletionSource<bool> _completed = new TaskCompletionSource<bool>();

        /// Allocated once, in Configure. `() => _pair.Advance(_runner)`
        /// written inline in Update is a fresh closure object every frame -
        /// about 115 KB a minute at 60fps, handed to the collector for
        /// nothing, on the path whose entire job is not to hitch.
        Action _onTick;

        /// The HUD's per-frame snapshot, reused. Same reason: this is rebuilt
        /// every frame and a fresh list per frame is the same garbage by
        /// another name.
        readonly HudSnapshot _snapshot = new HudSnapshot();
        readonly List<BodyBar> _bodies = new List<BodyBar>();

        /// Exposed so the scene builder can frame the camera on the lane
        /// without duplicating its length as a literal.
        public static int LaneTiles => Stats.LaneTiles;

        /// The engine runner for the wave in progress, so the host can build
        /// a `WaveReport` from its `Outcome` when it finishes. Null until
        /// `Configure`.
        public SimRunner Runner => _runner;

        /// Whether taps reach the simulation as Rally.
        ///
        /// Observable because the standalone capture path passes `true` and
        /// THAT LITERAL IS LOAD-BEARING: a capture with no Rally in it is an
        /// invalid artifact - `EditorReplayTests` asserts the record carries a
        /// tap at a tick index, and `DeviceReplayTests` asserts the Rally
        /// window overlaps the engagement. Flipping it to `false` breaks
        /// nothing that compiles and nothing that runs; it fails on hardware,
        /// with a person waiting.
        public bool InputEnabled => _inputEnabled;

        /// Completes when the wave terminates. Replaced by every `Configure`,
        /// so a host awaits the run it just started rather than one that
        /// finished earlier.
        public Task Completed => _completed.Task;

        /// waves_01_12 section 3's "expected roster 5, none carrying Chill":
        /// four Vetch and a Loam. Identical to `GoldenTests.Deployment
        /// WithoutChill()` in the engine suite, deliberately - the capture
        /// this produces is re-simulated against that golden.
        public static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        /// The wave to play. Called by `WaveHost` for a hosted run, and by
        /// this component itself on the standalone capture path.
        ///
        /// THROWS on a second call rather than reconfiguring. `WaveView.Build`
        /// instantiates a body per creature and per raider and owns no
        /// teardown, so a second Configure would leave the first wave's bodies
        /// in the scene beside the second's - two Arks, two lanes, and a
        /// renderer drawing from the newer runner into the older world. A
        /// hosted run gets a freshly loaded scene every time, so the legal
        /// case never needs this.
        public void Configure(WaveDef wave, CreatureSpec[] deployment, ulong seed, bool inputEnabled)
        {
            if (_runner != null)
                throw new InvalidOperationException(
                    "[WaveRunner] already configured - Wave.unity is loaded fresh per run and " +
                    "WaveView.Build cannot be applied twice to one scene.");

            _runner = new SimRunner(wave, wave.Lane, deployment, seed);
            _clock = new WaveClock();
            _pair = new WavePair(_runner);
            _onTick = () => _pair.Advance(_runner);
            _inputEnabled = inputEnabled;
            _completed = new TaskCompletionSource<bool>();

            _view = gameObject.AddComponent<WaveView>();
            _view.Build(_runner, deployment, wave);

            BuildHud(wave.Id);
        }

        /// Attaches the HUD to whichever `UIDocument` the scene carries.
        ///
        /// The document lives in the SCENE rather than being created here,
        /// because it is the scene's own presentation: `WaveSceneBuilder`
        /// adds it, with the shell's `PanelSettings` so a hosted wave draws
        /// into the same panel the rest of the app does, at a sorting order
        /// above it.
        /// `waveId` IS PASSED RATHER THAN READ FROM A FIELD, because nothing
        /// here holds the `WaveDef` after `Configure` and a second copy of it
        /// would be a second thing to keep in step with `_runner`. It is the
        /// only argument the HUD's chrome needs that its per-frame snapshot
        /// does not carry - `WaveHudView.Wave` has why it is not a snapshot
        /// field.
        void BuildHud(int waveId)
        {
            var document = GetComponentInChildren<UIDocument>();
            if (document == null)
            {
                // AN ERROR, NOT A WARNING. A wave with no HUD shows no
                // integrity, no bars and no Rally countdown - design section
                // 4 makes this "the one piece of existing presentation that a
                // tester sees" - and the capture procedure reads the live
                // tick off it. A warning would let a scene rebuilt without
                // the document play on looking merely plain, and would not
                // fail the PlayMode test that loads it.
                Debug.LogError("[WaveRunner] no UIDocument in the wave scene - " +
                               "rebuild it with Broodline > Build Wave Scene.");
                return;
            }

            // THE SAME TREATMENT `BootController` GIVES THE SHELL'S ROOT, and
            // it has to be done again here rather than inherited. The wave
            // scene's document is a SIBLING root in the shared panel, so none
            // of the shell's padding reaches it - and on the standalone
            // capture path the Boot scene is not loaded at all, so there is no
            // binder in the process to inherit from.
            //
            // The file this HUD replaced recorded the cost: the integrity
            // readout at y=110 with the Dynamic Island occupying 0..177,
            // invisible in the Editor because a notchless display has a zero
            // inset and the bug is an identity. It matters more now than it
            // did then - `DeviceReplayTests`' re-capture message tells the
            // capturer to read the live tick off this HUD to time a tap near
            // 190, so a readout under the notch is a wasted device trip.
            //
            // Re-applied on every layout change, not once: an iPad in Split
            // View resizes the window with no rotation involved. See
            // SafeAreaBinder's own header.
            var root = document.rootVisualElement;
            _safeArea = SafeAreaBinder.ForRuntimePanel(root);
            _safeArea.ApplyIfChanged();
            root.RegisterCallback<GeometryChangedEvent>(_ => _safeArea.ApplyIfChanged());

            _hud = new WaveHudView { Camera = Camera.main, Wave = waveId };
            root.Add(_hud);
            _hud.Bind(Snapshot);
            _hud.OnPause += () => { _clock.Paused = !_clock.Paused; _hud.SetPaused(_clock.Paused); };
            _hud.OnSpeed += () => { _clock.Scale = _clock.Scale >= 2.0 ? 1.0 : 2.0; _hud.SetSpeed(_clock.Scale); };
        }

        /// True on the frame a tap or click begins.
        ///
        /// The project runs the Input System package with legacy input
        /// disabled (ProjectSettings activeInputHandler: 1), so
        /// UnityEngine.Input throws rather than returning false - the failure
        /// is loud but only at runtime, which is why it survived compilation.
        ///
        /// Both devices are checked and both may be absent: Mouse.current is
        /// null on an iPhone and Touchscreen.current is null in the Editor
        /// unless simulated, so neither can be assumed. The device that
        /// matters for the done-when is the touchscreen; the mouse is what
        /// makes the Editor run usable.
        /// A press that began this frame, and where. `WaveHudView.PicksControlAt`
        /// then decides whether it was a control or a Rally - Phase 10 Task 1.7.
        static bool TapBegan(out Vector2 at)
        {
            at = default;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                at = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                at = mouse.position.ReadValue();
                return true;
            }
            return false;
        }

        void Update()
        {
            if (_runner == null)
            {
                // A hosted scene waits for its host; there is nothing here to
                // play until `Configure` arrives. The standalone capture path
                // has no host, so it deploys wave 6 itself - which is what
                // "open the scene and press Play" has always done.
                if (!StandaloneCapture) return;
                Configure(WaveDef.ForId(CaptureWaveId), Deployment(), Seed, inputEnabled: true);
            }

            // A tap anywhere Rallies creature 0. Deliberately crude: choosing
            // a creature is a deployment-UI concern and this phase has no UI
            // for it. The HUD is PickingMode.Ignore throughout precisely so
            // this still reaches here.
            if (_inputEnabled && TapBegan(out var at) && !(_hud != null && _hud.PicksControlAt(at))) _clock.RequestRally(0);

            // Time.deltaTime, NOT a fixed value. Feeding the accumulator the
            // real frame delta is the whole point - it is what makes a stall
            // on a real device produce catch-up steps rather than a slower
            // wave.
            _clock.Advance(_runner, Time.deltaTime, _onTick);
            _view.Render(_pair, _clock.Alpha);

            if (!_runner.Done) return;

            if (StandaloneCapture && !_written) WriteArtifact();
            _completed.TrySetResult(true);
        }

        /// One frame of HUD state, from the SNAPSHOT PAIR and nothing else.
        ///
        /// `WaveHud` mixed them - existence and HP from the live runner,
        /// position from the snapshot - and between tick boundaries the bar
        /// trailed the body it labelled, while on the terminating tick the two
        /// sources disagreed about whether the raider existed at all. One
        /// source per entity, interpolated by the same expression `WaveView`
        /// uses, which is literally the same method rather than a second lerp
        /// that happens to agree.
        HudSnapshot Snapshot()
        {
            _bodies.Clear();
            _snapshot.Bodies = _bodies;
            if (_runner == null) return _snapshot;

            _snapshot.Integrity = _runner.Integrity;
            _snapshot.Tick = _runner.Tick;

            var alpha = (float)_clock.Alpha;          // clamped at the clock
            var current = _pair.Current;
            var previous = _pair.Previous;

            for (var i = 0; i < current.RaiderCount; i++)
            {
                if (!current.RaiderVisible(i)) continue;
                var tile = WaveSnapshot.LerpTile(previous, current, i, alpha);
                _bodies.Add(new BodyBar
                {
                    Kind = BodyKind.Raider,
                    World = new Vector3(tile * WaveView.TileSize, 0f, 0f),
                    Hp = current.RaiderHp(i),
                    MaxHp = Stats.RaiderHp(_runner.RaiderType[i]),     // static for the wave
                    State = BodyBars.RaiderState(current.RaiderBreaching(i), current.RaiderChilled(i)),
                });
            }

            for (var c = 0; c < current.CreatureCount; c++)
            {
                if (current.CreatureHp(c) <= 0) continue;
                // The countdown is read from the engine rather than
                // subtracted here - the view renders and never derives.
                var rally = _runner.CreatureRallyRemaining(c);
                _bodies.Add(new BodyBar
                {
                    Kind = BodyKind.Creature,
                    World = new Vector3(current.CreatureTile(c) * WaveView.TileSize, 0f, WaveView.PocketOffset),
                    Hp = current.CreatureHp(c),
                    MaxHp = Stats.CreatureHp(_runner.CreatureSpecies[c]),   // static for the wave
                    State = BodyBars.CreatureState(rally),
                    RallyRemaining = rally,
                });
            }

            return _snapshot;
        }

        void WriteArtifact()
        {
            _written = true;

            // Application.persistentDataPath is the app's Documents directory
            // on iOS, which is what UIFileSharingEnabled exposes to devicectl.
            // The engine does no file I/O; it hands back bytes and this writes
            // them. Same split Phase 0's CorpusPlayerHarness uses.
            var dir = Application.persistentDataPath;
            File.WriteAllBytes(Path.Combine(dir, "replay.bin"), _runner.SerializeRecord());
            File.WriteAllText(Path.Combine(dir, "replay-outcome.txt"),
                _runner.Outcome.Hash + "\n" +
                (int)_runner.Outcome.Result + "\n" +
                _runner.Outcome.Ticks + "\n" +
                _runner.Outcome.IntegrityRemaining + "\n");

            Debug.Log("[broodline] wrote replay.bin to " + dir +
                      "  hash=" + _runner.Outcome.Hash +
                      "  result=" + _runner.Outcome.Result +
                      "  ticks=" + _runner.Outcome.Ticks);
        }
    }
}
