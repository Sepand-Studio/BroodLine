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
        /// 2.5, 1.2, 1.1 - all exact multiples of a 30Hz tick except where
        /// noted, so they are transcribed as ticks directly rather than
        /// computed, keeping the value table free of rounding.
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

        // --- Raiders, section 5 ---

        public static int RaiderHp(RaiderType r) => r switch
        {
            RaiderType.Courser => 220,
            _ => 0
        };

        public static int RaiderIntegrityCost(RaiderType r) => r switch
        {
            RaiderType.Courser => 2,
            _ => 0
        };

        /// Speed in thousandths of a tile per second. Integer so the value
        /// table carries no fixed-point encoding; Lane converts once.
        public static int RaiderMilliTilesPerSec(RaiderType r) => r switch
        {
            RaiderType.Courser => 1600,
            _ => 0
        };

        /// The trait that answers this raider. combat_numbers section 222.
        public static Trait CounterFor(RaiderType r) => r switch
        {
            RaiderType.Courser => Trait.Chill,
            _ => Trait.None
        };

        /// Chill's effect: the affected raider moves at 0.5 tiles/sec.
        public const int ChilledMilliTilesPerSec = 500;

        /// Per-trait capacity. combat_numbers section 134 gives Chill 1/2/4 and
        /// section 115 says it twice in prose - "Chill III stops four of them".
        /// combat_engine section 5.3's global 1/3/5 ladder is a superseded
        /// placeholder; see the Phase 2 design doc section 2.
        public static int ChillCapacity(int tier) => tier switch
        {
            1 => 1, 2 => 2, 3 => 4,
            _ => 0
        };
    }
}
