using System;
using System.Collections.Generic;
using System.Linq;
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

        [Test]
        public void CreatureCard_ShowsTwoPipsWithCoverageAndTheFounderName()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            Assert.AreEqual(2, card.Query<TraitPip>().ToList().Count);           // two, not four - screen_inventory 3
            StringAssert.Contains("Ash", card.Q<Label>("name").text);
            Assert.IsTrue(card.ClassListContains("founder"));

            // CreatureLabel.Generation is the one place this format is
            // written (fix round 1: it used to be inlined here too).
            Assert.AreEqual("G1", card.Q<Label>("generation").text);
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
        public void CreatureCard_Rebind_ReplacesRatherThanAccumulatesPips()
        {
            // The card owns exactly two TraitPip children for its whole
            // lifetime (constructed once, re-bound per Bind) - a recycled
            // card in a roster list must never grow a third or fourth pip.
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            card.Bind(Creature("Skitter", gen: 1, name: "Bramble", founder: true, trait1: "Sprint", tier1: 2, trait2: "Litter", tier2: 1), Counters());

            Assert.AreEqual(2, card.Query<TraitPip>().ToList().Count);
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

        // ---------------------------------------------------------------
        // CurrencyHeader
        // ---------------------------------------------------------------

        [Test]
        public void CurrencyHeader_RendersOneLabelPerBalance()
        {
            var header = new CurrencyHeader();
            header.Bind(new Dictionary<string, int> { { "charges", 3 }, { "shards", 120 } });

            StringAssert.Contains("3", header.Q<Label>("charges").text);
            StringAssert.Contains("120", header.Q<Label>("shards").text);
            Assert.AreEqual(2, header.Query<Label>().ToList().Count);
        }

        [Test]
        public void CurrencyHeader_Rebind_ReplacesRatherThanAccumulates()
        {
            var header = new CurrencyHeader();
            header.Bind(new Dictionary<string, int> { { "charges", 3 }, { "shards", 120 }, { "tier", 4 } });
            Assert.AreEqual(3, header.Query<Label>().ToList().Count);

            header.Bind(new Dictionary<string, int> { { "charges", 5 } });
            Assert.AreEqual(1, header.Query<Label>().ToList().Count);
            StringAssert.Contains("5", header.Q<Label>("charges").text);
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
