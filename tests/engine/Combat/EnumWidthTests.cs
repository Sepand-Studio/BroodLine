using System;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// Replay.Validate bounds every deserialized enum against a hardcoded
    /// width, because Enum.IsDefined allocates and reflects and the enums are
    /// contiguous from zero. That is a fair trade only while the constants and
    /// the enums agree, and nothing made them agree - an earlier comment in
    /// Replay.cs claimed this file existed when it did not.
    ///
    /// The cost of drift is a bad diagnosis, not just a missed check. Trait has
    /// two members today and combat_engine names seven more; add Splash without
    /// moving TraitCount and every replay carrying it is rejected as CORRUPT,
    /// sending the reader after a forgery that is not there.
    public class EnumWidthTests
    {
        [Fact]
        public void TheWidthsReplayValidatesAgainstMatchTheEnums()
        {
            Assert.Equal(Enum.GetValues(typeof(Species)).Length, Replay.SpeciesCount);
            Assert.Equal(Enum.GetValues(typeof(Instinct)).Length, Replay.InstinctCount);
            Assert.Equal(Enum.GetValues(typeof(Trait)).Length, Replay.TraitCount);
        }

        [Fact]
        public void TheEnumsAreContiguousFromZero()
        {
            // The bare int comparison in Validate is only equivalent to
            // IsDefined while this holds. Ids.cs declares explicit values, so a
            // gap or a renumber is a one-character edit away - and replays
            // persist these values, so a renumber silently reinterprets every
            // record already written.
            AssertContiguous(typeof(Species));
            AssertContiguous(typeof(Instinct));
            AssertContiguous(typeof(Trait));
        }

        private static void AssertContiguous(Type t)
        {
            var values = Enum.GetValues(t);
            for (int i = 0; i < values.Length; i++)
                Assert.Equal(i, (int)values.GetValue(i));
        }
    }
}
