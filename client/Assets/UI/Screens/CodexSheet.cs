using System;
using System.Collections.Generic;
using Broodline.Model;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// The Trait Codex index - `screen_inventory_v2` 4 calls it required:
    /// "Without it the counter system is learnable only by losing."
    ///
    /// A SHEET, NOT A SCREEN, and the difference is `client_architecture`
    /// section 9's: "bottom sheets are an overlay layer, not screens ... it
    /// overlays, it dismisses, it does not push." So this goes to
    /// `ScreenHost.ShowSheet` and carries a dismiss of its own rather than a
    /// back chevron. `screen_inventory_v2` 13's open question - tab entry or
    /// contextual - is answered "both, leaning contextual", and the sheet is
    /// the contextual half: it opens from any trait pip.
    ///
    /// SO IT TAKES NO `ScreenScaffold`, AND THAT IS NOT AN OVERSIGHT. This
    /// type IS in `Broodline.UI.Screens` - checked, not assumed - so
    /// `ScaffoldTests.EveryScreenComposesTheScaffold` does reach it, and that
    /// sweep exempts it BY NAME for the reason above. A scaffold here would
    /// give a sheet a screen's header and a back affordance it must not have,
    /// and moving the type to another namespace to dodge the sweep would
    /// trade a named exemption for a silent one. It takes the COMPONENTS -
    /// the cards and the empty state - because those are about content and
    /// not about navigation.
    ///
    /// It renders the BUNDLE's `config.traits`, verbatim. `Broodline.UI`
    /// references no engine assembly, so what a trait counters here is what
    /// the content bundle authored, not what `Stats.CounterFor` derived.
    [UxmlElement]
    public partial class CodexSheet : VisualElement
    {
        public const string UssClassName = "codex-sheet";
        public const string EntryUssClassName = "codex-entry";

        readonly Label _title;
        readonly VisualElement _entries;
        readonly Button _dismiss;

        Action _onDismiss;

        public CodexSheet()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CodexSheet");
            tree.CloneTree(this);

            _title = this.Q<Label>("title");
            _entries = this.Q<VisualElement>("entries");
            _dismiss = this.Q<Button>("dismiss");

            _dismiss.clicked += () => _onDismiss?.Invoke();
        }

        public void Bind(IReadOnlyList<TraitSummary> traits, Action onDismiss = null)
        {
            if (traits == null) throw new ArgumentNullException(nameof(traits));

            _title.text = TraitCodexScreen.Title;
            _dismiss.text = TraitCodexScreen.DismissLabel;
            _onDismiss = onDismiss;

            _entries.Clear();
            foreach (var trait in traits)
            {
                if (trait == null) continue;
                _entries.Add(EntryFor(trait));
            }

            // AFTER the loop and against the CHILD COUNT, not against
            // `traits.Count` before it: a list of nothing but nulls is
            // skipped entry by entry above and would otherwise reach here
            // looking populated. bible 10.5 makes recognition the counter
            // system's teaching surface, so an index that opens on blank
            // paper teaches that the sheet is broken.
            if (_entries.childCount == 0)
            {
                _entries.Add(new EmptyState(TraitCodexScreen.EmptyMessage, "tier"));
            }
        }

        static VisualElement EntryFor(TraitSummary trait)
        {
            // Named after the trait id, so a deep link from a trait pip can
            // scroll to its own entry without counting children.
            //
            // THE TRAIT ID IS THE CARD'S HEADING rather than a third label in
            // a row of three. The old entry put name, counter and provenance
            // on one line at --text-body, which at 430px left "Found on Vetch
            // Crawler." 14px from the sheet's right edge; a card states the
            // subject once, at the top, and the two facts under it.
            //
            // NOT A `TraitPip`, and that is a correctness decision rather
            // than a layout one. `TraitPip.Bind`'s contract is data_model 2's:
            // "a null coverage tier is an Aberrant", and it sets the
            // `aberrant` class for one. `config.traits` carries no tier -
            // this table is about what a trait IS, not about any one
            // creature's coverage of it - so binding a pip here would mark
            // every trait in the codex Aberrant. (The phase 8 plan's step 3
            // for this screen says "TraitPip row unchanged"; there has never
            // been one on this sheet, for that reason.)
            var entry = new SectionCard(trait.Id) { name = trait.Id };
            entry.AddToClassList(EntryUssClassName);

            entry.Body.Add(new Label { name = "counters", text = TraitCodexScreen.Counters(trait) });

            var foundOn = new Label { name = "found-on", text = TraitCodexScreen.FoundOn(trait) };
            foundOn.AddToClassList("t-secondary");
            entry.Body.Add(foundOn);

            return entry;
        }
    }
}
