namespace Broodline.Sim.Combat
{
    /// A creature as deployed. data_model section 2: species, two traits with
    /// coverage, one Instinct. No level, no XP, no power score - everything
    /// about its strength derives from these.
    public struct CreatureSpec
    {
        public Species Species;
        public Trait Trait1;
        public int Tier1;
        public Trait Trait2;
        public int Tier2;
        public Instinct Instinct;
        public int Pocket;
    }

    /// The whole mutable world, in dense parallel arrays indexed by stable
    /// integer ID and iterated in ID order.
    ///
    /// Every array is sized at construction from the wave's spawn table, so
    /// the tick loop never allocates. A raider's array index IS its spawn
    /// index, which is what makes spawn index available as a tie-break without
    /// storing it.
    public sealed class SimState
    {
        public readonly WaveDef Wave;
        public readonly Lane Lane;

        public int Tick;
        public int Integrity;

        // --- Raiders. Index == spawn index. ---
        public readonly RaiderType[] RaiderType;
        public readonly int[] RaiderHp;
        public readonly Fix64[] RaiderProgress;
        public readonly bool[] RaiderAlive;
        public readonly bool[] RaiderChilled;      // recomputed every tick
        public int RaiderCount;                    // spawned so far

        // --- Creatures. Index == deployment order. ---
        public readonly Species[] CreatureSpecies;
        public readonly Trait[] CreatureTrait1;
        public readonly int[] CreatureTier1;
        public readonly Trait[] CreatureTrait2;
        public readonly int[] CreatureTier2;
        public readonly Instinct[] CreatureInstinct;
        public readonly int[] CreaturePocket;
        public readonly int[] CreatureHp;
        public readonly int[] CreatureTarget;        // raider id, or -1
        public readonly int[] CreatureAcquireAt;     // tick it may next acquire
        public readonly int[] CreatureNextAttackAt;  // tick it may next fire
        public readonly int CreatureCount;

        public SimState(WaveDef wave, Lane lane, CreatureSpec[] deployment)
        {
            Wave = wave;
            Lane = lane;
            Integrity = wave.Integrity;
            Tick = 0;

            int capacity = wave.Spawns.Length;
            RaiderType = new RaiderType[capacity];
            RaiderHp = new int[capacity];
            RaiderProgress = new Fix64[capacity];
            RaiderAlive = new bool[capacity];
            RaiderChilled = new bool[capacity];
            RaiderCount = 0;

            CreatureCount = deployment.Length;
            CreatureSpecies = new Species[CreatureCount];
            CreatureTrait1 = new Trait[CreatureCount];
            CreatureTier1 = new int[CreatureCount];
            CreatureTrait2 = new Trait[CreatureCount];
            CreatureTier2 = new int[CreatureCount];
            CreatureInstinct = new Instinct[CreatureCount];
            CreaturePocket = new int[CreatureCount];
            CreatureHp = new int[CreatureCount];
            CreatureTarget = new int[CreatureCount];
            CreatureAcquireAt = new int[CreatureCount];
            CreatureNextAttackAt = new int[CreatureCount];

            for (int c = 0; c < CreatureCount; c++)
            {
                CreatureSpecies[c] = deployment[c].Species;
                CreatureTrait1[c] = deployment[c].Trait1;
                CreatureTier1[c] = deployment[c].Tier1;
                CreatureTrait2[c] = deployment[c].Trait2;
                CreatureTier2[c] = deployment[c].Tier2;
                CreatureInstinct[c] = deployment[c].Instinct;
                CreaturePocket[c] = deployment[c].Pocket;
                CreatureHp[c] = Stats.CreatureHp(deployment[c].Species);
                CreatureTarget[c] = -1;
                CreatureAcquireAt[c] = 0;
                CreatureNextAttackAt[c] = 0;
            }
        }

        /// The raider's current tile, clamped into the table's bounds. Progress
        /// can reach the Ark tile exactly on the tick it breaches, and the
        /// distance table is only defined over [0, Tiles).
        public int RaiderTile(int r)
        {
            int tile = RaiderProgress[r].ToIntFloor();
            if (tile < 0) return 0;
            if (tile >= Lane.Tiles) return Lane.Tiles - 1;
            return tile;
        }

        public bool CreatureCarries(int c, Trait trait, out int tier)
        {
            if (CreatureTrait1[c] == trait) { tier = CreatureTier1[c]; return true; }
            if (CreatureTrait2[c] == trait) { tier = CreatureTier2[c]; return true; }
            tier = 0;
            return false;
        }

        public bool CreatureAlive(int c) => CreatureHp[c] > 0;
    }
}
