using System.IO;
using Broodline.Sim.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Game.Tests
{
    /// The standalone capture path, which this task's whole risk sits on.
    ///
    /// `Assets/Scenes/Wave.unity` played on its own is the Editor half of the
    /// replay round-trip capture - Task 10 of the Phase 3 plan, re-simulated
    /// by `EditorReplayTests` on every `dotnet test` run. Task 16 took the
    /// wave choice away from `WaveRunner` and gave it to `WaveHost`, and the
    /// failure mode of getting that wrong is SILENT: the scene still plays,
    /// still renders, and simply stops writing `replay.bin` - which nobody
    /// discovers until the next capture session, on hardware, with a person
    /// waiting.
    ///
    /// So the gate is asserted from three directions: the rule itself, the
    /// scene asset that carries its default, and what the path still
    /// simulates.
    public class WaveRunnerTests
    {
        static string WaveScenePath => Path.Combine(Application.dataPath, "Scenes", "Wave.unity");

        [TearDown]
        public void ResetHostedFlag()
        {
            // Static, and every test below writes it. Left true, it would
            // make every later test in the whole EditMode run think a host
            // was present.
            WaveRunner.Hosted = false;
        }

        static WaveRunner NewRunner()
        {
            // `Awake` does NOT run for a component added in EditMode, which
            // is exactly why `StandaloneCapture` resolves its two facts live
            // rather than latching them there.
            var go = new GameObject("WaveRunnerTest");
            go.hideFlags = HideFlags.HideAndDontSave;
            return go.AddComponent<WaveRunner>();
        }

        [Test]
        public void AnUnhostedRunner_OwnsTheCapture()
        {
            WaveRunner.Hosted = false;
            Assert.IsTrue(NewRunner().StandaloneCapture,
                "playing Wave.unity on its own must still deploy wave 6 and write replay.bin");
        }

        [Test]
        public void AHostedRunner_DoesNot()
        {
            // The complement, and the half that matters for correctness: a
            // wave the shell started must not deploy wave 6 over the player's
            // own roster, and must not overwrite the capture artifacts with
            // a run nobody asked to record.
            WaveRunner.Hosted = true;
            Assert.IsFalse(NewRunner().StandaloneCapture);
        }

        [Test]
        public void TheHostedFlagIsReadLive_NotLatchedWhenTheComponentIsCreated()
        {
            // `WaveHost` sets `Hosted` BEFORE the additive load, because the
            // scene's components come alive during it. This proves the order
            // that matters cannot silently stop mattering: a runner that
            // resolved the flag at construction would answer `true` here.
            WaveRunner.Hosted = false;
            var runner = NewRunner();

            WaveRunner.Hosted = true;
            Assert.IsFalse(runner.StandaloneCapture);
        }

        [Test]
        public void TheWaveSceneStillLeavesTheCaptureFlagOn()
        {
            // The rule above is only half the guarantee; the other half is
            // the scene ASSET, which is what a person actually opens and
            // presses Play on. A `WaveSceneBuilder` change that flipped the
            // default - or a field rename that made the serialized value
            // orphaned - lands here and nowhere else.
            Assert.IsTrue(File.Exists(WaveScenePath), WaveScenePath + " is missing");
            var scene = File.ReadAllText(WaveScenePath);

            StringAssert.Contains("standaloneCapture: 1", scene,
                "Wave.unity must keep standaloneCapture on, or the tracked-capture procedure " +
                "(open the scene, press Play, collect replay.bin) silently stops producing anything.");
        }

        [Test]
        public void TheWaveSceneCarriesAHudDocumentWithItsPanelAttached()
        {
            // BootSceneBuilder records the failure this guards: assigning
            // `UIDocument.panelSettings` through the property does not
            // survive SaveScene under -batchmode, and the scene comes back
            // with `m_PanelSettings: {fileID: 0}` - a document that renders
            // nothing, with no error anywhere.
            var scene = File.ReadAllText(WaveScenePath);

            StringAssert.Contains("UnityEngine.UIElements.UIDocument", scene,
                "the wave scene needs a UIDocument for the UI Toolkit HUD");
            StringAssert.DoesNotContain("m_PanelSettings: {fileID: 0}", scene,
                "the wave scene's UIDocument has no PanelSettings, so its panel would draw nothing");
        }

        [Test]
        public void TheStandaloneCaptureStillSimulatesWave6sAuthoredLoss()
        {
            // What the capture path CAPTURES, asserted against the same
            // properties `EditorReplayTests` asserts about the artifact it
            // produces. Task 16 moved the wave id, the deployment and the
            // seed out of `Start` and into constants a host also uses; this
            // is what stops that move from quietly changing the run.
            var wave = WaveDef.ForId(WaveRunner.CaptureWaveId);
            var runner = new SimRunner(wave, wave.Lane, WaveRunner.Deployment(), WaveRunner.Seed);
            while (runner.Step()) { }
            var outcome = runner.Outcome;

            Assert.AreEqual(6, wave.Id);
            // Fully qualified: `Terrain` unqualified is ambiguous between
            // the engine's terrain family and `UnityEngine.Terrain`.
            Assert.AreEqual(Broodline.Sim.Combat.Terrain.Defile, wave.Lane.Family);
            Assert.AreEqual(5, WaveRunner.Deployment().Length);
            foreach (var creature in WaveRunner.Deployment())
            {
                Assert.AreNotEqual(Trait.Chill, creature.Trait1);
                Assert.AreNotEqual(Trait.Chill, creature.Trait2);
            }

            // waves_01_12 section 3 designs wave 6 as a loss - the beat that
            // teaches the counter system - so this is the wave working.
            Assert.AreEqual(Result.Loss, outcome.Result);
            Assert.AreEqual(1, outcome.BreachCount);
            Assert.AreEqual(RaiderType.Courser, outcome.Breaches[0].Type);
            Assert.IsFalse(outcome.Breaches[0].Access, "the trait is ABSENT, not mis-tiered and not mis-placed");
        }
    }
}
