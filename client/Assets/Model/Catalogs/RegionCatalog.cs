using System;
using System.Collections.Generic;

namespace Broodline.Model.Catalogs
{
    public enum Band { Inner, Mid, Outer }

    /// One of the thirty regions of `specs/broodline_region_graph.md` §7.
    public sealed class RegionInfo
    {
        public string Id;          // slug, e.g. "holdfast"
        public string Name;        // display name
        public Band Band;
        public int Lanes;          // 1–3, per band until the roster is wired
        public string[] Neighbours; // slugs, from the spec's adjacency table
    }

    /// THE REGION GRAPH, TRANSCRIBED - Phase 10 Task 1.5. Thirty regions in
    /// three rings, 43 edges, 8 gates, from `specs/broodline_region_graph.md`
    /// §2–§8, which is authoritative over the older roster's names. Hop times
    /// are §8's: 25 minutes within a band, 50 across a gate. This is a
    /// catalog, not state: which region the Ark is in comes from the server's
    /// `region/state`, whose ids do not yet name these regions (`Locate`).
    public static class RegionCatalog
    {
        static RegionInfo R(string name, Band band, int lanes, params string[] neighbours)
            => new RegionInfo { Id = Slug(name), Name = name, Band = band, Lanes = lanes, Neighbours = Array.ConvertAll(neighbours, Slug) };

        public static string Slug(string name) => name.ToLowerInvariant().Replace("the ", "").Replace(' ', '-');

        public static readonly IReadOnlyList<RegionInfo> All = new[]
        {
            // Inner Reach - a ring of 8 plus two chords; Holdfast starts the game.
            R("Holdfast",    Band.Inner, 1, "Tellin", "Chalkrise", "Quillmoor"),
            R("Tellin",      Band.Inner, 1, "Holdfast", "Ashfold", "Greyspan"),
            R("Ashfold",     Band.Inner, 1, "Tellin", "Millgate", "Barrowlight"),
            R("Millgate",    Band.Inner, 1, "Ashfold", "Quillmoor", "Fenwatch"),
            R("Quillmoor",   Band.Inner, 1, "Millgate", "Windfell", "Holdfast"),
            R("Windfell",    Band.Inner, 1, "Quillmoor", "Barrowlight", "Highmarl"),
            R("Barrowlight", Band.Inner, 1, "Windfell", "Chalkrise", "Ashfold"),
            R("Chalkrise",   Band.Inner, 1, "Barrowlight", "Holdfast", "Stonewake"),
            // Mid Reach - a ring of 12 plus two chords.
            R("Greyspan",    Band.Mid, 2, "Cadewater", "Sablewick", "Tellin"),
            R("Sablewick",   Band.Mid, 2, "Greyspan", "Coldharrow", "The Narrows"),
            R("Coldharrow",  Band.Mid, 2, "Sablewick", "Fenwatch", "Deepscree"),
            R("Fenwatch",    Band.Mid, 2, "Coldharrow", "Thornwyke", "Millgate"),
            R("Thornwyke",   Band.Mid, 2, "Fenwatch", "Dunmarsh", "Rookspire"),
            R("Dunmarsh",    Band.Mid, 2, "Thornwyke", "Highmarl", "The Spill"),
            R("Highmarl",    Band.Mid, 2, "Dunmarsh", "The Narrows", "Windfell"),
            R("The Narrows", Band.Mid, 2, "Highmarl", "Netherfold", "Sablewick"),
            R("Netherfold",  Band.Mid, 2, "The Narrows", "Stonewake", "Threnody"),
            R("Stonewake",   Band.Mid, 2, "Netherfold", "Rookspire", "Chalkrise"),
            R("Rookspire",   Band.Mid, 2, "Stonewake", "Cadewater", "Thornwyke"),
            R("Cadewater",   Band.Mid, 2, "Rookspire", "Greyspan", "Sheerdown"),
            // Outer Reach - a ring of 7 plus the Delta chain of 3.
            R("Deepscree",   Band.Outer, 3, "Sheerdown", "Blacksump", "Coldharrow"),
            R("Blacksump",   Band.Outer, 3, "Deepscree", "The Spill", "The Gyre"),
            R("The Spill",   Band.Outer, 3, "Blacksump", "Kettlemoor", "Dunmarsh"),
            R("Kettlemoor",  Band.Outer, 3, "The Spill", "Threnody", "Rimfall"),
            R("Threnody",    Band.Outer, 3, "Kettlemoor", "Saltwrack", "Netherfold"),
            R("Saltwrack",   Band.Outer, 3, "Threnody", "Sheerdown"),
            R("Sheerdown",   Band.Outer, 3, "Saltwrack", "Deepscree", "Cadewater"),
            R("The Gyre",    Band.Outer, 3, "Blacksump", "Weltering"),
            R("Weltering",   Band.Outer, 3, "The Gyre", "Rimfall"),
            R("Rimfall",     Band.Outer, 3, "Weltering", "Kettlemoor"),
        };

