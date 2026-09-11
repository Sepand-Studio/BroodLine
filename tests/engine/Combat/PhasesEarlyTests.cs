using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class PhasesEarlyTests
    {
        private static SimState Wave6State(CreatureSpec[] deployment)
            => new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);

        [Fact]
        public void Spawn_IntroducesARaiderOnlyWhenItsTickArrives()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());

            s.Tick = 89;
            Phases.Spawn(s);
            Assert.Equal(0, s.RaiderCount);

            s.Tick = 90;                  // t=3s at 30Hz
            Phases.Spawn(s);
            Assert.Equal(1, s.RaiderCount);
            Assert.True(s.RaiderAlive[0]);
            Assert.Equal(220, s.RaiderHp[0]);
            Assert.Equal(Fix64.Zero, s.RaiderProgress[0]);
        }

        [Fact]
        public void Spawn_IsIdempotentWithinATick()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            Phases.Spawn(s);
            Assert.Equal(1, s.RaiderCount);
        }

        [Fact]
        public void Movement_AdvancesByFullSpeedWhenNotChilled()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);

            Fix64 before = s.RaiderProgress[0];
            Phases.Movement(s);
            Fix64 delta = s.RaiderProgress[0] - before;

            Assert.Equal(Lane.SpeedPerTick(1600), delta);
        }

        [Fact]
        public void State_ChillSlowsTheRaiderAndMovementHonoursIt()
        {
            var d = SimStateTests.FiveWithoutChill();
            d[0] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 0,
                Instinct = Instinct.Vanguard,
                Trait1 = Trait.Chill, Tier1 = 1
            };
            var s = Wave6State(d);
            s.Tick = 90;
            Phases.Spawn(s);

            // Chill is assigned "within range" (combat_engine 5.1), so walk the
            // raider into the carrier's reach first. A Pale in pocket 0 sits at
            // tile 6 with range 5, covering tiles 2-10; a raider still at the
            // spawn line is correctly NOT chillable.
            s.RaiderProgress[0] = Fix64.FromInt(6);

            var scratch = new int[1];
            Phases.State(s, scratch);
            Assert.True(s.RaiderChilled[0]);

            Fix64 before = s.RaiderProgress[0];
            Phases.Movement(s);

            // 0.5 tiles/sec, not 1.6.
            Assert.Equal(Lane.SpeedPerTick(500), s.RaiderProgress[0] - before);
        }

        [Fact]
        public void State_WithoutAChillCarrierLeavesTheRaiderAtFullSpeed()
        {
            var s = Wave6State(SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);

            Phases.State(s, new int[1]);
            Assert.False(s.RaiderChilled[0]);
        }
    }
}
