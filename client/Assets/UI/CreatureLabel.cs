using System;
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
        ///
        /// IT DOES NOT SCREEN FOR AN ABSENCE AND MUST NOT - `IsAbsentTrait`
        /// below has the argument. A formatter that silently returned the
        /// empty string for `"None"` would hide the absence from every caller
        /// instead of letting each one answer it, and one of those callers
        /// builds a chip whose BOX would still be drawn around nothing.
        public static string TraitWithTier(string trait, int? tier)
        {
            var roman = Tier(tier);
            return roman.Length == 0 ? trait : trait + " " + roman;
        }

        /// Whether a combat slot holds NO TRAIT, which is a different thing
        /// from holding an unknown one.
        ///
        /// PHASE 9 TASK 21e, AND THE DEFECT IT CLOSES WAS ON A PLAYER'S
        /// SCREEN. The exit gate's walk of the packaged app found the granted
        /// Hollow's card drawing TWO CHIPS BOTH READING "None" - the wire's
        /// own word for an empty slot, laid out as though it were the name of
        /// a trait. It is the same shape as the HUD's "2 tick 155" and the
        /// post-wave screen's "Post-Wave" title: an internal value reaching
        /// the player unedited.
        ///
        /// `"None"` IS THE ENGINE'S `Trait.None` COMING OVER THE WIRE, and it
        /// is modelled correctly on the way here - this is a RENDERING fault
        /// and not a data one. `Trait` is a C# enum whose zero member is
        /// `None`, the contract serialises it by name (`SimulateTests`:
        /// `Assert.Equal("None", echo[3].Trait2)` with `Assert.Null(echo[3]
        /// .Tier2)`), and Task 6b already ruled on what that pair MEANS:
        /// `FtueDirector.Tier` returns 0 for it rather than throwing, because
        /// "an absent trait has no coverage to null out". The engine says
        /// absence; only the view said "None".
        ///
        /// ORDINAL-IGNORE-CASE, MATCHING `FtueDirector.Tier`'s OWN TEST. One
        /// spelling of this comparison, not two that happen to agree - and
        /// ordinal rather than culture-aware for the reason every comparison
        /// against a wire literal takes it: a Turkish locale lower-cases "I"
        /// to a dotless one and `"NONE"` stops matching.
        ///
        /// NULL AND EMPTY ARE ABSENCES TOO. `LineageView.AddTrait` already
        /// screened for those and not for `"None"`, which is exactly how the
        /// lineage tree drew the same chips; there is one definition now.
        ///
        /// IT ASKS ABOUT THE TRAIT AND NOT ABOUT THE TIER, deliberately. A
        /// null TIER is an Aberrant (data_model 2) and a `"None"` TRAIT is an
        /// absence, and conflating them is the mistake `TraitChip` and
        /// `CreatureCard` were both making: an empty slot arrives with a null
        /// tier, so both were marking it `aberrant` and drawing the rare-trait
        /// treatment around the word "None".
        public static bool IsAbsentTrait(string trait)
        {
            var name = (trait ?? string.Empty).Trim();
            return name.Length == 0
                || string.Equals(name, "None", StringComparison.OrdinalIgnoreCase);
        }

        /// bible 1.2's Role column - "Wall" for a Vetch, "Sniper" for a
        /// Hollow. The one word that says what a species is FOR.
        ///
        /// THE HANDOFF ASKS FOR THIS LINE AND THE DTO DOES NOT CARRY IT.
        /// `Splice Chamber.dc.html:65` draws a muted line under each parent's
        /// name ("Armored · Slow") and `Creature Roster.dc.html:64` draws the
        /// same shape on every card ("Warform · on duty"); `CreatureDto` has a
        /// species and an instinct and no role field at all. bible 1.2's table
        /// is where that word actually lives, so this is a LOOKUP OF A
        /// PUBLISHED TABLE rather than a derivation - the six rows are
        /// `broodline_bible.md:42-48`, in the order that file lists them.
        ///
        /// EMPTY FOR ANYTHING ELSE, NEVER A GUESS, on `CreatureSprites`' rule
        /// and `HeroSlot.SetSpecies`'s. A display name like "Ember Skitter"
        /// names two of the six and nothing can pick between them, so the line
        /// it produces is empty and `RoleLine` collapses to whatever else it
        /// has - which is visible, and therefore findable.
        public static string SpeciesRole(string species)
        {
            switch ((species ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "vetch":   return "Wall";
                case "ember":   return "Splash";
                case "skitter": return "Swarm";
                case "hollow":  return "Sniper";
                case "loam":    return "Support";
                case "pale":    return "Control";
                default:        return string.Empty;
            }
        }

        /// "Wall · Forage" - the muted line under a creature's name.
        ///
        /// TWO FACTS THE PLAYER ALREADY OWNS, not a third invented one. The
        /// handoff's own line is two descriptors joined by a middle dot; ours
        /// are the species' role (bible 1.2) and the creature's Instinct,
        /// which is a field on the DTO and is the other thing bible 1.2 makes
        /// a breeding target ("Instinct inherits independently of the body").
        ///
        /// EITHER HALF MAY BE MISSING AND THE JOINER GOES WITH IT, because a
        /// line reading " · Forage" is a hole where a fact should be. Both
        /// missing collapses to empty, and both callers hide the row on that.
        public static string RoleLine(CreatureDto creature)
        {
            if (creature == null) return string.Empty;

            var role = SpeciesRole(creature.Species);
            var instinct = (creature.Instinct ?? string.Empty).Trim();

            if (role.Length == 0) return instinct;
            if (instinct.Length == 0) return role;
            return role + " · " + instinct;
        }
    }
}
