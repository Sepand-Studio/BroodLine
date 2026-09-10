using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class DiagnosisTests
    {
        private static CreatureSpec[] WithChill(int tier) => new[]
        {
            new CreatureSpec { Species = Species.Pale,   Pocket = 1, Instinct = Instinct.Vanguard,
                               Trait1 = Trait.Chill, Tier1 = tier },
            new CreatureSpec { Species = Species.Hollow, Pocket = 2, Instinct = Instinct.Vanguard }
        };

        [Fact]
        public void Access_IsFalseWhenNoCreatureCarriesTheTraitAtAll()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            var v = Diagnosis.Evaluate(s, RaiderType.Courser, simultaneous: 1, tile: 12);

            Assert.False(v.Access);
            Assert.False(v.Answered);
        }

        [Fact]
        public void Coverage_IsAboutTheLiveCountNotTheDeployment()
        {
            // Chill I is capacity 1. Sufficient against one Courser,
            // insufficient against two - the same deployment either way.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 12).Coverage);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 2, 12).Coverage);
        }

        [Fact]
        public void Coverage_UsesPerTraitCapacityNotAGlobalLadder()
        {
            // Chill II is 2, not combat_engine 5.3's superseded 3.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 2));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 2, 12).Coverage);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 3, 12).Coverage);
        }

        [Fact]
        public void Placement_IsFalseWhenNoCarrierCouldEverReachThatTile()
        {
            // Pale range 5 from pocket 1 (tile 10) reaches tiles 6-14.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            Assert.True(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 10).Placement);
            Assert.False(Diagnosis.Evaluate(s, RaiderType.Courser, 1, 23).Placement);
        }

        [Fact]
        public void TheFirstFalseIsTheDiagnosis_EvaluatedInOrder()
        {
            // No access AND unreachable. Access is reported first, and
            // coverage/placement are not claimed to be meaningful.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            var v = Diagnosis.Evaluate(s, RaiderType.Courser, 1, 23);

            Assert.False(v.Access);
            Assert.False(v.Coverage);
            Assert.False(v.Placement);
        }

        [Fact]
        public void PreWaveCheck_IsTheSameFunctionAgainstTheAuthoredWave()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 1));

            // Wave 6 authors exactly one Courser, so the pre-wave check should
            // agree with the live evaluation at a reachable tile.
            var pre = Diagnosis.PreWaveCheck(s, RaiderType.Courser);
            Assert.True(pre.Answered);

            var none = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                    SimStateTests.FiveWithoutChill());
            Assert.False(Diagnosis.PreWaveCheck(none, RaiderType.Courser).Answered);
        }
    }
}
