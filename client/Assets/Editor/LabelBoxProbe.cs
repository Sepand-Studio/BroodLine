using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// THE NESTED-LABEL DOUBLING, INSTRUMENTED AT LAST - Phase 9 Task 20.
///
/// SIX SIGHTINGS ACROSS FOUR TASKS before anything measured the mechanism
/// itself. `.chip` was the first, the splice chamber's `PARENT A` eyebrow the
/// second, `.deploy-view__pill-label`/`-value` the third and fourth, StatCell
/// the fifth, and `TraitPip` is the sixth and still open. Every one of them
/// was found by measuring a CAPTURE and reasoning backwards; the rule that
/// came out of Task 16b - HEIGHT ON THE BOX, NEVER ON THE LABEL - has held
/// every time, but nothing ever measured WHY, and Task 18's probe (both
/// StatCell heights at 0 gives 13px where padding predicts 21) established
/// only that the overhead is NOT linear.
///
/// WHAT IT ACTUALLY IS, MEASURED HERE AND NOT WHAT SIX TASKS BELIEVED.
/// Unity's own default runtime theme gives every Label padding 4/4 and
/// margin 4/2. At --text-micro (10px) the line box is 13.6 -> 14, so the
/// Label's border box is 4 + 14 + 4 = 22 and its MARGIN box is 28. There is
/// no doubling and no second line box: it is 14px of FIXED CHROME around one
/// 14px line box. It reads as a doubling only because 28 and 2 x 13.6 = 27.2
/// agree to within 0.8px - the coincidence that kept the wrong model alive.
/// Being a constant, it also cannot scale with font-size, which is precisely
/// the non-linearity Task 18 saw and could not name.
///
/// The parent has nothing to do with it. Cases A-D below measure the SAME
/// Label at 22px in a row parent, a column parent, stretched, unstretched and
/// with white-space: nowrap. Case F zeroes the Label's own padding and margin
/// and gets 14 - the bare line box - and doing that to TraitPip takes it from
/// 34px to 20px, which is TraitChip's height exactly.
///
/// WHY THIS IS A PROBE AND NOT AN EditMode TEST. It needs a real layout, and
/// this project's EditMode suites build NO PANEL - `resolvedStyle` geometry
/// there is default-valued, which several suites already say out loud
/// (FirstHourScreensTests.cs:1291, ScaffoldTests.cs:218). A gate that asserted
/// heights from an EditMode tree would read zeros and pass on anything, which
/// is precisely the could-not-fail test this phase has now found three times.
/// So this runs in batchmode WITH a graphics device, the way
/// `capture-screens.sh` does, and reports measurements rather than asserting.
///
/// Run it:  ./implementation/scripts/probe-label-box.sh
///
/// The panel construction below is ScreenshotCapture.cs's, deliberately: same
/// PanelSettings asset, same shell stylesheets, same two-pass ForceRender. A
/// probe that built its own panel would be measuring its own panel.
public static class LabelBoxProbe
{
    const string SharedPanelSettingsPath = "Assets/UI/Shell/PanelSettings.asset";
    const int Width = 430;
    const int Height = 932;

    /// The string every sighting was measured on: two words, so it has
    /// somewhere to wrap if it is going to.
    const string Specimen = "Carapace III";

