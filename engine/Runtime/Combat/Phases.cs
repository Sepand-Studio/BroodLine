namespace Broodline.Sim.Combat
{
    /// The eight tick phases of combat_engine section 4, in normative order.
    ///
    /// "Each 33ms tick, in this order, and the order is normative - changing it
    /// changes outcomes." Movement precedes targeting so a creature never fires
    /// at a position a raider has already left; death resolves after attack so
    /// a raider that dies this tick has already moved and already been hit.
    /// Anything else produces same-tick cascades that cannot be reasoned about.
    public static partial class Phases
    {
        /// Phase 1 - Spawn. Introduce raiders whose spawn tick has arrived.
        ///
        /// The spawn table is ordered by tick ascending (WaveDef.Validate
        /// enforces it), so this walks forward from RaiderCount and stops at
        /// the first entry in the future. Idempotent within a tick.
        public static void Spawn(SimState s)
        {
            while (s.RaiderCount < s.Wave.Spawns.Length &&
                   s.Wave.Spawns[s.RaiderCount].Tick <= s.Tick)
            {
                int r = s.RaiderCount;
                s.RaiderType[r] = s.Wave.Spawns[r].Type;
                s.RaiderHp[r] = Stats.RaiderHp(s.RaiderType[r]);
                s.RaiderProgress[r] = Fix64.Zero;
                s.RaiderAlive[r] = true;
                s.RaiderChilled[r] = false;
                s.RaiderCount++;
            }
        }

        /// Phase 2 - State. Forced state changes.
        ///
        /// In the full engine this resolves Burrow surfacing, Delver reaching
        /// the Ark, Drift altitude, Chill expiry and Skittish repositioning.
        /// Phase 2's slice carries Chill only.
        public static void State(SimState s, int[] scratch)
        {
            Counters.ApplyChill(s, scratch);
        }

        /// Phase 3 - Movement. Advance every live raider by its current speed.
        public static void Movement(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                s.RaiderProgress[r] = s.RaiderProgress[r] + RaiderSpeed(s, r);
            }
        }

        /// Current speed in tiles per tick, after state effects.
        public static Fix64 RaiderSpeed(SimState s, int r)
        {
            int milli = s.RaiderChilled[r]
                ? Stats.ChilledMilliTilesPerSec
                : Stats.RaiderMilliTilesPerSec(s.RaiderType[r]);
            return Lane.SpeedPerTick(milli);
        }

        /// Phase 4 - Targeting. Retarget any creature whose target is dead, out
        /// of range, or whose Instinct preference now names a different valid
        /// target. Respects the 0.4s retarget lockout.
        ///
        /// combat_engine section 6: "The 0.4s retarget lockout is a lockout on
        /// ACQUIRING, not on firing." A creature whose target dies stops firing
        /// immediately and acquires 12 ticks later. That gap is what makes
        /// Splash a counter rather than a convenience, so dropping the target
        /// eagerly while gating acquisition is load-bearing, not incidental.
        public static void Targeting(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;

                int current = s.CreatureTarget[c];
                bool held = current >= 0 && Combat.Targeting.CanReach(s, c, current);

                if (!held && current >= 0)
                {
                    // Drop immediately, and start the lockout.
                    s.CreatureTarget[c] = -1;
                    s.CreatureAcquireAt[c] = s.Tick + Stats.RetargetLockoutTicks;
                    continue;
                }

                if (held)
                {
                    // Still valid, but the preference may have moved.
                    int preferred = Combat.Targeting.Select(s, c);
                    if (preferred != current && s.Tick >= s.CreatureAcquireAt[c])
                        s.CreatureTarget[c] = preferred;
                    continue;
                }

                if (s.Tick >= s.CreatureAcquireAt[c])
                    s.CreatureTarget[c] = Combat.Targeting.Select(s, c);
            }
        }
    }
}
