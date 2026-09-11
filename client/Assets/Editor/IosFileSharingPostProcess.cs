using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// Makes the app's Documents directory reachable from the host.
///
/// Unity exposes no PlayerSettings field for either of these keys, so they have
/// to be written into the generated Info.plist after the Xcode project exists.
///
/// This is a convenience, not a requirement. Phase 0 retrieved the sweep CSV
/// from a Development build through Xcode's Download Container, which needs
/// neither key, and that remains the route to try first because it is the one
/// already proven here. What these two buy is the COMMAND-LINE route -
/// `xcrun devicectl device copy from --domain-type appDataContainer` and the
/// Files app - which matters because Phase 3's artifact is pulled once per
/// engine change rather than once ever.
///
///   UIFileSharingEnabled            - the container is exposed at all
///   LSSupportsOpeningDocumentsInPlace - it is browsable rather than a copy
///
/// Both are development conveniences on a development build. They should be
/// reconsidered before anything ships to a real player: a shipped app that
/// exposes its Documents directory is handing the player its own save data.
///
/// NOT guarded by #if UNITY_IOS. That symbol follows the ACTIVE build target,
/// not the target being built, and WaveBuilder.BuildIOS explicitly supports
/// starting from another one - it calls SwitchActiveBuildTarget and then builds
/// while the domain reload is still deferred, so the callback would not be
/// registered at all. On any machine not already on iOS - a fresh clone, or one
/// that just ran cross-runtime-diff.sh, which switches to StandaloneOSX - the
/// plist keys would be silently absent and the build would still report
/// Succeeded. The runtime target check below is what makes the guard correct.
/// UnityEditor.iOS.Xcode ships with the iOS module, which verify-prereqs.sh
/// already asserts is installed.
public static class IosFileSharingPostProcess
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = System.IO.Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        plist.root.SetBoolean("UIFileSharingEnabled", true);
        plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

        plist.WriteToFile(plistPath);
        Debug.Log("[IosFileSharingPostProcess] UIFileSharingEnabled and " +
                  "LSSupportsOpeningDocumentsInPlace -> YES in " + plistPath);
    }
}
