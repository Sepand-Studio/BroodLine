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
