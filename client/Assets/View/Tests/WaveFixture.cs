using Broodline.Sim.Combat;

namespace Broodline.View.Tests
{
    /// Wave 6 as authored, built once for this assembly.
    ///
    /// The same five creatures - four Vetch and a Loam, none carrying Chill,
    /// pockets 0 to 4 - were typed out in WaveClockTests and again in
    /// WaveSnapshotTests, in the same folder and the same asmdef. Two more
    /// copies live outside it: GoldenTests.DeploymentWithoutChill on the xUnit
    /// side and WaveRunner.Deployment in Broodline.Game. Those two are across
    /// assembly boundaries and are left alone; these two had no excuse.
    internal static class WaveFixture
    {
        internal const ulong Seed = 6UL;

        internal static SimRunner Runner() =>
            new SimRunner(WaveDef.Wave6(), Lane.Defile(), Deployment(), Seed);

        internal static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };
    }
}
