using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    /// `ConfigureAwait(false)` must not appear in code that touches the UI.
    ///
    /// A SOURCE ASSERTION, AND DELIBERATELY SO. The defect it guards cannot
    /// be reached from a unit test: `ConfigureAwait(false)` only diverts a
    /// task that has NOT already completed synchronously, and every stub a
    /// test hands the director completes inline. The suite ran green over a
    /// first hour that could not start against a real server - 267 EditMode
    /// tests, none of which could have failed. Task 17 Step 6 found it by
    /// pressing Play against a local `api` and reading the exception off the
    /// bottom of the Editor.
    ///
    /// So the rule is asserted where it IS visible: in the text. That is
    /// weaker than a behavioural test and it is what is available; the
    /// alternative is the rule living in a comment nobody runs.
    ///
    /// WHY THE RULE. Unity's `UnitySynchronizationContext` is the only thing
    /// that returns a continuation to the main thread, and
    /// `ConfigureAwait(false)` is the explicit instruction not to use it. The
    /// await resumes on a threadpool thread, and the first VisualElement
    /// built after it throws `VisualElementCreation can only be called from
    /// the main thread`. One occurrence poisons everything downstream: a pool
    /// thread carries no context, so every later await stays off the main
    /// thread however it is written.
    ///
    /// `Net/` IS EXEMPT, and the exemption is the point rather than an
    /// oversight. It touches no VisualElement, and a method's own
    /// `ConfigureAwait` does not follow its caller home -
    /// `OutboxPump.cs` awaits `FlushAsync()` WITHOUT the flag, so the pump
    /// resumes on the main thread whatever `OutboxClient` does internally.
    /// Widening this test to `Net/` would delete a correct optimisation.
    public class MainThreadAffinityTests
    {
        /// Assets-relative roots whose code builds or mutates UI.
        static readonly string[] UiFacingRoots = { "Game", "UI" };

        /// Matches the call however it is written - inline after an `await`,
        /// on its own continuation line, or chained off a closing brace.
        static readonly Regex Offender = new Regex(@"\.ConfigureAwait\s*\(\s*false\s*\)");

        [Test]
        public void NoConfigureAwaitFalse_InCodeThatTouchesTheUi()
        {
            var offenders = new List<string>();
            var scanned = 0;

            foreach (var root in UiFacingRoots)
            {
                var dir = Path.Combine(Application.dataPath, root);
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    // Generated clients are not ours to edit, and this test's
                    // own text would otherwise match itself.
                    if (file.Contains("/Generated/")) continue;
                    if (Path.GetFileName(file) == "MainThreadAffinityTests.cs") continue;

                    scanned++;
                    var text = File.ReadAllText(file);
                    var lines = text.Split('\n');
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (!Offender.IsMatch(lines[i])) continue;
                        var rel = file.Substring(Application.dataPath.Length + 1);
                        offenders.Add($"{rel}:{i + 1}");
                    }
                }
            }

            // A scan that found no files would pass vacuously, which is the
            // failure mode this whole file exists to argue against.
            Assert.That(scanned, Is.GreaterThan(20),
                $"only {scanned} files scanned under {string.Join(", ", UiFacingRoots)} - the scan itself is broken, "
                + "so a pass here would mean nothing.");

            Assert.That(offenders, Is.Empty,
                "ConfigureAwait(false) in UI-facing code resumes the await off Unity's main thread, and the next "
                + "VisualElement built after it throws. Remove it (Net/ is exempt - see this class's comment):\n  "
                + string.Join("\n  ", offenders));
        }
    }
}
