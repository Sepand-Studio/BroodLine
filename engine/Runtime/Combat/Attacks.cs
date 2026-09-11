namespace Broodline.Sim.Combat
{
    /// Attack rate and damage, including the two Instinct triggers that modify
    /// them. All modifiers are integer ratios so no float enters the core, and
    /// they are applied to the INTERVAL rather than to a rate, because the
    /// interval is what the tick loop compares against.
    public static class Attacks
    {
        /// Ticks between attacks, after Instinct modifiers.
        ///
        /// Overwatch: -20% attack speed, so the interval grows by 5/4.
        /// Last Stand below 25% HP: +50% attack speed, so it shrinks by 2/3.
        public static int IntervalTicks(SimState s, int c)
        {
            int interval = Stats.CreatureIntervalTicks(s.CreatureSpecies[c]);

            switch (s.CreatureInstinct[c])
            {
                case Instinct.Overwatch:
                    return interval * 5 / 4;

                case Instinct.LastStand:
                    return BelowFraction(s, c, 1, 4) ? interval * 2 / 3 : interval;

                default:
                    return interval;
            }
        }

        /// Damage per hit, after Instinct modifiers.
        ///
        /// Pack Sense: +15% to BOTH when an adjacent pocket holds a live ally
        /// of the same species. Only the carrier's own bonus is computed here;
        /// the ally's own call sees the same adjacency and gets the same
        /// answer, which is what "to both" means without shared state.
        public static int Damage(SimState s, int c)
        {
            int damage = Stats.CreatureDamage(s.CreatureSpecies[c]);

            if (s.CreatureInstinct[c] == Instinct.PackSense && HasPackAlly(s, c))
                damage = damage * 115 / 100;

            return damage;
        }

        private static bool HasPackAlly(SimState s, int c)
        {
            int pocket = s.CreaturePocket[c];
            for (int other = 0; other < s.CreatureCount; other++)
            {
                if (other == c || !s.CreatureAlive(other)) continue;
                if (s.CreatureSpecies[other] != s.CreatureSpecies[c]) continue;

                int delta = s.CreaturePocket[other] - pocket;
                if (delta == 1 || delta == -1) return true;
            }
            return false;
        }

        /// current HP < max * numerator / denominator, without division by a
        /// possibly-zero max and without float.
        public static bool BelowFraction(SimState s, int c, int numerator, int denominator)
        {
            int max = Stats.CreatureHp(s.CreatureSpecies[c]);
            return s.CreatureHp[c] * denominator < max * numerator;
        }
    }
}
