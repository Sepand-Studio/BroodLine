using System;
using System.Collections.Generic;
using System.Globalization;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.UI.Components;
using Broodline.UI.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with FrameProbe and ScreenHarness.

/// WHAT A THUMB HITS, NOT WHAT A RECT SAYS - Phase 9 Task 21i.
///
/// `FrameProbe` renders one fixture into a `#screen-host` slot and reports
/// its rects. That is the right tool for "is this card cut off at 402" and it
/// is blind to the defect this file exists for, because the frame it builds
/// has no tab bar, no sheet layer and no notice layer in it - so nothing in
/// the corpus or the frame sweep has ever had two shell layers to be wrong
/// about. A player on an iPhone 17 put a thumb on the lower half of
/// `AbandonedWaveSheet`'s Forfeit button four times and the app did nothing
/// twice; the button was there, drawn, and under something else.
///
/// SO THIS BUILDS THE WHOLE SHELL FROM `Shell.uxml` ITSELF - the real asset,
/// cloned the way `UIDocument` clones it, with the real `TabBar`, the real
/// `NoticeToast`, the real `ScreenHost` and the real `SafeAreaBinder` through
/// the seam `BootController.BindSafeAreas` already exposes. A probe that
/// re-assembled the shell by hand would be measuring its own assembly, which
/// is `ScreenshotCapture`'s stated reason for sharing a panel rather than
/// building one, applied one level up.
///
/// IT ASSERTS, AND IT IS THE ONLY THING IN THIS PROJECT THAT CAN.
/// `FrameProbe` and `LabelBoxProbe` deliberately report and do not assert,
/// and their reason is sound and unchanged: geometry needs a live panel and
/// the EditMode suites build none, so a rect read there is default-valued and
/// a gate on it passes on anything. That reason argues against an EditMode
/// assertion. It does not argue against asserting HERE, where there IS a live
/// panel - this runs in `-batchmode` WITH a graphics device, exactly as
/// `capture-screens.sh` and `probe-frame.sh` do. The acceptance criterion for
/// Task 21i is "a point inside the button's lower band resolves to the button
/// rather than to the tab bar", `IPanel.Pick` answers precisely that, and an
/// answer that cannot come back wrong is not worth writing down.
///
/// `Pick` RATHER THAN A RECT COMPARISON, AND THE DIFFERENCE IS THE DEFECT.
/// Two elements can overlap and still both be reachable (the notice layer
/// overlaps every screen in the app and is `PickingMode.Ignore` from its root
/// down). Two elements can also fail to overlap on the numbers a careless
/// reading produces and still steal each other's taps. `Pick` walks the same
/// hierarchy in the same order the runtime dispatches a `PointerDownEvent`
/// through, so what it returns is what the tap gets.
///
/// Run it:  ./implementation/scripts/probe-shell.sh [WxH] [top,bottom] [subject,...]
public static class ShellProbe
{
    /// The frame the Phase 9 exit-gate walk ran in, in device points, and the
    /// iPhone 17's own safe-area insets. `FrameProbe` carries the same 402x874
    /// and the same note: not a token, not a constant anything ships against -
    /// the frame a human held.
    ///
    /// THE TOP INSET IS REPORTED AND IS NOT LOAD-BEARING FOR THIS DEFECT, and
    /// saying so is what keeps the number from being argued about. Everything
    /// this file measures is anchored to the BOTTOM of `#shell-root`: the tab
    /// bar is the last in-flow child and the sheet layer's `bottom: 0` is the
    /// same edge. Move the top inset and the whole stack above it moves; the
    /// distance from the bottom of the frame to the top of the tab bar does
    /// not change at all.
    const int DefaultWidth = 402;
    const int DefaultHeight = 874;
    const float DefaultSafeTop = 62f;
    const float DefaultSafeBottom = 34f;

    /// The two sheets and the four screens whose bottom control a walk
    /// actually puts a thumb on. `AbandonedWaveSheet` is not in
    /// `ScreenFixtures.Names` and so is not in the corpus either - it is built
    /// here directly.
    static readonly string[] DefaultSubjects =
    {
        "AbandonedWaveSheet", "CodexSheet",
        "DeployView", "PostWaveView", "FounderNamingView", "InterruptedView",
    };

