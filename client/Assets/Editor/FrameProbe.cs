using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with ScreenHarness and LabelBoxProbe.

/// THE CORPUS RENDERS AT 430 AND THE APP DOES NOT - Phase 9 Task 21e.
///
/// THE STANDING HAZARD, NAMED. `capture-screens.sh` renders every fixture at
/// 430x932, which is the handoff's reference frame. No iPhone this game ships
/// to is 430 points wide in the frame the game actually gets: the walk that
/// opened this task ran at 402x874, and Task 21b's lane crop was measured at
/// 390. That gap has now hidden two defects that only a person walking the
/// packaged app found - a 0.903-unit lane crop at 390 that measures 0.100 at
/// 430, and a founder card clipped at 402 that has its corners at 430. A
/// clean corpus is not evidence about a phone.
///
/// WHY THIS REPORTS AND DOES NOT ASSERT, which is `LabelBoxProbe`'s reason
/// and worth restating because it is the whole design: geometry needs a real
/// panel, and this project's EditMode suites build none, so `resolvedStyle`
/// there is default-valued (`ScaffoldTests.cs:218` and
/// `FirstHourScreensTests.cs:1291` both say so out loud). An EditMode gate
/// on a rect would read zeros and pass on anything, which is precisely the
/// could-not-fail test this phase has found three times. So this runs in
/// batchmode WITH a graphics device, the way `capture-screens.sh` does, and
/// prints numbers for a person to compare.
///
/// IT SHARES `ScreenshotCapture`'s PANEL RATHER THAN BUILDING ONE. Same
/// PanelSettings asset, same shell stylesheets, same `screen-host` slot, same
/// two-pass ForceRender - a probe that built its own panel would be measuring
/// its own panel, which is the mistake `LabelBoxProbe`'s header records
/// declining for the same reason.
///
/// Run it:  ./implementation/scripts/probe-frame.sh [WxH,WxH,...] [screen,screen,...]
public static class FrameProbe
{
    /// The frame the Phase 9 exit-gate walk ran in: an iPhone 17 simulator in
    /// points. Not a token and not a constant anything ships against - it is
    /// the frame a human held, which is what makes it worth measuring.
    const int DefaultWidth = 402;
    const int DefaultHeight = 874;

    public static void Run()
    {
        var sizes = new List<Vector2Int>();
        var screens = new List<string>();

        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            // A LIST, NOT ONE FRAME, AND IT IS ONE EDITOR LAUNCH PER SWEEP.
            // Unity holds a single lock on `client/`, so N frames as N runs
            // is N cold starts serialized behind each other - the constraint
            // this project already records about batch runs. The panel is
            // rebuilt per size inside `ScreenshotCapture.Capture` either way,
            // so a sweep in one process measures exactly what a sweep in N
            // processes would.
            if (args[i] == "-frameSizes" && i + 1 < args.Length)
            {
                foreach (var spec in args[i + 1].Split(','))
                {
                    var wh = spec.Trim().Split('x');
                    sizes.Add(new Vector2Int(int.Parse(wh[0], CultureInfo.InvariantCulture),
                                             int.Parse(wh[1], CultureInfo.InvariantCulture)));
                }
            }
            if (args[i] == "-frameScreens" && i + 1 < args.Length) screens.AddRange(args[i + 1].Split(','));
        }

        if (sizes.Count == 0) sizes.Add(new Vector2Int(DefaultWidth, DefaultHeight));
        if (screens.Count == 0) screens.AddRange(ScreenFixtures.Names);

        foreach (var size in sizes) Sweep(size.x, size.y, screens);
    }

    static void Sweep(int width, int height, List<string> screens)
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../implementation/results/frames/" + width + "x" + height));
        Directory.CreateDirectory(dir);

        var failed = new List<string>();
        foreach (var name in screens)
        {
            byte[] png;
            try
            {
                var screen = ScreenFixtures.Build(name);
                png = ScreenshotCapture.Capture(screen, ScreenFixtures.GoesInTheScreenHost(name),
                    width, height, host => Report(name, width, height, host));
            }
            catch (Exception e)
            {
                // ScreenshotCapture.CaptureAll's rule: a screen that throws is
                // a finding to report and a reason to exit non-zero, never a
                // reason to abandon the others.
                Debug.LogError("[frame] " + name + " threw and was not probed: " + e);
                failed.Add(name);
                continue;
            }

            File.WriteAllBytes(Path.Combine(dir, name + ".png"), png);
        }

        Debug.Log("[frame] probed " + (screens.Count - failed.Count) + " of " + screens.Count
                  + " screens at " + width + "x" + height + " into " + dir);

        if (failed.Count > 0) EditorApplication.Exit(1);
    }

    /// EVERY ELEMENT THAT CARRIES A NAME, IN TREE ORDER, WITH ITS RECT IN
    /// FRAME SPACE AND WHETHER THE FRAME HOLDS IT.
    ///
    /// `worldBound` RATHER THAN `layout`, AND THAT IS THE WHOLE POINT.
    /// `layout` is relative to the parent, so a clipped card and a card
    /// sitting happily inside a scrolled container report the same numbers -
    /// the mistake `WaveHudView.Place`'s own note records making once, on a
    /// measurement that "answers in the wrong space while still returning a
    /// plausible number". `worldBound` is in the panel's space, which is the
    /// space the frame's own edges are in, so `bottom > height` is literally
    /// the defect a person saw.
    ///
    /// CUT IS COMPUTED, NOT EYEBALLED. A row whose bottom is past the frame
    /// is reported with how many points of it fell off, because "clipped" is
    /// a yes/no a picture already answers and "clipped by 63" is the number
    /// that says whether a fix worked.
    static void Report(string name, int width, int height, VisualElement host)
    {
        foreach (var e in host.Query<VisualElement>().Build())
        {
            if (string.IsNullOrEmpty(e.name)) continue;

            var r = e.worldBound;

            // A zero-area element is either display:none or genuinely empty,
            // and both are answers a reader wants stated rather than filtered
            // out - "the note is not drawn at all" was the walk's own finding
            // and this is the line that would have said so.
            var cut = r.yMax - height;
            var state = float.IsNaN(r.yMax) ? "unresolved"
                : r.width <= 0f || r.height <= 0f ? "empty"
                : cut > 0.5f ? "CUT " + cut.ToString("0.0", CultureInfo.InvariantCulture)
                : "in";

            Debug.Log(string.Format(CultureInfo.InvariantCulture,
                "[frame] {0} {1}x{2} {3,-22} x={4,7:0.0} y={5,7:0.0} w={6,6:0.0} h={7,6:0.0} {8}",
                name, width, height, e.name, r.x, r.y, r.width, r.height, state));
        }
    }
}
