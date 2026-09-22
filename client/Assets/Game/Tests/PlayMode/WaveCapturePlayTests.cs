using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Broodline.Game;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Sim.Combat;
using Broodline.UI.Screens;
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
/// The EditMode half (`WaveRunnerTests`) asserts the flag's rule, the reset
/// hook that survives a disabled domain reload, and that the scene asset still
/// carries the default. None of that can press Play, and pressing Play is the
/// part that was worth doubting. This does.
public class WaveCapturePlayTests
{
    const string SceneName = "Wave";

    /// `WaveClock` caps catch-up at `MaxCatchUpSteps` (8) ticks per frame, and
    /// `Time.deltaTime` is itself capped at `Time.maximumDeltaTime` (0.333s,
    /// i.e. 10 ticks), so a scale past ~16x buys nothing. It changes the
    /// PACING, not the simulation: the engine sees a fixed timestep either
    /// way, which is the whole point of the accumulator - and the assertions
    /// below prove it, by re-simulating the record headlessly and demanding
    /// the same hash.
    const float TimeScale = 16f;

    /// FRAMES, NOT SECONDS, and the unit is the point.
    ///
    /// A wall-clock deadline makes this test's outcome depend on how fast the
    /// machine is, which is how a suite acquires a flaky gate that fails once
    /// a fortnight on CI and gets muted. The work here is bounded in FRAMES
    /// instead, and bounded from both ends: a slow frame runs at most 8 ticks
    /// (the catch-up cap), so wave 6's 540 ticks need at least 68 frames; a
    /// fast frame runs `unscaledDelta * 16 * 30` ticks, so even a 1ms
    /// batchmode frame is ~0.5 ticks and needs ~1125. This budget is five
    /// times the latter, and a machine slow enough to matter needs FEWER
    /// frames rather than more.
    const int FrameBudget = 6000;

    string _replayPath;
    string _outcomePath;
    byte[] _previousReplay;
    string _previousOutcome;

