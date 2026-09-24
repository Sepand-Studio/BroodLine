namespace Broodline.UI
{
    /// The Allies tab's words - Phase 10 Task 1.5. Alliances have no backend
    /// yet (`specs/broodline_alliance_territory.md` is design only), so this
    /// is the tab's honest destination: what an alliance is for and one
    /// preview action, rather than a tab that goes nowhere.
    public static class AlliesScreen
    {
        public const string Title = "Allies";
        public const string Eyebrow = "ALLIANCE";
        public const string NoAlliance = "No alliance yet. Forty geneticists hold ground together: claim stakes, run convoys, and share what a Rally learns.";
        public const string Create = "Create alliance (preview)";
        public static string Member(string name) => "You fly the banner of " + name + ".";
        public const string Preview = "Alliances are a preview in this build.";
        public const string DefaultName = "the Frontier Compact";
        public const string PreviewNotice = "Alliance created (preview) - it lives on this device only.";
        public const string PlannedHeading = "PLANNED TOGETHER";
        public static readonly (string Icon, string Title, string Detail)[] PlannedTools =
        {
            ("map", "Shared claims", "Hold territory as a group."),
            ("timer", "Convoys", "Move supplies together."),
            ("sparkle", "Alliance rallies", "Answer danger with friends."),
        };
    }
}
