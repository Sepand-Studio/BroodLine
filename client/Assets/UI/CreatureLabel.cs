using System.Globalization;
using Broodline.Api;

namespace Broodline.UI
{
    /// How a creature and a coverage tier are WRITTEN. One place, because
    /// splice_confirm_spec section 3 requires the destruction notice, the
    /// confirm dialog and the reveal screen to use the same language - "three
    /// different phrasings of the same fact reads as evasion" - and three call
    /// sites formatting a name independently is exactly how that drifts.
    ///
    /// Formatting only. Nothing here decides anything: no liveness, no odds,
    /// no coverage. Those are the server's and arrive already decided.
    public static class CreatureLabel
    {
        /// The player's chosen name where one exists, and the species
        /// otherwise. splice_confirm_spec section 3 makes the name half
        /// mandatory; the species fallback is what keeps the notice naming an
        /// ACTUAL creature rather than "the selected parents" when there is no
        /// name to use.
        ///
        /// Today a name only ever appears on a Founder - drizzle 0005's
        /// `only_founders_named` refuses a named non-Founder - but the rule is
        /// written against the FIELD, not against Founder-ness, so relaxing
        /// that constraint later needs no change here.
        public static string DisplayName(CreatureDto creature)
        {
            if (creature == null) return string.Empty;
            return string.IsNullOrWhiteSpace(creature.Name) ? creature.Species : creature.Name;
        }

        /// "G4" - the generation badge on its own, e.g. `CreatureCard`'s
        /// standalone generation label. Centralized for the same reason as
        /// every other method here: two call sites formatting a generation
        /// independently is exactly how "G4" and "Gen 4" end up on different
        /// screens.
        public static string Generation(int generation)
        {
            return "G" + generation.ToString(CultureInfo.InvariantCulture);
        }

        /// "Ash (G4)". The generation belongs beside the name in the
        /// destruction notice - splice_confirm_spec section 3's own example
        /// carries it, and it is what distinguishes two creatures of a species
        /// the player owns several of.
        public static string WithGeneration(CreatureDto creature)
        {
            if (creature == null) return string.Empty;
            return DisplayName(creature) + " (" + Generation(creature.Generation) + ")";
        }

        /// Coverage tiers are written I-III (splice_confirm_spec section 2,
        /// reconciliation constraint 9). A tier outside that range cannot be
        /// authored today; it falls back to the digits rather than to an empty
        /// string, because a coverage warning that silently loses its tier is
        /// the cost being hidden again.
        public static string Tier(int? tier)
        {
            if (tier == null) return string.Empty;
            switch (tier.Value)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return tier.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// "Chill III", or just "Chill" for an untiered trait. Instincts are
        /// untiered per reconciliation 1.7, and `tier` is nullable on the wire
        /// for exactly that reason.
        public static string TraitWithTier(string trait, int? tier)
        {
            var roman = Tier(tier);
            return roman.Length == 0 ? trait : trait + " " + roman;
        }
    }
}
