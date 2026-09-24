using Broodline.Sim.Combat;
using NUnit.Framework;

namespace Broodline.View.Tests
{
    public sealed class PresentationCuesTests
    {
        [Test]
        public void CatchUpCueObservationKeepsTheExactWaveHash()
        {
            var deployment = new[]
            {
                new CreatureSpec { Species = Species.Vetch, Instinct = Instinct.Vanguard, Pocket = 0 },
                new CreatureSpec { Species = Species.Vetch, Instinct = Instinct.Vanguard, Pocket = 2 },
                new CreatureSpec { Species = Species.Loam, Instinct = Instinct.Vanguard, Pocket = 4 },
            };
            foreach (int id in new[] { 6, 7 })
            {
                var wave = WaveDef.ForId(id);
                var direct = new SimRunner(wave, wave.Lane, deployment, 6UL);
                while (direct.Step()) { }

                var observed = new SimRunner(wave, wave.Lane, deployment, 6UL);
                var clock = new WaveClock();
                var pair = new WavePair(observed);
                var feedback = new FrontierBattleFeedback(observed);
                int cues = 0, frames = 0;
                while (!clock.Terminated && frames++ < Stats.HardTickCap * 4)
                    clock.Advance(observed, frames % 3 == 0 ? .1 : 1.0 / 60,
                        () => { pair.Advance(observed); feedback.Observe(observed, _ => cues++); });

                Assert.IsTrue(clock.Terminated);
                Assert.Greater(cues, 0);
                Assert.AreEqual(direct.Outcome.Hash, observed.Outcome.Hash);
                Assert.AreEqual(observed.Tick, pair.Current.Tick);
            }
        }
    }
}
