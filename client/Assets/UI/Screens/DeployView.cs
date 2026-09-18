using System;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The deployment screen bound to `DeployScreenModel`
    /// (`client/Assets/UI/DeployScreen.cs`).
    ///
    /// `DeployScreen.Floor` and `.Cap` (mirrored from
    /// services/api/src/wave/issuance.ts) are the model's rule, not this
    /// view's. This view reads `CanDeploy`/`Blocker` only - it never counts
    /// `m.Slots` to decide whether Start should be enabled, because that
    /// would be a second copy of a rule the model already owns and could
    /// disagree with it (a duplicate id or a committed creature makes
    /// `CanDeploy` false at a count the floor/cap alone would allow).
    ///
    /// A SLOT IS A ROW AND NOT A CARD AS OF PHASE 8 TASK 10, AND THE REASON
    /// IS THAT THE OLD GRID WITHHELD THE ONE FACT THE SCREEN IS ABOUT.
    /// `DeploymentSlot.Pocket` is protocol-load-bearing - "Index ==
    /// deployment order", and `deploymentMatches` compares the replay's
    /// deployment against the stored one IN ORDER, so two deployments that
    /// agree as multisets and differ in order are different deployments - and
    /// the previous layout drew three identical `CreatureCard`s with the
    /// pocket printed nowhere at all. An `OptionRow` states it: the creature
    /// in the title, the pocket in the detail. What is lost is the trait
    /// pips, which is a real cost and the right trade here: the splice
    /// chamber is where a player reads traits, and this screen is where they
    /// read ORDER.
    [UxmlElement]
    public partial class DeployView : VisualElement
    {
        public const string UssClassName = "deploy-view";
        public const string SlotUssClassName = "deploy-view__slot";

        readonly ScreenScaffold _scaffold;
        readonly VisualElement _stats;
        readonly VisualElement _slots;
        readonly Label _blocker;
        readonly Button _start;

        Action _onStart;

        public DeployView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("DeployView");
            tree.CloneTree(this);

            _stats = this.Q<VisualElement>("stats");
            _slots = this.Q<VisualElement>("slots");
            _blocker = this.Q<Label>("blocker");
            _start = this.Q<Button>("start");

            // THE FRAME, COMPOSED AND NOT INHERITED - ScreenScaffold's class
            // comment has the reason, and ScaffoldTests' sweep is what
            // depends on it.
            //
            // pushed: true. The handoff has no deploy screen of its own - it
            // folds placement into "Wave Defense" as `phase: 'placing'` - and
            // its push table reaches Wave Defense from the Gene Ark, so the
            // screen Broodline split out of it inherits that. The CHEVRON
            // follows `Bind`'s `onBack`, not this flag; see below.
            _scaffold = new ScreenScaffold(DeployScreen.Title, pushed: true);

            // The UXML authors this screen's furniture as children of this
            // element and the scaffold's slots take them over here - a
            // re-parent rather than a second tree, so each element is
            // authored in exactly one place.
            //
            // THE BLOCKER IS THE LAST THING IN CONTENT, directly above the
            // CTA row, because it is the sentence explaining why the button
            // under it is off.
            var card = new SectionCard();
            card.Body.Add(_stats);
            _scaffold.Content.Add(card);
            _scaffold.Content.Add(_slots);
            _scaffold.Content.Add(_blocker);
            _scaffold.CtaRow.Add(_start);
            Add(_scaffold);

            // Registered once, in the constructor, against a field the next
            // Bind can overwrite - so re-binding this same instance (a
            // recycled screen) cannot stack a second `onStart` behind the
            // first the way `clicked += onStart` inside Bind would.
            _start.clicked += () => _onStart?.Invoke();
        }

        /// `onBack` is what the chevron does, and null means there is no
        /// chevron - `RosterView.Bind` has the full reasoning, which is the
        /// same here: `Broodline.UI` cannot name `ScreenHost.Pop`, and today
        /// `FtueDirector` reaches this screen through `ScreenFlow.ShowAsync`
        /// (`ScreenHost.Show`, back stack cleared) rather than a push, so
        /// there is nowhere for a chevron to go back to.
        public void Bind(DeployScreenModel m, Action onStart, Action onBack = null)
        {
            if (m == null) throw new ArgumentNullException(nameof(m));

            _scaffold.OnBack = onBack;

            // Both numbers are the model's. The count is `Slots.Count` read
            // back, not a tally this view keeps; the cap is `DeployScreen.Cap`
            // written by `DeployedStatValue`, which is the only place the two
            // are put in one string.
            _stats.Clear();
            _stats.Add(new StatCell(
                DeployScreen.WaveStatLabel, DeployScreen.WaveStatValue(m.WaveId)));
            _stats.Add(new StatCell(
                DeployScreen.DeployedStatLabel, DeployScreen.DeployedStatValue(m.Slots.Count)));

            _slots.Clear();
            if (m.Slots.Count == 0)
            {
                // Says the list is empty; the blocker below says what to do
                // about it. DeployScreen.EmptyMessage's comment has why those
                // are two sentences and not one.
                _slots.Add(new EmptyState(DeployScreen.EmptyMessage, "ark"));
            }
            foreach (var slot in m.Slots)
            {
                // No handler: a slot in an assembled deployment is not a
                // picker. Nothing in `Bind`'s signature carries a per-slot
                // callback, so a row that reported a tap would report it to
                // no one - and `OptionRow` registers no ClickEvent at all for
                // a null `onSelect`, so the row is inert rather than
                // silently swallowing the tap.
                var row = new OptionRow(
                    CreatureLabel.WithGeneration(slot.Creature),
                    DeployScreen.PocketLabel(slot.Pocket),
                    null)
                {
                    // The same handle `RosterView` and `SpliceChamberView`
                    // give a creature's element, so a caller reaching one slot
                    // does not have to count children.
                    name = slot.Creature.CreatureId.ToString(),
                };
                row.AddToClassList(SlotUssClassName);
                _slots.Add(row);
            }

            // The model's own sentence, verbatim - empty when `CanDeploy`.
            Blocker(m.Blocker);

            // The model's own label too - DeployScreen.Cta, not a literal
            // this view authors itself.
            _start.text = m.CtaLabel ?? string.Empty;
            _start.SetEnabled(m.CanDeploy);
            _onStart = onStart;
        }

        /// The refusal banner, shown only when it says something.
        ///
        /// THE DISPLAY TOGGLE IS NOT TIDYING, and this screen is where it was
        /// most visible: `.deploy-view__blocker` carries a --coral-tint fill
        /// and --space-3 of padding, so a LEGAL deployment - the ordinary
        /// case, the one a player sees every wave - drew a blank coral strip
        /// between the roster grid and the Start button. It is in every
        /// capture of this screen before Phase 8 Task 10.
        /// `FounderNamingView.Blocker` is the same rule on the same kind of
        /// row, and `ScreenScaffold.FooterNote` is where it started. The TEXT
        /// still round-trips to string.Empty, which is what
        /// `ScreenBindingTests` reads.
        void Blocker(string message)
        {
            _blocker.text = message ?? string.Empty;
            _blocker.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
