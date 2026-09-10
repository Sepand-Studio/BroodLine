using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// combat_engine section 8.1: "Every simulation terminates." Three of the
    /// eight raiders required soft-lock fixes during design - Delver, Bulwark
    /// and Breaker all had versions that could permanently block progress -
    /// and the engine is supposed to refuse to let a new one ship.
    ///
    /// A golden proves one run. These prove the property.
    public class FuzzTests
    {
        private static CreatureSpec[] RandomDeployment(ref Rng gen)
        {
            var d = new CreatureSpec[5];
            for (int c = 0; c < 5; c++)
            {
                int tier = gen.NextInt(4);   // 0 means the trait is absent
                d[c] = new CreatureSpec
                {
                    Species = (Species)gen.NextInt(6),
                    Pocket = c,
                    Instinct = (Instinct)gen.NextInt(6),
                    Trait1 = tier > 0 ? Trait.Chill : Trait.None,
                    Tier1 = tier
                };
            }
            return d;
        }

        private static WaveDef RandomWave(ref Rng gen, int index)
        {
            int count = 1 + gen.NextInt(6);
            var spawns = new SpawnEntry[count];
            int tick = 0;
            for (int i = 0; i < count; i++)
            {
                tick += gen.NextInt(90);            // ordered by construction
                spawns[i] = new SpawnEntry { Tick = tick, Type = RaiderType.Courser };
            }
            return new WaveDef(1000 + index, 1 + gen.NextInt(12), 1, spawns);
        }

        [Fact]
        public void EverySimulationTerminates_AndNeverStalls()
        {
            for (int i = 0; i < 500; i++)
            {
                var gen = new Rng((ulong)(i + 1));
                var wave = RandomWave(ref gen, i);
                var deployment = RandomDeployment(ref gen);

                var o = Broodline.Sim.Combat.Sim.Run(wave, Lane.Defile(), deployment, (ulong)(i + 1));

                Assert.NotEqual(Result.Running, o.Result);
                Assert.True(o.Ticks <= Stats.HardTickCap,
                    "run " + i + " exceeded the hard tick cap");

                // Stalled is a CONTENT bug, not a gameplay outcome. A valid
                // wave must never reach it - and every wave here is valid.
                Assert.NotEqual(Result.Stalled, o.Result);
            }
        }

        [Fact]
        public void EveryRunIsReproducibleFromItsInputs()
        {
            for (int i = 0; i < 200; i++)
            {
                var g1 = new Rng((ulong)(i + 1));
                var w1 = RandomWave(ref g1, i);
                var d1 = RandomDeployment(ref g1);

                var g2 = new Rng((ulong)(i + 1));
                var w2 = RandomWave(ref g2, i);
                var d2 = RandomDeployment(ref g2);

                Assert.Equal(
                    Broodline.Sim.Combat.Sim.Run(w1, Lane.Defile(), d1, (ulong)(i + 1)).Hash,
                    Broodline.Sim.Combat.Sim.Run(w2, Lane.Defile(), d2, (ulong)(i + 1)).Hash);
            }
        }

        [Fact]
        public void IntegrityNeverGoesUnnoticedNegativeWithoutALoss()
        {
            for (int i = 0; i < 300; i++)
            {
                var gen = new Rng((ulong)(i + 9001));
                var wave = RandomWave(ref gen, i);
                var o = Broodline.Sim.Combat.Sim.Run(wave, Lane.Defile(), RandomDeployment(ref gen), (ulong)i);

                if (o.IntegrityRemaining <= 0)
                    Assert.Equal(Result.Loss, o.Result);
            }
        }

        [Fact]
        public void AnUnopposedRaiderTerminatesPromptlyRatherThanCreepingToTheCap()
        {
            // Renamed and re-scoped from a "soft-lock" test that never reached
            // the stall detector at all. The lone Vetch has range 2 and the
            // Courser crosses essentially uncontested, so this is an ordinary
            // resolution at roughly 450 ticks. Still worth asserting -- a raider
            // that advances every tick must resolve promptly rather than creep
            // toward the 5400-tick cap -- but it is NOT a stall test, and naming
            // it one hid the fact that nothing covers StallTicks.
            //
            // THE STALL DETECTOR'S TRIP PATH IS UNREACHABLE IN THIS SLICE, and
            // that is expected rather than a defect. The only raider is a
            // Courser, which advances every tick even while chilled (0.5 t/s is
            // 0.0167 tiles per tick, never zero), so the fingerprint always
            // changes. combat_engine 8.1's soft-lock shape -- "a submerged
            // Delver with no Burrow present and nothing able to reach it" --
            // needs a raider that can stop moving, and none exists yet. Whoever
            // adds Delver owns the first genuine test of StallTicks.
            var wave = new WaveDef(9999, 99, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var d = new[]
            {
                new CreatureSpec { Species = Species.Vetch, Pocket = 4,
                                   Instinct = Instinct.Vanguard }
            };

            var o = Broodline.Sim.Combat.Sim.Run(wave, Lane.Defile(), d, 1);

            // Integrity 99 against one Courser costing 2: it breaches, and the
            // wave is still won because nothing remains and the table is spent.
            Assert.Equal(Result.Win, o.Result);
            Assert.Equal(97, o.IntegrityRemaining);
            Assert.True(o.Ticks < Stats.StallTicks * 2,
                "an advancing raider must resolve promptly, not creep toward the cap");
        }
    }
}
