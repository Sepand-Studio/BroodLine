using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class BatchRunnerTests
    {
        [Fact]
        public void Batch_CountsOutcomesAcrossRuns()
        {
            var r = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(),
                                    firstSeed: 1, runs: 20);

            Assert.Equal(20, r.Runs);
            Assert.Equal(0, r.Stalls);
            // Wave 6 without Chill is designed to be lost, and the simulation
            // is deterministic, so every seed loses it.
            Assert.Equal(20, r.Losses);
            Assert.Equal(0, r.Clears);
        }

        [Fact]
        public void Batch_ShowsTheCounterIsWhatChangesTheOutcome()
        {
            var without = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                          GoldenTests.DeploymentWithoutChill(), 1, 20);
            var with = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                       GoldenTests.DeploymentWithChill(), 1, 20);

            Assert.Equal(0, without.Clears);
            Assert.Equal(20, with.Clears);
        }

        [Fact]
        public void Batch_IsReproducible()
        {
            var a = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(), 7, 10);
            var b = BatchRunner.Run(WaveDef.Wave6(), Lane.Defile(),
                                    GoldenTests.DeploymentWithoutChill(), 7, 10);
            Assert.Equal(a.Hash, b.Hash);
        }
    }
}
