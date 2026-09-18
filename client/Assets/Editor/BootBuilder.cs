using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder and BenchmarkBuilder.
// `-executeMethod` takes the type name exactly as written, so a namespace here
// would silently move the entry point to `Broodline.X.BootBuilder.BuildIOS` and
// the batch invocation below would stop resolving.

/// Builds the RELEASE iOS Xcode project - Boot plus Wave, non-development,
/// with the API base URL baked in.
///
///   export BROODLINE_API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod BootBuilder.BuildIOS -logFile &lt;path&gt;
///
/// The third near-copy of BenchmarkBuilder, for the reason WaveBuilder's header
/// gives: the two development builders differ from this one in more than a
/// scene list, and BenchmarkBuilder is still the measuring instrument behind a
/// delivered render budget. Only `ConfigureSigning` is shared, because a second
/// identity is the one thing none of the three wants.
///
/// WHAT MAKES THIS ONE DIFFERENT FROM THE OTHER TWO, all four deliberate:
///
/// 1. `BuildOptions.None`, NOT `Development`. A development build carries
///    get-task-allow and managed stack traces, neither of which belongs in
///    something a player installs. It is also what makes
///    `IosFileSharingPostProcess` stand down - see point 4.
///
/// 2. TWO SCENES. Boot is the entry point; Wave has to be in the build because
///    the shell routes into it, and a scene absent from the build settings
///    list cannot be loaded at runtime no matter what references it.
///
/// 3. THE API URL IS REQUIRED AND IS NOT INFERRED. `BroodlineConfig.json` is a
///    TRACKED file whose committed value is `http://127.0.0.1:8080`, which is
///    a laptop. A release that silently inherits it is installable, launches,
///    and can never reach an API - and nothing about the build, the archive or
///    the upload would say so. So the environment variable is mandatory, and
///    the loopback spellings throw as loudly as an unset one: an empty string
///    is not the only way to point a shipped build at the developer's machine.
///    THE COMMITTED DEFAULT IS ITSELF ONE OF THE REJECTED VALUES, which is the
///    whole argument for checking more than `localhost`.
///
///    This overwrites that tracked file. That is intended - the Phase 7 plan
///    that introduced it says so in as many words - so a release build leaves
///    the working tree dirty on one line, and the deployed URL is what gets
///    committed or reverted by hand afterwards.
///
/// 4. NO FILE-SHARING PLIST KEYS. `IosFileSharingPostProcess` writes
///    `UIFileSharingEnabled` and `LSSupportsOpeningDocumentsInPlace` so the
///    Documents directory can be pulled off a device; its own header says both
///    should be reconsidered before anything ships to a real player, because a
///    shipped app that exposes its Documents directory is handing the player
///    its own save data. That file now returns early unless
///    `EditorUserBuildSettings.development`, and this is the build that made
///    the distinction necessary.
///
///    `EditorUserBuildSettings.development` is the flag `BuildPipeline
///    .BuildPlayer` sets from `BuildPlayerOptions.options`, so passing
///    `BuildOptions.None` here is what stands the post-process down. It is
///    logged either side of the build below rather than assumed, because the
///    failure mode - a shipped Info.plist with both keys - is invisible in the
///    build summary and visible only in the generated project.
public static class BootBuilder
{
    const string BootScene = "Assets/Scenes/Boot.unity";
    const string WaveScene = "Assets/Scenes/Wave.unity";

    const string ConfigPath  = "Assets/Resources/BroodlineConfig.json";
    const string ApiUrlEnvar = "BROODLINE_API_URL";

    /// Spellings of "this machine". `localhost` is the one the plan names; the
    /// other three are the same mistake typed differently, and `127.0.0.1` is
    /// the value sitting in the committed config file right now.
    static readonly string[] Loopback = { "localhost", "127.0.0.1", "0.0.0.0", "[::1]" };

    /// Mirrors `BootController.ClientConfig`, so the bytes written here are
    /// produced by the same serializer that reads them back. Hand-rolling the
    /// JSON would mean hand-rolling the escaping, and a URL is exactly the kind
    /// of string that eventually contains something worth escaping.
    [Serializable]
    class ClientConfig
    {
        public string apiBaseUrl;
    }

