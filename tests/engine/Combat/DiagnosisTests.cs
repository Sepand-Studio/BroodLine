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

        [Fact]
        public void Breach_DiagnosesAgainstTheBoardIncludingTheBreachingRaider()
        {
            // Pins the reordering this task exists for. Two Coursers are on the
            // board and Chill I covers exactly one, so coverage is insufficient -
            // but ONLY if the breaching raider is still counted when its own
            // diagnosis is computed. Mark it dead first and the count drops to
            // one, capacity 1 >= 1 reads "sufficient", and the loss screen
            // explains the defeat with the wrong reason. Nothing else in the
            // suite would notice that change.
            var wave = new WaveDef(7, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var s = new SimState(wave, Lane.Defile(), WithChill(tier: 1));
            s.Tick = 0;
            Phases.Spawn(s);

            s.RaiderProgress[0] = Fix64.FromInt(Stats.LaneTiles);   // at the Ark
            s.RaiderProgress[1] = Fix64.FromInt(10);                // still coming

            var log = new Breach[2];
            int count = 0;
            Phases.Breach(s, log, ref count);

            Assert.Equal(1, count);
            Assert.True(log[0].Access);      // a Pale does carry Chill I
            Assert.False(log[0].Coverage);   // but capacity 1 against 2 on the board
        }

        [Fact]
        public void Evaluate_StopsAtTheFirstFalseRatherThanScoringEveryField()
        {
            // With no carrier at all, access is false and the later fields must
            // stay false rather than be computed. simultaneous:0 is the only
            // input that tells the two implementations apart: an Evaluate that
            // scored every field unconditionally would report coverage TRUE here,
            // because capacity 0 >= 0 is vacuously satisfied - and the loss
            // screen would claim the deployment covered a raider it had no
            // answer to.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());

            var v = Diagnosis.Evaluate(s, RaiderType.Courser, simultaneous: 0, tile: 12);

            Assert.False(v.Access);
            Assert.False(v.Coverage);
            Assert.False(v.Placement);
            Assert.False(v.Answered);
        }
    }
}
