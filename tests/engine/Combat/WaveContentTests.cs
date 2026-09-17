using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// The first hour hands a player specific creatures and promises specific
    /// outcomes. These pin the promises against the engine, so a content edit
    /// that breaks a beat goes red here rather than on a tester's phone.
    public class WaveContentTests
    {
        static CreatureSpec Spec(Species s, int pocket, Trait t1 = Trait.None, int tier1 = 0,
                                 Trait t2 = Trait.None, int tier2 = 0) =>
            new CreatureSpec { Species = s, Pocket = pocket, Trait1 = t1, Tier1 = tier1,
                               Trait2 = t2, Tier2 = tier2, Instinct = Instinct.Vanguard };

        /// starter.json's pair, exactly as Task 3 authors them.
        static CreatureSpec[] ColdOpenPair() => new[]
        {
            Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
            Spec(Species.Ember, 1, Trait.Splash, 1, Trait.Carapace, 1),
        };

        [Fact]
        public void Wave1_IsWonByTheColdOpenPair_InAnyTwoPockets()
        {
            // "The lane is a Defile - six pockets for two creatures - so no
            // placement is wrong." Every ordered pair of pockets, not one.
            var w = WaveDef.Wave1();
            for (int a = 0; a < 6; a++)
            for (int b = 0; b < 6; b++)
            {
                if (a == b) continue;
                var d = ColdOpenPair(); d[0].Pocket = a; d[1].Pocket = b;
                var o = Broodline.Sim.Combat.Sim.Run(w, w.Lane, d, 1);
                Assert.True(o.Result == Result.Win, "wave 1 lost with pockets " + a + "," + b);
            }
        }

        [Fact]
        public void Wave2_IsWonByTheTrio_WithTauntOnTheVetch()
        {
            // Beat 5: "the Vetch holds the Lash because Taunt is on it". The
            // Hollow is the Founder as Task 6 grants it - no traits.
            var w = WaveDef.Wave2();
            var d = new[]
            {
                Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
                Spec(Species.Ember, 2, Trait.Splash, 1, Trait.Carapace, 1),
                Spec(Species.Hollow, 4),
            };
            var o = Broodline.Sim.Combat.Sim.Run(w, w.Lane, d, 2);
            Assert.Equal(Result.Win, o.Result);
        }

        [Fact]
        public void Wave2_WithoutTaunt_TheLashReachesPastTheFrontLine()
        {
            // Not necessarily a loss - but the Hollow must take damage it does
            // not take with Taunt present, or beat 5 teaches nothing.
            var w = WaveDef.Wave2();
            var without = new[] { Spec(Species.Vetch, 0, Trait.Carapace, 1, Trait.Carapace, 1),
                                  Spec(Species.Ember, 2, Trait.Splash, 1), Spec(Species.Hollow, 4) };
            var with    = new[] { Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
                                  Spec(Species.Ember, 2, Trait.Splash, 1), Spec(Species.Hollow, 4) };
            Assert.NotEqual(Broodline.Sim.Combat.Sim.Run(w, w.Lane, without, 2).Hash,
                            Broodline.Sim.Combat.Sim.Run(w, w.Lane, with, 2).Hash);
        }
    }
}
