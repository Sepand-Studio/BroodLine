using System;
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class ReplayTests
    {
        private static SimRunner Fresh() =>
            new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                          GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

        [Fact]
        public void RoundTrip_WithoutRally_ReproducesTheOutcome()
        {
            var live = Fresh();
            while (live.Step()) { }

            var bytes = live.Record.Serialize();
            var replayed = Broodline.Sim.Combat.Sim.Replay(Replay.Deserialize(bytes));

            Assert.Equal(live.Outcome.Hash, replayed.Hash);
            Assert.Equal(live.Outcome.Result, replayed.Result);
            Assert.Equal(live.Outcome.Ticks, replayed.Ticks);
        }

        [Fact]
        public void RoundTrip_WithRally_ReproducesTheOutcome()
        {
            // The one that matters. A replay is the INPUTS, so if Rally is not
            // recorded at the tick it was consumed, this diverges.
            var live = Fresh();
            live.Step();
            live.Step();
            Assert.True(live.TryRally(2));
            while (live.Step()) { }

            Assert.Equal(2, live.Record.RallyTick);
            Assert.Equal(2, live.Record.RallyCreature);

            var bytes = live.Record.Serialize();
            var replayed = Broodline.Sim.Combat.Sim.Replay(Replay.Deserialize(bytes));

            Assert.Equal(live.Outcome.Hash, replayed.Hash);
        }

        [Fact]
        public void RejectedRallyIsNotRecorded()
        {
            var live = Fresh();
            Assert.False(live.TryRally(99));
            while (live.Step()) { }

            Assert.Equal(-1, live.Record.RallyTick);
            Assert.Equal(-1, live.Record.RallyCreature);
        }

        [Fact]
        public void ARecordIsAFewHundredBytes()
        {
            // combat_engine section 3 makes this a constraint, not a
            // description: bible section 4.9 gives every raid a replay and
            // there are two raids per player per day. Asserting it is what
            // stops someone reaching for JSON later.
            var live = Fresh();
            while (live.Step()) { }
            Assert.InRange(live.Record.Serialize().Length, 1, 512);
        }

        [Fact]
        public void DeserializeRejectsCorruption()
        {
            var live = Fresh();
            while (live.Step()) { }
            var bytes = live.Record.Serialize();

            Assert.Throws<ReplayFormatException>(() => Replay.Deserialize(new byte[] { 1, 2, 3 }));

            var badMagic = (byte[])bytes.Clone();
            badMagic[0] ^= 0xFF;
            Assert.Throws<ReplayFormatException>(() => Replay.Deserialize(badMagic));
        }

        [Fact]
        public void ValidateRejectsHpThatDisagreesWithTheStatTable()
        {
            // combat_engine section 3 stores HP per deployed creature; the
            // engine derives it from Stats. Storing a value the simulation then
            // ignores is worse than not storing it, because the two can
            // disagree and nothing says so. So it is checked on load.
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.Record.Serialize());

            record.DeploymentHp[0] = 9999;
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void ValidateRejectsGeometryThatDisagreesWithTheTerrain()
        {
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.Record.Serialize());

            record.PocketCount = 4;          // Defile has 5
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void EngineVersionIsStored()
        {
            var live = Fresh();
            while (live.Step()) { }
            Assert.Equal(SimVersion.Value, live.Record.EngineVersion);
        }
    }
}
