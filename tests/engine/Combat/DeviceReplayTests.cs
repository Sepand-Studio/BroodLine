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

        /// Path or a diagnosable failure. Returning null let File.ReadAllBytes
        /// raise a bare ArgumentNullException, which points at nothing.
        public static string Require(string name)
        {
            var p = Path(name);
            Assert.True(p != null,
                "could not locate Broodline.sln above " + AppContext.BaseDirectory +
                " - the artifact path cannot be resolved");
            Assert.True(File.Exists(p), "missing tracked artifact: implementation/results/" + name);
            return p;
        }
    }

    /// The artifacts are TRACKED, so absence means a committed file was
    /// deleted, not that a contributor lacks the hardware.
    ///
    /// This used to be a Fact attribute that set Skip when the file was
    /// missing, with a prose "Definition of Done requires Skipped: 0" as the
    /// compensating control. That inverted the gate: deleting the artifact that
    /// IS Phase 3's done-when produced "Failed: 0, Passed: 139, Skipped: 2" and
    /// exit 0, and nothing in CI looked at the skip count. A rule this repo
    /// cares about becomes a red test, the way EnforcementTests does it - not a
    /// sentence someone has to remember to check.
    public class ReplayArtifactPresenceTests
    {
        [Fact]
        public void BothRoundTripArtifactsArePresent()
        {
            foreach (var name in new[]
            {
                ReplayArtifact.DeviceBin, ReplayArtifact.DeviceOutcome,
                ReplayArtifact.EditorBin, ReplayArtifact.EditorOutcome
            })
            {
                var path = ReplayArtifact.Path(name);
                Assert.True(path != null, "could not locate the repository root from " + AppContext.BaseDirectory);
                Assert.True(File.Exists(path),
                    "implementation/results/" + name + " is missing. It is tracked, so this means a " +
                    "committed artifact was deleted rather than that you lack the device. Restore it " +
                    "with git checkout, or re-capture it per Task 10 of the Phase 3 plan.");
            }
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
        [Fact]
        public void TheDeviceRunReSimulatesToTheSameHash()
        {
            var record = Replay.Deserialize(File.ReadAllBytes(ReplayArtifact.Require(ReplayArtifact.DeviceBin)));
            record.Validate();

            var lines = File.ReadAllLines(ReplayArtifact.Require(ReplayArtifact.DeviceOutcome));
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

        [Fact]
        public void TheDeviceRunWasRecordedByThisEngineVersion()
        {
            // solo_execution section 9.4: a replay from a superseded engine
            // shows its recorded outcome and is not re-simulated. If this fails,
            // the test above is comparing across a balance change and its
            // verdict means nothing.
            var record = Replay.Deserialize(File.ReadAllBytes(ReplayArtifact.Require(ReplayArtifact.DeviceBin)));
            Assert.Equal(SimVersion.Value, record.EngineVersion);
        }
    }
}
