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

        /// Blanks out comments and string literals, preserving offsets so
        /// reported line numbers still point at the real line.
        ///
        /// WHY THIS EXISTS. The scan used to match raw source text, so any
        /// PROSE naming the pattern tripped the gate that forbids it. That is
        /// not hypothetical: `TabBarTests.cs` carries a doc comment explaining
        /// that this very test is a known, correct failure against three
        /// deliberate sites - and the sentence naming them became a fourth.
        /// Documentation of a rule is not a violation of it, and a gate that
        /// cannot tell code from commentary punishes the commenting.
        ///
        /// String literals go too, for the same reason at one remove: a test
        /// asserting on the text of an error message should not register as
        /// the defect the message describes.
        ///
        /// `verify-uss-tokens.sh` strips comments before its hex check for
        /// exactly this reason, and for exactly the same original cause -
        /// `Theme.uss` documents the two primitives USS cannot express, hexes
        /// and all. This is that lesson, applied to C#.
        static string StripCommentsAndStrings(string src)
        {
            var outp = new System.Text.StringBuilder(src.Length);
            // 0 code, 1 line comment, 2 block comment, 3 string, 4 char, 5 verbatim string
            var state = 0;
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                var next = i + 1 < src.Length ? src[i + 1] : '\0';
                var keep = false;

                switch (state)
                {
                    case 0:
                        if (c == '/' && next == '/') { state = 1; }
                        else if (c == '/' && next == '*') { state = 2; }
                        else if (c == '"' && i > 0 && src[i - 1] == '@') { state = 5; }
                        else if (c == '"') { state = 3; }
                        else if (c == '\'') { state = 4; }
                        else keep = true;
                        break;
                    case 1:
                        if (c == '\n') { state = 0; keep = true; }
                        break;
                    case 2:
                        if (c == '*' && next == '/') { state = 0; i++; }
                        else if (c == '\n') keep = true;
                        break;
                    case 3:
                        if (c == '\\') i++;
                        else if (c == '"') state = 0;
                        break;
                    case 4:
                        if (c == '\\') i++;
                        else if (c == '\'') state = 0;
                        break;
                    case 5:
                        if (c == '"' && next == '"') i++;
                        else if (c == '"') state = 0;
                        else if (c == '\n') keep = true;
                        break;
                }
                outp.Append(keep ? c : (c == '\n' ? '\n' : ' '));
            }
            return outp.ToString();
        }

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
                    // Production code owns this rule. Test-only HTTP stubs
                    // intentionally resume off-thread and never build UI;
                    // scanning them reported a false production violation.
                    if (file.Contains("/Generated/")) continue;
                    if (file.Contains("/Tests/")) continue;

                    scanned++;
                    var text = StripCommentsAndStrings(File.ReadAllText(file));
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