        /// §6's eight gate pairings, Inner↔Mid then Mid↔Outer.
        public static readonly IReadOnlyList<(string A, string B)> Gates = new[]
        {
            ("tellin", "greyspan"), ("millgate", "fenwatch"), ("windfell", "highmarl"), ("chalkrise", "stonewake"),
            ("coldharrow", "deepscree"), ("dunmarsh", "spill"), ("netherfold", "threnody"), ("cadewater", "sheerdown"),
        };

        public const string Start = "holdfast";
        public const int MinutesWithinBand = 25, MinutesAcrossGate = 50;

        static readonly Dictionary<string, RegionInfo> ById = Index();
        static Dictionary<string, RegionInfo> Index()
        {
            var d = new Dictionary<string, RegionInfo>();
            foreach (var r in All) d[r.Id] = r;
            return d;
        }

        public static RegionInfo Find(string id) => id != null && ById.TryGetValue(id, out var r) ? r : null;

        public static bool IsGate(string a, string b)
        {
            foreach (var g in Gates) if ((g.A == a && g.B == b) || (g.A == b && g.B == a)) return true;
            return false;
        }

        /// A region with a crossing to another reach. This is authored
        /// geography, not a claim or deposit state.
        public static bool HasGate(string id)
        {
            foreach (var g in Gates) if (g.A == id || g.B == id) return true;
            return false;
        }

        public static bool AreAdjacent(string a, string b)
        {
            var r = Find(a);
            return r != null && Array.IndexOf(r.Neighbours, b) >= 0;
        }

        /// Minutes for one hop; -1 if the two are not adjacent.
        public static int HopMinutes(string a, string b)
            => !AreAdjacent(a, b) ? -1 : IsGate(a, b) ? MinutesAcrossGate : MinutesWithinBand;

        /// Fewest minutes from `from` to `to` over the graph (Dijkstra on 30
        /// nodes), and the route taken. Null route if unreachable.
        public static int TravelMinutes(string from, string to, out List<string> route)
        {
            route = null;
            if (Find(from) == null || Find(to) == null) return -1;
            var best = new Dictionary<string, int>(); var prev = new Dictionary<string, string>();
            var open = new List<string> { from }; best[from] = 0;
            while (open.Count > 0)
            {
                open.Sort((x, y) => best[x].CompareTo(best[y]));
                var cur = open[0]; open.RemoveAt(0);
                if (cur == to) break;
                foreach (var n in Find(cur).Neighbours)
                {
                    var cost = best[cur] + HopMinutes(cur, n);
                    if (!best.TryGetValue(n, out var known) || cost < known)
                    { best[n] = cost; prev[n] = cur; if (!open.Contains(n)) open.Add(n); }
                }
            }
            if (!best.ContainsKey(to)) return -1;
            route = new List<string>();
            for (var at = to; at != null; prev.TryGetValue(at, out at)) { route.Insert(0, at); if (at == from) break; }
            return best[to];
        }

        /// The undirected edge count, for the tests that pin §7's 43.
        public static int EdgeCount()
        {
            int degree = 0; foreach (var r in All) degree += r.Neighbours.Length; return degree / 2;
        }

        /// WHERE THE ARK IS, given the server's region id. The API names
        /// regions "region-N" today, not by these slugs, so N indexes this
        /// catalog; an unknown or absent id is the starting region. Replace
        /// when `region/state` learns the graph.
        public static RegionInfo Locate(string serverRegionId)
        {
            if (string.IsNullOrEmpty(serverRegionId)) return Find(Start);
            var direct = Find(serverRegionId);
            if (direct != null) return direct;
            var dash = serverRegionId.LastIndexOf('-');
            if (dash >= 0 && int.TryParse(serverRegionId.Substring(dash + 1), out var n) && n >= 1 && n <= All.Count)
                return All[n - 1];
            return Find(Start);
        }
    }
}
