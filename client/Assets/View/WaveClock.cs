using System;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    /// The seam between a fixed-timestep simulation and a variable frame rate.
    ///
    /// client_architecture section 2: View owns an accumulator, calls Step as
    /// many times as the elapsed time allows, and interpolates between the last
    /// two snapshots for display. The engine never sees a variable delta and
    /// never learns that rendering exists.
    ///
    /// The doubles here are deliberate and are not a determinism hazard. They
    /// never reach the simulation: the only thing that crosses the boundary is
    /// the DECISION to call Step, and Step takes no arguments. That is the
    /// entire point of a fixed timestep, and it is why the engine's no-float
    /// rule binds engine/ and not this file.
    public sealed class WaveClock
    {
        /// A dropped frame must never drop a tick, so a stall is paid off by
        /// stepping more times. Without a ceiling that spirals: a frame that
        /// took long enough schedules more work than the next frame has time
        /// for, forever. With one, a device that cannot keep up runs the wave
        /// SLOW - every tick still executes, in order, at the same arithmetic.
        /// Slow is recoverable; skipped is a run the server will not reproduce,
        /// and the player loses the reward for a wave they won.
        ///
        /// Eight ticks is 267ms of simulation in one frame, well past any stall
        /// worth absorbing silently.
        public const int MaxCatchUpSteps = 8;

        private const double TickSeconds = 1.0 / Stats.TicksPerSecond;

        private double _accumulator;
        private int _pendingRally = -1;

        /// How far between the previous tick and the current one the render
        /// should interpolate: 0 at a tick boundary, approaching 1, and NEVER
        /// above 1.
        ///
        /// The clamp is the contract, not a nicety. The raw accumulator carries
        /// catch-up debt - deliberately, because paying a transient stall off is
        /// what "a dropped frame never drops a tick" means - and under sustained
        /// overload that debt grows: measured at 22 ticks after one frame and
        /// 352 after fifteen. Handing that out raw invites a consumer to
        /// EXTRAPOLATE past Current instead of interpolating toward it.
        /// WaveView clamped defensively; the clamp belongs here, where the
        /// documented range is promised.
        ///
        /// The debt itself is visible as StepsLastFrame, which is the honest
        /// place to read "this frame is behind".
        public double Alpha
        {
            get
            {
                double a = _accumulator / TickSeconds;
                return a > 1.0 ? 1.0 : a;
            }
        }

        public int StepsLastFrame { get; private set; }
        public bool Terminated { get; private set; }

        /// Records a Rally tap. It is NOT applied here - it is held until the
        /// next tick boundary, so the simulation records a tick index rather
        /// than a moment in wall-clock time. The input log the server
        /// re-simulates from must be exactly what the local run consumed.
        public void RequestRally(int creatureId) => _pendingRally = creatureId;

        public void Advance(SimRunner runner, double deltaSeconds, Action onTick)
        {
            StepsLastFrame = 0;
            if (Terminated) return;

            _accumulator += deltaSeconds;

            while (_accumulator >= TickSeconds && StepsLastFrame < MaxCatchUpSteps)
            {
                if (_pendingRally >= 0)
                {
                    runner.TryRally(_pendingRally);
                    _pendingRally = -1;
                }

                bool advanced = runner.Step();

                // onTick fires EVEN ON THE TERMINATING STEP. Step runs all eight
                // phases and then returns false, so the final tick's mutations -
                // the breach, the integrity drop, the raider's death - are in
                // SimState whether or not the loop continues. Returning before
                // this left WavePair holding the second-to-last tick forever:
                // the Courser froze visibly one tile short of the Ark, still
                // alive, while the HUD printed Loss. The breach frame is the one
                // frame wave 6 exists to show, and it was the one never drawn.
                StepsLastFrame++;
                onTick?.Invoke();

                if (!advanced)
                {
                    Terminated = true;

                    // Alpha 0 would make Render lerp back to Previous. Pin it at
                    // a whole tick so the final frame shows Current - the tick
                    // that just terminated - and stays there.
                    _accumulator = TickSeconds;
                    return;
                }

                _accumulator -= TickSeconds;
            }
        }
    }
}
