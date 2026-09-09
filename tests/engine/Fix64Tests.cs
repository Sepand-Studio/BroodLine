using Xunit;

namespace Broodline.Sim.Tests
{
    public class Fix64Tests
    {
        [Fact]
        public void One_HasQ32Point32Scale()
        {
            Assert.Equal(1L << 32, Fix64.One.Raw);
        }

        [Fact]
        public void Arithmetic_RoundTripsThroughIntegers()
        {
            var a = Fix64.FromInt(7);
            var b = Fix64.FromInt(3);
            Assert.Equal(10, (a + b).ToIntFloor());
            Assert.Equal(4, (a - b).ToIntFloor());
            Assert.Equal(21, (a * b).ToIntFloor());
            Assert.Equal(2, (a / b).ToIntFloor());
        }

        [Fact]
        public void Multiply_IsExactAtHalves()
        {
            var half = Fix64.One / Fix64.FromInt(2);
            Assert.Equal(Fix64.One.Raw, (half * Fix64.FromInt(2)).Raw);
        }

        [Fact]
        public void Sqrt_OfPerfectSquares_IsExact()
        {
            Assert.Equal(Fix64.FromInt(5).Raw, Fix64.Sqrt(Fix64.FromInt(25)).Raw);
            Assert.Equal(Fix64.FromInt(12).Raw, Fix64.Sqrt(Fix64.FromInt(144)).Raw);
        }

        [Fact]
        public void Comparison_OrdersCorrectlyAcrossZero()
        {
            var neg = Fix64.FromInt(-3);
            var pos = Fix64.FromInt(3);
            Assert.True(neg < Fix64.Zero);
            Assert.True(Fix64.Zero < pos);
            Assert.True(neg < pos);
            Assert.Equal(-3, neg.ToIntFloor());
        }
    }
}