    [SetUp]
    public void MoveAnyExistingCaptureAside()
    {
        var directory = Application.persistentDataPath;
        _replayPath = Path.Combine(directory, "replay.bin");
        _outcomePath = Path.Combine(directory, "replay-outcome.txt");

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

    /// Yields until `until` holds, or fails after `FrameBudget` frames.
    static IEnumerator Until(Func<bool> until, string whatDidNotHappen)
    {
        for (var frame = 0; frame < FrameBudget; frame++)
        {
            if (until()) yield break;
            yield return null;
        }
        Assert.Fail(whatDidNotHappen + " within " + FrameBudget + " frames");
    }

    static IList<VisualElement> BarsIn(VisualElement root) =>
        root.Query<VisualElement>(className: WaveHudView.BarUssClassName).ToList();

    [UnityTest]
    public IEnumerator PlayingTheWaveSceneWithNoHost_StillWritesACaptureOfWave6()
    {
        yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);

        var runner = UnityEngine.Object.FindAnyObjectByType<WaveRunner>();
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

        // One frame, so WaveRunner.Update has configured the wave and attached
        // the HUD.
        yield return null;
        var root = document.rootVisualElement;
        Assert.Greater(root.childCount, 0,
            "nothing was attached to the wave scene's UI document - the HUD did not build");

        // THE LITERAL THAT IS LOAD-BEARING AND COMPILES EITHER WAY. A capture
        // with no Rally in it is an invalid artifact - `EditorReplayTests`
        // asserts the record carries a tap at a tick index and
        // `DeviceReplayTests` asserts its window overlaps the engagement - so
        // the standalone path must leave input on. Flipping it breaks nothing
        // that runs; it fails on hardware, with a person waiting.
        Assert.IsTrue(runner.InputEnabled,
            "the standalone capture path must accept the tap the capture is FOR");

        Time.timeScale = TimeScale;

        // `WaveRunner.Snapshot` has no other coverage, and no EditMode test
        // can reach it: it needs a live SimRunner and a WavePair. Five
        // creatures deploy at tick 0, so the HUD carries five bars before the
        // Courser exists...
        Assert.AreEqual(5, BarsIn(root).Count,
            "the HUD must draw a bar per deployed creature from the first frame");

        // ...and six once it spawns, which is what says the snapshot tracks
        // live raider visibility rather than echoing the deployment.
        yield return Until(() => BarsIn(root).Count > 5, "the Courser never appeared on the HUD");

        // And the readout ADVANCES. `DeviceReplayTests` tells the capturer to
        // aim a tap by reading the live tick off the HUD; a readout that froze
        // at its first value is a capture aimed with a stopped clock, and the
        // scheduled per-frame redraw is only reachable with a real panel.
        //
        // THE TICK IS ITS OWN ELEMENT AS OF PHASE 9 TASK 21e AND THIS BLOCK
        // FOLLOWED IT. It was appended to `#integrity`, whose caption is
        // "ARK INTEGRITY", so the packaged app printed "2 tick 155" as the
        // value of the loss condition and the exit gate's walk named it a
        // defect. Watching `#integrity` for a CHANGE would now fail on a
        // TIMEOUT rather than on a wrong value - integrity is a pool that
        // moves a handful of times in a wave, and this one holds a constant 2
        // for roughly 500 of 540 ticks - so the watch is on `#tick`, which is
        // the element that actually advances and the element the re-capture
        // message now names.
        //
        // ASSERTED TO EXIST BEFORE IT IS WATCHED. A `Q` that missed would
        // return null and the `Until` below would throw on the first frame
        // rather than time out, which is a slower and less obvious way to say
        // the same thing.
        var integrity = root.Q<Label>("integrity");
        Assert.IsNotNull(integrity, "the HUD has no #integrity readout");
        var tick = root.Q<Label>("tick");
        Assert.IsNotNull(tick,
            "the HUD has no #tick readout - the device re-capture procedure reads the live "
            + "tick off this element to time a tap (WaveHudScreen.Tick)");

        var before = tick.text;
        Assert.IsNotEmpty(before);
        yield return Until(() => tick.text != before, "the HUD's tick readout never advanced");

        // WHAT IS *NOT* ASSERTED HERE, deliberately: that the text equals
        // `WaveHudScreen.Integrity(...)` of the runner's current tick. The
        // redraw is scheduled on the panel and the runner steps in `Update`,
        // so a comparison against a tick read from this coroutine is one frame
        // out roughly half the time - a genuinely flaky assertion. It was
        // written, it failed exactly that way, and it is not being weakened
        // with a tolerance: the verbatim property belongs to
        // `WaveScreensTests.WaveHud_ShowsTheIntegrityAndTickLineVerbatim_AndItMoves`,
        // where the snapshot is handed over rather than raced. What is left
        // here is what only a live panel can show - that the scheduler pulls
        // a NEW snapshot every frame at all.
        //
        // THE INTEGRITY HALF IS A DIFFERENT QUESTION, AND IT *IS* ASSERTABLE.
        // Deleting the flaky assertion above took the tick half AND the
        // integrity half with it, and that left nothing anywhere pinning that
        // `WaveRunner.Snapshot()` fills `Integrity` and `Tick` FROM THE LIVE
        // RUNNER: transpose those two assignments and every EditMode test
        // stays green (their snapshots are hand-built) and every PlayMode
        // assertion above stays green too (they only ask that the line is
        // non-empty and that it moves).
        //
        // The tick is what made the old assertion race; integrity does not
        // race, because it does not move. Wave 6 is one Courser against
        // integrity 2, and nothing touches integrity until that Courser
        // breaches at ~tick 540 - so for ~500 of 540 ticks the value is a
        // constant 2, a stale frame reads the same 2, and the comparison
        // needs no tolerance. It was asserted on the PREFIX while the tick
        // followed it on the same line; Task 21e moved the tick off this
        // element, so the assertion below is an equality.
        var live = runner.Runner.Integrity;
        // CONTRASTIVE, and the reason the prefix check above means anything:
        // a transposed Snapshot() prints the TICK where integrity belongs, so
        // the assertion can only discriminate while the two differ. They do -
        // the Courser has spawned, so the tick is past 90 - and stating it
        // here is what stops a future edit moving this block earlier, to a
        // tick of 2, and quietly making it vacuous.
        Assert.AreNotEqual(live, runner.Runner.Tick,
            "integrity and tick must differ here or the equality below cannot tell them apart");
        // THE WORD "Integrity" LEFT THIS LINE IN PHASE 9 TASK 18 and this
        // assertion did not follow it; THE TICK LEFT IT IN TASK 21e and this
        // one did. Both were missed the same way and it is worth the sentence:
        // PlayMode deadlocks in batchmode on this Editor, so no task that
        // changes the HUD can run this file, and the only thing standing
        // between a stale assertion here and a wasted device trip is the
        // author reading it. Task 18's was caught by the human's Editor pass.
        //
        // `#integrity` IS NOW EXACTLY THE INTEGRITY, so this is an equality
        // rather than a prefix. The prefix form existed because the tick used
        // to follow the number on the same line and is the half that races -
        // the redraw is scheduled on the panel and the runner steps in
        // `Update`, so a tick read from this coroutine is one frame out about
        // half the time. Integrity does not race: wave 6 is one Courser
        // against integrity 2 and nothing touches it until that Courser
        // breaches at ~tick 540, so a stale frame reads the same 2 and the
        // comparison needs no tolerance. The contrastive guard above is what
        // keeps this honest - a transposed `Snapshot()` would print the tick
        // here, and the two provably differ at this point in the wave.
        Assert.AreEqual(live.ToString(CultureInfo.InvariantCulture), integrity.text,
            "the HUD's integrity readout must come from the live runner - WaveRunner.Snapshot()");

        // AND THE TICK IS STILL ON SCREEN, on its own element. Not asserted
        // against the runner's current tick, for the race named above; what is
        // asserted is that the readout the re-capture message points a person
        // at exists, names itself, and carries digits. The `Until` above
        // already proved it advances.
        StringAssert.Contains("tick", tick.text,
            "the tick readout must name itself - the device re-capture procedure tells a person "
            + "to read the live tick off the HUD to aim a tap");
        StringAssert.IsMatch(@"\d", tick.text,
            "the tick readout carries the word but no number, so there is nothing to aim by");

        yield return Until(() => runner.Runner.Done, "the wave never terminated");
        yield return null;   // the Update that writes the artifacts

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
        // The bool alone only proves a hide happened, not WHEN - the defect
        // this test exists to catch (hiding before the additive load) would
        // record the same `false` here. Pairing it with whether the wave
        // scene is resident AT THE MOMENT the callback fires is what makes
        // the mid-wave assertion below a real discriminator.
        var visibility = new List<(bool Visible, bool SceneLoaded)>();
        var host = new WaveHost(
            () => Bundle,
            visible => visibility.Add((visible, SceneManager.GetSceneByName(SceneName).isLoaded)));
        var run = host.RunAsync(
            WaveRunner.CaptureWaveId, WaveRunner.Deployment(), WaveRunner.Seed, inputEnabled: false);

        // Caught mid-wave, at real time, BEFORE the clock is accelerated -
        // the runner and its scene are gone by the time the task completes.
        WaveRunner hosted = null;
        yield return Until(() => (hosted = UnityEngine.Object.FindAnyObjectByType<WaveRunner>()) != null &&
                                 hosted.Runner != null,
                           "WaveHost never configured a runner");

        // Phase 9 design §2.2: hidden exactly once, and the scene is ALREADY
        // resident when it happens - `SceneLoaded: true` here is what rules
        // out the reverted ordering (hide before the load), which would
        // record `SceneLoaded: false` instead.
        CollectionAssert.AreEqual(new[] { (false, true) }, visibility,
            "the shell must be hidden exactly once, after the wave scene is already loaded");

        Assert.IsFalse(hosted.StandaloneCapture,
            "a hosted wave must not own the capture artifacts");
        Assert.IsFalse(hosted.InputEnabled,
            "inputEnabled:false must reach the runner - design section 5 beat 2 is a wave the " +
            "player watches");

        Time.timeScale = TimeScale;
        yield return Until(() => run.IsCompleted, "WaveHost.RunAsync never completed");
        Time.timeScale = 1f;

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

        // The restore is the finally's guarded FIRST statement, ahead of
        // UnloadAsync - so at the moment it fires the scene is still
        // resident (`SceneLoaded: true`), and only the later, unrecorded
        // unload takes it down. This is unchanged from before this fix; it
        // proves the restore runs, not its ordering relative to the load,
        // which the mid-wave assertion above already covers.
        CollectionAssert.AreEqual(new[] { (false, true), (true, true) }, visibility,
            "the shell must be hidden after the load, then restored before the unload, when the run ends");
    }

