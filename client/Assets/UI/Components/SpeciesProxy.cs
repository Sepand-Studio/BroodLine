using System;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A species name to the USS classes that draw its interim proxy.
    ///
    /// The proxies are placeholder art AND a pre-test of bible 10.2 rule 1 -
    /// "All six species must be distinguishable as flat black shapes at 40px;
    /// if two are confusable, one is wrong." `SilhouetteTests` asserts that
    /// rule against these six PNGs, which is why the art is drawn from bible
    /// 1.2's silhouette column verbatim rather than invented: a collision
    /// between two of THESE is a collision in the design, and
    /// `broodline_rig_proof.md` section 6 routes that back to the bible.
    ///
    /// EXACT MATCH ON THE SIX, AND NOTHING CLEVER. `broodline_data_model.md`
    /// section 2 says `species` is "one of six", so a value that is not one
    /// of six is a data defect and this class says so by drawing nothing -
    /// the slot falls back to the empty `--surface-sunk` paint it carried
    /// before this task. It deliberately does NOT tokenise, prefix-match or
    /// otherwise guess, for two reasons:
    ///
    ///  1. `Broodline.UI` has no ruleset of its own. `CreatureCard.Bind`'s
    ///     own comment refuses to re-derive a trait's counter here for the
    ///     same reason; inventing a species-name parser in the view layer
    ///     would be a rule about data the server owns.
    ///  2. The guess would be wrong. `ScreenFixtures` carried "Ember
    ///     Skitter", which contains TWO species names - Ember and Skitter are
    ///     both in the six - so any tie-break is arbitrary and would put one
    ///     species' colour on another species' card silently. (Those fixture
    ///     strings were corrected to canonical species by Phase 8 Task 13;
    ///     `Broodline.UI.Tests` still carries the compound form, which is
    ///     harmless there because no test reads it as a species.)
    ///
    /// Case-insensitive because a hex-cased wire value is a formatting
    /// difference and not a different species; ordinal-ignore-case rather
    /// than the current culture, so a Turkish locale cannot decide that
    /// "SKITTER" is not "skitter".
    public static class SpeciesProxy
    {
        /// The base class. Carries the scale mode and nothing else - every
        /// colour comes from the modifier below, i.e. from the token layer.
        public const string UssClassName = "proxy";

        /// The six, lowercase, in bible 1.2's table order. These ARE the
        /// modifier suffixes and the PNG basenames; `SilhouetteTests` walks
        /// the same six.
        public static readonly string[] Names =
            { "vetch", "ember", "skitter", "hollow", "loam", "pale" };

        /// "proxy--vetch", or null when `species` is not one of the six.
        public static string ModifierFor(string species)
        {
            if (string.IsNullOrWhiteSpace(species)) return null;
            var trimmed = species.Trim();
            foreach (var name in Names)
                if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
                    return UssClassName + "--" + name;
            return null;
        }

        /// "proxy proxy--vetch", or null when `species` is not one of the six.
        ///
        /// SPACE-SEPARATED, AND THAT IS NOT AN `AddToClassList` ARGUMENT.
        /// UI Toolkit treats a class name as an opaque string, so passing
        /// this whole value to `AddToClassList` creates one class literally
        /// named "proxy proxy--vetch" that no selector can ever match, with
        /// no error. It is the form a UXML `class=` attribute takes and the
        /// form a reader wants; `Apply` below is what an element should use.
        public static string UssClassFor(string species)
        {
            var modifier = ModifierFor(species);
            return modifier == null ? null : UssClassName + " " + modifier;
        }

        /// Put `species`' proxy on `element`, replacing whatever was there.
        ///
        /// CLEARS FIRST, ALWAYS. `CreatureCard` owns its children for its
        /// whole lifetime and re-binds them - a recycled row in a roster list
        /// is the case its class comment is written against - so an add-only
        /// version would leave a Vetch wearing Vetch's background under
        /// Skitter's tint the first time a card was reused. An unknown
        /// species clears too, which is what makes "draw nothing" an actual
        /// state rather than "keep the last creature's art".
        public static void Apply(VisualElement element, string species)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));

            element.RemoveFromClassList(UssClassName);
            foreach (var name in Names)
                element.RemoveFromClassList(UssClassName + "--" + name);

            var modifier = ModifierFor(species);
            if (modifier == null) return;

            element.AddToClassList(UssClassName);
            element.AddToClassList(modifier);
        }
    }
}
