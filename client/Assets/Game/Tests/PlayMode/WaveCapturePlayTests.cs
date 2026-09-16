using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Broodline.Game;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Sim.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// The tracked-capture procedure, run.
///
/// `Assets/Scenes/Wave.unity` played on its own is the Editor half of the
/// replay round-trip (Task 10 of the Phase 3 plan). A person opens it, presses
/// Play, taps once, and copies `replay.bin` and `replay-outcome.txt` out of
/// `Application.persistentDataPath` into `implementation/results/`, where
/// `EditorReplayTests` re-simulates them on every `dotnet test` run.
///
/// TASK 16 IS THE FIRST THING THAT COULD BREAK THAT SILENTLY. It took the
/// wave, the deployment and the seed away from `WaveRunner` and gave them to
/// `WaveHost`, behind a `standaloneCapture` flag - and a scene that stops
/// writing the artifacts still plays, still renders, and reports nothing. The
/// break would surface at the next capture session, on hardware, with a person
/// waiting.
///
/// The EditMode half (`WaveRunnerTests`) asserts the flag's rule and that the
/// scene asset still carries it. Neither can press Play, and pressing Play is
/// the part that was worth doubting. This does.
public class WaveCapturePlayTests
{
    const string SceneName = "Wave";

    /// Long enough for wave 6 (540 ticks) at the accelerated rate below, with
    /// room for a slow batchmode frame rate. Not open-ended: a wave that never
    /// terminates must fail this test rather than hang the run.
    const float TimeoutSeconds = 120f;

    /// `WaveClock` caps catch-up at `MaxCatchUpSteps` (8) ticks per frame, so
    /// a scale past ~16x at 60fps buys nothing. It changes the PACING, not the
    /// simulation: the engine sees a fixed timestep either way, which is the
    /// whole point of the accumulator - and the assertions below prove it, by
    /// re-simulating the record headlessly and demanding the same hash.
    const float TimeScale = 16f;

    string _artifactDirectory;
    string _replayPath;
    string _outcomePath;
    byte[] _previousReplay;
    string _previousOutcome;

    [SetUp]
    public void MoveAnyExistingCaptureAside()
    {
        _artifactDirectory = Application.persistentDataPath;
        _replayPath = Path.Combine(_artifactDirectory, "replay.bin");
        _outcomePath = Path.Combine(_artifactDirectory, "replay-outcome.txt");

        // A real capture may be sitting here uncollected. This test writes a
        // run with no tap in it, which would be a WORSE capture than the one
        // it replaced - so the old one is held and put back afterwards.
        _previousReplay = File.Exists(_replayPath) ? File.ReadAllBytes(_replayPath) : null;
        _previousOutcome = File.Exists(_outcomePath) ? File.ReadAllText(_outcomePath) : null;

        if (File.Exists(_replayPath)) File.Delete(_replayPath);
        if (File.Exists(_outcomePath)) File.Delete(_outcomePath);

        WaveRunner.Hosted = false;
    }

    [TearDown]
    public void RestoreTheCaptureAndTheClock()
    {
        Time.timeScale = 1f;
        WaveRunner.Hosted = false;

        if (_previousReplay != null) File.WriteAllBytes(_replayPath, _previousReplay);
        if (_previousOutcome != null) File.WriteAllText(_outcomePath, _previousOutcome);
    }

    [UnityTest]
    public IEnumerator PlayingTheWaveSceneWithNoHost_StillWritesACaptureOfWave6()
    {
        yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);

        var runner = Object.FindAnyObjectByType<WaveRunner>();
        Assert.IsNotNull(runner, "the wave scene must carry a WaveRunner");
        Assert.IsTrue(runner.StandaloneCapture,
            "nothing hosted this load, so the scene must own its own deployment and the artifacts");

        // The HUD is design section 4's "one piece of existing presentation
        // that a tester sees", and the capture procedure reads the live tick
        // off it. Asserted from the scene rather than trusted: `WaveRunner`
        // logs an error and carries on when the document is missing, and a
        // wave that renders but shows nothing would otherwise pass every
        // assertion below.
        var document = runner.GetComponent<UIDocument>();
        Assert.IsNotNull(document, "the wave scene must carry a UIDocument for the HUD");
        Assert.IsNotNull(document.panelSettings, "the HUD's document has no panel to draw into");

        // One frame, so the document builds its root and WaveRunner.Update
        // has attached the HUD to it.
        yield return null;
        Assert.Greater(document.rootVisualElement.childCount, 0,
            "nothing was attached to the wave scene's UI document - the HUD did not build");

        Time.timeScale = TimeScale;

