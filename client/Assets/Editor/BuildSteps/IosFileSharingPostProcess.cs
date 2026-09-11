using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Broodline.EditorBuild
{
    /// Makes the app's Documents directory reachable from the host.
    ///
    /// Unity exposes no PlayerSettings field for either of these keys, so they
    /// have to be written into the generated Info.plist after the Xcode project
    /// exists.
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
    /// THREE THINGS ABOUT HOW THIS IS BUILT, each the correction of a previous
    /// attempt.
    ///
    /// 1. No `#if UNITY_IOS`. That symbol follows the ACTIVE build target, not
    ///    the target being built, and WaveBuilder.BuildIOS explicitly supports
    ///    starting from another one - it calls SwitchActiveBuildTarget and then
    ///    builds while the domain reload is still deferred, so the callback
    ///    would not be registered at all. The runtime target check is the guard.
    ///
    /// 2. Its OWN assembly. Removing the `#if` left a hard reference to the iOS
    ///    module in a file with no asmdef, which puts it in
    ///    Assembly-CSharp-Editor on every platform - so on a machine without
    ///    iOS Build Support the whole editor assembly failed to resolve, taking
    ///    WaveSceneBuilder and DeterminismHarness with it and breaking
    ///    run-unity-tests.sh and cross-runtime-diff.sh with an unrelated error.
    ///    Dropping the dependency fixed the symptom; the cause was that five
    ///    unrelated editor scripts shared one unbounded assembly. Now a
    ///    reference that cannot resolve fails THIS assembly and nothing else.
    ///
    /// 3. No UnityEditor.iOS.Xcode. Two booleans in an XML file do not need
    ///    Apple's plist library, and the transform below is a pure function over
    ///    a string, which is what lets PlistTests assert it without an iOS
    ///    module, an Xcode project, or a device.
    public static class IosFileSharingPostProcess
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");

            // A throw, not Debug.LogError. A LogError during BuildPlayer does
            // not move summary.result off Succeeded, and WaveBuilder.BuildIOS
            // gates only on that - so the previous early-returns produced
            // exactly the "keys silently absent, build reports Succeeded"
            // outcome this file's own rationale condemns.
            if (!File.Exists(plistPath))
                throw new BuildFailedException(
                    "[IosFileSharingPostProcess] no Info.plist at " + plistPath +
                    " - the Documents directory would not be reachable from the host.");

            string original;
            try { original = File.ReadAllText(plistPath); }
            catch (IOException e)
            { throw new BuildFailedException("[IosFileSharingPostProcess] could not read " + plistPath + ": " + e.Message); }

            string updated;
            try { updated = WithFileSharingEnabled(original); }
            catch (XmlException e)
            {
                // The one format assumption worth naming. Unity emits an XML
                // plist here, but Apple's tooling can also write the binary
                // form, and PlistDocument used to absorb that difference. An
                // unhandled XmlException out of a [PostProcessBuild] callback
                // is a bad way to find out.
                throw new BuildFailedException(
                    "[IosFileSharingPostProcess] " + plistPath + " is not XML - a binary plist " +
                    "cannot be edited here. Convert it with `plutil -convert xml1`, or restore " +
                    "the UnityEditor.iOS.Xcode dependency. Parser said: " + e.Message);
            }

            // UTF-8 with no BOM. A BOM ahead of the declaration is the other
            // way to make Apple's parser reject line 1.
            File.WriteAllText(plistPath, updated, new System.Text.UTF8Encoding(false));
            Debug.Log("[IosFileSharingPostProcess] UIFileSharingEnabled and " +
                      "LSSupportsOpeningDocumentsInPlace -> YES in " + plistPath);
        }

        /// The whole transform, as a pure function, so it can be tested.
        ///
        /// Returns plist XML with both keys set to true.
        public static string WithFileSharingEnabled(string plistXml)
        {
            // DtdProcessing.Parse with a null resolver: a plist carries Apple's
            // external DOCTYPE, which XDocument's default (Prohibit) rejects
            // outright. Parse keeps the declaration so it survives the round
            // trip, and a null resolver means the DTD is never fetched.
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
            XDocument doc;
            using (var reader = XmlReader.Create(new StringReader(plistXml), settings))
                doc = XDocument.Load(reader);

            // THE LINE THIS FILE EXISTS TO GET RIGHT.
            //
            // DtdProcessing.Parse reports the internal DTD subset as "" rather
            // than null, and XmlWriter renders an empty-but-present subset as
            // `[]`. So the saved file's line 2 came out as
            //   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "…dtd"[]>
            // which is legal XML and ILLEGAL plist: `plutil -lint` answers
            // "Encountered unexpected character [ on line 2 while parsing DTD".
            // Every iOS build shipped an Info.plist Apple could not read at
            // all - strictly worse than the missing keys this replaced, and
            // invisible to every XML parser that is not Apple's.
            if (doc.DocumentType != null && doc.DocumentType.InternalSubset == "")
                doc.DocumentType.InternalSubset = null;

            var dict = doc.Root?.Element("dict");
            if (dict == null)
                throw new XmlException("plist has no top-level <dict>");

            SetBoolean(dict, "UIFileSharingEnabled", true);
            SetBoolean(dict, "LSSupportsOpeningDocumentsInPlace", true);

            // A StringWriter reports UTF-16, and XmlWriter takes the
            // declaration from the writer's encoding - so the naive
            // `new StringWriter()` produced `<?xml version="1.0"
            // encoding="utf-16"?>` above UTF-8 bytes, and Apple's parser
            // answered "Unexpected character at line 1". Same class of bug as
            // the `[]`: legal to every XML library, rejected by the only reader
            // that matters.
            var output = new Utf8StringWriter();
            doc.Save(output);
            return output.ToString();
        }

        /// A StringWriter that admits to being UTF-8, so the XML declaration
        /// says what the bytes actually are.
        private sealed class Utf8StringWriter : StringWriter
        {
            public override System.Text.Encoding Encoding =>
                new System.Text.UTF8Encoding(false);
        }

        /// A plist dict is a flat sequence of alternating key and value
        /// elements, so "the value of a key" is the element immediately after
        /// it - unless that element is itself a key, in which case the entry is
        /// malformed and has no value.
        ///
        /// Handles two shapes the first version got wrong. A DUPLICATE top-level
        /// key left the extra behind, and CFPropertyList takes the LAST value -
        /// so the build logged YES while the app saw the old one. And a key
        /// whose successor is another key had that successor REPLACED, deleting
        /// an unrelated entry outright.
        private static void SetBoolean(XElement dict, string name, bool value)
        {
            XElement kept = null;

            foreach (var key in dict.Elements("key").Where(k => k.Value == name).ToList())
            {
                var next = key.ElementsAfterSelf().FirstOrDefault();
                var existingValue = (next != null && next.Name.LocalName != "key") ? next : null;
                existingValue?.Remove();

                if (kept == null)
                {
                    kept = key;
                    key.AddAfterSelf(new XElement(value ? "true" : "false"));
                }
                else
                {
                    key.Remove();   // a duplicate: the format leaves it undefined
                }
            }

            if (kept == null)
                dict.Add(new XElement("key", name), new XElement(value ? "true" : "false"));
        }
    }
}
