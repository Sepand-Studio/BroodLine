using Xunit;
using Xunit.Abstractions;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class GoldenTests
    {
        private readonly ITestOutputHelper _out;
        public GoldenTests(ITestOutputHelper output) { _out = output; }

        public const ulong Seed = 6;

        // Pinned 2026-09-10. Re-baselined the same day when Rally joined the
        // state vector: CreatureRallyUntil is folded every tick, so every hash
        // moved even on runs where Rally is never used. Confirmed first that
        // both tests still failed ONLY on their final hash assertion, with the
        // verdict, breach count, diagnosis and integrity all passing above it.
        // A change here is a balance change or a bug - never update these to
        // match new output without knowing which.
        private const ulong GoldenAHash = 2495532238167386945;
        private const ulong GoldenBHash = 13482666686023521257;

        /// Golden A - wave 6 exactly as authored: five creatures, none
        /// carrying Chill, designed to be lost.
        ///
        /// Four Vetch and a Loam. Chosen for two reasons. It is a plausible
        /// pre-wave-6 roster - Vetch is the starter wall - and it dramatises the
        /// exact lesson the wave exists to teach, which waves_01_12 section 3
        /// states outright: "Courser ignores Taunt, so the Vetch does not save
        /// them."
        ///
        /// Chosen for MARGIN as well as verdict. The Courser breaches with 54 of
        /// its 220 hp remaining, so the Loss does not hinge on a damage race that
        /// an implementation detail could flip. A longer-ranged roster does not
        /// merely narrow that margin, it reverses the outcome: a Hollow running
        /// Overwatch covers 15 of the lane's 24 tiles and kills the Courser on its
        /// own, which would make this wave a clear and the golden pair meaningless.
        public static CreatureSpec[] DeploymentWithoutChill() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        /// Golden B - the Pale the player is granted on defeat, carrying Chill I,
        /// standing in the Loam's pocket. waves_01_12 section 3: "The Wave Defeat
        /// screen grants a Pale."
        ///
        /// The Courser dies at tile 16 of 24 - eight tiles of margin - so the Win
        /// does not hinge on a damage race either.
        ///
        /// This pair changes the creature as well as the trait, and that is the
        /// authored narrative rather than sloppy method: the player has no Pale at
        /// wave 6 and is granted one for losing. A same-body control (this roster
        /// with the Pale carrying NO trait) was evaluated and deliberately rejected
        /// as a golden - it loses by 3 hp of 220, a margin thin enough that
        /// ordinary implementation detail would flip it, which is exactly the
        /// property a pinned golden must not have. Chill's causality is isolated in
        /// unit tests instead, where it belongs: Task 5 asserts the speed change
        /// directly and Task 4 asserts the capacity assignment.
        public static CreatureSpec[] DeploymentWithChill()
        {
            var d = DeploymentWithoutChill();
            d[4] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 4, Instinct = Instinct.Vanguard,
                Trait1 = Trait.Chill, Tier1 = 1
            };
            return d;
        }

        [Fact]
        public void GoldenA_Wave6WithoutChillIsLostToAnUnanswerableCourser()
        {
            // Fully qualified: "Sim" unqualified inside Broodline.Sim.Tests.*
            // resolves to the Broodline.Sim namespace (a direct child of
            // Broodline, matched before the "using Broodline.Sim.Combat;"
            // import is ever consulted) rather than to Broodline.Sim.Combat.Sim.
            var o = Broodline.Sim.Combat.Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            _out.WriteLine("Golden A hash: " + o.Hash + "  ticks: " + o.Ticks);

            Assert.Equal(Result.Loss, o.Result);
            Assert.Equal(1, o.BreachCount);
            Assert.Equal(RaiderType.Courser, o.Breaches[0].Type);

            // The diagnosis is the point of the wave: the trait is ABSENT, not
            // mis-tiered and not mis-placed.
            Assert.False(o.Breaches[0].Access);

            Assert.Equal(GoldenAHash, o.Hash);
        }

        [Fact]
        public void GoldenB_TheSameWaveWithChillIsCleared()
        {
            var o = Broodline.Sim.Combat.Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithChill(), Seed);
            _out.WriteLine("Golden B hash: " + o.Hash + "  ticks: " + o.Ticks);

            Assert.Equal(Result.Win, o.Result);
            Assert.Equal(0, o.BreachCount);
            Assert.Equal(2, o.IntegrityRemaining);

            Assert.Equal(GoldenBHash, o.Hash);
        }

        [Fact]
        public void Run_IsReproducible()
        {
            var a = Broodline.Sim.Combat.Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            var b = Broodline.Sim.Combat.Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            Assert.Equal(a.Hash, b.Hash);
        }

        [Fact]
        public void PreWaveCheck_AgreesWithWhatActuallyHappens()
        {
            // The panel must not tell a player they are covered and then lose
            // the wave to access. Section 7's "one function, two call sites"
            // is what this asserts end to end: the SAME Access verdict must
            // come back whether it is read off the pre-wave panel or off the
            // loss screen produced by an actual Sim.Run.
            var noChill = new SimState(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill());
            Assert.False(Diagnosis.PreWaveCheck(noChill, RaiderType.Courser).Answered);

            var withChill = new SimState(WaveDef.Wave6(), Lane.Defile(), DeploymentWithChill());
            Assert.True(Diagnosis.PreWaveCheck(withChill, RaiderType.Courser).Answered);

            var o = Broodline.Sim.Combat.Sim.Run(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill(), Seed);
            var preWave = new SimState(WaveDef.Wave6(), Lane.Defile(), DeploymentWithoutChill());
            Assert.Equal(1, o.BreachCount);
            Assert.Equal(
                Diagnosis.PreWaveCheck(preWave, RaiderType.Courser).Access,
                o.Breaches[0].Access);
        }
    }
}
