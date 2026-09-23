using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// "G4" in a chip - the generation badge the handoff puts in the corner
    /// of every creature surface it has: both parent cards and the predicted
    /// hybrid panel in `Splice Chamber.dc.html`, every card in
    /// `Creature Roster.dc.html`, the hero card on `Splice Reveal`.
    ///
    /// IT EXISTS FOR THE `t-num` ON ITS LABEL, the same way `StatCell` does.
    /// A generation is a number a decision depends on - it is the whole
    /// depth axis of a breeding game, and the roster filters on it - so
    /// bible 10.6's tabular face and 11px floor apply to it, and the marker
    /// is the only thing either is enforceable through. `CreatureCard` has
    /// carried a bare `--text-secondary` generation label for two phases for
    /// exactly this reason; Task 16 replaces it with one of these.
    ///
    /// THE TEXT COMES FROM `CreatureLabel.Generation`, NOT FROM A FORMAT
    /// STRING HERE. That method exists so "G4" and "Gen 4" cannot end up on
    /// two different screens, and a second place writing `"G" + n` is
    /// precisely how that drifts.
    ///
    /// SPECIES-AGNOSTIC, AND THE HANDOFF IS NOT. Its parent-card gen chips
    /// are tinted by the parent's species (teal `G4` for a Vetch, coral `G6`
    /// for an Ember) while the predicted hybrid's `Gen 7` is violet. This
    /// component is the violet one, because the interface the phase fixed
    /// takes a generation and nothing else. A screen that wants the tinted
    /// form has `TraitChip`'s six species rules beside it; adding a second
    /// copy of that vocabulary here to serve one card was not worth it.
    public sealed class GenChip : VisualElement
    {
        public const string UssClassName = "gen-chip";

        /// Theme.uss's shared chip shape - fill, radius, padding. Named here
        /// rather than typed as a literal so the coupling is visible from
        /// both ends, on `SectionCard.ElevationUssClassName`'s convention.
        public const string ChipUssClassName = "chip";

        public GenChip(int generation)
        {
            AddToClassList(ChipUssClassName);
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("GenChip").CloneTree(this);

            this.Q<Label>("gen").text = CreatureLabel.Generation(generation);
        }
    }
}
