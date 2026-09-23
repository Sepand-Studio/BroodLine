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

    /// EVERY SHEET THE APP PRESENTS, AND THE FOUR SCREENS WHOSE BOTTOM CONTROL A
    /// WALK PUTS A THUMB ON.
    ///
    /// THE SHEET LIST IS THE ONE THAT HAS TO BE COMPLETE, because a sheet is
    /// what `#sheet-layer` holds and the layer is what was wrong. Counted off
    /// the call sites of `ScreenFlow.ShowSheetAsync`, not guessed: `CodexSheet`
    /// and `AbandonedWaveSheet` from `FtueDirector`'s two direct calls, and both
    /// `ConfirmDialog` faces from `ConfirmAsync`, which picks between them per
    /// dialog. Three of the four are absent from `ScreenFixtures.Names` and so
    /// from the screenshot corpus as well - which is part of how this went four
    /// tasks unseen.
    ///
    /// `ConfirmDialog` IS BOTH FACES ON PURPOSE. `Standard`'s scrim is
    /// `PickingMode.Position` and wired to `cancel` (splice_confirm_spec section
    /// 4), so the band the tab bar used to own was a band where tapping outside
    /// the dialog silently failed to dismiss it. `Named`'s scrim is
    /// `PickingMode.Ignore` by bible 3.3, so its scrim never dismissed anything
    /// - but its two BUTTONS are in the same band as every other sheet's, and it
    /// is the dialog that stands between a player and losing their Founder.
    static readonly string[] DefaultSubjects =
    {
        "AbandonedWaveSheet", "CodexSheet", "ConfirmDialogStandard", "ConfirmDialogNamed",
        "DeployView", "PostWaveView", "FounderNamingView", "InterruptedView",
    };

    const string AbandonedWaveSheetSubject = "AbandonedWaveSheet";
    const string ConfirmDialogStandardSubject = "ConfirmDialogStandard";
    const string ConfirmDialogNamedSubject = "ConfirmDialogNamed";

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
        var asserted = 0;
        foreach (var subject in subjects)
        {
            try
            {
                asserted += Probe(subject.Trim(), width, height, safeTop, safeBottom, unreachable);
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

        // THE COUNT OF CONTROLS IS IN THE SUMMARY SO THE GREEN CARRIES ITS OWN
        // EVIDENCE, and it is here because of what this instrument is for.
        // `Measure` skips a control whose rect is empty or NaN - which is right,
        // since a `display: none` control is not a defect - but it means a panel
        // that resolved NOTHING would skip every control, leave `unreachable`
        // empty, and print the same "OK" as a run that checked eight. That is
        // precisely the could-not-fail shape this file exists to catch, one level
        // up, inside the catcher. A reader now sees "8 subjects, 10 controls" and
        // knows something was asked; `Measure` reddens a subject that yields
        // none at all, so a silent nothing cannot pass either.
        Debug.Log("[shell] probed " + (subjects.Count - threw.Count) + " of " + subjects.Count
                  + " subjects at " + width + "x" + height
                  + " with safe insets " + safeTop.ToString("0.#", CultureInfo.InvariantCulture)
                  + "/" + safeBottom.ToString("0.#", CultureInfo.InvariantCulture)
                  + "; asserted " + asserted + " control(s)");

        if (unreachable.Count > 0)
        {
            Debug.LogError("[shell] DEAD: " + unreachable.Count + " control(s) have points a player can see "
                           + "and cannot press: " + string.Join("; ", unreachable));
        }

        // A LOGGED ERROR IS NOT A FAILED RUN under -batchmode -quit, which is
        // ScreenshotCapture.CaptureAll's own note and the reason it calls Exit.
        if (unreachable.Count > 0 || threw.Count > 0) EditorApplication.Exit(1);
    }

    /// Returns how many controls it actually asserted on, which the caller adds
    /// up and prints. Zero from a subject is itself a finding and `Measure`
    /// records it.
    static int Probe(string subject, int width, int height, float safeTop, float safeBottom,
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
            return Measure(subject, root, presented, isSheet, unreachable);
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

        // THE SPLICE PAIR THE WHOLE HARNESS USES, so the dialog carries the copy
        // a walk actually reads - and copy is geometry here, because `#body`
        // wraps and a shorter sentence makes a shorter card and moves the
        // buttons. `SpliceScreen.Build` is what `FtueDirector` calls too, and it
        // is the only public route to a `SpliceDialog` (every property is
        // `internal set`).
        if (subject == ConfirmDialogStandardSubject)
        {
            return ConfirmDialog.Standard(ScreenFixtures.SpliceModel().StandardDialog,
                confirm: () => { }, cancel: () => { });
        }
        if (subject == ConfirmDialogNamedSubject)
        {
            return ConfirmDialog.Named(ScreenFixtures.SpliceModel().FounderDialog,
                confirm: () => { }, cancel: () => { });
        }

        return ScreenFixtures.Build(subject);
    }

    /// WHICH LAYER THIS SUBJECT BELONGS IN, AND IT IS A LIST OF SHEETS RATHER
    /// THAN THE NEGATION OF ANOTHER LIST.
    ///
    /// THIS USED TO READ `!ScreenFixtures.GoesInTheScreenHost(subject)` AND THAT
    /// WAS WRONG. "Not in the screen host" is not "is a sheet": that method also
    /// answers false for `WaveHudView`, which `WaveRunner` adds to the wave
    /// scene's own panel root, and for the seven component catalogues, which the
    /// shell never holds at all. So `probe-shell.sh 402x874 62,34 WaveHudView`
    /// would have mounted the wave HUD through `ShowSheet` and measured, in
    /// silence, an arrangement the app never produces - a probe answering
    /// confidently about something that does not exist, which is the failure
    /// this whole task was called in to end.
    ///
    /// Named explicitly, so a fixture added to `ScreenFixtures` for any other
    /// reason cannot quietly become a sheet here.
    static bool IsSheet(string subject) =>
        subject == AbandonedWaveSheetSubject
        || subject == ConfirmDialogStandardSubject
        || subject == ConfirmDialogNamedSubject
        || subject == "CodexSheet";

    static int Measure(string subject, VisualElement root, VisualElement presented, bool isSheet,
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
        var asserted = 0;

        // A PRESENTED SHEET OWNS THE TAB BAR'S BAND, AND THIS IS THE ASSERTION
        // THAT SAYS SO RATHER THAN THE BUTTON SWEEP BELOW.
        //
        // WHY THE SWEEP IS NOT ENOUGH, found by review and not by this file:
        // `ConfirmDialog` is a CENTRED dialog, so its two buttons sit at y=485
        // and were never in the band at all - but `Standard`'s SCRIM is
        // `PickingMode.Position` and wired to `cancel` (splice_confirm_spec
        // section 4), and the scrim does reach the band. So the spec's "tapping
        // outside it cancels" was false in the bottom 56 points and no sweep over
        // Buttons could ever have noticed. A scrim cannot be swept the way a
        // button is, either: its own card is a later sibling and legitimately
        // wins the middle of the column, which a sweep would report as dead.
        //
        // ONE PICK, AT THE CENTRE OF THE BAR'S BAND, asserting only that it lands
        // somewhere INSIDE the sheet - scrim, card or button, whichever the sheet
        // puts there. That is the whole of what "the overlay is on top" means, it
        // is what the tab bar was taking, and it is true of all four sheets.
        if (isSheet)
        {
            asserted++;
            var atBar = new Vector2(tabBarRect.center.x, tabBarRect.center.y);
            var hit = panel.Pick(atBar);
            if (!IsInside(hit, presented))
            {
                unreachable.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0} does not own the tab bar's band: a tap at ({1:0.#}, {2:0.#}) goes to {3} instead "
                    + "of to the sheet, so the bar is drawn over the sheet and takes its taps",
                    subject, atBar.x, atBar.y, Describe(hit)));
            }
        }

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

            asserted++;
            var verdict = Reach(panel, control);
            if (verdict != null) unreachable.Add(subject + "/" + label + " " + verdict);
        }

        // A SUBJECT THAT ANSWERED NOTHING IS A FAILURE, NOT A PASS. Every one of
        // the eight default subjects has at least one Button outside a
        // ScrollView; if a run asserts on none of them, the panel did not resolve
        // - which is the one thing this probe claims over an EditMode test, and
        // the one thing it must not silently lose. It goes in the same list a
        // dead control goes in, so it reddens the script the same way.
        if (asserted == 0)
        {
            unreachable.Add(subject + " yielded NO measurable control at all, so nothing about it was "
                            + "checked - either the panel resolved no layout or every control it has is "
                            + "inside a ScrollView");
        }

        return asserted;
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
    /// inside it), and a sentence naming the dead points and their thief
    /// otherwise.
    ///
    /// THE DENOMINATOR IS COUNTED, NOT DERIVED, AND THE END OF THE BAND IS
    /// OBSERVED. This method's first version divided by `rect.height - 2`, which
    /// is 53 for a 55-point button while the loop actually samples 54, and it
    /// printed the band's end as `rect.yMax - 1` unconditionally - so three
    /// scattered dead points would have read as a band running to the bottom
    /// edge. In a project where the gate is the red's MESSAGE, an instrument that
    /// mis-states its own denominator is the defect it was built to find.
    /// `sampled`, `firstDead` and `lastDead` are all now what happened.
    static string Reach(IPanel panel, VisualElement control)
    {
        var rect = control.worldBound;
        var x = rect.center.x;
        var firstDead = float.NaN;
        var lastDead = float.NaN;
        VisualElement thief = null;
        var dead = 0;
        var sampled = 0;

        // Inset by a point at each end: the boundary row of a rect is shared
        // with whatever abuts it and is not a place a finger is aimed.
        for (var y = rect.yMin + 1f; y <= rect.yMax - 1f; y += 1f)
        {
            sampled++;
            var hit = panel.Pick(new Vector2(x, y));
            if (hit == control || IsInside(hit, control)) continue;

            dead++;
            lastDead = y;
            if (float.IsNaN(firstDead)) { firstDead = y; thief = hit; }
        }

        if (dead == 0) return null;

        // CONTIGUOUS OR NOT, SAID OUT LOUD. A bar lying across the bottom of a
        // button produces one run; anything else - a hole in the middle, a
        // checkerboard of some child's picking - is a different defect wearing
        // the same numbers, and the reader should not have to do the subtraction.
        var contiguous = Mathf.Approximately(lastDead - firstDead + 1f, dead);
        return string.Format(CultureInfo.InvariantCulture,
            "{0} of its {1} sampled points are dead, topmost y={2:0.#}, lowest y={3:0.#}, {4}; "
            + "the topmost miss goes to {5}",
            dead, sampled, firstDead, lastDead,
            contiguous ? "one contiguous band" : "SCATTERED, not one band", Describe(thief));
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