    public static void Run()
    {
        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.Create();
        var settings = BuildPanelSettings(rt);
        var go = new GameObject("LabelBoxProbe") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            ScreenHarness.AddShellStyles(document.rootVisualElement);
            document.rootVisualElement.AddToClassList("shell-root");

            var host = new VisualElement { style = { width = Width, height = Height } };
            document.rootVisualElement.Add(host);

            var probes = new List<(string name, VisualElement box, Func<VisualElement> label)>();

            // ---- A/B: the SAME Label, in a row parent and in a column parent.
            // This is the comparison every sighting implies and none ran.
            probes.Add(Case("A row-parent      ", host, FlexDirection.Row, l => { }));
            probes.Add(Case("B column-parent   ", host, FlexDirection.Column, l => { }));

            // ---- C: column parent, but the label may not wrap.
            probes.Add(Case("C column+nowrap   ", host, FlexDirection.Column,
                l => l.style.whiteSpace = WhiteSpace.NoWrap));

            // ---- D: column parent, but the parent does not stretch its child.
            probes.Add(Case("D column+start    ", host, FlexDirection.Column,
                l => { }, box => box.style.alignItems = Align.FlexStart));

            // ---- E: the rule. Height pinned ON THE BOX.
            probes.Add(Case("E box height 14   ", host, FlexDirection.Column,
                l => { }, box => box.style.height = 14));

            // ---- F: THE DECISIVE ONE. Zero the Label's own padding and
            // margin and leave everything else alone. If the extra height is
            // the default theme's chrome, this lands on the bare line box
            // (13.6 -> 14) and the "two line boxes" model is wrong.
            probes.Add(Case("F chrome zeroed   ", host, FlexDirection.Column, l =>
            {
                l.style.paddingTop = 0; l.style.paddingBottom = 0;
                l.style.marginTop = 0; l.style.marginBottom = 0;
            }));

            // ---- F/G: the real components, as the app builds them.
            var pip = new Broodline.UI.Components.TraitPip();
            pip.Bind("Carapace", 3, "Skitter");
            var pipBox = Wrap(host, pip);

            var chip = new Broodline.UI.Components.TraitChip("Carapace", 3, "Vetch");
            var chipBox = Wrap(host, chip);

            // TraitPip with the chrome zeroed on its label - the SIXTH
            // sighting, and what the fix would actually cost. Reported, not
            // applied: every screen task is closed and the capture corpus is
            // the baseline, so changing a shipped component's height here
            // would move captures nobody is re-reviewing.
            var pipFixed = new Broodline.UI.Components.TraitPip();
            pipFixed.Bind("Carapace", 3, "Skitter");
            var pipFixedLabel = pipFixed.Q<Label>("label");
            pipFixedLabel.style.paddingTop = 0; pipFixedLabel.style.paddingBottom = 0;
            pipFixedLabel.style.marginTop = 0; pipFixedLabel.style.marginBottom = 0;
            Wrap(host, pipFixed);

            ForceRender(host);

            Debug.Log("[labelbox] ---- the same Label, five parents ----");
            foreach (var (name, box, label) in probes)
            {
                var l = label();
                Debug.Log($"[labelbox] {name} box={F(box.resolvedStyle.height)}  " +
                          $"label={F(l.resolvedStyle.height)}  labelWidth={F(l.resolvedStyle.width)}  " +
                          Detail(l));
            }

            Debug.Log("[labelbox] ---- the real components ----");
            var pipLabel = pip.Q<Label>("label");
            var chipLabel = chip.Q<Label>();
            Debug.Log($"[labelbox] TraitPip           height={F(pip.resolvedStyle.height)}  " +
                      $"label={F(pipLabel.resolvedStyle.height)}  " +
                      $"labelWidth={F(pipLabel.resolvedStyle.width)}  " + Detail(pipLabel));
            Debug.Log($"[labelbox] TraitChip          height={F(chip.resolvedStyle.height)}  " +
                      $"label={F(chipLabel.resolvedStyle.height)}  " +
                      $"labelWidth={F(chipLabel.resolvedStyle.width)}  " + Detail(chipLabel));
            Debug.Log($"[labelbox] TraitPip chrome=0  height={F(pipFixed.resolvedStyle.height)}  " +
                      $"label={F(pipFixedLabel.resolvedStyle.height)}  " +
                      $"labelWidth={F(pipFixedLabel.resolvedStyle.width)}  " + Detail(pipFixedLabel));

            Debug.Log("[labelbox] done");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(settings);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }
        EditorApplication.Exit(0);
    }

    static string F(float v) => v.ToString("0.00");

    /// The numbers that decide between the competing explanations: is the
    /// extra height MARGIN on the Label, PADDING on it, or a bigger font than
    /// the rule asked for? Reported for every specimen so the answer is read
    /// rather than inferred.
    static string Detail(VisualElement l)
    {
        var r = l.resolvedStyle;
        return $"fontSize={F(r.fontSize)} " +
               $"mT={F(r.marginTop)} mB={F(r.marginBottom)} " +
               $"pT={F(r.paddingTop)} pB={F(r.paddingBottom)}";
    }

    static VisualElement Wrap(VisualElement host, VisualElement child)
    {
        var box = new VisualElement();
        box.Add(child);
        host.Add(box);
        return box;
    }

    static (string, VisualElement, Func<VisualElement>) Case(
        string name, VisualElement host, FlexDirection dir,
        Action<VisualElement> tweakLabel, Action<VisualElement> tweakBox = null)
    {
        var box = new VisualElement();
        box.style.flexDirection = dir;
        tweakBox?.Invoke(box);

        var label = new Label(Specimen);
        label.style.fontSize = 10;
        tweakLabel(label);

        box.Add(label);
        host.Add(box);
        return (name, box, () => label);
    }

    static PanelSettings BuildPanelSettings(RenderTexture rt)
    {
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(SharedPanelSettingsPath);
        var settings = existing != null
            ? UnityEngine.Object.Instantiate(existing)
            : ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "LabelBoxProbePanelSettings";
        settings.targetTexture = rt;
        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        return settings;
    }

    /// ScreenshotCapture.ForceRender, same reflection and the same two passes.
    static void ForceRender(VisualElement root)
    {
        var panel = root.panel;
        if (panel == null) throw new InvalidOperationException("no panel attached");
        var type = panel.GetType();

        MethodInfo Pub(string n) => type.GetMethod(
            n, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

        var repaint = Pub("Repaint");
        var render = Pub("Render");
        if (repaint == null || render == null)
            throw new MissingMethodException("UIElements internals moved; LabelBoxProbe needs a new hook.");

        var runtimeUpdate = type.GetMethod(
            "Update", BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);

        string[] phases =
        {
            "UpdateAnimations", "UpdateBindings", "UpdateDataBinding",
            "TickSchedulingUpdaters", "UpdateAssetTrackers",
        };

        for (var pass = 0; pass < 2; pass++)
        {
            runtimeUpdate?.Invoke(panel, null);
            foreach (var p in phases) Pub(p)?.Invoke(panel, null);
            repaint.Invoke(panel, null);
            render.Invoke(panel, null);
        }
    }
}
