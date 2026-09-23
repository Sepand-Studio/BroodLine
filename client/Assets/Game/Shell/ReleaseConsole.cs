using UnityEngine;

namespace Broodline.Game.Shell
{
    /// Asks Unity to keep its on-screen developer console off in a RELEASE
    /// player. **It is a SIMULATOR concern only, and it is not known to work.**
    ///
    /// WHAT WAS SEEN, twice, on an iPhone 17 simulator: a `Debug.LogError` -
    /// first from `BootController`'s cold-start catch, later from the
    /// wave-timeout path - raised Unity's Development Console, which drew a full
    /// red stack trace across the title bar, the recovery screen, the sheet and
    /// every button on it. In a build proven non-development by its own
    /// `boot.config` (no `player-connection` line where a development build has
    /// seven) and `Classes/Preprocessor.h` (`UNITY_DEVELOPER_BUILD 0` against
    /// its `1`).
    ///
    /// **AND IT CANNOT HAPPEN ON THE SHIPPING PLATFORM, WHICH IS THE WHOLE
    /// REASON THIS FILE IS SMALL** - Phase 9 Task 21h, fix round 2. A release
    /// DEVICE framework and a release SIMULATOR framework were built from the
    /// same non-development settings, differing only in SDK, and compared:
    ///
    ///     marker                      simulator   device
    ///     "Development Console"               1        0
    ///     developerConsoleVisible             6        4
    ///     developerConsoleEnabled             6        4
    ///     UnityFramework                 122 MB   112 MB
    ///
    /// The console's own `Clear` and `Close` chrome - the exact buttons in the
    /// screenshots - is in the simulator framework and not the device one. The
    /// scripting API survives on device, which is why the two properties below
    /// still compile and why `ReleaseConsoleTests` still passes; the UI
    /// implementation does not ship there. A binary cannot draw a window whose
    /// title string it does not contain.
    ///
    /// THE HONEST LIMIT: that is an absence found with `strings` over a
    /// controlled pair of binaries, so it is strong evidence and not proof, and
    /// no release build has yet run on real hardware. What makes it strong is
    /// the contrast being 1 against 0 on the exact string observed on screen.
    ///
    /// THE HEADER USED TO SAY THE OPPOSITE AND IT WAS THE REASONING, NOT A
    /// DETAIL. It read: "a tester opens the app, the request times out, the boot
    /// logs its error, and a red debug console appears over the game ... that is
    /// what a TestFlight tester would have seen first." On the platform
    /// TestFlight ships to, they would have seen no console at all. Everything
    /// this file claimed about a player-facing risk was about the simulator.
    ///
    /// SO WHY KEEP IT. Because a developer working on the simulator meets the
    /// console over the game, it is two property writes, and it is the API Unity
    /// documents for the job. It is not kept because it is known to help:
    ///
    ///   - **Setting both properties once at `BeforeSceneLoad` does not suppress
    ///     it.** Walked. `Disable` is not stripped and does run - it is listed in
    ///     the release player's own `Data/RuntimeInitializeOnLoads.json` as
    ///     `Broodline.Game / Broodline.Game.Shell / ReleaseConsole / Disable`
    ///     with `loadTypes: 1`, which is `BeforeSceneLoad` - and the console
    ///     appeared anyway, on an error raised minutes into play, long after it.
    ///   - **Neither property is deprecated.** Both still carry a setter in
    ///     6000.6 and neither is `[Obsolete]`, which `ReleaseConsoleTests`
    ///     asserts so an Editor upgrade that changes that reddens rather than
    ///     hollowing this class out in silence.
    ///   - **The re-assert below is UNTESTED, not refuted.** It was added in fix
    ///     round 1 and skipped warnings, and the walk that followed logged its
    ///     defect at warning severity - so the one path that raised the console
    ///     was the one path the handler ignored. It no longer skips anything,
    ///     because that walk also proved the console shows warnings. Nobody has
    ///     walked it since.
    ///
    /// **DO NOT SPEND MORE ON THIS WITHOUT A REASON.** Three rounds have gone
    /// into a red rectangle that a shipped build cannot draw. If a future walk
    /// still sees it on the simulator, the next honest step is to stop trying to
    /// suppress it and accept it as something developers see and testers do not.
    ///
    /// DEVELOPMENT BUILDS KEEP IT, deliberately and unchanged. The console is
    /// what surfaced this phase's launch crash - it printed the
    /// `ArgumentNullException` behind `Shader.Find` answering null in a player.
    /// The switch is `Debug.isDebugBuild` and nothing else.
    ///
    /// THERE IS NO READBACK LINE ANY MORE. Fix round 1 logged one so a device
    /// walk could tell "the method never ran" from "the setter did not take".
    /// The walk that followed received no game-side `Debug` output at all - a
    /// `--console-pty` capture stopped after 161 lines of engine startup - so it
    /// was a diagnostic nobody could read, aimed at a surface that does not ship.
    /// If something here ever does need to report to a device walk, the app's
    /// `Documents/` directory persists and already holds `snapshot.json` and
    /// `tokens.json`; a log line does not reach anyone.
    public static class ReleaseConsole
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Disable()
        {
            if (Debug.isDebugBuild) return;

            KeepItDown();

            // RE-ASSERTED ON EVERY LINE, for the one hypothesis left standing:
            // that something turns the console back on after
            // `BeforeSceneLoad`. Two bool writes per log line, in a release
            // player only.
            //
            // `logMessageReceived`, NOT `logMessageReceivedThreaded`. The
            // threaded one fires on whatever thread logged, and these are
            // main-thread state; the non-threaded one is marshalled to the main
            // thread, which is where writing them is legal. Never unsubscribed,
            // because there is nothing after a player's lifetime to unsubscribe
            // for - and in the Editor this method returns above, so nothing
            // subscribes there either.
            Application.logMessageReceived += OnLogged;
        }

        /// NO FILTER BY `LogType`, AND THAT IS THE FIX ROUND 2 CHANGE. This used
        /// to return early for `Log` and `Warning` on the assumption that only
        /// errors raise the console. The walk after fix round 1 painted the
        /// console from a WARNING, so the assumption was wrong and the filter was
        /// exactly what stopped this handler ever running on the path that
        /// mattered.
        static void OnLogged(string condition, string stackTrace, LogType type)
        {
            KeepItDown();
        }

        static void KeepItDown()
        {
            Debug.developerConsoleEnabled = false;
            Debug.developerConsoleVisible = false;
        }
    }
}
