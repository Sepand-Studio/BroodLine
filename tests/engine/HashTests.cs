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
    }
}
