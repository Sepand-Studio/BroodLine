using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder.

/// Renders any one screen against fixture data, with no server, no
/// session and no beat machine.
///
/// Phase 7 Task 17 Step 6 - "press Play in Boot.unity and walk beats 1-8"
/// against a local Postgres, a published bundle and a running api - was
/// not run, and the length of that sentence is why. This is the same look
/// at the same screens for the cost of one menu item.
public class ScreenHarness : EditorWindow
{
    [MenuItem("Broodline/Screen Harness")]
    static void Open() => GetWindow<ScreenHarness>("Screens").minSize = new Vector2(460, 960);

    int _index;

    void CreateGUI()
    {
        var names = ScreenFixtures.Names;
        var picker = new PopupField<string>("Screen", new List<string>(names), 0);

        // On the window's own root rather than on `host` below, so the
        // cascade reaches a screen the same way Shell.uxml's reaches one at
        // runtime. Tokens resolve here - verified by measurement, not by
        // eye; see AddShellStyles.
        AddShellStyles(rootVisualElement);
        rootVisualElement.AddToClassList("shell-root");

        var host = new VisualElement { style = { flexGrow = 1, width = 430, height = 932 } };

        void Show()
        {
            host.Clear();
            host.Add(ScreenFixtures.Build(names[_index]));
        }
        picker.RegisterValueChangedCallback(e => { _index = names.IndexOf(e.newValue); Show(); });

        rootVisualElement.Add(picker);
        rootVisualElement.Add(host);
        Show();
    }

    /// The shell's own stylesheets, in the order Shell.uxml would cascade
    /// them at a real panel root - Tokens (design tokens as custom
    /// properties), Theme (element defaults), then the icon and motion
    /// sheets Phase 8's later tasks add.
    ///
    /// ATTACHMENT POINT IS NOT LOAD-BEARING, THOUGH IT READS AS IF IT MIGHT
    /// BE. `:root`, `var()` and plain class selectors were each measured
    /// resolving correctly from a sheet attached at the window root, at the
    /// panel root, and at a child of either - so none of the three is what
    /// decides whether a token resolves. What decided it was whether the
    /// sheet parsed at all: `Tokens.uss` was compiling to zero rules.
    /// `ScreenshotCapture.cs`'s class comment has the full account. Pass
    /// this whichever element the screen actually hangs under and it works;
    /// `check-stylesheets.sh` is what guards the thing that really breaks.
    ///
    /// NULL-GUARDED, SEPARATELY. `AssetDatabase.LoadAssetAtPath` returns
    /// null for a path that does not exist rather than throwing, and
    /// `VisualElement.styleSheets.Add` throws `ArgumentNullException` on
    /// that null - so loading a sheet Phase 8 has not written yet would take
    /// down every screen in the picker, not just the one that wanted it.
    /// `icons.uss` and `Motion.uss` do not exist on this branch as of
    /// Task 14/15 (grep confirms it - see task-14-15-report.md): building
    /// the icon set and the motion sheet are later Phase 8 tasks. Once
    /// either lands at the path below, this picks it up with no further
    /// change here.
    internal static void AddShellStyles(VisualElement panelRoot)
    {
        AddStyleIfPresent(panelRoot, "Assets/UI/Shell/Tokens.uss");
        AddStyleIfPresent(panelRoot, "Assets/UI/Shell/Theme.uss");
        AddStyleIfPresent(panelRoot, "Assets/UI/Shell/icons.uss");
        AddStyleIfPresent(panelRoot, "Assets/UI/Shell/Motion.uss");
    }

    static void AddStyleIfPresent(VisualElement panelRoot, string path)
    {
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
        if (sheet != null) panelRoot.styleSheets.Add(sheet);
    }
}
