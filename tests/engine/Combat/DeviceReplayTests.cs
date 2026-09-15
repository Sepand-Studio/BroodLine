using System.IO;
using Xunit;
using Xunit.Abstractions;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// Locates a replay artifact captured from a real run through the
    /// renderer. Shared by the device tests and the Editor tests, so there is
    /// one definition of where these live.
    internal static class ReplayArtifact
    {
        public const string DeviceBin = "device-replay.bin";
        public const string DeviceOutcome = "device-replay-outcome.txt";
        public const string EditorBin = "editor-replay.bin";
        public const string EditorOutcome = "editor-replay-outcome.txt";

        /// The engine version the tracked captures were recorded under, READ
        /// OUT OF THE BYTES.
        ///
        /// This was a hand-maintained constant, and a constant that duplicates
        /// a fact already in the file is a claim with a maintenance cost and no
        /// enforcement. Worse, its own doc said a bump was "a two-line change:
        /// SimVersion.Value, and this" - and doing exactly that produced four
        /// failures, because this describes the ARTIFACTS, not the engine. It
        /// moves when someone re-captures, and at no other time. Deriving it
        /// removes the line to get wrong.
        public static string CapturedUnder => Read(DeviceBin).EngineVersion;

        /// Whether this engine can still re-simulate the tracked captures.
        public static bool AreCurrent => CapturedUnder == SimVersion.Value;

        /// What is owed once they are not.
        public static string ReCaptureOwed =>
            "The tracked captures were recorded under engine " + CapturedUnder +
            " and this engine is " + SimVersion.Value + ", so solo_execution 9.4 supersedes them " +
            "and the renderer round-trip is NOT being proven right now. Re-capture per Task 10 of " +
            "the Phase 3 plan: play Assets/Scenes/Wave.unity in the Editor and on device, with a " +
            "tap between ticks 184 and 240, then commit the four files in implementation/results/.";

        /// The supersession half of every test that re-simulates a capture.
        ///
        /// Returns true when the caller should stop. Written once because it
        /// was pasted into three places and forgotten in a fourth, which is how
        /// TheDeviceRunsRallyActuallyChangedTheSimulation ended up asserting
        /// against a stale capture while its siblings bailed out.
        public static bool Superseded(Replay record, ITestOutputHelper output)
        {
            if (AreCurrent) return false;

            // solo_execution 9.4, asserted rather than assumed: a superseded
            // record is REFUSED by the re-simulation path, and the refusal
            // names the version so a viewer can render the stored outcome with
            // a notice instead.
            var e = Assert.Throws<ReplayFormatException>(
                () => Broodline.Sim.Combat.Sim.Replay(record));
            Assert.Contains(record.EngineVersion, e.Message);

            output.WriteLine(ReCaptureOwed);
            return true;
        }

        public static string Path(string name)
        {
            var root = TestPaths.RepoRootOrNull();
            return root == null
                ? null
                : System.IO.Path.Combine(root, "implementation", "results", name);
        }

        /// Path or a diagnosable failure. Returning null let File.ReadAllBytes
        /// raise a bare ArgumentNullException, which points at nothing.
        public static string Require(string name)
        {
            var p = Path(name);
            Assert.True(p != null, "could not locate the repository root - the artifact path cannot be resolved");
            Assert.True(File.Exists(p), "missing tracked artifact: implementation/results/" + name);
            return p;
        }

        public static Replay Read(string name) =>
            Replay.Deserialize(File.ReadAllBytes(Require(name)));
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
                Assert.True(path != null, "could not locate the repository root");
                Assert.True(File.Exists(path),
                    "implementation/results/" + name + " is missing. It is tracked, so this means a " +
                    "committed artifact was deleted rather than that you lack the device. Restore it " +
                    "with git checkout, or re-capture it per Task 10 of the Phase 3 plan.");
            }
        }

        [Fact]
        public void TheTwoCapturesAgreeAboutTheirEngine()
        {
            // Catches the half-done re-capture: replace one artifact and not
            // the other and this fails, where every test that branches on
            // AreCurrent would keep passing on the stale one.
            Assert.Equal(ReplayArtifact.Read(ReplayArtifact.DeviceBin).EngineVersion,
                         ReplayArtifact.Read(ReplayArtifact.EditorBin).EngineVersion);
        }

        [Fact]
        public void TheTrackedCapturesAreCurrent()
        {
            // RESTORED 2026-09-14 by re-capturing both artifacts on hardware
            // under engine 0.2.0 - an iPhone 15 Pro for the device half, the
            // Unity 6 Editor for the other.
            //
            // From 2026-09-11 until then this test was
            // TheDeviceRunIsSupersededAndIsNotReSimulated and asserted the
            // OPPOSITE: that the captures were stale, pinned to "0.1.0". That
            // was the honest thing to write while it was true - saying outright
            // that the round-trip was dark beats a green suite implying
            // coverage it did not have - but it was always meant to be
            // temporary, and its own comment said so.
            //
            // WHY ONE ASSERT CARRIES MORE THAN IT LOOKS. Every re-simulating
            // test opens with `if (ReplayArtifact.Superseded(record, _out))
            // return;`. The moment AreCurrent goes false, all of them bail
            // BEFORE their assertions and report PASSED. Nothing counts that,
            // because there is no Skip anywhere in this solution and
            // `dotnet test` reporting "Skipped: 0" says nothing about it - the
            // README records exactly this trap. So when the engine next moves
            // past these artifacts, THIS test is the only thing that goes red.
            // It is the guard on the guard, and deleting it would take the
            // whole round-trip dark silently.
            Assert.True(ReplayArtifact.AreCurrent,
                "the tracked captures are engine " + ReplayArtifact.CapturedUnder + " and this engine is " +
                SimVersion.Value + ", so every round-trip test in this file is now bailing before its " +
                "assertions and reporting PASSED. " + ReplayArtifact.ReCaptureOwed);
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
        private readonly ITestOutputHelper _out;
        public DeviceReplayTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void TheDeviceRunReSimulatesToTheSameHash()
        {
            var record = ReplayArtifact.Read(ReplayArtifact.DeviceBin);
            if (ReplayArtifact.Superseded(record, _out)) return;

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
        public void TheDeviceRunsRallyActuallyChangedTheSimulation()
        {
            // A round-trip that reproduces the hash proves the tick index
            // survived the device. It does NOT prove Rally's arithmetic ran,
            // because CreatureRallyUntil is folded into the hash whether or not
            // the halved interval ever fires.
            //
            // The first two captures made that concrete: taps at ticks 373 and
            // 480 both round-tripped perfectly while being completely inert -
            // creature 0 only holds a target during ticks 184..240, and a tap
            // after that halves an interval nothing is using. Either capture
            // would still have passed with `interval /= 2` deleted.
            //
            // So this asserts the capture is WORTH having: re-simulate with the
            // recorded rally and without it, and require the damage landed to
            // differ. That is Attacks.IntervalTicks having executed on device.
            //
            // The rally fields are bounded before they get here - Deserialize
            // validates the format it produces, so RallyCreature is in range or
            // there is no Replay object at all.
            var record = ReplayArtifact.Read(ReplayArtifact.DeviceBin);

            // The branch its three siblings had and this one did not. Without
            // it, a SimVersion bump left this test re-simulating a stale
            // capture on the current engine and failing with "re-capture with
            // the tap while the Courser is still BELOW the first defender" -
            // pointing at tap timing when the cause was that the engine moved.
            if (ReplayArtifact.Superseded(record, _out)) return;

            Assert.True(record.RallyTick >= 0,
                "the device capture records no Rally at all - re-capture with a tap, per Task 10");

            int withRally = HpAtBreach(record.RallyTick, record.RallyCreature);
            int without = HpAtBreach(-1, -1);

            Assert.True(withRally != without,
                "the device capture's Rally at tick " + record.RallyTick + " changed nothing: the Courser " +
                "reached the Ark at hp " + withRally + " either way. Re-capture with the tap while the " +
                "Courser is still BELOW the first defender - ticks 150..207, about two seconds after it " +
                "appears. Until then this artifact cannot detect an IL2CPP divergence in what Rally does.");
        }

        /// Courser HP on the last tick it is alive - the damage the roster
        /// landed. Rallying a creature that is actually engaged shows up here
        /// and nowhere else, because wave 6's breach tick is set by the
        /// Courser's movement rather than by damage.
        private static int HpAtBreach(int rallyTick, int rallyCreature)
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            int last = -1;
            while (true)
            {
                if (rallyTick >= 0 && r.Tick == rallyTick) r.TryRally(rallyCreature);
                if (!r.Step()) break;
                if (r.RaiderCount > 0 && r.RaiderAlive[0]) last = r.RaiderHp[0];
            }
            return last;
        }

    }
}
