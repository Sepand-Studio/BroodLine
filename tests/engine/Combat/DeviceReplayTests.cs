using System;
using System.IO;
using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// Locates a replay artifact captured from a real run through the
    /// renderer. Shared by the device tests, the Editor tests and the skip
    /// attribute, so there is one definition of where these live.
    internal static class ReplayArtifact
    {
        public const string DeviceBin = "device-replay.bin";
        public const string DeviceOutcome = "device-replay-outcome.txt";
        public const string EditorBin = "editor-replay.bin";
        public const string EditorOutcome = "editor-replay-outcome.txt";

        public static string Path(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(System.IO.Path.Combine(dir.FullName, "Broodline.sln")))
                dir = dir.Parent;
            if (dir == null) return null;
            return System.IO.Path.Combine(dir.FullName, "implementation", "results", name);
        }

        public static bool Present(string name)
        {
            var p = Path(name);
            return p != null && File.Exists(p);
        }
    }

    /// Skips rather than fails when the device artifact is absent.
    ///
    /// The artifact cannot be produced by CI or by anyone without the physical
    /// device, so a hard failure would leave the suite permanently red for
    /// every other contributor and every other task. But a SILENT skip is the
    /// worse failure mode this repo already knows about - run-unity-tests.sh
    /// carries a comment about a stale results file printing a green summary
    /// beside a non-zero exit, and calls a stale pass worse than a failure.
    ///
    /// So the skip is loud: it names what is missing and how to produce it, and
    /// the Definition of Done requires Skipped: 0, which means "skipped" can
    /// never be mistaken for "passed" when the phase is signed off.
    public sealed class DeviceArtifactFactAttribute : FactAttribute
    {
        public DeviceArtifactFactAttribute()
        {
            if (!ReplayArtifact.Present(ReplayArtifact.DeviceBin))
                Skip = "implementation/results/" + ReplayArtifact.DeviceBin + " is absent. " +
                       "It is produced by running wave 6 on a physical device and pulling " +
                       "the artifact - Task 10 Steps 5-6 of the Phase 3 plan. Phase 3's " +
                       "Definition of Done requires this test to RUN, not to skip.";
        }
    }

    /// Phase 3's done-when: "a wave played on device replays bit-identically
    /// in xUnit."
    ///
    /// This proves something the corpus does not. cross-runtime-diff.sh proves
    /// CoreCLR and IL2CPP agree on generated scenarios run headlessly - that is
    /// arithmetic portability. This proves a wave run through a RENDERER, at a
    /// variable frame rate, with a human tap in it, consumed exactly the inputs
    /// its replay claims. Frame-pacing correctness, not arithmetic. Neither
    /// implies the other.
    public class DeviceReplayTests
    {
        [DeviceArtifactFact]
        public void TheDeviceRunReSimulatesToTheSameHash()
        {
            var record = Replay.Deserialize(File.ReadAllBytes(ReplayArtifact.Path(ReplayArtifact.DeviceBin)));
            record.Validate();

            var lines = File.ReadAllLines(ReplayArtifact.Path(ReplayArtifact.DeviceOutcome));
            Assert.True(lines.Length >= 4,
                "device-replay-outcome.txt should carry hash, result, ticks and integrity on four lines");

            ulong deviceHash = ulong.Parse(lines[0]);
            var deviceResult = (Result)int.Parse(lines[1]);
            int deviceTicks = int.Parse(lines[2]);
            int deviceIntegrity = int.Parse(lines[3]);

            var replayed = Broodline.Sim.Combat.Sim.Replay(record);

            // The whole claim, in four asserts. A mismatch here means the
            // device consumed something the record does not describe - the
            // likeliest cause being a dropped tick or a Rally recorded against
            // wall-clock rather than a tick index.
            Assert.Equal(deviceHash, replayed.Hash);
            Assert.Equal(deviceResult, replayed.Result);
            Assert.Equal(deviceTicks, replayed.Ticks);
            Assert.Equal(deviceIntegrity, replayed.IntegrityRemaining);
        }

        [DeviceArtifactFact]
        public void TheDeviceRunWasRecordedByThisEngineVersion()
        {
            // solo_execution section 9.4: a replay from a superseded engine
            // shows its recorded outcome and is not re-simulated. If this fails,
            // the test above is comparing across a balance change and its
            // verdict means nothing.
            var record = Replay.Deserialize(File.ReadAllBytes(ReplayArtifact.Path(ReplayArtifact.DeviceBin)));
            Assert.Equal(SimVersion.Value, record.EngineVersion);
        }
    }
}
