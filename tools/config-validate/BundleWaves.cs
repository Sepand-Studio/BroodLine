using System;
using System.Collections.Generic;
using System.Text.Json;
using Broodline.Sim.Combat;

namespace Broodline.Config.Validate
{
    /// Maps a bundle's waves.json onto the engine's WaveDef, then hands each
    /// one to the engine's own Validate().
    ///
    /// This file contains NO game rules. It contains a JSON shape and a type
    /// lookup, and everything that could reject a wave is thrown by WaveDef.
    /// If a rule ever appears here, it has been copied out of the engine and
    /// the two will diverge - that is the failure this whole project exists
    /// to prevent.
    public static class BundleWaves
    {
        public static IReadOnlyList<WaveDef> Parse(string json)
        {
            var doc = JsonSerializer.Deserialize<List<WaveJson>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (doc == null) throw new WaveCompositionException("waves.json did not parse as an array.");

            var waves = new List<WaveDef>();
            foreach (var w in doc)
            {
                var spawns = new SpawnEntry[w.Spawns?.Count ?? 0];
                for (int i = 0; i < spawns.Length; i++)
                {
                    var s = w.Spawns[i];
                    // Enum.TryParse rather than Enum.Parse: a bundle naming a
                    // raider this engine does not have is a PUBLISH failure
                    // with a readable message, not an ArgumentException.
                    if (!Enum.TryParse<RaiderType>(s.Type, ignoreCase: false, out var type))
                    {
                        throw new WaveCompositionException(
                            "Wave " + w.Id + " spawn " + i + " names raider type '" + s.Type +
                            "', which this engine does not have.");
                    }
                    spawns[i] = new SpawnEntry { Tick = s.Tick, Type = type };
                }

                var def = new WaveDef(w.Id, w.Integrity, w.LaneCount, spawns);
                // The engine's rules, invoked rather than reimplemented.
                def.Validate();
                waves.Add(def);
            }
            return waves;
        }

        private sealed class WaveJson
        {
            public int Id { get; set; }
            public int Integrity { get; set; }
            public int LaneCount { get; set; }
            public List<SpawnJson> Spawns { get; set; }
        }

        private sealed class SpawnJson
        {
            public int Tick { get; set; }
            public string Type { get; set; }
        }
    }
}
