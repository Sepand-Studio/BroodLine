using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Broodline.Sim.Tests
{
    /// Tests that enforce the *enforcement*.
    ///
    /// <para>
    /// Phase 1's determinism guarantees rest on four mechanisms: the banned-API
    /// analyzer, the IL-level float scan, an asmdef that gives the simulation no
    /// Unity references at all, and the cross-runtime corpus gate. Three of those
    /// four are declarative configuration, and before this file existed all three
    /// could be deleted with nothing going red. Drop the
    /// <c>&lt;AdditionalFiles Include="BannedSymbols.txt" /&gt;</c> line, or the
    /// analyzer <c>PackageReference</c>, or <c>RS0030</c> from
    /// <c>WarningsAsErrors</c>, and the build succeeds and every test passes —
    /// there is simply nothing left checking the banned list. Flip
    /// <c>noEngineReferences</c> to <c>false</c> or add an entry to
    /// <c>references</c> in the asmdef and Unity will happily let the simulation
    /// reference UnityEngine, while <c>dotnet build</c> — which never reads the
    /// asmdef — stays green.
    /// </para>
    ///
    /// <para>
    /// So these assert the configuration itself. They are deliberately blunt: a
    /// failure here is not "the code is wrong", it is "the thing that was
    /// checking the code is gone". Changing any of it is a decision to make
    /// explicitly, by editing this file too.
    /// </para>
    public class EnforcementTests
    {
        static readonly string RepoRoot = TestPaths.RepoRoot();

        static string RepoPath(params string[] parts) =>
            Path.Combine(new[] { RepoRoot }.Concat(parts).ToArray());

        // ------------------------------------------------------------------
        // The banned-API analyzer (Task 3)
        // ------------------------------------------------------------------

        [Fact]
        public void EngineCsproj_StillWiresTheBannedApiAnalyzer()
        {
            var csprojPath = RepoPath("engine", "Broodline.Sim.csproj");
            Assert.True(File.Exists(csprojPath), $"missing {csprojPath}");

            var doc = XDocument.Load(csprojPath);

            // Assert on MSBuild structure, not on substrings: a commented-out
            // line still contains the text, and XDocument does not see comments
            // as elements.
            var packageRefs = doc.Descendants("PackageReference")
                .Select(e => (string)e.Attribute("Include"))
                .ToList();
            Assert.True(
                packageRefs.Contains("Microsoft.CodeAnalysis.BannedApiAnalyzers"),
                "engine/Broodline.Sim.csproj no longer references " +
                "Microsoft.CodeAnalysis.BannedApiAnalyzers — nothing enforces " +
                "engine/BannedSymbols.txt any more. Found: " +
                string.Join(", ", packageRefs));

            var additionalFiles = doc.Descendants("AdditionalFiles")
                .Select(e => (string)e.Attribute("Include"))
                .ToList();
            Assert.True(
                additionalFiles.Contains("BannedSymbols.txt"),
                "engine/Broodline.Sim.csproj no longer passes BannedSymbols.txt to " +
                "the analyzer as an AdditionalFiles item. The analyzer is still " +
                "referenced but has no list to enforce, so it silently bans " +
                "nothing. Found: " + string.Join(", ", additionalFiles));

            var warningsAsErrors = doc.Descendants("WarningsAsErrors")
                .Select(e => e.Value)
                .ToList();
            Assert.True(
                warningsAsErrors.Any(v => v.Contains("RS0030", StringComparison.Ordinal)),
                "engine/Broodline.Sim.csproj no longer promotes RS0030 (the " +
                "banned-API diagnostic) to an error. The list becomes advisory. " +
                "Found WarningsAsErrors: " + string.Join(" | ", warningsAsErrors));
        }

        [Fact]
        public void BannedSymbols_StillListsTheSymbolsTheCoreCannotUse()
        {
            var path = RepoPath("engine", "BannedSymbols.txt");
            Assert.True(File.Exists(path), $"missing {path}");

            // A representative slice, not the whole list — deliberately the
            // entries whose absence would be least visible in review. Each is
            // matched with its trailing ';' so an arity-0 entry cannot satisfy an
            // assertion about an arity-1 type: BannedApiAnalyzers matches on the
            // exact DocID and generic arity is part of it.
            var required = new[]
            {
                "T:System.Random;",
                "T:System.Math;",
                "T:System.DateTime;",
                "N:System.Linq;",
                "T:System.Collections.Generic.Dictionary`2;",
                "T:System.Collections.Generic.HashSet`1;",
                "M:System.String.GetHashCode;",
                "T:System.Diagnostics.Stopwatch;",
                "T:System.Runtime.CompilerServices.RuntimeHelpers;",
                "T:System.Collections.Hashtable;",
                "T:System.Collections.Concurrent.ConcurrentDictionary`2;",
            };

            // Matched at line start, not as a substring: prefixing an entry
            // with '#' still leaves it a substring of the line (e.g. of
            // "#T:System.Random;implementation-defined…"), and
            // BannedApiAnalyzers 3.3.4 emits no diagnostic for an unparseable
            // entry, so a substring match would keep passing while the ban
            // was silently dropped.
            var lines = File.ReadAllLines(path);
            var missing = required
                .Where(r => !lines.Any(line => line.StartsWith(r, StringComparison.Ordinal)))
                .ToList();

            Assert.True(missing.Count == 0,
                "engine/BannedSymbols.txt no longer bans:\n  " + string.Join("\n  ", missing));
        }

        // ------------------------------------------------------------------
        // The asmdef (Task 1)
        // ------------------------------------------------------------------

        [Fact]
        public void EngineAsmdef_StillHasNoUnityReferencesAtAll()
        {
            var path = RepoPath("engine", "Runtime", "Broodline.Sim.asmdef");
            Assert.True(File.Exists(path), $"missing {path}");

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("references", out var references),
                "the asmdef has no \"references\" key at all — an absent key is " +
                "not the same promise as an empty list, so state it explicitly.");
            var listed = references.EnumerateArray().Select(e => e.ToString()).ToList();
            Assert.True(listed.Count == 0,
                "engine/Runtime/Broodline.Sim.asmdef now references other " +
                "assemblies: " + string.Join(", ", listed) + ". The simulation " +
                "core is required to have no dependencies — the empty list is " +
                "the mechanism, and dotnet build never reads this file, so " +
                "nothing else would notice.");

            Assert.True(root.TryGetProperty("noEngineReferences", out var noEngine),
                "the asmdef has no \"noEngineReferences\" key. Unity's default is " +
                "false, i.e. UnityEngine IS referenced — the key must be present " +
                "and true, not merely absent.");
            Assert.True(noEngine.ValueKind == JsonValueKind.True,
                "engine/Runtime/Broodline.Sim.asmdef sets noEngineReferences to " +
                noEngine + ". With it false, Unity links UnityEngine into the " +
                "simulation assembly and UnityEngine.Random, Time.deltaTime and " +
                "Mathf all become reachable from the tick loop — none of which " +
                "the .NET build could ever see.");
        }

        // ------------------------------------------------------------------
        // The corpus gate's own size (Task 7)
        // ------------------------------------------------------------------

        [Fact]
        public void Corpus_StillSweepsTheFullScenarioCount()
        {
            // The Definition of Done names 500. Both emitters read this constant,
            // and implementation/scripts/cross-runtime-diff.sh asserts the line
            // count it actually diffed equals it — previously the script printed
            // `wc -l` instead, so cutting the corpus to five scenarios produced a
            // cheerful "PASS: 5 scenarios agree" and exit 0.
            Assert.Equal(500, Corpus.ScenarioCount);
        }

        // ------------------------------------------------------------------
        // Conditional compilation (the seam between the two compilers)
        // ------------------------------------------------------------------

        /// The csproj glob and the asmdef cover the same *files*, but any
        /// <c>#if</c>/<c>#elif</c>/<c>#define</c>/<c>#undef</c> directive means
        /// they do not cover the same *code*: whichever branch Unity's Mono
        /// compiler takes and whichever branch <c>dotnet build</c> takes can
        /// differ for any symbol, not only a <c>UNITY_*</c> one —
        /// <c>ENABLE_IL2CPP</c>, <c>ENABLE_MONO</c>, <c>NETSTANDARD2_1</c>, even
        /// <c>DEBUG</c> are defined by one toolchain and not the other, so a
        /// symbol-list ban can never enumerate every symbol that creates this
        /// seam. Everything this project enforces statically — the banned-API
        /// analyzer, the IL float scan, <c>dotnet build</c> itself — runs only
        /// over the .NET compilation, so anything inside such a region is
        /// invisible to all of them while still shipping in the player. The
        /// cross-runtime gate would eventually catch a resulting divergence, but
        /// only for code the corpus happens to execute, and only after the fact.
        ///
        /// <para>
        /// Banning the four directives outright, with no symbol pattern left to
        /// evade, makes "both compilers see the same thing" an enforced premise
        /// rather than an assumption. Vendored sources are exempt: FixPointCS
        /// carries its own <c>#if</c> ladders (JAVA, CPP, NET5_0_OR_GREATER) and
        /// is byte-identical to upstream by design — see
        /// engine/Runtime/ThirdParty/FixPointCS/PROVENANCE.md.
        /// </para>
        [Fact]
        public void SimulationCore_HasNoConditionalCompilation()
        {
            var runtime = RepoPath("engine", "Runtime");
            Assert.True(Directory.Exists(runtime), $"missing {runtime}");

            // No symbol pattern: #if/#elif/#define/#undef are banned outright,
            // so there is no not-yet-invented symbol left that could evade this.
            // #else and #endif name no symbol, so they cannot introduce one.
            var directive = new Regex(
                @"^\s*#\s*(if|elif|define|undef)\b",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);

            var thirdParty = Path.Combine(runtime, "ThirdParty") + Path.DirectorySeparatorChar;
            var offenders = new List<string>();
            var scanned = 0;

            foreach (var file in Directory.EnumerateFiles(runtime, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(thirdParty, StringComparison.Ordinal)) continue;
                scanned++;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                    if (directive.IsMatch(lines[i]))
                        offenders.Add(
                            $"{Path.GetRelativePath(RepoRoot, file)}:{i + 1}: {lines[i].Trim()}");
            }

            // Guards against the check quietly scanning nothing — a moved folder
            // or a changed layout would otherwise read as a pass.
            Assert.True(scanned > 0,
                $"scanned no non-vendored .cs files under {runtime}");

            Assert.True(offenders.Count == 0,
                "engine/Runtime contains conditional compilation, which the " +
                ".NET build, the banned-API analyzer and the IL float scan are " +
                "all blind to:\n  " + string.Join("\n  ", offenders));
        }
    }
}
