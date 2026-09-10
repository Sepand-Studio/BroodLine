using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// One-shot project configuration for the Phase 0 entity-count proof.
/// Applied from the CLI so the settings are reproducible and do not depend on
/// where a given Unity version happens to put them in Project Settings.
///
///   Unity -batchmode -quit -projectPath client -executeMethod Phase0Setup.Apply
public static class Phase0Setup
{
    [MenuItem("Broodline/Apply Phase 0 Setup")]
    public static void Apply()
    {
        // --- Orientation: portrait only. client_architecture section 10.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        Debug.Log("[Phase0Setup] orientation -> Portrait, autorotate restricted");

        // --- Text serialization and visible meta files.
        EditorSettings.serializationMode = SerializationMode.ForceText;
        Debug.Log("[Phase0Setup] serialization -> ForceText");
        // Visible Meta Files is the default in Unity 6 and its setter is
        // obsolete, so it is asserted by the presence of .meta files rather
        // than set here.

        // --- URP/Lit must survive shader stripping. Shader.Find returns null in
        //     a player otherwise, and every device run renders magenta.
        AddAlwaysIncludedShader("Universal Render Pipeline/Lit");

        // --- FrameTimingManager returns nothing without this enabled.
        PlayerSettings.enableFrameTimingStats = true;
        Debug.Log("[Phase0Setup] enableFrameTimingStats -> true");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Phase0Setup] DONE");
    }

    /// Switching the build target is separate: it is slow, and it lives in
    /// Library/ rather than in a committed file.
    [MenuItem("Broodline/Switch Build Target to iOS")]
    public static void SwitchToIOS()
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
        {
            Debug.Log("[Phase0Setup] build target already iOS");
            return;
        }
        Debug.Log("[Phase0Setup] switching build target to iOS (slow)...");
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        Debug.Log("[Phase0Setup] build target -> " + EditorUserBuildSettings.activeBuildTarget);
    }

    static void AddAlwaysIncludedShader(string shaderName)
    {
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            Debug.LogError("[Phase0Setup] shader not found: " + shaderName);
            return;
        }

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError("[Phase0Setup] could not load GraphicsSettings.asset");
            return;
        }

        var so = new SerializedObject(assets[0]);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
            {
                Debug.Log("[Phase0Setup] already included: " + shaderName);
                return;
            }
        }

        int idx = list.arraySize;
        list.InsertArrayElementAtIndex(idx);
        list.GetArrayElementAtIndex(idx).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[Phase0Setup] always-included shader added: " + shaderName);
    }
}
