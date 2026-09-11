using System;
using static Broodline.Sim.Combat.RaiderTypeCounts;

namespace Broodline.Sim.Combat
{
    public sealed class WaveCompositionException : Exception
    {
        public WaveCompositionException(string message) : base(message) { }
    }

    public struct SpawnEntry
    {
        public int Tick;
        public RaiderType Type;
    }

    /// An authored wave. Spawns are ordered by tick ascending, then by the
    /// order authored - the array index IS the spawn index, and spawn index is
    /// the universal tie-break, so its order is load-bearing rather than
    /// incidental.
    public sealed class WaveDef
    {
        public const int MaxRaiderTypes = 4;

        public int Id { get; }
        public int Integrity { get; }
        public int LaneCount { get; }
        public SpawnEntry[] Spawns { get; }

        public WaveDef(int id, int integrity, int laneCount, SpawnEntry[] spawns)
        {
            Id = id;
            Integrity = integrity;
            LaneCount = laneCount;
            Spawns = spawns;
        }

        /// Wave 6, from broodline_waves_01_12.md section 3:
        /// "1 . Defile . Integrity 2 . 1 Courser. Nothing else . Timeline t=3".
        public static WaveDef Wave6() => new WaveDef(
            id: 6, integrity: 2, laneCount: 1,
            spawns: new[]
            {
                new SpawnEntry { Tick = 3 * Stats.TicksPerSecond, Type = RaiderType.Courser }
            });

        /// The two composition invariants of combat_engine section 5.4,
        /// checked at load so a content author cannot ship a violation.
        /// Replays store a wave ID rather than the wave, so there has to be a
        /// lookup. Throwing on an unknown ID is the point: a replay naming a
        /// wave this engine does not have must fail loudly at load rather than
        /// re-simulate something else.
        public static WaveDef ForId(int id)
        {
            if (id == 6) return Wave6();
            throw new WaveCompositionException("no authored wave with id " + id);
        }

        public void Validate()
        {
            AssertSpawnsOrdered();
            AssertTypeCount(DistinctTypeCount());
            AssertNoSharedCounter();
        }

        public int DistinctTypeCount()
        {
            // Dense scan over the enum rather than a HashSet, which is banned.
            int count = 0;
            for (int t = 0; t < RaiderTypeCount; t++)
            {
                for (int i = 0; i < Spawns.Length; i++)
                {
                    if ((int)Spawns[i].Type == t) { count++; break; }
                }
            }
            return count;
        }

        public static void AssertTypeCount(int distinctTypes)
        {
            if (distinctTypes > MaxRaiderTypes)
                throw new WaveCompositionException(
                    "A wave may carry at most " + MaxRaiderTypes +
                    " raider types; this one carries " + distinctTypes + ".");
        }

        /// "Never two raiders answered by the same trait in one wave." Two
        /// spawns of the same TYPE are volume and are legal; two different
        /// types sharing a counter are not.
        private void AssertNoSharedCounter()
        {
            for (int t = 0; t < RaiderTypeCount; t++)
            {
                if (!ContainsType((RaiderType)t)) continue;
                for (int u = t + 1; u < RaiderTypeCount; u++)
                {
                    if (!ContainsType((RaiderType)u)) continue;
                    if (Stats.CounterFor((RaiderType)t) == Stats.CounterFor((RaiderType)u))
                        throw new WaveCompositionException(
                            "Raider types " + (RaiderType)t + " and " + (RaiderType)u +
                            " are both answered by " + Stats.CounterFor((RaiderType)t) +
                            "; a wave may not contain both.");
                }
            }
        }

        private bool ContainsType(RaiderType type)
        {
            for (int i = 0; i < Spawns.Length; i++)
                if (Spawns[i].Type == type) return true;
            return false;
        }

        private void AssertSpawnsOrdered()
        {
            for (int i = 1; i < Spawns.Length; i++)
                if (Spawns[i].Tick < Spawns[i - 1].Tick)
                    throw new WaveCompositionException(
                        "Spawns must be ordered by tick ascending; entry " + i +
                        " at tick " + Spawns[i].Tick + " follows tick " +
                        Spawns[i - 1].Tick + ".");
        }
    }
}
