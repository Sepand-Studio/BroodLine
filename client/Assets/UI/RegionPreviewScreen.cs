using System;
using System.Collections.Generic;
using Broodline.Model.Catalogs;
using Broodline.Model.Stub;

namespace Broodline.UI
{
    public sealed class RegionPreviewModel
    {
        public string Id, Name, Band, Origin, Route, Neighbours, Status, Action;
        public int Lanes, Minutes, Hops;
        public DateTime? EndsAt;
        public bool CanStart, Active, Complete;
    }

    /// Detail for a region beyond the server-owned Ark. Its route and lane
    /// facts come from the region catalog; the countdown is explicitly local.
    public static class RegionPreviewScreen
    {
        public const string Eyebrow = "WORLD MAP · REGION DETAIL";
        public const string PreviewNote = "Travel is a local preview. Your Ark and claimable nodes remain in the server region.";

        public static RegionPreviewModel Build(string serverRegionId, string targetId, StubLedger ledger, DateTime now)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            var live = RegionCatalog.Locate(serverRegionId);
            var here = RegionCatalog.PreviewOrigin(serverRegionId);
            var target = RegionCatalog.Find(targetId);
            if (target == null || (live != null && target.Id == live.Id))
                throw new ArgumentException("Target must be a remote catalog region", nameof(targetId));

            var active = ledger.PreviewTravelActive();
            var lastTarget = ledger.PreviewTravelTarget();
            var ends = ledger.PreviewTravelEndsAt();
            var thisRoute = lastTarget == target.Id;
            var origin = thisRoute ? RegionCatalog.Find(ledger.PreviewTravelOrigin()) ?? here : here;
            var minutes = RegionCatalog.TravelMinutes(origin.Id, target.Id, out var route);
            var neighbours = new List<string>();
            foreach (var id in target.Neighbours) neighbours.Add(RegionCatalog.Find(id).Name);
            var names = new List<string>();
            if (route != null) foreach (var id in route) names.Add(RegionCatalog.Find(id).Name);
            var complete = thisRoute && ends.HasValue && ends.Value <= now;
            var previewOrigin = live == null && target.Id == here.Id;
            var status = previewOrigin ? "Preview route origin · Ark has not moved" : active
                ? thisRoute ? "Preview journey underway" : "Another preview journey is underway"
                : complete ? "Preview journey complete · Ark has not moved"
                : "Ready to preview this route";

            return new RegionPreviewModel
            {
                Id = target.Id, Name = target.Name,
                Band = MapScreen.BandHeadings[(int)target.Band].ToUpperInvariant(),
                Origin = live == null ? origin.Name + " (preview)" : origin.Name,
                Route = names.Count > 0 ? string.Join("  →  ", names) : "No route available",
                Neighbours = string.Join(" · ", neighbours),
                Lanes = target.Lanes, Minutes = minutes, Hops = route == null ? 0 : route.Count - 1,
                Status = status,
                Action = previewOrigin ? "Preview origin" : active ? "Journey in progress" : "Relocate Ark (preview)",
                CanStart = !previewOrigin && !active && minutes > 0,
                Active = active && thisRoute,
                Complete = complete,
                EndsAt = thisRoute ? ends : null,
            };
        }
    }
}
