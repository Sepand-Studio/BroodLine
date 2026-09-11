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

            var bytes = live.SerializeRecord();
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

            Assert.Equal(2, live.ReadRecord().RallyTick);
            Assert.Equal(2, live.ReadRecord().RallyCreature);

            var bytes = live.SerializeRecord();
            var replayed = Broodline.Sim.Combat.Sim.Replay(Replay.Deserialize(bytes));

            Assert.Equal(live.Outcome.Hash, replayed.Hash);
        }

        [Fact]
        public void RejectedRallyIsNotRecorded()
        {
            var live = Fresh();
            Assert.False(live.TryRally(99));
            while (live.Step()) { }

            Assert.Equal(-1, live.ReadRecord().RallyTick);
            Assert.Equal(-1, live.ReadRecord().RallyCreature);
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
            Assert.InRange(live.SerializeRecord().Length, 1, 512);
        }

        [Fact]
        public void DeserializeRejectsCorruption()
        {
            var live = Fresh();
            while (live.Step()) { }
            var bytes = live.SerializeRecord();

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
            var record = Replay.Deserialize(live.SerializeRecord());

            record.DeploymentHp[0] = 9999;
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void ValidateRejectsGeometryThatDisagreesWithTheTerrain()
        {
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.SerializeRecord());

            record.PocketCount = 4;          // Defile has 5
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void ValidateRejectsEveryOutOfRangeDeploymentField()
        {
            // Sim.Replay is the server verification path, so every field here
            // arrives from untrusted bytes. Before Validate was widened, a
            // forged pocket of 178956971 PASSED and simulated to a plausible
            // outcome, because 178956971 * 24 wraps into Lane's 120-entry
            // distance table; -1 and int.MaxValue threw IndexOutOfRangeException
            // out of the verifier instead of being rejected.
            var live = Fresh();
            while (live.Step()) { }
            var bytes = live.SerializeRecord();

            AssertRejected(bytes, r => r.Deployment[0].Pocket = 5, "pocket");
            AssertRejected(bytes, r => r.Deployment[0].Pocket = -1, "pocket");
            AssertRejected(bytes, r => r.Deployment[0].Pocket = 178956971, "pocket");

            // Species matters twice over: Stats.CreatureHp returns 0 for an
            // unknown one, so an unbounded species would ALSO defeat the HP
            // cross-check below it and let a forged record delete a creature.
            AssertRejected(bytes, r => r.Deployment[0].Species = (Species)99, "species");
            AssertRejected(bytes, r => r.Deployment[0].Instinct = (Instinct)99, "instinct");
            AssertRejected(bytes, r => r.Deployment[0].Trait1 = (Trait)99, "trait");
            AssertRejected(bytes, r => r.Deployment[0].Tier1 = 9, "tier");
            AssertRejected(bytes, r => { r.RallyTick = Stats.HardTickCap; r.RallyCreature = 0; }, "hard cap");
        }

        private static void AssertRejected(byte[] bytes, Action<Replay> corrupt, string expected)
        {
            var record = Replay.Deserialize(bytes);
            corrupt(record);
            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains(expected, e.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ValidateRejectsAnUnknownWaveAsAReplayFault()
        {
            // WaveDef.ForId throws WaveCompositionException, a SIBLING of this
            // class's exception. A caller written to the documented contract -
            // catch (ReplayFormatException) - would let it escape, so the single
            // most likely single-field corruption crashed the verifier rather
            // than being rejected. Validate translates it at the boundary.
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.SerializeRecord());

            record.WaveId = 7;
            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains("not authored", e.Message);
        }

        [Fact]
        public void DeserializeRejectsTrailingData()
        {
            var live = Fresh();
            while (live.Step()) { }
            var bytes = live.SerializeRecord();

            var padded = new byte[bytes.Length + 8];
            Array.Copy(bytes, padded, bytes.Length);
            Assert.Throws<ReplayFormatException>(() => Replay.Deserialize(padded));
        }

        [Fact]
        public void ValidateRejectsAReplayFromAnotherEngineVersion()
        {
            // solo_execution 9.4 - "a superseded replay renders its stored
            // outcome and is not re-simulated" - had no implementation: the
            // field was written and never compared. A pre-Rally replay would
            // re-simulate silently under the post-Rally state vector and return
            // a different hash, which on the server reads an honest player's
            // raid as a mismatch.
            var live = Fresh();
            while (live.Step()) { }
            var record = Replay.Deserialize(live.SerializeRecord());

            record.EngineVersion = "0.0.1-ancient";
            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains("stored outcome", e.Message);
        }

        [Fact]
        public void EngineVersionIsStored()
        {
            var live = Fresh();
            while (live.Step()) { }
            Assert.Equal(SimVersion.Value, live.ReadRecord().EngineVersion);
        }
    }
}
