using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Broodline.Sim.Combat;

namespace Broodline.Config.Validate
{
    public static class Program
    {
        /// Usage: Broodline.Config.Validate <bundle-dir>
        ///
        /// Emits {"ok":bool,"violations":[string]} on stdout and exits 0 or 1,
        /// so the TypeScript publish step parses a result rather than scraping
        /// a log. A bundle that fails is not published - solo_execution 5.2.
        public static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("usage: Broodline.Config.Validate <bundle-dir>");
                return 2;
            }

            var violations = new List<string>();
            string wavesPath = Path.Combine(args[0], "waves.json");

            if (!File.Exists(wavesPath))
            {
                violations.Add("waves.json is missing from " + args[0]);
            }
            else
            {
                try
                {
                    BundleWaves.Parse(File.ReadAllText(wavesPath));
                }
                catch (WaveCompositionException ex)
                {
                    violations.Add(ex.Message);
                }
                catch (JsonException ex)
                {
                    violations.Add("waves.json is not valid JSON: " + ex.Message);
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = violations.Count == 0,
                violations,
            }));
            return violations.Count == 0 ? 0 : 1;
        }
    }
}
