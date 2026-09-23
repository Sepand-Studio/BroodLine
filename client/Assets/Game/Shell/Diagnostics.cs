using UnityEngine;

namespace Broodline.Game.Shell
{
    /// How a developer diagnostic reaches a device log, beside the sentence a
    /// player is shown and separately from it.
    ///
    /// WHY IT EXISTS - Phase 9 Task 21h. `ServerError.From(Exception)`'s
    /// transport branch used to hand a raw `Exception.Message` to
    /// `PlayerMessage`, so a `TimeoutException` written for a console became
    /// the permanent explanation on `InterruptedView`. Closing that took the
    /// exception's text away from the only place it was recorded: of the nine
    /// readers of `PlayerMessage` in `FtueDirector`, exactly one had a log of
    /// its own. `BootController.OnNotice` logs the SENTENCE, which after the fix
    /// is an authored string and nothing else - so an NRE inside the lineage
    /// read produced "The game hit an unexpected problem." in the log and no
    /// message, no type and no stack anywhere. This is where that goes.
    ///
    /// `detail` IS AN `object` SO AN EXCEPTION ARRIVES THROUGH `ToString()`,
    /// which carries the type, the message AND the stack - `.Message` carries
    /// only the sentence. A `ServerError` arrives through its own `ToString()`,
    /// which is where its `Diagnostic` surfaces; `LoadRosterAsync` is the one
    /// caller that never holds an exception and needs that.
    ///
    /// ---------------------------------------------------------------------
    ///
    /// **ERROR SEVERITY, AND IT WAS BRIEFLY NOT.** Fix round 1 logged a defect
    /// at `LogWarning` in a release player, to stop Unity's on-screen
    /// Development Console painting a red stack trace over the recovery screen.
    /// Fix round 2 reverted that, on two measurements:
    ///
    ///   1. **The console cannot exist on the shipping platform.** A release
    ///      DEVICE framework and a release SIMULATOR framework - both
    ///      `BuildOptions.None`, both `UNITY_DEVELOPER_BUILD 0`, both with no
    ///      `player-connection` line, differing only in SDK - contain the string
    ///      `Development Console` **once** and **zero** times respectively. The
    ///      console's own `Clear` and `Close` chrome is in the simulator
    ///      framework too. The scripting API survives on device
    ///      (`developerConsoleEnabled` appears four times against the
    ///      simulator's six), which is why `ReleaseConsole` still compiles; the
    ///      UI implementation does not. A binary cannot draw a window whose
    ///      title it does not contain.
    ///   2. **It was not buying anything on the simulator either.** The walk
    ///      that followed fix round 1 found the console still painting, with
    ///      `[defect]` - logged at WARNING severity by that very change - as the
    ///      live log path. So the console shows warnings, and "`LogWarning` does
    ///      not raise the console", which this header asserted, was simply
    ///      false.
    ///
    /// THE HONEST LIMIT ON (1), because it is evidence and not proof: it is an
    /// absence found with `strings` over a controlled pair of binaries, and no
    /// release build has been run on real hardware. What makes it strong is that
    /// the contrast is 1 against 0 on the exact string that was observed painted
    /// across the screen.
    ///
    /// SO A DEFECT IS AN ERROR, WHICH IS WHAT THE ONE AUDIENCE THAT MATTERS
    /// NEEDS. A TestFlight log is where a shipped defect has to be findable, and
    /// a warning is what gets filtered out of one. The console was a simulator
    /// cosmetic; a genuine failure that nobody can find in a device log is not.
    ///
    /// `ReleaseConsole` is still the lever for the simulator, and its header
    /// says what is and is not known about whether it works.
    public static class Diagnostics
    {
        /// The marker a device log is grepped for. Distinct from `[Ftue]`, which
        /// `BootController.OnNotice` puts on the sentence a PLAYER was shown -
        /// the two are deliberately separable, because the whole point of this
        /// task's D1 is that they are different strings now.
        public const string Marker = "[defect]";

        /// One line: what failed, and the thing that failed.
        public static string Line(string what, object detail)
        {
            return Marker + " " + what + ": " + (detail == null ? "<nothing>" : detail.ToString());
        }

        /// Record a defect.
        ///
        /// NO SEVERITY SEAM, DELIBERATELY. Fix round 1 had a `SeverityFor(bool
        /// development)` so both arms could be driven from EditMode. With one
        /// severity there is one arm, and a parameter that cannot change the
        /// answer is a seam that only looks like a decision.
        public static void Defect(string what, object detail)
        {
            Debug.LogError(Line(what, detail));
        }
    }
}
