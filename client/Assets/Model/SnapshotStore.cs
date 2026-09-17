using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Broodline.Model
{
    public interface ISnapshotStore
    {
        PlayerSnapshot Load();
        void Save(PlayerSnapshot snapshot);
    }

    /// The last `/v1/sync` response, on disk under
    /// `Application.persistentDataPath` - so `Session.ColdStartAsync` can
    /// render it before the next sync returns (client_architecture section 7).
    ///
    /// `JsonUtility` cannot serialize an interface, a `Dictionary`, or the
    /// `IReadOnlyList` fields `PlayerSnapshot` exposes to its callers, so this
    /// keeps a private, `[Serializable]` DTO shaped for `JsonUtility` and maps
    /// to and from it. Dictionaries become parallel key/value lists; both of
    /// `PlayerSnapshot`'s maps (balances, tab thresholds) are server-authored
    /// with no duplicate keys, so the round trip loses nothing.
    public sealed class SnapshotStore : ISnapshotStore
    {
        const string FileName = "snapshot.json";

        readonly string _path;

        public SnapshotStore() : this(Application.persistentDataPath) { }

        public SnapshotStore(string directory)
        {
            _path = Path.Combine(directory, FileName);
        }

        public PlayerSnapshot Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var dto = JsonUtility.FromJson<Dto>(File.ReadAllText(_path));
                return dto == null ? null : FromDto(dto);
            }
            catch (Exception)
            {
                // A cache that cannot be read renders nothing, rather than
                // this throwing out of Session.ColdStartAsync before the
                // network call it guards even gets a chance to run.
                return null;
            }
        }

        /// Writes via a temp file and an atomic replace, and never throws -
        /// the same two properties `Net/OutboxStore.Save` spells out, for the
        /// same reasons.
        ///
        /// `File.WriteAllText` TRUNCATES `_path` before it writes. A kill in
        /// that window - iOS suspending the app, a crash, a flat battery -
        /// leaves a half-written file that `Load` can only discard, so the
        /// cold start that is supposed to "render the cached snapshot
        /// immediately" shows nothing until `/v1/sync` returns. Replacing
        /// means there is no instant at which `_path` is not a whole
        /// snapshot: either the old one or the new one.
        ///
        /// And a cache write is never worth taking the caller down for.
        /// `Load` already swallows its own I/O failures; an unguarded `Save`
        /// left the two halves of this class disagreeing about whether
        /// snapshot persistence is allowed to fail. It is: the next sync
        /// rebuilds it.
        public void Save(PlayerSnapshot snapshot)
        {
            if (snapshot == null) return;

            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var tempPath = _path + ".tmp";
                File.WriteAllText(tempPath, JsonUtility.ToJson(ToDto(snapshot)));

                // File.Move's 3-arg overwrite overload is .NET Standard 2.1+
                // and this project's api compatibility level rejects it, so
                // File.Replace does the swap where a destination already
                // exists; the first save has nothing to replace and a plain
                // Move is already atomic there.
                if (File.Exists(_path)) File.Replace(tempPath, _path, destinationBackupFileName: null);
                else File.Move(tempPath, _path);
            }
            catch (Exception error)
            {
                Debug.LogWarning("[SnapshotStore] could not cache the snapshot: " + error);
            }
        }

        [Serializable]
        sealed class Dto
        {
            public string PlayerId;
            public int ServerId;
            public int HighestWaveCleared;
            public string BundleVersion;
            public string MinimumClientVersion;

            public List<string> BalanceKeys = new List<string>();
            public List<int> BalanceValues = new List<int>();

            public List<string> TabKeys = new List<string>();
            public List<int> TabValues = new List<int>();

            public List<WaveDto> Waves = new List<WaveDto>();
            public List<TraitDto> Traits = new List<TraitDto>();

            public bool FounderNamed;
            public bool TutorialStockGranted;
            public int Splices;
        }

        [Serializable]
        sealed class WaveDto
        {
            public int Id;
            public string RewardCurrency;
            public int RewardAmount;
        }

        [Serializable]
        sealed class TraitDto
        {
            public string Id;
            public string Species;
            public string Counters;
        }

        static Dto ToDto(PlayerSnapshot s)
        {
            var dto = new Dto
            {
                PlayerId = s.PlayerId,
                ServerId = s.ServerId,
                HighestWaveCleared = s.HighestWaveCleared,
                BundleVersion = s.BundleVersion,
                MinimumClientVersion = s.MinimumClientVersion,
                FounderNamed = s.Ftue?.FounderNamed ?? false,
                TutorialStockGranted = s.Ftue?.TutorialStockGranted ?? false,
                Splices = s.Ftue?.Splices ?? 0,
            };

            if (s.Balances != null)
            {
                foreach (var kv in s.Balances) { dto.BalanceKeys.Add(kv.Key); dto.BalanceValues.Add(kv.Value); }
            }
            if (s.Tabs != null)
            {
                foreach (var kv in s.Tabs) { dto.TabKeys.Add(kv.Key); dto.TabValues.Add(kv.Value); }
            }
            if (s.Waves != null)
            {
                foreach (var w in s.Waves)
                    dto.Waves.Add(new WaveDto { Id = w.Id, RewardCurrency = w.RewardCurrency, RewardAmount = w.RewardAmount });
            }
            if (s.Traits != null)
            {
                foreach (var t in s.Traits)
                    dto.Traits.Add(new TraitDto { Id = t.Id, Species = t.Species, Counters = t.Counters });
            }

            return dto;
        }

        static PlayerSnapshot FromDto(Dto dto)
        {
            var balances = new Dictionary<string, int>();
            for (var i = 0; i < dto.BalanceKeys.Count && i < dto.BalanceValues.Count; i++)
                balances[dto.BalanceKeys[i]] = dto.BalanceValues[i];

            var tabs = new Dictionary<string, int>();
            for (var i = 0; i < dto.TabKeys.Count && i < dto.TabValues.Count; i++)
                tabs[dto.TabKeys[i]] = dto.TabValues[i];

            var waves = new List<WaveSummary>();
            foreach (var w in dto.Waves)
                waves.Add(new WaveSummary { Id = w.Id, RewardCurrency = w.RewardCurrency, RewardAmount = w.RewardAmount });

            var traits = new List<TraitSummary>();
            foreach (var t in dto.Traits)
                traits.Add(new TraitSummary { Id = t.Id, Species = t.Species, Counters = t.Counters });

            return new PlayerSnapshot
            {
                PlayerId = dto.PlayerId,
                ServerId = dto.ServerId,
                HighestWaveCleared = dto.HighestWaveCleared,
                BundleVersion = dto.BundleVersion,
                MinimumClientVersion = dto.MinimumClientVersion,
                Balances = balances,
                Tabs = tabs,
                Waves = waves,
                Traits = traits,
                Ftue = new FtueFacts
                {
                    FounderNamed = dto.FounderNamed,
                    TutorialStockGranted = dto.TutorialStockGranted,
                    Splices = dto.Splices,
                },
            };
        }
    }
}
