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

        // --------------------------------------------------------------
        // Contract-pinning tests.
        //
        // These deliberately snapshot behaviour that Fix64 inherits from the
        // vendored FixPointCS implementation and does not change (see the
        // header comment on Fix64 itself for the full contract). A failure
        // here means the pinned behaviour changed underneath us — that is a
        // deliberate decision to revisit, and the fix is to update the
        // header comment together with the assertion, not to treat this test
        // as wrong.
        // --------------------------------------------------------------

        [Fact]
        public void Pinned_DivideByZero_PositiveDividend_SaturatesToLongMaxValue()
        {
            var result = Fix64.FromInt(5) / Fix64.Zero;
            Assert.Equal(long.MaxValue, result.Raw);
        }

        [Fact]
        public void Pinned_DivideByZero_NegativeDividend_AlsoSaturatesToLongMaxValue()
        {
            // Same saturation value as the positive case above: DivPrecise's
            // overflow guard returns 0x7fffffffffffffff before the caller's
            // sign is ever examined, so a negative dividend divided by zero
            // is a large POSITIVE value, not a large negative one.
            var result = Fix64.FromInt(-5) / Fix64.Zero;
            Assert.Equal(long.MaxValue, result.Raw);
        }

        [Fact]
        public void Pinned_Sqrt_OfNegative_ReturnsZeroRatherThanThrowing()
        {
            var result = Fix64.Sqrt(Fix64.FromInt(-4));
            Assert.Equal(Fix64.Zero, result);
        }

        [Fact]
        public void Pinned_Multiply_FloorsTowardNegativeInfinity()
        {
            // Raw(-1) is the smallest representable negative magnitude
            // (real value -2^-32); Raw(3) is real value 3*2^-32. Their exact
            // mathematical product is -3*2^-64, which in the result's Q32.32
            // scale is a fraction strictly between -1 and 0 raw units.
            // Flooring that lands on raw -1; truncating toward zero would
            // have landed on raw 0 (Zero) instead.
            var a = Fix64.FromRaw(-1);
            var b = Fix64.FromRaw(3);
            Assert.Equal(-1L, (a * b).Raw);
        }

        [Fact]
        public void Pinned_Divide_TruncatesTowardZero()
        {
            // Exact mathematical value of -1/3 in Q32.32 raw units is
            // -1431655765.333..., a non-terminating fraction. Truncating the
            // magnitude toward zero and reapplying the sign afterward lands
            // on raw -1431655765; flooring (as Multiply does) would instead
            // have landed one unit further from zero, at -1431655766.
            var a = Fix64.FromInt(-1);
            var b = Fix64.FromInt(3);
            Assert.Equal(-1431655765L, (a / b).Raw);
        }

        // --------------------------------------------------------------
        // Property tests.
        //
        // These assert invariants that must hold for ANY Fix64 values, not a
        // snapshot of today's behaviour — a failure here is a real defect in
        // an operator or override, not a change to acknowledge.
        // --------------------------------------------------------------

        // Deliberately includes both ends of the long range (long.MinValue
        // has no positive counterpart — negating it overflows — which is
        // exactly the kind of edge a hand-rolled comparator can get wrong),
        // values adjacent to those extremes, zero, values that straddle the
        // Q32.32 integer/fraction boundary (+/- 1L << 32), and ordinary
        // positive/negative values.
        static readonly long[] SampleRaws =
        {
            long.MinValue,
            long.MinValue + 1,
            -(1L << 40),
            -(1L << 32),
            -1L,
            0L,
            1L,
            1L << 32,
            1L << 40,
            long.MaxValue - 1,
            long.MaxValue,
        };

        [Fact]
        public void ComparisonOperators_AgreeWithSignOfCompareTo()
        {
            foreach (var ra in SampleRaws)
            {
                foreach (var rb in SampleRaws)
                {
                    var a = Fix64.FromRaw(ra);
                    var b = Fix64.FromRaw(rb);
                    int cmp = a.CompareTo(b);

                    Assert.True(cmp < 0 == a < b, $"< disagreed with CompareTo for ({ra}, {rb})");
                    Assert.True(cmp <= 0 == a <= b, $"<= disagreed with CompareTo for ({ra}, {rb})");
                    Assert.True(cmp > 0 == a > b, $"> disagreed with CompareTo for ({ra}, {rb})");
                    Assert.True(cmp >= 0 == a >= b, $">= disagreed with CompareTo for ({ra}, {rb})");
                    Assert.True((cmp == 0) == (a == b), $"== disagreed with CompareTo for ({ra}, {rb})");
                    Assert.True((cmp != 0) == (a != b), $"!= disagreed with CompareTo for ({ra}, {rb})");
                }
            }
        }

        [Fact]
        public void CompareTo_IsAntisymmetric()
        {
            foreach (var ra in SampleRaws)
            {
                foreach (var rb in SampleRaws)
                {
                    var a = Fix64.FromRaw(ra);
                    var b = Fix64.FromRaw(rb);
                    int ab = a.CompareTo(b);
                    int ba = b.CompareTo(a);

                    if (ab < 0) Assert.True(ba > 0, $"antisymmetry failed for ({ra}, {rb})");
                    else if (ab > 0) Assert.True(ba < 0, $"antisymmetry failed for ({ra}, {rb})");
                    else Assert.Equal(0, ba);
                }
            }
        }

        [Fact]
        public void CompareTo_IsTransitive()
        {
            foreach (var ra in SampleRaws)
            foreach (var rb in SampleRaws)
            foreach (var rc in SampleRaws)
            {
                var a = Fix64.FromRaw(ra);
                var b = Fix64.FromRaw(rb);
                var c = Fix64.FromRaw(rc);

                if (a.CompareTo(b) <= 0 && b.CompareTo(c) <= 0)
                    Assert.True(a.CompareTo(c) <= 0,
                        $"transitivity failed: {ra} <= {rb} <= {rc} but CompareTo(a,c) = {a.CompareTo(c)}");
            }
        }

        [Fact]
        public void FromRaw_RoundTripsThroughRaw()
        {
            foreach (var r in SampleRaws)
                Assert.Equal(r, Fix64.FromRaw(r).Raw);
        }

        [Fact]
        public void Equality_AgreesAcrossEqualsAndOperatorsAndHashCode()
        {
            foreach (var ra in SampleRaws)
            {
                foreach (var rb in SampleRaws)
                {
                    var a = Fix64.FromRaw(ra);
                    var b = Fix64.FromRaw(rb);
                    bool opEquals = a == b;

                    Assert.True(opEquals == a.Equals(b), $"Equals(Fix64) disagreed with == for ({ra}, {rb})");
                    Assert.True(opEquals == a.Equals((object)b), $"Equals(object) disagreed with == for ({ra}, {rb})");
                    Assert.True(!opEquals == (a != b), $"!= disagreed with == for ({ra}, {rb})");

                    if (opEquals)
                        Assert.Equal(a.GetHashCode(), b.GetHashCode());
                }
            }
        }

        [Fact]
        public void Equals_Object_RejectsNullAndOtherTypes()
        {
            var a = Fix64.FromInt(1);
            Assert.False(a.Equals(null));
            Assert.False(a.Equals("not a Fix64"));
            Assert.False(a.Equals(1));
        }
    }
}
