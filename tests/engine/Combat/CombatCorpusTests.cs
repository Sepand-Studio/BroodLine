using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class CombatCorpusTests
    {
        [Fact]
        public void CombatScenario_IsStableAcrossCalls()
        {
            Assert.Equal(Corpus.RunCombatScenario(0), Corpus.RunCombatScenario(0));
            Assert.Equal(Corpus.RunCombatScenario(499), Corpus.RunCombatScenario(499));
        }

        [Fact]
        public void CombatScenario_VariesWithIndex()
        {
            // If every index produced the same hash, a divergence would show on
            // one line of the diff or none, and the gate would be near-blind.
            Assert.NotEqual(Corpus.RunCombatScenario(0), Corpus.RunCombatScenario(1));
        }

        [Fact]
        public void CombatScenario_ExercisesBothOutcomes()
        {
            int clears = 0, losses = 0;
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                var o = Corpus.CombatOutcome(i);
                if (o.Result == Result.Win) clears++;
                else if (o.Result == Result.Loss) losses++;
                Assert.NotEqual(Result.Stalled, o.Result);
            }

            // A corpus that only ever lost would never execute the counter path
            // on IL2CPP, which is the half most worth proving.
            Assert.True(clears > 0, "corpus never clears a wave");
            Assert.True(losses > 0, "corpus never loses a wave");
        }
    }
}
