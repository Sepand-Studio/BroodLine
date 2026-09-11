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
    /// In scope for Phase 2: Chill (State, phase 2).
    /// Deferred: Splash, Pierce, Sprint (Attack), Cinder (Death),
    ///           Taunt, Reach (Targeting), Burrow (State).
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
    }
}
