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
        public Lane Lane { get; }

        private readonly SpawnEntry[] _spawns;

        /// The authored timeline. ReadOnlySpan over a CLONE, for the reason
        /// Lane.PocketTiles gives: a get-only array property is only read-only
        /// about the reference. Validate runs once at construction, so a caller
        /// that kept its array and edited an entry afterwards moved a verified
        /// wave out from under its own verification - mutating Spawns[0].Tick
        /// mid-run took a 540-tick outcome to 650.
        public ReadOnlySpan<SpawnEntry> Spawns => _spawns;

        /// The four-argument form keeps every existing caller compiling and gives
        /// synthetic waves the geometry they always had. Authored waves use the
        /// five-argument form, and Deployments.Unrecordable holds a run to it.
        public WaveDef(int id, int integrity, int laneCount, SpawnEntry[] spawns)
            : this(id, integrity, laneCount, Lane.Defile(), spawns) { }

        public WaveDef(int id, int integrity, int laneCount, Lane lane, SpawnEntry[] spawns)
        {
            Id = id; Integrity = integrity; LaneCount = laneCount; Lane = lane;
            _spawns = (SpawnEntry[])spawns.Clone();
        }

        /// Wave 1, waves_01_12: "6 Skirmishers . One every 2.0s from t=3 .
        /// Integrity 2 . Defile layout, 6 pockets". Slower and fewer than the
        /// Skirmisher's designed pattern on purpose - it is showing the player that
        /// placement produces a result, not testing them.
        public static WaveDef Wave1()
        {
            var spawns = new SpawnEntry[6];
            for (int i = 0; i < 6; i++)
                spawns[i] = new SpawnEntry { Tick = 3 * Stats.TicksPerSecond + i * 60, Type = RaiderType.Skirmisher };
            return new WaveDef(id: 1, integrity: 2, laneCount: 1, Lane.DefileSix(), spawns);
        }

        /// Wave 2: "8 Skirmishers . 1 Lash . Skirmishers from t=3, 1.5s apart .
        /// Lash at t=12". The Lash shares tick 360 with the seventh Skirmisher and
        /// is authored AFTER it, so spawn-index order keeps the lane busy when it
        /// arrives - "arriving into a busy lane is what makes it register".
        public static WaveDef Wave2()
        {
            var spawns = new SpawnEntry[9];
            int n = 0;
            for (int i = 0; i < 8; i++)
            {
                int tick = 3 * Stats.TicksPerSecond + i * 45;
                spawns[n++] = new SpawnEntry { Tick = tick, Type = RaiderType.Skirmisher };
                if (tick == 12 * Stats.TicksPerSecond)
                    spawns[n++] = new SpawnEntry { Tick = tick, Type = RaiderType.Lash };
            }
            return new WaveDef(id: 2, integrity: 2, laneCount: 1, Lane.DefileSix(), spawns);
        }

        /// Wave 6, from broodline_waves_01_12.md section 3:
        /// "1 . Defile . Integrity 2 . 1 Courser. Nothing else . Timeline t=3".
        public static WaveDef Wave6() => new WaveDef(
            id: 6, integrity: 2, laneCount: 1, Lane.Defile(),
            spawns: new[]
            {
                new SpawnEntry { Tick = 3 * Stats.TicksPerSecond, Type = RaiderType.Courser }
            });

        /// Wave 7, from broodline_waves_01_12.md: "1 Lash . 6 Skirmishers .
        /// Integrity 3 . Lash t=4, Skirmishers from t=6, 1.5s apart".
        /// The recovery wave after wave 6's designed loss.
        ///
        /// SIX Skirmishers, not the raider roster's eight. roster section 6
        /// gives the raider's arrival PATTERN - eights, 1.5s apart - and the
        /// wave table gives this wave's COMPOSITION. Where they differ the
        /// wave table wins, because it is the thing an author tuned against a
        /// budget: waves_01_12 puts this wave at 120 of 127 points.
        public static WaveDef Wave7()
        {
            var spawns = new SpawnEntry[7];
            spawns[0] = new SpawnEntry { Tick = 4 * Stats.TicksPerSecond, Type = RaiderType.Lash };
            for (int i = 0; i < 6; i++)
                spawns[i + 1] = new SpawnEntry
                {
                    Tick = 6 * Stats.TicksPerSecond + i * 45,   // 1.5s = 45 ticks
                    Type = RaiderType.Skirmisher
                };

            return new WaveDef(id: 7, integrity: 3, laneCount: 1, Lane.Defile(), spawns: spawns);
        }

        /// The two composition invariants of combat_engine section 5.4,
        /// checked at load so a content author cannot ship a violation.
        /// Replays store a wave ID rather than the wave, so there has to be a
        /// lookup. Throwing on an unknown ID is the point: a replay naming a
        /// wave this engine does not have must fail loudly at load rather than
        /// re-simulate something else.
        public static WaveDef ForId(int id)
        {
            if (id == 1) return Wave1();
            if (id == 2) return Wave2();
            if (id == 6) return Wave6();
            if (id == 7) return Wave7();
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
