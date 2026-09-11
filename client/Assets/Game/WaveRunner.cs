using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.Game
{
    /// Wires the runner, the clock, the view and the HUD, and writes the replay
    /// artifact when the wave ends.
    ///
    /// Wave 6 as authored, with the golden-A deployment: four Vetch and a Loam,
    /// none carrying Chill. waves_01_12 section 3 designs this as a LOSS - the
    /// beat that teaches the counter system - so a Loss here is the wave
    /// working, not the build failing.
    ///
    /// Deployment is programmatic. Phase 3 captures one input, Rally, because
    /// combat_engine section 8 makes it the only player input during a wave.
    public sealed class WaveRunner : MonoBehaviour
    {
        public const ulong Seed = 6UL;

        private SimRunner _runner;
        private WaveClock _clock;
        private WavePair _pair;
        private WaveView _view;
        private WaveHud _hud;
        private bool _written;

        /// Exposed so the scene builder can frame the camera on the lane
        /// without duplicating its length as a literal.
        public static int LaneTiles => Stats.LaneTiles;

        public static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        private void Start()
        {
            _runner = new SimRunner(WaveDef.Wave6(), Lane.Defile(), Deployment(), Seed);
            _clock = new WaveClock();
            _pair = new WavePair(_runner);

            _view = gameObject.AddComponent<WaveView>();
            _view.Build(_runner);

            _hud = gameObject.AddComponent<WaveHud>();
            _hud.Runner = _runner;
            _hud.Clock = _clock;
            _hud.Pair = _pair;
            _hud.View = Camera.main;
        }

        /// True on the frame a tap or click begins.
        ///
        /// The project runs the Input System package with legacy input disabled
        /// (ProjectSettings activeInputHandler: 1), so UnityEngine.Input throws
        /// rather than returning false - the failure is loud but only at
        /// runtime, which is why it survived compilation.
        ///
        /// Both devices are checked and both may be absent: Mouse.current is
        /// null on an iPhone and Touchscreen.current is null in the Editor
        /// unless simulated, so neither can be assumed. The device that matters
        /// for the done-when is the touchscreen; the mouse is what makes the
        /// Editor run usable.
        private static bool TapBegan()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

            return false;
        }

        private void Update()
        {
            // A tap anywhere Rallies creature 0. Deliberately crude: choosing a
            // creature is a deployment-UI concern and this phase has no UI.
            if (TapBegan()) _clock.RequestRally(0);

            // Time.deltaTime, NOT a fixed value. Feeding the accumulator the
            // real frame delta is the whole point - it is what makes a stall on
            // a real device produce catch-up steps rather than a slower wave.
            _clock.Advance(_runner, Time.deltaTime, () => _pair.Advance(_runner));
            _view.Render(_runner, _pair, _clock.Alpha);

            if (_runner.Done && !_written) WriteArtifact();
        }

        private void WriteArtifact()
        {
            _written = true;

            // Application.persistentDataPath is the app's Documents directory
            // on iOS, which is what UIFileSharingEnabled exposes to devicectl.
            // The engine does no file I/O; it hands back bytes and this writes
            // them. Same split Phase 0's CorpusPlayerHarness uses.
            var dir = Application.persistentDataPath;
            File.WriteAllBytes(Path.Combine(dir, "replay.bin"), _runner.Record.Serialize());
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
