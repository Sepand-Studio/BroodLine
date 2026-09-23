using System;
using System.IO;
using System.Reflection;
using Broodline.Frontier;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// Reproducible isolated scene; does not modify release build settings.
public static class FrontierProofBuilder
{
    public const string ScenePath = "Assets/Frontier/Generated/FrontierProof.unity";
    const string PanelPath = "Assets/Frontier/Generated/FrontierPanel.asset";

    [MenuItem("Broodline/Frontier Proof/Open")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding the proof.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Build();
    }

    // Unity -batchmode -quit -projectPath client -executeMethod FrontierProofBuilder.Build
    public static void Build()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Frontier/FrontierSurface.shader");
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Frontier/FrontierProof.uss");
        var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Shell/PanelSettings.asset");
        if (shader == null || ShaderUtil.ShaderHasError(shader) || sheet == null || existing == null)
            throw new InvalidOperationException("Missing/invalid Frontier shader, stylesheet, or existing shell PanelSettings. Check import errors first.");
        // Loading a non-null stylesheet alone does not establish that USS parsed.
        var rulesProperty = typeof(StyleSheet).GetProperty("rules", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (!(rulesProperty?.GetValue(sheet) is Array rules) || rules.Length == 0)
            throw new InvalidOperationException("Frontier stylesheet has no compiled rules. Check USS import errors.");
        foreach (string font in new[] { "Baloo2-Bold SDF.asset", "Nunito-Bold SDF.asset" })
            if (AssetDatabase.LoadMainAssetAtPath("Assets/UI/Fonts/" + font) == null)
                throw new InvalidOperationException("Missing font: " + font);
        if (!AssetDatabase.IsValidFolder("Assets/Frontier/Generated")) AssetDatabase.CreateFolder("Assets/Frontier", "Generated");
        var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
        if (panel == null)
        {
            panel = UnityEngine.Object.Instantiate(existing);
            panel.name = "FrontierPanel"; AssetDatabase.CreateAsset(panel, PanelPath);
        }
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(430, 932);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = 0;
        EditorUtility.SetDirty(panel);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Frontier Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.cullingMask = 1 << 6;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = FrontierArt.Hex("#6b9361");
        camera.nearClipPlane = .05f; camera.farClipPlane = 100;
        camera.allowHDR = false; camera.allowMSAA = false;
        var light = new GameObject("Warm afternoon sun").AddComponent<Light>();
        light.type = LightType.Directional; light.color = new Color(1, .97f, .91f); light.intensity = 1;
        light.shadows = LightShadows.Soft; light.transform.rotation = Quaternion.Euler(45, -40, 0);
        RenderSettings.sun = light;
        var root = new GameObject("Frontier Proof");
        var document = root.AddComponent<UIDocument>();
        // Unity's UIDocument setter does not reliably serialize from batch builders.
        var serialized = new SerializedObject(document);
        serialized.FindProperty("m_PanelSettings").objectReferenceValue = panel;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<FrontierProof>().Configure(shader, sheet, camera);
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Frontier proof] built " + ScenePath + ". Set Game view to 430 x 932 and press Play.");
    }

    [MenuItem("Broodline/Frontier Proof/Capture Game View")]
    public static void Capture()
    {
        if (!EditorApplication.isPlaying || UnityEngine.Object.FindFirstObjectByType<FrontierProof>() == null)
            throw new InvalidOperationException("Run the Frontier proof before capturing.");
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../implementation/results/frontier"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "frontier-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[Frontier proof] queued Game-view capture: " + path);
    }
}
