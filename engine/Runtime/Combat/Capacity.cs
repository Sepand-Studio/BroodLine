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

        /// The (distance, spawnIndex) total order of combat_engine 5.1.
        ///
        /// Nearest-first alone is not deterministic: 2.2 makes distance an
        /// exact integer, so ties are common, and a sort over equal keys can
        /// order them differently on two runtimes. Tie-breaking on spawn index
        /// makes this a total order, so any correct sort produces identical
        /// output everywhere.
        public static int Compare(SimState s, int raiderA, int raiderB, int pocket)
        {
            int da = s.Lane.DistSq(pocket, s.RaiderTile(raiderA));
            int db = s.Lane.DistSq(pocket, s.RaiderTile(raiderB));
            if (da != db) return da < db ? -1 : 1;
            if (raiderA != raiderB) return raiderA < raiderB ? -1 : 1;
            return 0;
        }

        /// Chill is one of combat_engine 5.1's three exceptions: its carrier
        /// may not be attacking the raider it slows, so assignment is
        /// nearest-first within range, re-evaluated every tick.
        ///
        /// "Nearest" is measured from the carrier pocket that is nearest to
        /// each candidate, since capacity sums across carriers standing in
        /// different pockets.
        public static void AssignChill(SimState s, int[] scratch)
        {
            for (int r = 0; r < s.RaiderCount; r++) s.RaiderChilled[r] = false;

            int capacity = TotalChillCapacity(s);
            if (capacity <= 0) return;

            int anchor = NearestChillPocket(s);
            if (anchor < 0) return;

            int n = 0;
            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r]) scratch[n++] = r;

            // Insertion sort: stable by construction over a total order, and
            // allocation-free. n is bounded by the wave's spawn count.
            for (int i = 1; i < n; i++)
            {
                int v = scratch[i];
                int j = i - 1;
                while (j >= 0 && Compare(s, scratch[j], v, anchor) > 0)
                {
                    scratch[j + 1] = scratch[j];
                    j--;
                }
                scratch[j + 1] = v;
            }

            int slowed = n < capacity ? n : capacity;
            for (int i = 0; i < slowed; i++) s.RaiderChilled[scratch[i]] = true;
        }

        /// The lowest-indexed pocket holding a live Chill carrier. Lowest index
        /// rather than "best" so the anchor is itself deterministic.
        private static int NearestChillPocket(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureCarries(c, Trait.Chill, out _))
                    return s.CreaturePocket[c];
            }
            return -1;
        }
    }
}
