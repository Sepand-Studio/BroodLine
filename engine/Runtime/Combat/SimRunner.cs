using System;

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

        private readonly Replay _record;
        private bool _rallyUsed;

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

            // The record is produced BY the runner rather than observed from
            // outside. That is what kills the "the recording disagreed with
            // what was consumed" bug class structurally: TryRally is the only
            // thing that writes the Rally fields, and it writes them at the
            // moment it accepts the input.
            var hp = new int[deployment.Length];
            for (int c = 0; c < deployment.Length; c++)
                hp[c] = Stats.CreatureHp(deployment[c].Species);

            _record = new Replay
            {
                WaveId = wave.Id,
                Seed = seed,
                Terrain = Terrain.Defile,
                LaneCount = wave.LaneCount,
                PocketCount = lane.PocketCount,
                LaneTiles = lane.Tiles,
                Deployment = (CreatureSpec[])deployment.Clone(),
                DeploymentHp = hp
            };
        }

        public int Tick => _s.Tick;
        public Result Result => _result;
        public bool Done => _done;
        public Outcome Outcome => _outcome;

        // --- The read-only surface. ---
        //
        // client_architecture section 2 specifies "ref readonly SimState".
        // That guarantees nothing: SimState is a sealed CLASS whose arrays are
        // readonly REFERENCES holding mutable contents, so a readonly reference
        // to it still permits RaiderHp[0] = 0. ReadOnlySpan<T> cannot be
        // written through, so the rule "View renders and never derives" holds
        // by the type system rather than by anyone remembering it.
        //
        // Zero-copy: the implicit T[] to ReadOnlySpan<T> conversion wraps the
        // existing array. Nothing is allocated and nothing is copied.

        public int Integrity => _s.Integrity;
        public int RaiderCount => _s.RaiderCount;
        public int CreatureCount => _s.CreatureCount;
        public Lane Lane => _s.Lane;
        public int LaneTiles => _s.Lane.Tiles;

        public ReadOnlySpan<RaiderType> RaiderType => _s.RaiderType;
        public ReadOnlySpan<int> RaiderHp => _s.RaiderHp;
        public ReadOnlySpan<Fix64> RaiderProgress => _s.RaiderProgress;
        public ReadOnlySpan<bool> RaiderAlive => _s.RaiderAlive;
        public ReadOnlySpan<bool> RaiderChilled => _s.RaiderChilled;

        public ReadOnlySpan<Species> CreatureSpecies => _s.CreatureSpecies;
        public ReadOnlySpan<int> CreatureHp => _s.CreatureHp;
        public ReadOnlySpan<int> CreaturePocket => _s.CreaturePocket;
        public ReadOnlySpan<int> CreatureTarget => _s.CreatureTarget;
        public ReadOnlySpan<int> CreatureRallyUntil => _s.CreatureRallyUntil;
        public ReadOnlySpan<int> CreatureNextAttackAt => _s.CreatureNextAttackAt;
        public bool RallyUsed => _rallyUsed;

        /// The inputs this run consumed. Complete from construction except for
        /// Rally, which TryRally appends when - and only when - it accepts one.
        public Replay Record => _record;

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

        /// combat_engine section 8: four seconds of doubled attack speed on one
        /// creature, one use per wave, no cooldown. 4s at 30Hz.
        public const int RallyTicks = 4 * Stats.TicksPerSecond;

        /// The only player input during a wave. Returns false - harmlessly -
        /// for every invalid case rather than throwing.
        ///
        /// An invalid Rally MUST be a no-op, because the replay records only
        /// what the simulation consumed. An input that was rejected but still
        /// written to the record is precisely the shape of a replay that does
        /// not reproduce, and it would surface as a rejected raid for an honest
        /// player rather than as a bug anyone could find.
        public bool TryRally(int creatureId)
        {
            if (_done) return false;
            if (_rallyUsed) return false;
            if (creatureId < 0 || creatureId >= _s.CreatureCount) return false;
            if (!_s.CreatureAlive(creatureId)) return false;

            _rallyUsed = true;
            _s.CreatureRallyUntil[creatureId] = _s.Tick + RallyTicks;

            // Halve the REMAINING cooldown too. NextAttackAt is an absolute
            // tick already computed against the un-halved interval, so without
            // this a Hollow's 75-tick interval swallows most of the 120-tick
            // window and the player's one input per wave looks dropped.
            int remaining = _s.CreatureNextAttackAt[creatureId] - _s.Tick;
            if (remaining > 0)
                _s.CreatureNextAttackAt[creatureId] = _s.Tick + remaining / 2;

            _record.RallyTick = _s.Tick;
            _record.RallyCreature = creatureId;
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
                hash.Add(s.CreatureRallyUntil[c]);
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
