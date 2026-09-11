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
        public void ABreachThatDoesNotEndTheWaveIsStillVisibleToTheRenderer()
        {
            // The accessor compared the logged breach tick against _s.Tick,
            // which Step advances on every tick that does NOT terminate - so it
            // answered true only for a breach that ended the wave, and wave 6
            // has exactly that shape. Measured before the fix: integrity 6 and
            // two Coursers logs breaches at ticks 540 and 750, and the
            // renderer's read point saw the flag true ONCE.
            //
            // Polled exactly where WaveSnapshot.Capture polls it: after Step
            // returns, from WaveClock's onTick.
            var wave = new WaveDef(6, 6, 1, new[]
            {
                new SpawnEntry { Tick = 90,  Type = RaiderType.Courser },
                new SpawnEntry { Tick = 300, Type = RaiderType.Courser }
            });
            var r = new SimRunner(wave, Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

            int seen = 0;
            for (int i = 0; i <= Stats.HardTickCap + 1; i++)
            {
                bool more = r.Step();
                for (int raider = 0; raider < r.RaiderCount; raider++)
                    if (r.RaiderBreachedThisTick(raider)) seen++;
                if (!more) break;
            }

            Assert.Equal(2, r.Outcome.BreachCount);
            Assert.Equal(r.Outcome.BreachCount, seen);
        }

        [Fact]
        public void AliveCountsDoNotCountCorpses()
        {
            // The property the accessors exist for. RaiderCount is the number
            // SPAWNED and never goes down, so a ladder keyed off it degrades on
            // a board that has emptied.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(1, r.RaiderCount);          // one Courser, spawned
            Assert.Equal(0, r.AliveRaiderCount);     // and gone at the Ark
            Assert.Equal(5, r.AliveCreatureCount);   // the roster survives wave 6
        }

        [Fact]
        public void ARunOnContentTheRecordCannotDescribeRefusesToRecord()
        {
            // The worst shape in the family, and the one nothing reported: a
            // WaveDef claiming id 6 while carrying integrity 8 ran to Win with
            // hash 8532104364784216008, its record VALIDATED, and Sim.Replay
            // returned Loss with hash 2495532238167386945. An honest player's
            // win scored as a loss on the verification path.
            //
            // The record stores an id and a family, not their contents, so the
            // only content a RECORDED run may use is what this engine rebuilds
            // for them. Running unauthored content is still allowed - FuzzTests
            // does it twelve times a run - it just cannot be written down.
            var lying = new WaveDef(6, 8, 1, new[]
            {
                new SpawnEntry { Tick = 90, Type = RaiderType.Courser }
            });
            var r = new SimRunner(lying, Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

            Assert.NotNull(r.Unrecordable);
            Assert.Contains("integrity", r.Unrecordable);
            Assert.Throws<WaveCompositionException>(() => r.SerializeRecord());
            Assert.Throws<WaveCompositionException>(() => r.ReadRecord());

            // Same family, one axis over: a lane that claims a family whose
            // geometry it does not have. This one at least failed loudly on
            // load before; now it never gets written.
            var wrongLane = new SimRunner(WaveDef.Wave6(), new Lane(Terrain.Defile, 30, new[] { 6, 10, 13, 17, 20 }),
                                          GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            Assert.NotNull(wrongLane.Unrecordable);
            Assert.Throws<WaveCompositionException>(() => wrongLane.SerializeRecord());

            // And the authored pair records normally.
            Assert.Null(new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                      GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed).Unrecordable);
        }

        [Fact]
        public void EveryDoorIntoTheTickLoopValidatesTheDeployment()
        {
            // SimRunner's constructor and Replay.Validate were the two doors
            // someone remembered. `new SimState(...)` plus direct Phases calls
            // is a third, and thirty-one call sites in this suite already take
            // it - so the shared rules had to move to where the state is BUILT.
            var bad = GoldenTests.DeploymentWithoutChill();
            bad[0].Pocket = 178956971;      // wraps into Lane's distance table

            Assert.Throws<WaveCompositionException>(
                () => new SimState(WaveDef.Wave6(), Lane.Defile(), bad));
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
            // worse than no guard: it is a claim. So the predicate is pointed
            // at things that ARE violations and required to say so.
            //
            // Span and List are the fixtures that matter. The previous
            // predicate recognised only T[] and SimState, so `public Span<int>
            // RaiderHp => _s.RaiderHp;` - ONE KEYWORD from the real property,
            // and the exact regression this exists to prevent - sailed through
            // at depth zero, and no fixture could notice because all three were
            // built from the two shapes the predicate already knew.
            Assert.NotEmpty(Leaks(typeof(LeaksAnArray)));
            Assert.NotEmpty(Leaks(typeof(LeaksSimState)));
            Assert.NotEmpty(Leaks(typeof(LeaksASpan)));
            Assert.NotEmpty(Leaks(typeof(LeaksAList)));
            Assert.NotEmpty(Leaks(typeof(LeaksOneHopAway)));
            Assert.NotEmpty(Leaks(typeof(LeaksAFieldOneHopAway)));
        }

        [Fact]
        public void EveryExemptedTypeIsActuallyImmutable()
        {
            // The cost of an allow-list is that an exemption is a promise. This
            // is the part that checks it: nothing Lane or Outcome exposes as a
            // property or a field may itself be writable-through.
            foreach (var t in Exempt)
            {
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.True(IsSealedSurface(p.PropertyType),
                        t.Name + "." + p.Name + " is exempted but hands out " + p.PropertyType.Name);
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    Assert.True(IsSealedSurface(f.FieldType),
                        t.Name + "." + f.Name + " is exempted but hands out " + f.FieldType.Name);
            }
        }

        private sealed class LeaksAnArray { public int[] Table => null; }
        private sealed class LeaksSimState { public SimState World => null; }
        private sealed class LeaksASpan { public Span<int> Live => default; }
        private sealed class LeaksAList { public List<int> Items => null; }
        private sealed class LeaksOneHopAway { public LeaksAnArray Inner => null; }
        private sealed class HasAPublicArrayField { public int[] Table = null; }
        private sealed class LeaksAFieldOneHopAway { public HasAPublicArrayField Inner => null; }

        /// The only engine types a SimRunner property may hand out whole.
        ///
        /// Named, not inferred, and each one is CHECKED below rather than
        /// trusted - an exemption that is never verified is how a deny-list
        /// rots. Both are immutable surfaces: Lane exposes ints, a Terrain and
        /// a ReadOnlySpan; Outcome exposes a Result, ints, a ulong and a
        /// ReadOnlySpan.
        private static readonly Type[] Exempt = { typeof(Lane), typeof(Outcome) };

        /// Public properties that hand out anything a caller could write
        /// through.
        ///
        /// An ALLOW-list, which is the axis that needed hardening. The previous
        /// predicate denied "T[] or SimState", which only catches the shapes
        /// someone thought of: `public Span&lt;int&gt; RaiderHp => _s.RaiderHp;` is
        /// ONE KEYWORD from the real property and is not an array, and
        /// List&lt;T&gt; is worse still - every property it exposes is an int, so
        /// even a structural walk of its surface calls it clean while Add and
        /// Clear sit on it. Shape cannot tell you a type is immutable. A name
        /// can, once someone has looked.
        ///
        /// So: a primitive, an enum, a string, a ReadOnlySpan, or a type on the
        /// Exempt list. Anything else is a finding until someone adds it there
        /// and says why.
        ///
        /// Properties only, deliberately. SerializeRecord() returns a freshly
        /// encoded byte[] and ReadRecord() returns a Replay built by Copy();
        /// both are copies whose whole purpose is to be handed out and written
        /// to, and both have their own tests. A property is a SURFACE - it
        /// reads as "the runner's X" - which is what makes the same shape a
        /// mistake there and not in a method named Read or Serialize.
        private static List<string> Leaks(Type t)
        {
            var found = new List<string>();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (IsSealedSurface(p.PropertyType)) continue;
                if (Array.IndexOf(Exempt, p.PropertyType) >= 0) continue;
                found.Add(t.Name + "." + p.Name + " returns " + p.PropertyType.Name +
                          ", which is not provably read-only. Use ReadOnlySpan<T>, or add the " +
                          "type to SimRunnerTests.Exempt with a reason.");
            }
            return found;
        }

        /// A type nothing can be written through: a value, a string, or a
        /// ReadOnlySpan.
        private static bool IsSealedSurface(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) ||
            (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>));
    }
}
