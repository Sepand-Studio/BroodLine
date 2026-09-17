using System.Collections.Generic;
using Broodline.Model;
using Broodline.Sim.Combat;

namespace Broodline.Game.Shell
{
    /// Turns a finished `SimRunner` into the plain `WaveReport` a screen can
    /// render.
    ///
    /// WHY THIS IS A SEPARATE CLASS FROM `WaveReport` ITSELF: the report is
    /// the thing `Broodline.UI` binds, so it lives in `Broodline.Model`, and
    /// `Broodline.Model` references only `Generated.Api` - it cannot name
    /// `SimRunner`. `Broodline.Game` is the only assembly that may read an
    /// engine `Outcome`, so the conversion lives here and the DATA lives
    /// there. A `WaveReport.From(SimRunner, ...)` static would have put the
    /// engine in `Broodline.Model`, and from there in front of every screen.
    ///
    /// SYNCHRONOUS, AND THAT IS NOT INCIDENTAL. `Outcome.Breaches` is a
    /// `ReadOnlySpan&lt;Breach&gt;` - a ref struct, which cannot cross an
    /// `await`, be captured by a lambda or be yielded from an iterator.
    /// `WaveHost.RunAsync` is async, so it calls this and never touches the
    /// span itself. The span is also why there is no LINQ below: the closure
    /// over `raider` is fine, but a query over the span would not compile,
    /// and a hand-written loop makes that a non-question.
    public static class WaveReportBuilder
    {
        public static WaveReport From(SimRunner runner, IReadOnlyList<TraitSummary> traits)
        {
            var outcome = runner.Outcome;

            // Sized from the count so the list never grows. BreachCount and
            // Breaches.Length are the same expression by construction -
            // Outcome derives one from the other precisely so the old
            // "iterate to BreachCount, never Length" hazard cannot return.
            var breaches = new List<BreachSummary>(outcome.BreachCount);
            foreach (var breach in outcome.Breaches)
            {
                var raider = breach.Type.ToString();
                breaches.Add(new BreachSummary
                {
                    RaiderType = raider,
                    Counter = CounterFor(traits, raider),
                    // Carried, never recomputed. `Diagnosis.Evaluate` ran at
                    // the moment of the breach against the board as it was;
                    // re-deriving here would judge a board that no longer
                    // exists - which is the bug combat_engine section 7
                    // records the diagnosis at the breach to prevent.
                    Access = breach.Access,
                    Coverage = breach.Coverage,
                    Placement = breach.Placement,
                });
            }

            return new WaveReport
            {
                Result = outcome.Result.ToString(),
                Ticks = outcome.Ticks,
                IntegrityRemaining = outcome.IntegrityRemaining,
                ReplayBytes = runner.SerializeRecord(),
                Breaches = breaches,
            };
        }

        /// The answering trait as the CONTENT BUNDLE names it -
        /// `/v1/sync`'s `config.traits[].counters`, the same table the Codex
        /// sheet reads - and deliberately NOT `Stats.CounterFor`.
        ///
        /// Two reasons, and neither is style. `Broodline.UI` would need the
        /// engine to resolve it otherwise, and the copy on the single most
        /// important teaching screen in the game would then be frozen into a
        /// client build: retuning which trait answers a raider is meant to be
        /// a bundle publish, not an App Review cycle (design section 4 makes
        /// exactly that argument for the gate thresholds).
        ///
        /// Empty when the bundle names no answer. `WaveDefeatScreen.Headline`
        /// drops the trait half rather than inventing one.
        /// EVERY answering trait, not the first one found. `Counters` is a
        /// plain string on `TraitSummary` and nothing in `/v1/sync`'s schema
        /// makes it unique, so a bundle may publish two traits that answer
        /// the same raider. Returning the first meant the defeat screen named
        /// one and silently dropped the other - and which one it named would
        /// change with the server's array order between publishes, on the one
        /// screen (bible 4.11) whose entire job is to name the trait that
        /// would have answered.
        static string CounterFor(IReadOnlyList<TraitSummary> traits, string raider)
        {
            if (traits == null) return string.Empty;

            string first = null;
            List<string> all = null;
            for (var i = 0; i < traits.Count; i++)
            {
                var trait = traits[i];
                if (trait == null || trait.Counters != raider) continue;

                if (first == null) { first = trait.Id; continue; }
                if (all == null) all = new List<string> { first };
                all.Add(trait.Id);
            }

            if (first == null) return string.Empty;
            // "Chill or Ward would have answered it." - reads as the headline
            // at `WaveScreens.WaveDefeatScreen` already builds it.
            return all == null ? first : string.Join(" or ", all);
        }
    }
}
