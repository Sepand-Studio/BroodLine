namespace Broodline.Sim.Combat
{
    /// The simulation. Inputs in, result out - no ambient state, no
    /// wall-clock, no callbacks into the host. That is what lets the same code
    /// path serve live play, server verification and auto-resolve without
    /// branching (combat_engine section 1), and auto-resolve is the identical
    /// path with no player input rather than a stat roll.
    public static class Sim
    {
        public static Outcome Run(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            wave.Validate();

            var s = new SimState(wave, lane, deployment);
            var log = new Breach[wave.Spawns.Length];
            var scratch = new int[wave.Spawns.Length];
            int breachCount = 0;

            var hash = Hash.Create();
            hash.Add(wave.Id);
            hash.Add(unchecked((long)seed));

            Result result = Result.Running;
            int stallTicks = 0;
            long lastFingerprint = long.MinValue;

            while (s.Tick < Stats.HardTickCap)
            {
                Phases.Spawn(s);            // 1
                Phases.State(s, scratch);   // 2
                Phases.Movement(s);         // 3
                Phases.Targeting(s);        // 4
                Phases.Attack(s);           // 5
                Phases.Death(s);            // 6
                Phases.Breach(s, log, ref breachCount);   // 7
                result = Phases.Resolve(s); // 8

                FoldTick(ref hash, s);

                if (result != Result.Running) break;

                // Stall detector. combat_engine 8.1: if no raider has advanced
                // and no HP has changed for 300 consecutive ticks, terminate
                // immediately rather than burning to the cap. This catches the
                // soft-lock shape in ten seconds instead of three minutes.
                long fingerprint = Fingerprint(s);
                if (fingerprint == lastFingerprint)
                {
                    stallTicks++;
                    if (stallTicks >= Stats.StallTicks)
                    {
                        result = Result.Stalled;
                        break;
                    }
                }
                else
                {
                    stallTicks = 0;
                    lastFingerprint = fingerprint;
                }

                s.Tick++;
            }

            // The hard cap is a content bug, not a gameplay outcome.
            if (result == Result.Running) result = Result.Stalled;

            hash.Add((int)result);
            hash.Add(s.Integrity);

            return new Outcome
            {
                Result = result,
                Ticks = s.Tick,
                IntegrityRemaining = s.Integrity,
                Breaches = log,
                BreachCount = breachCount,
                Hash = hash.Value
            };
        }

        /// Folds the whole visible world into the run hash, every tick. A
        /// whole-run hash that only sampled the end state would let a
        /// mid-simulation divergence that self-corrects pass the gate.
        private static void FoldTick(ref Hash hash, SimState s)
        {
            hash.Add(s.Tick);
            hash.Add(s.Integrity);
            for (int r = 0; r < s.RaiderCount; r++)
            {
                hash.Add(s.RaiderHp[r]);
                hash.Add(s.RaiderProgress[r].Raw);
                hash.Add(s.RaiderAlive[r] ? 1 : 0);
                hash.Add(s.RaiderChilled[r] ? 1 : 0);
            }
            for (int c = 0; c < s.CreatureCount; c++)
            {
                hash.Add(s.CreatureHp[c]);
                hash.Add(s.CreatureTarget[c]);
                hash.Add(s.CreaturePocket[c]);
            }
        }

        /// Cheap "has anything moved or been hurt" summary for the stall
        /// detector. Deliberately not the run hash: it must not include the
        /// tick index, or nothing would ever compare equal.
        private static long Fingerprint(SimState s)
        {
            long f = s.Integrity;
            for (int r = 0; r < s.RaiderCount; r++)
                f = unchecked(f * 31 + s.RaiderProgress[r].Raw + s.RaiderHp[r]);
            for (int c = 0; c < s.CreatureCount; c++)
                f = unchecked(f * 31 + s.CreatureHp[c]);
            return f;
        }
    }
}
