using System;
using System.Collections.Generic;
using System.Globalization;
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
            _coverageWarning.text = m.CoverageWarning ?? string.Empty;
            _cta.text = m.CtaLabel ?? string.Empty;
            // SpliceScreen.MutationLine is the model's own sentence for the
            // mutation/Aberrant pair (section 2: "stated as two numbers").
            _mutation.text = SpliceScreen.MutationLine(m.Forecast);

            _forecast.Clear();
            if (m.Forecast != null)
            {
                foreach (var outcome in m.Forecast.Combat2)
                {
                    _forecast.Add(ForecastRowFor(outcome));
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

        static VisualElement ForecastRowFor(CombatOutcomeDto outcome)
        {
            var row = new VisualElement();
            row.AddToClassList(ForecastRowUssClassName);

            row.Add(new Label
            {
                name = "trait",
                text = CreatureLabel.TraitWithTier(outcome.Trait, outcome.Tier),
            });
            // The server's own probability, formatted with .NET's standard
            // percent specifier - not re-derived, just rendered
            // (client_architecture 9.1: "renders the server's numbers and
            // computes none").
            row.Add(new Label
            {
                name = "probability",
                text = outcome.P.ToString("P1", CultureInfo.InvariantCulture),
            });

            return row;
        }
    }
}
