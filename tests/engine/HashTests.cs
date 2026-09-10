using Xunit;

namespace Broodline.Sim.Tests
{
    public class HashTests
    {
        [Fact]
        public void SameInputs_SameHash()
        {
            var a = Hash.Create(); a.Add(1); a.Add(2); a.Add(3);
            var b = Hash.Create(); b.Add(1); b.Add(2); b.Add(3);
            Assert.Equal(a.Value, b.Value);
        }

        [Fact]
        public void OrderMatters()
        {
            var a = Hash.Create(); a.Add(1); a.Add(2);
            var b = Hash.Create(); b.Add(2); b.Add(1);
            Assert.NotEqual(a.Value, b.Value);
        }

        [Fact]
        public void EmptyHash_IsTheFnvOffsetBasis()
        {
            Assert.Equal(14695981039346656037UL, Hash.Create().Value);
        }

        [Fact]
        public void Add_PinsKnownFnv1aValues()
        {
            // Pins the algorithm Add() actually performs -- byte order, fold
            // order, and the prime constant -- not just the initial state
            // (EmptyHash_IsTheFnvOffsetBasis only covers that, before any byte
            // is folded). These literals are folds of Add(1), and of Add(1)
            // then Add(2), taken verbatim from a real run of this code, never
            // computed by hand. If either changes, the fold algorithm changed
            // and every previously-recorded hash comparison across runs is
            // invalid.
            var a = Hash.Create();
            a.Add(1);
            Assert.Equal(12161961113530546194UL, a.Value);

            var b = Hash.Create();
            b.Add(1);
            b.Add(2);
            Assert.Equal(17633220212635757292UL, b.Value);
        }
    }
}
