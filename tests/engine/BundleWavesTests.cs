using System;
using Xunit;
using Broodline.Sim.Combat;
using Broodline.Config.Validate;

namespace Broodline.Sim.Tests
{
    /// The bundle's wave JSON, mapped onto the engine's own WaveDef and
    /// validated by the engine's own rules. Nothing here reimplements a rule;
    /// these tests pin the MAPPING, which is the only part that is new.
    public class BundleWavesTests
    {
        private const string Wave6 = @"
        [
          { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
            ""spawns"": [ { ""tick"": 90, ""type"": ""Courser"" } ] }
        ]";

        [Fact]
        public void ParsesAnAuthoredWave()
        {
            var waves = BundleWaves.Parse(Wave6);
            Assert.Single(waves);
            Assert.Equal(6, waves[0].Id);
            Assert.Equal(2, waves[0].Integrity);
            Assert.Equal(1, waves[0].LaneCount);
            Assert.Equal(1, waves[0].Spawns.Length);
            Assert.Equal(90, waves[0].Spawns[0].Tick);
            Assert.Equal(RaiderType.Courser, waves[0].Spawns[0].Type);
        }

        [Fact]
        public void ThrowsWhenTheTimelineIsOutOfOrder()
        {
            // Ordering is one of three rules that can fire here now. RaiderType
            // had a single value when this was written, so the max-four-types
            // and shared-counter rules had nothing to compare; at three values
            // they are live, and they started being enforced through this
            // parser with no change to this project at all. That is the
            // argument for invoking the engine rather than copying it, and it
            // has now actually been paid out rather than merely predicted.
            const string outOfOrder = @"
            [
              { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
                ""spawns"": [ { ""tick"": 120, ""type"": ""Courser"" },
                              { ""tick"": 90,  ""type"": ""Courser"" } ] }
            ]";

            var ex = Assert.Throws<WaveCompositionException>(() => BundleWaves.Parse(outOfOrder));
            Assert.Contains("ordered by tick ascending", ex.Message);
        }

        [Fact]
        public void ThrowsOnARaiderThisEngineDoesNotHave()
        {
            // A bundle naming a raider the engine cannot simulate must fail at
            // PUBLISH rather than at wave load on a player's device.
            //
            // Breaker, not Skirmisher. This named Skirmisher until Skirmisher
            // was added, at which point the assertion became "a raider this
            // engine DOES have is rejected" - and it went red, which is what a
            // test pinned to a real absent name is for. Breaker is the next
            // raider in combat_numbers section 6 that this engine lacks.
            const string unknown = @"
            [
              { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
                ""spawns"": [ { ""tick"": 90, ""type"": ""Breaker"" } ] }
            ]";

            var ex = Assert.Throws<WaveCompositionException>(() => BundleWaves.Parse(unknown));
            Assert.Contains("Breaker", ex.Message);
        }
    }
}
