namespace Broodline.Sim.Combat
{
    /// Values copied from broodline_combat_numbers.md. This file owns no
    /// decisions - it is a transcription, and a change here is a balance patch.
    public static class Stats
    {
        public const int TicksPerSecond = 30;
        public const int LaneTiles = 24;
        public const int DeploymentCap = 5;
        public const int RetargetLockoutTicks = 12;   // 0.4s at 30Hz
        public const int HardTickCap = 5400;          // 180s
        public const int StallTicks = 300;            // 10s

        // --- Creatures, section 3 ---

        public static int CreatureHp(Species s) => s switch
        {
            Species.Vetch => 260, Species.Ember => 130, Species.Skitter => 80,
            Species.Hollow => 60, Species.Loam => 190, Species.Pale => 120,
            _ => 0
        };

        public static int CreatureDamage(Species s) => s switch
        {
            Species.Vetch => 14, Species.Ember => 36, Species.Skitter => 11,
            Species.Hollow => 55, Species.Loam => 18, Species.Pale => 21,
            _ => 0
        };

        /// Attack interval in ticks. Source values are seconds: 1.5, 1.7, 0.4,
        /// 2.5, 1.2, 1.1 - all exact multiples of a 30Hz tick, so they are
        /// transcribed as ticks directly rather than computed, keeping the
        /// value table free of rounding.
        public static int CreatureIntervalTicks(Species s) => s switch
        {
            Species.Vetch => 45, Species.Ember => 51, Species.Skitter => 12,
            Species.Hollow => 75, Species.Loam => 36, Species.Pale => 33,
            _ => 0
        };

        public static int CreatureRange(Species s) => s switch
        {
            Species.Vetch => 2, Species.Ember => 3, Species.Skitter => 2,
            Species.Hollow => 7, Species.Loam => 3, Species.Pale => 5,
            _ => 0
        };

        // --- Raiders, section 6 ---

        public static int RaiderHp(RaiderType r) => r switch
        {
            RaiderType.Courser => 220, RaiderType.Lash => 180, RaiderType.Skirmisher => 40,
            _ => 0
        };

        public static int RaiderIntegrityCost(RaiderType r) => r switch
        {
            RaiderType.Courser => 2, RaiderType.Lash => 2, RaiderType.Skirmisher => 1,
            _ => 0
        };

        /// Speed in thousandths of a tile per second. Integer so the value
        /// table carries no fixed-point encoding; Lane converts once.
        public static int RaiderMilliTilesPerSec(RaiderType r) => r switch
        {
            RaiderType.Courser => 1600, RaiderType.Lash => 500, RaiderType.Skirmisher => 900,
            _ => 0
        };

        /// The trait that answers this raider. combat_numbers section 4.2.
        ///
        /// Total over the three raiders now, so the `_ => Trait.None` arm is
        /// unreachable and stays only as the compiler's exhaustiveness escape.
        /// WaveDef.AssertNoSharedCounter reads this to reject a wave carrying
        /// two types with one answer, so the map being total is what makes
        /// that invariant able to fire at all.
        public static Trait CounterFor(RaiderType r) => r switch
        {
            RaiderType.Courser    => Trait.Chill,
            RaiderType.Lash       => Trait.Taunt,
            RaiderType.Skirmisher => Trait.Splash,
            _ => Trait.None
        };

        // --- Raider attacks, section 6 ---
        //
        // Lash is the ONLY raider in the roster that attacks: it "attacks the
        // furthest defender in range 5 for 30 every 2s, reaching past the
        // front line into support pockets". Every other raider's mechanic is
        // about movement, targetability or what happens on its death -
        // Skirmisher's row says "No attack" in as many words. So these three
        // tables return zero for everything else, and phase 5 reads a zero
        // INTERVAL as "does not attack" rather than as "attacks every tick".

        public static int RaiderDamage(RaiderType r) => r switch
        {
            RaiderType.Lash => 30,
            _ => 0
        };

        /// Attack interval in ticks. 2s at 30Hz, spelled as arithmetic for the
        /// reason CreatureIntervalTicks is not: that table transcribes six
        /// values that are all exact tick multiples, while this is one value
        /// the source states in seconds.
        public static int RaiderIntervalTicks(RaiderType r) => r switch
        {
            RaiderType.Lash => 2 * TicksPerSecond,
            _ => 0
        };

        public static int RaiderRange(RaiderType r) => r switch
        {
            RaiderType.Lash => 5,
            _ => 0
        };

        /// Chill's effect: the affected raider moves at 0.5 tiles/sec.
        public const int ChilledMilliTilesPerSec = 500;

        /// Per-trait capacity. combat_numbers section 4.1 puts Chill, Taunt and
        /// Splash on the SIMULTANEITY axis - tier buys how many raiders of that
        /// type the creature answers at once - which is why each is a count
        /// rather than a magnitude.
        ///
        /// Section 134 gives Chill 1/2/4 and section 115 says it twice in prose
        /// - "Chill III stops four of them". combat_engine section 5.3's global
        /// 1/3/5 ladder is a superseded placeholder; see the Phase 2 design doc
        /// section 2.
        public static int ChillCapacity(int tier) => tier switch
        {
            1 => 1, 2 => 2, 3 => 4,
            _ => 0
        };

        /// Taunt's capacity. combat_numbers section 4.2: "Forces 1 Lash to
        /// target this creature", 2 at II, 4 at III - the same ladder as Chill,
        /// against a different raider.
        public static int TauntCapacity(int tier) => tier switch
        {
            1 => 1, 2 => 2, 3 => 4,
            _ => 0
        };

        /// Carapace's incoming damage reduction, as a PERCENTAGE.
        /// combat_numbers section 4.3: -25% / -40% / -55%.
        ///
        /// Percent rather than a ratio so the value table carries no division;
        /// Attacks applies it once, as damage * (100 - percent) / 100.
        ///
        /// Carapace is on the MAGNITUDE axis and answers no raider, which is
        /// section 4.3's whole point: the four utility traits have to pull
        /// real weight or the counter species collapse into "the one trait I
        /// need in a body I do not want".
        public static int CarapacePercent(int tier) => tier switch
        {
            1 => 25, 2 => 40, 3 => 55,
            _ => 0
        };
    }
}
