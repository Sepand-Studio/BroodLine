using System.Collections.Generic;
using Broodline.Model.Catalogs;

namespace Broodline.UI
{
    public sealed class MapRegionRow
    {
        public string Id, Name, Detail;
        public bool Here, Gate;
        public int Lanes;
    }

    public sealed class MapScreenModel
    {
        public string CurrentRegionId;
        public IReadOnlyList<(string Heading, IReadOnlyList<MapRegionRow> Rows)> Bands;
    }

    /// The World Map's words and model - Phase 10 Task 1.5, skin fidelity:
    /// the thirty regions grouped by ring, the Ark's own region marked, each
    /// row saying its lane count and its hop time from here. WorldMapView
    /// places the same rows on a tappable three-ring atlas.
    public static class MapScreen
    {
        public const string Title = "World Map";
        public const string Eyebrow = "THE FRONTIER";
        public const string HereDetail = "Your Ark is here";
        public const string PreviewOriginDetail = "Preview route origin";
        public static readonly string[] BandHeadings = { "Inner Reach", "Mid Reach", "Outer Reach" };

        public static string Lanes(int lanes) => lanes == 1 ? "1 lane" : lanes + " lanes";

        public static string LaneGlyph(int lanes)
        {
            switch (lanes)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                default: return "?";
            }
        }

        public static string Travel(int minutes)
        {
            if (minutes < 0) return "unreachable";
            if (minutes < 60) return minutes + " min away";
            var h = minutes / 60; var m = minutes % 60;
            return m == 0 ? h + " h away" : h + " h " + m + " min away";
        }

        public static MapScreenModel Build(string serverRegionId)
        {
            var live = RegionCatalog.Locate(serverRegionId);
            var here = live ?? RegionCatalog.PreviewOrigin(serverRegionId);
            var preview = live == null;
            var bands = new List<(string, IReadOnlyList<MapRegionRow>)>();
            foreach (Band band in new[] { Band.Inner, Band.Mid, Band.Outer })
            {
                var rows = new List<MapRegionRow>();
                foreach (var r in RegionCatalog.All)
                {
                    if (r.Band != band) continue;
                    var isHere = r.Id == here.Id;
                    var gate = RegionCatalog.HasGate(r.Id);
                    var minutes = isHere ? 0 : RegionCatalog.TravelMinutes(here.Id, r.Id, out _);
                    rows.Add(new MapRegionRow
                    {
                        Id = r.Id, Name = r.Name, Lanes = r.Lanes, Here = isHere && !preview,
                        Gate = gate,
                        Detail = (isHere ? (preview ? PreviewOriginDetail : HereDetail) + " · " + Lanes(r.Lanes) : Lanes(r.Lanes) + " · " + Travel(minutes))
                            + (gate ? " · Reach gate" : string.Empty),
                    });
                }
                bands.Add((BandHeadings[(int)band], rows));
            }
            return new MapScreenModel { CurrentRegionId = here.Id, Bands = bands };
        }
    }
}
