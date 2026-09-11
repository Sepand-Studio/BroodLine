using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimRunnerTests
    {
        [Fact]
        public void SteppedToCompletion_ReproducesGoldenA()
        {
            // Asserted against the PINNED LITERAL, not against Sim.Run - once
            // Run delegates to SimRunner, comparing the two proves nothing.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(2495532238167386945UL, r.Outcome.Hash);
            Assert.Equal(Result.Loss, r.Outcome.Result);
            Assert.Equal(1, r.Outcome.BreachCount);
            Assert.False(r.Outcome.Breaches[0].Access);
        }

        [Fact]
        public void SteppedToCompletion_ReproducesGoldenB()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(13482666686023521257UL, r.Outcome.Hash);
            Assert.Equal(Result.Win, r.Outcome.Result);
            Assert.Equal(2, r.Outcome.IntegrityRemaining);
        }

        [Fact]
        public void TrueReturnsEqualOutcomeTicks()
        {
            // The loop increments Tick only on a step that does NOT terminate,
            // so the count of true returns is exactly Outcome.Ticks. This is
            // the invariant that catches an off-by-one in the extraction - the
            // single likeliest way to get this wrong.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            int steps = 0;
            while (r.Step()) steps++;

            Assert.Equal(r.Outcome.Ticks, steps);
        }

        [Fact]
        public void StepAfterTermination_IsFalseAndHarmless()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }
            ulong hash = r.Outcome.Hash;

            Assert.False(r.Step());
            Assert.False(r.Step());
            Assert.Equal(hash, r.Outcome.Hash);
        }

        [Fact]
        public void TickAdvancesOneAtATime()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            Assert.Equal(0, r.Tick);
            r.Step();
            Assert.Equal(1, r.Tick);
            r.Step();
            Assert.Equal(2, r.Tick);
        }

        [Fact]
        public void ReadOnlySurface_TracksTheLiveState()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

            Assert.Equal(2, r.Integrity);
            Assert.Equal(5, r.CreatureCount);
            Assert.Equal(24, r.LaneTiles);
            Assert.Equal(0, r.RaiderCount);          // the Courser spawns at t=90
            Assert.Equal(260, r.CreatureHp[0]);      // Vetch
            Assert.Equal(Species.Loam, r.CreatureSpecies[4]);

            // Step past the spawn tick and the raider surface populates.
            while (r.Tick < 91 && r.Step()) { }
            Assert.Equal(1, r.RaiderCount);
            Assert.True(r.RaiderAlive[0]);
            Assert.False(r.RaiderChilled[0]);        // no Chill in this deployment

            // Not an equality against 220. Spawn is phase 1 and Attack is phase
            // 5, so a creature can acquire and fire on the Courser's own spawn
            // tick - pinning "undamaged" here would be pinning phase ordering
            // in the wrong test.
            Assert.InRange(r.RaiderHp[0], 1, Stats.RaiderHp(RaiderType.Courser));
        }

        [Fact]
        public void SimRunner_ExposesNoPathToMutableState()
        {
            // The guarantee is structural, so it is asserted structurally: no
            // public member of SimRunner may hand out SimState or a raw array.
            // A ReadOnlySpan property cannot be written through; a T[] property
            // can, and that is the mistake this test exists to prevent.
            var t = typeof(SimRunner);
            foreach (var p in t.GetProperties())
            {
                Assert.False(p.PropertyType == typeof(SimState),
                    "SimRunner." + p.Name + " hands out SimState, which View could write through.");
                Assert.False(p.PropertyType.IsArray,
                    "SimRunner." + p.Name + " hands out a raw array. Use ReadOnlySpan<T>.");
            }
            foreach (var m in t.GetMethods())
            {
                Assert.False(m.ReturnType == typeof(SimState),
                    "SimRunner." + m.Name + " returns SimState.");
            }
        }
    }
}
