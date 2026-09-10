using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Builds the macOS ARM64 IL2CPP player that emits the determinism corpus.
///
/// This script only *builds*; the corpus itself is computed by
/// Assets/Determinism/CorpusPlayerHarness.cs, which ships inside the player.
/// The split is the whole point. -executeMethod runs in the Unity Editor, and
/// the Editor executes managed code under Mono — a corpus emitted from an
/// -executeMethod harness would prove CoreCLR agrees with Editor-Mono while
/// IL2CPP, the runtime that actually ships, went untested. So the gate builds
/// a real player and runs that.
///
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod DeterminismHarness.BuildMacIl2CppPlayer \
///         -logFile - [-corpusPlayerOut <path/to/Name.app>]
public static class DeterminismHarness
{
    const string Tag = "[DeterminismHarness] ";
    const string OutFlag = "-corpusPlayerOut";

    /// An empty scene that exists only to give the player something to load.
    ///
    /// A no-scene build was tried first and Unity 6000.6 rejects it: passing an
    /// empty scenes array makes BuildPipeline fall back to the currently open
    /// scene, which in batchmode is an unsaved one, and the build dies with
    /// "Cannot build untitled scene." (BuildResult.Pending, 1 error). So the
    /// player gets one deliberately empty scene — no camera, no light, no
    /// renderers — rather than borrowing SampleScene, which would couple the
    /// determinism gate to unrelated URP rendering assets.
    ///
    /// The corpus itself still runs from a [RuntimeInitializeOnLoadMethod] at
    /// BeforeSplashScreen, i.e. before this scene loads; the scene is inert.
    const string ScenePath = "Assets/Determinism/CorpusHarness.unity";

    /// implementation/results/ is gitignored, which it needs to be: this build
    /// lands a ~120 MB .app next to a ~1.4 GB
    /// *_BackUpThisFolder_ButDontShipItWithYourGame/ of IL2CPP symbols.
    /// It is a build artifact; rebuilding is cheaper than storing it.
    const string DefaultOut = "../../implementation/results/il2cpp-player/BroodlineCorpus.app";

    public static void BuildMacIl2CppPlayer()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            throw new FileNotFoundException("harness scene missing: " + ScenePath);

        var outPath = Path.GetFullPath(Path.Combine(Application.dataPath,
            ReadArg(OutFlag) ?? DefaultOut));

        // Wipe first. A leftover .app from an earlier build would make the
        // "is this really IL2CPP?" evidence meaningless: stale Mono artifacts
        // could sit beside fresh IL2CPP ones and no inspection could tell.
        var outDir = Path.GetDirectoryName(outPath);
        if (Directory.Exists(outPath)) Directory.Delete(outPath, true);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneOSX)
        {
            Debug.Log(Tag + "switching active build target to StandaloneOSX...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new Exception("could not switch build target to StandaloneOSX — is Mac Build Support installed?");
        }
        EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Player;

        // Transient, deliberately. Task 1's brief forbids reconfiguring client/,
        // and both of these are PlayerSettings — they serialise into
        // ProjectSettings/ProjectSettings.asset (scriptingBackend and
        // platformArchitecture). We set what this one build needs and put the
        // previous values back in a finally, so a build that throws still
        // leaves the committed project settings as it found them.
        var prevBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
        var prevArch = PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone);
        Debug.Log(Tag + "saved standalone settings: backend=" + prevBackend + " architecture=" + prevArch);

        try
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, (int)OSArchitecture.ARM64);

            // Read them back rather than trusting the setters: this line is the
            // build log's own record of which runtime it is about to compile for.
            Debug.Log(Tag + "building with backend=" +
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) +
                " architecture=" + PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone) +
                " (OSArchitecture.ARM64=" + (int)OSArchitecture.ARM64 + ")");

            // Exactly one scene, and not EditorBuildSettings' list: the gate
            // must not start depending on whatever someone last ticked in the
            // Build Profiles window.
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                // BuildOptions.None == non-development: no profiler, no script
                // debugging, and the nondevelopment IL2CPP player variation.
                options = BuildOptions.None
            };

            Debug.Log(Tag + "building to " + outPath);
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            Debug.Log(string.Format(Tag + "result={0} errors={1} size={2:N0} bytes time={3}",
                s.result, s.totalErrors, s.totalSize, s.totalTime));

            if (s.result != BuildResult.Succeeded)
                throw new Exception("IL2CPP player build failed: " + s.result +
                                    " with " + s.totalErrors + " errors");

            ReportRuntimeArtifacts(outPath);
            Debug.Log(Tag + "DONE -> " + outPath);
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, prevBackend);
            PlayerSettings.SetArchitecture(NamedBuildTarget.Standalone, prevArch);
            AssetDatabase.SaveAssets();
            Debug.Log(Tag + "restored standalone settings: backend=" +
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) +
                " architecture=" + PlayerSettings.GetArchitecture(NamedBuildTarget.Standalone));
        }
    }

    /// Logs which runtime actually landed in the bundle. IL2CPP compiles the
    /// managed code to native and ships GameAssembly.dylib; a Mono player ships
    /// a MonoBleedingEdge/ tree and no GameAssembly. Naming both means the log
    /// distinguishes them rather than just asserting one.
    static void ReportRuntimeArtifacts(string appPath)
    {
        var gameAssembly = Path.Combine(appPath, "Contents/Frameworks/GameAssembly.dylib");
        var monoDir = Path.Combine(appPath, "Contents/Frameworks/MonoBleedingEdge");
        Debug.Log(Tag + "GameAssembly.dylib present=" + File.Exists(gameAssembly) +
                  "  MonoBleedingEdge/ present=" + Directory.Exists(monoDir));
    }

    /// Returns the value following <flag> on Unity's command line, or null.
    static string ReadArg(string flag)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag)
                return args[i + 1];
        return null;
    }
}
