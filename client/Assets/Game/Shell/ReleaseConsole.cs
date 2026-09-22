using UnityEngine;

namespace Broodline.Game.Shell
{
    /// Keeps Unity's on-screen developer console off in a RELEASE player.
    ///
    /// THE DEFECT THIS CLOSES, found on the simulator during Phase 9's exit
    /// gate: a packaged, NON-development player — `BuildOptions.None`, no
    /// `player-connection` in its `boot.config`, verified against a
    /// development build of the same tree that carries seven such lines —
    /// drew the red developer console over the UI on a `Debug.LogError`. In
    /// the capture that settled it, the console was painting across the tab
    /// bar and covering the Map tab.
    ///
    /// WHY THAT IS NOT A CURIOSITY. `BootController.Start` logs an error when
    /// the cold start fails, deliberately, so a failed boot cannot vanish as
    /// an unobserved exception out of an `async void`. And the backend runs at
    /// `min_instance_count = 0`, so a first launch against a cold service can
    /// time out. Those two compose: a tester opens the app, the request times
    /// out, the boot logs its error, and a red debug console appears over the
    /// game. Neither half is wrong on its own; together they are what a
    /// TestFlight tester would have seen first.
    ///
    /// DEVELOPMENT BUILDS KEEP IT, deliberately. The console is what surfaced
    /// the launch crash this phase spent a task on — it printed the
    /// `ArgumentNullException` that led to `Shader.Find` answering null in a
    /// player. Suppressing it everywhere would trade a real diagnostic for a
    /// cosmetic one, so the switch is `Debug.isDebugBuild` and nothing else.
    ///
    /// BEFORE THE SCENE LOADS, not from `BootController`, because the console
    /// answers the FIRST error and anything logged during scene load would
    /// already have raised it.
    public static class ReleaseConsole
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Disable()
        {
            if (Debug.isDebugBuild) return;

            Debug.developerConsoleEnabled = false;
            Debug.developerConsoleVisible = false;
        }
    }
}
