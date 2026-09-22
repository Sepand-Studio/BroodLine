using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder and ScreenHarness.

/// Renders every `ScreenFixtures` screen to
/// `implementation/results/screens/&lt;name&gt;.png` at 430x932, headless.
///
///   Unity -batchmode -quit -projectPath client \
///         -executeMethod ScreenshotCapture.CaptureAll -logFile &lt;path&gt;
///
/// Driven by `implementation/scripts/capture-screens.sh`, which also checks
/// the count and the file sizes - see that script for why the size check is
/// load-bearing rather than decorative.
///
/// THE PART THIS FILE OWNS THAT `ScreenHarness` DOES NOT: forcing a runtime
/// UI Toolkit panel to actually draw one frame into `PanelSettings.
/// targetTexture` from inside a single, synchronous `-executeMethod` call,
/// with no Play Mode and nothing else pumping Unity's player loop in
/// between. There is no public API for that - `IPanel` (what `VisualElement.
/// panel` returns) exposes none of `Update`/`Repaint`/`Render`. Those exist,
/// public, one frame below it, on `UnityEngine.UIElements.
/// BaseVisualElementPanel`/`BaseRuntimePanel` - confirmed by loading
/// `UnityEngine.UIElementsModule.dll` into a
/// `System.Reflection.MetadataLoadContext` and listing its members, because
/// guessing against a moving internal surface is how this kind of script
/// rots. The types are internal; the methods on them are public, so
/// reflection reaches them without touching anything private - the same
/// trade `Broodline.TestHarness/EditModeRunner.cs` already made one file
/// over, for the same reason (its own commit: "NUnit's own engine can't stay
/// on the main thread"; here: nothing else will call this for us in batch
/// mode).
///
/// `Repaint()` ALONE IS NOT ENOUGH, and finding that out cost three failed
/// runs - twelve pixel-identical, correctly-cleared-white, otherwise empty
/// PNGs each time, despite the panel, the layout and the child hierarchy all
/// resolving exactly as expected (`root.layout` came back 430x932 with
/// `childCount=1` every time - logged and checked before this was believed).
/// `Repaint()` reads as Unity's IMGUI-era "mark dirty for the next tick", not
/// "draw now", and that reading turned out to be literal: the separate
/// public `Render()` is the call that actually issues geometry into
/// `targetTexture`. `ForceRender` calls both, plus every other public
/// per-frame phase this reflection can find, because nothing here can
/// inspect the IL to confirm which of them the real per-frame driver
/// (`UIElementsRuntimeUtility`, native-hooked into the player loop this
/// script has none of) actually calls and in what order.
///
/// THE TOKEN GAP THAT WAS OPEN HERE FOR TWO ROUNDS IS CLOSED, AND IT WAS
/// NEVER THIS FILE. For the record, because two wrong theories were
/// committed to this comment before the right one:
///
/// Symptom: `resolvedStyle.backgroundColor` on a `.shell-root` element
/// measured fully transparent instead of `--paper`, and text measured
/// Unity's default grey instead of `--ink` - so every capture rendered
/// untokenized. Theories 1 and 2 (driving the internal `VisualTreeUpdatePhase`
/// updaters by hand; `:root` needing the sheet on the panel's true root
/// rather than a child) were both tested and both falsified, and the second
/// one was additionally built on a false premise - `document.rootVisualElement`
/// is a `UIDocumentRootElement` parented UNDER the panel's `PanelRootElement`,
/// so it is not the panel root either and that "fix" moved the sheets from
/// one child to another.
///
/// Actual cause: `Tokens.uss` compiled to ZERO RULES. Its header comment
/// cited the path `specs/Designs/_ds/modernist-<variant>/styles.css`, and the
/// comment-terminator in that glob closed the block comment eighteen
/// lines early. Lines
/// 10-28 were then parsed as USS source, where the apostrophes in
/// `Broodline's` and `Shell.uss's` and the quotes on lines 18-19 opened
/// unterminated strings - the four `LineBreakUnexpected` errors Unity had
/// been logging at import all along, at exactly the end-of-line columns of
/// those four lines, and at no line before the accidental terminator.
/// A whole design system was dead for the want of one slash.
///
/// WHAT MAKES THIS WORTH READING RATHER THAN DELETING: nothing in the
/// capture path was broken, and every measurement taken here was correct.
/// The harness was faithfully reporting a real defect in the shipped app -
/// the runtime panel had exactly the same dead sheet - and it was the only
/// thing in the project that noticed. USS import errors do not fail a
/// build, do not fail a test, and do not throw at runtime; a stylesheet
/// that parses to nothing renders as nothing, silently. That is why
/// `check-stylesheets.sh` now asserts every sheet compiles to a non-zero
/// rule count, and why it runs as a gate rather than on request.
public static class ScreenshotCapture
{
    // The handoff's reference frame - the same 430x932 ScreenHarness hosts a
    // screen in, so a capture and an Editor-window look at the same fixture
    // agree on what they show.
    const int Width = 430;
    const int Height = 932;

