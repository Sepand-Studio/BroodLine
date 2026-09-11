using System;
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
        // Walks up looking for Broodline.sln rather than for a directory named
        // "engine": the test project itself lives at tests/engine, so a search
        // for a same-named directory stops one level too early, at tests/,
        // which also contains an "engine" subfolder. Matches the pattern
        // already proven in tests/engine/EnforcementTests.cs (Phase 1).
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Broodline.sln")))
                dir = dir.Parent;
            Assert.True(dir != null, "could not locate the repository root");
            return dir.FullName;
        }

        [Fact]
        public void TheTickLoop_StillCallsTheEightPhasesInNormativeOrder()
        {
            // Scans SimRunner.cs rather than Sim.cs. Phase 3 moved the loop
            // body there so a renderer could step it, and Sim.Run became a
            // four-line loop over SimRunner - the phases did not move in any
            // sense that matters to this contract, but the file they live in
            // did. TheTickLoopLivesInExactlyOnePlace below is what keeps this
            // scan pointed at the only loop there is.
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "SimRunner.cs");
            Assert.True(File.Exists(path), "missing " + path);

            string source = File.ReadAllText(path);

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
            // Phase 3's whole claim about the decomposition: Sim.Run is a loop
            // over SimRunner, not a second implementation of it.
            // solo_execution section 9.3 rejects "a second model of the game
            // that has to stay in sync with the first" on cost grounds, and two
            // tick loops is exactly that shape.
            //
            // It is also what keeps the order scan above honest. A scan pointed
            // at one file proves nothing if a second loop can exist in another,
            // so this is the other half of that test rather than a separate
            // concern.
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Sim.cs");
            string source = File.ReadAllText(path);

            Assert.False(source.Contains("Phases."),
                "Sim.cs calls a phase function directly. The tick loop lives in " +
                "SimRunner; Sim.Run drives it. A second loop here would have to " +
                "be kept in step with that one forever, and the order-enforcement " +
                "scan only reads SimRunner.cs.");
        }

        [Fact]
        public void CapacityIsRecomputedRatherThanAccumulated()
        {
            // 5.3: "Capacity is recomputed from scratch every tick from the
            // live creature set, never accumulated." An accumulating
            // implementation would need somewhere to accumulate INTO, so the
            // guard is that AssignChill clears before it assigns.
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Capacity.cs");
            string source = File.ReadAllText(path);

            Assert.Contains("RaiderChilled[r] = false", source);
        }
    }
}
