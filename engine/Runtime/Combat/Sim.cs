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

        /// Re-runs a recorded replay. Playing and replaying are ONE code path
        /// differing only in where Rally comes from, which is what
        /// client_architecture section 9.1 already decided for the replay
        /// viewer: "Wave Defense gains one flag - input enabled or not -
        /// rather than a second renderer."
        ///
        /// This is also the server verification path. It is not wired to a
        /// server here; that is Phase 5's.
        public static Outcome Replay(Replay record)
        {
            record.Validate();

            var runner = new SimRunner(
                WaveDef.ForId(record.WaveId), record.BuildLane(),
                record.Deployment, record.Seed);

            while (true)
            {
                // Injected at the tick boundary the live run consumed it at -
                // the same place View calls TryRally from.
                if (record.RallyTick >= 0 && runner.Tick == record.RallyTick)
                    runner.TryRally(record.RallyCreature);

                if (!runner.Step()) break;
            }

            return runner.Outcome;
        }
    }
}
