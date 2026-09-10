using System;
using System.IO;
using System.Text;
using Broodline.Sim;
using UnityEngine;

/// Emits the determinism corpus from inside a *built player*, so the hashes we
/// compare against CoreCLR were actually produced by IL2CPP.
///
/// Why this is not an -executeMethod harness: -executeMethod runs inside the
/// Unity Editor, and the Editor runs managed code under Mono. A gate built that
/// way would compare CoreCLR against Editor-Mono and report agreement having
/// never executed a single IL2CPP-compiled instruction — the precise blind spot
/// the cross-runtime gate exists to close. So the corpus runner ships in the
/// player instead, and DeterminismHarness (Assets/Editor) only *builds* it.
///
/// Deliberately NOT under an Editor/ folder: anything there is stripped from
/// player builds, which would leave the player with nothing to run.
///
/// Usage (the built player, not the editor):
///   BroodlineCorpus.app/Contents/MacOS/Broodline\ Bench \
///     -batchmode -nographics -logfile - -corpusOut /path/to/corpus-il2cpp.txt
public static class CorpusPlayerHarness
{
    // The loop bound lives in the shared engine source (Broodline.Sim.Corpus)
    // rather than being re-declared here. It used to be a local `const int
    // ScenarioCount = 500` carrying a "must match the CoreCLR emitter" comment —
    // a correspondence nothing checked. Both emitters now read the same constant,
    // which the .NET suite asserts is 500 and the diff script asserts it counted.

    const string OutFlag = "-corpusOut";
    const string Tag = "[CorpusPlayerHarness] ";

    /// BeforeSplashScreen is the earliest player-side hook: it fires before the
    /// first scene loads, so the corpus is computed, written and the process
    /// asked to quit before the player renders anything. The player still ships
    /// one deliberately empty scene — Unity 6000.6 refuses to build a player
    /// with none; see DeterminismHarness.ScenePath for that finding.
    ///
    /// Gated on BROODLINE_CORPUS_PLAYER, which DeterminismHarness sets via
    /// extraScriptingDefines on its own build and nothing else sets. The
    /// attribute fires in EVERY player built from this project, not only this
    /// one, so ungated the quit-on-missing-argument path above reaches players
    /// that were never meant to emit a corpus: it terminated the Phase 0
    /// benchmark player before its scene loaded, which read on the device as the
    /// app crashing on launch. Scoping the define rather than softening the quit
    /// keeps a forgotten -corpusOut an immediate, obvious failure in the player
    /// where that is the right answer.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Emit()
    {
#if !BROODLINE_CORPUS_PLAYER
        return;
#else
        var path = ReadOutputPath(Environment.GetCommandLineArgs());
        if (path == null)
        {
            // Launched without the flag. This player exists only to emit the
            // corpus, so that is a usage error, not a mode — and quitting beats
            // returning: with nothing else to do the player would otherwise sit
            // on an empty scene forever, turning a caller's forgotten argument
            // into a hung job instead of an immediate, obvious failure.
            // Exit 2 keeps "you called it wrong" distinct from 1, "it ran and
            // failed", so a caller can tell the two apart.
            Debug.LogError(Tag + "missing required argument: " + OutFlag + " <path>");
            Application.Quit(2);
            return;
        }

        int exitCode;
        try
        {
            // Byte-for-byte the construction used by the CoreCLR emitter in
            // tests/engine/CorpusTests.cs: "<index>,<hash>" per line, indices
            // ascending from 0, AppendLine's newline after every line including
            // the last. The two files are diffed directly, so a formatting
            // difference here would read as a determinism failure that isn't one.
            var sb = new StringBuilder();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
                sb.AppendLine(i + "," + Corpus.RunScenario(i));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // UTF-8 without a BOM — File.WriteAllText's default on both runtimes,
            // stated explicitly so the diff can never trip over a leading BOM.
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));

            Debug.Log(Tag + "wrote " + Corpus.ScenarioCount + " hashes to " + path);
            exitCode = 0;
        }
        catch (Exception e)
        {
            // A player that died silently would leave the caller diffing a stale
            // file from a previous run, so say so loudly and fail the exit code.
            Debug.LogError(Tag + "FAILED: " + e);
            exitCode = 1;
        }

        Application.Quit(exitCode);
#endif
    }

    /// Returns the value after -corpusOut, or null if the flag is absent or last.
    static string ReadOutputPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == OutFlag)
                return args[i + 1];
        return null;
    }
}
