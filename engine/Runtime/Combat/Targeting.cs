namespace Broodline.Sim.Combat
{
    /// combat_engine section 6. Instinct is a target-selection predicate plus
    /// an optional trigger, not a behaviour tree - six rules, each a comparator
    /// over the raiders currently in range.
    ///
    /// Ties break on spawn index ascending, everywhere. The RNG is used only
    /// where a tie-break must not be predictable, which in this slice is
    /// nowhere: Contrary and the Aberrants are deferred.
    public static class Targeting
    {
        /// Overwatch is passive: +25% range, -20% attack speed. Applied as
        /// integer arithmetic - range * 5 / 4 - so no float enters the core.
        public static int EffectiveRange(SimState s, int creature)
        {
            int baseRange = Stats.CreatureRange(s.CreatureSpecies[creature]);
            return s.CreatureInstinct[creature] == Instinct.Overwatch
                ? baseRange * 5 / 4
                : baseRange;
        }

        public static bool CanReach(SimState s, int creature, int raider)
        {
            if (!s.RaiderAlive[raider]) return false;
            return s.Lane.InRange(
                s.CreaturePocket[creature],
                s.RaiderTile(raider),
                EffectiveRange(s, creature));
        }

        /// Returns the raider this creature's Instinct selects, or -1.
        ///
        /// One linear pass in ID order, keeping the best so far. Because the
        /// scan runs in ascending raider order and a challenger only wins on a
        /// STRICT improvement, an equal key leaves the earlier raider in place
        /// - which is the spawn-index tie-break, achieved without a sort.
        public static int Select(SimState s, int creature)
        {
            int best = -1;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!CanReach(s, creature, r)) continue;
                if (best < 0) { best = r; continue; }
                if (Prefers(s, creature, r, best)) best = r;
            }
            return best;
        }

        /// Strictly prefers challenger over incumbent under this Instinct.
        private static bool Prefers(SimState s, int creature, int challenger, int incumbent)
        {
            switch (s.CreatureInstinct[creature])
            {
                case Instinct.Bloodscent:
                    // Lowest current HP in range.
                    return s.RaiderHp[challenger] < s.RaiderHp[incumbent];

                case Instinct.Vanguard:
                    // Closest to the Ark - greatest progress along the lane.
                    return s.RaiderProgress[challenger] > s.RaiderProgress[incumbent];

                case Instinct.Overwatch:
                    // Furthest in range, measured from the creature.
                    return DistSq(s, creature, challenger) > DistSq(s, creature, incumbent);

                default:
                    // LastStand, Skittish and PackSense all target nearest.
                    return DistSq(s, creature, challenger) < DistSq(s, creature, incumbent);
            }
        }

        private static int DistSq(SimState s, int creature, int raider) =>
            s.Lane.DistSq(s.CreaturePocket[creature], s.RaiderTile(raider));

        /// The defender a raider attacks, or -1. The other direction of the
        /// same phase-4 question, and the only raider it has an answer for is
        /// the Lash: combat_numbers section 6 gives it "the furthest defender
        /// in range 5", and every other raider has range 0 and selects nothing.
        ///
        /// FURTHEST, not nearest, and that inversion IS the mechanic - the
        /// Lash "reaches past the front line into support pockets", so a wall
        /// in the near pocket does not shield the thin bodies behind it. That
        /// is what Taunt exists to override.
        ///
        /// One linear pass in creature order, keeping the best so far. Because
        /// the scan runs in ascending creature order and a challenger only wins
        /// on a STRICT improvement, an equal distance leaves the EARLIER
        /// creature in place - creature index is deployment order, the stable
        /// index on this side of the board, and it is the same tie-break spawn
        /// index gives on the raider side. Lane 2.2 makes squared distances
        /// exact integers and says outright that this makes ties common rather
        /// than rare, so a comparator that stopped at "furthest" would not be a
        /// total order, and two runtimes could rank the tied pair differently.
        public static int SelectDefender(SimState s, int raider)
        {
            if (!s.RaiderAlive[raider]) return -1;

            int range = Stats.RaiderRange(s.RaiderType[raider]);
            if (range <= 0) return -1;      // this raider has no attack

            int tile = s.RaiderTile(raider);
            int best = -1;
            int bestDistSq = 0;

            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;

                int pocket = s.CreaturePocket[c];
                if (!s.Lane.InRange(pocket, tile, range)) continue;

                int d = s.Lane.DistSq(pocket, tile);
                if (best < 0 || d > bestDistSq) { best = c; bestDistSq = d; }
            }
            return best;
        }
    }
}
