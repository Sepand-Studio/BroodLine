using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Model
{
    /// One raider that reached the Ark, written as PLAIN DATA a screen can
    /// render without an engine.
    ///
    /// The three booleans are combat_engine section 7's diagnosis, evaluated
    /// in order with the first false as the verdict, and they are CARRIED
    /// here rather than recomputed: `Diagnosis.Evaluate` runs at the moment
    /// of the breach because by the time the wave ends the board it judged is
    /// gone. Anything that re-derives them later is judging a different
    /// board.
    ///
    /// `Counter` is NOT `Stats.CounterFor`. It is the answering trait as the
    /// CONTENT BUNDLE names it (`PlayerSnapshot.Traits[].Counters`, the same
    /// table the Codex sheet reads), so retuning which trait answers which
    /// raider is a bundle publish and the defeat copy follows it. Empty when
    /// the bundle names no answer - which is a content gap, not a crash.
    public sealed class BreachSummary
    {
        public string RaiderType;
        public string Counter;

        /// False when no live creature carried the answering trait at all.
        public bool Access;

        /// False when a carrier was present but its tier's capacity was below
        /// the number of that raider on the board at the breach.
        public bool Coverage;

        /// False when a carrier was present and sufficient but could never
        /// have reached the tile the raider broke through at.
        public bool Placement;
    }

    /// What one played wave produced, as the shell hands it to a screen.
    ///
    /// WHY THIS LIVES IN `Broodline.Model` AND NOT IN `Broodline.Game`:
    /// `Broodline.Game` references `Broodline.UI`, so `Broodline.UI` cannot
    /// reference `Broodline.Game` without a cycle Unity refuses outright.
    /// `Broodline.Model` is the shared data layer both already reference and
    /// is the only assembly that can hold a type BOTH of them name. The
    /// half that reads the engine's `Outcome` - `WaveReportBuilder` - stays
    /// in `Broodline.Game`, which is the actual constraint: nothing that
    /// touches `Broodline.Sim` moves closer to the views.
    public sealed class WaveReport
    {
        /// `Result` as text - "Win", "Loss", "Stalled". A string rather than
        /// the engine's enum for the same reason as everything else here:
        /// naming `Broodline.Sim.Combat.Result` in this assembly would put
        /// the engine in front of every screen that renders a wave.
        public string Result;

        public int Ticks;
        public int IntegrityRemaining;

        /// The replay record for this run, as the server's submit endpoint
        /// wants it. Bytes, never a parsed object - the client produces this
        /// and forwards it; it is not the client's to interpret.
        public byte[] ReplayBytes;

        /// Empty on a win. Never null - a screen that has to null-check
        /// before it can say "no breaches" is one branch away from claiming
        /// a breach it does not have.
        public IReadOnlyList<BreachSummary> Breaches;
    }

    /// Which of the three colours a bar is drawn in, as a FACT about the
    /// body rather than as a colour. `WaveHudView` maps these onto USS
    /// classes; nothing here knows what they look like.
    ///
    /// Breaching outranks chilled, which is the rule `WaveHud` carried in
    /// prose: "Chill is the thing wave 6 exists to teach ... a breach
    /// outranks it: that frame is the verdict."
    public enum BodyState { Normal = 0, Chilled = 1, Breaching = 2, Rallied = 3 }

    public enum BodyKind { Raider = 0, Creature = 1 }

    /// One health bar over one body, for one frame.
    ///
    /// The position is a WORLD position, not a screen or panel one. The
    /// conversion belongs to whoever owns the panel and the camera
    /// (`WaveHudView`, via `RuntimePanelUtils`), because it is the camera
    /// that knows which world axis maps to screen-up - the correction
    /// `WaveHud` records three failed attempts at.
    public struct BodyBar
    {
        public BodyKind Kind;
        public Vector3 World;
        public int Hp;
        public int MaxHp;
        public BodyState State;

        /// Ticks of Rally left on this creature, read from the engine rather
        /// than counted down here. Zero for a raider and for an un-rallied
        /// creature.
        public int RallyRemaining;
    }

    /// Which state a body's bar is in, as a pure function of the facts the
    /// snapshot carries.
    ///
    /// It is a function rather than two `if`s at the one call site because
    /// the PRECEDENCE is the interesting part and precedence written inline
    /// is precedence nobody tests. `WaveHud` carried the rule in prose -
    /// "Chill is the thing wave 6 exists to teach ... a breach outranks it:
    /// that frame is the verdict" - and nothing enforced it.
    public static class BodyBars
    {
        public static BodyState RaiderState(bool breaching, bool chilled)
        {
            if (breaching) return BodyState.Breaching;
            return chilled ? BodyState.Chilled : BodyState.Normal;
        }

        /// Rally is the player's ONLY input during a wave, so a rallied
        /// creature must look different from one that is not - `WaveRunner`
        /// names the failure: "a tap with no visible consequence is
        /// indistinguishable from a tap that was dropped."
        public static BodyState CreatureState(int rallyRemaining)
        {
            return rallyRemaining > 0 ? BodyState.Rallied : BodyState.Normal;
        }
    }

    /// Everything the live Wave Defense HUD draws, for one frame.
    ///
    /// Built fresh by the shell each frame and handed over through a
    /// `Func<HudSnapshot>` - so the view pulls when it is ready to draw
    /// rather than the simulation pushing on a tick boundary the renderer
    /// is not aligned to.
    ///
    /// `WaveHud`'s DrawDebug block (entity count, alpha, catch-up, backlog,
    /// frame ms) is deliberately ABSENT. Design section 4: the HUD is "the
    /// one piece of existing presentation that a tester sees", and those
    /// five numbers are what made it read as a debug tool.
    public sealed class HudSnapshot
    {
        /// The loss condition. combat_engine section 8 makes it a pool, not
        /// a life count.
        public int Integrity;

        public int Tick;

        /// One entry per VISIBLE body - dead raiders and dead creatures are
        /// already filtered out by the builder, so a view that draws every
        /// entry draws the right set.
        public IReadOnlyList<BodyBar> Bodies;
    }
}
