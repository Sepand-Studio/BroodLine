namespace Broodline.Sim.Combat
{
    /// Attack rate and damage, including the two Instinct triggers that modify
    /// them. All modifiers are integer ratios so no float enters the core, and
    /// they are applied to the INTERVAL rather than to a rate, because the
    /// interval is what the tick loop compares against.
    public static class Attacks
    {
        /// Ticks between attacks, after Instinct modifiers and then Rally.
        ///
        /// Overwatch: -20% attack speed, so the interval grows by 5/4.
        /// Last Stand below 25% HP: +50% attack speed, so it shrinks by 2/3.
        /// Rally: doubled attack speed, so the interval halves.
        ///
        /// RALLY IS APPLIED LAST AND THE ORDER IS NORMATIVE. Integer division
        /// does not commute with the ratios above. Vetch is 45 ticks and is the
        /// starter species: under Overwatch, halving last gives 45*5/4 = 56 ->
        /// 28, and halving first gives 45/2 = 22 -> 27. The choice between 28
        /// and 27 is arbitrary; fixing it is not. Instinct describes the
        /// creature and Rally is a transient laid on top, so last is also the
        /// reading that matches the fiction.
        public static int IntervalTicks(SimState s, int c)
        {
            int interval = Stats.CreatureIntervalTicks(s.CreatureSpecies[c]);

            switch (s.CreatureInstinct[c])
            {
                case Instinct.Overwatch:
                    interval = interval * 5 / 4;
                    break;

                case Instinct.LastStand:
                    if (BelowFraction(s, c, 1, 4)) interval = interval * 2 / 3;
                    break;
            }

            if (s.Tick < s.CreatureRallyUntil[c]) interval /= 2;

            return interval;
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

        /// What a defender actually loses from an incoming hit, after Carapace.
        ///
        /// The other side of this file. Everything above modifies what a
        /// CREATURE deals; this modifies what one TAKES, and it is the only
        /// thing in the engine that does - combat_numbers section 4.2 is
        /// explicit that "no counter has a damage component", which is what
        /// keeps counters off the power ladder, and Carapace is a utility
        /// trait precisely so that it may be a number.
        ///
        /// Integer and truncating, like every other modifier here: 100 at
        /// -25% is 75, and the Lash's 30 at -25% is 22 rather than 22.5.
        /// Rounding toward zero costs the defender less than a point per hit
        /// and keeps the arithmetic exact on both runtimes.
        ///
        /// Tier 0 means the trait is not really carried - CreatureCarries
        /// reports a match on a Trait.None slot too - so the percentage table
        /// returning 0 there is load-bearing, not defensive.
        public static int DamageTaken(SimState s, int c, int incoming)
        {
            if (!s.CreatureCarries(c, Trait.Carapace, out int tier)) return incoming;

            int percent = Stats.CarapacePercent(tier);
            if (percent <= 0) return incoming;

            return incoming * (100 - percent) / 100;
        }

        /// Ticks between a raider's attacks. Zero means it does not attack at
        /// all, which is every raider but the Lash - see Stats section 6.
        ///
        /// No Instinct and no Rally on this side: those describe creatures.
        /// The indirection exists so phase 5 asks one question rather than
        /// reaching into the stat table itself, and so a raider that later
        /// gains a rate modifier has one place to gain it.
        public static int RaiderIntervalTicks(SimState s, int r) =>
            Stats.RaiderIntervalTicks(s.RaiderType[r]);

        /// Damage a raider lands per hit, before the defender's Carapace.
        public static int RaiderDamage(SimState s, int r) =>
            Stats.RaiderDamage(s.RaiderType[r]);
    }
}
