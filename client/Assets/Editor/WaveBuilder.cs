using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Builds the Phase 3 wave's iOS Xcode project.
///
/// Deliberately a near-copy of BenchmarkBuilder rather than a generalisation of
/// it. The two differ only in the scene, but BenchmarkBuilder is the measuring
/// instrument behind a delivered render budget, and refactoring it to serve a
/// second caller risks changing what Phase 0 measured with. They will diverge
/// or one will be deleted; either is fine and neither wants a shared base.
///
///   Unity -batchmode -quit -projectPath client -executeMethod WaveBuilder.BuildIOS
public static class WaveBuilder
{
    const string ScenePath = "Assets/Scenes/Wave.unity";

    [MenuItem("Broodline/Build Wave iOS Xcode Project")]
    public static void BuildIOS()
    {
        // Signing is shared with the benchmark: same bundle id, same team, same
        // wildcard profile. Nothing about Phase 3 wants a second identity.
        BenchmarkBuilder.ConfigureSigning();

        if (!File.Exists(ScenePath))
            throw new FileNotFoundException(
                "wave scene missing: " + ScenePath + " — run Broodline > Build Wave Scene first");

        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/ios-wave"));
        Directory.CreateDirectory(outDir);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            Debug.Log("[WaveBuilder] switching active build target to iOS...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new Exception("could not switch build target to iOS — is the iOS module installed?");
        }

        // Development build, for the reason BenchmarkBuilder gives: it keeps the
        // managed stack traces that make a throw on device diagnosable, and it
        // is what lets Xcode's Download Container reach the app's Documents
        // directory - which is where WaveRunner writes replay.bin. Phase 0
        // retrieved the sweep CSV exactly this way; it is the proven route off
        // a device in this project, and the one to try before devicectl.
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.iOS,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        Debug.Log("[WaveBuilder] building to " + outDir);
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log(string.Format("[WaveBuilder] result={0} errors={1} size={2:N0} bytes time={3}",
            s.result, s.totalErrors, s.totalSize, s.totalTime));

        if (s.result != BuildResult.Succeeded)
            throw new Exception("iOS build failed: " + s.result + " with " + s.totalErrors + " errors");

        Debug.Log("[WaveBuilder] DONE -> " + Path.Combine(outDir, "Unity-iPhone.xcodeproj"));
    }
}
