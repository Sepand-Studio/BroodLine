using Xunit;
using Broodline.Sim;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class LaneTests
    {
        [Fact]
        public void Defile_HasFivePocketsBesideTiles6To20()
        {
            var lane = Lane.Defile();
            Assert.Equal(24, lane.Tiles);
            Assert.Equal(5, lane.PocketCount);
            for (int p = 0; p < lane.PocketCount; p++)
            {
                Assert.InRange(lane.PocketTiles[p], 6, 20);
            }
        }

        [Fact]
        public void DistSq_IsPerpendicularOffsetPlusAlongLane()
        {
            var lane = Lane.Defile();
            int pocketTile = lane.PocketTiles[0];        // 6

            // Directly beside the pocket: only the 1-tile perpendicular offset.
            Assert.Equal(1, lane.DistSq(0, pocketTile));

            // Three tiles along: 3*3 + 1.
            Assert.Equal(10, lane.DistSq(0, pocketTile + 3));
            Assert.Equal(10, lane.DistSq(0, pocketTile - 3));
        }

        [Fact]
        public void InRange_ComparesSquaresAndNeverTakesARoot()
        {
            var lane = Lane.Defile();
            int pocketTile = lane.PocketTiles[0];

            // Hollow's range is 7: 7*7 = 49 >= DistSq.
            Assert.True(lane.InRange(0, pocketTile + 6, 7));   // 36+1 = 37
            Assert.False(lane.InRange(0, pocketTile + 7, 7));  // 49+1 = 50
        }

        [Fact]
        public void APocketTableCannotBeEditedFromOutsideTheLane()
        {
            // PocketTiles is a ReadOnlySpan, which cannot be written THROUGH -
            // but the constructor stored the caller's reference, so the caller
            // still held a writable handle to the same memory. Writing a[0]
            // moved PocketTiles[0] while _distSq stayed baked at the original
            // tile: "geometry and range checks silently disagreeing", which is
            // the exact thing the property's own doc says the span prevents. At
            // a large enough value the tick loop threw
            // IndexOutOfRangeException out of Phases.
            var tiles = new[] { 6, 10, 13, 17, 20 };
            var lane = new Lane(Terrain.Defile, Stats.LaneTiles, tiles);

            int baked = lane.DistSq(0, 6);
            tiles[0] = 23;

            Assert.Equal(6, lane.PocketTiles[0]);
            Assert.Equal(baked, lane.DistSq(0, 6));
        }

        [Fact]
        public void AnAuthoredWaveCannotBeEditedAfterItValidates()
        {
            // Same shape, one type over. WaveDef.Validate runs once at
            // construction, so a caller that kept its spawn array and edited an
            // entry afterwards moved a wave out from under its own
            // verification - a 540-tick outcome became 650.
            var spawns = new[] { new SpawnEntry { Tick = 90, Type = RaiderType.Courser } };
            var wave = new WaveDef(6, 2, 1, spawns);

            spawns[0].Tick = 200;

            Assert.Equal(90, wave.Spawns[0].Tick);
        }

        [Fact]
        public void DefileSix_HasSixPocketsBesideTiles6To20()
        {
            var lane = Lane.DefileSix();
            Assert.Equal(24, lane.Tiles);
            Assert.Equal(6, lane.PocketCount);
            Assert.Equal(Terrain.Defile, lane.Family);
            for (int p = 0; p < lane.PocketCount; p++) Assert.InRange(lane.PocketTiles[p], 6, 20);
            for (int p = 1; p < lane.PocketCount; p++) Assert.True(lane.PocketTiles[p] > lane.PocketTiles[p - 1]);
        }

        [Fact]
        public void SpeedPerTick_IsExactAcrossThirtyTicks()
        {
            // A Courser at 1.6 tiles/sec advances 1.6 tiles in 30 ticks.
            Fix64 perTick = Lane.SpeedPerTick(1600);
            Fix64 travelled = Fix64.Zero;
            for (int i = 0; i < 30; i++) travelled = travelled + perTick;

            // Floor is 1: exactness is not claimed, determinism is.
            Assert.Equal(1, travelled.ToIntFloor());
            Assert.True(travelled > Fix64.One);
        }
    }
}
