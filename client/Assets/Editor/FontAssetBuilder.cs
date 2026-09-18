using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

// NO NAMESPACE, deliberately. Broodline.EditorBuild.asmdef lives in
// Assets/Editor/BuildSteps/, so a file directly under Assets/Editor/ is in
// Assembly-CSharp-Editor - which is where WaveBuilder and BenchmarkBuilder
// already are, both global. -executeMethod takes the type name as it is.

/// Generates the two UI Toolkit FontAssets from the committed TTFs.
///
/// Batch rather than by hand because a FontAsset created through the
/// editor UI records the atlas settings whoever created it happened to
/// have, and those settings are what decides whether a numeral renders
/// crisply at 11px. One entry point, one set of values, reproducible.
public static class FontAssetBuilder
{
    const int AtlasWidth = 1024, AtlasHeight = 1024, SamplingPointSize = 90, Padding = 9;

    [MenuItem("Broodline/Rebuild Font Assets")]
    public static void Rebuild()
    {
        Build("Assets/UI/Fonts/Baloo2-Bold.ttf",  "Assets/UI/Fonts/Baloo2-Bold SDF.asset");
        Build("Assets/UI/Fonts/Nunito-Bold.ttf",  "Assets/UI/Fonts/Nunito-Bold SDF.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Build(string ttf, string outPath)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null) throw new System.IO.FileNotFoundException($"no TTF at {ttf}");

        var asset = FontAsset.CreateFontAsset(
            font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
            AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
        asset.name = System.IO.Path.GetFileNameWithoutExtension(outPath);

        AssetDatabase.DeleteAsset(outPath);
        AssetDatabase.CreateAsset(asset, outPath);
        // The atlas texture and material are sub-assets, or the .asset
        // references objects that do not survive a reimport.
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
    }
}
