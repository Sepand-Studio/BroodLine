using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Builds an Xcode project for the iOS SIMULATOR, which `BootBuilder` does not.
///
/// SEPARATE FILE, DELIBERATELY. `BootBuilder`'s own header warns that moving
/// its entry point silently breaks the `-executeMethod BootBuilder.BuildIOS`
/// command that the TestFlight procedure in the phase plan cites verbatim.
/// A second method on that class would not move it, but a second file cannot
/// even be mistaken for doing so, and this one is a development convenience
/// rather than part of the release path.
///
/// WHY IT DUPLICATES SIX LINES RATHER THAN CALLING `BootBuilder`:
/// `RequireApiBaseUrl` and `WriteConfig` are private there. Widening them for
/// a convenience build would change the release builder's surface to serve a
/// non-release caller, which is the wrong direction. The duplication is the
/// env var read and the config write; both are named below so a change to
/// either is findable from here.
///
/// THE TRAP THIS FILE EXISTS TO CLOSE, and it is the reason for the `finally`:
/// `PlayerSettings.iOS.sdkVersion` is PROJECT STATE, not a build argument. A
/// simulator build that left it set would make the NEXT device build - the
/// TestFlight one - produce a simulator binary, which uploads and then fails
/// App Store Connect processing with a message that says nothing about SDKs.
/// So it is restored whether the build succeeds, fails, or throws.
public static class SimulatorBuilder
{
    const string BootScene = "Assets/Scenes/Boot.unity";
    const string WaveScene = "Assets/Scenes/Wave.unity";
    const string ApiUrlEnvar = "BROODLINE_API_URL";

    /// Usage:
    ///
    ///   export BROODLINE_API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
    ///   "/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" \
    ///     -batchmode -quit -projectPath "$(pwd)/client" \
    ///     -executeMethod SimulatorBuilder.BuildIOSSimulator \
    ///     -logFile implementation/results/ios-simulator-build.log
    public static void BuildIOSSimulator() => Build(development: false);

    /// The same build with `BuildOptions.Development`, to `build/ios-simulator-dev`.
    ///
    /// WHY IT EXISTS: a Release IL2CPP player strips the managed line-number
    /// tables, so every frame of a runtime stack trace reads `[0x00000]` with
    /// no file and no line, and a throw from inside an inlined static reads as
    /// though it came from its caller. A development player keeps them, which
    /// is the difference between "something under BootController.Start threw"
    /// and the file and line that did. It is a DIAGNOSTIC build and nothing
    /// else: it is never the TestFlight artifact, and its separate output
    /// directory keeps it from overwriting the release simulator project.
    ///
    ///   -executeMethod SimulatorBuilder.BuildIOSSimulatorDevelopment
    public static void BuildIOSSimulatorDevelopment() => Build(development: true);

    static void Build(bool development)
    {
        var apiBaseUrl = Environment.GetEnvironmentVariable(ApiUrlEnvar);
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new Exception(
                ApiUrlEnvar + " is not set. A build with no API base URL boots to a blank " +
                "screen and looks like a rendering bug.\n  export " + ApiUrlEnvar +
                "=\"$(terraform -chdir=infra/terraform output -raw api_url)\"");

        foreach (var scene in new[] { BootScene, WaveScene })
            if (!File.Exists(scene))
                throw new FileNotFoundException("scene missing: " + scene);

        // The same file `BootController` reads at runtime through
        // `Resources.Load<TextAsset>("BroodlineConfig")`.
        var configPath = Path.Combine(Application.dataPath, "Resources/BroodlineConfig.json");
        File.WriteAllText(configPath, "{\n  \"apiBaseUrl\": \"" + apiBaseUrl + "\"\n}\n");

        var outDir = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            development ? "../../build/ios-simulator-dev" : "../../build/ios-simulator"));
        Directory.CreateDirectory(outDir);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            Debug.Log("[SimulatorBuilder] switching active build target to iOS...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new Exception("could not switch to iOS — is the iOS module installed?");
        }

        var restoreSdk = PlayerSettings.iOS.sdkVersion;
        try
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;

            Debug.Log("[SimulatorBuilder] building to " + outDir +
                      "  api=" + apiBaseUrl + "  sdk=" + PlayerSettings.iOS.sdkVersion +
                      "  development=" + development);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { BootScene, WaveScene },
                locationPathName = outDir,
                target = BuildTarget.iOS,
                options = development ? BuildOptions.Development : BuildOptions.None,
            });

            var s = report.summary;
            Debug.Log(string.Format(
                "[SimulatorBuilder] result={0} errors={1} size={2:N0} bytes time={3}",
                s.result, s.totalErrors, s.totalSize, s.totalTime));

            if (s.result != BuildResult.Succeeded)
                throw new Exception("simulator build did not succeed: " + s.result +
                                    " with " + s.totalErrors + " error(s)");

            Debug.Log("[SimulatorBuilder] DONE -> " +
                      Path.Combine(outDir, "Unity-iPhone.xcodeproj"));
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = restoreSdk;
            Debug.Log("[SimulatorBuilder] sdkVersion restored to " + restoreSdk +
                      " — the next device build is unaffected.");
        }
    }
}
