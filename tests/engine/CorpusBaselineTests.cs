using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using Broodline.Sim;

namespace Broodline.Sim.Tests
{
    /// The corpus's regression net.
    ///
    /// cross-runtime-diff.sh generates BOTH sides fresh and diffs them, so it
    /// proves the two runtimes agree with each other - not that either still
    /// agrees with yesterday. A refactor that changed behaviour identically on
    /// both would pass it in silence. This file is the other half: 500 hashes
    /// pinned in a tracked file, covering species, trait and Instinct
    /// combinations no golden reaches.
    public class CorpusBaselineTests
    {
        // System.IO is banned in the ENGINE, not in the tests. This file is in
        // tests/ and reads a build artifact; nothing here ships.
        private static string BaselinePath =>
            Path.Combine(AppContext.BaseDirectory, "corpus-baseline.txt");

        private static string SourcePath()
        {
            // Walk up from bin/Debug/net10.0 to the project folder, so the
            // emitter writes the TRACKED file rather than the copied one.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Broodline.Sim.Tests.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "corpus-baseline.txt");
        }

        private static string Render()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
                sb.Append(i).Append(' ').Append(Corpus.RunScenario(i)).Append('\n');
            return sb.ToString();
        }

        [Fact]
        public void Emit()
        {
            // Only does anything under emit-corpus-baseline.sh. As an ordinary
            // test run it is a no-op, so `dotnet test` can never silently
            // rewrite the thing it is supposed to be checking against.
            if (Environment.GetEnvironmentVariable("BROODLINE_EMIT_BASELINE") != "1") return;
            File.WriteAllText(SourcePath(), Render());
        }

        [Fact]
        public void EveryScenarioMatchesTheCommittedBaseline()
        {
            Assert.True(File.Exists(BaselinePath),
                "corpus-baseline.txt was not copied to the output directory - check the csproj Content item.");

            var expected = File.ReadAllLines(BaselinePath);
            Assert.Equal(Corpus.ScenarioCount, expected.Length);

            var drifted = new List<string>();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                string actual = i + " " + Corpus.RunScenario(i);
                if (actual != expected[i]) drifted.Add("  line " + i + ": expected '" + expected[i] + "', got '" + actual + "'");
                if (drifted.Count == 10) break;
            }

            Assert.True(drifted.Count == 0,
                "The engine no longer reproduces the committed corpus.\n" +
                "If this is an INTENDED behaviour change, run\n" +
                "  ./implementation/scripts/emit-corpus-baseline.sh\n" +
                "and say in the commit message what changed and why.\n" +
                "First differences:\n" + string.Join("\n", drifted));
        }
    }
}
