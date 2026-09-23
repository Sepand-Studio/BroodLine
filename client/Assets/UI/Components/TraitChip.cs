using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A trait, tinted by the species that carries it - "Carapace III" in
    /// teal for a Vetch, "Cinder Spit" in coral for an Ember.
    ///
    /// COUNTED, NOT CHOSEN. `Creature Roster.dc.html` draws this shape once
    /// per trait per card and tints it from exactly six families: violet
    /// (#ece4fa/#6b5fa8), amber (#fbf3e0/#8e6d15), teal (#e7f3f9/#3c7f98),
    /// coral (#fbeae7/#b3564a), green (#e8f5ec/#43704f) and a neutral grey
    /// (#f2eff8/#8d86a0) - one per founder species, in the order bible 1.2
    /// lists them. The six rules in TraitChip.uss are that table, THROUGH
    /// THE TOKEN LAYER RATHER THAN VERBATIM: each rule names a `-tint` and a
    /// `-text` token, and three of those tokens sit a step off the hex above
    /// because Tokens.uss reconciled the handoff's per-screen values into
    /// one palette. Teal's ink is the one that moved on PURPOSE - Phase 9
    /// Task 14 ruled --teal-text from #3c7f98 to #336b81 because the
    /// handoff's own value measures 3.97:1 on its own tint and this chip
    /// renders at 10px. Tokens.uss's --teal-text note has the ruling; this
    /// file changed nothing for it, which is the point of reading a token.
    ///
    /// IT IS NOT `TraitPip`, AND BOTH SURVIVE. A pip says what a trait
    /// COUNTERS - its dot goes green when the trait answers the raider on
    /// screen, which is the counter system's teaching surface (bible 10.5) -
    /// and it is violet whatever the creature is. A chip says whose trait it
    /// is. The splice chamber shows chips under each parent's name because
    /// the question there is "what does this animal bring"; the roster card
    /// shows pips because the question there is "what does this animal
    /// answer".
    ///
    /// THE TEXT COMES FROM `CreatureLabel.TraitWithTier`, the same call
    /// `TraitPip.Bind` makes, so a tier is written I-III in one place.
    ///
    /// A NULL TIER IS AN ABERRANT AND IS NEVER A ZERO. data_model 2 is
    /// explicit that the two must not be conflated - "zero would sort and
    /// display as 'less than tier I'" - so a null tier renders as the bare
    /// trait name plus the `aberrant` marker, which is the same contract
    /// `TraitPip` and `CreatureCard` already keep.
    ///
    /// UNLESS THERE IS NO TRAIT, WHICH IS A THIRD STATE AND WAS BEING READ AS
    /// THE SECOND - Phase 9 Task 21e. An empty combat slot comes over the
    /// wire as `"None"` with a null tier, so every absence was wearing the
    /// Aberrant outline. `CreatureLabel.IsAbsentTrait` is the one definition
    /// of that state and the constructor consults it.
    public sealed class TraitChip : VisualElement
    {
        public const string UssClassName = "trait-chip";
        public const string ChipUssClassName = "chip";

        /// Shared with `TraitPip` and `CreatureCard` by spelling rather than
        /// by reference, because all three mean the same thing by it and a
        /// stylesheet cannot import a constant.
        public const string AberrantUssClassName = "aberrant";

        /// A CHIP WAS TAPPED, ANYWHERE - Phase 10 Task 1.3. The Codex was
        /// reachable from nothing at runtime (Phase 9 follow-ups §2); every
        /// trait chip is now a way in. Static, because the chip is built by
        /// six screens that have no channel to the Game layer, and the one
        /// subscriber (BootController) shows the sheet. The tap stops here so
        /// a chip on a selectable card does not also select the card.
        public static event Action<string> AnyTapped;

        /// `species` is one of bible 1.2's six. Anything else - null, empty,
        /// or a display name like "Ember Skitter" that names two of them -
        /// adds no modifier and the chip renders in `.chip`'s default
        /// violet. NOT A GUESS AND NOT A THROW: `CreatureSprites` already
        /// set this rule for the same field, and a screen is not the place
        /// to find out that a DTO carried a display name. The untinted chip
        /// is visible, which is what makes it findable.
        public TraitChip(string trait, int? tier, string species)
        {
            AddToClassList(ChipUssClassName);
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("TraitChip").CloneTree(this);

            var key = (species ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length > 0) AddToClassList(UssClassName + "--" + key);

            // A NULL TIER IS AN ABERRANT ONLY IF THERE IS A TRAIT - Phase 9
            // Task 21e. An empty combat slot arrives as `"None"` with a null
            // tier (`CreatureLabel.IsAbsentTrait` has the wire shape and Task
            // 6b's ruling), so `tier == null` alone marked every absence with
            // the rare-trait outline. No screen builds such a chip any more -
            // `CreatureCard` and `LineageView` both skip an absent slot - and
            // this is the guard that keeps a direct caller from re-creating
            // the defect the exit gate's walk found.
            EnableInClassList(AberrantUssClassName,
                tier == null && !CreatureLabel.IsAbsentTrait(trait));

            this.Q<Label>("trait").text = CreatureLabel.TraitWithTier(trait, tier);

            var name = trait;
            RegisterCallback<ClickEvent>(evt =>
            {
                if (CreatureLabel.IsAbsentTrait(name)) return;
                evt.StopPropagation();
                AnyTapped?.Invoke(name);
            });
        }
    }
}
