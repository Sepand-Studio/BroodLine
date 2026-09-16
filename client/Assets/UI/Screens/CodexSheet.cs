using System;
using System.Collections.Generic;
using Broodline.Model;
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
        }

        static VisualElement EntryFor(TraitSummary trait)
        {
            // Named after the trait id, so a deep link from a trait pip can
            // scroll to its own entry without counting children.
            var entry = new VisualElement { name = trait.Id };
            entry.AddToClassList(EntryUssClassName);

            // NOT A `TraitPip`, and that is a correctness decision rather
            // than a layout one. `TraitPip.Bind`'s contract is data_model 2's:
            // "a null coverage tier is an Aberrant", and it sets the
            // `aberrant` class for one. `config.traits` carries no tier -
            // this table is about what a trait IS, not about any one
            // creature's coverage of it - so binding a pip here would mark
            // every trait in the codex Aberrant.
            entry.Add(new Label { name = "trait", text = trait.Id });

            entry.Add(new Label { name = "counters", text = TraitCodexScreen.Counters(trait) });
            entry.Add(new Label { name = "found-on", text = TraitCodexScreen.FoundOn(trait) });

            return entry;
        }
    }
}
