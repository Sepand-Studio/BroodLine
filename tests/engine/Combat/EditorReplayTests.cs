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
    /// The artifact is tracked, so this runs everywhere, including CI - there
    /// is no skip and no hardware gate.
    ///
    /// Re-captured 2026-09-14 from Assets/Scenes/Wave.unity in the Unity 6
    /// Editor under engine 0.2.0, replacing the 0.1.0 capture the engine had
    /// moved past. The tap landed at tick 200, inside creature 0's engagement
    /// (ticks 184..240), so the Rally window 200..319 overlaps it directly.
    ///
    /// JUDGE A CAPTURE BY ITS WINDOW, NOT THE TAP INSTANT. Rally lasts 120
    /// ticks. The 2026-09-11 capture tapped at 78 - twelve ticks before the
    /// Courser even spawns - and was still valid, because 78..197 covered most
    /// of the engagement. An earlier comment called that one inert by reasoning
    /// from the instant alone, which is the same mistake that made the first
    /// two device captures look acceptable.
    public class EditorReplayTests
    {
        private readonly ITestOutputHelper _out;
        public EditorReplayTests(ITestOutputHelper output) { _out = output; }

        private static Replay Record() => ReplayArtifact.Read(ReplayArtifact.EditorBin);

        [Fact]
        public void TheEditorRunReSimulatesToTheSameHash()
        {
            var record = Record();
            if (ReplayArtifact.Superseded(record, _out)) return;

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
            //
            // ASSERTED AS A RANGE, NOT A LITERAL. This was
            // Assert.Equal(78, record.RallyTick), pinning whatever the
            // 2026-09-11 capture happened to record - so it failed on the first
            // re-capture for a reason with nothing to do with the property
            // under test. That is the same fault ReplayArtifact.CapturedUnder
            // was rewritten to remove: a constant duplicating a fact already in
            // the file, carrying a maintenance cost and enforcing nothing.
            //
            // THE RANGE IS THE TEST. A tick index is bounded by the wave that
            // produced it. A millisecond timestamp for the same tap would be
            // roughly 6,600 against a 540-tick wave and fails loudly, which is
            // exactly the regression this test exists to catch. Every honest
            // human tap passes, so a re-capture never edits this file again.
            var record = Record();
            var lines = File.ReadAllLines(ReplayArtifact.Require(ReplayArtifact.EditorOutcome));
            int ticks = int.Parse(lines[2]);

            Assert.InRange(record.RallyTick, 0, ticks - 1);
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
            var record = Record();
            if (ReplayArtifact.Superseded(record, _out)) return;

            var replayed = Broodline.Sim.Combat.Sim.Replay(record);

            Assert.Equal(Result.Loss, replayed.Result);
            Assert.Equal(0, replayed.IntegrityRemaining);
            Assert.Equal(1, replayed.BreachCount);
            Assert.Equal(RaiderType.Courser, replayed.Breaches[0].Type);
            Assert.False(replayed.Breaches[0].Access);
        }
    }
}