    const string SharedPanelSettingsPath = "Assets/UI/Shell/PanelSettings.asset";

    [MenuItem("Broodline/Capture All Screens")]
    public static void CaptureAll()
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../implementation/results/screens"));
        Directory.CreateDirectory(dir);

        var captured = 0;
        var threw = new List<string>();
        foreach (var name in ScreenFixtures.Names)
        {
            byte[] png;
            try
            {
                png = Capture(ScreenFixtures.Build(name), ScreenFixtures.GoesInTheScreenHost(name));
            }
            catch (Exception e)
            {
                // Resolution 4: a screen that throws while binding is a
                // finding to report, not a reason to abort the others - so
                // the loop continues. But it is recorded and it fails the
                // run below, which it did not used to.
                Debug.LogError("[ScreenshotCapture] " + name + " threw and was not captured: " + e);
                threw.Add(name);
                continue;
            }

            File.WriteAllBytes(Path.Combine(dir, name + ".png"), png);
            captured++;
        }

        Debug.Log($"captured {captured} of {ScreenFixtures.Names.Count} screens to {dir}");

        // A LOGGED ERROR IS NOT A FAILED RUN, AND THAT IS THE WHOLE PROBLEM.
        // Unity's -batchmode -quit returns 0 for a Debug.LogError; only an
        // uncaught throw or an explicit Exit makes it non-zero. So a screen
        // that broke used to leave a corpus one PNG short, a green exit code,
        // and a reviewer comparing fifteen pictures against sixteen. The
        // corpus is this phase's primary verification, and the one thing it
        // must never do is look complete when it is not. StylesheetCheck.Run
        // already calls Exit for the same reason.
        if (threw.Count > 0)
        {
            Debug.LogError($"[ScreenshotCapture] {threw.Count} of {ScreenFixtures.Names.Count} screens "
                + $"failed to capture: {string.Join(", ", threw)}");
            EditorApplication.Exit(1);
        }
    }

    static byte[] Capture(VisualElement screen, bool inScreenHost)
    {
        return Capture(screen, inScreenHost, Width, Height, null);
    }

    /// THE SAME CAPTURE AT A FRAME THE CALLER CHOOSES, AND `measure` IS RUN
    /// ON THE HOST WHILE THE PANEL IS STILL ALIVE.
    ///
    /// PHASE 9 TASK 21e ADDED THIS BECAUSE THE CORPUS'S 430 IS NOT A PHONE.
    /// The app runs at 390-402 points and the corpus renders at 430, and that
    /// gap has now hidden two defects that a person found by walking the
    /// packaged app: Task 21b's lane crop (0.903 units at 390 measured 0.100
    /// at 430) and this task's clipped founder card. A capture is a picture;
    /// what a narrower frame needs is a MEASUREMENT, taken after layout has
    /// resolved and before the panel is torn down - which is the one moment
    /// `resolvedStyle` means anything, and the moment an EditMode test can
    /// never reach (it builds no panel, so every rect there reads zero -
    /// `ScaffoldTests.cs:218` and `FirstHourScreensTests.cs:1291` both say so).
    ///
    /// `FrameProbe` is the only caller and it reports rather than asserts,
    /// for `LabelBoxProbe`'s stated reason.
    public static byte[] Capture(VisualElement screen, bool inScreenHost,
                                 int width, int height, Action<VisualElement> measure)
    {
        // ARGB32 + sRGB read/write: PanelSettings.targetTexture's own doc
        // asks for an sRGB-formatted target when the project's color space
        // is linear, which ProjectSettings.asset's m_ActiveColorSpace: 1
        // says this one is. Wrong here would not throw or shrink the file -
        // it would just quietly wash out or darken every colour, which is
        // exactly the kind of wrong this task's screenshots must not be.
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "ScreenshotCapture",
        };
        rt.Create();

        var settings = BuildPanelSettings(rt);
        var go = new GameObject("ScreenshotCapture") { hideFlags = HideFlags.HideAndDontSave };
        Texture2D tex = null;
        try
        {
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;

            // ON document.rootVisualElement, THE PANEL'S ACTUAL ROOT, matching
            // how Shell.uxml applies Tokens/Theme/Shell at the true UXML root
            // rather than on the inner "shell-root"-classed element. This is
            // structurally the right place regardless of the paragraph below -
            // MarkDirtyRepaint on the elements right after is the same idea:
            // ordinary, correct, well-known API.
            ScreenHarness.AddShellStyles(document.rootVisualElement);
            document.rootVisualElement.AddToClassList("shell-root");

            var host = new VisualElement { style = { width = width, height = height } };

            // A SCREEN GOES IN A `screen-host` SLOT, NOT STRAIGHT INTO THE
            // FRAME, AND THAT IS THE WHOLE POINT OF THIS ELEMENT.
            //
            // Shell.uxml puts every pushed and shown screen inside
            // `#screen-host`. This harness used to add every fixture to a
            // bare VisualElement, so no rule of Shell.uss's reached any
            // capture - which meant the corpus could see what a SCREEN does
            // to itself and nothing of what the SHELL does to a screen. It
            // cost Phase 8 Task 9 a finding that had to be read out of two
            // stylesheets rather than looked at: `.screen-host` carried 12px
            // of side padding and `.screen-scaffold__content` another 12,
            // for a 24px gutter where the handoff says 12, in the shipped
            // app, invisible here. `.screen-host`'s padding is gone as of
            // that fix; this slot is what would have shown it.
            //
            // NOT EVERY FIXTURE, and `ScreenFixtures.GoesInTheScreenHost` has
            // the list - a sheet, a HUD and four component catalogues are not
            // things the shell ever puts in that slot, and rendering them
            // there would be a second lie in place of the first.
            //
            // Named as well as classed, so it is the same element a reader
            // finds in Shell.uxml.
            if (inScreenHost)
            {
                var slot = new VisualElement { name = "screen-host" };
                slot.AddToClassList("screen-host");
                slot.Add(screen);
                host.Add(slot);
            }
            else
            {
                host.Add(screen);
            }
            document.rootVisualElement.Add(host);
            document.rootVisualElement.MarkDirtyRepaint();
            host.MarkDirtyRepaint();
            screen.MarkDirtyRepaint();

            ForceRender(host);
            if (measure != null) measure(host);

            var previouslyActive = RenderTexture.active;
            RenderTexture.active = rt;
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previouslyActive;

            return tex.EncodeToPNG();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(settings);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    /// A copy of the shipped shell's own `PanelSettings`, retargeted at
    /// `rt`, rather than a `ScriptableObject.CreateInstance&lt;PanelSettings&gt;()`
    /// built from nothing.
    ///
    /// THAT DIFFERENCE IS NOT COSMETIC. A freshly-created `PanelSettings`
    /// has no default/SDF/bitmap/atlas-blit shader assigned - the checked-in
    /// asset's four `m_*Shader` fields are wired up by the Editor's own
    /// "create Panel Settings asset" flow, not by the type's own defaults -
    /// so `ScriptableObject.CreateInstance` here would very plausibly render
    /// nothing (or a shader-error magenta) rather than throwing, which is
    /// indistinguishable from "worked" until a human opens the PNG. Copying
    /// the real asset carries those references, its theme, and its dynamic
    /// atlas settings over for free.
    static PanelSettings BuildPanelSettings(RenderTexture rt)
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(SharedPanelSettingsPath);
        PanelSettings settings;
        if (existing != null)
        {
            settings = UnityEngine.Object.Instantiate(existing);
        }
        else
        {
            Debug.LogWarning("[ScreenshotCapture] " + SharedPanelSettingsPath + " not found; building a bare "
                + "PanelSettings instead, which is missing its default shaders and may capture blank.");
            settings = ScriptableObject.CreateInstance<PanelSettings>();
        }

        settings.name = "ScreenshotCapturePanelSettings";
        settings.targetTexture = rt;
        // Constant, scale 1: one layout unit is one texture pixel, so the
        // 430x932 render target matches the host's own 430x932 exactly, with
        // no DPI-driven scaling to second-guess in a headless process.
        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.scale = 1f;
        settings.clearColor = true;
        settings.colorClearValue = Color.white;
        return settings;
    }

    /// Forces one frame of a runtime UI Toolkit panel to update and draw
    /// into its `targetTexture`, via the public-but-declared-on-an-internal-
    /// type methods this file's class comment explains.
    ///
    /// `Render()` AND `Repaint()` ARE BOTH REQUIRED - if the installed
    /// Editor's internals stop exposing either, this throws
    /// `MissingMethodException` rather than writing a blank frame that looks
    /// like success. `Render()` is the one this file's class comment records
    /// discovering the hard way: it is what actually issues geometry, and
    /// `Repaint()` alone produced twelve empty frames despite a fully
    /// resolved layout underneath. The other five phases below
    /// (`UpdateAnimations`/`UpdateBindings`/`UpdateDataBinding`/
    /// `TickSchedulingUpdaters`/`UpdateAssetTrackers`) and the internal
    /// `Update()` are called too, best-effort: reflection alone cannot
    /// confirm the real per-frame driver's exact call order, and calling
    /// each of these ahead of `Render()` is harmless if it is redundant.
    ///
    /// TWICE THROUGH, NOT ONCE. The first pass is what resolves layout for
    /// content that only just got added to the tree; a label that measures
    /// its own text to size a sibling, or a row built from a freshly-bound
    /// list, can still be settling on that pass. A second pass draws the
    /// already-resolved layout rather than trusting the first frame's
    /// numbers to be final - cheap insurance for a script whose entire
    /// output is a picture nobody re-renders to double check.
    static void ForceRender(VisualElement root)
    {
        var panel = root.panel;
        if (panel == null)
        {
            throw new InvalidOperationException(
                "no panel attached to " + root.GetType().Name + " after assigning "
                + "UIDocument.panelSettings - PanelSettings.targetTexture should attach one immediately.");
        }

        var type = panel.GetType();
        MethodInfo Required(string name)
        {
            var m = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m == null)
            {
                throw new MissingMethodException(
                    "UnityEngine.UIElements internals moved: " + type.FullName
                    + " has no public parameterless " + name + "(). ScreenshotCapture.ForceRender needs a new hook.");
            }
            return m;
        }

        var repaint = Required("Repaint");
        var render = Required("Render");

        string[] optionalPublicPhases =
        {
            "UpdateAnimations", "UpdateBindings", "UpdateDataBinding",
            "TickSchedulingUpdaters", "UpdateAssetTrackers",
        };

        // BaseRuntimePanel.Update() is internal, not public - the one call
        // in this sequence reflection has to reach past an access modifier
        // for, not just past the internal class holding a public member.
        var runtimeUpdate = type.GetMethod(
            "Update", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

        for (var pass = 0; pass < 2; pass++)
        {
            runtimeUpdate?.Invoke(panel, null);
            foreach (var phase in optionalPublicPhases)
            {
                type.GetMethod(phase, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
                    ?.Invoke(panel, null);
            }
            repaint.Invoke(panel, null);
            render.Invoke(panel, null);
        }
    }
}
