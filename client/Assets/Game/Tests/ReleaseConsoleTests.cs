using System;
using System.Collections.Generic;
using System.Reflection;
using Broodline.Game.Shell;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    /// **A release player must not paint a red developer console over the
    /// game**, and this file holds the part of that which is checkable without
    /// a device.
    ///
    /// THE DEFECT, on an iPhone 17 at the Phase 9 exit gate: Unity's on-screen
    /// Development Console drew a full red stack trace across the title bar,
    /// the recovery screen, the sheet and its buttons, on a `Debug.LogError`
    /// from the wave-timeout path - in a build proven non-development by its
    /// own `boot.config` and `Preprocessor.h`. `ReleaseConsole` was written to
    /// prevent exactly that and had never been verified.
    ///
    /// WHAT THIS FILE CANNOT DO, said first so nobody reads more into it: it
    /// cannot prove the console stays down. That needs a player, and the
    /// player is the only place the two `UnityEngine.Debug` properties have any
    /// effect at all. What it CAN do is hold the two things whose failure would
    /// make `ReleaseConsole` a silent no-op again - the API still being real,
    /// and a release player never logging at a severity that raises the console
    /// in the first place - plus the shape of the readback line the next device
    /// walk will be read for.
    public class ReleaseConsoleTests
    {
        [Test]
        public void UnitysDeveloperConsoleSwitches_AreStillRealAndSettable()
        {
            // THE ARM OF THE DIAGNOSIS THIS CAN ACTUALLY CLOSE. One candidate
            // for why the suppression did nothing was that these properties
            // had been deprecated or hollowed out under Unity 6000.6. They
            // have not: both still carry a setter and neither is `[Obsolete]`.
            // That is measured here rather than asserted in a comment, so the
            // Editor upgrade that DOES deprecate them reddens this instead of
            // turning `ReleaseConsole` back into a method that compiles and
            // achieves nothing.
            foreach (var name in new[] { "developerConsoleEnabled", "developerConsoleVisible" })
            {
                var property = typeof(Debug).GetProperty(
                    name, BindingFlags.Static | BindingFlags.Public);

                Assert.IsNotNull(property,
                    "UnityEngine.Debug." + name + " is gone, so ReleaseConsole no longer compiles against " +
                    "the thing it was written to set");
                Assert.IsNotNull(property.GetSetMethod(),
                    "UnityEngine.Debug." + name + " is read-only now, so nothing can turn the console off");
                Assert.IsEmpty(property.GetCustomAttributes(typeof(ObsoleteAttribute), inherit: false),
                    "UnityEngine.Debug." + name + " is deprecated, so ReleaseConsole is suppressing the " +
                    "console through an API Unity has stopped honouring - which is one of the two " +
                    "candidates for why it did not work on the device");
            }
        }

        [Test]
        public void ADefectKeepsItsExceptionText_AndOnlyRaisesTheConsoleWhereItIsWANTED()
        {
            // BOTH HALVES MATTER AND THEY PULL AGAINST EACH OTHER, which is
            // why they are one case. The severity half is what keeps a release
            // player from raising the console at all, whatever the two
            // properties above turn out to do. The TEXT half is what stops that
            // from becoming the regression it would otherwise be: Task 21h's
            // broad `ServerError` fix stopped `PlayerMessage` carrying
            // `Exception.Message`, so if the log line does not carry the
            // exception either, a defect on a device leaves no record anywhere.
            //
            // READ OFF THE ACTUAL LOG EVENT, not off `SeverityFor`. Asserting
            // the constant would pass on a `Defect` that computed the right
            // severity and then logged at a different one.
            var seen = new List<KeyValuePair<LogType, string>>();
            Application.LogCallback capture =
                (message, stack, type) => seen.Add(new KeyValuePair<LogType, string>(type, message));

            var thrown = new InvalidOperationException("the wave never came back");

            Application.logMessageReceived += capture;
            try
            {
                Diagnostics.Defect("a wave produced no report", thrown, development: true);
                Diagnostics.Defect("a wave produced no report", thrown, development: false);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            Assert.AreEqual(2, seen.Count,
                "the defects did not reach the log at all, so this case is not testing what it says");

            Assert.AreEqual(LogType.Error, seen[0].Key,
                "a development build lost the red console that is the whole reason it keeps one");
            Assert.AreEqual(LogType.Warning, seen[1].Key,
                "a release player logged at error severity, which is what raises the console over the game");

            foreach (var line in seen)
            {
                StringAssert.Contains(Diagnostics.Marker, line.Value);
                StringAssert.Contains("a wave produced no report", line.Value,
                    "the line does not say what failed");
                StringAssert.Contains("the wave never came back", line.Value,
                    "the exception's own text did not reach the log, which is the regression this exists to prevent");
                StringAssert.Contains(nameof(InvalidOperationException), line.Value,
                    "the exception's TYPE did not reach the log - Defect takes an object so ToString() carries it");
            }
        }

        [Test]
        public void ADefectWithNothingToReport_SaysSoRatherThanThrowing()
        {
            // It is called from `catch` blocks. A null in here must not become
            // a second exception thrown out of the handler for the first.
            StringAssert.Contains("<nothing>", Diagnostics.Line("a call failed", null));
        }

        [Test]
        public void TheReadbackLineNamesWhatItFound_SoTheNextDeviceWalkCanSettleIt()
        {
            // THE LINE A DEVICE LOG IS GREPPED FOR. Its three outcomes are the
            // three remaining hypotheses, and the walk distinguishes them by
            // reading it: absent means `Disable` never ran, `enabled=True`
            // means the setter did not take, and `enabled=False` beside a
            // visible console means the properties do not gate it. Asserted so
            // that a change to the wording cannot quietly break the grep the
            // report tells the next walker to run.
            var line = ReleaseConsole.Readback(enabled: false, visible: false);

            StringAssert.StartsWith(ReleaseConsole.Marker, line);
            StringAssert.Contains("enabled=False", line);
            StringAssert.Contains("visible=False", line);
            StringAssert.Contains("enabled=True", ReleaseConsole.Readback(enabled: true, visible: false));
        }
    }
}
