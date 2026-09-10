using Xunit;

namespace Broodline.Sim.Tests
{
    public class FuzzTests
    {
        [Fact]
        public void RandomScenarios_AreSelfConsistent()
        {
            // Catches intra-runtime nondeterminism — unordered iteration,
            // uninitialised memory — without needing a second platform.
            for (ulong seed = 1; seed <= 200; seed++)
            {
                var gen = new Rng(seed);
                int entities = 1 + gen.NextInt(8);
                int ticks = 30 + gen.NextInt(120);
                int inputCount = gen.NextInt(6);

                var inputs = new ToyInput[inputCount];
                for (int i = 0; i < inputCount; i++)
                    inputs[i] = new ToyInput
                    {
                        Tick = gen.NextInt(ticks),
                        EntityId = gen.NextInt(entities)
                    };

                ulong first = ToySim.Run(seed, entities, inputs, ticks);
                ulong second = ToySim.Run(seed, entities, inputs, ticks);
                Assert.Equal(first, second);
            }
        }
    }
}