    /// THE ERROR PATH, which is the one that stranded the player.
    ///
    /// `RunAsync`'s unload used to sit on the happy path, so every throw
    /// between the additive load and the report - a missing runner, an
    /// unauthored wave id, a `Configure` that rejects its arguments, the
    /// completion give-up - left `Wave.unity` resident on top of the shell
    /// permanently. `FtueDirector.FightAsync` catches, shows a notice and
    /// ends the walk, so nothing downstream ever unloads it either.
    ///
    /// Wave id 999 is the cheapest way in: `WaveDef.ForId` throws for it, and
    /// it throws as an ARGUMENT to `Configure`, which is after the scene has
    /// loaded and inside the `try`. That is the shape of all four.
    [UnityTest]
    public IEnumerator AHostedWaveThatThrows_StillUnloadsTheBattlefield()
    {
        // Same pairing as the completion-path test above, and for the same
        // reason: the bool alone cannot tell "hidden after the load" from
        // "hidden before it", which is the ordering this whole task is
        // about.
        var visibility = new List<(bool Visible, bool SceneLoaded)>();
        var host = new WaveHost(
            () => Bundle,
            visible => visibility.Add((visible, SceneManager.GetSceneByName(SceneName).isLoaded)));
        var run = host.RunAsync(999, WaveRunner.Deployment(), WaveRunner.Seed, inputEnabled: false);

        yield return Until(() => run.IsCompleted, "WaveHost.RunAsync never completed");

        // Read, not merely present: an unload that swallowed the real failure
        // and reported its own would pass an IsNotNull and tell the player
        // the wrong thing.
        Assert.IsNotNull(run.Exception, "an unauthored wave id must surface as a fault");
        Assert.IsInstanceOf<WaveCompositionException>(
            run.Exception.InnerException,
            "the ORIGINAL failure must reach the caller - the finally's unload must not replace it");

        Assert.IsFalse(SceneManager.GetSceneByName(SceneName).isLoaded,
            "a wave that threw must not leave the 3D battlefield loaded over the shell");
        Assert.IsFalse(WaveRunner.Hosted,
            "the hosted latch must be cleared on the error path too");

        // The white screen the reverted fix produced is consistent with
        // hiding before the load AND with the restore never running on the
        // throw path. Neither is true here: `SceneLoaded: true` on the first
        // entry says the hide happened after the scene loaded, and the
        // second entry says the finally's guarded restore still ran, even
        // though `Configure` never did.
        CollectionAssert.AreEqual(new[] { (false, true), (true, true) }, visibility,
            "hidden after the load with the scene resident, restored by the finally, even when Configure throws");
    }

    /// `config.traits` from `/v1/sync`, as the shell would hand it over. The
    /// answering trait is deliberately not first - see `WaveReportTests`.
    static IReadOnlyList<TraitSummary> Bundle => new[]
    {
        new TraitSummary { Id = "Taunt", Species = "Vetch", Counters = "Lash" },
        new TraitSummary { Id = "Chill", Species = "Pale", Counters = "Courser" },
    };
}
