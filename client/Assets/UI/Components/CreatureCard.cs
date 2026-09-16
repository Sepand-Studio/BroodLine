using System;
using System.Collections.Generic;
using System.Globalization;
using Broodline.Api;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// screen_inventory_v2 11: "the atom of the interface" - it appears on at
    /// least nine screens, and is worth disproportionate effort here because
    /// every one of those screens just binds it rather than rebuilding it.
    ///
    /// screen_inventory_v2 3's rework note is explicit: TWO trait pips, not
    /// four. This card owns exactly two `TraitPip` children for the whole of
    /// its lifetime - constructed once, re-bound on every `Bind` - so no
    /// amount of re-binding (a recycled row in a roster list, for instance)
    /// can accumulate a third or a fourth.
    [UxmlElement]
    public partial class CreatureCard : VisualElement
    {
        public const string UssClassName = "creature-card";
        public const string FounderUssClassName = "founder";
        public const string AberrantUssClassName = "aberrant";
        public const string CommittedUssClassName = "committed";

        readonly VisualElement _silhouette;
        readonly Label _name;
        readonly Label _generation;
        readonly Label _instinct;
        readonly TraitPip _pip1;
        readonly TraitPip _pip2;

        public CreatureCard()
        {
            AddToClassList(UssClassName);

            var tree = Resources.Load<VisualTreeAsset>("CreatureCard");
            tree.CloneTree(this);

            _silhouette = this.Q<VisualElement>("silhouette");
            _name = this.Q<Label>("name");
            _generation = this.Q<Label>("generation");
            _instinct = this.Q<Label>("instinct");

            var pips = this.Q<VisualElement>("pips");
            _pip1 = new TraitPip();
            _pip2 = new TraitPip();
            pips.Add(_pip1);
            pips.Add(_pip2);
        }

        /// `counters` maps a trait name to the single raider/name it answers
        /// (bible 1.2's "bold traits are counters") - empty or missing for
        /// one of the four traits that counter nothing by design. It is not
        /// re-derived here; Broodline.UI does not reference Broodline.Sim and
        /// has no ruleset of its own to derive it from.
        public void Bind(CreatureDto creature, IReadOnlyDictionary<string, string> counters)
        {
            if (creature == null) throw new ArgumentNullException(nameof(creature));
            counters = counters ?? new Dictionary<string, string>();

            _name.text = CreatureLabel.DisplayName(creature);
            _generation.text = "G" + creature.Generation.ToString(CultureInfo.InvariantCulture);
            _instinct.text = creature.Instinct;
            _silhouette.tooltip = creature.Species;

            EnableInClassList(FounderUssClassName, creature.IsFounder);
            EnableInClassList(CommittedUssClassName, creature.CommittedTo.HasValue);

            // data_model 2: a TraitInstance's coverage_tier is null exactly
            // when it holds an Aberrant. Either combat slot can hold one.
            EnableInClassList(AberrantUssClassName, creature.Tier1 == null || creature.Tier2 == null);

            _pip1.Bind(creature.Trait1, creature.Tier1, CounterFor(creature.Trait1, counters));
            _pip2.Bind(creature.Trait2, creature.Tier2, CounterFor(creature.Trait2, counters));
        }

        static string CounterFor(string trait, IReadOnlyDictionary<string, string> counters)
        {
            if (trait == null) return null;
            return counters.TryGetValue(trait, out var counteredName) ? counteredName : null;
        }
    }
}
