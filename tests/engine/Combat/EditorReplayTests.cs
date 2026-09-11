using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// The device round-trip minus IL2CPP and the device: wave 6 played through
    /// the real renderer in the Editor, at a real variable frame rate, with a
    /// real human tap in it, re-simulated headlessly on CoreCLR.
    ///
    /// It exists for the diagnosis it gives when the DEVICE round-trip fails.
    /// Without it, a device mismatch has two candidate causes - something
    /// IL2CPP-specific, or something wrong with the accumulator and the record
    /// in general - and separating them means bisecting on hardware. With it,
    /// that question is already answered before the device is involved.
    ///
    /// Unlike DeviceReplayTests this never skips. The artifact is tracked, so
    /// it runs everywhere, including CI.
    ///
    /// Captured 2026-09-11 from Assets/Scenes/Wave.unity in the Unity 6 Editor.
    /// The tap landed at tick 78, twelve ticks before the Courser spawns - but
    /// Rally lasts 120 ticks, so the window runs 78..197 and covers most of
    /// creature 0's engagement (ticks 184..240). It is NOT inert, and an
    /// earlier version of this comment said it was: "spent on an empty lane"
    /// reasons from the tap instant rather than the window, which is the same
    /// mistake that made the first two device captures look acceptable.
    public class EditorReplayTests
    {
        private readonly ITestOutputHelper _out;
        public EditorReplayTests(ITestOutputHelper output) { _out = output; }

        private static Replay Record() =>
            Replay.Deserialize(File.ReadAllBytes(ReplayArtifact.Require(ReplayArtifact.EditorBin)));

        [Fact]
        public void TheEditorRunReSimulatesToTheSameHash()
        {
            var record = Record();
            record.Validate();

            // solo_execution section 9.4: a replay recorded under a superseded
            // engine renders its stored outcome and is NOT re-simulated. That
            // rule is what keeps this test honest across a balance change -
            // re-running a stale record would compare two different games and
            // report the difference as a bug.
            //
            // NOTE, and it is a real gap rather than a caution: SimVersion is
            // not bumped when behaviour changes. It was still 0.1.0 after Rally
            // moved every hash in the project. So this guard cannot currently
            // fire, and section 9.4's mechanism is inert. Tracked as a decision
            // owed in the Phase 3 plan.
            Assert.Equal(SimVersion.Value, record.EngineVersion);

            var lines = File.ReadAllLines(ReplayArtifact.Require(ReplayArtifact.EditorOutcome));
            Assert.True(lines.Length >= 4,
                "editor-replay-outcome.txt should carry hash, result, ticks and integrity on four lines");

            ulong editorHash = ulong.Parse(lines[0]);
            var editorResult = (Result)int.Parse(lines[1]);
            int editorTicks = int.Parse(lines[2]);
            int editorIntegrity = int.Parse(lines[3]);

            var replayed = Broodline.Sim.Combat.Sim.Replay(record);
            _out.WriteLine("editor : hash=" + editorHash + " result=" + editorResult +
                           " ticks=" + editorTicks + " integrity=" + editorIntegrity);
            _out.WriteLine("resim  : hash=" + replayed.Hash + " result=" + replayed.Result +
                           " ticks=" + replayed.Ticks + " integrity=" + replayed.IntegrityRemaining);

            Assert.Equal(editorHash, replayed.Hash);
            Assert.Equal(editorResult, replayed.Result);
            Assert.Equal(editorTicks, replayed.Ticks);
            Assert.Equal(editorIntegrity, replayed.IntegrityRemaining);
        }

        [Fact]
        public void TheEditorRunCarriesAPlayerInputAtATickIndex()
        {
            // The half of the contract a headless test cannot reach. This tap
            // happened at a wall-clock moment during a variable-rate frame, and
            // what survived into the record is a TICK. If the accumulator ever
            // starts recording timestamps, or consumes input outside a tick
            // boundary, this is where it shows.
            var record = Record();

            Assert.Equal(78, record.RallyTick);
            Assert.Equal(0, record.RallyCreature);
        }

        [Fact]
        public void TheEditorRunIsWave6AsAuthored()
        {
            // The record is the inputs, so this is also a check that the
            // composition root deployed what it claims to: waves_01_12 section
            // 3's "expected roster 5, none carrying Chill", on Defile.
            var record = Record();

            Assert.Equal(6, record.WaveId);
            Assert.Equal(Terrain.Defile, record.Terrain);
            Assert.Equal(1, record.LaneCount);
            Assert.Equal(5, record.PocketCount);
            Assert.Equal(Stats.LaneTiles, record.LaneTiles);
            Assert.Equal(5, record.Deployment.Length);

            foreach (var c in record.Deployment)
            {
                Assert.NotEqual(Trait.Chill, c.Trait1);
                Assert.NotEqual(Trait.Chill, c.Trait2);
            }
        }

        [Fact]
        public void TheEditorRunIsTheAuthoredLoss()
        {
            // waves_01_12 section 3 designs wave 6 as the beat that teaches the
            // counter system, so a Loss here is the wave working. The diagnosis
            // is the point: ACCESS false means the trait was absent, not
            // mis-tiered and not mis-placed.
            var replayed = Broodline.Sim.Combat.Sim.Replay(Record());

            Assert.Equal(Result.Loss, replayed.Result);
            Assert.Equal(0, replayed.IntegrityRemaining);
            Assert.Equal(1, replayed.BreachCount);
            Assert.Equal(RaiderType.Courser, replayed.Breaches[0].Type);
            Assert.False(replayed.Breaches[0].Access);
        }
    }
}
