namespace Broodline.Sim.Combat
{
    /// combat_engine section 5.3: every counter trait is a capacity resource,
    /// and capacity is recomputed from scratch every tick from the live
    /// creature set, never accumulated. Incremental capacity drifts, and drift
    /// in a counter system is a fairness bug.
    public static class Capacity
    {
        /// Summed across every LIVE carrier. A dead carrier contributes
        /// nothing, so its capacity frees the tick it dies.
        public static int TotalChillCapacity(SimState s)
        {
            int total = 0;
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureCarries(c, Trait.Chill, out int tier))
                    total += Stats.ChillCapacity(tier);
            }
            return total;
        }

        /// Squared distance from this raider to the NEAREST live Chill carrier
        /// that currently has it IN RANGE, or -1 when no carrier can reach it.
        ///
        /// combat_engine 5.1 assigns Chill "nearest-first within range,
        /// re-evaluated each tick", and the range gate is not decoration.
        /// Without it a carrier slows raiders it could never reach and an
        /// unopposed Courser crosses in 48s, against the 30s combat_numbers 240
        /// describes. Gated, it crosses in about 27s - which is what makes
        /// 134's "0.5 t/s" and 240's "halves its speed to 30 seconds" the same
        /// claim rather than a contradiction.
        ///
        /// Measured per raider rather than from one fixed anchor pocket:
        /// capacity sums across carriers standing in different pockets, so
        /// "nearest" has to mean nearest to THAT raider.
        public static int NearestCarrierDistSq(SimState s, int raider)
        {
            int best = -1;
            int tile = s.RaiderTile(raider);

            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (!s.CreatureCarries(c, Trait.Chill, out int tier) || tier <= 0) continue;

                int pocket = s.CreaturePocket[c];
                if (!s.Lane.InRange(pocket, tile, Stats.CreatureRange(s.CreatureSpecies[c])))
                    continue;

                int d = s.Lane.DistSq(pocket, tile);
                if (best < 0 || d < best) best = d;
            }
            return best;
        }

        /// The (distance, spawnIndex) total order of combat_engine 5.1.
        ///
        /// Nearest-first alone is not deterministic: 2.2 makes distance an
        /// exact integer, so ties are common, and a sort over equal keys can
        /// order them differently on two runtimes. Tie-breaking on spawn index
        /// makes this a total order, so any correct sort produces identical
        /// output everywhere.
        public static int Compare(SimState s, int raiderA, int raiderB)
        {
            int da = NearestCarrierDistSq(s, raiderA);
            int db = NearestCarrierDistSq(s, raiderB);
            if (da != db) return da < db ? -1 : 1;
            if (raiderA != raiderB) return raiderA < raiderB ? -1 : 1;
            return 0;
        }

        /// Chill - one of combat_engine 5.1's three exceptions to
        /// follow-the-carrier's-target, because a Chill carrier may not be
        /// attacking the raider it slows. Assignment is nearest-first within
        /// range, re-evaluated every tick.
        public static void AssignChill(SimState s, int[] scratch)
        {
            for (int r = 0; r < s.RaiderCount; r++) s.RaiderChilled[r] = false;

            int capacity = TotalChillCapacity(s);
            if (capacity <= 0) return;

            // Only raiders some live carrier can actually reach are candidates.
            int n = 0;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (NearestCarrierDistSq(s, r) < 0) continue;
                scratch[n++] = r;
            }

            // Insertion sort: stable by construction over a total order, and
            // allocation-free. n is bounded by the wave's spawn count.
            for (int i = 1; i < n; i++)
            {
                int v = scratch[i];
                int j = i - 1;
                while (j >= 0 && Compare(s, scratch[j], v) > 0)
                {
                    scratch[j + 1] = scratch[j];
                    j--;
                }
                scratch[j + 1] = v;
            }

            int slowed = n < capacity ? n : capacity;
            for (int i = 0; i < slowed; i++) s.RaiderChilled[scratch[i]] = true;
        }

        // --- Taunt, the same resource shape against a different raider ---

        /// Summed across every LIVE carrier, exactly as Chill is. A dead
        /// carrier contributes nothing, so the Lash it was holding is released
        /// on the tick it dies rather than on the next retarget.
        public static int TotalTauntCapacity(SimState s)
        {
            int total = 0;
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureCarries(c, Trait.Taunt, out int tier))
                    total += Stats.TauntCapacity(tier);
            }
            return total;
        }

        /// The carrier a taunted raider is forced onto, or -1 when none can
        /// hold it.
        ///
        /// Gated on the RAIDER's range, not the carrier's - the opposite of
        /// NearestCarrierDistSq above, and deliberately. Chill is something a
        /// creature does TO a raider, so the creature has to reach it. Taunt
        /// changes who the raider attacks, so a taunt thrown from outside the
        /// raider's own reach would pin it to a target it can never hit: that
        /// is a stun, and combat_numbers section 4.2 gives Taunt no such
        /// effect. Ungated, one Vetch in the back pocket would freeze every
        /// Lash on the board.
        ///
        /// Nearest first, tie-broken on creature index ascending - the scan
        /// runs in ascending creature order and a challenger only wins on a
        /// STRICT improvement, so an equal distance leaves the earlier carrier
        /// in place.
        public static int NearestTaunter(SimState s, int raider)
        {
            int tile = s.RaiderTile(raider);
            int range = Stats.RaiderRange(s.RaiderType[raider]);
            if (range <= 0) return -1;

            int best = -1;
            int bestDistSq = 0;

            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (!s.CreatureCarries(c, Trait.Taunt, out int tier) || tier <= 0) continue;

                int pocket = s.CreaturePocket[c];
                if (!s.Lane.InRange(pocket, tile, range)) continue;

                int d = s.Lane.DistSq(pocket, tile);
                if (best < 0 || d < bestDistSq) { best = c; bestDistSq = d; }
            }
            return best;
        }

        /// Squared distance from a raider to the carrier NearestTaunter picked,
        /// or -1 when there is none. Split out so the assignment scan can rank
        /// on it without a second search returning a different answer.
        public static int NearestTaunterDistSq(SimState s, int raider)
        {
            int c = NearestTaunter(s, raider);
            if (c < 0) return -1;
            return s.Lane.DistSq(s.CreaturePocket[c], s.RaiderTile(raider));
        }

        /// The (distance, spawnIndex) total order of combat_engine 5.1, applied
        /// to the raiders competing for a finite Taunt capacity.
        ///
        /// int.MaxValue rather than Compare's -1 for "no carrier". Compare can
        /// afford -1 because AssignChill filters those raiders out before it
        /// sorts; spelling it as the worst key instead makes this one correct
        /// whether or not its caller filters first, and a comparator that ranks
        /// "unreachable" BEST is the kind of thing that survives until the day
        /// someone reuses it.
        public static int CompareTaunt(SimState s, int raiderA, int raiderB)
        {
            int da = NearestTaunterDistSq(s, raiderA);
            int db = NearestTaunterDistSq(s, raiderB);
            if (da < 0) da = int.MaxValue;
            if (db < 0) db = int.MaxValue;

            if (da != db) return da < db ? -1 : 1;
            if (raiderA != raiderB) return raiderA < raiderB ? -1 : 1;
            return 0;
        }
    }
}
