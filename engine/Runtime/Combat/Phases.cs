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
                // 0 rather than s.Tick: a raider may swing on the first tick it
                // has a defender in reach, which is how creatures work too -
                // CreatureNextAttackAt starts at 0 and fires immediately.
                s.RaiderTargetCreature[r] = -1;
                s.RaiderNextAttackAt[r] = 0;
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
            Skittish(s);
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
                    {
                        s.CreatureTarget[c] = preferred;
                        s.CreatureAcquireAt[c] = s.Tick + Stats.RetargetLockoutTicks;
                    }
                    continue;
                }

                if (s.Tick >= s.CreatureAcquireAt[c])
                {
                    int acquired = Combat.Targeting.Select(s, c);
                    s.CreatureTarget[c] = acquired;
                    if (acquired >= 0)
                        s.CreatureAcquireAt[c] = s.Tick + Stats.RetargetLockoutTicks;
                }
            }

            RaiderTargeting(s);
        }

        /// The raider half of phase 4.
        ///
        /// Taunt runs FIRST and the order is the rule: combat_numbers 4.2 says
        /// it FORCES the target, so a preference computed first and overridden
        /// afterwards would be the same answer by a longer route only until the
        /// day the default acquires a lockout. ApplyTaunt clears every slot to
        /// -1 and fills in the ones it holds; this fills in the rest.
        ///
        /// No retarget lockout here. combat_numbers section 5 gives the 0.4s
        /// delay to creatures - "Retarget delay is 0.4s for every creature" -
        /// and a lockout on this side would fight Taunt, which has to be free
        /// to move a Lash the instant its carrier dies or walks out of reach.
        /// Recomputed every tick, like Chill, for the same reason.
        private static void RaiderTargeting(SimState s)
        {
            Counters.ApplyTaunt(s);

            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (s.RaiderTargetCreature[r] >= 0) continue;   // held by Taunt
                s.RaiderTargetCreature[r] = Combat.Targeting.SelectDefender(s, r);
            }
        }

        /// Phase 5 - Attack. Resolve attacks whose interval has elapsed.
        ///
        /// Creatures first, then raiders. Within one phase the order is a
        /// choice, and this is the one section 4 already implies: a raider that
        /// dies this tick "has already moved and already been hit", and phase 6
        /// is what ends it - so a Lash taken to zero by the creature pass still
        /// swings in the raider pass, exactly as a creature taken to zero by
        /// the raider pass is only cleared in phase 6. Reversing it would let a
        /// defender kill an attacker before it ever answers, which is the
        /// same-tick cascade section 4 exists to forbid.
        public static void Attack(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureCanAct(c)) continue;
                if (s.Tick < s.CreatureNextAttackAt[c]) continue;

                int target = s.CreatureTarget[c];
                if (target < 0 || !s.RaiderAlive[target]) continue;

                s.RaiderHp[target] -= Attacks.Damage(s, c);
                s.CreatureNextAttackAt[c] = s.Tick + Attacks.IntervalTicks(s, c);
            }

            RaiderAttack(s);
        }

        /// The raider half of phase 5.
        ///
        /// Lash is the only raider in combat_numbers section 6 that attacks;
        /// every other type has interval 0 and is skipped here, which is why
        /// this is a no-op for wave 6 and for all 500 corpus scenarios. A zero
        /// interval means "does not attack" rather than "attacks every tick",
        /// and the guard is the first thing in the loop so it can never be read
        /// the other way.
        private static void RaiderAttack(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;

                int interval = Attacks.RaiderIntervalTicks(s, r);
                if (interval <= 0) continue;
                if (s.Tick < s.RaiderNextAttackAt[r]) continue;

                int target = s.RaiderTargetCreature[r];
                if (target < 0 || !s.CreatureAlive(target)) continue;

                s.CreatureHp[target] -= Attacks.DamageTaken(s, target, Attacks.RaiderDamage(s, r));
                s.RaiderNextAttackAt[r] = s.Tick + interval;
            }
        }

        /// Phase 6 - Death. Resolve deaths.
        ///
        /// In the full engine this is also where a Brood splits and where
        /// Cinder suppresses the split at birth. Neither is in this slice, so
        /// death here is only a clearing pass - but it stays its own phase
        /// because section 4 makes the ordering normative: a raider that dies
        /// this tick has already moved and has already been hit.
        public static void Death(SimState s)
        {
            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r] && s.RaiderHp[r] <= 0)
                    s.RaiderAlive[r] = false;

            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureHp[c] < 0)
                    s.CreatureHp[c] = 0;
        }

        /// Skittish - phase 2, State. A forced state change, not an attack
        /// modifier, which is why it lives here rather than in Attacks.
        ///
        /// combat_engine section 6: below 40% HP, reposition to the nearest
        /// free pocket away from the threat, 2s, cannot act. Deterministic by
        /// decision (whats_left section 2), which is what keeps the engine's
        /// RNG surface down to tie-breaks.
        public static void Skittish(SimState s)
        {
            for (int c = 0; c < s.CreatureCount; c++)
            {
                if (!s.CreatureAlive(c)) continue;
                if (s.CreatureInstinct[c] != Instinct.Skittish) continue;
                if (s.CreatureRepositioned[c]) continue;
                if (!Attacks.BelowFraction(s, c, 2, 5)) continue;   // < 40%

                int destination = NearestFreePocketAwayFromThreat(s, c);
                if (destination < 0) continue;

                s.CreaturePocket[c] = destination;
                s.CreatureRepositioned[c] = true;
                s.CreatureBusyUntil[c] = s.Tick + 2 * Stats.TicksPerSecond;
                s.CreatureTarget[c] = -1;
            }
        }

        /// "Away from the threat" is away from the raider nearest the Ark,
        /// which is the one the pocket most needs distance from. Among free
        /// pockets strictly farther from that raider than the current pocket,
        /// this picks the NEAREST one to the creature's current pocket - the
        /// shortest move that still increases distance from the threat -
        /// tie-breaking on lowest pocket index so the choice is total-ordered
        /// like every other comparator here.
        private static int NearestFreePocketAwayFromThreat(SimState s, int c)
        {
            int threat = -1;
            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (threat < 0 || s.RaiderProgress[r] > s.RaiderProgress[threat]) threat = r;
            }
            if (threat < 0) return -1;

            int threatTile = s.RaiderTile(threat);
            int currentPocket = s.CreaturePocket[c];
            int currentTile = s.Lane.PocketTiles[currentPocket];
            int here = s.Lane.DistSq(currentPocket, threatTile);

            int best = -1;
            int bestMoveDistSq = int.MaxValue;
            for (int p = 0; p < s.Lane.PocketCount; p++)
            {
                if (Occupied(s, p)) continue;
                if (s.Lane.DistSq(p, threatTile) <= here) continue;

                int moveDistSq = s.Lane.DistSq(p, currentTile);
                if (moveDistSq < bestMoveDistSq)
                {
                    bestMoveDistSq = moveDistSq;
                    best = p;
                }
            }
            return best;
        }

        private static bool Occupied(SimState s, int pocket)
        {
            for (int c = 0; c < s.CreatureCount; c++)
                if (s.CreatureAlive(c) && s.CreaturePocket[c] == pocket) return true;
            return false;
        }

        /// Phase 7 - Breach. Any raider at the Ark: deduct integrity, record
        /// the diagnosis, remove it. Returns true if anything breached.
        public static bool Breach(SimState s, Breach[] log, ref int logCount)
        {
            bool any = false;
            Fix64 ark = Fix64.FromInt(Stats.LaneTiles);

            for (int r = 0; r < s.RaiderCount; r++)
            {
                if (!s.RaiderAlive[r]) continue;
                if (s.RaiderProgress[r] < ark) continue;

                if (logCount < log.Length)
                {
                    var verdict = Diagnosis.Evaluate(
                        s,
                        s.RaiderType[r],
                        Diagnosis.SimultaneousCount(s, s.RaiderType[r]),
                        s.RaiderTile(r));

                    log[logCount] = new Breach
                    {
                        Tick = s.Tick,
                        Raider = r,
                        Type = s.RaiderType[r],
                        Lane = 0,
                        Access = verdict.Access,
                        Coverage = verdict.Coverage,
                        Placement = verdict.Placement
                    };
                    logCount++;
                }

                s.Integrity -= Stats.RaiderIntegrityCost(s.RaiderType[r]);
                s.RaiderAlive[r] = false;

                any = true;
            }
            return any;
        }

        /// Phase 8 - Resolve.
        ///
        /// combat_engine section 8: the wave is lost the tick integrity reaches
        /// zero or below, and the simulation STOPS there - raiders still on the
        /// board are not resolved and are not counted. Loss is therefore
        /// checked before win.
        public static Result Resolve(SimState s)
        {
            if (s.Integrity <= 0) return Result.Loss;

            for (int r = 0; r < s.RaiderCount; r++)
                if (s.RaiderAlive[r]) return Result.Running;

            // Pending spawns count as remaining. When a splitting raider type
            // (Brood) is added, its pending children must also count toward
            // the win condition per combat_engine section 8 - this is where
            // that future author will look.
            if (s.RaiderCount < s.Wave.Spawns.Length) return Result.Running;

            return Result.Win;
        }
    }
}
