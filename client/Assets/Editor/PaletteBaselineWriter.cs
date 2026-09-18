using System.Globalization;
using System.IO;
using System.Linq;
using Broodline.UI.Diagnostics;
using UnityEditor;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder.
//
/// Writes palette-cvd-baseline.txt from the SAME PaletteContrast.Measure()
/// the test reads, so the file can never disagree with the code that checks
/// it. The emit-corpus-baseline.sh pattern, in C#.
///
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod PaletteBaselineWriter.Write -logFile <path>
public static class PaletteBaselineWriter
{
    [MenuItem("Broodline/Write Palette CVD Baseline")]
    public static void Write()
    {
        var path = Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath, "../../implementation/results/palette-cvd-baseline.txt"));
        // INVARIANT CULTURE, BECAUSE THE TEST PARSES IT THAT WAY.
        // `PaletteContrastTests.ReadBaseline` uses CultureInfo.InvariantCulture,
        // so writing in the Editor's culture - which follows the OS and is not
        // forced to invariant - puts "83,2" in this file on any comma-decimal
        // locale. double.Parse then reads that as NumberStyles.Float |
        // AllowThousands, does not validate group size, and returns 832: every
        // pinned value inflated tenfold, all 45 pairs reported as regressed,
        // and a reader sent hunting a palette change that never happened.
        var rows = PaletteContrast.Measure()
                                  .Select(kv => string.Format(CultureInfo.InvariantCulture,
                                      "{0}\t{1:F1}\t{2:F1}", kv.Key, kv.Value.DeltaL, kv.Value.DeltaE));
        File.WriteAllText(path, Header + string.Join("\n", rows) + "\n");
        UnityEngine.Debug.Log($"wrote {path}");
    }

    const string Header = @"# Species palette under colour-vision deficiency - DATA, NOT PROSE.
#
# Columns: pair, deficiency, dL*, dE76.
#
# Both are CIE L*a*b* distances between the two species colours as that
# viewer sees them, at severity 1.0 (Machado, Oliveira & Fernandes 2009,
# applied in LINEAR RGB - see PaletteContrast.cs for why the gamma-space
# variant, which the Phase 8 plan drafted, is a known and named defect).
#
#   dE76 is the CONFUSABILITY channel. Lower is worse. Roughly: 2.3 is the
#        just-noticeable difference, under 10 is 'similar at a glance',
#        over 25 is 'obviously different colours'. THIS is the column to
#        read when asking whether two species can be told apart.
#   dL*  is the GREYSCALE channel - what survives with colour removed
#        entirely. It does NOT rank confusability and must not be read as
#        though it does: Vetch/Ember under tritanopia is dL* 0.1 and dE76
#        83.2, which is two equally-bright and completely different colours.
#        It is kept because a repaint that held hue while collapsing value
#        would be invisible to dE alone, and because it is the channel
#        bible 10.2's silhouette rule actually leans on.
#
# THIS FILE IS A BASELINE, NOT A TARGET. See PaletteContrastTests for why a
# threshold is not available: 45 constraints against 6 free colours, and the
# worst pair - Vetch/Loam under tritanopia at dE76 10.5 - is one that deleting
# Pale outright would not improve either. Roughly dE 10-12 is the floor for six
# saturated hues in this family. The test fails when a number here gets WORSE
# on either channel, which is the question a change can actually answer.
#
# Regenerate: Unity -> Broodline/Write Palette CVD Baseline, or
#   Unity -executeMethod PaletteBaselineWriter.Write
#
# WHAT THIS FILE SAYS ABOUT broodline_accessibility.md section 4:
#   Ember/Loam     deutan  dE76 11.0  <- section 4 is RIGHT about this one.
#   Vetch/Hollow   deutan  dE76 26.9  <- section 4 says this collapses. It does not.
#   Skitter/Loam   tritan  dE76 63.0  <- section 4 says these 'move closer'. They do not.
#   Vetch/Pale     protan  dE76 17.3  <- WAS 7.2 and the worst pair in the palette,
#                                        unmentioned by section 4 and by the plan,
#                                        and the closest pair (17.7) at normal vision
#                                        too, where nothing else was under 43. Phase 8
#                                        Task 6 moved Pale #a9b0c4 -> #c6cede for this
#                                        one pair; normal vision is now 23.9.
#
# PALE IS THEREFORE NOT THE HANDOFF'S VALUE, and bible 1.2, accessibility.md
# section 4 and the handoff README are all stale on that one row until Task 19
# records the decision. Tokens.uss --slate and PaletteContrast.Species are the
# live values, and PaletteContrastTests asserts they cannot drift apart.
#
# The two collisions no simulation can see, because they are not between two
# species: Hollow IS --violet (every CTA) and Skitter IS --amber (every
# warning and every Founder marker), dE76 0.0, for every viewer.
# PaletteContrastTests pins those against Tokens.uss.
#
# Costed options: implementation/results/palette-decision.md
#
";
}
