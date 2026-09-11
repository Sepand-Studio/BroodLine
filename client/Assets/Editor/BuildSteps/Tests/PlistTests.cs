using NUnit.Framework;
using Broodline.EditorBuild;

namespace Broodline.EditorBuild.Tests
{
    /// The post-process had no test, which is how it shipped a transform that
    /// produced an Info.plist Apple's own parser refuses.
    ///
    /// These run without an iOS module, an Xcode project or a device, because
    /// WithFileSharingEnabled is a pure function over a string. That shape is
    /// the point: the thing worth checking is the bytes, and nothing about the
    /// bytes needs Unity.
    public class PlistTests
    {
        private const string Header =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" " +
            "\"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n";

        private static string Plist(string body) =>
            Header + "<plist version=\"1.0\">\n<dict>\n" + body + "</dict>\n</plist>\n";

        private const string Typical =
            "\t<key>CFBundleName</key>\n\t<string>BroodlineBench</string>\n";

        [Test]
        public void TheDoctypeSurvivesWithoutAnEmptyInternalSubset()
        {
            // THE regression. DtdProcessing.Parse reports the internal subset as
            // "" rather than null, and XmlWriter renders an empty-but-present
            // subset as `[]`, so line 2 came out ending `...PropertyList-1.0.dtd"[]>`.
            // Legal XML, illegal plist: plutil answers "Encountered unexpected
            // character [ on line 2 while parsing DTD" and refuses the whole
            // file - so every iOS build shipped an unreadable Info.plist, which
            // is worse than the missing keys the rewrite was meant to add.
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(Plist(Typical));

            Assert.IsFalse(result.Contains("[]"),
                "the DOCTYPE carries an empty internal subset - Apple's parser rejects the file");
            Assert.IsTrue(result.Contains("-//Apple//DTD PLIST 1.0//EN"),
                "the DOCTYPE was dropped entirely");
        }

        [Test]
        public void TheDeclarationSaysUtf8BecauseTheBytesAre()
        {
            // The second way to make line 1 unreadable. XmlWriter takes the
            // declared encoding from the TextWriter, and a plain StringWriter
            // reports UTF-16 - so the file said utf-16 over UTF-8 bytes and
            // plutil answered "Unexpected character at line 1". The `[]` fix
            // alone left this one standing, which is why the assertion is
            // separate from it.
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(Plist(Typical));

            Assert.IsFalse(result.Contains("utf-16"), "the declaration claims UTF-16");
            StringAssert.Contains("encoding=\"utf-8\"", result.ToLowerInvariant());
        }

        [Test]
        public void BothKeysAreSetAndNothingElseIsDisturbed()
        {
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(Plist(Typical));

            StringAssert.Contains("<key>UIFileSharingEnabled</key>", result);
            StringAssert.Contains("<key>LSSupportsOpeningDocumentsInPlace</key>", result);
            StringAssert.Contains("<key>CFBundleName</key>", result);
            StringAssert.Contains("<string>BroodlineBench</string>", result);
        }

        [Test]
        public void AnExistingValueIsReplacedRatherThanAppended()
        {
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(
                Plist("\t<key>UIFileSharingEnabled</key>\n\t<false />\n" + Typical));

            Assert.IsFalse(result.Contains("<false />") || result.Contains("<false/>"),
                "the old value is still present");
            Assert.AreEqual(1, Occurrences(result, "<key>UIFileSharingEnabled</key>"));
        }

        [Test]
        public void ADuplicateKeyIsCollapsedRatherThanLeftBehind()
        {
            // Duplicate keys are undefined in the format and CFPropertyList
            // takes the LAST - so leaving one behind meant the build log said
            // YES while the app saw the old value.
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(
                Plist("\t<key>UIFileSharingEnabled</key>\n\t<false />\n" + Typical +
                      "\t<key>UIFileSharingEnabled</key>\n\t<false />\n"));

            Assert.AreEqual(1, Occurrences(result, "<key>UIFileSharingEnabled</key>"),
                "a duplicate key survived, and the last one wins in CFPropertyList");
            Assert.IsFalse(result.Contains("<false />") || result.Contains("<false/>"));
        }

        [Test]
        public void AKeyWithNoValueDoesNotEatTheNextEntry()
        {
            // A malformed dict whose key is followed directly by another key.
            // Replacing "the element after" destroyed CFBundleName outright.
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(
                Plist("\t<key>UIFileSharingEnabled</key>\n" + Typical));

            StringAssert.Contains("<key>CFBundleName</key>", result);
            StringAssert.Contains("<string>BroodlineBench</string>", result);
            StringAssert.Contains("<true />", result);
        }

        [Test]
        public void AMatchingKeyInsideANestedDictIsLeftAlone()
        {
            var result = IosFileSharingPostProcess.WithFileSharingEnabled(
                Plist("\t<key>Nested</key>\n\t<dict>\n\t\t<key>UIFileSharingEnabled</key>\n" +
                      "\t\t<false />\n\t</dict>\n"));

            // The nested false is untouched; the top-level key is added.
            Assert.AreEqual(2, Occurrences(result, "<key>UIFileSharingEnabled</key>"));
            StringAssert.Contains("<false />", result);
        }

        private static int Occurrences(string haystack, string needle)
        {
            int n = 0, at = 0;
            while ((at = haystack.IndexOf(needle, at, System.StringComparison.Ordinal)) >= 0)
            { n++; at += needle.Length; }
            return n;
        }
    }
}
