using System;
using Broodline.Game.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// Builds Scenes/Boot.unity from a script, the WaveSceneBuilder pattern, so
/// the app's one persistent root scene is reproducible and reviewable as text
/// rather than hand-built in the editor:
///
///   Unity -batchmode -quit -projectPath client -executeMethod BootSceneBuilder.Build
///
/// One GameObject, "Shell", carrying a UIDocument (this PanelSettings and
/// Shell.uxml) and BootController - client_architecture section 9's whole
/// composition root for the shell.
///
/// Lives beside WaveSceneBuilder in Assets/Editor, in the default
/// Assembly-CSharp-Editor with no asmdef of its own - the pattern Phase 0 set.
public static class BootSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Boot.unity";
    const string WavePath = "Assets/Scenes/Wave.unity";
    const string ShellFolder = "Assets/UI/Shell";
    const string PanelSettingsPath = ShellFolder + "/PanelSettings.asset";
    const string UxmlPath = ShellFolder + "/Shell.uxml";

    [MenuItem("Broodline/Build Boot Scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var panelSettings = BuildPanelSettings();

        var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (uxml == null)
            throw new InvalidOperationException(
                "[BootSceneBuilder] no VisualTreeAsset at " + UxmlPath + " - Shell.uxml is missing or failed to import.");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var shellGo = new GameObject("Shell");
        var document = shellGo.AddComponent<UIDocument>();

        // NOT `document.panelSettings = panelSettings;`. That property
        // setter's assignment did not survive SaveScene here - the written
        // Boot.unity came back with `m_PanelSettings: {fileID: 0}` even
        // though the very next line's `document.visualTreeAsset = uxml`
        // DOES persist (verified: `sourceAsset` serializes correctly). The
        // setter's own "attach to panel" side effect appears to require the
        // component to already be live in a running panel, which a
        // freshly-added component under `-batchmode -executeMethod` is not.
        // SerializedObject writes the field directly and has none of that.
        var serializedDocument = new SerializedObject(document);
        serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panelSettings;
        serializedDocument.ApplyModifiedProperties();

        document.visualTreeAsset = uxml;
        shellGo.AddComponent<BootController>();

        EditorSceneManager.SaveScene(scene, ScenePath);

        // Boot first, Wave second - Wave Defense loads additively over the
        // shell (client_architecture section 9); Benchmark and SampleScene
        // are pre-app scratch scenes and are not part of the shipped app.
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(WavePath, true),
        };

        Debug.Log("[broodline] wrote " + ScenePath + " and set EditorBuildSettings.scenes to [Boot, Wave]");
    }

    static PanelSettings BuildPanelSettings()
    {
        if (!AssetDatabase.IsValidFolder(ShellFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/UI"))
                AssetDatabase.CreateFolder("Assets", "UI");
            AssetDatabase.CreateFolder("Assets/UI", "Shell");
        }

        // Idempotent: a second run reconfigures the existing asset rather
        // than minting a new GUID that would orphan the UIDocument's
        // reference to the checked-in one.
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null) return Configure(existing);

        var created = Configure(ScriptableObject.CreateInstance<PanelSettings>());
        AssetDatabase.CreateAsset(created, PanelSettingsPath);
        return created;
    }

    static PanelSettings Configure(PanelSettings panelSettings)
    {
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(390, 844);
        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panelSettings.match = 0.5f;
        EditorUtility.SetDirty(panelSettings);
        return panelSettings;
    }
}
