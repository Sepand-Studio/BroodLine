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
    ///
    /// ---------------------------------------------------------------------
    ///
    /// **AND IT DID NOT WORK. Phase 9 Task 21h, fix round 1.** The same
    /// release player, re-walked on an iPhone 17, drew the console over the
    /// whole recovery screen on a `Debug.LogError` from the wave-timeout path.
    /// So this class has been fixed in name only since it landed, and nothing
    /// noticed because for a while nothing on a reachable path logged an error
    /// - which was itself not true: `BootController`'s cold-start catch has
    /// logged one all along, on exactly the timed-out-first-launch path this
    /// header describes.
    ///
    /// WHAT IS RULED OUT, from the build artifacts rather than by argument:
    ///
    ///   - **It is not a development build.** `build/ios-simulator/Data/boot.config`
    ///     carries no `player-connection` line where the development build's
    ///     carries seven, and `Classes/Preprocessor.h` has
    ///     `UNITY_DEVELOPER_BUILD 0` against the development project's `1`. So
    ///     `Debug.isDebugBuild` is false and the guard below does not swallow
    ///     the call.
    ///   - **`Disable` is not stripped and does run.** It is listed in the
    ///     release player's own `Data/RuntimeInitializeOnLoads.json` as
    ///     `Broodline.Game / Broodline.Game.Shell / ReleaseConsole / Disable`
    ///     with `loadTypes: 1`, which is `BeforeSceneLoad`.
    ///   - **It is not an ordering problem.** The error that raised the console
    ///     came out of `FightAsync` minutes into play, long after
    ///     `BeforeSceneLoad`.
    ///   - **The API has not gone away.** Both properties still have getters
    ///     and setters in 6000.6 and neither is `[Obsolete]` -
    ///     `ReleaseConsoleTests` asserts that, so an Editor upgrade that
    ///     deprecates them reddens rather than silently no-ops.
    ///
    /// WHAT IS LEFT, AND IT IS NOT DIAGNOSED: the two properties were set and
    /// the console appeared anyway. Either they no longer gate it, or something
    /// turns it back on after `BeforeSceneLoad`. Nothing reachable from a test
    /// machine can tell those apart, so this round does two things instead of
    /// guessing between them:
    ///
    ///   1. **Re-asserts on every error**, below, for the "something turns it
    ///      back on" arm. It cannot help the other arm and it cannot hurt.
    ///   2. **Reads the properties back and logs what it got**, which is what
    ///      makes the NEXT device walk conclusive rather than a third attempt.
    ///      If that line is missing, this method did not run. If it says
    ///      `enabled=True`, the setter did not take. If it says
    ///      `enabled=False` and the console still appears, the properties do
    ///      not gate it and the only remaining lever is severity - which is
    ///      why `Diagnostics` exists and does not depend on any of this.
    ///
    /// The readback is a `Debug.Log`, deliberately: an error or a warning here
    /// would be a line that could raise the very thing it is reporting on.
    public static class ReleaseConsole
    {
        /// What the readback line starts with, so a device log can be grepped
        /// for it and `ReleaseConsoleTests` can assert its shape without a
        /// player.
        public const string Marker = "[ReleaseConsole]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Disable()
        {
            if (Debug.isDebugBuild) return;

            KeepItDown();

            // `logMessageReceived`, NOT `logMessageReceivedThreaded`. The
            // threaded one fires on whatever thread logged, and these two
            // properties are main-thread state; the non-threaded one is
            // marshalled to the main thread, which is where a re-assert is
            // legal. Never unsubscribed, because there is nothing after a
            // player's lifetime to unsubscribe for - and in the Editor this
            // method returns above, so nothing subscribes there either.
            Application.logMessageReceived += OnLogged;

            Debug.Log(Readback(Debug.developerConsoleEnabled, Debug.developerConsoleVisible));
        }

        static void OnLogged(string condition, string stackTrace, LogType type)
        {
            // Only the three that raise it, and never for a Log or a Warning -
            // the readback above is a `Debug.Log` and re-entering here for it
            // would be pointless work on every line the game ever prints.
            if (type == LogType.Log || type == LogType.Warning) return;
            KeepItDown();
        }

        static void KeepItDown()
        {
            Debug.developerConsoleEnabled = false;
            Debug.developerConsoleVisible = false;
        }

        /// The readback line, as a pure function so its shape is asserted
        /// without a player. What a device log will show is exactly this.
        public static string Readback(bool enabled, bool visible)
        {
            return Marker + " suppressed: enabled=" + enabled + " visible=" + visible;
        }
    }
}
