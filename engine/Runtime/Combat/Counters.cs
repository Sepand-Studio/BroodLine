namespace Broodline.Sim.Combat
{
    /// The counters, each as an explicit rule at its own tick-phase site.
    ///
    /// combat_engine section 5 rejects a generic counter interface outright:
    /// the eight counters are eight different KINDS of rule operating at five
    /// different points in the tick, and a unified interface "would have to be
    /// a switch statement wearing a coat". This file therefore has one method
    /// per counter, each called from exactly one phase, and it is expected to
    /// grow to eight unrelated methods rather than to acquire an abstraction.
    ///
    /// In scope: Chill (State, phase 2), Taunt (Targeting, phase 4).
    /// Deferred: Splash, Pierce, Sprint (Attack), Cinder (Death),
    ///           Reach (Targeting), Burrow (State).
    ///
    /// Two methods now, at two different phases, and they are already shaped
    /// nothing like each other: Chill writes a bool per raider from a sorted
    /// scratch buffer, Taunt writes a creature id per raider with no buffer at
    /// all. That is section 5's claim about the generic interface arriving on
    /// schedule rather than in the abstract.
    public static class Counters
    {
        /// Chill - phase 2, State. A speed modifier on the raider.
        ///
        /// Assignment is nearest-first within range, re-evaluated every tick:
        /// combat_engine 5.1 makes Chill one of three exceptions to
        /// follow-the-carrier's-target, because a Chill carrier may not be
        /// attacking the raider it slows.
        public static void ApplyChill(SimState s, int[] scratch)
        {
            Capacity.AssignChill(s, scratch);
        }

        /// Taunt - phase 4, Targeting. A forcing rule on the raider's choice
        /// of defender, not a modifier on anything.
        ///
        /// combat_numbers 4.2: "Forces 1 Lash to target this creature", 2 at
        /// tier II and 4 at III. So it is a capacity like Chill's and it is
        /// recomputed from scratch every tick for the same reason 5.3 gives -
        /// an accumulated capacity drifts, and drift in a counter system is a
        /// fairness bug.
        ///
        /// CLEARS EVERY RAIDER'S TARGET FIRST. This runs before the default
        /// preference in phase 4, and it is what leaves an untaunted raider at
        /// -1 for that pass to fill in - so the clear is the handoff, not just
        /// hygiene.
        ///
        /// No scratch buffer and no sort. Chill needs one because it ranks
        /// every candidate and then takes a prefix; here the target slot it is
        /// about to write doubles as the "already taken" mark, so repeatedly
        /// selecting the best REMAINING candidate costs no memory and stays a
        /// total order. Capacity is bounded by the deployment cap times the
        /// tier-III ladder, so the outer loop runs at most twenty times.
        public static void ApplyTaunt(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++) s.RaiderTargetCreature[r] = -1;

            int capacity = Capacity.TotalTauntCapacity(s);

            for (int taken = 0; taken < capacity; taken++)
            {
                int best = -1;
                for (int r = 0; r < s.RaiderCount; r++)
                {
                    if (!s.RaiderAlive[r]) continue;
                    if (s.RaiderTargetCreature[r] >= 0) continue;   // already held
                    if (Stats.CounterFor(s.RaiderType[r]) != Trait.Taunt) continue;
                    if (Capacity.NearestTaunter(s, r) < 0) continue;

                    if (best < 0 || Capacity.CompareTaunt(s, r, best) < 0) best = r;
                }
                if (best < 0) return;      // nothing left that Taunt can hold

                s.RaiderTargetCreature[best] = Capacity.NearestTaunter(s, best);
            }
        }
    }
}
