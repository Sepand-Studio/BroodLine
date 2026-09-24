using System;
using Broodline.Game;
using Broodline.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// Builds Scenes/Wave.unity from a script so the Phase 3 battlefield is
/// reproducible and reviewable as text, rather than hand-built in the editor.
///
///   Unity -batchmode -quit -projectPath client -executeMethod WaveSceneBuilder.Build
///
/// Lives beside BenchmarkSceneBuilder in Assets/Editor, in the default
/// Assembly-CSharp-Editor and with no asmdef of its own - that is the pattern
/// Phase 0 set, and Broodline.Game is autoReferenced so it is reachable.
public static class WaveSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Wave.unity";

    /// The HUD draws into the SHELL's panel, not one of its own.
    ///
    /// Wave Defense loads additively over the shell (client_architecture
    /// section 9), and two PanelSettings assets would mean two panels, two
    /// scale calculations against the same screen, and a HUD whose text size
    /// could disagree with every other screen in the app. One asset, one
    /// panel, and the wave's document sorts above the shell's.
    const string PanelSettingsPath = "Assets/UI/Shell/PanelSettings.asset";

    /// Above the shell's document, which leaves its sorting order at 0.
    const float HudSortingOrder = 1f;

    [MenuItem("Broodline/Build Wave Scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        // The painted plane normally fills the frame; this is its soft
        // fallback when the texture is absent or the camera sees past it.
        camera.backgroundColor = BattleBackdrop.FallbackField;
        // Everything but Studio (layer 6) - CreatureBaker's isolated bake rig
        // lives there so no scene camera sees it; this camera is a scene
        // camera.
        camera.cullingMask = ~(1 << 6);

        BattleCameraFrame.Apply(camera, WaveRunner.LaneTiles);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var runnerGo = new GameObject("WaveRunner");
        runnerGo.AddComponent<WaveRunner>();
        AddHudDocument(runnerGo);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[broodline] wrote " + ScenePath +
                  "  perspectiveFov=" + camera.fieldOfView +
                  "  laneTiles=" + WaveRunner.LaneTiles);
    }

    /// The UIDocument the UI Toolkit HUD attaches to.
    ///
    /// No visualTreeAsset: `WaveRunner` adds a `WaveHudView` to the root at
    /// run time, and that view loads its own UXML. The document is here only
    /// to provide the panel, which is why it carries settings and nothing
    /// else.
    ///
    /// THE COMPONENT LEAVES `standaloneCapture` TRUE, which is the whole
    /// reason this scene is still playable on its own: it is the Editor half
    /// of the replay round-trip capture (Task 10 of the Phase 3 plan, and
    /// `EditorReplayTests` re-simulates what it writes). `WaveRunner.Hosted`
    /// is what a hosted run clears it with, and nothing in this builder
    /// touches it - the field's own initializer is the default, and
    /// `WaveRunnerTests` asserts the saved scene still carries it.
    static void AddHudDocument(GameObject go)
    {
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings == null)
            throw new InvalidOperationException(
                "[WaveSceneBuilder] no PanelSettings at " + PanelSettingsPath +
                " - run Broodline > Build Boot Scene first; it authors the one panel both scenes share.");

        var document = go.AddComponent<UIDocument>();

        // NOT `document.panelSettings = panelSettings;`. BootSceneBuilder
        // records why at length: that property setter's assignment does not
        // survive SaveScene under -batchmode -executeMethod, and the scene
        // comes back with `m_PanelSettings: {fileID: 0}`. SerializedObject
        // writes the field directly and has none of that side effect.
        var serialized = new SerializedObject(document);
        serialized.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
        serialized.FindProperty("m_SortingOrder").floatValue = HudSortingOrder;
        serialized.ApplyModifiedProperties();
    }
}
