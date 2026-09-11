using System;
using System.IO;
using Xunit;

namespace Broodline.Sim.Tests.Combat
{
    /// combat_engine section 4: "the order is normative - changing it changes
    /// outcomes." That makes tick order a behavioural contract, but a contract
    /// nothing enforces: reorder two lines in Sim.Run and every unit test still
    /// passes, because each phase in isolation is still correct.
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
        public void SimRun_StillCallsTheEightPhasesInNormativeOrder()
        {
            string path = Path.Combine(RepoRoot(), "engine", "Runtime", "Combat", "Sim.cs");
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
                Assert.True(at >= 0, "Sim.Run no longer calls " + ordered[i]);
                Assert.True(at > previous,
                    ordered[i] + " is out of normative order - see combat_engine section 4. " +
                    "Movement must precede targeting so a creature never fires at a " +
                    "position a raider has already left, and death must follow attack.");
                previous = at;
            }
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
