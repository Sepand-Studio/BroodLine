using System;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// Deployments.Problem bounds every enum against a hardcoded width in
    /// Ids.cs - for the play path and the replay codec alike - because
    /// Enum.IsDefined allocates and reflects and the enums are contiguous from
    /// zero. That is a fair trade only while the constants and the enums agree,
    /// and nothing made them agree - an earlier comment in Replay.cs claimed
    /// this file existed when it did not.
    ///
    /// The cost of drift is a bad diagnosis, not just a missed check. Trait has
    /// five members today and combat_numbers names seven more; add Pierce
    /// without moving TraitCount and every replay carrying it is rejected as
    /// CORRUPT, sending the reader after a forgery that is not there. Splash
    /// was the example here until Splash was one of the three this engine
    /// gained, and TraitCount moved with it.
    public class EnumWidthTests
    {
        [Fact]
        public void TheWidthsReplayValidatesAgainstMatchTheEnums()
        {
#pragma warning disable xUnit2013   // these ARE width assertions, not size checks
            Assert.Equal(Ids.SpeciesCount, Enum.GetValues(typeof(Species)).Length);
            Assert.Equal(Ids.InstinctCount, Enum.GetValues(typeof(Instinct)).Length);
            Assert.Equal(Ids.TraitCount, Enum.GetValues(typeof(Trait)).Length);
            Assert.Equal(RaiderTypeCounts.RaiderTypeCount, Enum.GetValues(typeof(RaiderType)).Length);
#pragma warning restore xUnit2013
        }

        [Fact]
        public void TheEnumsAreContiguousFromZero()
        {
            // The bare int comparison in Deployments.Problem is only equivalent to
            // IsDefined while this holds. Ids.cs declares explicit values, so a
            // gap or a renumber is a one-character edit away - and replays
            // persist these values, so a renumber silently reinterprets every
            // record already written.
            AssertContiguous(typeof(Species));
            AssertContiguous(typeof(Instinct));
            AssertContiguous(typeof(Trait));
            AssertContiguous(typeof(RaiderType));
        }

        private static void AssertContiguous(Type t)
        {
            var values = Enum.GetValues(t);
            for (int i = 0; i < values.Length; i++)
                Assert.Equal(i, (int)values.GetValue(i));
        }
    }
}