    [MenuItem("Broodline/Build Release iOS Xcode Project")]
    public static void BuildIOS()
    {
        // EVERY CHECK BEFORE EVERY MUTATION. The guard below is the point of
        // this builder, and a guard that throws after it has already rewritten
        // a tracked config file and the project's signing settings makes the
        // failing run more expensive than the passing one.
        var apiBaseUrl = RequireApiBaseUrl();

        foreach (var scene in new[] { BootScene, WaveScene })
            if (!File.Exists(scene))
                throw new FileNotFoundException(
                    "release scene missing: " + scene +
                    " — run Broodline > Build Boot Scene / Build Wave Scene first");

        // Baked, not fetched: the app reads this out of Resources at cold
        // start (BootController.LoadApiBaseUrl), so the URL has to be in the
        // player before the player exists.
        WriteConfig(apiBaseUrl);

        // Same bundle id, same team, same wildcard profile as the two
        // development builders. productName is set AFTER, because
        // ConfigureSigning sets it to "Broodline Bench" — this is a player's
        // home screen, not a measuring instrument.
        BenchmarkBuilder.ConfigureSigning();
        PlayerSettings.productName = "Broodline";

        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/ios-release"));
        Directory.CreateDirectory(outDir);

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
        {
            Debug.Log("[BootBuilder] switching active build target to iOS...");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new Exception("could not switch build target to iOS — is the iOS module installed?");
        }

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { BootScene, WaveScene },
            locationPathName = outDir,
            target = BuildTarget.iOS,
            options = BuildOptions.None   // NOT Development. See point 1 above.
        };

        Debug.Log("[BootBuilder] building to " + outDir +
                  "  api=" + apiBaseUrl +
                  "  development(before)=" + EditorUserBuildSettings.development);

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;

        // Logged, not inferred. If this reads True after a BuildOptions.None
        // build then BuildPlayer did not take the flag from the options, and
        // IosFileSharingPostProcess wrote the two keys into a build meant for
        // a player - which the build summary would report as Succeeded.
        Debug.Log(string.Format(
            "[BootBuilder] result={0} errors={1} size={2:N0} bytes time={3} development(after)={4}",
            s.result, s.totalErrors, s.totalSize, s.totalTime, EditorUserBuildSettings.development));

        if (s.result != BuildResult.Succeeded)
            throw new Exception("iOS release build failed: " + s.result + " with " + s.totalErrors + " errors");

        Debug.Log("[BootBuilder] DONE -> " + Path.Combine(outDir, "Unity-iPhone.xcodeproj"));
    }

    /// Reads BROODLINE_API_URL, or throws with the command that sets it.
    ///
    /// Trimmed before it is judged and before it is written: a URL with a
    /// trailing newline passes every check here and then fails at runtime as
    /// an unroutable host, which is a bad way to spend a TestFlight round trip.
    static string RequireApiBaseUrl()
    {
        var raw = Environment.GetEnvironmentVariable(ApiUrlEnvar);
        var url = raw == null ? null : raw.Trim();

        const string HowToSet =
            "\n  export BROODLINE_API_URL=\"$(terraform -chdir=infra/terraform output -raw api_url)\"";

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException(
                "[BootBuilder] " + ApiUrlEnvar + " is not set. A release build bakes the API base " +
                "URL into " + ConfigPath + ", and this builder will not fall back to whatever that " +
                "tracked file happens to hold — its committed value is http://127.0.0.1:8080, which " +
                "is a laptop." + HowToSet);

        foreach (var host in Loopback)
            if (url.IndexOf(host, StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException(
                    "[BootBuilder] " + ApiUrlEnvar + " points at this machine: \"" + url + "\" " +
                    "contains \"" + host + "\". A release build that points at a laptop installs, " +
                    "launches, and can never reach an API — and nothing in the build, the archive " +
                    "or the upload would say so. That is the failure this check exists for." + HowToSet);

        return url;
    }

    /// Writes ConfigPath and re-imports it, so the TextAsset the player embeds
    /// is the bytes written here rather than the ones the AssetDatabase last
    /// happened to cache.
    static void WriteConfig(string apiBaseUrl)
    {
        var json = JsonUtility.ToJson(new ClientConfig { apiBaseUrl = apiBaseUrl }, true);
        var full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ConfigPath));

        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, json + "\n", new System.Text.UTF8Encoding(false));
        AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceUpdate);

        Debug.Log("[BootBuilder] " + ConfigPath + " apiBaseUrl=" + apiBaseUrl);
    }
}
