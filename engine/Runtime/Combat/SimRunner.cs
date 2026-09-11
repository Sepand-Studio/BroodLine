namespace Broodline.Sim.Combat
{
    /// The tick loop as an object that can be stepped, so a renderer can read
    /// the world between ticks and interpolate across it.
    ///
    /// This is a DECOMPOSITION of what Sim.Run already did, not a second
    /// implementation of it. Sim.Run is reimplemented as a thin loop over this
    /// class precisely so there is never a second tick loop to keep in sync -
    /// solo_execution section 9.3 rejects that shape by name.
    ///
    /// Two orderings inside Step are load-bearing and are the reason this class
    /// has a test asserting the count of true returns:
    ///   - FoldTick runs BEFORE the termination check, so the terminating tick
    ///     is folded into the hash like any other.
    ///   - Tick is incremented AFTER it, so a terminating tick does not advance
    ///     the counter and Outcome.Ticks is the number of COMPLETED ticks.
    public sealed class SimRunner
    {
        private readonly SimState _s;
        private readonly Breach[] _log;
        private readonly int[] _scratch;

        private Hash _hash;
        private int _breachCount;
        private int _stallTicks;
        private long _lastFingerprint = long.MinValue;

        private Result _result = Result.Running;
        private Outcome _outcome;
        private bool _done;

        public SimRunner(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            wave.Validate();

            _s = new SimState(wave, lane, deployment);
            _log = new Breach[wave.Spawns.Length];
            _scratch = new int[wave.Spawns.Length];

            _hash = Hash.Create();
            _hash.Add(wave.Id);
            _hash.Add(unchecked((long)seed));
        }

        public int Tick => _s.Tick;
        public Result Result => _result;
        public bool Done => _done;
        public Outcome Outcome => _outcome;

        /// Advances exactly one tick. Returns false once the wave has
        /// terminated, after which it is a harmless no-op.
        public bool Step()
        {
            if (_done) return false;

            // The hard cap is checked BEFORE the phases, exactly as the
            // original while-condition did.
            if (_s.Tick >= Stats.HardTickCap) { Finish(Result.Stalled); return false; }

            Phases.Spawn(_s);            // 1
            Phases.State(_s, _scratch);  // 2
            Phases.Movement(_s);         // 3
            Phases.Targeting(_s);        // 4
            Phases.Attack(_s);           // 5
            Phases.Death(_s);            // 6
            Phases.Breach(_s, _log, ref _breachCount);   // 7
            _result = Phases.Resolve(_s); // 8

            FoldTick(ref _hash, _s);

            if (_result != Result.Running) { Finish(_result); return false; }

            // Stall detector. combat_engine 8.1: if no raider has advanced
            // and no HP has changed for 300 consecutive ticks, terminate
            // immediately rather than burning to the cap. This catches the
            // soft-lock shape in ten seconds instead of three minutes.
            long fingerprint = Fingerprint(_s);
            if (fingerprint == _lastFingerprint)
            {
                _stallTicks++;
                if (_stallTicks >= Stats.StallTicks) { Finish(Result.Stalled); return false; }
            }
            else
            {
                _stallTicks = 0;
                _lastFingerprint = fingerprint;
            }

            _s.Tick++;
            return true;
        }

        private void Finish(Result result)
        {
            _result = result;
            _hash.Add((int)result);
            _hash.Add(_s.Integrity);

            _outcome = new Outcome
            {
                Result = result,
                Ticks = _s.Tick,
                IntegrityRemaining = _s.Integrity,
                Breaches = _log,
                BreachCount = _breachCount,
                Hash = _hash.Value
            };
            _done = true;
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
