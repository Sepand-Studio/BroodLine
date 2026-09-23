using System;
using System.Collections.Generic;
using Broodline.Game.Shell;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    /// **A defect must reach a device log with the exception that caused it.**
    ///
    /// THE NAME IS HISTORICAL AND THE CLASS IT IS NAMED FOR NO LONGER EXISTS -
    /// Phase 9 Task 21i. `ReleaseConsole` was deleted; the file keeps its name so
    /// the two cases below keep their full test names, and nothing in it has been
    /// about a console for two rounds. Task 21h's D1 fix stopped `ServerError`'s
    /// transport branch handing a raw `Exception.Message` to a player, which took
    /// that text away from the only place it was recorded; `Diagnostics` is where
    /// it goes instead, and dropping it is the regression these cases catch.
    ///
    /// Fix round 1 also made a release player log a defect at WARNING severity,
    /// to keep Unity's on-screen Development Console from painting over the
    /// game. Fix round 2 reverted that: the console's UI is not in the device
    /// framework at all - `Diagnostics`'s own header carries the marker counts -
    /// and on the simulator the downgrade suppressed nothing anyway. A TestFlight
    /// log is the one place a shipped defect has to be findable, and a warning is
    /// what gets filtered out of one. So the severity assertion below is now
    /// single-valued, and it is asserted off the real log event rather than off a
    /// constant - a `Defect` that computed the right severity and then logged at
    /// a different one would pass the other way.
    ///
    /// THE CASE THAT USED TO GUARD THE CONSOLE PROPERTIES IS GONE WITH THE CLASS.
    /// It asserted that `Debug.developerConsoleEnabled`/`Visible` were still real
    /// and not `[Obsolete]`, so that an Editor upgrade could not hollow
    /// `ReleaseConsole` out in silence. With no file setting either property,
    /// that case guarded nothing but Unity's own surface - and a test that
    /// reddens when an engine API moves, over behaviour this project no longer
    /// attempts, is a gate on somebody else's code.
    public class ReleaseConsoleTests
    {
        [Test]
        public void ADefectReachesTheLogAtERRORSeverity_CarryingTheExceptionThatCausedIt()
        {
            // FILTERED INSIDE THE CALLBACK, because `Application
            // .logMessageReceived` is process-global: an unrelated log inside
            // this window would otherwise be counted as one of ours, and a
            // passing fix would redden for a reason that has nothing to do with
            // it. Presence of the expected line is what is asserted, not a
            // total.
            var seen = new List<KeyValuePair<LogType, string>>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (message != null && message.Contains(Diagnostics.Marker))
                    seen.Add(new KeyValuePair<LogType, string>(type, message));
            };

            var thrown = new InvalidOperationException("the wave never came back");

            Application.logMessageReceived += capture;
            try
            {
                Diagnostics.Defect("a wave produced no report", thrown);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.IsNotEmpty(seen,
                "the defect did not reach the log at all, so this case is not testing what it says");

            var line = seen[0];

            // ERROR, NOT WARNING. The console this was briefly downgraded for
            // does not exist on the device; a TestFlight log filtered to errors
            // is where a shipped defect has to show up.
            Assert.AreEqual(LogType.Error, line.Key,
                "a defect is logged below error severity, so it is filterable out of the one log " +
                "a shipped failure has to be findable in");

            StringAssert.Contains("a wave produced no report", line.Value,
                "the line does not say what failed");
            StringAssert.Contains("the wave never came back", line.Value,
                "the exception's own text did not reach the log, which is the regression this exists to prevent");
            StringAssert.Contains(nameof(InvalidOperationException), line.Value,
                "the exception's TYPE did not reach the log - Defect takes an object so ToString() carries it");
        }

        [Test]
        public void ADefectWithNothingToReport_SaysSoRatherThanThrowing()
        {
            // It is called from `catch` blocks. A null in here must not become a
            // second exception thrown out of the handler for the first.
            StringAssert.Contains("<nothing>", Diagnostics.Line("a call failed", null));
        }
    }
}
