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
    /// In scope: Chill (State, phase 2), Taunt (Targeting, phase 4),
    ///           Splash (Attack, phase 5).
    /// Deferred: Pierce, Sprint (Attack), Cinder (Death),
    ///           Reach (Targeting), Burrow (State).
    ///
    /// Three methods now, at three different phases, and they are shaped
    /// nothing like each other: Chill writes a bool per raider from a sorted
    /// scratch buffer, Taunt writes a creature id per raider with no buffer at
    /// all, and Splash returns a count and answers a question about ONE swing
    /// rather than about the board. A unified interface would have had to be
    /// "a switch statement wearing a coat" by the third one, exactly as
    /// section 5 predicted.
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
                int bestCarrier = -1;
                int bestDistSq = 0;

                for (int r = 0; r < s.RaiderCount; r++)
                {
                    if (!s.RaiderAlive[r]) continue;
                    if (s.RaiderTargetCreature[r] >= 0) continue;   // already held
                    if (Stats.CounterFor(s.RaiderType[r]) != Trait.Taunt) continue;

                    // WITH ROOM. The capacity above is a pooled sum, but 4.2
                    // spends it per creature, so a carrier that already holds
                    // its tier's worth is not a candidate for the next raider
                    // however near it is - see NearestTaunterWithRoom.
                    //
                    // Carrier and distance are taken ONCE here and carried to
                    // the assignment below. This scan used to call
                    // NearestTaunter to test validity, again twice inside
                    // CompareTaunt to rank, and once more for the winner -
                    // three O(creatures) searches per candidate, every tick,
                    // for an answer that cannot change within one iteration.
                    int carrier = Capacity.NearestTaunterWithRoom(s, r);
                    if (carrier < 0) continue;

                    int d = s.Lane.DistSq(s.CreaturePocket[carrier], s.RaiderTile(r));

                    // CompareTaunt's order, inlined: (distance, raider index)
                    // ascending. The scan runs in ascending raider order and a
                    // challenger only wins on a STRICT improvement, so an equal
                    // distance leaves the earlier raider in place - the same
                    // total order, tie-broken the same way.
                    if (best < 0 || d < bestDistSq)
                    {
                        best = r;
                        bestCarrier = carrier;
                        bestDistSq = d;
                    }
                }
                if (best < 0) return;      // nothing left that Taunt can hold

                s.RaiderTargetCreature[best] = bestCarrier;
            }
        }

        /// Splash - phase 5, Attack. An area rule on one carrier's own swing.
        ///
        /// combat_numbers 4.2: 2 targets within 1 tile at tier I, 3 at II, 5 at
        /// radius 2 at III. Writes the raiders this swing lands on into `hits`
        /// and returns how many - the caller must size `hits` to at least
        /// Stats.MaxSplashTargets.
        ///
        /// SELECTS, AND DOES NOT DAMAGE. Phase 5 subtracts, here and nowhere
        /// else, so there stays exactly one place in the engine where a
        /// creature's damage is applied - and 4.2's "no counter has a damage
        /// component" stays visibly true in the code rather than only in the
        /// comment. Splash chooses WHO, never how much: every raider in the
        /// returned set takes the same Attacks.Damage the single target would
        /// have taken alone, with no falloff, because 4.2 gives none.
        ///
        /// THE PRIMARY IS ALWAYS hits[0], before the trait is even looked up.
        /// It is the creature's actual attack; splash is the spill. Without
        /// that, a carrier whose target happened to tie with a lower-indexed
        /// raider could have been ordered out of its own swing.
        ///
        /// A creature without the trait returns 1 with the primary in hits[0],
        /// so phase 5 does exactly what it did before this existed. That is
        /// what makes the change inert for the 500 corpus scenarios, none of
        /// which deploys a Splash carrier.
        ///
        /// MEASURED FROM THE STRUCK RAIDER, not from the creature, and along
        /// the lane rather than through Lane.DistSq - DistSq is a pocket-to-
        /// tile table and carries the pocket's perpendicular offset, which has
        /// no meaning between two bodies that are both ON the lane. A bare
        /// tile difference is already an exact integer, and squaring it would
        /// order the candidates identically, so it would buy nothing.
        ///
        /// The splash may reach a tile the creature itself could not, which is
        /// deliberate: the radius is around the impact, and 4.2 places it there
        /// rather than inside the attacker's range.
        ///
        /// TIE-BROKEN ON SPAWN INDEX, which is the determinism-critical part.
        /// Ties are common rather than rare here - Lane 2.2 - because six
        /// Skirmishers 1.5s apart bunch up, and a swing that hits 2 of 3 bodies
        /// at identical distance has to pick the same two on CoreCLR and on
        /// IL2CPP. The scan runs in ascending raider order and a challenger
        /// wins only on a STRICT improvement, so an equal distance leaves the
        /// earlier raider in place: that is the (distance, spawnIndex) total
        /// order of solo_execution 12, achieved without a sort, and it is the
        /// same idiom Targeting.Select and Capacity.NearestTaunter use.
        public static int ApplySplash(SimState s, int creature, int primary, int[] hits)
        {
            hits[0] = primary;

            if (!s.CreatureCarries(creature, Trait.Splash, out int tier)) return 1;

            int max = Stats.SplashTargets(tier);
            if (max <= 1) return 1;

            int radius = Stats.SplashRadius(tier);
            int centre = s.RaiderTile(primary);
            int found = 1;

            while (found < max)
            {
                int best = -1;
                int bestDistance = 0;

                for (int r = 0; r < s.RaiderCount; r++)
                {
                    if (r == primary) continue;
                    if (!s.RaiderAlive[r]) continue;
                    if (AlreadyHit(hits, found, r)) continue;

                    int distance = s.RaiderTile(r) - centre;
                    if (distance < 0) distance = -distance;
                    if (distance > radius) continue;

                    if (best < 0 || distance < bestDistance)
                    {
                        best = r;
                        bestDistance = distance;
                    }
                }

                if (best < 0) break;      // nothing else in radius
                hits[found++] = best;
            }

            return found;
        }

        /// Linear over at most Stats.MaxSplashTargets entries, which is why it
        /// is a scan rather than a set - HashSet is banned, and at five entries
        /// it would lose anyway.
        private static bool AlreadyHit(int[] hits, int count, int raider)
        {
            for (int i = 0; i < count; i++)
                if (hits[i] == raider) return true;
            return false;
        }
    }
}
