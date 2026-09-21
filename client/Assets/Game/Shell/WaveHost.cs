using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Model;
using Broodline.Sim.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Broodline.Game.Shell
{
    /// Plays one wave and hands back a report.
    ///
    /// `client_architecture` section 9 and design section 4: one persistent
    /// root scene holds the composition root, and "Wave Defense loads
    /// ADDITIVELY and unloads on exit - it is the only scene with a 3D
    /// battlefield and the only one with a per-frame budget worth defending."
    /// This is that load and that unload.
    ///
    /// What crosses the boundary back is a `WaveReport`: plain data, built
    /// synchronously from the engine's `Outcome`, so no screen ever holds an
    /// engine type and `Broodline.UI` keeps referencing no engine assembly.
    public sealed class WaveHost
    {
        public const string SceneName = "Wave";

        readonly Func<IReadOnlyList<TraitSummary>> _traits;
        readonly Action<bool> _setShellVisible;

        /// `traits` is read PER RUN rather than captured once, because the
        /// snapshot it comes from is replaced wholesale by every `/v1/sync`
        /// (`PlayerSnapshot`'s own rule: "the next sync replaces this
        /// wholesale, with no merge"). A host holding the list it was
        /// constructed with would name last week's counter on the defeat
        /// screen after a bundle publish.
        public WaveHost(Func<IReadOnlyList<TraitSummary>> traits, Action<bool> setShellVisible = null)
        {
            _traits = traits ?? throw new ArgumentNullException(nameof(traits));
            _setShellVisible = setShellVisible ?? (_ => { });
        }

        /// How long a hosted wave may run before the host abandons it.
        ///
        /// A LIVENESS bound, not a gameplay one. The longest authored wave
        /// spawns its last raider at 13.5s of sim time (`WaveDef.Wave2`'s
        /// eighth Skirmisher and `Wave7`'s sixth, both at tick 405), so two
        /// minutes is not a number a wave that is still playing can reach.
        public const double CompletionTimeoutSeconds = 120;

        /// Loads the wave scene, plays `waveId` with `deployment`, and
        /// unloads.
        ///
        /// `inputEnabled` false is a wave the player watches - design section
        /// 5's beat 2, "creatures act on their own via Instinct; the player
        /// watches and wins".
        ///
        /// EVERY EXIT UNLOADS, which is why the unload is in the `finally`
        /// rather than on the happy path. Four things between the load and
        /// the report throw: `FindRunner` when the scene or its runner is
        /// missing, `WaveDef.ForId` on an id this build does not author,
        /// `Configure`, and `Completion`'s give-up. `FtueDirector.FightAsync`
        /// catches all of them, shows a notice and ends the walk - so an
        /// unload that ran only on success left the additive 3D battlefield,
        /// and its per-frame budget, sitting over the shell for the rest of
        /// the process, with a log line as the only trace.
        /// Zero or one. `WaveRunner.Hosted` is a process-wide latch and this
        /// is what keeps a second host from clearing it out from under the
        /// first: whichever run finished first would clear the latch while
        /// the other's scene was still resident, and `StandaloneCapture` is
        /// `standaloneCapture && !Hosted` read live every frame - so the
        /// survivor would get exactly the frame the `finally` below exists to
        /// prevent. Refused rather than counted: two waves cannot share one
        /// additive scene, so a second concurrent run is a caller bug and
        /// should say so at the call site rather than corrupt a capture.
        static int _active;

        public async Task<WaveReport> RunAsync(int waveId, CreatureSpec[] deployment, ulong seed, bool inputEnabled)
        {
            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
                throw new InvalidOperationException(
                    "[WaveHost] a wave is already hosted. " + SceneName + " is loaded additively and " +
                    "there is one of it - a second concurrent RunAsync would clear WaveRunner.Hosted " +
                    "while the first wave is still resident.");

            // BEFORE the load, not after. The scene's components run `Awake`
            // as part of the additive integration, so this is the last moment
            // at which "before play" is still true - see `WaveRunner.Hosted`.
            WaveRunner.Hosted = true;
            try
            {
                await Await(SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive));

                var runner = FindRunner();

                // Phase 9 design §2.2. AFTER the load, not before: the wave
                // scene's UIDocument is a sibling root in the shared panel and
                // is attached by now, so hiding the shell here leaves the HUD
                // and the battlefield on screen and nothing else. Hidden
                // before the load, the frame between hide and attach showed
                // whatever the last camera cleared to - the reverted fix.
                _setShellVisible(false);

                runner.Configure(WaveDef.ForId(waveId), deployment, seed, inputEnabled);
                await Completion(runner);

                // SYNCHRONOUS, and it must stay that way. `Outcome.Breaches`
                // is a `ReadOnlySpan` and therefore a ref struct: it cannot
                // live across an `await`. Nothing in this method reads it -
                // the whole span lifetime is inside this one call, which is
                // still true with an `await` in the `finally` below, because
                // the span never becomes a local here.
                return WaveReportBuilder.From(runner.Runner, _traits());
            }
            finally
            {
                // THE ORDER IS LOAD-BEARING, and it is the order the happy
                // path already had: unload FIRST, clear `Hosted` SECOND.
                // `WaveRunner.StandaloneCapture` is `standaloneCapture &&
                // !Hosted` read LIVE every frame, and `Wave.unity` ships
                // `standaloneCapture: true` - so a frame in which the latch
                // is clear and the scene is still resident is a frame in
                // which the runner either deploys wave 6 over the player's
                // own roster or overwrites the tracked capture artifacts
                // with a wave nobody asked to record.
                // RESTORE FIRST, and on every path. The exception path is the
                // one the reverted fix never restored. Guarded so a throwing
                // callback cannot skip the unload below.
                try { _setShellVisible(true); }
                catch (Exception error) { Debug.LogError("[WaveHost] setShellVisible(true) threw: " + error); }

                await UnloadAsync();
                WaveRunner.Hosted = false;
                Interlocked.Exchange(ref _active, 0);
            }
        }

        /// `runner.Completed`, bounded.
        ///
        /// That task is signalled from `WaveRunner.Update` and from nowhere
        /// else, so anything that stops the component ticking - the scene
        /// torn down from outside, the component disabled, a throw inside the
        /// frame - leaves it pending FOREVER. Unbounded, that is a player
        /// watching a battlefield that will never end, on a screen with no
        /// way back, and no code path that would ever say so.
        ///
        /// WALL CLOCK, deliberately. `Task.Delay` does not see
        /// `Time.timeScale`, so a test that plays the wave at 16x shortens
        /// the wave and not the bound - the bound can only ever be reached by
        /// a wave that has genuinely stopped.
        static async Task Completion(WaveRunner runner)
        {
            // Captured once: `Completed` is a property, and `Configure`
            // replaces the `TaskCompletionSource` behind it. The reference
            // comparison below is against the task actually awaited.
            var completed = runner.Completed;

            using (var giveUp = new CancellationTokenSource())
            {
                var expired = Task.Delay(TimeSpan.FromSeconds(CompletionTimeoutSeconds), giveUp.Token);
                if (await Task.WhenAny(completed, expired) != completed)
                    throw new TimeoutException(
                        "[WaveHost] " + SceneName + " did not finish within " +
                        CompletionTimeoutSeconds + "s of wall clock. Its WaveRunner stopped " +
                        "signalling; the scene is unloaded and the wave abandoned.");

                // So the timer does not sit in the queue for the rest of the
                // bound after every wave that ends normally. A cancelled
                // `Task.Delay` is Canceled rather than Faulted, so leaving it
                // unobserved raises nothing.
                giveUp.Cancel();
            }
        }

        /// Unloads the wave scene if it is loaded, and never throws.
        ///
        /// It runs from a `finally` that may be unwinding the exception the
        /// caller needs to see, and `UnloadSceneAsync` throws
        /// `ArgumentException` for a scene that is not loaded - which is
        /// exactly the state a failed `LoadSceneAsync` leaves behind. An
        /// unguarded unload here would REPLACE the real failure with a
        /// misleading one, on the path where the real failure is the only
        /// thing the player's notice has to go on.
        static async Task UnloadAsync()
        {
            try
            {
                var scene = SceneManager.GetSceneByName(SceneName);
                if (!scene.IsValid() || !scene.isLoaded) return;

                await Await(SceneManager.UnloadSceneAsync(scene));
            }
            catch (Exception error)
            {
                Debug.LogError("[WaveHost] could not unload " + SceneName + ": " + error);
            }
        }

        /// The one `WaveRunner` in the loaded wave scene.
        ///
        /// Searched by scene rather than with `Object.FindObjectOfType`,
        /// which would also find a runner in any other loaded scene - and
        /// under an ADDITIVE load there is always at least one other scene.
        WaveRunner FindRunner()
        {
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException(
                    "[WaveHost] " + SceneName + " did not load. It must be in EditorBuildSettings.scenes - " +
                    "BootSceneBuilder puts it there, after Boot.");

            foreach (var root in scene.GetRootGameObjects())
            {
                var runner = root.GetComponentInChildren<WaveRunner>(includeInactive: true);
                if (runner != null) return runner;
            }

            throw new InvalidOperationException(
                "[WaveHost] no WaveRunner in " + SceneName + " - rebuild it with Broodline > Build Wave Scene.");
        }

        /// An `AsyncOperation` as an awaitable.
        ///
        /// `completed` fires on Unity's main thread and the continuation
        /// resumes there too, because Unity installs a SynchronizationContext
        /// for it - which is what makes it legal for the line after the await
        /// to touch GameObjects.
        ///
        /// `isDone` is checked first: an operation that finished before the
        /// handler was attached never raises `completed` again, and awaiting
        /// it would hang the wave forever with no error.
        static Task Await(AsyncOperation operation)
        {
            var completion = new TaskCompletionSource<bool>();
            if (operation == null || operation.isDone)
            {
                completion.SetResult(true);
                return completion.Task;
            }
            operation.completed += _ => completion.TrySetResult(true);
            return completion.Task;
        }
    }
}
