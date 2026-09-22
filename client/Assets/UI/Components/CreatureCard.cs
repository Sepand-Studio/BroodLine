using System;
using System.Collections.Generic;
using Broodline.Api;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// screen_inventory_v2 11: "the atom of the interface" - it appears on at
    /// least nine screens, and is worth disproportionate effort here because
    /// every one of those screens just binds it rather than rebuilding it.
    ///
    /// screen_inventory_v2 3's rework note is explicit: TWO trait marks, not
    /// four. This card renders exactly two on every `Bind` - cleared and
    /// rebuilt rather than re-bound - so no amount of re-binding (a recycled
    /// row in a roster list, for instance) can accumulate a third or a
    /// fourth.
    ///
    /// ===================================================================
    /// PHASE 9 TASK 16: THE MARKS ARE `TraitChip`s AND THE GENERATION IS A
    /// `GenChip`, WHICH IS THE HANDOFF'S OWN CARD AND A REVERSAL OF ONE
    /// SENTENCE IN `TraitChip`'s CLASS COMMENT.
    ///
    /// `Creature Roster.dc.html:66-69` draws each card's traits as
    /// SPECIES-TINTED CHIPS - `#8e6d15` on `#fbf3e0` for a Skitter trait,
    /// `#3c7f98` on `#e7f3f9` for a Vetch one - with no dot and no counter
    /// mark anywhere on the card. `TraitChip`'s comment reasoned the other
    /// way ("the roster card shows pips because the question there is what
    /// does this animal answer"), and the measurement that settles it is
    /// this: EVERY `CreatureCard.Bind` IN THE APP PASSES `counters: null`.
    /// All six call sites - roster, splice chamber, splice reveal (twice),
    /// post-wave and wave-defeat - hand over nothing, so the pip's dot has
    /// never once gone green in a shipped screen and the affordance the
    /// comment defends is not in use. Nothing is lost by the swap and the
    /// six species tints are gained.
    ///
    /// `TraitPip` SURVIVES AND IS STILL THE RIGHT ANSWER WHERE A COUNTER IS
    /// ACTUALLY KNOWN. Both components still exist; what changed is which
    /// one this card composes.
    ///
    /// THIS PARAGRAPH NAMED `LineageView` AS THE SURVIVING CONSUMER AND THAT
    /// WAS WRONG WHEN IT WAS WRITTEN - Phase 9 Task 19 found it. The tree's
    /// `AddTrait` passed `counters: null` too, and its own comment said it
    /// always would ("the lineage response carries no trait table, and
    /// `Broodline.UI` cannot derive one"), so the measurement above applied
    /// there unchanged. It draws `TraitChip`s now and no screen in the app
    /// builds a pip. The component and its tests stay: the pip is the right
    /// answer the day a screen holds a real counter map, and the tree was
    /// never that screen.
    ///
    /// THE `counters` PARAMETER IS KEPT ON `Bind`, unused by the marks. It
    /// is a public shape five screens call, bible 10.5 still makes the
    /// counter the teaching surface, and a card that wanted to draw one
    /// again would need the map back. Removing it would make restoring it a
    /// six-call-site change for no gain today.
    /// ===================================================================
    [UxmlElement]
    public partial class CreatureCard : VisualElement
    {
        public const string UssClassName = "creature-card";
        public const string FounderUssClassName = "founder";
        public const string AberrantUssClassName = "aberrant";
        public const string CommittedUssClassName = "committed";

        readonly VisualElement _silhouette;
        readonly VisualElement _gen;
        readonly VisualElement _traits;
        readonly Label _name;
        readonly Label _instinct;

        public CreatureCard()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CreatureCard");
            tree.CloneTree(this);

            _silhouette = this.Q<VisualElement>("silhouette");
            _gen = this.Q<VisualElement>("gen");
            _traits = this.Q<VisualElement>("traits");
            _name = this.Q<Label>("name");
            _instinct = this.Q<Label>("instinct");
        }

        /// `counters` maps a trait name to the single raider/name it answers
        /// (bible 1.2's "bold traits are counters") - empty or missing for
        /// one of the four traits that counter nothing by design. It is not
        /// re-derived here; Broodline.UI does not reference Broodline.Sim and
        /// has no ruleset of its own to derive it from.
        ///
        /// READ BY NOTHING SINCE PHASE 9 TASK 16 AND KEPT ANYWAY - the class
        /// comment has the full account. The short version: the card's trait
        /// marks are `TraitChip`s now, which are tinted by the species that
        /// carries the trait rather than by what it answers; every one of the
        /// six live call sites already passed null here, so nothing that was
        /// being drawn stopped being drawn. The parameter stays because bible
        /// 10.5 still makes the counter the teaching surface and restoring a
        /// counter mark should not be a six-call-site change.
        public void Bind(CreatureDto creature, IReadOnlyDictionary<string, string> counters)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));

            _name.text = CreatureLabel.DisplayName(creature);
            _silhouette.tooltip = creature.Species;

            // THE ROLE LINE, NOT THE BARE INSTINCT. `Creature Roster
            // .dc.html:64` draws a two-part muted line under every name
            // ("Warform · on duty"); `CreatureLabel.RoleLine` is the two
            // halves of that this project actually holds - bible 1.2's role
            // word and the creature's own Instinct. Both were on the card
            // already in substance; only the role was missing and the
            // instinct was reading as a lone shouted word.
            //
            // AND IT COLLAPSES WHEN THERE IS NEITHER, on the rule every
            // conditional row in this project keeps: the label carries
            // --text-micro tracking and 6px of margin, so an empty one is a
            // gap under the name rather than nothing.
            var role = CreatureLabel.RoleLine(creature);
            _instinct.text = role;
            _instinct.style.display = role.Length == 0
                ? DisplayStyle.None : DisplayStyle.Flex;

            // REBUILT, NOT RE-BOUND, AND THE CONTRACT IS UNCHANGED BY IT.
            // `GenChip` and `TraitChip` both take their whole content in the
            // constructor - a generation, a trait and a species tint decided
            // once - so there is nothing to re-point on a second `Bind`.
            // Clearing first is what keeps screen_inventory_v2 3's "two, not
            // four" true across a recycled card, which is the same guarantee
            // the two held fields used to give and is asserted the same way.
            _gen.Clear();
            _gen.Add(new GenChip(creature.Generation));

            // AN ABSENT SLOT DRAWS NOTHING - Phase 9 Task 21e. The granted
            // Hollow on the post-wave screen has no combat traits at all, and
            // this card drew two chips reading "None" at the player: the
            // wire's word for an empty slot, laid out as though it were the
            // name of a trait. `CreatureLabel.IsAbsentTrait` has the ruling
            // and why the engine is not at fault.
            //
            // "TWO, NOT FOUR" IS UNCHANGED AND IS STILL THE GUARANTEE
            // (screen_inventory_v2 3). What this adds is an upper bound that
            // was never the question: two SLOTS are read, in order, and each
            // draws a chip only if it holds something. A creature with one
            // trait shows one chip; a creature with none shows no row at all.
            //
            // AND THE ROW COLLAPSES WHEN IT IS EMPTY, which is not tidying:
            // `.creature-card__traits` carries `margin-top: 7px`, so a row
            // with nothing in it is a 7px hole under the role line rather
            // than nothing. This is the same rule `_instinct` two blocks up
            // keeps, and `FounderNamingView.Blocker` and
            // `ScreenScaffold.FooterNote` before it - every conditional row
            // in this project is removed from the layout rather than blanked,
            // because every one of them carries a margin of its own.
            _traits.Clear();
            AddTrait(creature.Trait1, creature.Tier1, creature.Species);
            AddTrait(creature.Trait2, creature.Tier2, creature.Species);
            _traits.style.display = _traits.childCount == 0
                ? DisplayStyle.None : DisplayStyle.Flex;

            // The baked sprites, stacked body / dorsal / flank - Phase 9's
            // bridge from the 3D pipeline to the flat card. `CreatureSprites`
            // returns null on anything it does not have art for, never a
            // guess, so an unrecognised species or an unbaked trait leaves
            // its layer empty rather than showing the wrong creature.
            SetLayer("silhouette", CreatureSprites.Body(creature.Species));
            SetLayer("part-dorsal", CreatureSprites.Part(creature.Species, "sk_dorsal", creature.Trait1));
            SetLayer("part-flank", CreatureSprites.Part(creature.Species, "sk_flank", creature.Trait2));

            EnableInClassList(FounderUssClassName, creature.IsFounder);
            EnableInClassList(CommittedUssClassName, creature.CommittedTo.HasValue);

            // data_model 2: a TraitInstance's coverage_tier is null exactly
            // when it holds an Aberrant. Either combat slot can hold one.
            //
            // AN EMPTY SLOT IS NOT AN ABERRANT, AND THIS CARD SAID IT WAS -
            // Phase 9 Task 21e. `"None"` arrives with a null tier (the
            // contract serialises `Trait.None` by name and leaves the tier
            // null - `SimulateTests` asserts exactly that pair), so the test
            // above marked every traitless creature `aberrant` and drew the
            // rare-trait treatment around it. The granted Hollow the walk
            // found was wearing it. Task 6b ruled on this same pair in
            // `FtueDirector.Tier` - "an absent trait has no coverage to null
            // out" - and this is that ruling applied one layer up.
            EnableInClassList(AberrantUssClassName,
                IsAberrant(creature.Trait1, creature.Tier1)
                || IsAberrant(creature.Trait2, creature.Tier2));
        }

        /// A slot holds an Aberrant when it holds a TRAIT whose coverage tier
        /// is null. Both halves are load-bearing: no trait is not an Aberrant,
        /// and a trait with a tier is not one either.
        static bool IsAberrant(string trait, int? tier)
        {
            return tier == null && !CreatureLabel.IsAbsentTrait(trait);
        }

        /// One combat slot, drawn only if it holds something.
        void AddTrait(string trait, int? tier, string species)
        {
            if (CreatureLabel.IsAbsentTrait(trait)) return;
            _traits.Add(new TraitChip(trait, tier, species));
        }

        void SetLayer(string name, Texture2D texture)
        {
            var layer = this.Q<VisualElement>(name);
            layer.style.backgroundImage = texture == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(texture);
        }
    }
}
