using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimRunnerTests
    {
        [Fact]
        public void SteppedToCompletion_ReproducesGoldenA()
        {
            // Asserted against the PINNED LITERAL, not against Sim.Run - once
            // Run delegates to SimRunner, comparing the two proves nothing.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(2495532238167386945UL, r.Outcome.Hash);
            Assert.Equal(Result.Loss, r.Outcome.Result);
            Assert.Equal(1, r.Outcome.BreachCount);
            Assert.False(r.Outcome.Breaches[0].Access);
        }

        [Fact]
        public void SteppedToCompletion_ReproducesGoldenB()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(13482666686023521257UL, r.Outcome.Hash);
            Assert.Equal(Result.Win, r.Outcome.Result);
            Assert.Equal(2, r.Outcome.IntegrityRemaining);
        }

        [Fact]
        public void TrueReturnsEqualOutcomeTicks()
        {
            // The loop increments Tick only on a step that does NOT terminate,
            // so the count of true returns is exactly Outcome.Ticks. This is
            // the invariant that catches an off-by-one in the extraction - the
            // single likeliest way to get this wrong.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            int steps = 0;
            while (r.Step()) steps++;

            Assert.Equal(r.Outcome.Ticks, steps);
        }

        [Fact]
        public void StepAfterTermination_IsFalseAndHarmless()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }
            ulong hash = r.Outcome.Hash;

            Assert.False(r.Step());
            Assert.False(r.Step());
            Assert.Equal(hash, r.Outcome.Hash);
        }

        [Fact]
        public void TickAdvancesOneAtATime()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            Assert.Equal(0, r.Tick);
            r.Step();
            Assert.Equal(1, r.Tick);
            r.Step();
            Assert.Equal(2, r.Tick);
        }

        [Fact]
        public void ADeploymentOverTheCapIsRefusedBySimulationAndReplayAlike()
        {
            // The cap was enforced in Replay.Deserialize but not in the
            // simulation, so Sim.Run would run six creatures to completion and
            // write a 244-byte record it could not itself read back - the device
            // writes the file happily and verification later calls the honest
            // player's own run corrupt.
            var six = new CreatureSpec[Stats.DeploymentCap + 1];
            for (int i = 0; i < six.Length; i++)
                six[i] = new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard };

            var e = Assert.Throws<WaveCompositionException>(
                () => new SimRunner(WaveDef.Wave6(), Lane.Defile(), six, GoldenTests.Seed));
            Assert.Contains("cap", e.Message);
        }

        [Fact]
        public void OutcomeHandsOutNoWritableBreachBuffer()
        {
            // The version of this test that shipped with the previous fix
            // asserted 0 == 0. It read Breaches[0].Type, wrote
            // `Breaches[0] = default`, and compared the two - but
            // RaiderType.Courser is 0 and so is default(Breach).Type, so it
            // passed with the whole fix reverted. Asserting .Tick instead -
            // 540 against 0 - failed immediately, and did so because the fix
            // never worked: Outcome is copied by value, the array reference
            // inside it is not.
            //
            // So the guarantee is structural now. `Breaches[0] = default` does
            // not compile, and this asserts the shape that makes that true,
            // because a runtime test of a compile-time property can only ever
            // assert the wrong thing.
            var t = typeof(Outcome);
            Assert.Empty(t.GetFields(BindingFlags.Public | BindingFlags.Instance));
            foreach (var p in t.GetProperties())
                Assert.False(p.PropertyType.IsArray,
                    "Outcome." + p.Name + " hands out a raw array; callers share it with the runner.");

            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            // Bounded at BreachCount, which retires the "iterate to
            // BreachCount, never Length" hazard Outcome.cs used to warn about:
            // the two lengths are the same number.
            Assert.Equal(r.Outcome.BreachCount, r.Outcome.Breaches.Length);
            Assert.Equal(540, r.Outcome.Breaches[0].Tick);
        }

        [Fact]
        public void ReadOnlySurface_TracksTheLiveState()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

            Assert.Equal(2, r.Integrity);
            Assert.Equal(5, r.CreatureCount);
            Assert.Equal(24, r.LaneTiles);
            Assert.Equal(0, r.RaiderCount);          // the Courser spawns at t=90
            Assert.Equal(260, r.CreatureHp[0]);      // Vetch
            Assert.Equal(Species.Loam, r.CreatureSpecies[4]);

            // Step past the spawn tick and the raider surface populates.
            while (r.Tick < 91 && r.Step()) { }
            Assert.Equal(1, r.RaiderCount);
            Assert.True(r.RaiderAlive[0]);
            Assert.False(r.RaiderChilled[0]);        // no Chill in this deployment

            // Not an equality against 220. Spawn is phase 1 and Attack is phase
            // 5, so a creature can acquire and fire on the Courser's own spawn
            // tick - pinning "undamaged" here would be pinning phase ordering
            // in the wrong test.
            Assert.InRange(r.RaiderHp[0], 1, Stats.RaiderHp(RaiderType.Courser));
        }

        [Fact]
        public void SimRunner_ExposesNoPathToMutableState()
        {
            // The guarantee is structural, so it is asserted structurally: no
            // public property of SimRunner may hand out SimState, a raw array,
            // or an object that has one of those on ITS public surface. A
            // ReadOnlySpan property cannot be written through; a T[] property
            // can, and so can a T[] one hop further away.
            //
            // One hop is what the previous version was missing, and it was the
            // hop that mattered: zero of SimRunner's twenty-one properties were
            // ever flagged while Outcome carried a writable Breach[] and Lane
            // handed out its pocket table. It went green on a surface it was
            // not looking at.
            Assert.Empty(Leaks(typeof(SimRunner)));

            foreach (var m in typeof(SimRunner).GetMethods())
                Assert.False(m.ReturnType == typeof(SimState),
                    "SimRunner." + m.Name + " returns SimState.");
        }

        [Fact]
        public void TheReadOnlySurfaceGuardCanActuallyFail()
        {
            // A guard whose green is decoupled from the property it asserts is
            // worse than no guard: it is a claim. So the predicate above is
            // pointed at things that ARE violations and required to say so.
            //
            // SimState itself is the reference case - the whole mutable world,
            // reached in one hop through a public property.
            Assert.NotEmpty(Leaks(typeof(LeaksAnArray)));
            Assert.NotEmpty(Leaks(typeof(LeaksSimState)));
            Assert.NotEmpty(Leaks(typeof(LeaksOneHopAway)));
        }

        private sealed class LeaksAnArray { public int[] Table => null; }
        private sealed class LeaksSimState { public SimState World => null; }
        private sealed class LeaksOneHopAway { public LeaksAnArray Inner => null; }

        /// Public properties that hand out writable engine state, directly or
        /// one hop away.
        ///
        /// Properties only, deliberately. SerializeRecord() returns a freshly
        /// encoded byte[] and ReadRecord() returns a Replay built by Copy();
        /// both are copies whose whole purpose is to be handed out and written
        /// to, and both have their own tests. A property is a SURFACE - it
        /// reads as "the runner's X" - which is what makes the same array
        /// shape a mistake there and not in a method named Read or Serialize.
        private static List<string> Leaks(Type t)
        {
            var found = new List<string>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.PropertyType == typeof(SimState))
                { found.Add(t.Name + "." + p.Name + " hands out SimState"); continue; }
                if (p.PropertyType.IsArray)
                { found.Add(t.Name + "." + p.Name + " hands out a raw array; use ReadOnlySpan<T>"); continue; }

                foreach (var inner in p.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    if (inner.PropertyType == typeof(SimState) || inner.PropertyType.IsArray)
                        found.Add(t.Name + "." + p.Name + "." + inner.Name + " is writable one hop away");
                foreach (var inner in p.PropertyType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    if (inner.FieldType == typeof(SimState) || inner.FieldType.IsArray)
                        found.Add(t.Name + "." + p.Name + "." + inner.Name + " is writable one hop away");
            }
            return found;
        }
    }
}
