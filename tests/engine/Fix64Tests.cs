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

        [Fact]
        public void Pinned_Negation_IsIdentityAtLongMinValue()
        {
            // Fix64 exposes no unary minus. Negation is written Zero - a, which
            // is Fixed64.Sub's plain unchecked `0 - a.Raw`, so it wraps: at
            // Raw == long.MinValue it hands back the very same value. The other
            // route to a negation reduces to the same unchecked long negation.
            var a = Fix64.FromRaw(long.MinValue);
            Assert.Equal(long.MinValue, (Fix64.Zero - a).Raw);
            Assert.Equal(long.MinValue, (Fix64.FromInt(-1) * a).Raw);

            // Zero and FromRaw(long.MinValue) are the only two values that are
            // their own negation: x == -x modulo 2^64 exactly when 2x is a
            // multiple of 2^64. One raw unit away, negation behaves normally.
            Assert.Equal(0L, (Fix64.Zero - Fix64.Zero).Raw);
            Assert.Equal(long.MaxValue, (Fix64.Zero - Fix64.FromRaw(long.MinValue + 1)).Raw);
        }

        [Fact]
        public void Pinned_DivisionSignIdentity_FailsAtLongMinValue_WithoutSaturating()
        {
            // (Zero - a) / b == Zero - (a / b) fails at Raw == long.MinValue for
            // the reason pinned above -- negating the dividend changes nothing --
            // and NOT because the division saturated. This quotient is an
            // ordinary computed value, nowhere near long.MaxValue.
            var a = Fix64.FromRaw(long.MinValue);
            var b = Fix64.FromInt(3);
            var quotient = a / b;

            Assert.Equal(-3074457345618258602L, quotient.Raw);
            Assert.NotEqual(long.MaxValue, quotient.Raw);

            // Negating the dividend leaves the quotient untouched...
            Assert.Equal(quotient.Raw, ((Fix64.Zero - a) / b).Raw);
            // ...while negating the quotient genuinely negates it, so the two
            // sides of the identity disagree.
            Assert.Equal(3074457345618258602L, (Fix64.Zero - quotient).Raw);
            Assert.NotEqual(((Fix64.Zero - a) / b).Raw, (Fix64.Zero - quotient).Raw);

            // One raw unit away the dividend negates properly, and with the same
            // divisor on the same non-saturating path the identity does hold --
            // so long.MinValue, not the divisor and not the rounding, is the
            // cause of the failure above.
            var negatable = Fix64.FromRaw(long.MinValue + 1);
            Assert.Equal((Fix64.Zero - (negatable / b)).Raw, ((Fix64.Zero - negatable) / b).Raw);
        }

        [Fact]
        public void Pinned_MultiplicationSignIdentity_FailsAtLongMinValue_WithNoRemainderDiscarded()
        {
            // Raw(long.MinValue) is the real value -2^31 and Raw(3) is 3*2^-32,
            // so the exact product is -1.5 -- raw -6442450944, representable with
            // nothing at all discarded below the format's 2^-32 resolution. The
            // sign identity still fails, so a discarded remainder is not the only
            // way it can fail; here the un-negatable operand is the sole cause.
            var a = Fix64.FromRaw(long.MinValue);
            var b = Fix64.FromRaw(3);
            var product = a * b;

            Assert.Equal(-6442450944L, product.Raw);
            Assert.Equal(product.Raw, ((Fix64.Zero - a) * b).Raw);
            Assert.Equal(6442450944L, (Fix64.Zero - product).Raw);
            Assert.NotEqual(((Fix64.Zero - a) * b).Raw, (Fix64.Zero - product).Raw);

            // Contrast: a negatable operand whose product is likewise exact --
            // FromInt(-2) * Raw(3) is raw -6, again with no remainder -- and the
            // identity holds.
            var negatable = Fix64.FromInt(-2);
            Assert.Equal(-6L, (negatable * b).Raw);
            Assert.Equal((Fix64.Zero - (negatable * b)).Raw, ((Fix64.Zero - negatable) * b).Raw);
        }

        [Fact]
        public void Pinned_Multiply_OverflowsSilentlyWithoutSaturating()
        {
            // Unlike DivPrecise, Mul carries no overflow guard whatsoever: it is
            // plain unchecked long arithmetic, so a product too large for the
            // format wraps to an unrelated value rather than saturating or
            // throwing. int.MaxValue squared is about 4.6e18; Mul returns the
            // real value 1.
            var big = Fix64.FromInt(int.MaxValue);
            Assert.Equal(Fix64.One, big * big);

            // Raw(long.MinValue) is the real value -2^31. Times 2 the true
            // product is -2^32 and the result is 0; times 3 it happens to land on
            // long.MinValue. That extremum is a coincidence of the wrap, not
            // saturation -- as the times-2 case landing on 0 demonstrates.
            var a = Fix64.FromRaw(long.MinValue);
            Assert.Equal(0L, (a * Fix64.FromInt(2)).Raw);
            Assert.Equal(long.MinValue, (a * Fix64.FromInt(3)).Raw);
        }

        [Fact]
        public void Pinned_Divide_QuotientAboveLongMaxValueWrapsInsteadOfSaturating()
        {
            // DivPrecise's overflow guard is (|a.Raw| >> 32) >= |b.Raw|, which is
            // not the same test as "the quotient fits": it fires only once the
            // exact quotient's raw magnitude reaches 2^64, twice what a long
            // holds. A quotient landing between long.MaxValue and 2^64 slips past
            // it and is wrapped -- here flipping the sign, so two positive
            // operands produce a large negative result. The exact quotient is raw
            // +9223372039002259455.
            var a = Fix64.FromRaw(long.MaxValue);
            var b = Fix64.FromRaw(0xFFFFFFFFL);
            var quotient = a / b;

            Assert.True(a.Raw > 0 && b.Raw > 0);
            Assert.Equal(-9223372034707292161L, quotient.Raw);
            Assert.NotEqual(long.MaxValue, quotient.Raw);

            // The guard's threshold, pinned to the raw unit. For this dividend
            // |a.Raw| >> 32 is 2^31 - 1, so a divisor of 2^31 - 1 fires the guard
            // and saturates, while a divisor one raw unit larger does not fire it
            // and wraps instead -- to raw -2, from a division of two positives.
            Assert.Equal(long.MaxValue, (a / Fix64.FromRaw((1L << 31) - 1)).Raw);
            Assert.Equal(-2L, (a / Fix64.FromRaw(1L << 31)).Raw);
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
