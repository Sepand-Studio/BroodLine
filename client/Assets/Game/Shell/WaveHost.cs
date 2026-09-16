using System;
using System.Collections.Generic;
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

        /// `traits` is read PER RUN rather than captured once, because the
        /// snapshot it comes from is replaced wholesale by every `/v1/sync`
        /// (`PlayerSnapshot`'s own rule: "the next sync replaces this
        /// wholesale, with no merge"). A host holding the list it was
        /// constructed with would name last week's counter on the defeat
        /// screen after a bundle publish.
        public WaveHost(Func<IReadOnlyList<TraitSummary>> traits)
        {
            _traits = traits ?? throw new ArgumentNullException(nameof(traits));
        }

        /// Loads the wave scene, plays `waveId` with `deployment`, and
        /// unloads.
        ///
        /// `inputEnabled` false is a wave the player watches - design section
        /// 5's beat 2, "creatures act on their own via Instinct; the player
        /// watches and wins".
        public async Task<WaveReport> RunAsync(int waveId, CreatureSpec[] deployment, ulong seed, bool inputEnabled)
        {
            // BEFORE the load, not after. The scene's components run `Awake`
            // as part of the additive integration, so this is the last moment
            // at which "before play" is still true - see `WaveRunner.Hosted`.
            WaveRunner.Hosted = true;
            try
            {
                await Await(SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive));

                var runner = FindRunner();
                runner.Configure(WaveDef.ForId(waveId), deployment, seed, inputEnabled);
                await runner.Completed;

                // SYNCHRONOUS, and it must stay that way. `Outcome.Breaches`
                // is a `ReadOnlySpan` and therefore a ref struct: it cannot
                // live across an `await`. Nothing in this method reads it -
                // the whole span lifetime is inside this one call.
                var report = WaveReportBuilder.From(runner.Runner, _traits());

                await Await(SceneManager.UnloadSceneAsync(SceneName));
                return report;
            }
            finally
            {
                WaveRunner.Hosted = false;
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
