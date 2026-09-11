using System;

namespace Broodline.Sim.Combat
{
    public enum Result { Running = 0, Win = 1, Loss = 2, Stalled = 3 }

    /// One raider reaching the Ark, with its diagnosis attached at Task 9.
    /// combat_engine section 7: the diagnosis is recorded AT the breach,
    /// because by the time the wave ends the information about why a specific
    /// raider was unanswerable is gone.
    public struct Breach
    {
        public int Tick;
        public int Raider;
        public RaiderType Type;
        public int Lane;

        public bool Access;
        public bool Coverage;
        public bool Placement;
    }

    /// What a simulation returns. Inputs in, result out.
    ///
    /// A READONLY struct with a private breach buffer, which is the second
    /// attempt at this. The first kept `public Breach[] Breaches` and had
    /// SimRunner hand out a trimmed copy - and that fixed nothing a caller
    /// could observe: the struct is copied by value but the array reference is
    /// not, so `runner.Outcome.Breaches[0] = default` still wrote through into
    /// the runner's stored outcome, and two Outcome copies still shared one
    /// array. The test written to prove otherwise compared Breach.Type, where
    /// RaiderType.Courser == 0 == default(Breach).Type, so it asserted 0 == 0.
    ///
    /// ReadOnlySpan cannot be written through, so `Breaches[0] = default` is
    /// now a compile error rather than a silent corruption of the diagnosis
    /// every other consumer reads.
    ///
    /// The count is DERIVED, not passed. The first version took the buffer and
    /// a separate breachCount and checked neither against the other, so
    /// `new Outcome(..., new Breach[2], 5, ...)` was constructible and threw
    /// ArgumentOutOfRangeException out of the Breaches getter - from OnGUI,
    /// every frame - while a null buffer with a count of 5 reported
    /// BreachCount 5 and Length 0 with no complaint at all. For a type whose
    /// whole point is "made impossible rather than promised", a free-floating
    /// number the caller can get wrong is the promise. One buffer, trimmed by
    /// its producer, and the count is its length; that also retires the
    /// "iterate to BreachCount, never Breaches.Length" hazard by making them
    /// the same expression.
    public readonly struct Outcome
    {
        public Result Result { get; }
        public int Ticks { get; }
        public int IntegrityRemaining { get; }
        public ulong Hash { get; }

        private readonly Breach[] _breaches;

        /// `breaches` must already be trimmed to the breaches recorded -
        /// SimRunner.Finish does that, and it is the only producer.
        public Outcome(Result result, int ticks, int integrityRemaining,
                       Breach[] breaches, ulong hash)
        {
            Result = result;
            Ticks = ticks;
            IntegrityRemaining = integrityRemaining;
            Hash = hash;
            _breaches = breaches;
        }

        /// The breaches recorded.
        ///
        /// default(Outcome) - the value a runner holds before it finishes -
        /// has no buffer at all, so this answers with an empty span rather
        /// than throwing.
        public ReadOnlySpan<Breach> Breaches => _breaches;

        /// How many raiders reached the Ark. Kept because it reads better than
        /// Breaches.Length at a call site that does not want the span, and it
        /// cannot disagree with one.
        public int BreachCount => Breaches.Length;
    }
}