        var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!File.Exists(_outcomePath) && Time.realtimeSinceStartup < deadline)
            yield return null;

        Time.timeScale = 1f;

        Assert.IsTrue(File.Exists(_replayPath),
            "playing Wave.unity with no host must still write replay.bin - the tracked-capture " +
            "procedure (Task 10 of the Phase 3 plan) has no other source.");
        Assert.IsTrue(File.Exists(_outcomePath), "replay-outcome.txt was not written");

        var record = Replay.Deserialize(File.ReadAllBytes(_replayPath));
        Assert.AreEqual(WaveRunner.CaptureWaveId, record.WaveId);
        Assert.AreEqual(WaveRunner.Seed, record.Seed);
        Assert.AreEqual(Broodline.Sim.Combat.Terrain.Defile, record.Terrain);
        Assert.AreEqual(5, record.Deployment.Length);
        foreach (var creature in record.Deployment)
        {
            Assert.AreNotEqual(Trait.Chill, creature.Trait1);
            Assert.AreNotEqual(Trait.Chill, creature.Trait2);
        }

        // THE ROUND TRIP, in miniature: what the renderer's variable-rate run
        // recorded, re-simulated at a fixed timestep, must agree exactly. This
        // is the property `EditorReplayTests` proves about the COMMITTED
        // artifact; proving it about a freshly-played one is what says the
        // scene is still capable of producing a committable capture.
        var lines = File.ReadAllLines(_outcomePath);
        Assert.GreaterOrEqual(lines.Length, 4,
            "replay-outcome.txt carries hash, result, ticks and integrity on four lines");

        var replayed = Broodline.Sim.Combat.Sim.Replay(record);
        Assert.AreEqual(ulong.Parse(lines[0]), replayed.Hash);
        Assert.AreEqual((int)replayed.Result, int.Parse(lines[1]));
        Assert.AreEqual(replayed.Ticks, int.Parse(lines[2]));
        Assert.AreEqual(replayed.IntegrityRemaining, int.Parse(lines[3]));

        // waves_01_12 section 3 designs wave 6 as the beat that teaches the
        // counter system, so a Loss here is the wave working.
        Assert.AreEqual(Result.Loss, replayed.Result);

        yield return SceneManager.UnloadSceneAsync(SceneName);
    }

    /// The other half of the same flag, and the only coverage `WaveHost` has:
    /// a wave the SHELL started must not deploy wave 6 over the player's own
    /// roster, and must not overwrite the capture artifacts with a run nobody
    /// asked to record.
    ///
    /// It also exercises the one ordering this task could get wrong in a way
    /// no EditMode test can reach: `WaveHost` sets `WaveRunner.Hosted` BEFORE
    /// the additive load, because the scene's components come alive during it.
    /// Set it after, and the runner deploys wave 6 by itself first.
    [UnityTest]
    public IEnumerator AHostedWave_ReportsItsOutcomeAndWritesNoCapture()
    {
        Time.timeScale = TimeScale;

        var host = new WaveHost(() => Bundle);
        var run = host.RunAsync(
            WaveRunner.CaptureWaveId, WaveRunner.Deployment(), WaveRunner.Seed, inputEnabled: false);

        var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (!run.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;

        Time.timeScale = 1f;
        Assert.IsTrue(run.IsCompleted, "WaveHost.RunAsync never completed");
        Assert.IsNull(run.Exception, run.Exception == null ? "" : run.Exception.ToString());

        var report = run.Result;
        Assert.AreEqual("Loss", report.Result);
        Assert.AreEqual(1, report.Breaches.Count);
        Assert.AreEqual("Courser", report.Breaches[0].RaiderType);
        Assert.AreEqual("Chill", report.Breaches[0].Counter, "the counter comes from the bundle handed to the host");
        Assert.IsNotNull(report.ReplayBytes);
        Assert.AreEqual(WaveRunner.CaptureWaveId, Replay.Deserialize(report.ReplayBytes).WaveId);

        // NO ARTIFACTS. SetUp deleted any that were here, and a hosted run
        // must not have written new ones - `replay.bin` is a capture, not a
        // by-product of every wave a player ever plays.
        Assert.IsFalse(File.Exists(_replayPath),
            "a hosted wave must not overwrite the tracked-capture artifacts");
        Assert.IsFalse(File.Exists(_outcomePath));

        // Design section 4: Wave Defense "loads additively and unloads on
        // exit". A host that left the battlefield resident would keep the one
        // per-frame budget in the app alive behind every other screen.
        Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded);
        Assert.IsFalse(WaveRunner.Hosted, "the hosted latch must be cleared when the run ends");
    }

    /// `config.traits` from `/v1/sync`, as the shell would hand it over. The
    /// answering trait is deliberately not first - see `WaveReportTests`.
    static IReadOnlyList<TraitSummary> Bundle => new[]
    {
        new TraitSummary { Id = "Taunt", Species = "Vetch", Counters = "Lash" },
        new TraitSummary { Id = "Chill", Species = "Pale", Counters = "Courser" },
    };
}
