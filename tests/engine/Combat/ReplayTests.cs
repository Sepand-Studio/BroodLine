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

        /// A serialized record, WITHOUT running the wave.
        ///
        /// The record is complete at construction - SimRunner fills every field
        /// from the wave, the lane and the deployment, and only TryRally ever
        /// writes to it afterwards. Eight tests below were stepping a full
        /// 540-tick wave to reach bytes the constructor had already produced.
        /// Any test that needs the OUTCOME still runs the wave; these need the
        /// codec.
        private static byte[] Record() => Fresh().SerializeRecord();

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
            Assert.InRange(Record().Length, 1, 512);
        }

        [Fact]
        public void DeserializeRejectsCorruption()
        {
            var bytes = Record();

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
            var record = Replay.Deserialize(Record());

            record.DeploymentHp[0] = 9999;
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void ValidateRejectsGeometryThatDisagreesWithTheTerrain()
        {
            var record = Replay.Deserialize(Record());

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
            var bytes = Record();

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
            var record = Replay.Deserialize(Record());

            record.WaveId = 7;
            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains("not authored", e.Message);
        }

        [Fact]
        public void DeserializeRejectsTrailingData()
        {
            var bytes = Record();

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
            var record = Replay.Deserialize(Record());

            record.EngineVersion = "0.0.1-ancient";
            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains("stored outcome", e.Message);
        }

        [Fact]
        public void EngineVersionIsStored()
        {
            Assert.Equal(SimVersion.Value, Fresh().ReadRecord().EngineVersion);
        }

        [Fact]
        public void DeserializeValidatesTheFormatItProduces()
        {
            // There is no such thing as an unvalidated Replay object. Handing
            // one back and trusting every caller to remember a second call is a
            // rule, and the batch that HARDENED Validate wrote two callers that
            // skipped it - one feeding the unbounded RallyCreature straight
            // into TryRally.
            //
            // -7 is the case the old upper-bound-only check missed entirely: it
            // read as "absent", so six distinct byte encodings validated to one
            // outcome in a format whose job is to be canonical.
            var record = Replay.Deserialize(Record());
            record.RallyTick = -1;
            record.RallyCreature = -7;      // pairs with "absent", and is not -1

            var e = Assert.Throws<ReplayFormatException>(
                () => Replay.Deserialize(record.Serialize()));
            Assert.Contains("out of range", e.Message);
        }

        [Fact]
        public void AFutureEngineRecordIsDiagnosedAsSupersededAndNotAsCorrupt()
        {
            // Every bound in Validate is THIS engine's width. Trait has two
            // members and combat_engine names seven more, so a legitimate
            // record from a later engine trips them - and checked last, as the
            // version was, the diagnosis came back "creature 0 carries an
            // unknown trait", which sends the reader after a forgery that is
            // not there.
            var record = Replay.Deserialize(Record());
            record.EngineVersion = "9.9.9-later";
            record.Deployment[0].Trait1 = (Trait)7;     // a Trait this engine does not have

            var e = Assert.Throws<ReplayFormatException>(() => record.Validate());
            Assert.Contains("stored outcome", e.Message);
            Assert.DoesNotContain("trait", e.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(record.IsFromThisEngine);
        }

        [Fact]
        public void TheSimulationRefusesEveryDeploymentTheCodecRefuses()
        {
            // The produce-vs-validate asymmetry, closed. The constructor
            // bounded the array LENGTH and nothing else, so the engine could
            // run a wave to completion and emit a record it could not read
            // back: Pocket 178956971 simulated to Loss in 540 ticks because
            // 178956971 * 24 wraps into Lane's 120-entry distance table, and
            // Species 6, Trait 2, Tier 7 and Instinct 9 each produced a
            // 212-byte record that Validate then rejected. On the verification
            // path that reads as an honest player's raid being corrupt.
            AssertBothRefuse(d => d[0].Pocket = 5);
            AssertBothRefuse(d => d[0].Pocket = -1);
            AssertBothRefuse(d => d[0].Pocket = 178956971);
            AssertBothRefuse(d => d[0].Species = (Species)6);
            AssertBothRefuse(d => d[0].Instinct = (Instinct)9);
            AssertBothRefuse(d => d[0].Trait1 = (Trait)2);
            AssertBothRefuse(d => d[0].Tier1 = 7);
        }

        private static void AssertBothRefuse(Action<CreatureSpec[]> corrupt)
        {
            var deployment = GoldenTests.DeploymentWithoutChill();
            corrupt(deployment);

            // The play path: refused before a single tick runs, and refused as
            // a composition fault rather than as an IndexOutOfRangeException
            // thrown out of the tick loop.
            Assert.Throws<WaveCompositionException>(
                () => new SimRunner(WaveDef.Wave6(), Lane.Defile(), deployment, GoldenTests.Seed));

            // The codec path: the same rule, from the same list.
            var record = Replay.Deserialize(Record());
            for (int c = 0; c < deployment.Length; c++) record.Deployment[c] = deployment[c];
            Assert.Throws<ReplayFormatException>(() => record.Validate());
        }

        [Fact]
        public void EveryRallyTheEngineAcceptsProducesARecordItCanReadBack()
        {
            // The produce-vs-validate symmetry as a PROPERTY rather than as one
            // case: whatever TryRally accepts, Validate must accept back. The
            // rally fields had the same asymmetry the deployment fields did -
            // Validate rejects a rally at or past the hard cap on the grounds
            // that no run reaches it, while TryRally would happily record one
            // there - so this sweeps the whole reachable range instead of
            // pinning the single tick that happened to be wrong.
            for (int tick = 0; tick <= 600; tick += 29)
            {
                var live = Fresh();
                while (live.Tick < tick && live.Step()) { }
                if (live.Done) break;
                if (!live.TryRally(0)) continue;

                // Must not throw: Deserialize validates the format, Validate
                // the rest.
                Replay.Deserialize(live.SerializeRecord()).Validate();
            }
        }

        [Fact]
        public void TryRallyAfterTheRunEndsIsRefused()
        {
            var live = Fresh();
            while (live.Step()) { }

            Assert.False(live.TryRally(0));
            Assert.Equal(-1, live.ReadRecord().RallyTick);
        }

        [Fact]
        public void ALaterEnginesBiggerRosterIsSupersededRatherThanUndecodable()
        {
            // Stats.DeploymentCap is a BALANCE constant, so a later engine that
            // raises it writes a legitimate record this one cannot simulate.
            // Enforcing it inside Deserialize made that record undecodable:
            // "deployment count 6 out of range", no Replay object at all, and
            // therefore no way to reach IsFromThisEngine and render the stored
            // outcome. Same misdiagnosis the version-first ordering exists to
            // prevent, one layer down.
            var record = Replay.Deserialize(Record());
            record.EngineVersion = "9.9.9-later";
            var six = new CreatureSpec[Stats.DeploymentCap + 1];
            var hp = new int[six.Length];
            for (int i = 0; i < six.Length; i++)
            {
                six[i] = new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard };
                hp[i] = Stats.CreatureHp(Species.Vetch);
            }
            record.Deployment = six;
            record.DeploymentHp = hp;

            // Decodes: the format can describe it.
            var readBack = Replay.Deserialize(record.Serialize());
            Assert.False(readBack.IsFromThisEngine);
            Assert.Equal(6, readBack.Deployment.Length);

            // And is diagnosed as superseded, not as corrupt.
            var e = Assert.Throws<ReplayFormatException>(() => readBack.Validate());
            Assert.Contains("stored outcome", e.Message);

            // The cap itself still binds for a record from THIS engine.
            readBack.EngineVersion = SimVersion.Value;
            Assert.Contains("cap", Assert.Throws<ReplayFormatException>(() => readBack.Validate()).Message);
        }

        [Fact]
        public void SerializeRejectsAHandBuiltRecordRatherThanThrowingNre()
        {
            // Copy tolerates null arrays, so it MANUFACTURES the object that
            // used to make Serialize raise a bare NullReferenceException - and
            // the test below asserts Copy produces exactly it.
            var e = Assert.Throws<ReplayFormatException>(() => new Replay().Copy().Serialize());
            Assert.Contains("disagree", e.Message);
        }

        [Fact]
        public void CopyToleratesARecordThatWasNotDeserialized()
        {
            // Copy is the only method that dereferenced both arrays without a
            // check, so a hand-built Replay - which the public settable fields
            // invite - raised a bare NullReferenceException from inside a
            // method called Copy.
            var copy = new Replay().Copy();
            Assert.Null(copy.Deployment);
            Assert.Null(copy.DeploymentHp);
        }

        [Fact]
        public void ReadRecordHandsOutACopyAndNotTheRecord()
        {
            // SerializeRecord returns bytes precisely so a caller cannot stamp
            // a rally onto a run that never had one. ReadRecord returns an
            // object, so it owes the same guarantee by copying.
            var live = Fresh();
            var first = live.ReadRecord();
            first.RallyTick = 400;
            first.RallyCreature = 1;
            first.Deployment[0].Pocket = 3;

            var second = live.ReadRecord();
            Assert.Equal(-1, second.RallyTick);
            Assert.Equal(-1, second.RallyCreature);
            Assert.Equal(0, second.Deployment[0].Pocket);
        }
    }
}
