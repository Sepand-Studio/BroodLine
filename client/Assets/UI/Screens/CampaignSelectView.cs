using System;
using System.Collections.Generic;
using Broodline.UI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Screens
{
    /// Campaign Select - design section 5.2's second deliberately-added
    /// screen: "how a player reaches wave 6 after wave 2. Without it the
    /// first hour ends at beat 8 with no route to the designed loss."
    ///
    /// Every enabled/disabled decision is `CampaignSelectScreen`'s, which
    /// mirrors `issueWave`'s branch rather than reinventing it - the rule is
    /// "the smallest AUTHORED id past what has been cleared", not
    /// `cleared + 1`, because this bundle authors 1, 2, 6 and 7 and the
    /// arithmetic version locks the only wave a player who cleared 2 can
    /// actually play. This view renders the verdict; it never counts ids
    /// itself.
    ///
    /// A WAVE ROW IS AN `OptionRow` AS OF PHASE 8 TASK 9, and the row's two
    /// labels are that component's - `title` and `detail`, not this screen's
    /// old `label` and `state`. The element NAME is still
    /// `CampaignSelectScreen.RowName(id)`, because that is the handle the
    /// director and the tests reach a single wave by and it belongs to this
    /// screen rather than to the component.
    ///
    /// THE ROWS SIT INSIDE ONE `SectionCard` AS OF PHASE 9 TASK 14, AND THAT
    /// CLOSES A MEASURED DEFECT RATHER THAN MOVING FURNITURE.
    /// `phase8-visual-review.md`'s "one thing that is objectively out of
    /// spec" is this list: `OptionRow`'s --surface-sunk fill on --paper
    /// measures 1.0151:1 and its --hairline border 1.1131:1, both under WCAG
    /// 1.4.11's 3:1 for a non-text boundary, so a row's extent was carried by
    /// almost nothing. The review offered three fixes and deferred the choice
    /// to this phase; design section 4.1 took the third - "`OptionRow` ...
    /// is composed inside a `SectionCard` where the handoff composes it, so
    /// its fill sits on white. The WCAG note ... closes by following the
    /// design, not exceeding it." On white the fill measures 1.0726:1. That
    /// is better and is still not 3:1, which is the honest state of it: the
    /// handoff's own unselected option is a 1px #ece7f6 inset on #f7f5fb -
    /// the same numbers - and the decision taken was to follow it. What
    /// actually carries each row's state is the word in its detail line plus
    /// a mark at its right edge, which is bible 10.4 working as designed.
    [UxmlElement]
    public partial class CampaignSelectView : VisualElement
    {
        public const string UssClassName = "campaign-select-view";

        /// Kept, and still applied, though `.option-row` now draws the row:
        /// it is what `CampaignSelectView.uss` hangs the LOCKED treatment off,
        /// which is this screen's own rule rather than the component's.
        public const string RowUssClassName = "campaign-row";

        /// The green "Cleared" chip at a beaten wave's right edge. Named here
        /// rather than typed as a literal at the two places that need it -
        /// this file and the stylesheet - on `SectionCard`'s convention.
        public const string ClearedChipUssClassName = "campaign-select-view__cleared";
        public const string HeldUssClassName = "campaign-select-view__held";

        readonly VisualElement _waves;
        readonly ScreenScaffold _scaffold;

        public CampaignSelectView()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CampaignSelectView");
            tree.CloneTree(this);

            _waves = this.Q<VisualElement>("waves");

            // Composed, not inherited - ScreenScaffold's class comment has
            // the reason, and ScaffoldTests' sweep is what depends on it.
            // pushed: false - the campaign is a tab destination, reached by
            // ScreenHost.Show, so it carries no back chevron. The screen's
            // own `title` Label is gone: the scaffold's header IS the title,
            // and two elements named "title" in one tree is a Q() that
            // answers whichever it meets first.
            //
            // THE EYEBROW IS ALREADY UPPERCASE AND NOTHING HERE MAKES IT SO -
            // UI Toolkit has no `text-transform`, so the casing is baked into
            // the constant. `CampaignSelectScreen.Eyebrow` has the note.
            _scaffold = new ScreenScaffold(
                CampaignSelectScreen.Title, eyebrow: CampaignSelectScreen.Eyebrow);

            var chapter = new VisualElement { name = "chapter-art" };
            chapter.AddToClassList("campaign-select-view__chapter");
            var illustration = Resources.Load<Texture2D>("Art/campaign/hollow-reach");
            if (illustration != null) chapter.style.backgroundImage = new StyleBackground(illustration);
            var chapterLabel = new Label(CampaignSelectScreen.ChapterLabel);
            chapterLabel.AddToClassList("campaign-select-view__chapter-label");
            var chapterName = new Label(CampaignSelectScreen.ChapterName);
            chapterName.AddToClassList("campaign-select-view__chapter-name");
            chapter.Add(chapterLabel);
            chapter.Add(chapterName);
            _scaffold.Content.Add(chapter);

            // ONE CARD FOR THE WHOLE LIST, NOT ONE PER ROW. The rows are a
            // single list of one kind of thing, which is what the handoff
            // puts on one surface; a card each would give every wave its own
            // drop shadow and turn a list into a stack of unrelated panels.
            var card = new SectionCard();
            card.Body.Add(_waves);
            _scaffold.Content.Add(card);
            Add(_scaffold);
        }

        /// `waves` is the bundle's authored ids, as `/v1/sync`'s
        /// `config.waves` sent them. IN THE ORDER GIVEN - the config is the
        /// campaign's order and re-sorting it here would be this view
        /// deciding something the bundle already decided.
        public void Bind(IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick, Action onBack = null)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));

            // A back only when the hub shows this as a pushed step (Phase 10
            // Task 1.3's Defend); the tutorial shows it top-level and passes none.
            _scaffold.OnBack = onBack;

            _waves.Clear();

            // An authored-nothing bundle says so rather than rendering as a
            // screen that failed to load. `FtueDirector.CampaignAsync`
            // returns before binding on an empty set, so this is one
            // caller's guard becoming the view's own.
            if (waves.Count == 0)
            {
                _waves.Add(new EmptyState(CampaignSelectScreen.EmptyMessage, "map"));
                return;
            }

            VisualElement last = null;
            foreach (var id in waves)
            {
                last = RowFor(id, waves, highestWaveCleared, onPick);
                _waves.Add(last);
            }

            // THE LAST ROW'S BOTTOM MARGIN IS CANCELLED INLINE, AND USS
            // CANNOT DO IT. `.option-row` carries `margin-bottom:
            // var(--space-2)` so consecutive rows separate; inside a card
            // that leaves 8px of the component's margin on top of the card's
            // own 16px padding, so the list sits 24px off the bottom edge
            // against 16px off the top. UI Toolkit has no structural
            // pseudo-classes - no `:last-child`, no `:nth-child` - so there
            // is no selector that can say "this one". An inline style is what
            // is left, and it is safe here because every `Bind` clears the
            // list and rebuilds it, so no row can keep a cancellation it is
            // no longer entitled to.
            if (CampaignSelectScreen.AllCleared(waves, highestWaveCleared))
            {
                var held = new OptionRow(CampaignSelectScreen.HeldLabel, CampaignSelectScreen.HeldDetail, null)
                    { name = CampaignSelectScreen.HeldName };
                held.AddToClassList(HeldUssClassName);
                _waves.Add(held);
                last = held;
            }

            if (last != null) last.style.marginBottom = 0;
        }

        static VisualElement RowFor(
            int id, IReadOnlyList<int> waves, int highestWaveCleared, Action<int> onPick)
        {
            var playable = CampaignSelectScreen.IsPlayable(id, waves, highestWaveCleared);
            var picked = id;

            // NO HANDLER AT ALL on a locked row, rather than a handler behind
            // a disabled element. `SetEnabled(false)` already stops UI Toolkit
            // routing pointer events there, so this is belt and braces - but
            // the belt is what a reader can check, and `onPick` reaching the
            // director for a wave `wave/start` can only refuse is the kind of
            // thing that presents as an unexplained `wave_locked`.
            Action onSelect = null;
            if (playable) onSelect = () => onPick?.Invoke(picked);

            var row = new OptionRow(
                CampaignSelectScreen.RowLabel(id),
                CampaignSelectScreen.StateLabel(id, waves, highestWaveCleared),
                onSelect)
            {
                name = CampaignSelectScreen.RowName(id),
            };
            row.AddToClassList(RowUssClassName);

            // SetEnabled ON THE ROW, not on a button inside it: a locked wave
            // must not be reachable by tapping its label either, and
            // `enabledSelf` on the row is what a caller - and a test - reads
            // back to know whether this wave can be started.
            row.SetEnabled(playable);

            if (!playable)
            {
                // The padlock, out of flow at the row's right edge - the
                // handoff's mark for a destination that exists and is not
                // yours yet. The word "Locked" is already in the detail line;
                // the glyph is what makes the row readable at a glance
                // against three others, which is what design 5.2 asks the
                // campaign list to do.
                var lockGlyph = new VisualElement { name = "lock" };
                lockGlyph.AddToClassList("icon");
                lockGlyph.AddToClassList("icon--lock");
                lockGlyph.pickingMode = PickingMode.Ignore;
                row.Add(lockGlyph);
            }
            else if (CampaignSelectScreen.IsCleared(id, highestWaveCleared))
            {
                // THE GREEN CHIP IS THE PADLOCK'S OPPOSITE NUMBER AND IS
                // REDUNDANT IN EXACTLY THE SAME WAY, ON PURPOSE. "Cleared" is
                // already in the detail line, as "Locked" already is - and
                // the mark is what makes a beaten wave findable in a list at
                // a glance, which words in an 11px second line are not. It
                // reads the same constant the detail line does, so the two
                // cannot drift into saying different things.
                //
                // A `.chip` RATHER THAN A `GenChip`. That component's whole
                // interface is `GenChip(int generation)` - it renders
                // `CreatureLabel.Generation`, which would put a "G" in front
                // of this - so what is shared is the SHAPE, and the shape is
                // Theme.uss's `.chip` underneath both. Widening GenChip to
                // take arbitrary text to serve one row is the kind of
                // contract change Task 13's vocabulary was fixed to prevent.
                //
                // READ OFF `GenChip` RATHER THAN TYPED AGAIN. The shared
                // shape has a name - `GenChip.ChipUssClassName`, declared for
                // exactly this - and the line below it in this same method
                // already reads `ClearedChipUssClassName` for the same
                // reason. A literal here would have been the one string in
                // the pair that nothing keeps in step.
                var chip = new Label(CampaignSelectScreen.ClearedLabel) { name = "cleared" };
                chip.AddToClassList(GenChip.ChipUssClassName);
                chip.AddToClassList(ClearedChipUssClassName);
                chip.pickingMode = PickingMode.Ignore;
                row.Add(chip);
            }

            return row;
        }
    }
}
