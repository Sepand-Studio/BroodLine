using System;
using System.Collections.Generic;
using System.Linq;
using Broodline.Api;
using Broodline.UI.Components;
using NUnit.Framework;
using UnityEngine.UIElements;

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
    }
}