    const string AbandonedWaveSheetSubject = "AbandonedWaveSheet";

    public static void Run()
    {
        int width = DefaultWidth, height = DefaultHeight;
        float safeTop = DefaultSafeTop, safeBottom = DefaultSafeBottom;
        var subjects = new List<string>();

        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "-shellFrame" && i + 1 < args.Length)
            {
                var wh = args[i + 1].Trim().Split('x');
                width = int.Parse(wh[0], CultureInfo.InvariantCulture);
                height = int.Parse(wh[1], CultureInfo.InvariantCulture);
            }
            if (args[i] == "-shellInsets" && i + 1 < args.Length)
            {
                var insets = args[i + 1].Trim().Split(',');
                safeTop = float.Parse(insets[0], CultureInfo.InvariantCulture);
                safeBottom = float.Parse(insets[1], CultureInfo.InvariantCulture);
            }
            if (args[i] == "-shellSubjects" && i + 1 < args.Length) subjects.AddRange(args[i + 1].Split(','));
        }

        if (subjects.Count == 0) subjects.AddRange(DefaultSubjects);

        var unreachable = new List<string>();
        var threw = new List<string>();
        foreach (var subject in subjects)
        {
            try
            {
                Probe(subject.Trim(), width, height, safeTop, safeBottom, unreachable);
            }
            catch (Exception e)
            {
                // ScreenshotCapture.CaptureAll's rule: a subject that throws is
                // a finding to report and a reason to exit non-zero, never a
                // reason to abandon the others.
                Debug.LogError("[shell] " + subject + " threw and was not probed: " + e);
                threw.Add(subject);
            }
        }

        Debug.Log("[shell] probed " + (subjects.Count - threw.Count) + " of " + subjects.Count
                  + " subjects at " + width + "x" + height
                  + " with safe insets " + safeTop.ToString("0.#", CultureInfo.InvariantCulture)
                  + "/" + safeBottom.ToString("0.#", CultureInfo.InvariantCulture));

        if (unreachable.Count > 0)
        {
            Debug.LogError("[shell] DEAD: " + unreachable.Count + " control(s) have points a player can see "
                           + "and cannot press: " + string.Join("; ", unreachable));
        }

        // A LOGGED ERROR IS NOT A FAILED RUN under -batchmode -quit, which is
        // ScreenshotCapture.CaptureAll's own note and the reason it calls Exit.
        if (unreachable.Count > 0 || threw.Count > 0) EditorApplication.Exit(1);
    }

    static void Probe(string subject, int width, int height, float safeTop, float safeBottom,
                      List<string> unreachable)
    {
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "ShellProbe",
        };
        rt.Create();

        var settings = ScreenshotCapture.BuildPanelSettings(rt);
        var go = new GameObject("ShellProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;

            var shell = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Shell/Shell.uxml");
            if (shell == null) throw new InvalidOperationException("Assets/UI/Shell/Shell.uxml did not load.");

            var root = document.rootVisualElement;
            root.style.width = width;
            root.style.height = height;

            // The root `<ui:Style>` elements of Shell.uxml land on `root` here
            // exactly as they do under a live UIDocument, so Tokens/Theme/
            // icons/Motion/Shell all cascade from the same place the app
            // cascades them from. Nothing is re-declared by this file.
            shell.CloneTree(root);

            // BootController.Start's own composition, in its own order, and
            // the order matters for the same reason it does there: the toast
            // has to exist before the safe areas are bound to it.
            var tabBar = new TabBar();
            root.Q<VisualElement>("tab-bar").Add(tabBar);

            var toast = new NoticeToast();
            root.Q<VisualElement>("notice-layer").Add(toast);

            // THE REAL BINDER THROUGH THE SEAM IT ALREADY HAS. `Screen.safeArea`
            // in batchmode is the whole editor window with no insets at all, so
            // the production `ForRuntimePanel` wiring would measure a phone with
            // no Dynamic Island and no home indicator - which is the one thing
            // this probe must not do. `bind` exists on BindSafeAreas for
            // exactly this substitution and its header says so.
            //
            // The Rect is in SCREEN space, y up, which is what Screen.safeArea
            // is: yMin IS the bottom inset and `height - yMax` IS the top one,
            // which is the arithmetic SafeAreaBinder then runs. `screenYToPanelY`
            // is the identity because this panel is ConstantPixelSize at scale 1
            // - one layout unit is one point of the frame named above.
            var safeArea = new Rect(0f, safeBottom, width, height - safeTop - safeBottom);
            BootController.BindSafeAreas(root, toast, element => new SafeAreaBinder(
                element,
                isReady: () => true,
                getSafeArea: () => safeArea,
                getScreenWidth: () => width,
                getScreenHeight: () => height,
                screenYToPanelY: y => y));

            var host = new ScreenHost(root.Q<VisualElement>("screen-host"),
                                      root.Q<VisualElement>("sheet-layer"), tabBar);

            // THE BAR AS A FRESH INSTALL DRAWS IT, WHICH IS ALSO THE SHORTEST
            // IT EVER IS. `Progression.TabsFor` gives Map and Ark to everyone
            // at every wave count (they are `AlwaysA`/`AlwaysB`), and the bar
            // lays its tabs out in a ROW - so two tabs and five tabs are the
            // same height and this is not a "fewest tabs" special case. The one
            // state that differs is the bar before the first snapshot arrives,
            // when it has no children at all; that state is measured below as
            // `tab-bar-empty` rather than assumed away.
            tabBar.Render(Progression.TabsFor(0, new Dictionary<string, int>()), Progression.AlwaysA, _ => { });

            var presented = Build(subject);
            var isSheet = IsSheet(subject);
            if (isSheet) host.ShowSheet(presented); else host.Show(presented);

            ScreenshotCapture.ForceRender(root);
            Measure(subject, width, height, root, presented, unreachable);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(settings);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
    }

    static VisualElement Build(string subject)
    {
        if (subject == AbandonedWaveSheetSubject) return new AbandonedWaveSheet(() => { });
        return ScreenFixtures.Build(subject);
    }

    /// A sheet goes to `#sheet-layer`, a screen to `#screen-host`, and
    /// `ScreenFixtures.GoesInTheScreenHost` is already the list of which is
    /// which for everything the corpus knows about. `AbandonedWaveSheet` is not
    /// in that list because it is not a fixture, so it is named here.
    static bool IsSheet(string subject) =>
        subject == AbandonedWaveSheetSubject || !ScreenFixtures.GoesInTheScreenHost(subject);

    static void Measure(string subject, int width, int height, VisualElement root, VisualElement presented,
                        List<string> unreachable)
    {
        var panel = root.panel;
        var tabBarSlot = root.Q<VisualElement>("tab-bar");
        var tabBarRect = tabBarSlot.worldBound;

        foreach (var name in new[] { "shell-root", "top-bar", "screen-host", "sheet-layer", "tab-bar" })
        {
            var element = root.Q<VisualElement>(name);
            if (element == null) continue;
            Log(subject, "layer", name, element.worldBound, tabBarRect);
        }

        // THE PRESENTED THING'S OWN BOX, BECAUSE "the screens are clear of the
        // bar" IS A CLAIM ABOUT THE SLOT AND NOT ABOUT WHAT IS IN IT.
        // `#screen-host` is an in-flow sibling of the tab bar and its bottom
        // edge IS the bar's top edge, so nothing laid out inside it can reach
        // under the bar - UNLESS the screen overflows the slot, which UI
        // Toolkit allows (nothing sets `overflow: hidden` on `.screen-host`)
        // and which is exactly the shape of defect `FrameProbe`'s CUT column
        // was built to catch one level down. Measured rather than reasoned.
        Log(subject, "subject", "(root)", presented.worldBound, tabBarRect);

        // Every control the presented thing offers, in tree order. A Button is
        // what every primary action in this project is; `.btn-quiet` links are
        // Buttons too, which is what puts FounderNamingView's "Not now" in
        // here without naming it.
        foreach (var control in presented.Query<Button>().Build())
        {
            var rect = control.worldBound;
            var label = string.IsNullOrEmpty(control.name) ? Describe(control) : control.name;
            Log(subject, "control", label, rect, tabBarRect);

            if (rect.width <= 0f || rect.height <= 0f || float.IsNaN(rect.yMax)) continue;

            // INSIDE A ScrollView IS A DIFFERENT QUESTION AND IS NOT THIS
            // FILE'S. A scrolled-out row is legitimately unpickable and
            // scrolling it into view is what a player does; asserting on it
            // would redden on correct behaviour. The controls this defect is
            // about are the ones the scaffold pins OUTSIDE the scroller -
            // `#cta-row`, `#footer-note` - and everything on a sheet.
            if (InsideAScrollView(control, presented)) continue;

            var verdict = Reach(panel, control);
            if (verdict != null) unreachable.Add(subject + "/" + label + " " + verdict);
        }
    }

    /// Walks the centre column of a control one point at a time and asks the
    /// panel what a tap there would hit.
    ///
    /// THE CENTRE COLUMN, BECAUSE THAT IS WHERE A THUMB GOES and because the
    /// defect is a horizontal bar lying across a horizontal band - a column
    /// finds it and costs one pick per point. A full grid would find a
    /// same-shaped defect on a vertical edge, and nothing in this project has
    /// ever produced one.
    ///
    /// Returns null when every point resolves to the control (or to something
    /// inside it), and a sentence naming the dead band and its thief otherwise.
    static string Reach(IPanel panel, VisualElement control)
    {
        var rect = control.worldBound;
        var x = rect.center.x;
        var firstDead = float.NaN;
        VisualElement thief = null;
        var dead = 0;

        // Inset by a point at each end: the boundary row of a rect is shared
        // with whatever abuts it and is not a place a finger is aimed.
        for (var y = rect.yMin + 1f; y <= rect.yMax - 1f; y += 1f)
        {
            var hit = panel.Pick(new Vector2(x, y));
            if (hit == control || IsInside(hit, control)) continue;

            dead++;
            if (float.IsNaN(firstDead)) { firstDead = y; thief = hit; }
        }

        if (dead == 0) return null;
        return string.Format(CultureInfo.InvariantCulture,
            "{0:0.#} of its {1:0.#} points are dead, from y={2:0.#} down to y={3:0.#}; the tap goes to {4}",
            dead, Mathf.Max(0f, rect.height - 2f), firstDead, rect.yMax - 1f, Describe(thief));
    }

    static bool IsInside(VisualElement element, VisualElement ancestor)
    {
        for (var e = element; e != null; e = e.parent)
        {
            if (e == ancestor) return true;
        }
        return false;
    }

    static bool InsideAScrollView(VisualElement element, VisualElement stopAt)
    {
        for (var e = element; e != null && e != stopAt; e = e.parent)
        {
            if (e is ScrollView) return true;
        }
        return false;
    }

    static string Describe(VisualElement element)
    {
        if (element == null) return "nothing at all (the tap falls off the panel)";
        var name = string.IsNullOrEmpty(element.name) ? element.GetType().Name : element.name;
        var classes = element.GetClasses();
        foreach (var c in classes) return name + "." + c;
        return name;
    }

    /// One line per measured box, with its rect in frame space and how much of
    /// it the tab bar lies across.
    ///
    /// `worldBound` RATHER THAN `layout`, FrameProbe's reason verbatim: layout
    /// is relative to a parent, so a box sitting happily inside its slot and a
    /// box hanging out of the bottom of one report the same numbers.
    static void Log(string subject, string kind, string name, Rect rect, Rect tabBar)
    {
        var overlap = Mathf.Max(0f, Mathf.Min(rect.yMax, tabBar.yMax) - Mathf.Max(rect.yMin, tabBar.yMin));
        var state = float.IsNaN(rect.yMax) ? "unresolved"
            : rect.width <= 0f || rect.height <= 0f ? "empty"
            : overlap > 0.5f ? "UNDER-TAB-BAR " + overlap.ToString("0.0", CultureInfo.InvariantCulture)
            : "clear";

        Debug.Log(string.Format(CultureInfo.InvariantCulture,
            "[shell] {0,-20} {1,-7} {2,-18} x={3,7:0.0} y={4,7:0.0} w={5,6:0.0} h={6,6:0.0} bottom={7,7:0.0} {8}",
            subject, kind, name, rect.x, rect.y, rect.width, rect.height, rect.yMax, state));
    }
}
