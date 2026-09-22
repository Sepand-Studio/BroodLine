using UnityEngine;

namespace Broodline.Game.Shell
{
    /// How a developer diagnostic reaches a device log without painting over
    /// the game.
    ///
    /// THE DEFECT THIS EXISTS FOR, seen on an iPhone 17 at the Phase 9 exit
    /// gate: a `Debug.LogError` on the wave-timeout path raised Unity's
    /// on-screen Development Console, which drew a full red stack trace across
    /// the title bar, the recovery screen, the sheet and every button on it -
    /// in a RELEASE player, where `ReleaseConsole` is supposed to have turned
    /// that console off. See `ReleaseConsole` for what is and is not known
    /// about why it did not.
    ///
    /// SEVERITY IS THE ONLY PART OF THAT THIS FILE CAN MAKE CERTAIN, and that
    /// is why it exists rather than a comment telling callers to be careful.
    /// `ReleaseConsole` turns the console off through two properties whose
    /// effect nobody has been able to verify on a device; a log that is never
    /// raised at error severity in a release player cannot be the thing that
    /// raises the console, whatever those properties do. So:
    ///
    ///   - development build or Editor -> `LogError`, because that is where the
    ///     console is WANTED. It is what printed the `ArgumentNullException`
    ///     behind this phase's launch crash.
    ///   - release player -> `LogWarning`. `Debug.LogWarning` does not raise
    ///     the console, and `BootController.OnNotice` already uses that
    ///     severity for the same audience.
    ///
    /// NOTHING IS LOST BY THE DOWNGRADE, WHICH IS THE WHOLE TEST IT HAD TO
    /// PASS. The exception's own text and stack still reach the log, in full,
    /// at both severities - that is asserted in `ReleaseConsoleTests`, because
    /// dropping the detail is the regression this helper was added to prevent
    /// rather than cause. What changes is a colour and a log level on the one
    /// configuration where the colour covers the game.
    ///
    /// IF THE SEVERITY IS EVER WANTED BACK, `SeverityFor` is the one line to
    /// change - and then `ReleaseConsole` has to hold on its own, which is not
    /// yet known.
    public static class Diagnostics
    {
        /// The marker a device log is grepped for. Distinct from `[Ftue]`,
        /// which `BootController.OnNotice` puts on the sentence a PLAYER was
        /// shown - the two are deliberately separable, because the whole point
        /// of this phase's D1 is that they are different strings now.
        public const string Marker = "[defect]";

        /// What a release player logs a defect at, and what a development one
        /// does.
        ///
        /// A SEAM RATHER THAN A READ OF `Debug.isDebugBuild`, so both arms can
        /// be driven from EditMode - where `isDebugBuild` is always true and
        /// the release arm would otherwise be unreachable. The same arrangement
        /// `BootController.BindSafeAreas` and `BroodlineClient.ColdStartAsync`
        /// use for their own seams.
        public static LogType SeverityFor(bool development)
        {
            return development ? LogType.Error : LogType.Warning;
        }

        /// One line: what failed, and the thing that failed.
        ///
        /// `detail` is an `object` so an `Exception` arrives through
        /// `ToString()` - which carries the type, the message AND the stack -
        /// rather than through `.Message`, which carries only the sentence. A
        /// `ServerError` arrives through its own `ToString()`, which is where
        /// its `Diagnostic` lives.
        public static string Line(string what, object detail)
        {
            return Marker + " " + what + ": " + (detail == null ? "<nothing>" : detail.ToString());
        }

        /// Record a defect. Production entry point.
        public static void Defect(string what, object detail)
        {
            Defect(what, detail, Debug.isDebugBuild);
        }

        /// The seamed form. `development` is `Debug.isDebugBuild` in production.
        public static void Defect(string what, object detail, bool development)
        {
            Debug.unityLogger.Log(SeverityFor(development), Line(what, detail));
        }
    }
}
