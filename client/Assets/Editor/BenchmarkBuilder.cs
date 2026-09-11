using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Builds the benchmark's iOS Xcode project.
///
/// Scripted rather than driven through the editor UI: Unity moved the build
/// window between versions (Build Settings, then Build Profiles), so menu
/// instructions rot. BuildPipeline.BuildPlayer is stable across both.
///
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod BenchmarkBuilder.BuildIOS
public static class BenchmarkBuilder
{
    const string ScenePath = "Assets/Scenes/Benchmark.unity";

    // Unity's URP template leaves applicationIdentifier as
    // com.Unity-Technologies.com.unity.template.urp-blank — Unity's own id, with
    // a malformed double "com.". No team can provision it, so signing has
    // nothing to attach to and Xcode's Signing & Capabilities looks broken.
    const string BundleId = "com.sepandstudio.broodlinebench";
    const string TeamId   = "R4Z6W7AW86";

    /// Configures signing in Unity so the generated Xcode project needs no
    /// manual step. The team holds a wildcard profile (R4Z6W7AW86.*), so any
    /// identifier under it provisions automatically.
    [MenuItem("Broodline/Configure iOS Signing")]
    public static void ConfigureSigning()
    {
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
        PlayerSettings.iOS.appleDeveloperTeamID = TeamId;
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.companyName = "Sepand Studio";
        PlayerSettings.productName = "Broodline Bench";
        AssetDatabase.SaveAssets();
        Debug.Log("[BenchmarkBuilder] bundle=" + BundleId + " team=" + TeamId + " automatic signing on");
    }

    [MenuItem("Broodline/Build iOS Xcode Project")]
    public static void BuildIOS()
    {
        ConfigureSigning();

        if (!File.Exists(ScenePath))
            throw new FileNotFoundException("benchmark scene missing: " + ScenePath);

        // build/ is gitignored at the repo root; the Xcode project is a build
        // artifact and regenerating it is cheaper than storing it.
        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/ios"));
        Directory.CreateDirectory(outDir);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            Debug.Log("[BenchmarkBuilder] switching active build target to iOS...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new Exception("could not switch build target to iOS — is the iOS module installed?");
        }

        // Development build: keeps the managed stack traces that make a throw on
        // device diagnosable, and is required for Xcode's Download Container to
        // reach the CSV the sweep writes.
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.iOS,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        Debug.Log("[BenchmarkBuilder] building to " + outDir);
        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log(string.Format("[BenchmarkBuilder] result={0} errors={1} size={2:N0} bytes time={3}",
            s.result, s.totalErrors, s.totalSize, s.totalTime));

        if (s.result != BuildResult.Succeeded)
            throw new Exception("iOS build failed: " + s.result + " with " + s.totalErrors + " errors");

        Debug.Log("[BenchmarkBuilder] DONE -> " + Path.Combine(outDir, "Unity-iPhone.xcodeproj"));
    }
}
