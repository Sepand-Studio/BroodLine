namespace Broodline.Sim
{
    public static class Corpus
    {
        /// Generates scenario N deterministically and returns its whole-run hash.
        /// Both runtimes must produce identical output for every index.
        public static ulong RunScenario(int index)
        {
            var gen = new Rng((ulong)(index + 1));
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
            return ToySim.Run((ulong)(index + 1), entities, inputs, ticks);
        }
    }
}
