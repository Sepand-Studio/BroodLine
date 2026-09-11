using System;
using System.Collections.Generic;

namespace Broodline.Sim
{
    // A scripted poke: entity EntityId is nudged at tick Tick. No further semantics.
    public struct ToyInput
    {
        public int Tick;
        public int EntityId;
    }

    /// The smallest simulation that can drift — no raiders, no traits, no
    /// combat, just entities on a fixed timestep folded into a Hash — so the
    /// determinism harness is proven before there is real gameplay to test.
    /// One tick loop only: <see cref="Run"/> delegates to <see cref="RunToTick"/>
    /// with checkpoints disabled, rather than duplicating the loop.
    public static class ToySim
    {
        // Q32.32 constants. MoveStep is the deliberate single perturbation
        // point used to prove the golden test catches drift (Task 6 Step 5).
        private static readonly Fix64 StartingHealth = Fix64.FromInt(100);
        private static readonly Fix64 MoveStep = Fix64.FromInt(1);
        private static readonly Fix64 InputBump = Fix64.FromInt(5);
        private const int DrainBound = 3; // per-entity, per-tick health drain in [0, DrainBound)

        // Runs to completion, returning the whole run's folded hash. No
        // checkpoints: Task 7's corpus uses this entry point.
        public static ulong Run(ulong seed, int entityCount, ToyInput[] inputs, int ticks)
        {
            return RunToTick(seed, entityCount, inputs, ticks, 1, null);
        }

        // As Run, but also appends the running hash to checkpoints every
        // checkpointEvery ticks. Diagnostic only; pass null to disable.
        public static ulong RunToTick(ulong seed, int entityCount, ToyInput[] inputs, int ticks,
            int checkpointEvery, List<ulong> checkpoints)
        {
            var rng = new Rng(seed); // explicit, local — never a static/ambient stream
            var hash = Hash.Create(); // one accumulator for the whole run — order-sensitive by design

            var health = new Fix64[entityCount];
            var position = new Fix64[entityCount];
            var ids = new int[entityCount];
            for (int i = 0; i < entityCount; i++)
            {
                health[i] = StartingHealth;
                position[i] = Fix64.Zero;
                ids[i] = i;
            }

            // Sorted once, outside the loop, into total order (Tick, EntityId) —
            // matches the ascending-id order entities are folded in below.
            var sorted = (ToyInput[])inputs.Clone();
            Array.Sort(sorted, CompareByTickThenEntity);
            int next = 0;

            for (int tick = 0; tick < ticks; tick++)
            {
                for (int i = 0; i < entityCount; i++)
                {
                    position[i] += MoveStep;
                    health[i] -= Fix64.FromInt(rng.NextInt(DrainBound));

                    while (next < sorted.Length && sorted[next].Tick == tick && sorted[next].EntityId == ids[i])
                    {
                        health[i] += InputBump;
                        next++;
                    }

                    hash.Add(ids[i]);
                    hash.Add(health[i].Raw);
                    hash.Add(position[i].Raw);
                }

                if (checkpoints != null && (tick + 1) % checkpointEvery == 0)
                    checkpoints.Add(hash.Value); // amortised growth; accepted tension, see Task 6 report

                while (next < sorted.Length && sorted[next].Tick <= tick)
                    next++; // drop any input that never matched a valid entity id
            }

            return hash.Value;
        }

        private static int CompareByTickThenEntity(ToyInput a, ToyInput b)
        {
            return a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : a.EntityId.CompareTo(b.EntityId);
        }
    }
}
