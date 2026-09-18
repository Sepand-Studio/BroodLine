using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The Splice Chamber bound to `SpliceScreenModel`
    /// (`client/Assets/UI/SpliceScreen.cs`). Every sentence on this view -
    /// the destruction notice, the CTA, the coverage warning, the mutation
    /// line - is the model's own; this view rewrites none of it.
    ///
    /// `onSplice` fires once, when the CTA is tapped. Showing
    /// `m.Dialogs` (the standard confirmation and any Founder interrupts,
    /// in order) before actually committing is the caller's job, not this
    /// view's - `Bind`'s own signature carries a single callback and no
    /// per-dialog confirm/cancel handles, so this view cannot orchestrate a
    /// sequence it has no hooks into. `ConfirmDialog` (Task 14) already
    /// renders that sequence element-by-element; wiring it in front of this
    /// callback belongs to whatever screen composes the two.
    ///
    /// ===================================================================
    /// THE PARENTS ARE STILL `CreatureCard`s, THOUGH THE PHASE 8 PLAN'S
    /// TASK 10 STEP 2 ASKS FOR "parent pickers become `OptionRow`s". Three
    /// reasons, and the first alone settles it:
    ///
    ///  1. THERE ARE NO PICKERS ON THIS SCREEN. `Bind` is handed
    ///     `m.ParentA` and `m.ParentB` already chosen, and neither it nor
    ///     `SpliceScreenModel` carries a per-parent callback - `FtueDirector`
    ///     says so in as many words at its lock-out comment ("there is no
    ///     lock picker on `SpliceChamberView`"). An `OptionRow` whose whole
    ///     purpose is to report a tap would report it to nobody.
    ///  2. IT WOULD DROP THE TRAIT PIPS. A row is a title and a detail line;
    ///     the pips are the only thing on this screen that says WHAT is about
    ///     to be destroyed, on the one screen `splice_confirm_spec` exists to
    ///     make the cost legible on.
    ///  3. Three `ScreenBindingTests` read the parents as `CreatureCard`s
    ///     named after their creature id, carrying `LockedOutUssClassName` -
    ///     which is section 6's "the named Founder is visibly locked out",
    ///     asserted from both directions.
    ///
    /// What DID change is that the pair now sits inside a `SectionCard`, so
    /// the two tiles read as one thing being paired rather than as two
    /// unrelated cards at the top of a screen.
    /// ===================================================================
    ///
    /// THE DESTRUCTION NOTICE MOVED TO THE BOTTOM OF THE CONTENT REGION, and
    /// that is the spec rather than a layout preference. splice_confirm_spec
    /// section 3: it "sits directly above the CTA. Not a tooltip, not a
    /// footnote." Before this task the forecast table sat between the two.
    [UxmlElement]
    public partial class SpliceChamberView : VisualElement
    {
        public const string UssClassName = "splice-chamber-view";
        public const string ForecastRowUssClassName = "forecast-row";
        public const string LockedOutUssClassName = "locked-out";

        readonly VisualElement _parents;
        readonly Label _destructionNotice;
        readonly Label _coverageWarning;
        readonly Label _mutation;
        readonly VisualElement _forecast;
        readonly Button _cta;

        Action _onSplice;

        public SpliceChamberView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("SpliceChamberView");
            tree.CloneTree(this);

            _parents = this.Q<VisualElement>("parents");
            _destructionNotice = this.Q<Label>("destruction-notice");
            _coverageWarning = this.Q<Label>("coverage-warning");
            _mutation = this.Q<Label>("mutation");
            _forecast = this.Q<VisualElement>("forecast");
            _cta = this.Q<Button>("cta");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: false. The handoff's navigation model points the
            // `Splice` tab straight at this screen, so it is a hub with a
            // bottom nav and no back chevron - and it is the screen its push
            // table reaches Creature Roster and Splice Reveal FROM.
            var scaffold = new ScreenScaffold(SpliceScreen.Title);

            var parents = new SectionCard();
            parents.Body.Add(_parents);

            var forecast = new SectionCard(SpliceScreen.ForecastHeading);
            forecast.Body.Add(_forecast);

            scaffold.Content.Add(parents);
            scaffold.Content.Add(forecast);
            scaffold.Content.Add(_mutation);
            scaffold.Content.Add(_coverageWarning);
            // LAST, so it is directly above the CTA - section 3's own
            // requirement. See the class comment.
            scaffold.Content.Add(_destructionNotice);
            scaffold.CtaRow.Add(_cta);
            Add(scaffold);

            // Registered once against a mutable field - see DeployView's
            // constructor for why (re-binding must not stack handlers).
            _cta.clicked += () => _onSplice?.Invoke();
        }

        /// `lockedOut` names parents this screen must refuse to submit again
        /// - splice_confirm_spec 6's "the named Founder is visibly locked
        /// out". `Broodline.UI` does not decide who belongs in that set; it
        /// only renders the one it is handed.
        public void Bind(SpliceScreenModel m, ISet<Guid> lockedOut, Action onSplice)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));
            lockedOut = lockedOut ?? new HashSet<Guid>();

            _parents.Clear();
            _parents.Add(CardFor(m.ParentA, lockedOut));
            _parents.Add(CardFor(m.ParentB, lockedOut));

            _destructionNotice.text = m.DestructionNotice ?? string.Empty;
            _cta.text = m.CtaLabel ?? string.Empty;
            // SpliceScreen.MutationLine is the model's own sentence for the
            // mutation/Aberrant pair (section 2: "stated as two numbers").
            //
            // AND IT COLLAPSES, for the same reason the coverage warning
            // below does: `MutationLine` returns empty for a null forecast -
            // a state `Bind` itself treats as reachable two blocks down, where
            // it renders `ForecastEmptyMessage` - and `_mutation` sits in a
            // `--violet-tint` panel with 12px of padding, so an empty one drew
            // a blank violet strip directly under a card saying the server
            // named no outcomes. The TEXT still round-trips to string.Empty,
            // which is what `ScreenBindingTests` reads.
            var mutation = SpliceScreen.MutationLine(m.Forecast);
            _mutation.text = mutation;
            _mutation.style.display = string.IsNullOrEmpty(mutation)
                ? DisplayStyle.None : DisplayStyle.Flex;

            // THE COVERAGE WARNING COLLAPSES WHEN THE SERVER NAMED NOTHING.
            // `CoverageWarningFor` returns empty for an empty `coverageLost`,
            // which is the ordinary case - and the amber panel it sits in has
            // a fill and 12px of padding, so an empty one drew a blank amber
            // strip under the forecast on a screen where nothing is being
            // lost. It is in every capture of this screen before Phase 8
            // Task 10. Same rule as `DeployView.Blocker`,
            // `FounderNamingView.Blocker` and `ScreenScaffold.FooterNote`;
            // the TEXT still round-trips to string.Empty, which is what
            // `ScreenBindingTests` reads.
            _coverageWarning.text = m.CoverageWarning ?? string.Empty;
            _coverageWarning.style.display = string.IsNullOrEmpty(m.CoverageWarning)
                ? DisplayStyle.None : DisplayStyle.Flex;

            _forecast.Clear();
            var outcomes = m.Forecast == null ? null : m.Forecast.Combat2;
            if (outcomes == null || outcomes.Count == 0)
            {
                // A forecast card with an empty table is section 2's one
                // non-negotiable rendering as a screen that failed to load.
                _forecast.Add(new EmptyState(SpliceScreen.ForecastEmptyMessage, "warning"));
            }
            else
            {
                foreach (var outcome in outcomes)
                {
                    _forecast.Add(ForecastCellFor(outcome));
                }
            }

            _onSplice = onSplice;
        }

        static CreatureCard CardFor(CreatureDto creature, ISet<Guid> lockedOut)
        {
            var card = new CreatureCard { name = creature.CreatureId.ToString() };
            // No counters map reaches this Bind - see DeployView/RosterView.
            card.Bind(creature, null);

            var locked = lockedOut.Contains(creature.CreatureId);
            card.SetEnabled(!locked);
            // Neither the name property nor the lockout class lives on
            // CreatureCard itself - this view owns both, per the brief.
            card.EnableInClassList(LockedOutUssClassName, locked);
            return card;
        }

        /// One forecast outcome as a `StatCell`.
        ///
        /// A `StatCell` RATHER THAN THIS SCREEN'S OLD TWO-LABEL ROW, AND THE
        /// DIFFERENCE IS `t-num`. bible 10.6 keeps two requirements - tabular
        /// figures and an 11px floor on any number a decision depends on -
        /// and the cell's value Label carries the class both are enforced
        /// through (`TypographyTests` pins the face, `verify-uss-tokens.sh`
        /// check 2 pins the floor). The old `.forecast-row` set a font-size
        /// on the ROW and no `t-num` anywhere, so these percentages - which
        /// are the entire reason section 2 requires the forecast before the
        /// charge - were the one set of decision-bearing numbers in the app
        /// rendered in proportional figures.
        ///
        /// The class is kept on the cell because `ScreenBindingTests` counts
        /// outcomes by it, and the count is the assertion that matters: a
        /// view rendering a single static row would pass a "not empty" check
        /// and fail that one.
        static VisualElement ForecastCellFor(CombatOutcomeDto outcome)
        {
            // The server's own probability, formatted by SpliceScreen.Percent
            // - the same helper MutationLine uses - not re-derived here.
            // (A view-local ToString("P1") looked equivalent but is not:
            // Percent gives "55%", InvariantCulture's "P1" gives "55.0 %".)
            var cell = new StatCell(
                CreatureLabel.TraitWithTier(outcome.Trait, outcome.Tier),
                SpliceScreen.Percent(outcome.P));
            cell.AddToClassList(ForecastRowUssClassName);
            return cell;
        }
    }
}
