using System.Collections.Generic;
using Broodline.Benchmark;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds Scenes/Benchmark.unity from a script so the measuring instrument for
/// the entity-count proof is reproducible and reviewable as text, rather than
/// hand-built in the editor.
///
///   Unity -batchmode -quit -projectPath client -executeMethod BenchmarkSceneBuilder.Build
public static class BenchmarkSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Benchmark.unity";

    // Mirrors WaveBenchmark.Spawn's grid for Wave44Composition (104 entities):
    // perRow = CeilToInt(Sqrt(104)) = 11, spacing 1.2m, so X spans roughly
    // [-6.6, 5.4] and Z spans roughly [0, 10.8] -- a ~12x12m area, just not
    // centered exactly on the world origin. Framing the camera on this
    // bounding box (rather than literal (0,0,0)) is what actually keeps
    // every spawned entity inside the frustum.
    const float GridCenterX = -0.6f;
    const float GridCenterZ = 5.4f;
    const float GridHalfExtent = 6.6f;

    // Phase0Setup.Apply locks the player to portrait orientation, so the
    // on-device aspect ratio (width/height) will be well below 1 -- an
    // iPhone-class portrait screen is roughly 9:19.5 (~0.46). An orthographic
    // camera's horizontal coverage is orthographicSize * aspect, so sizing
    // for a square view and trusting the Editor's (landscape) Game view
    // aspect would clip entities off the sides of a real device build. Size
    // against a conservative worst-case portrait aspect instead, with margin.
    const float MinPortraitAspect = 0.45f;
    const float FramingMargin = 1.15f;

    [MenuItem("Broodline/Build Benchmark Scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- SweepRunner: the harness itself.
        var runnerGO = new GameObject("SweepRunner");
        runnerGO.AddComponent<SweepRunner>();

        // --- Camera: orthographic top-down, sized so the ~12x12m spawn area
        // is fully inside the frustum even at the narrowest aspect the
        // device sweep will actually run at. An entity outside the frustum
        // is culled rather than rendered, which would silently turn this
        // into a culling benchmark instead of a rendering one.
        var camGO = new GameObject("BenchmarkCamera");
        var cam = camGO.AddComponent<Camera>();
        camGO.tag = "MainCamera";
        camGO.transform.SetPositionAndRotation(
            new Vector3(GridCenterX, 20f, GridCenterZ),
            Quaternion.Euler(90f, 0f, 0f));
        cam.orthographic = true;
        cam.orthographicSize = (GridHalfExtent * FramingMargin) / MinPortraitAspect;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 50f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        // --- Directional light: without one, URP/Lit shades to nothing and
        // the measured GPU cost is unrepresentative of the real render.
        var lightGO = new GameObject("Directional Light");
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = Color.white;
        light.intensity = 1f;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError("[BenchmarkSceneBuilder] failed to save " + ScenePath);
            return;
        }
        AssetDatabase.Refresh();

        // --- Register as build-settings index 0: the entry point for the
        // on-device player build in later steps.
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();

        AssetDatabase.SaveAssets();
        Debug.Log("[BenchmarkSceneBuilder] built " + ScenePath + " and set as build-settings index 0");
    }
}
