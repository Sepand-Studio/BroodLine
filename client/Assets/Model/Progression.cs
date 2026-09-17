using System.Collections.Generic;

namespace Broodline.Model
{
    /// The tab bar's reveal order, as a pure function of campaign progress and
    /// the bundle's reveal thresholds.
    ///
    /// client_architecture section 9: "the bar is a pure function of campaign
    /// progress ... both already returned by /v1/sync. There is no local
    /// state to lose on reinstall and no new server field." Map and Ark are
    /// always shown; a fresh install with no cached snapshot and no
    /// thresholds still shows exactly those two - "correct for a new player,
    /// and wrong for a reinstalling veteran only for the length of one sync."
    public static class Progression
    {
        public static readonly string[] Order = { "Map", "Ark", "Splice", "Lab", "Allies" };
        public const string AlwaysA = "Map", AlwaysB = "Ark";

        public static IReadOnlyList<string> TabsFor(int highestWaveCleared, IReadOnlyDictionary<string, int> thresholds)
        {
            var tabs = new List<string>(5);
            foreach (var tab in Order)
            {
                bool always = tab == AlwaysA || tab == AlwaysB;
                if (always || (thresholds.TryGetValue(tab, out var at) && highestWaveCleared >= at)) tabs.Add(tab);
            }
            return tabs;
        }
    }
}
