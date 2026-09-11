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
    }
}
