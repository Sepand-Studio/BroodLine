using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
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
///   UIFileSharingEnabled              - the container is exposed at all
///   LSSupportsOpeningDocumentsInPlace - it is browsable rather than a copy
///
/// Both are development conveniences on a development build. They should be
/// reconsidered before anything ships to a real player: a shipped app that
/// exposes its Documents directory is handing the player its own save data.
///
/// NO `#if UNITY_IOS`, AND NO UnityEditor.iOS.Xcode. Those two facts are the
/// same decision.
///
/// The guard cannot be UNITY_IOS: that symbol follows the ACTIVE build target,
/// not the target being built, and WaveBuilder.BuildIOS explicitly supports
/// starting from another one - it calls SwitchActiveBuildTarget and then builds
/// while the domain reload is still deferred, so the callback would not be
/// registered at all. On a fresh clone, or a machine that just ran
/// cross-runtime-diff.sh (which switches to StandaloneOSX), the keys would be
/// silently absent and the build would still report Succeeded.
///
/// But removing the guard left `using UnityEditor.iOS.Xcode` unconditional in a
/// file with no asmdef of its own, which puts it in Assembly-CSharp-Editor on
/// EVERY platform. On a machine without the iOS Build Support module that
/// namespace does not resolve and the whole editor assembly fails - taking
/// WaveSceneBuilder and DeterminismHarness with it, so run-unity-tests.sh and
/// cross-runtime-diff.sh die with an unrelated missing-assembly error. A runner
/// provisioned to determinism.yml's own stated spec - "macOS Build Support
/// (IL2CPP)" - is exactly such a machine.
///
/// So the dependency is gone instead. Two booleans in an XML file do not need
/// Apple's plist library; System.Xml.Linq is in the same .NET profile
/// everywhere Unity runs. The runtime target check below is the whole guard,
/// and it is correct on every host.
public static class IosFileSharingPostProcess
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        if (!File.Exists(plistPath))
        {
            Debug.LogError("[IosFileSharingPostProcess] no Info.plist at " + plistPath +
                           " - the Documents directory will not be reachable from the host.");
            return;
        }

        // DtdProcessing.Parse with a null resolver: a plist carries Apple's
        // external DOCTYPE, which XDocument's default (Prohibit) rejects
        // outright. Parse keeps the declaration in the tree so it survives the
        // round trip, and a null resolver means the DTD is never fetched.
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
        XDocument doc;
        using (var reader = XmlReader.Create(plistPath, settings))
            doc = XDocument.Load(reader);

        var dict = doc.Root?.Element("dict");
        if (dict == null)
        {
            Debug.LogError("[IosFileSharingPostProcess] " + plistPath +
                           " has no top-level <dict> - refusing to guess at its shape.");
            return;
        }

        SetBoolean(dict, "UIFileSharingEnabled", true);
        SetBoolean(dict, "LSSupportsOpeningDocumentsInPlace", true);

        doc.Save(plistPath);
        Debug.Log("[IosFileSharingPostProcess] UIFileSharingEnabled and " +
                  "LSSupportsOpeningDocumentsInPlace -> YES in " + plistPath);
    }

    /// A plist dict is a flat sequence of alternating key and value elements,
    /// so "the value of a key" is the element immediately after it. Replaces an
    /// existing entry rather than appending a second one with the same key -
    /// a duplicate key is undefined behaviour in the format, and which one wins
    /// is a parser detail nobody should be relying on.
    private static void SetBoolean(XElement dict, string name, bool value)
    {
        var element = new XElement(value ? "true" : "false");

        var key = dict.Elements("key").FirstOrDefault(k => k.Value == name);
        if (key != null)
        {
            var existing = key.ElementsAfterSelf().FirstOrDefault();
            if (existing != null) { existing.ReplaceWith(element); return; }
            key.AddAfterSelf(element);
            return;
        }

        dict.Add(new XElement("key", name), element);
    }
}
