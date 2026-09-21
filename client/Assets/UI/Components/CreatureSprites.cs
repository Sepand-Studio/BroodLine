using System;
using UnityEngine;

namespace Broodline.UI.Components
{
    /// The baked sprites, by name. Replaces `SpeciesProxy`: the six interim
    /// proxies were a pre-test of bible 10.2 rule 1; these are the bodies
    /// the pipeline renders, and `SilhouetteTests` now measures them.
    ///
    /// NULL WHEN MISSING, NEVER A GUESS - the same rule SpeciesProxy kept.
    /// A trait this build has no part for leaves its layer empty; the card
    /// still names it in the pip.
    public static class CreatureSprites
    {
        public const string Root = "Art/creatures";

        public static string BodyPath(string species) => Root + "/bodies/" + Lower(species);
        public static string PartPath(string species, string socket, string trait) =>
            Root + "/parts/" + Lower(species) + "-" + Lower(socket) + "-" + Lower(trait);

        public static Texture2D Body(string species) =>
            string.IsNullOrWhiteSpace(species) ? null : Resources.Load<Texture2D>(BodyPath(species));

        public static Texture2D Part(string species, string socket, string trait) =>
            string.IsNullOrWhiteSpace(species) || string.IsNullOrWhiteSpace(trait) ? null
                : Resources.Load<Texture2D>(PartPath(species, socket, trait));

        static string Lower(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    }
}
