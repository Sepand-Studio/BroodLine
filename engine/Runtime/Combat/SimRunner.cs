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

        /// The tick whose phases most recently ran.
        ///
        /// NOT _s.Tick. Step increments that after a non-terminating tick and
        /// leaves it alone on the terminating one, so anything reading it from
        /// outside Step is one ahead in the common case and exact in the rare
        /// one - which made RaiderBreachedThisTick true only for a breach that
        /// ended the wave. Wave 6 has exactly that shape, so the suite could
        /// not see it.
        private int _completedTick = -1;
        private int _stallTicks;
        private long _lastFingerprint = long.MinValue;

        private readonly Replay _record;
        private readonly string _unrecordable;
        private bool _rallyUsed;

        private Result _result = Result.Running;
        private Outcome _outcome;
        private bool _done;

        public SimRunner(WaveDef wave, Lane lane, CreatureSpec[] deployment, ulong seed)
        {
            wave.Validate();

            // The deployment's invariants belong here, beside the wave's own.
            // Replay bounded every field of a deserialized deployment while the
            // simulation bounded exactly one of them - the array length - so
            // the engine could run a wave to completion and write a record it
            // could not itself read back. Measured with a single Vetch: Pocket
            // 178956971 simulated to Loss in 540 ticks because 178956971 * 24
            // wraps into Lane's 120-entry distance table, Pocket 5 threw
            // IndexOutOfRangeException out of the tick loop, and Species 6,
            // Trait 2, Tier 7 and Instinct 9 each emitted a 212-byte record
            // that Validate then rejected.
            //
            // ONE list, shared with Replay.Validate. A second, shorter copy of
            // the same rules is how the asymmetry arose in the first place.
            //
            var problem = Deployments.Problem(deployment, lane);
            if (problem != null) throw new WaveCompositionException(problem);

            // Whether this run could be RECORDED faithfully - computed here,
            // where the inputs are, and enforced at SerializeRecord.
            //
            // Not thrown here, deliberately. A replay stores a wave ID and a
            // terrain family rather than their contents, so only authored
            // content round-trips - but running UNauthored content is a
            // first-class use of this engine, and FuzzTests generates 12
            // synthetic waves per run precisely to exercise the tick loop on
            // shapes no author wrote. Refusing those at construction would
            // trade a real bug for a worse one.
            //
            // The real bug is narrower: a run whose record describes a
            // DIFFERENT game. A WaveDef claiming id 6 while carrying integrity
            // 8 ran to Win with hash 8532104364784216008; the record stored
            // only the id, so verification rebuilt the authored wave 6 and
            // returned Loss with hash 2495532238167386945 - and Validate
            // accepted it, because nothing cross-checks fields the record does
            // not carry. A win read back as a loss, reported by nobody. So the
            // check lands at the moment the lie would be written, not before.
            _unrecordable = Deployments.Unrecordable(wave, lane);

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
                Terrain = lane.Family,
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

        /// Raiders on the board right now - spawned, not yet dead or breached.
        ///
        /// client_architecture section 4 keys every rung of the degradation
        /// ladder off a live entity count, and the HUD was deriving one as
        /// RaiderCount + CreatureCount. RaiderCount is the number SPAWNED, so
        /// that number counts corpses: it never goes down, and the ladder would
        /// have degraded on a board that had emptied.
        ///
        /// The HUD does NOT read this. It counts what it draws, from the
        /// snapshot - because a breaching raider is drawn and is not alive, so
        /// this number and the picture disagree for exactly one tick. That is
        /// correct for both: the ladder wants live entities, the readout wants
        /// entities on screen. This is the engine's answer, kept because
        /// section 11 says the view must not derive it and asserted by
        /// AliveCountsDoNotCountCorpses so it cannot rot before Phase 4 uses
        /// it.
        public int AliveRaiderCount
        {
            get
            {
                int n = 0;
                for (int r = 0; r < _s.RaiderCount; r++) if (_s.RaiderAlive[r]) n++;
                return n;
            }
        }

        public int AliveCreatureCount
        {
            get
            {
                int n = 0;
                for (int c = 0; c < _s.CreatureCount; c++) if (_s.CreatureHp[c] > 0) n++;
                return n;
            }
        }

        /// Whether raider r reached the Ark on the tick that just ran.
        ///
        /// Phases.Breach deducts integrity and clears RaiderAlive in the same
        /// breath, so by the time anything outside the loop can look, a raider
        /// that BREACHED is indistinguishable from one that was killed - and
        /// both renderers gate on RaiderAlive. The breach frame is the one
        /// frame wave 6 exists to show, and it was never drawn: the Courser
        /// simply vanished a tile short of the Ark while the HUD printed Loss.
        ///
        /// Compared against _completedTick, NOT _s.Tick. The first version of
        /// this used _s.Tick, which Step has already advanced on every tick
        /// that did not terminate - so a wave whose integrity outlives a breach
        /// lost the breach frame exactly as before. Measured: integrity 6 and
        /// two Coursers logs breaches at ticks 540 and 750, and the renderer's
        /// read point saw the flag true once.
        ///
        /// An accessor over the breach log rather than a second RaiderAlive-
        /// shaped array, because the log already records exactly this and a
        /// parallel array is redundant state that can drift from it. (The
        /// earlier claim here - that a new SimState array would move every
        /// golden - was simply wrong: FoldTick is a hand-written field list,
        /// so an array costs nothing until someone folds it.)
        public bool RaiderBreachedThisTick(int raider)
        {
            for (int b = 0; b < _breachCount; b++)
                if (_log[b].Raider == raider && _log[b].Tick == _completedTick) return true;
            return false;
        }

        /// Ticks of Rally left on a creature, 0 when it is not rallied.
        ///
        /// client_architecture section 11: "if the view needs a number that the
        /// engine does not expose, the engine gains an accessor - the view never
        /// derives it." The HUD was computing both this countdown and the
        /// rallied predicate by subtracting CreatureRallyUntil from Tick, which
        /// put a second copy of Attacks.IntervalTicks' own test in the renderer.
        public int CreatureRallyRemaining(int c)
        {
            int left = _s.CreatureRallyUntil[c] - _s.Tick;
            return left > 0 ? left : 0;
        }

        /// The inputs this run consumed, as bytes. Complete from construction
        /// except for Rally, which TryRally appends when - and only when - it
        /// accepts one.
        ///
        /// Bytes rather than the live Replay. Handing out the object made the
        /// "TryRally is the only writer" claim false: its RallyTick,
        /// RallyCreature and Deployment are public mutable fields, so a caller
        /// could stamp a rally onto a run that never had one and serialize a
        /// structurally valid forgery - which is precisely the "the recording
        /// disagreed with what was consumed" bug this design says it killed.
        public byte[] SerializeRecord()
        {
            RequireRecordable();
            return _record.Serialize();
        }

        /// A COPY of the record for tests and tooling that want the fields
        /// rather than the bytes. Callers cannot reach _record through it, so
        /// mutating the result cannot forge what this run claims to have
        /// consumed.
        ///
        /// Built directly rather than round-tripped through
        /// Serialize/Deserialize: the round trip also worked, but it paid a
        /// full encode and decode - plus every Validate-adjacent bounds check -
        /// for what is a field copy, and callers in the test suite invoke this
        /// two or three times in a row.
        public Replay ReadRecord()
        {
            RequireRecordable();
            return _record.Copy();
        }

        /// Why this run cannot be recorded, or null.
        ///
        /// Public so a caller that INTENDS to record can ask before it spends a
        /// wave, rather than finding out at the end. SerializeRecord and
        /// ReadRecord throw on it; running a wave and never recording it is
        /// fine and is what the fuzz and corpus suites do.
        public string Unrecordable => _unrecordable;

        private void RequireRecordable()
        {
            if (_unrecordable != null) throw new WaveCompositionException(_unrecordable);
        }

        /// Advances exactly one tick. Returns false once the wave has
        /// terminated, after which it is a harmless no-op.
        public bool Step()
        {
            if (_done) return false;

            // The hard cap is checked BEFORE the phases, exactly as the
            // original while-condition did.
            if (_s.Tick >= Stats.HardTickCap) { Finish(Result.Stalled); return false; }

            _completedTick = _s.Tick;    // the tick these eight phases belong to

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
            // At the cap, the NEXT Step terminates before running a phase, so an
            // input accepted here is written to the record and never consumed -
            // and Replay.Validate rejects exactly that record on the grounds
            // that no run reaches the cap. The producer has to agree with the
            // validator or the engine writes records it cannot read back.
            if (_s.Tick >= Stats.HardTickCap) return false;
            if (creatureId < 0 || creatureId >= _s.CreatureCount) return false;
            // CreatureCanAct, not CreatureAlive. A creature repositioning after
            // Skittish has CreatureBusyUntil set and is skipped by Phases.Attack
            // until it expires, so accepting a Rally here spends the player's
            // one input per wave on a creature that cannot swing - and the
            // record faithfully stores an input that did almost nothing.
            if (!_s.CreatureCanAct(creatureId)) return false;

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

            // A TRIMMED COPY, not _log, so an Outcome does not hold a handle on
            // the runner's live buffer. Trimming alone was the PREVIOUS attempt
            // at this and it fixed nothing observable: Outcome is copied by
            // value but the array reference inside it is not, so
            // `runner.Outcome.Breaches[0] = default` still wrote through into
            // the stored outcome. Outcome now keeps the buffer private and
            // exposes ReadOnlySpan, which makes that line a compile error; the
            // copy here is what keeps the claim true for the runner's own
            // buffer as well.
            var breaches = new Breach[_breachCount];
            for (int b = 0; b < _breachCount; b++) breaches[b] = _log[b];

            _outcome = new Outcome(result, _s.Tick, _s.Integrity, breaches, _hash.Value);
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
