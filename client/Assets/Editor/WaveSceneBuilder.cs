using Broodline.Game;
using Broodline.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

    // PORTRAIT. Phase0Setup locks the player to portrait and
    // client_architecture section 10 keeps it there, so the on-device aspect
    // (width/height) is roughly 0.46 on an iPhone-class screen.
    //
    // That makes the naive framing wrong in a way the Editor's landscape Game
    // view hides completely: the lane is 24 tiles, and an orthographic camera
    // covers orthographicSize * aspect * 2 horizontally. Laying the lane across
    // the screen would need orthographicSize ~26, giving 52 units of vertical
    // coverage for a 2-unit-tall battlefield - the lane would be a hairline.
    //
    // So the lane runs UP the screen, which is also what the shipped Wave
    // Defense screen does: screen_inventory section 4.2 puts "pockets beside
    // the lane" in a portrait frame. The camera looks straight down with world
    // +X mapped to screen-up, and orthographicSize sizes the lane's length.
    //
    // BenchmarkSceneBuilder hit the same trap from the other side and says so;
    // this is the same lesson applied to a long thin battlefield.
    const float MinPortraitAspect = 0.45f;
    const float FramingMargin = 1.08f;

    [MenuItem("Broodline/Build Wave Scene")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        float laneLength = WaveRunner.LaneTiles * WaveView.TileSize;
        float laneMidX = laneLength * 0.5f;

        // The lane sits on z = 0 and pockets at z = +1, so the occupied strip
        // is about [-1, +2] in z. Centre on it rather than on the lane itself,
        // or the pockets sit against the right edge.
        const float occupiedMinZ = -1f;
        const float occupiedMaxZ = WaveView.PocketOffset + 1f;
        float stripMidZ = (occupiedMinZ + occupiedMaxZ) * 0.5f;
        float stripWidth = occupiedMaxZ - occupiedMinZ;

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.07f, 0.09f);

        // orthographicSize is the VERTICAL half-extent. Screen-up is world +X,
        // so it sizes the lane's length; then check the across-screen axis has
        // room for the pocket strip at the worst-case portrait aspect, and grow
        // if it does not.
        float sizeForLane = laneLength * 0.5f * FramingMargin;
        float sizeForStrip = (stripWidth * 0.5f * FramingMargin) / MinPortraitAspect;
        camera.orthographicSize = Mathf.Max(sizeForLane, sizeForStrip);

        cameraGo.transform.position = new Vector3(laneMidX, 20f, stripMidZ);
        // Forward = straight down, up = world +X. That is what puts the lane
        // along the tall axis of a portrait screen.
        cameraGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right);

        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        new GameObject("WaveRunner").AddComponent<WaveRunner>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[broodline] wrote " + ScenePath +
                  "  orthographicSize=" + camera.orthographicSize +
                  "  laneLength=" + laneLength);
    }
}
