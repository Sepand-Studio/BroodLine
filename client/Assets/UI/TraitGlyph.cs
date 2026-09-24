namespace Broodline.UI
{
    /// The twelve authored trait silhouettes. Unknown or absent traits keep
    /// their text label without borrowing an unrelated icon.
    public static class TraitGlyph
    {
        public static string ClassFor(string trait)
        {
            switch ((trait ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "carapace": return "icon--trait-carapace";
                case "taunt": return "icon--trait-taunt";
                case "cinder": return "icon--trait-cinder";
                case "splash": return "icon--trait-splash";
                case "sprint": return "icon--trait-sprint";
                case "litter": return "icon--trait-litter";
                case "reach": return "icon--trait-reach";
                case "pierce": return "icon--trait-pierce";
                case "regrow": return "icon--trait-regrow";
                case "burrow": return "icon--trait-burrow";
                case "screen": return "icon--trait-screen";
                case "chill": return "icon--trait-chill";
                default: return null;
            }
        }
    }
}
