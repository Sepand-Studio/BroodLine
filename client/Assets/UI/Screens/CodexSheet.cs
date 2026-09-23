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
    ///
    /// AND AS OF PHASE 9 TASK 19 IT IS ACTUALLY A BOTTOM SHEET. The sentence
    /// above quoted `client_architecture` section 9 from the day this file
    /// was written; what shipped was a column flush with the TOP of the frame,
    /// because `.sheet-layer` sets no `justify-content` and flex defaults to
    /// `flex-start`. The root positions now and `#surface` carries the fill,
    /// the top-only radius and the padding - `AbandonedWaveSheet`'s split,
    /// element for element. CodexSheet.uss's header has the five specs and
    /// the measurement.
    [UxmlElement]
    public partial class CodexSheet : VisualElement
    {
        public const string UssClassName = "codex-sheet";
        public const string EntryUssClassName = "codex-entry";

        /// The two-sided heading row and the chip host at its right edge.
        /// Named here rather than typed as literals in the sheet, so the
        /// coupling between the C# that builds the row and the USS that
        /// lays it out is visible from both ends -
        /// `SectionCard.ElevationUssClassName`'s convention.
        public const string EntryHeaderUssClassName = "codex-entry__header";
        public const string EntryHeadingUssClassName = "codex-entry__heading";
        public const string EntryTraitsUssClassName = "codex-entry__traits";

        /// The element the fill, the radius and the padding live on - one
        /// level in from the root, which positions and does nothing else.
        /// `AbandonedWaveSheet`'s split, element for element; CodexSheet.uss's
        /// header has the whole account and the five specs that call this a
        /// bottom sheet.
        public const string SurfaceUssClassName = "codex-sheet__surface";

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
            // NO `SectionCard(heading)`, AND `SpliceChamberView` SET THIS
            // PRECEDENT. That constructor draws one 19px Baloo line and
            // nothing may sit beside it; the handoff's card header is a
            // TWO-SIDED ROW, and the chamber's parent tiles and forecast card
            // are all built with no heading and carry their own. So is this,
            // for the same reason and in the same shape: the lookup key on
            // the left, the specimen chip at the right edge - which is where
            // a roster card and a parent tile both put their `GenChip`.
            var entry = new SectionCard { name = trait.Id };
            entry.AddToClassList(EntryUssClassName);

            // `.t-section` IS THE SAME 19px BALOO `SectionCard`'s OWN HEADING
            // WEARS, so moving the line into the row changes the type not at
            // all - only what is allowed to sit next to it.
            var heading = new Label(trait.Id) { name = "heading" };
            heading.AddToClassList(EntryHeadingUssClassName);
            heading.AddToClassList("t-section");

            // THE SPECIMEN. It is the one thing on this sheet carrying
            // colour, and the colour is the whole reason it is here: a player
            // learns "teal means Vetch" on the roster and on the parent
            // tiles, and the codex is where they come to look a trait up.
            // bible 10.5 makes recognition this screen's job; the chip is
            // what the player is recognising.
            //
            // BESIDE THE HEADING AS OF FIX ROUND 1, NOT UNDER `found-on`.
            // `TraitCodexScreen.FoundOn` is literally "Found on " + species,
            // and a chip's payload IS the species tint - so stacked directly
            // under that sentence the chip repeated it on both channels at
            // once. The word still repeats the heading's, which is inherent
            // to showing a specimen; what is fixed is the tint repeating the
            // line above it. CodexSheet.uss's `.codex-entry__header` note has
            // the full account.
            var traits = new VisualElement { name = "traits" };
            traits.AddToClassList(EntryTraitsUssClassName);
            traits.Add(ChipFor(trait));

            var header = new VisualElement { name = "header" };
            header.AddToClassList(EntryHeaderUssClassName);
            header.Add(heading);
            header.Add(traits);
            entry.Body.Add(header);

            entry.Body.Add(new Label { name = "counters", text = TraitCodexScreen.Counters(trait) });

            var foundOn = new Label { name = "found-on", text = TraitCodexScreen.FoundOn(trait) };
            foundOn.AddToClassList("t-secondary");
            entry.Body.Add(foundOn);

            return entry;
        }

        /// The trait as it is drawn on a creature - `TraitChip`, tinted by
        /// the species the bundle names.
        ///
        /// IT CARRIES NO TIER, AND IT MUST NOT CLAIM THE ONE THING A NULL
        /// TIER MEANS. `TraitChip.cs` keeps data_model 2's contract - "a null
        /// tier renders as the bare trait name plus the `aberrant` marker" -
        /// and `EnableInClassList(AberrantUssClassName, tier == null)` is
        /// where it sets it. That contract is about a CREATURE's coverage of
        /// a trait. `config.traits` has no tier column at all: this table is
        /// about what a trait IS, so there is no coverage here to be absent.
        /// Left alone, every chip on this sheet would wear the Aberrant
        /// outline, which is exactly the defect `EntryFor`'s own comment
        /// refuses `TraitPip` for, one component over and for the identical
        /// reason.
        ///
        /// SO THE MARKER IS REMOVED AT THE ONE CALL SITE THAT HAS NO
        /// CREATURE, rather than by giving the component a third parameter -
        /// no component's public shape moves this task, and a chip that could
        /// be told to lie about its tier would be a worse component than one
        /// that cannot. `AberrantUssClassName` is public precisely so a call
        /// site can name it; this is greppable from both ends, and the test
        /// `CodexSheet_MarksEachTraitWithItsSpeciesTintedChip_AndClaimsNoAberrant`
        /// fails if either half is dropped.
        ///
        /// (This is the one place the task brief and the code disagreed. The
        /// brief says entries gain "a TraitChip"; the code says that chip
        /// would mark every trait in the codex Aberrant. Both are honoured:
        /// the chip, minus the claim.)
        static TraitChip ChipFor(TraitSummary trait)
        {
            var chip = new TraitChip(trait.Id, tier: null, species: trait.Species)
            {
                name = "chip",
            };
            chip.RemoveFromClassList(TraitChip.AberrantUssClassName);
            return chip;
        }
    }
}
