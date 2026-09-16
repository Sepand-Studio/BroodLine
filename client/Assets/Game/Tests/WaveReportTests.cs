using System.Collections.Generic;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Sim.Combat;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// `WaveReportBuilder` - the one place an engine `Outcome` becomes data a
    /// screen can render.
    ///
    /// The property every test here stands for: **the report carries what the
    /// engine recorded and what the BUNDLE authored, and derives neither.**
    ///
    /// TWO DEVIATIONS FROM THE BRIEF'S SKETCH, both structural:
    ///
    /// 1. It sketches `WaveReport.From(runner, traits)` - a static on the
    ///    type. That does not compile. `WaveReport` is bound by
    ///    `Broodline.UI` views, `Broodline.Game` references `Broodline.UI`,
    ///    and Unity refuses the reference cycle that a `Broodline.UI` ->
    ///    `Broodline.Game` edge would create. So the DATA lives in
    ///    `Broodline.Model` (which both assemblies already reference and
    ///    which names no engine type) and the CONVERSION lives in
    ///    `Broodline.Game` as `WaveReportBuilder.From`.
    ///
    /// 2. This assembly gained a reference to `Broodline.Sim`, which it did
    ///    not have. It is the assembly permitted to have one - the Global
    ///    Constraint is that `Broodline.UI` and `Broodline.UI.Tests` never
    ///    do, and neither does.
    public class WaveReportTests
    {
        // ---------------------------------------------------------------
        // Fixtures. Every wave below is AUTHORED (`WaveDef.ForId` knows it),
        // because `SimRunner.SerializeRecord` refuses to write a record for
        // a wave a replay could not name - and the report carries a record.
        // ---------------------------------------------------------------

        /// Golden A: wave 6 exactly as authored, four Vetch and a Loam, none
        /// carrying Chill. `GoldenTests.DeploymentWithoutChill()` in the
        /// engine suite is the same five creatures, and `WaveRunner.
        /// Deployment()` is the same five again - this is the deployment the
        /// tracked capture uses.
        ///
        /// waves_01_12 section 3 designs it as a Loss: "Courser ignores
        /// Taunt, so the Vetch does not save them."
        static SimRunner Wave6WithoutChill()
        {
            var wave = WaveDef.Wave6();
            return Run(wave, WaveRunner.Deployment(), WaveRunner.Seed);
        }

        /// Wave 1 with a single Vetch carrying Splash I in pocket 0.
        ///
        /// Chosen because it is the only shape in the authored content that
        /// produces TWO breaches whose diagnoses DIFFER: the first is
        /// Access true / Coverage false, the second Access true / Coverage
        /// true, and both are Placement false. A builder that read breach 0
        /// and reused it, or that wrote a constant into any of the three
        /// booleans, cannot pass this - which a wave-6-only suite could not
        /// say, because wave 6's single breach is false on all three.
        static SimRunner Wave1UndercoveredSplash()
        {
            var wave = WaveDef.Wave1();
            return Run(wave, new[]
            {
                new CreatureSpec
                {
                    Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard,
                    Trait1 = Trait.Splash, Tier1 = 1,
                }
            }, 1UL);
        }

        /// Wave 1 with the full five. Wins, so the report has a verdict and
        /// no breaches.
        static SimRunner Wave1Won()
        {
            var wave = WaveDef.Wave1();
            var deployment = new CreatureSpec[5];
            for (var i = 0; i < 5; i++)
                deployment[i] = new CreatureSpec { Species = Species.Vetch, Pocket = i, Instinct = Instinct.Vanguard };
            return Run(wave, deployment, 1UL);
        }

        static SimRunner Run(WaveDef wave, CreatureSpec[] deployment, ulong seed)
        {
            var runner = new SimRunner(wave, wave.Lane, deployment, seed);
            while (runner.Step()) { }
            return runner;
        }

        /// The bundle's trait table, `config.traits` from `/v1/sync`.
        ///
        /// THE ANSWERING TRAIT IS DELIBERATELY NOT FIRST. A lookup that
        /// returned `traits[0].Id` - or the first entry with any `Counters`
        /// at all - passes a table where Chill leads and fails this one.
        static IReadOnlyList<TraitSummary> Bundle() => new[]
        {
            new TraitSummary { Id = "Carapace", Species = "Loam", Counters = null },
            new TraitSummary { Id = "Taunt", Species = "Vetch", Counters = "Lash" },
            new TraitSummary { Id = "Chill", Species = "Pale", Counters = "Courser" },
            new TraitSummary { Id = "Splash", Species = "Ember", Counters = "Skirmisher" },
        };

        // ---------------------------------------------------------------

        [Test]
        public void From_NamesTheRaiderAndTheTraitThatWouldHaveAnsweredIt()
        {
            // bible 4.11 and 9.3, the same sentence twice: Wave Defeat "names
            // the raider that broke through and the trait that would have
            // answered it". This is where both halves come from.
            var report = WaveReportBuilder.From(Wave6WithoutChill(), Bundle());

            Assert.AreEqual("Loss", report.Result);
            Assert.AreEqual(1, report.Breaches.Count);
            Assert.AreEqual("Courser", report.Breaches[0].RaiderType);
            Assert.AreEqual("Chill", report.Breaches[0].Counter);
            Assert.IsFalse(report.Breaches[0].Access, "no creature in golden A carries Chill at all");
        }

        [Test]
        public void From_TakesTheCounterFromTheBundleTableAndNotFromTheEngine()
        {
            // THE SHARPEST TEST IN THIS FILE. `Stats.CounterFor(Courser)` is
            // `Trait.Chill` and always will be, so a builder that asked the
            // engine passes the test above and fails this one. The bundle
            // here says something the engine does not, and the report must
            // say what the bundle says - that is what makes retuning a
            // counter a bundle publish rather than an App Review cycle.
            Assert.AreEqual(Trait.Chill, Stats.CounterFor(RaiderType.Courser),
                "sanity: the engine's own answer, which this test exists to NOT see");

            var retuned = new[]
            {
                new TraitSummary { Id = "Ward", Species = "Pale", Counters = "Courser" },
            };
            var report = WaveReportBuilder.From(Wave6WithoutChill(), retuned);

            Assert.AreEqual("Ward", report.Breaches[0].Counter);
        }

        [Test]
        public void From_ABundleThatAnswersNothing_NamesNoTraitRatherThanGuessing()
        {
            // bible 4.12 keeps the counter pool closed, so this is a content
            // gap rather than a legal state - but an empty string is a thing
            // `WaveDefeatScreen.Headline` can drop a clause for, and a wrong
            // trait name is not.
            var report = WaveReportBuilder.From(Wave6WithoutChill(), new TraitSummary[0]);

            Assert.AreEqual("Courser", report.Breaches[0].RaiderType);
            Assert.AreEqual(string.Empty, report.Breaches[0].Counter);
        }

        [Test]
        public void From_CarriesEachBreachesOwnDiagnosisRatherThanOneVerdictForTheWave()
        {
            // combat_engine section 7 records the diagnosis AT the breach,
            // because by the time the wave ends the board it judged is gone.
            // Two breaches, two different boards, two different answers.
            var runner = Wave1UndercoveredSplash();
            var report = WaveReportBuilder.From(runner, Bundle());

            Assert.AreEqual(2, report.Breaches.Count);
            Assert.AreEqual("Skirmisher", report.Breaches[0].RaiderType);
            Assert.AreEqual("Splash", report.Breaches[0].Counter);

            // Access true on BOTH - a live Splash carrier was on the field -
            // so a builder hardcoding false fails here while passing the
            // wave-6 tests above.
            Assert.IsTrue(report.Breaches[0].Access);
            Assert.IsTrue(report.Breaches[1].Access);

            // And coverage DIFFERS between them: Splash I covers one
            // Skirmisher, and there were more than one on the board at the
            // first breach and one at the second. This is the assertion that
            // a per-wave verdict, or breach 0 copied into every entry,
            // cannot satisfy.
            Assert.IsFalse(report.Breaches[0].Coverage);
            Assert.IsTrue(report.Breaches[1].Coverage);

            Assert.IsFalse(report.Breaches[0].Placement);
            Assert.IsFalse(report.Breaches[1].Placement);
        }

        [Test]
        public void From_TheDiagnosisIsTheEnginesOwn_FieldForField()
        {
            // The complement of the test above, read from the other side:
            // whatever the engine recorded is what the report carries, in
            // order, with no reordering and no re-derivation.
            var runner = Wave1UndercoveredSplash();
            var report = WaveReportBuilder.From(runner, Bundle());
            var outcome = runner.Outcome;

            // Asserted before the loop, because a loop over zero breaches
            // checks nothing and passes.
            Assert.AreEqual(2, outcome.BreachCount, "sanity: this fixture breaches twice");
            Assert.AreEqual(outcome.BreachCount, report.Breaches.Count);
            for (var i = 0; i < outcome.BreachCount; i++)
            {
                Assert.AreEqual(outcome.Breaches[i].Type.ToString(), report.Breaches[i].RaiderType);
                Assert.AreEqual(outcome.Breaches[i].Access, report.Breaches[i].Access);
                Assert.AreEqual(outcome.Breaches[i].Coverage, report.Breaches[i].Coverage);
                Assert.AreEqual(outcome.Breaches[i].Placement, report.Breaches[i].Placement);
            }
        }

        [Test]
        public void From_AWonWave_ReportsTheWinAndAnEmptyBreachListRatherThanNull()
        {
            // Without this, every assertion above is satisfied by a builder
            // that hardcodes "Loss". And `Breaches` must be EMPTY rather than
            // null: a screen that has to null-check before it can say "no
            // breaches" is one missing branch away from claiming one.
            var runner = Wave1Won();
            var report = WaveReportBuilder.From(runner, Bundle());

            Assert.AreEqual("Win", report.Result);
            Assert.IsNotNull(report.Breaches);
            Assert.AreEqual(0, report.Breaches.Count);
        }

        [Test]
        public void From_CarriesTheTicksAndIntegrityTheEngineReported()
        {
            var runner = Wave6WithoutChill();
            var report = WaveReportBuilder.From(runner, Bundle());

            Assert.AreEqual(runner.Outcome.Ticks, report.Ticks);
            Assert.AreEqual(runner.Outcome.IntegrityRemaining, report.IntegrityRemaining);

            // Pinned against the other wave too, so "carries Ticks" cannot be
            // satisfied by a field that happens to hold wave 6's number.
            var won = Wave1Won();
            Assert.AreNotEqual(runner.Outcome.Ticks, won.Outcome.Ticks,
                "sanity: the two fixtures must not agree, or the check below proves nothing");
            Assert.AreEqual(won.Outcome.Ticks, WaveReportBuilder.From(won, Bundle()).Ticks);
        }

        [Test]
        public void From_CarriesTheReplayRecordForTHISRun()
        {
            // The bytes are what the server re-simulates, so "non-empty" is
            // not enough - they have to describe this run. Read back rather
            // than compared to a second SerializeRecord() call, which would
            // only prove the method is deterministic.
            var report = WaveReportBuilder.From(Wave6WithoutChill(), Bundle());
            var record = Replay.Deserialize(report.ReplayBytes);

            Assert.AreEqual(WaveRunner.CaptureWaveId, record.WaveId);
            Assert.AreEqual(WaveRunner.Seed, record.Seed);
            Assert.AreEqual(5, record.Deployment.Length);
            foreach (var creature in record.Deployment)
            {
                Assert.AreNotEqual(Trait.Chill, creature.Trait1);
                Assert.AreNotEqual(Trait.Chill, creature.Trait2);
            }
        }
    }
}
