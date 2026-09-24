using System;
using System.Collections.Generic;

namespace Broodline.Model.Stub
{
    /// LOCAL STATE FOR THE SCREENS WHOSE BACKEND DOES NOT EXIST YET - Phase 10
    /// Task 1.5. Store purchases, facility tiers and upgrade timers, map
    /// travel previews and alliance membership: none has an endpoint, so their screens read and write this
    /// ledger instead, through a key/value store the Game layer supplies
    /// (PlayerPrefs in the client, a dictionary in tests). EVERY write logs a
    /// `[stub]` line through `Log`, every CTA that lands here is labelled
    /// "(preview)" on screen, and nothing in here touches a real balance:
    /// `PlayerSnapshot.Balances` stays the server's word. Delete this class
    /// when the endpoints arrive; nothing else should depend on it.
    public sealed class StubLedger
    {
        public interface IStore
        {
            string Get(string key);
            void Set(string key, string value);
        }

        public sealed class MemoryStore : IStore
        {
            readonly Dictionary<string, string> _d = new Dictionary<string, string>();
            public string Get(string key) => _d.TryGetValue(key, out var v) ? v : null;
            public void Set(string key, string value) => _d[key] = value;
        }

        readonly IStore _store;
        readonly Action<string> _log;
        readonly Func<DateTime> _now;

        public StubLedger(IStore store, Action<string> log = null, Func<DateTime> now = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _log = log ?? (_ => { });
            _now = now ?? (() => DateTime.UtcNow);
        }

        void Write(string key, string value)
        {
            _store.Set(key, value);
            _log("[stub] " + key + " = " + value);
        }

        // ---- facilities ----
        public int FacilityTier(string id)
            => int.TryParse(_store.Get("facility." + id + ".tier"), out var t) && t >= 1 ? t : 1;

        public DateTime? UpgradeEndsAt(string id)
            => long.TryParse(_store.Get("facility." + id + ".ends"), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;

        /// Starts a preview upgrade; the tier rises when `Settle` sees the timer expire.
        public void StartUpgrade(string id, TimeSpan duration)
            => Write("facility." + id + ".ends", (_now() + duration).Ticks.ToString());

        /// Promotes any facility whose timer has ended. Call before reading tiers.
        public void Settle()
        {
            foreach (var key in new List<string>(KnownFacilityKeys()))
            {
                var ends = UpgradeEndsAt(key);
                if (ends == null || ends > _now()) continue;
                Write("facility." + key + ".tier", (FacilityTier(key) + 1).ToString());
                Write("facility." + key + ".ends", null);
            }
        }

        IEnumerable<string> KnownFacilityKeys()
        {
            var seen = _store.Get("facility.keys");
            if (string.IsNullOrEmpty(seen)) yield break;
            foreach (var k in seen.Split(',')) if (k.Length > 0) yield return k;
        }

        public void RememberFacility(string id)
        {
            var seen = _store.Get("facility.keys") ?? string.Empty;
            if (("," + seen + ",").Contains("," + id + ",")) return;
            Write("facility.keys", seen.Length == 0 ? id : seen + "," + id);
        }

        // ---- store ----
        public DateTime? DailyGiftClaimedAt()
            => long.TryParse(_store.Get("store.gift.at"), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;

        public bool DailyGiftAvailable()
        {
            var at = DailyGiftClaimedAt();
            return at == null || at.Value.Date < _now().Date;
        }

        public void ClaimDailyGift() => Write("store.gift.at", _now().Ticks.ToString());

        public void RecordPreviewPurchase(string id) => Write("store.preview." + id, _now().Ticks.ToString());

        // ---- map travel (local presentation only; never changes server region) ----
        public string PreviewTravelTarget() => _store.Get("map.travel.target");
        public string PreviewTravelOrigin() => _store.Get("map.travel.origin");
        public DateTime? PreviewTravelEndsAt()
            => long.TryParse(_store.Get("map.travel.ends"), out var ticks)
                ? new DateTime(ticks, DateTimeKind.Utc) : (DateTime?)null;

        public bool PreviewTravelActive()
        {
            var ends = PreviewTravelEndsAt();
            return !string.IsNullOrEmpty(PreviewTravelTarget()) && ends.HasValue && ends.Value > _now();
        }

        /// A completed preview remains visible until the next route starts.
        /// No real region or claim state is ever written here.
        public bool StartPreviewTravel(string origin, string target, TimeSpan duration)
        {
            if (string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(target) || duration <= TimeSpan.Zero || PreviewTravelActive())
                return false;
            Write("map.travel.origin", origin);
            Write("map.travel.target", target);
            Write("map.travel.ends", (_now() + duration).Ticks.ToString());
            return true;
        }

        // ---- allies ----
        public string AllianceName() => _store.Get("allies.name");
        public void CreateAlliance(string name) => Write("allies.name", name);
    }
}
