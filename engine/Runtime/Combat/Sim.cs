namespace Broodline.Sim.Combat
{
    /// The simulation. Inputs in, result out - no ambient state, no
    /// wall-clock, no callbacks into the host. That is what lets the same code
    /// path serve live play, server verification and auto-resolve without
    /// branching (combat_engine section 1), and auto-resolve is the identical
    /// path with no player input rather than a stat roll.
    ///
    /// The loop itself lives in SimRunner so a renderer can step it. This
    /// entry point is preserved unchanged for every headless caller - the
    /// corpus, the batch runner, server verification - and there is exactly
    /// one tick loop underneath both.
    public static class Sim
    {
        public static Outcome Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            var runner = new SimRunner(wave, lane, deployment, seed);
            while (runner.Step()) { }
            return runner.Outcome;
        }
    }
}
