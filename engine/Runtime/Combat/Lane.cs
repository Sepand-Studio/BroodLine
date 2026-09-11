using System;

namespace Broodline.Sim.Combat
{
    /// Lane geometry, precomputed at construction.
    ///
    /// combat_engine section 2.2: a lane is an authored polyline sampled at
    /// authoring time, a raider's position is a single scalar along it, and a
    /// creature's position is a fixed tile. Every region ships a precomputed
    /// distance table, so range checking in the tick loop is an array lookup
    /// and a comparison - no roots, no trigonometry, no vectors.
    ///
    /// Distances are SQUARED and in whole tiles, which keeps them exact
    /// integers. combat_engine 2.2 notes this makes ties common rather than
    /// rare, which is why every comparator tie-breaks on spawn index.
    public sealed class Lane
    {
        /// Pockets sit one tile off the lane. That perpendicular offset is the
        /// +1 in every squared distance.
        private const int PerpendicularOffsetSq = 1;

        public int Tiles { get; }
        public int PocketCount { get; }

        /// Which authored family this geometry came from. A replay stores it and
        /// rebuilds the lane from it, so a Lane that does not know its own
        /// family cannot be recorded honestly - SimRunner used to stamp
        /// Terrain.Defile unconditionally, which meant any other geometry
        /// recorded a lie that Validate could not catch, because both sides of
        /// its cross-check derived from the same literal.
        public Terrain Family { get; }

        private readonly int[] _pocketTiles;
        private readonly int[] _distSq;   // [pocket * Tiles + tile]

        /// The tile each pocket sits beside. ReadOnlySpan rather than the array:
        /// _distSq is baked from these once in the constructor and never
        /// recomputed, so a write here would leave geometry and range checks
        /// silently disagreeing. View dereferences this every frame.
        public ReadOnlySpan<int> PocketTiles => _pocketTiles;

        public Lane(Terrain family, int tiles, int[] pocketTiles)
        {
            Family = family;
            Tiles = tiles;

            // CLONED, not aliased. The span above cannot be written THROUGH,
            // but storing the caller's reference left the caller holding a
            // writable handle to the same memory - so `a[0] = 23` after
            // construction moved PocketTiles[0] to 23 while _distSq stayed
            // baked at tile 6, which is precisely the "geometry and range
            // checks silently disagreeing" the property claims to prevent. At
            // a[0] = 1000000, Phases feeds it to DistSq and the tick loop
            // throws IndexOutOfRangeException.
            //
            // Once per lane, which is once per run.
            _pocketTiles = (int[])pocketTiles.Clone();
            PocketCount = _pocketTiles.Length;

            _distSq = new int[PocketCount * tiles];
            for (int p = 0; p < PocketCount; p++)
            {
                for (int t = 0; t < tiles; t++)
                {
                    int along = _pocketTiles[p] - t;
                    _distSq[p * tiles + t] = along * along + PerpendicularOffsetSq;
                }
            }
        }

        /// Defile - the terrain family wave 6 runs on. 24 tiles, 5 pockets.
        /// combat_numbers section 42: pockets sit beside tiles 6-20.
        public static Lane Defile() =>
            new Lane(Terrain.Defile, Stats.LaneTiles, new[] { 6, 10, 13, 17, 20 });

        public int DistSq(int pocket, int tile) => _distSq[pocket * Tiles + tile];

        public bool InRange(int pocket, int tile, int rangeTiles) =>
            rangeTiles * rangeTiles >= DistSq(pocket, tile);

        /// Converts a speed in thousandths of a tile per second into tiles per
        /// tick. Not exact in binary - 1600/30000 recurs - but Fix64 division
        /// is deterministic, and determinism rather than exactness is the
        /// property the engine needs.
        public static Fix64 SpeedPerTick(int milliTilesPerSec) =>
            Fix64.FromInt(milliTilesPerSec) /
            Fix64.FromInt(1000 * Stats.TicksPerSecond);
    }
}
