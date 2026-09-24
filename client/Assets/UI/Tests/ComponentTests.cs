using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Broodline.Api;
using Broodline.UI.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

// UnityEngine.UIElements HAS ITS OWN `ProgressBar` (base
// `AbstractProgressBar`), so in a file importing both namespaces the bare
// name binds to Unity's type. It does not report as an ambiguity: the
// compiler says "'ProgressBar' does not contain a constructor that takes 1
// arguments", which reads like a broken signature on ours. Measured - that
// is the exact error this task's first red run produced. Every consumer in
// Tasks 9-12 will need this line too; ProgressBar.cs's class comment says so.
using ProgressBar = Broodline.UI.Components.ProgressBar;

namespace Broodline.UI.Tests
{
    /// The five shared components - screen_inventory_v2 11: "build once,
    /// reuse everywhere." Every one of these is constructed and inspected as
    /// a plain `VisualElement` tree with no scene and no live `Panel`, the
    /// same approach `TabBarTests` established in Task 13.
    ///
    /// Two of the given assertions did not survive contact with this Editor
    /// version and are called out at their use site rather than silently
    /// swapped:
    ///
    /// 1. `VisualElement.GetCallbackCount&lt;TEventType&gt;()` does not exist.
    ///    Confirmed by loading UnityEngine.UIElementsModule.dll (and, for
    ///    good measure, every other managed assembly under this Editor's
    ///    Contents folder) via reflection and listing every member of
    ///    `CallbackEventHandler`/`VisualElement` with "callback" or "count"
    ///    in its name: the closest things that exist are
    ///    `CallbackEventHandler`'s internal `HasTrickleDownEventCallbacks
    ///    (int)` and `HasBubbleUpEventCallbacks(int)`, keyed by an internal
    ///    event-category integer rather than a generic type argument, and
    ///    `internal` regardless. See `NamedConfirm_HasNoDefaultButtonAnd
    ///    CannotBeDismissedOutside` for what is asserted instead.
    ///
    /// 2. `SpliceDialog`'s properties are `{ get; internal set; }`
    ///    (`client/Assets/UI/SpliceScreen.cs`), deliberately - the file's own
    ///    comment on `Suppressible` says it "exists so that a future
    ///    'remember my choice' feature has to change a value a test reads."
    ///    A `new SpliceDialog { Title = ... }` object initializer from this
    ///    assembly does not compile (CS0122
    ///    on every property). `SpliceConfirmTests.cs` already established the
    ///    fix for the exact same constraint: build the dialog the same way
    ///    production code does, through `SpliceScreen.Build(...)`.
    ///
    /// 3. (Fix round 1.) There is no way, from this assembly, to prove a
    ///    registered `ClickEvent` callback actually runs - only that one
    ///    COULD run. Confirming it fires needs a dispatched `ClickEvent`,
    ///    which needs an attached `Panel` (the same constraint
    ///    `TabBarTests`'s class comment documents for `Button.clicked`), and
    ///    a bare `new ConfirmDialog()` has none. So
    ///    `StandardConfirm_IsPrimaryAndTheScrimAcceptsPointerEvents` pins
    ///    only the structural precondition for dismissal - the scrim is
    ///    pickable, so a live Panel WOULD route a tap to it - not the
    ///    dismissal itself. `ConfirmDialog.Standard`'s
    ///    `RegisterCallback&lt;ClickEvent&gt;(_ =&gt; cancel?.Invoke())` is
    ///    trusted the same way `TabBar`'s per-button click closures are.
    ///    (The mirror-image `Named` case in point 1 above is provable
    ///    precisely because `PickingMode.Ignore` is a structural fact a
    ///    test CAN read back; "a handler is registered and would fire" has
    ///    no equivalently-readable positive form.)
    public class ComponentTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static CreatureDto Creature(
            string species, int gen, string name, bool founder,
            string trait1 = "Chill", int? tier1 = 1,
            string trait2 = "Guard", int? tier2 = 1)
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = species,
                Generation = gen,
                Trait1 = trait1,
                Tier1 = tier1,
                Trait2 = trait2,
                Tier2 = tier2,
                Instinct = "Forage",
                Name = name,
                IsFounder = founder,
                CommittedTo = null,
            };
        }

        static CreatureDto Named(string name) => Creature("Vetch Crawler", 4, name, founder: true);
        static CreatureDto Founder(string name) => Named(name);
        static CreatureDto Unnamed(string species, int gen) => Creature(species, gen, name: null, founder: false);
        static CreatureDto A() => Unnamed("Vetch Crawler", 4);
        static CreatureDto B() => Unnamed("Ember Skitter", 6);

        /// Trait -> the single raider it answers, straight off bible's own
        /// raider table (section 10.5 / the Threat table): Taunt answers
        /// Lash, Chill answers Courser. Carapace is deliberately absent - one
        /// of the four utility traits that "counter nothing by design"
        /// (bible 1.2).
        static IReadOnlyDictionary<string, string> Counters()
        {
            return new Dictionary<string, string>
            {
                { "Taunt", "Lash" },
                { "Chill", "Courser" },
            };
        }

        /// The forecast and coverage warning are the server's - see
        /// SpliceConfirmTests for why this assembly never re-derives either.
        /// Only `SpliceScreen.Build` needs a well-formed one here; nothing in
        /// this file reads it directly.
        static SplicePreviewResponse Preview()
        {
            var r = new SplicePreviewResponse();
            r.Forecast = new SpliceForecast { Mutation = 0.09, Aberrant = 0.01 };
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Guard", Tier = 1, P = 0.55 });
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Ward", Tier = 2, P = 0.45 });
            r.Forecast.Instinct.Add(new Instinct { Instinct1 = "Forage", P = 1.0 });
            return r;
        }

        // ---------------------------------------------------------------
        // CreatureCard - screen_inventory_v2 11 & 3
        // ---------------------------------------------------------------

        /// PHASE 9 TASK 16: TWO `TraitChip`s WHERE THIS ASSERTED TWO
        /// `TraitPip`s, AND A `GenChip` WHERE IT ASSERTED A BARE LABEL. The
        /// COUNT is the part screen_inventory_v2 3 is about and it is
        /// unchanged - two marks, not four. `CreatureCard`'s class comment
        /// has the measurement behind the swap: `Creature Roster.dc.html
        /// :66-69` draws species-tinted chips with no counter mark, and every
        /// one of the six live `Bind` call sites in the app passes
        /// `counters: null`, so the pip's dot has never gone green in a
        /// shipped screen. `TraitPip` itself is untouched and its own tests
        /// below are unchanged.
        ///
        /// IT HAS NO SCREEN CONSUMER LEFT AS OF PHASE 9 TASK 19, and this
        /// comment claimed one until that task. `LineageView` was the last,
        /// and it was exempted from the Task 16 swap on the grounds that the
        /// tree is "where a counter IS actually known" - which `LineageView
        /// .AddTrait`'s own comment had always denied ("the lineage response
        /// carries no trait table, and `Broodline.UI` cannot derive one").
        /// It passes a `TraitChip` now, for the reason this very swap gives.
        /// The component and these tests stay: the pip is still the right
        /// answer the day a screen holds a real counter map, and retiring a
        /// tested component to record that nothing calls it today would be
        /// the more expensive mistake.
        [Test]
        public void CreatureCard_ShowsTwoTraitChipsWithCoverageAndTheFounderName()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            Assert.AreEqual(2, card.Query<TraitChip>().ToList().Count);          // two, not four - screen_inventory 3
            Assert.IsEmpty(card.Query<TraitPip>().ToList(),
                "the card draws both a chip and a pip per trait, so each trait is on it twice");
            StringAssert.Contains("Ash", card.Q<Label>("name").text);
            Assert.IsTrue(card.ClassListContains("founder"));

            // CreatureLabel.Generation is the one place this format is
            // written (fix round 1: it used to be inlined here too), and
            // `GenChip` reads it - which is what carries the `t-num` a bare
            // --text-secondary Label never had.
            var gen = card.Q<GenChip>();
            Assert.IsNotNull(gen, "the generation is not a GenChip, so it carries no numeral marker");
            Assert.AreEqual("G1", gen.Q<Label>("gen").text);
            Assert.IsTrue(gen.Q<Label>("gen").ClassListContains("t-num"));

            // AND THE ROLE LINE IS bible 1.2's ROLE BESIDE THE INSTINCT -
            // the handoff's `Warform · on duty` at `:64`, built from the two
            // halves a `CreatureDto` actually holds.
            Assert.AreEqual(CreatureLabel.RoleLine(
                    Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1)),
                card.Q<Label>("instinct").text);
            StringAssert.Contains("Wall", card.Q<Label>("instinct").text);
        }

        /// THE ONE DEFINITION OF AN EMPTY COMBAT SLOT - Phase 9 Task 21e.
        ///
        /// THERE WERE TWO AND A HALF BEFORE THIS. `LineageView.AddTrait`
        /// screened `string.IsNullOrEmpty`, which catches a missing STRING and
        /// not the wire's word for a missing TRAIT; `CreatureCard` screened
        /// nothing and drew "None" at the player; `FtueDirector.Tier` had the
        /// right test and kept it to itself. One place knows now, and the
        /// three readers consult it.
        ///
        /// EVERY ARM BELOW IS A WAY THE SLOT ACTUALLY ARRIVES OR A WAY IT
        /// MUST NOT BE READ. Dropping the `"None"` arm restores the defect;
        /// dropping `Trim` lets a padded value through; making the comparison
        /// case-sensitive breaks on a wire that ever shouts; and the
        /// "Noneish" arm is what stops the test passing against a
        /// `StartsWith` or a `Contains`, which would swallow a real trait
        /// called "Nonesuch".
        [Test]
        public void AnAbsentTraitIsRecognisedByOneDefinition_AndARealOneIsNot()
        {
            Assert.IsTrue(CreatureLabel.IsAbsentTrait("None"), "the wire's own word for an empty slot");
            Assert.IsTrue(CreatureLabel.IsAbsentTrait("none"), "case must not decide this");
            Assert.IsTrue(CreatureLabel.IsAbsentTrait("NONE"));
            Assert.IsTrue(CreatureLabel.IsAbsentTrait(" None "), "a padded value is the same absence");
            Assert.IsTrue(CreatureLabel.IsAbsentTrait(null));
            Assert.IsTrue(CreatureLabel.IsAbsentTrait(string.Empty));

            Assert.IsFalse(CreatureLabel.IsAbsentTrait("Chill"));
            Assert.IsFalse(CreatureLabel.IsAbsentTrait("Nonesuch"),
                "a real trait whose name opens with the absence's was swallowed - this is what "
                + "a StartsWith or a Contains would do");
        }

        /// THE SAME ABSENCE IN PROSE - `SpliceRevealScreen.ChildLine` names a
        /// new creature's two combat slots in a sentence, and read both of
        /// them unconditionally. A child with one trait was introduced as
        /// "Ash (G3) — Carapace II, None", with a comma in front of the hole.
        ///
        /// THE NO-TRAIT ARM DROPS THE DASH WITH IT, on `CreatureLabel
        /// .RoleLine`'s own rule: "a line reading ` · Forage` is a hole where
        /// a fact should be". A dangling em dash is that hole one punctuation
        /// mark over.
        [Test]
        public void TheRevealsChildLine_NamesOnlyTheSlotsThatHoldSomething()
        {
            var one = SpliceRevealScreen.ChildLine(
                Creature("Vetch", gen: 3, name: "Ash", founder: false,
                         trait1: "Carapace", tier1: 2, trait2: "None", tier2: null));
            StringAssert.DoesNotContain("None", one, "the empty slot was named in the sentence");
            StringAssert.Contains("Carapace II", one);
            StringAssert.DoesNotContain(", ", one, "the joiner outlived the trait it was joining");

            var both = SpliceRevealScreen.ChildLine(
                Creature("Vetch", gen: 3, name: "Ash", founder: false,
                         trait1: "Carapace", tier1: 2, trait2: "Taunt", tier2: 1));
            StringAssert.Contains("Carapace II, Taunt I", both,
                "sanity: two real traits must still be joined, or the check above proves nothing");

            var none = SpliceRevealScreen.ChildLine(
                Creature("Hollow", gen: 1, name: null, founder: false,
                         trait1: "None", tier1: null, trait2: "None", tier2: null));
            StringAssert.DoesNotContain("—", none, "a dangling em dash is a hole where a fact should be");
            StringAssert.DoesNotContain("None", none);
            Assert.IsNotEmpty(none, "the creature still has a name and a generation to state");
        }

        /// AN EMPTY COMBAT SLOT DRAWS NO CHIP - PHASE 9 TASK 21e, AND THIS IS
        /// THE DEFECT THE EXIT GATE'S WALK FOUND ON A REAL DEVICE. The
        /// granted Hollow on the post-wave screen has no combat traits, and
        /// this card drew TWO CHIPS BOTH READING "None": the wire's own word
        /// for an empty slot, laid out as though it were the name of a trait.
        ///
        /// THE ENGINE IS NOT AT FAULT AND THAT IS WHY THE FIX IS HERE.
        /// `Trait` is an enum whose zero member is `None`, the contract
        /// serialises it by name with a null tier, and Task 6b already ruled
        /// on what the pair MEANS - `FtueDirector.Tier` returns 0 for it
        /// rather than throwing, because "an absent trait has no coverage to
        /// null out". `CreatureLabel.IsAbsentTrait` is the one place that
        /// knows it, and `CreatureLabelTests` holds its own arms.
        ///
        /// BOTH HALVES OF THE ABERRANT BUG TOO. A null tier IS an Aberrant
        /// (data_model 2) and an empty slot ALSO arrives with a null tier, so
        /// `Tier1 == null || Tier2 == null` marked every traitless creature
        /// `aberrant` and drew the rare-trait treatment around the word
        /// "None". Reverting either the chip guard or the aberrant guard
        /// reddens a line below.
        [Test]
        public void CreatureCard_WithAnEmptySlot_DrawsNoChipForItAndIsNotAberrant()
        {
            // ONE trait and one absence - the shape `FtueDirectorTests`
            // documents ("a creature with one combat trait arrives as trait2
            // \"None\" with a null tier").
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: false,
                               trait1: "Taunt", tier1: 1, trait2: "None", tier2: null), Counters());

            var chips = card.Query<TraitChip>().ToList();
            Assert.AreEqual(1, chips.Count, "the empty slot drew a chip of its own");
            Assert.AreEqual(CreatureLabel.TraitWithTier("Taunt", 1), chips[0].Q<Label>("trait").text);
            Assert.IsFalse(card.ClassListContains("aberrant"),
                "an empty slot is not an Aberrant - Task 6b ruled on exactly this pair");

            // NEITHER trait: the granted Hollow the walk found. No chips at
            // all, and the row is removed rather than left as a 7px hole
            // under the role line.
            var bare = new CreatureCard();
            bare.Bind(Creature("Hollow", gen: 1, name: null, founder: false,
                               trait1: "None", tier1: null, trait2: "None", tier2: null), Counters());

            Assert.IsEmpty(bare.Query<TraitChip>().ToList(),
                "the granted Hollow's card drew two chips reading \"None\" at the player");
            Assert.IsFalse(bare.ClassListContains("aberrant"));
            Assert.AreEqual(DisplayStyle.None, bare.Q<VisualElement>("traits").style.display.value,
                "the empty trait row carries margin-top: 7px, so it has to be removed rather "
                + "than left empty");

            // AND A REAL ABERRANT STILL READS AS ONE, which is what stops the
            // guard above being written as `tier == null ? not aberrant`. A
            // null tier on a PRESENT trait is data_model 2's Aberrant and the
            // card must still say so.
            var aberrant = new CreatureCard();
            aberrant.Bind(Creature("Ember", gen: 2, name: null, founder: false,
                                   trait1: "Cinder", tier1: null, trait2: "None", tier2: null), Counters());
            Assert.IsTrue(aberrant.ClassListContains("aberrant"),
                "a trait with a null coverage tier IS an Aberrant and the card stopped saying so");
            Assert.AreEqual(1, aberrant.Query<TraitChip>().ToList().Count);
        }

        /// THE CHIPS ARE TINTED BY THE SPECIES THAT CARRIES THE TRAIT, which
        /// is the whole reason the card moved off `TraitPip`: a pip is violet
        /// whatever the creature is. Without this, a card that built two
        /// untinted chips would pass the count above.
        [Test]
        public void CreatureCard_TintsItsTraitChipsWithItsOwnSpecies()
        {
            var card = new CreatureCard();
            card.Bind(Unnamed("Skitter", 2), Counters());

            foreach (var chip in card.Query<TraitChip>().ToList())
            {
                Assert.IsTrue(chip.ClassListContains(TraitChip.UssClassName + "--skitter"),
                    "a roster card's chips are untinted, so six species read as one");
            }
        }

        [Test]
        public void CreatureCard_FallsBackToSpeciesWhenNoNameWasChosen()
        {
            var card = new CreatureCard();
            card.Bind(Unnamed("Hollow", 3), Counters());

            StringAssert.Contains("Hollow", card.Q<Label>("name").text);
            Assert.IsFalse(card.ClassListContains("founder"));
        }

        [Test]
        public void CreatureCard_MarksAberrantWhenACombatSlotHasNoCoverageTier()
        {
            // data_model 2: a null coverage tier is an Aberrant. Either
            // combat slot can hold one.
            var card = new CreatureCard();
            card.Bind(Creature("Ember", gen: 2, name: null, founder: false, trait1: "Cinder", tier1: null, trait2: "Splash", tier2: 1), Counters());

            Assert.IsTrue(card.ClassListContains("aberrant"));
        }

        [Test]
        public void CreatureCard_Rebind_ReplacesRatherThanAccumulatesTraitMarks()
        {
            // The card renders exactly two trait marks and one generation
            // chip per Bind - a recycled card in a roster list must never
            // grow a third or a fourth. Phase 9 Task 16 changed HOW that is
            // kept (cleared and rebuilt, rather than two held fields
            // re-bound) because `TraitChip` and `GenChip` both take their
            // whole content in the constructor; the guarantee is the same
            // and this is the test that says so.
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            card.Bind(Creature("Skitter", gen: 3, name: "Bramble", founder: true, trait1: "Sprint", tier1: 2, trait2: "Litter", tier2: 1), Counters());

            Assert.AreEqual(2, card.Query<TraitChip>().ToList().Count);
            Assert.AreEqual(1, card.Query<GenChip>().ToList().Count);
            Assert.AreEqual("G3", card.Q<GenChip>().Q<Label>("gen").text,
                "the generation chip was not replaced, so it still states the first creature's");
            StringAssert.Contains("Bramble", card.Q<Label>("name").text);
        }

        [Test]
        public void CreatureCard_StacksBodyAndBothPartsInTheSilhouetteSlot()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 2, name: "Ash", founder: false, trait1: "Carapace", tier1: 1, trait2: "Cinder", tier2: 1), Counters());
            var body = card.Q<VisualElement>("silhouette");
            var dorsal = card.Q<VisualElement>("part-dorsal");
            var flank = card.Q<VisualElement>("part-flank");
            Assert.IsNotNull(body.style.backgroundImage.value.texture, "the body sprite");
            Assert.IsNotNull(dorsal.style.backgroundImage.value.texture, "combat one at the dorsal socket");
            Assert.IsNotNull(flank.style.backgroundImage.value.texture, "combat two at the flank socket");
            // Carapace (dorsal) and Cinder (flank) are deliberately different
            // traits: a regression that bound the flank layer to Trait1 (or
            // vice versa) still produces a non-null texture here, because
            // Carapace has its own flank sprite too - only asserting WHICH
            // texture landed on WHICH layer catches a swap. Both layers get
            // that check, because "both combat traits visible on the card"
            // is the one thing this bake exists to guarantee.
            Assert.AreEqual(CreatureSprites.Part("vetch", "sk_dorsal", "carapace"), dorsal.style.backgroundImage.value.texture);
            Assert.AreEqual(CreatureSprites.Part("vetch", "sk_flank", "cinder"), flank.style.backgroundImage.value.texture);
        }

        [Test]
        public void CreatureCard_UnknownSpecies_LeavesTheSlotEmpty()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Ash", gen: 1, name: "X", founder: false, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            Assert.IsNull(card.Q<VisualElement>("silhouette").style.backgroundImage.value.texture);
            Assert.IsNull(card.Q<VisualElement>("part-dorsal").style.backgroundImage.value.texture);
            Assert.IsNull(card.Q<VisualElement>("part-flank").style.backgroundImage.value.texture);
        }

        // ---------------------------------------------------------------
        // TraitPip - screen_inventory_v2 11: "the single most-repeated
        // element in the app"
        // ---------------------------------------------------------------

        [Test]
        public void TraitPip_ReadsTierAsRomanAndNamesWhatItCounters()
        {
            var pip = new TraitPip(); pip.Bind("Chill", 3, "Courser");
            Assert.AreEqual("Chill III", pip.Q<Label>("label").text);
            StringAssert.Contains("Courser", pip.tooltip);
        }

        [Test]
        public void TraitPip_WithNullTier_IsAberrantNotZero()
        {
            // data_model 2: null is an Aberrant, never "tier 0".
            var pip = new TraitPip(); pip.Bind("Chill", null, "Courser");
            Assert.AreEqual("Chill", pip.Q<Label>("label").text);
            Assert.IsTrue(pip.ClassListContains("aberrant"));
        }

        [Test]
        public void TraitPip_UtilityTraitHasNoCounterIndicator()
        {
            // Carapace is one of bible 1.2's four traits that "counter
            // nothing by design" - a tiered, non-Aberrant trait can still
            // have nothing to show a counter indicator for.
            var pip = new TraitPip();
            pip.Bind("Carapace", 1, null);

            Assert.AreEqual("Carapace I", pip.Q<Label>("label").text);
            Assert.IsFalse(pip.ClassListContains(TraitPip.CountersUssClassName));
            Assert.IsFalse(pip.ClassListContains("aberrant"));
        }

        [Test]
        public void TraitGlyphChangesWhenAPipIsReboundAndUnknownTraitsKeepTheirNames()
        {
            var pip = new TraitPip();
            pip.Bind("Chill", 1, "Courser");
            var glyph = pip.Q<VisualElement>("glyph");
            Assert.IsTrue(glyph.ClassListContains("icon--trait-chill"));

            pip.Bind("Carapace", 2, null);
            Assert.IsFalse(glyph.ClassListContains("icon--trait-chill"));
            Assert.IsTrue(glyph.ClassListContains("icon--trait-carapace"));

            pip.Bind("Unknown", 1, null);
            Assert.IsFalse(glyph.ClassListContains("icon--trait-carapace"));
            Assert.AreEqual("Unknown I", pip.Q<Label>("label").text);
            Assert.AreEqual(DisplayStyle.None, glyph.style.display.value);

            var chip = new TraitChip("Splash", 1, "Ember");
            Assert.IsTrue(chip.Q<VisualElement>("glyph").ClassListContains("icon--trait-splash"));
            Assert.AreEqual("Splash I", chip.Q<Label>("trait").text);
        }

        // ---------------------------------------------------------------
        // ResourceBar (replaced CurrencyHeader in Phase 10 Task 1.2)
        // ---------------------------------------------------------------

        [Test]
        public void ResourceBar_RendersOnePillPerBalance_ValueNamedByKey()
        {
            var bar = new ResourceBar();
            bar.Bind(new Dictionary<string, int> { { "charges", 3 }, { "shards", 120 } });

            Assert.AreEqual("3", bar.Q<Label>("charges").text);
            Assert.AreEqual("120", bar.Q<Label>("shards").text);
            Assert.AreEqual(2, bar.Query(className: ResourceBar.PillUssClassName).ToList().Count);
        }

        [Test]
        public void ResourceBar_Rebind_ReplacesRatherThanAccumulates()
        {
            var bar = new ResourceBar();
            bar.Bind(new Dictionary<string, int> { { "charges", 3 }, { "shards", 120 }, { "tier", 4 } });
            Assert.AreEqual(3, bar.Query(className: ResourceBar.PillUssClassName).ToList().Count);

            bar.Bind(new Dictionary<string, int> { { "charges", 5 } });
            Assert.AreEqual(1, bar.Query(className: ResourceBar.PillUssClassName).ToList().Count);
            Assert.AreEqual("5", bar.Q<Label>("charges").text);
        }

        [Test]
        public void ResourceBar_OnlyShardsOfferAPurchase_AndMarksNever()
        {
            // STRUCTURE ONLY, like every other click in this file: without an
            // attached Panel a Button's click cannot be dispatched from this
            // assembly (class comment, point 3), so the "+" is pinned by
            // where it exists and where it does not, and its closure is
            // trusted the way TabBar's are.
            var bar = new ResourceBar();
            bar.Bind(new Dictionary<string, int> { { "shards", 120 }, { "marks", 2 }, { "splice_charges", 4 } });

            var buys = bar.Query<Button>(className: ResourceBar.BuyUssClassName).ToList();
            Assert.AreEqual(1, buys.Count, "exactly one balance is purchasable");
            Assert.IsNotNull(bar.Q("pill-shards").Q<Button>("buy"));
            Assert.IsNull(bar.Q("pill-marks").Q<Button>("buy"), "Marks are never purchasable (bible 8.2)");
            Assert.IsNull(bar.Q("pill-splice_charges").Q<Button>("buy"), "charges regenerate; they are not bought here");
        }

        [Test]
        public void ResourceBar_ShowsTheNextChargeTimerInsideTheChargesPill()
        {
            var bar = new ResourceBar();
            bar.Bind(new Dictionary<string, int> { { "splice_charges", 4 }, { "shards", 1 } },
                     TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(7));
            var timer = bar.Q("pill-splice_charges").Q<TimerChip>("next-charge");
            Assert.IsNotNull(timer);
            Assert.AreEqual("12:07", timer.Q<Label>("time").text);
            Assert.IsNull(bar.Q("pill-shards").Q<TimerChip>("next-charge"));
        }

        // ---------------------------------------------------------------
        // TimerChip
        // ---------------------------------------------------------------

        [Test]
        public void TimerChip_FormatsMinutesAndSeconds()
        {
            var chip = new TimerChip();
            chip.Bind(TimeSpan.FromSeconds(125));

            Assert.AreEqual("02:05", chip.Q<Label>("time").text);
            Assert.IsFalse(chip.ClassListContains(TimerChip.ExpiredUssClassName));
        }

        [Test]
        public void TimerChip_FormatsHoursOnceAnHourOrMoreRemains()
        {
            var chip = new TimerChip();
            chip.Bind(new TimeSpan(1, 2, 3));

            Assert.AreEqual("1:02:03", chip.Q<Label>("time").text);
        }

        [Test]
        public void TimerChip_AtOrBelowZero_ReadsExpiredNotNegative()
        {
            var chip = new TimerChip();
            chip.Bind(TimeSpan.FromSeconds(-5));

            Assert.AreEqual("00:00", chip.Q<Label>("time").text);
            Assert.IsTrue(chip.ClassListContains(TimerChip.ExpiredUssClassName));
        }

        // ---------------------------------------------------------------
        // ConfirmDialog - the two levels. Never suppressible either way;
        // splice_confirm_spec section 4 rejects a "don't show this again" on
        // both, which is why SpliceDialog.Suppressible has no setter this
        // assembly can reach in the first place.
        // ---------------------------------------------------------------

        [Test]
        public void NamedConfirm_HasNoDefaultButtonAndCannotBeDismissedOutside()
        {
            var vm = SpliceScreen.Build(Founder("Ash"), B(), Preview());
            var d = ConfirmDialog.Named(vm.FounderDialog, () => { }, () => { });

            Assert.IsFalse(d.Q<Button>("confirm").ClassListContains("primary"));

            // See the class comment (point 1): GetCallbackCount<T>() does
            // not exist on this Editor version, on VisualElement or
            // anywhere else. PickingMode.Ignore is what actually delivers
            // "cannot be dismissed by tapping outside" - a live Panel never
            // routes a pointer event to an Ignore element, which is a
            // stronger guarantee than "nothing happens to be registered".
            Assert.AreEqual(PickingMode.Ignore, d.Q("scrim").pickingMode);
        }

        [Test]
        public void NamedConfirm_ShowsTheFoundersNameOnBothButtons()
        {
            // bible 3.3: "Consume Ash" stops a player where "Consume Vetch
            // Crawler" does not.
            var vm = SpliceScreen.Build(Founder("Ash"), B(), Preview());
            var d = ConfirmDialog.Named(vm.FounderDialog, () => { }, () => { });

            Assert.AreEqual("Consume Ash", d.Q<Button>("confirm").text);
            Assert.AreEqual("Keep Ash", d.Q<Button>("cancel").text);
        }

        [Test]
        public void NamedConfirm_IgnoresConfirmIsPrimary_EvenWhenTheModelSaysOtherwise()
        {
            // vm.StandardDialog carries ConfirmIsPrimary = true. Feeding a
            // Standard-shaped model into .Named proves the no-destructive-
            // default guarantee belongs to the component, not to whichever
            // model a future caller happens to pass in.
            var vm = SpliceScreen.Build(A(), B(), Preview());
            var d = ConfirmDialog.Named(vm.StandardDialog, () => { }, () => { });

            Assert.IsFalse(d.Q<Button>("confirm").ClassListContains("primary"));
            Assert.AreEqual(PickingMode.Ignore, d.Q("scrim").pickingMode);
        }

        [Test]
        public void StandardConfirm_IsPrimaryAndTheScrimAcceptsPointerEvents()
        {
            // Fix round 1: this does NOT prove a tap on the scrim cancels -
            // see the class comment's point 3. It pins the structural
            // precondition (the scrim is pickable, unlike Named's
            // PickingMode.Ignore) plus the primary-button flag; the actual
            // dismiss-on-tap wiring in ConfirmDialog.Standard is trusted,
            // not dispatched.
            var vm = SpliceScreen.Build(A(), B(), Preview());
            var d = ConfirmDialog.Standard(vm.StandardDialog, () => { }, () => { });

            Assert.IsTrue(d.Q<Button>("confirm").ClassListContains("primary"));
            Assert.AreEqual(PickingMode.Position, d.Q("scrim").pickingMode);
        }

        // ---------------------------------------------------------------
        // The five shared components - design 5.2 / screen_inventory_v2 11
        //
        // No panel here either, and for the same reason the rest of this
        // file has none. `OptionRow`'s selection is read as a class change,
        // not as a dispatched click: the ctor's `onSelect` registers a
        // `ClickEvent` callback exactly the way `ConfirmDialog.Standard`
        // does, and it is trusted exactly the same way, for the reason
        // point 3 of this class's comment gives - "a handler is registered
        // and would fire" has no positive form this assembly can read back.
        // ---------------------------------------------------------------

        [Test]
        public void AnUnselectedOptionRowCarriesNeitherTheRingNorTheFill()
        {
            var row = new OptionRow("Coast road", "4h · low risk", () => { });
            Assert.IsFalse(row.ClassListContains("option-row--selected"));
            Assert.AreEqual("Coast road", row.Q<Label>("title").text);
            Assert.AreEqual("4h · low risk", row.Q<Label>("detail").text);
        }

        [Test]
        public void SelectingAnOptionRowIsAClassChangeAndNothingElse()
        {
            var row = new OptionRow("Coast road", "4h · low risk", () => { });
            row.Selected = true;
            Assert.IsTrue(row.ClassListContains("option-row--selected"),
                "the handoff's selected treatment is a 2px ring and a white fill; both hang off this class");
            row.Selected = false;
            Assert.IsFalse(row.ClassListContains("option-row--selected"));
        }

        /// The class name is not a free choice, and this is the assertion
        /// that says so. `Motion.uss` carries a `.option-row` rule -
        /// `transition-property: border-color, background-color` on
        /// `--motion-quick` - written one task before this component
        /// existed, and its own header names `option-row` as one of the four
        /// selectors that "match nothing today". Renaming this component's
        /// root class silently returns that rule to matching nothing, and
        /// the only visible symptom is that a row snaps between its two
        /// treatments instead of easing. A still frame cannot see it and
        /// this assembly has no panel to tick one, so the name is what gets
        /// pinned.
        [Test]
        public void AnOptionRowsRootClassIsTheOneMotionUssKeysItsTransitionOn()
        {
            Assert.AreEqual("option-row", OptionRow.UssClassName);
            Assert.IsTrue(new OptionRow("Coast road", "4h", () => { }).ClassListContains("option-row"));
        }

        [Test]
        public void AStatCellsValueIsANumeralAndCarriesTheNumeralClass()
        {
            var cell = new StatCell("Travel", "4h 20m");
            Assert.AreEqual("Travel", cell.Q<Label>("label").text);
            var value = cell.Q<Label>("value");
            Assert.AreEqual("4h 20m", value.text);
            Assert.IsTrue(value.ClassListContains("t-num"),
                "a stat value without .t-num escapes bible 10.6's 11px floor and the tabular face");
        }

        /// `Value` is the setter the three-cell row in the handoff's region
        /// detail card re-binds on every selection change - the whole card
        /// swaps when a region is picked. A cell that only reads its ctor
        /// argument would look right in a screenshot and be wrong in use.
        [Test]
        public void AStatCellsValueCanBeReboundWithoutRebuildingTheCell()
        {
            var cell = new StatCell("Travel", "4h 20m");
            cell.Value = "1h 05m";
            Assert.AreEqual("1h 05m", cell.Q<Label>("value").text);
            Assert.AreEqual("Travel", cell.Q<Label>("label").text);
            Assert.IsTrue(cell.Q<Label>("value").ClassListContains("t-num"));
        }

        [Test]
        public void AProgressBarClampsRatherThanOverflowing()
        {
            foreach (var (input, expected) in new[] { (0.5f, 50f), (-1f, 0f), (3f, 100f) })
            {
                var bar = new ProgressBar(input);
                Assert.AreEqual(expected, bar.Q<VisualElement>("fill").style.width.value.value, 0.01f,
                    $"fill {input} should clamp to {expected}%");
            }
        }

        /// The handoff draws this bar in five different fills depending on
        /// what it measures - teal for coverage, coral for risk, violet for
        /// growth, amber for a warning, mute for a locked row - and the
        /// modifier is how a caller picks one. No modifier means the default
        /// violet and NO extra class, so a bar that was never given one
        /// cannot be mistaken for one that was.
        [Test]
        public void AProgressBarsModifierIsAClassOnItsRootOrIsAbsent()
        {
            Assert.IsTrue(new ProgressBar(0.4f, "coral").ClassListContains("progress-bar--coral"));
            var plain = new ProgressBar(0.4f);
            Assert.IsFalse(plain.GetClasses().Any(c => c.StartsWith("progress-bar--", StringComparison.Ordinal)),
                "a bar with no modifier must carry no modifier class");
            Assert.IsTrue(plain.ClassListContains("progress-bar"));
        }

        [Test]
        public void ASectionCardWithNoHeadingDoesNotReserveOne()
        {
            Assert.IsNull(new SectionCard().Q<Label>("heading"),
                "an unheaded card reserved a heading row");
            Assert.AreEqual("Lineage", new SectionCard("Lineage").Q<Label>("heading").text);
        }

        /// THE GREY SMUDGE, MADE STRUCTURAL. `Theme.uss`'s header note 2 and
        /// the `Primitives` fixture both record the same measurement: UI
        /// Toolkit clips `background-image` to the element's own box, so a
        /// nine-sliced drop shadow put on the card it belongs to stretches
        /// its centre region across the card's interior - card centre 248/245
        /// against an unelevated 255, densest in the middle, nothing outside.
        /// It shipped once this phase and only a capture caught it.
        ///
        /// So the two halves are asserted separately: the ROOT is the
        /// wrapper and carries the elevation, and the element carrying the
        /// `--surface` fill is a DESCENDANT of it and carries no elevation
        /// class at all. Putting `elev-1` back on the surface reddens the
        /// second assertion without needing a panel, a render or an eye.
        [Test]
        public void ASectionCardWearsItsElevationOnTheWrapperAndNotOnTheSurface()
        {
            var card = new SectionCard("Lineage");

            Assert.IsTrue(card.ClassListContains("elev-1"),
                "the section card's root is the elevation wrapper - Theme.uss header note 2");

            var surface = card.Q<VisualElement>("surface");
            Assert.IsNotNull(surface, "the card's --surface fill lives on a child of the wrapper, named 'surface'");
            Assert.IsFalse(surface.ClassListContains("elev-1"),
                "elevation on the surface itself paints a grey smudge across the card - UI Toolkit clips "
                + "background-image to the element's own box, so the shadow has to be drawn by something larger");
            Assert.IsFalse(surface.ClassListContains("elev-2"));
            Assert.IsNotNull(surface.Q<VisualElement>("body"),
                "the body sits inside the surface, not beside it in the shadow's padding");
        }

        // ---------------------------------------------------------------
        // HeroBand - Phase 9 Task 14c. The gradient panel a subject is
        // looked at inside, on six screens in the handoff bundle.
        // ---------------------------------------------------------------

        /// THE ORDER IS THE WHOLE PICTURE. The ring is the surface's FIRST
        /// child and the subject its second, which in UI Toolkit is
        /// back-to-front painting order - so the ring is behind the animal
        /// and not over it. Reversed, the band would draw a dashed circle
        /// across the creature's face and nothing structural would be wrong.
        ///
        /// The pool is a CHILD of the ring rather than a sibling, because in
        /// the handoff the two never appear apart: all six `class="spin"`
        /// instances in the bundle have a filled circle inside them and none
        /// has one without. Nesting is what makes `ring: false` take both.
        [Test]
        public void AHeroBandDrawsItsRingBehindItsSubject()
        {
            var band = new HeroBand();

            var surface = band.Q<VisualElement>("surface");
            var halo = band.Q<VisualElement>("halo");
            Assert.IsNotNull(surface, "the band's fill lives on a child of the wrapper, named 'surface'");
            Assert.IsNotNull(halo, "a band built with a ring has no ring layer");

            Assert.AreEqual(0, surface.IndexOf(halo),
                "the ring is not the surface's first child, so it paints over the subject rather than behind it");
            Assert.AreEqual(1, surface.IndexOf(band.Subject),
                "the subject does not follow the ring layer");

            var ring = band.Q<VisualElement>("ring");
            Assert.IsTrue(ring.ClassListContains(HeroBand.RingUssClassName),
                "the ring lost the class that carries band-ring.png and its tint");
            Assert.AreSame(ring, band.Q<VisualElement>("glow").parent,
                "the pool is not inside the ring, so `ring: false` would leave it behind");
        }

        /// REMOVED, NOT HIDDEN - `HeroSlot`'s and `SectionCard`'s rule. Three
        /// of the six band screens draw no ring at all (`Gene Ark`,
        /// `Gene Lab`, `Splice Chamber`), so this is a first-class form and
        /// not a degenerate one. A `display: none` layer would still answer
        /// `Q("ring")` and no test could tell the two forms apart.
        [Test]
        public void AHeroBandWithNoRingRemovesTheLayerRatherThanHidingIt()
        {
            var band = new HeroBand(ring: false);

            Assert.IsNull(band.Q<VisualElement>("halo"), "the ringless band kept its ring layer");
            Assert.IsNull(band.Q<VisualElement>("ring"));
            Assert.IsNull(band.Q<VisualElement>("glow"), "the pool outlived the ring it belongs to");

            Assert.IsNotNull(band.Subject, "a ringless band lost its content slot as well");
            Assert.AreEqual(0, band.Q<VisualElement>("surface").IndexOf(band.Subject),
                "the subject did not move up when the ring layer was removed");
        }

        /// PHASE 9 TASK 14c FIX ROUND 2, NEW FINDING 2. `ring` is already
        /// the signal for "does this instance have a halo to size its
        /// subject against" - `HeroBand.uss`'s
        /// `.hero-band__subject--haloed .hero-slot` rule is what actually
        /// enlarges a `HeroSlot` placed in `Subject`, and it cannot be
        /// exercised from a panel-less tree (that is a stylesheet
        /// question, not a structure one - `TheDefaultFormStaysContent
        /// SizedInTheStylesheetItself` above is this project's answer to
        /// that class of question). What a structural test CAN and must
        /// pin is that the CLASS the CSS keys off actually tracks `ring`,
        /// so deleting the one line in the constructor that adds it goes
        /// unnoticed by nothing.
        [Test]
        public void AHeroBandsSubjectIsHaloedOnlyWhenTheRingIs()
        {
            var haloed = new HeroBand();
            Assert.IsTrue(haloed.Subject.ClassListContains(HeroBand.SubjectHaloedUssClassName),
                "a ring-bearing band's subject lost the class HeroBand.uss sizes a HeroSlot against");

            var ringless = new HeroBand(ring: false);
            Assert.IsFalse(ringless.Subject.ClassListContains(HeroBand.SubjectHaloedUssClassName),
                "a ringless band's subject was haloed - Splice Chamber's 74px/112px parent slots "
                + "would be silently enlarged to the halo size meant for a lone founder creature");
        }

        /// THE TINT HOOK - Phase 9 Task 18. `Wave Defeat.dc.html:31` is this
        /// band in coral, and the component had no way to say so.
        ///
        /// THE DEFAULT ADDS NO CLASS AT ALL, which is the half that protects
        /// the six screens written before the hook existed: a
        /// `.hero-band--violet` modifier would have had to restate
        /// `.hero-band__surface`'s own declarations, and every one of those
        /// screens would then depend on a rule nothing forced anyone to keep
        /// in step with the base one.
        [Test]
        public void AHeroBandsTintIsAModifierOnTheRootAndTheDefaultIsNoModifierAtAll()
        {
            var violet = new HeroBand();
            Assert.AreEqual(HeroBand.Tint.Violet, violet.Tinted);
            Assert.IsNull(HeroBand.TintUssClassName(HeroBand.Tint.Violet),
                "the default tint named a class; the base rule would then be shadowed by a copy");
            Assert.IsFalse(
                violet.GetClasses().Any(c => c.StartsWith(HeroBand.UssClassName + "--")),
                "a default band wears a tint modifier: " + string.Join(" ", violet.GetClasses()));

            var coral = new HeroBand(ring: false, tint: HeroBand.Tint.Coral);
            Assert.AreEqual(HeroBand.Tint.Coral, coral.Tinted);
            Assert.IsTrue(coral.ClassListContains(HeroBand.TintUssClassName(HeroBand.Tint.Coral)),
                "a coral band does not carry the class HeroBand.uss keys its ramp off");

            // THE MODIFIER IS ON THE ROOT AND NOT ON THE SURFACE, which is
            // what makes the stylesheet's descendant selector resolve. Pinned
            // because the two are one line apart in the constructor and
            // putting it on the surface would leave every assertion above
            // green while the band drew violet.
            Assert.IsFalse(
                coral.Q<VisualElement>("surface").ClassListContains(
                    HeroBand.TintUssClassName(HeroBand.Tint.Coral)),
                "the tint modifier landed on the surface; `.hero-band--coral .hero-band__surface` "
                + "is a DESCENDANT selector and would then match nothing");
        }

        /// The rule behind the class, read out of the sheet.
        ///
        /// IN A PANEL-LESS SUITE AN INLINE-STYLE ASSERTION CANNOT SEE A
        /// STYLESHEET RULE - `TheDefaultFormStaysContentSizedInTheStylesheet
        /// Itself` is the pattern and its comment carries the argument. The
        /// class-list test above proves the band ASKS for the coral ramp;
        /// nothing else in the project would notice if the rule it asks for
        /// did not exist, because a modifier that matches nothing renders as
        /// the base rule and looks exactly like a band that is meant to be
        /// violet.
        ///
        /// BOTH DECLARATIONS, BECAUSE HALF OF THIS RULE IS THE FAILURE MODE.
        /// `.hero-band__surface` sets a background-image AND a background-
        /// colour, and its own note says the colour is what the band degrades
        /// to when a texture fails to import. A tint that swapped the image
        /// and left `--violet-tint` underneath would be correct in every
        /// screenshot and wrong on the one device where the import failed.
        [Test]
        public void TheCoralTintSwapsBothTheRampAndTheFillItDegradesTo()
        {
            var path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "UI/Components/Resources/HeroBand.uss"));
            Assert.IsTrue(File.Exists(path), $"HeroBand.uss not found at {path}");

            var stripped = StripBlockComments(File.ReadAllText(path));
            var body = BlockOf(stripped, ".hero-band--coral .hero-band__surface");

            StringAssert.Contains("band-ramp-coral.png", body,
                "the coral tint does not name its own ramp, so it draws the violet one");
            StringAssert.Contains("--coral-tint-deep", body,
                "the coral tint leaves --violet-tint as the fill under its ramp - correct in every "
                + "capture and violet on any device where the texture fails to import");

            // THE FAILURE MODE, CONSTRUCTED. Both assertions above would pass
            // against the BASE rule's own body if the selector had silently
            // stopped matching what it names, so the base rule is read too and
            // must NOT be the coral one.
            var baseBody = BlockOf(stripped, ".hero-band__surface");
            StringAssert.DoesNotContain("band-ramp-coral.png", baseBody,
                "the base rule draws the coral ramp - every violet band in the bundle just turned");
        }

        /// The declarations of the first rule whose selector is exactly
        /// `selector`. `GrowsOrSizesItself` does the same match inline; this is
        /// that match with the body returned rather than tested, because two
        /// tests now want to read a block instead of asking one question of it.
        static string BlockOf(string strippedUss, string selector)
        {
            var pattern = @"(?<![\w-])" + Regex.Escape(selector) + @"(?![\w-])\s*\{([^}]*)\}";
            var m = Regex.Match(strippedUss, pattern);
            Assert.IsTrue(m.Success, $"selector `{selector}` not found in HeroBand.uss");
            return m.Groups[1].Value;
        }

        /// A band with nothing in it is a real state, not a broken one: the
        /// handoff's splice chamber opens with neither parent picked, and
        /// `Gene Ark` draws its band around a scene that has not loaded. The
        /// growth calls have to survive it too - they write to the surface,
        /// which exists whether or not anything is in it.
        [Test]
        public void AHeroBandWithNoSubjectDoesNotCrash()
        {
            var band = new HeroBand();
            Assert.AreEqual(0, band.Subject.childCount, "an unfilled band invented a child");

            Assert.DoesNotThrow(() => band.Fill(300f));
            Assert.DoesNotThrow(() => band.Fix(372f));
            Assert.DoesNotThrow(() => new HeroBand(ring: false).Fill(0f));
        }

        /// The same arrangement `ASectionCardWearsItsElevationOnTheWrapper
        /// AndNotOnTheSurface` pins, at the band's own elevation. Theme.uss
        /// header note 2: UI Toolkit clips a background image to the
        /// element's own box, so a nine-sliced drop shadow has to be drawn by
        /// something larger than the surface it falls around.
        ///
        /// `.elev-2` RATHER THAN `.elev-1` IS ASSERTED AS A VALUE, not just
        /// as "some elevation". All six band instances in the bundle are
        /// `0 4px 16px` where the cards above and below them are `0 2px 8px`,
        /// which is the 2:1 pair the two classes already map.
        [Test]
        public void AHeroBandWearsItsElevationOnTheWrapperAndNotOnTheSurface()
        {
            var band = new HeroBand();

            Assert.IsTrue(band.ClassListContains(HeroBand.UssClassName),
                "the root lost `.hero-band`, so `flex-direction: column` and `flex-shrink: 0` "
                + "no longer apply to it");
            Assert.IsTrue(band.ClassListContains("elev-2"),
                "the band's root is the elevation wrapper, at the handoff's own 0 4px 16px");
            Assert.IsFalse(band.ClassListContains("elev-1"),
                "the band is a step above the cards around it, not level with them");

            var surface = band.Q<VisualElement>("surface");
            Assert.IsFalse(surface.ClassListContains("elev-2"),
                "elevation on the surface itself paints a smudge across the band's interior");
            Assert.IsFalse(surface.ClassListContains("elev-1"));
        }

        /// THE DEFAULT FORM HAS NO CALLER IN THIS FILE UNTIL NOW, AND FOUR OF
        /// THE SIX SCREENS THE CLASS COMMENT NAMES TAKE IT: `Gene Ark`,
        /// `Gene Lab`, `Splice Chamber` and `Hybrid Growth` all construct
        /// `new HeroBand()` and call neither `Fill` nor `Fix`.
        /// `AHeroBandThatFillsGrowsItsSurfaceAndNotOnlyItsWrapper` and
        /// `AHeroBandThatIsFixedSizesItsSurfaceAndClearsTheFill` each pin
        /// what their own method writes; neither pins what a band that calls
        /// NEITHER looks like. A default `flex-grow` added to `.hero-band`
        /// or `.hero-band__surface` later would silently change four of
        /// Tasks 16-19's screens with nothing red.
        [Test]
        public void ABareHeroBandLeavesGrowthUnset()
        {
            var band = new HeroBand();
            var surface = band.Q<VisualElement>("surface");

            Assert.AreEqual(StyleKeyword.Null, band.style.flexGrow.keyword,
                "the content-sized form grew the wrapper by default - none of Gene Ark, Gene Lab, "
                + "Splice Chamber or Hybrid Growth asked this band to grow");
            Assert.AreEqual(StyleKeyword.Null, surface.style.flexGrow.keyword,
                "the content-sized form grew the surface by default");
            Assert.AreEqual(StyleKeyword.Null, surface.style.minHeight.keyword,
                "the content-sized form floored its surface's height by default");
            Assert.AreEqual(StyleKeyword.Null, surface.style.height.keyword,
                "the content-sized form fixed its surface's height by default");
        }

        /// STYLESHEET-LEVEL, AND THAT DISTINCTION IS THE WHOLE TEST.
        /// `ABareHeroBandLeavesGrowthUnset` above reads `element.style.X`,
        /// the INLINE-style accessor. A `flex-grow: 1` rule added to
        /// `.hero-band` in `HeroBand.uss` reaches an element only through
        /// the CASCADE, which is read back via `resolvedStyle` - and this
        /// suite deliberately builds no live panel to resolve one against.
        /// So the inline check above CANNOT redden for "a default
        /// `flex-grow` added to `.hero-band` later", the regression this
        /// test exists to catch; it only catches an inline default added
        /// inside the constructor. PROVEN, not assumed: the two functions
        /// below, run standalone against a copy of this file with
        /// `flex-grow: 1;` inserted into `.hero-band {}`, flip this test's
        /// verdict from green to red while leaving
        /// `ABareHeroBandLeavesGrowthUnset` green throughout - and a copy
        /// with the same text inside a COMMENT leaves both green, so
        /// documenting the rule does not trip the gate that enforces it.
        ///
        /// SOURCE-TEXT ASSERTION, ON `MainThreadAffinityTests`' PRECEDENT
        /// (`client/Assets/Game/Tests/MainThreadAffinityTests.cs:48-112`
        /// strips comments and strings from `.cs` files before matching, for
        /// exactly the reason above). `StripBlockComments` below is that
        /// idea applied to USS's one comment form. `RuleBody` then reads the
        /// two rules the default form's contract actually depends on as
        /// plain text. `.hero-band__subject` (`HeroBand.uss:200-205`)
        /// legitimately carries `flex-grow: 1` on the inner layer that
        /// fills the band once something IS in it - a different selector,
        /// so it is untouched by either `RuleBody` call below.
        [Test]
        public void TheDefaultFormStaysContentSizedInTheStylesheetItself()
        {
            var path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "UI/Components/Resources/HeroBand.uss"));
            Assert.IsTrue(File.Exists(path), $"HeroBand.uss not found at {path}");

            var stripped = StripBlockComments(File.ReadAllText(path));

            Assert.IsFalse(GrowsOrSizesItself(stripped, ".hero-band"),
                "`.hero-band` grows or sizes itself in the stylesheet - the content-sized default "
                + "form (Gene Ark, Gene Lab, Splice Chamber, Hybrid Growth) would silently change. "
                + "Growth belongs on `Fill`/`Fix`'s inline style, not the cascade.");
            Assert.IsFalse(GrowsOrSizesItself(stripped, ".hero-band__surface"),
                "`.hero-band__surface` grows or sizes itself in the stylesheet - the same "
                + "regression, on the surface instead of the wrapper.");
        }

        static readonly Regex GrowthOrSizeProperty = new Regex(
            @"(^|[{;])\s*(flex-grow|height|min-height)\s*:", RegexOptions.Multiline);

        static bool GrowsOrSizesItself(string strippedUss, string selector)
        {
            var pattern = @"(?<![\w-])" + Regex.Escape(selector) + @"(?![\w-])\s*\{([^}]*)\}";
            var m = Regex.Match(strippedUss, pattern);
            Assert.IsTrue(m.Success, $"selector `{selector}` not found in HeroBand.uss");
            return GrowthOrSizeProperty.IsMatch(m.Groups[1].Value);
        }

        /// Masks `/* ... */` to spaces (newlines kept, so a report against
        /// the original line numbers would still be possible even though
        /// this test does not need one). USS has no `//` line comments and
        /// no string type that could itself contain an unmatched brace, so
        /// this is the whole of what stripping USS needs - unlike the C#
        /// stripper it has no quote states to track.
        ///
        /// `internal` AS OF PHASE 9 TASK 19 FIX ROUND 1, and the alternative
        /// was shipping a blind spot twice. `FirstHourScreensTests` needs the
        /// same pass to assert that the codex sheet is anchored to the bottom
        /// edge and that the lineage highlight's ground is not a species chip
        /// tint - both stylesheet facts, both previously unpinned, and the
        /// bottom-anchoring defect existed precisely because nothing asserted
        /// a position. One shared pure function beats a second copy that can
        /// drift from this one; it stays here rather than moving to a new
        /// file because this is where its whole account already is.
        internal static string StripBlockComments(string src)
        {
            var outp = new StringBuilder(src.Length);
            var inComment = false;
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                var next = i + 1 < src.Length ? src[i + 1] : '\0';
                if (!inComment && c == '/' && next == '*') { inComment = true; i++; continue; }
                if (inComment && c == '*' && next == '/') { inComment = false; i++; continue; }
                outp.Append(inComment && c != '\n' ? ' ' : c);
            }
            return outp.ToString();
        }

        /// THE TASK 14b REGRESSION, PINNED. `flex-grow` on an elevation
        /// wrapper stretches the wrapper while the surface keeps its own
        /// height, so the nine-slice draws its drop around bare paper - 160px
        /// of it on the founder screen, seen by no test and caught only by
        /// the capture corpus. `SectionCard.uss`'s note beside
        /// `.section-card__surface` is the record.
        ///
        /// THIS IS WHY `Fill` WRITES INLINE STYLE RATHER THAN ADDING A CLASS.
        /// These trees have no panel, so a `resolvedStyle` read would be
        /// meaningless and a pair of USS rules would be exactly as silent as
        /// the pair that came apart. `style.flexGrow.value` is the property
        /// the method actually writes, so deleting either half reddens here.
        [Test]
        public void AHeroBandThatFillsGrowsItsSurfaceAndNotOnlyItsWrapper()
        {
            var band = new HeroBand();
            band.Fill(300f);

            var surface = band.Q<VisualElement>("surface");
            Assert.AreEqual(1f, band.style.flexGrow.value,
                "the band does not take the column's slack; the handoff's hero is `flex: 1`");
            Assert.AreEqual(1f, surface.style.flexGrow.value,
                "only the elevation wrapper grew - the surface stayed at its floor and the nine-sliced "
                + "shadow is being drawn around bare paper, which is Task 14b's defect exactly");
            Assert.AreEqual(300f, surface.style.minHeight.value.value,
                "the handoff's `min-height: 300px` landed somewhere other than the surface");
        }

        /// The other growth form - `Splice Reveal.dc.html:43` pins its band
        /// at 372 rather than growing it. The height goes on the SURFACE for
        /// the mirror of the reason above: set on the root it would size the
        /// shadow and leave the fill at whatever its content came to.
        ///
        /// THE TWO FORMS UNDO EACH OTHER, which is asserted rather than
        /// assumed. A band told to fill and then told to fix is a band a
        /// screen changed its mind about, and leaving `flex-grow` behind
        /// would make the "fixed" height a floor instead.
        [Test]
        public void AHeroBandThatIsFixedSizesItsSurfaceAndClearsTheFill()
        {
            var band = new HeroBand();
            band.Fill(300f);
            band.Fix(372f);

            var surface = band.Q<VisualElement>("surface");
            Assert.AreEqual(372f, surface.style.height.value.value,
                "the fixed height is not on the surface");
            Assert.AreEqual(StyleKeyword.Null, band.style.flexGrow.keyword,
                "a fixed band kept the wrapper's flex-grow, so it still stretches past its height");
            Assert.AreEqual(StyleKeyword.Null, surface.style.flexGrow.keyword,
                "a fixed band kept the surface's flex-grow");
            Assert.AreEqual(StyleKeyword.Null, surface.style.minHeight.keyword,
                "the old floor outlived the Fill that set it");

            // And back the other way, so neither call is the special one.
            band.Fill(300f);
            Assert.AreEqual(StyleKeyword.Null, surface.style.height.keyword,
                "the fixed height outlived the Fill that replaced it, so the band cannot grow past it");
        }

        /// THE ORNAMENT FITS THE BAND, AND THE BAND CAN BE SHORTER THAN IT -
        /// Phase 9 Task 21e. `HeroBand.OrnamentScale`'s own note has the
        /// measurement: on the iPhone 17 the exit-gate walk ran on, the
        /// founder screen's band lands near 226 points against a fixed 264px
        /// ring, and `.hero-band__surface`'s `overflow: hidden` answers that
        /// by sawing the top and bottom arcs off a dashed circle.
        ///
        /// A PURE FUNCTION IS TESTED HERE AND THE WIRING IS NOT, DELIBERATELY.
        /// These trees have no panel, so the `resolvedStyle` reads `FitOrnament`
        /// makes would every one of them return zero and this would pass on
        /// anything - which is the could-not-fail shape this phase has found
        /// three times. The arithmetic is the part a test can hold; that it is
        /// CALLED on a real band was measured with `probe-frame.sh` at
        /// 402x704 and on the device, and the report names both.
        ///
        /// EVERY ARM IS A FAILURE SOMETHING WOULD CAUSE. Dropping the
        /// `Mathf.Min(1f, ...)` reddens the tall-band arm and would blow the
        /// corpus's ring up from 264 to 456; dropping the guards reddens the
        /// unresolved arms and would write a `scale` of 0, NaN or infinity
        /// onto a band that had not been laid out yet, which paints nothing
        /// at all.
        [Test]
        public void AHeroBandsOrnamentShrinksToFitAShortBand_AndNeverGrows()
        {
            // The case the defect was measured in: a 226pt band, a 264px ring.
            Assert.AreEqual(226f / 264f, HeroBand.OrnamentScale(226f, 264f), 0.0001f,
                "a band shorter than its ring must shrink the ornament, or overflow:hidden saws "
                + "the top and bottom arcs off the dashed circle");

            // Exactly the ring: no scaling, and no floating-point creep past 1.
            Assert.AreEqual(1f, HeroBand.OrnamentScale(264f, 264f), 0.0001f);

            // The ordinary case, and the corpus's. The founder band measures
            // 456 at 430x932; the ring is an ornament in a band that grows,
            // not a thing that fills it.
            Assert.AreEqual(1f, HeroBand.OrnamentScale(456f, 264f),
                "a tall band scaled its ornament UP, which would move every committed capture");

            // Unresolved, in each of the ways a band can be. All four mean
            // "draw it at the size the stylesheet said".
            Assert.AreEqual(1f, HeroBand.OrnamentScale(0f, 264f), "a band before its first layout");
            Assert.AreEqual(1f, HeroBand.OrnamentScale(226f, 0f), "a ring whose stylesheet did not load");
            Assert.AreEqual(1f, HeroBand.OrnamentScale(float.NaN, 264f), "an unresolved band height");
            Assert.AreEqual(1f, HeroBand.OrnamentScale(226f, float.NaN), "an unresolved ring width");
        }

        /// A RINGLESS BAND HAS NOTHING TO FIT AND MUST NOT TRY. Splice
        /// Chamber's band is `ring: false` and its subject is a padded column
        /// the handoff draws at its own size (`Splice Chamber:101`); scaling
        /// that would shrink two parent cards for no reason. The halo is
        /// REMOVED rather than hidden on such a band, so a `FitOrnament` that
        /// ran anyway would dereference null - which is the failure this
        /// asserts is not reachable.
        [Test]
        public void ARinglessHeroBandIsBuiltAndResizedWithoutAnOrnament()
        {
            var band = new HeroBand(ring: false);
            Assert.IsNull(band.Q<VisualElement>("halo"), "a ringless band kept its halo");
            Assert.IsNull(band.Q<VisualElement>("ring"), "a ringless band kept its ring");

            // Fill/Fix are what a screen calls on a resized band; neither may
            // reach the fit path on this instance.
            band.Fill(300f);
            band.Fix(372f);
            Assert.AreEqual(StyleKeyword.Null, band.Subject.style.scale.keyword,
                "a ringless band scaled its subject, which is the handoff's own padded column");
        }

        [Test]
        public void AnEmptyStateSaysSomethingRatherThanRenderingNothing()
        {
            var e = new EmptyState("No creatures yet.");
            Assert.AreEqual("No creatures yet.", e.Q<Label>("message").text);
        }

        /// The glyph is `icons.uss`'s vocabulary, not a second one:
        /// `icon` supplies the 19px box, the scale mode and the `--mute-soft`
        /// tint, and `icon--<name>` supplies the raster. An empty state given
        /// no glyph drops the element rather than reserving an empty 19px
        /// square above the message - the same removed-not-hidden rule
        /// `ScreenScaffold` applies to its back chevron.
        [Test]
        public void AnEmptyStatesGlyphIsAnIconsUssMarkOrIsNotThereAtAll()
        {
            var withGlyph = new EmptyState("Nothing in the Ark yet.", "ark");
            var glyph = withGlyph.Q<VisualElement>("glyph");
            Assert.IsNotNull(glyph);
            Assert.IsTrue(glyph.ClassListContains("icon"), "the 19px box and the tint come from icons.uss");
            Assert.IsTrue(glyph.ClassListContains("icon--ark"), "the raster comes from icons.uss");

            Assert.IsNull(new EmptyState("Nothing in the Ark yet.").Q<VisualElement>("glyph"),
                "a glyphless empty state reserved an empty icon box");
        }

        // ---------------------------------------------------------------
        // NoticeToast - design 2.1. `BootController.OnNotice` used to log
        // and stop; `OutboxPump.Notices` filled a list nothing rendered.
        // This is the surface both now reach.
        // ---------------------------------------------------------------

        [Test]
        public void NoticeToast_IsHiddenUntilShown_AndStacksRows()
        {
            var toast = new NoticeToast();
            Assert.AreEqual(DisplayStyle.None, toast.style.display.value, "empty toast must not occupy the layer");

            toast.Show("Your roster could not be loaded. Try again.");
            Assert.AreEqual(DisplayStyle.Flex, toast.style.display.value);
            Assert.AreEqual(1, toast.Pending);
            StringAssert.Contains("roster", toast.Q<Label>(className: NoticeToast.RowUssClassName).text);

            toast.Show("A second sentence.");
            Assert.AreEqual(2, toast.Pending, "a second notice stacks rather than replacing the first");
        }

        [Test]
        public void NoticeToast_IgnoresEmptyText()
        {
            var toast = new NoticeToast();
            toast.Show(null);
            toast.Show("   ");
            Assert.AreEqual(0, toast.Pending);
            Assert.AreEqual(DisplayStyle.None, toast.style.display.value);
        }

        // ---------------------------------------------------------------
        // AbandonedWaveSheet - Phase 9 design 2.1 part 3. The only way out
        // of a roster committed to a wave nobody will submit.
        // ---------------------------------------------------------------

        [Test]
        public void AbandonedWaveSheet_StatesTheSituationAndOffersOnlyForfeit()
        {
            var sheet = new AbandonedWaveSheet(onForfeit: () => { });
            Assert.AreEqual(AbandonedWaveSheet.Headline, sheet.Q<Label>("headline").text);
            Assert.AreEqual(AbandonedWaveSheet.Body, sheet.Q<Label>("body").text);
            var buttons = sheet.Query<Button>().ToList();
            Assert.AreEqual(1, buttons.Count, "one way out, and it is forfeit");
            Assert.AreEqual(AbandonedWaveSheet.ForfeitLabel, buttons[0].text);
            Assert.IsTrue(buttons[0].ClassListContains("btn-primary"));
            Assert.IsNull(sheet.Q<VisualElement>(className: ScreenScaffold.UssClassName), "a sheet takes no scaffold");
        }

        /// Regression for the bug `ASectionCardWearsItsElevationOnTheWrapperAndNotOnTheSurface`
        /// already pins once in this file: `elev-2` and the `--surface` fill
        /// must not land on the same element, or UI Toolkit's nine-slice
        /// paints a grey smudge across the card's interior instead of an
        /// edge shadow (Theme.uss header note 2; SectionCard.cs's class
        /// comment records this shipping once already this phase).
        [Test]
        public void AnAbandonedWaveSheetWearsItsElevationOnTheWrapperAndNotOnTheSurface()
        {
            var sheet = new AbandonedWaveSheet(onForfeit: () => { });

            var card = sheet.Q<VisualElement>("card");
            Assert.IsNotNull(card);
            Assert.IsTrue(card.ClassListContains("elev-2"),
                "the sheet's card is the elevation wrapper - Theme.uss header note 2");

            var surface = card.Q<VisualElement>("surface");
            Assert.IsNotNull(surface, "the card's --surface fill lives on a child of the wrapper, named 'surface'");
            Assert.IsFalse(surface.ClassListContains("elev-1"));
            Assert.IsFalse(surface.ClassListContains("elev-2"),
                "elevation on the surface itself paints a grey smudge across the card - UI Toolkit clips "
                + "background-image to the element's own box, so the shadow has to be drawn by something larger");
            Assert.IsNotNull(surface.Q<Label>("headline"), "the content sits inside the surface, not beside it in the shadow's padding");
        }

        // ---------------------------------------------------------------
        // CreatureStage - Phase 9 design 3.8. Shows a Texture and nothing
        // more; the portrait studio (Broodline.Game) is the only thing in
        // production that ever hands it one, which is what keeps this
        // assembly free of Broodline.Creatures.
        // ---------------------------------------------------------------

        [Test]
        public void CreatureStage_ShowsATexture_AndClearsOnNull()
        {
            var stage = new CreatureStage();
            var tex = new Texture2D(4, 4);
            stage.SetTexture(tex);
            Assert.AreEqual(tex, stage.Q<VisualElement>("frame").style.backgroundImage.value.texture);
            stage.SetTexture(null);
            Assert.IsNull(stage.Q<VisualElement>("frame").style.backgroundImage.value.texture);

            // `using System;` above makes the bare `Object` ambiguous between
            // System.Object and UnityEngine.Object in this file - qualified
            // here rather than adding another file-wide alias.
            UnityEngine.Object.DestroyImmediate(tex);
        }

        // ===============================================================
        // THE COMPONENT VOCABULARY - Phase 9 design 4.1. Nine parts the
        // handoff's screens are actually made of, built once so the six
        // screen tasks after this one compose rather than re-derive.
        //
        // EVERY ONE OF THESE IS A STRUCTURE-AND-TEXT TEST, on the rule this
        // file's class comment sets out: a plain `VisualElement` tree, no
        // scene, no live `Panel`, no dispatched clicks. What a still tree
        // can prove is what is asserted - which children exist, what they
        // say, which classes they carry. What it cannot prove (that a tap
        // fires, that a colour resolves) is left to the capture corpus and
        // said so at the site.
        // ===============================================================

        [Test]
        public void GenChip_ShowsGWithTheGeneration_OnTNum()
        {
            var chip = new GenChip(4);

            Assert.AreEqual("G4", chip.Q<Label>().text,
                "the generation badge is written by CreatureLabel.Generation, so it cannot read 'Gen 4' here "
                + "and 'G4' on a card");
            Assert.IsTrue(chip.Q<Label>().ClassListContains("t-num"),
                "a generation is a number a decision depends on - bible 10.6's tabular face and 11px floor "
                + "are both enforced through this marker and nothing else");
            Assert.IsTrue(chip.ClassListContains("chip"),
                "the chip shape comes from Theme.uss rather than from a second copy of it");
        }

        [Test]
        public void TraitChip_IsTintedByTheSpecies_AndMarksAberrant()
        {
            // AN ABSENCE IS NOT AN ABERRANT, AND THE CHIP USED TO SAY IT WAS
            // - Phase 9 Task 21e. `EnableInClassList(aberrant, tier == null)`
            // is true of every empty slot, because an empty slot arrives as
            // `"None"` with a null tier. No screen builds such a chip any
            // more, so this is the guard that keeps a direct caller from
            // re-creating the defect rather than a path in use.
            Assert.IsFalse(new TraitChip("None", null, "Hollow")
                    .ClassListContains(TraitChip.AberrantUssClassName),
                "an empty combat slot was wearing the rare-trait outline");

            var vetch = new TraitChip("Carapace", 3, "Vetch");
            Assert.IsTrue(vetch.ClassListContains("trait-chip--vetch"),
                "the species modifier is what picks the tint; without it every chip is the default violet");
            StringAssert.Contains("Carapace", vetch.Q<Label>().text);
            StringAssert.Contains("III", vetch.Q<Label>().text,
                "tiers are written I-III by CreatureLabel.TraitWithTier, not as digits");

            // data_model 2: a null coverage tier IS an Aberrant, and null and
            // zero must never be conflated - "zero would sort and display as
            // 'less than tier I'".
            var aberrant = new TraitChip("Cinder", null, "Ember");
            Assert.IsTrue(aberrant.ClassListContains("aberrant"),
                "an Aberrant carries no tier, so the outline is the only thing that distinguishes it from "
                + "an untiered Instinct");
            Assert.AreEqual("Cinder", aberrant.Q<Label>().text,
                "a null tier must not render as a '0' or as a stray space");
        }

        /// THE CHIP'S BOX IS PINNED IN THE SHEET, BECAUSE NOTHING ELSE IN
        /// THIS SUITE CAN SEE IT.
        /// `TheDefaultFormStaysContentSizedInTheStylesheetItself` is the
        /// pattern and its comment carries the full argument: a rule in a
        /// stylesheet reaches an element only through a live panel's
        /// cascade, this suite deliberately builds no panel, and
        /// `chip.style.height` is the INLINE accessor - it reads `auto`
        /// however Theme.uss is written.
        ///
        /// WHAT IT CATCHES, MEASURED. Task 16 shipped `.chip` as 4px of
        /// vertical padding with no height, and the two components that wear
        /// it as a CONTAINER rendered 37-38px against the handoff's 15-19,
        /// because the Label INSIDE a chip measures two line boxes where the
        /// same Label as the chip itself measures one. The height is what
        /// contains that to the chip. Taking it out again - or putting the
        /// vertical padding back beside it, which is border-box and so would
        /// only shrink the box the label centres in - returns four captured
        /// screens to 37px with every gate in this repo still green and only
        /// the capture corpus able to say so.
        ///
        /// THE RANGE IS THE HANDOFF'S OWN, NOT A TOLERANCE.
        /// `Creature Roster.dc.html:56` is 15 and `Splice Chamber
        /// .dc.html:67` is 19; 20 is that 19 with our 10px micro type in
        /// place of its 9.5. Anything above 20 is the defect returning by
        /// degrees.
        [Test]
        public void TheChipPinsItsOwnHeightInTheStylesheetItself()
        {
            var theme = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "UI/Shell/Theme.uss"));
            var tokens = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "UI/Shell/Tokens.uss"));
            Assert.IsTrue(File.Exists(theme), $"Theme.uss not found at {theme}");
            Assert.IsTrue(File.Exists(tokens), $"Tokens.uss not found at {tokens}");

            var rule = Regex.Match(
                StripBlockComments(File.ReadAllText(theme)),
                @"(?<![\w-])\.chip(?![\w-])\s*\{([^}]*)\}");
            Assert.IsTrue(rule.Success, "`.chip` is gone from Theme.uss");
            var body = rule.Groups[1].Value;

            StringAssert.Contains("height: var(--chip-height)", body,
                "`.chip` no longer sets its own height, so every chip is as tall as the Label "
                + "inside it again - 37-38px against the handoff's 15-19");
            StringAssert.Contains("-unity-text-align: middle-center", body,
                "without the inherited middle alignment a label sits at the top of the box the "
                + "height just fixed, which looks worse than the overrun did");
            Assert.IsFalse(Regex.IsMatch(body, @"padding-(top|bottom)\s*:"),
                "`height` in UI Toolkit is border-box, so vertical padding on `.chip` decides "
                + "nothing but the content box the label centres in - it was removed when the "
                + "height arrived and reads as the old sizing model if it comes back");

            var h = Regex.Match(StripBlockComments(File.ReadAllText(tokens)),
                @"--chip-height\s*:\s*(\d+)px\s*;");
            Assert.IsTrue(h.Success, "--chip-height is gone from Tokens.uss");
            // 22, NOT 20, SINCE PHASE 10 TASK 1.1: --text-micro moved from 10px
            // to 11px (the type scale's floor for a label), and Nunito-Bold's
            // 1.36em line box on 11px is 15, so the handoff's own 3px padding
            // gives 21 -> 22. The ceiling below is that arithmetic, not a
            // preference; Tokens.uss's --chip-height note carries it.
            Assert.That(int.Parse(h.Groups[1].Value), Is.InRange(15, 22),
                "the handoff's chips measure 15px (Creature Roster) to 19px (Splice Chamber); "
                + "20 is the 19 carried onto our 10px micro type and is the ceiling");
        }

        [Test]
        public void HeroSlot_StacksThreeLayers_AndClearsOnNull()
        {
            var slot = new HeroSlot();
            slot.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true,
                trait1: "Carapace", tier1: 1, trait2: "Taunt", tier2: 1));

            Assert.IsNotNull(slot.Q<VisualElement>("body").style.backgroundImage.value.texture,
                "the body layer has no sprite - CreatureSprites.Body returned null for 'Vetch', which is "
                + "the one species baked before Task 15");
            Assert.IsNotNull(slot.Q<VisualElement>("ring"),
                "no ring: USS has no dashed border, so the ring is a texture element and not a border");
            Assert.IsTrue(slot.ClassListContains("hero-slot--vetch"),
                "the ring and disc take the species tint, which is what the handoff draws");

            slot.Bind(null);
            Assert.IsNull(slot.Q<VisualElement>("body").style.backgroundImage.value.texture,
                "an unbound slot still showed the last creature - the splice chamber opens with no parent "
                + "picked and would open showing one");
            Assert.IsFalse(slot.ClassListContains("hero-slot--vetch"),
                "clearing left the previous species tint on the ring");
        }

        [Test]
        public void HeroSlot_FramesALivePortraitInsteadOfSprites()
        {
            var stage = new CreatureStage();
            var slot = new HeroSlot(stage);

            Assert.IsTrue(slot.ClassListContains(HeroSlot.LiveUssClassName));
            Assert.IsNotNull(slot.Q<VisualElement>("ring"), "a live slot keeps the ring - that is the point");
            Assert.AreSame(stage, slot.Q<CreatureStage>(), "the stage handed in is not the one in the tree");
            Assert.IsNull(slot.Q<VisualElement>("body"),
                "a live slot must not also answer to 'body': the screen tests tell the two forms apart by "
                + "exactly that question");
        }

        [Test]
        public void InheritanceBar_ShowsOddsOnTNum_AndTheTag()
        {
            var bar = new InheritanceBar("Carapace III", 0.78f, "DOM", "teal");

            Assert.AreEqual("78%", bar.Q<Label>("odds").text);
            Assert.IsTrue(bar.Q<Label>("odds").ClassListContains("t-num"),
                "three odds are read down a column against each other, and Baloo 2's ten digit widths do "
                + "not line up - Theme.uss's header has the measurement");
            Assert.AreEqual("DOM", bar.Q<Label>("tag").text);
            Assert.IsNotNull(bar.Q<VisualElement>(className: "progress-bar--teal"),
                "the bar takes the same colour family as the dot and the tag, from one argument");
        }

        [Test]
        public void MutationBanner_CollapsesWhenEmpty()
        {
            var banner = new MutationBanner();
            Assert.AreEqual(DisplayStyle.None, banner.style.display.value,
                "a banner with nothing to say still occupied a row; the server decides whether there is a "
                + "mutation window and a screen should not have to branch on it");

            banner.Text = "Mutation window open";
            Assert.AreEqual(DisplayStyle.Flex, banner.style.display.value);
            Assert.AreEqual("Mutation window open", banner.Q<Label>("text").text);
            Assert.IsNotNull(banner.Q<VisualElement>(className: "icon--sparkle"),
                "the sparkle is what makes this amber strip read as the rare good thing rather than as "
                + "another warning panel");
        }

        [Test]
        public void LineageStrip_DrawsOneNodePerGeneration_AndMarksTheCurrent()
        {
            var strip = new LineageStrip();
            strip.Bind(new[] { (1, "vetch", false), (4, "vetch", false), (7, "vetch", true) },
                "Unbroken Vetch line since G1");

            Assert.AreEqual(3, strip.Query<VisualElement>(className: LineageStrip.NodeUssClassName).ToList().Count);
            Assert.AreEqual(1, strip.Query<VisualElement>(className: LineageStrip.CurrentUssClassName).ToList().Count,
                "exactly one generation is the current one; a rebuilt strip that kept a stale mark would "
                + "put it on the wrong animal");
            Assert.AreEqual("G7", strip.Query<Label>(className: LineageStrip.GenUssClassName).ToList().Last().text);
            Assert.AreEqual("Unbroken Vetch line since G1", strip.Q<Label>("note").text);
        }

        [Test]
        public void LineageStrip_CollapsesWithNoChain()
        {
            var strip = new LineageStrip();
            Assert.AreEqual(DisplayStyle.None, strip.style.display.value,
                "a rail with no nodes on it is a horizontal line across a card, which reads as a divider "
                + "somebody forgot to delete");

            strip.Bind(new[] { (1, "vetch", true) }, null);
            Assert.AreEqual(DisplayStyle.Flex, strip.style.display.value);
            Assert.AreEqual(DisplayStyle.None, strip.Q<Label>("note").style.display.value,
                "no pedigree note was sent, so no row is reserved for one");
        }

        [Test]
        public void CostCtaRow_ShowsCostOnTNum_AndOnePrimaryButton()
        {
            var row = new CostCtaRow("icon--charge", "2", "Begin Splice", () => { });

            Assert.AreEqual("2", row.Q<Label>("cost").text);
            Assert.IsTrue(row.Q<Label>("cost").ClassListContains("t-num"),
                "the cost is the number the decision is about");
            Assert.AreEqual("Begin Splice", row.Q<Button>().text);
            Assert.IsTrue(row.Q<Button>().ClassListContains("btn-primary"),
                "the commit action is the screen's primary CTA and carries the handoff's gradient and press");
            Assert.AreEqual(CostCtaRow.CostEyebrow, row.Q<Label>("eyebrow").text);
            Assert.IsNotNull(row.Q<VisualElement>(className: "icon--charge"),
                "the cost glyph says WHICH currency; a bare '2' does not");

            row.Enabled = false;
            Assert.IsFalse(row.Q<Button>().enabledSelf,
                "a row that cannot be afforded must disable its own button rather than leave the screen to");
        }

        [Test]
        public void FieldSlotRow_StatesTheLetterAndTheOccupant()
        {
            var empty = new FieldSlotRow("B", "Empty", filled: false, onTap: () => { });
            Assert.AreEqual("B", empty.Q<Label>("letter").text);
            Assert.AreEqual("Empty", empty.Q<Label>("label").text);
            Assert.IsFalse(empty.ClassListContains(FieldSlotRow.FilledUssClassName));

            var filled = new FieldSlotRow("A", "Vetch Wall R2", filled: true, onTap: () => { });
            Assert.IsTrue(filled.ClassListContains(FieldSlotRow.FilledUssClassName));

            filled.Selected = true;
            Assert.IsTrue(filled.ClassListContains(FieldSlotRow.SelectedUssClassName));
            filled.Selected = false;
            Assert.IsFalse(filled.ClassListContains(FieldSlotRow.SelectedUssClassName),
                "selection reads and writes the class list directly, so there is no second copy of the "
                + "state to drift from what is rendered");
        }

        [Test]
        public void LanePreviewCard_ShowsATextureAndFourSlotTags()
        {
            var card = new LanePreviewCard();
            var tex = new Texture2D(4, 4);
            card.SetTexture(tex);
            Assert.AreEqual(tex, card.Q<VisualElement>("lane").style.backgroundImage.value.texture);

            card.SetSlots(new[] { ("A", true), ("B", false), ("C", false), ("D", true) });
            var tags = card.Query<Label>(className: LanePreviewCard.SlotUssClassName).ToList();
            Assert.AreEqual(4, tags.Count);
            Assert.AreEqual("A", tags[0].text);
            Assert.IsTrue(tags[0].ClassListContains(LanePreviewCard.SlotFilledUssClassName));
            Assert.IsFalse(tags[1].ClassListContains(LanePreviewCard.SlotFilledUssClassName));

            // A SHORTER WAVE MUST NOT LEAVE A POCKET BEHIND. The strip is
            // rebuilt rather than recycled for exactly this - a leftover tag
            // would be a pocket the player cannot fill.
            card.SetSlots(new[] { ("A", false) });
            Assert.AreEqual(1, card.Query<Label>(className: LanePreviewCard.SlotUssClassName).ToList().Count);

            card.SetTexture(null);
            Assert.IsNull(card.Q<VisualElement>("lane").style.backgroundImage.value.texture,
                "the card showed the previous wave's lane after being cleared");

            UnityEngine.Object.DestroyImmediate(tex);
        }

        [Test]
        public void StatCell_ShowsATrendArrow()
        {
            var up = new StatCell("Armor", "B+", StatCell.Trend.Up);
            Assert.IsNotNull(up.Q<VisualElement>(className: "icon--trend-up"));

            var down = new StatCell("Speed", "C", StatCell.Trend.Down);
            Assert.IsNotNull(down.Q<VisualElement>(className: "icon--trend-down"));

            // REMOVED, NOT HIDDEN, and the default is what the twelve
            // existing two-argument call sites get - see StatCell.Trend.
            var flat = new StatCell("Travel", "4h 20m");
            Assert.IsNull(flat.Q<VisualElement>("trend"),
                "a cell with no trend reserved a node for an arrow it will never draw");
        }
    }
}
