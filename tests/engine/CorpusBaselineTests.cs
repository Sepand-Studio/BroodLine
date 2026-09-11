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

        // The PROJECT folder, not the repo root: the emitter must write the
        // TRACKED file rather than the copy in bin/Debug/net10.0.
        private static string SourcePath() =>
            Path.Combine(TestPaths.ProjectDir(), "corpus-baseline.txt");

        private const string HeaderPrefix = "# simversion ";

        private static string Render()
        {
            var sb = new StringBuilder();
            sb.Append(HeaderPrefix).Append(SimVersion.Value).Append('\n');
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
        public void TheBaselineHeaderNamesTheCurrentEngineVersion()
        {
            // The emitter writes this line from SimVersion.Value, and refuses
            // to re-baseline when the hashes moved and the version did not.
            // This assertion catches the other direction: a bump that never
            // re-ran the emitter, leaving the file claiming an older engine.
            var lines = File.ReadAllLines(BaselinePath);
            Assert.True(lines.Length > 0, "corpus-baseline.txt is empty.");
            Assert.Equal("# simversion " + SimVersion.Value, lines[0]);
        }

        [Fact]
        public void EveryScenarioMatchesTheCommittedBaseline()
        {
            Assert.True(File.Exists(BaselinePath),
                "corpus-baseline.txt was not copied to the output directory - check the csproj Content item.");

            var lines = File.ReadAllLines(BaselinePath);

            // One header line, then one line per scenario. Asserting the total
            // rather than the body length keeps a truncated file from reading
            // as a header problem.
            Assert.Equal(Corpus.ScenarioCount + 1, lines.Length);
            Assert.StartsWith(HeaderPrefix, lines[0]);

            var drifted = new List<string>();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                string actual = i + " " + Corpus.RunScenario(i);
                // +1 throughout: line 0 is the header, scenario i is line i+1.
                if (actual != lines[i + 1]) drifted.Add("  line " + (i + 1) + ": expected '" + lines[i + 1] + "', got '" + actual + "'");
                if (drifted.Count == 10) break;
            }

            Assert.True(drifted.Count == 0,
                "The engine no longer reproduces the committed corpus.\n" +
                "If this is an INTENDED behaviour change, BUMP SimVersion.Value first,\n" +
                "then run\n" +
                "  ./implementation/scripts/emit-corpus-baseline.sh\n" +
                "and say in the commit message what changed and why.\n" +
                "First differences:\n" + string.Join("\n", drifted));
        }
    }
}
