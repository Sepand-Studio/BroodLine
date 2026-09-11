using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Broodline.Sim.Tests.Combat
{
    /// combat_engine section 4: "the order is normative - changing it changes
    /// outcomes." That makes tick order a behavioural contract, but a contract
    /// nothing enforces: reorder two lines in the tick loop and every unit test
    /// still passes, because each phase in isolation is still correct.
    ///
    /// The goldens would catch it - but only until someone re-pins them to
    /// match new output, which is exactly what a person does when they believe
    /// they made a harmless refactor. So this asserts the order in the source
    /// directly, and is deliberately blunt: a failure here is not "the code is
    /// wrong", it is "a balance change is being made and should be explicit".
    public class CombatEnforcementTests
    {
        /// Named once, as a path RELATIVE TO engine/Runtime. Both facts below
        /// depend on agreeing about which file holds the loop, and they
        /// disagreed silently when it moved. A bare file name is not enough:
        /// the exemption below compared basenames, so a second tick loop
        /// reintroduced the violation simply by being called
        /// engine/Runtime/AutoResolve/SimRunner.cs.
        private const string TickLoopFile = "Combat/SimRunner.cs";
        private const string PhasesFile = "Combat/Phases.cs";

        private static string EngineRuntime() =>
            Path.Combine(TestPaths.RepoRoot(), "engine", "Runtime");

        [Fact]
        public void TheTickLoop_StillCallsTheEightPhasesInNormativeOrder()
        {
            // Scans SimRunner.cs rather than Sim.cs. Phase 3 moved the loop
            // body there so a renderer could step it, and Sim.Run became a
            // four-line loop over SimRunner - the phases did not move in any
            // sense that matters to this contract, but the file they live in
            // did. TheTickLoopLivesInExactlyOnePlace below is what keeps this
            // scan pointed at the only loop there is.
            string path = Path.Combine(EngineRuntime(), "Combat", "SimRunner.cs");
            Assert.True(File.Exists(path), "missing " + path);

            // CODE, not prose. This is the load-bearing scan of the pair and it
            // was the one reading raw source: a doc comment at the top of
            // SimRunner.cs listing the eight calls in order satisfied all eight
            // assertions no matter what the real loop did - and this file's own
            // class comment is exactly the kind of prose that would do it.
            string source = CodeOnly(File.ReadAllText(path));

            string[] ordered =
            {
                "Phases.Spawn(",
                "Phases.State(",
                "Phases.Movement(",
                "Phases.Targeting(",
                "Phases.Attack(",
                "Phases.Death(",
                "Phases.Breach(",
                "Phases.Resolve("
            };

            int previous = -1;
            for (int i = 0; i < ordered.Length; i++)
            {
                int at = source.IndexOf(ordered[i], StringComparison.Ordinal);
                Assert.True(at >= 0, "the tick loop no longer calls " + ordered[i]);
                Assert.True(at > previous,
                    ordered[i] + " is out of normative order - see combat_engine section 4. " +
                    "Movement must precede targeting so a creature never fires at a " +
                    "position a raider has already left, and death must follow attack.");
                previous = at;
            }
        }

        [Fact]
        public void TheTickLoopLivesInExactlyOnePlace()
        {
            // Phase 3's claim about the decomposition: Sim.Run is a loop over
            // SimRunner, not a second implementation of it. solo_execution 9.3
            // rejects "a second model of the game that has to stay in sync with
            // the first" on cost grounds, and two tick loops is that shape.
            //
            // This used to check only Sim.cs, which left the hole it claimed to
            // close: a reviewer demonstrated it by adding AutoResolve.cs with a
            // full second tick loop - Targeting BEFORE Movement, the exact
            // violation the order scan exists to catch - and all 141 tests
            // passed. Scanning EVERY engine file is what makes the order scan
            // above mean anything, because that scan reads one file and is
            // worthless if a second loop can live in another.
            //
            // Everything below is judged by path RELATIVE to engine/Runtime.
            // Absolute paths made two of these checks wrong at once: the
            // exemption matched Path.GetFileName, so the reviewer's second tick
            // loop reproduced the violation after being renamed to
            // AutoResolve/SimRunner.cs, and the ThirdParty skip matched the
            // whole absolute path, so a clone into ~/ThirdParty/BroodLine
            // emptied the offender list and passed the test unconditionally.
            string root = EngineRuntime();
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                if (rel == TickLoopFile || rel == PhasesFile) continue;   // the loop, and the phases themselves
                if (rel == "ThirdParty" || rel.StartsWith("ThirdParty/", StringComparison.Ordinal)) continue;

                if (CodeOnly(File.ReadAllText(file)).Contains("Phases."))
                    offenders.Add(rel);
            }

            Assert.True(offenders.Count == 0,
                "these engine files call a phase function directly: " + string.Join(", ", offenders) + ". " +
                "The tick loop lives in " + TickLoopFile + " and Sim.Run drives it. A second loop " +
                "would have to be kept in step with that one forever, and the order-enforcement " +
                "scan only reads " + TickLoopFile + ".");
        }

        /// Source with line comments removed, so the scan above judges CODE.
        /// Without this, a doc comment that merely mentions the phases fails the
        /// test - which is exactly what happened: Ids.cs documents the tick
        /// order for readers and names "Phases.*" while calling nothing. A
        /// guard that fires on prose teaches people to weaken it.
        ///
        /// Line comments only. The engine has no block comments, and a scanner
        /// that tried to handle strings and verbatim literals would be a parser
        /// pretending to be a grep.
        private static string CodeOnly(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            foreach (var line in source.Split('\n'))
            {
                int at = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at >= 0 ? line.Substring(0, at) : line).Append('\n');
            }
            return sb.ToString();
        }

        [Fact]
        public void TheOrderScanReadsCodeAndNotProse()
        {
            // Both scans above are only as good as this, and the order scan -
            // the load-bearing one - was not using it at all. A file whose
            // ONLY mention of the phases is a comment must read as calling
            // none of them, or a doc comment listing the eight in order
            // satisfies the contract on its own.
            string prose =
                "/// The tick order is Phases.Spawn( then Phases.State( then the rest.\n" +
                "int x = 1;   // Phases.Resolve(\n";

            Assert.DoesNotContain("Phases.", CodeOnly(prose));
            Assert.Contains("int x = 1;", CodeOnly(prose));
        }

        [Fact]
        public void CapacityIsRecomputedRatherThanAccumulated()
        {
            // 5.3: "Capacity is recomputed from scratch every tick from the
            // live creature set, never accumulated." An accumulating
            // implementation would need somewhere to accumulate INTO, so the
            // guard is that AssignChill clears before it assigns.
            string path = Path.Combine(EngineRuntime(), "Combat", "Capacity.cs");
            string source = File.ReadAllText(path);

            Assert.Contains("RaiderChilled[r] = false", source);
        }
    }
}
