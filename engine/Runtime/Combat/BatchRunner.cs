namespace Broodline.Sim.Combat
{
    public struct BatchResult
    {
        public int Runs;
        public int Clears;
        public int Losses;
        public int Stalls;
        public ulong Hash;
    }

    /// combat_engine section 9.1: because the engine is deterministic, headless
    /// and free of rendering, it can run a wave far faster than real time -
    /// and that is what turns "what is Regrow worth" from an argument into a
    /// measurement. Built with the engine rather than as a later tool.
    ///
    /// Minimal by intent. Sweeping trait sets is Phase 3, once there are
    /// enough traits to sweep over.
    public static class BatchRunner
    {
        public static BatchResult Run(
            WaveDef wave, Lane lane, CreatureSpec[] deployment,
            ulong firstSeed, int runs)
        {
            var result = new BatchResult { Runs = runs };
            var hash = Hash.Create();

            for (int i = 0; i < runs; i++)
            {
                var outcome = Sim.Run(wave, lane, deployment, firstSeed + (ulong)i);

                switch (outcome.Result)
                {
                    case Result.Win:     result.Clears++;  break;
                    case Result.Loss:    result.Losses++;  break;
                    default:             result.Stalls++;  break;
                }

                hash.Add(unchecked((long)outcome.Hash));
            }

            result.Hash = hash.Value;
            return result;
        }
    }
}
