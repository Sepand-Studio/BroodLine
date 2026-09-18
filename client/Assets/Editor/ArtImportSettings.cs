using UnityEditor;
using UnityEngine;

// No namespace: Assembly-CSharp-Editor, as above.

/// Import settings for the two generated UI textures.
///
/// In an AssetPostprocessor rather than committed .meta edits: a .meta is
/// regenerated on a fresh clone if the importer version moves, and the
/// nine-slice border silently reverting to zero turns every card shadow
/// into a stretched blur. This re-asserts it on every import.
public class ArtImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/UI/Art/")) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.mipmapEnabled = false;
        t.filterMode = FilterMode.Bilinear;
        t.alphaIsTransparency = true;

        if (assetPath.EndsWith("shadow-card.png"))
            t.spriteBorder = new Vector4(20, 20, 20, 20);   // L, B, R, T
    }
}
