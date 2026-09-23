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
    /// Phase 9's baked creature sprites live under Resources (so
    /// `Resources.Load` can find them) rather than under `Assets/UI/Art/`
    /// where every other hand-authored UI texture sits - a different root,
    /// not a subfolder of it, so it needs its own guard rather than falling
    /// under the check below by accident.
    const string BakedCreaturesRoot = "Assets/UI/Resources/Art/creatures/";

    void OnPreprocessTexture()
    {
        var isBakedCreature = assetPath.StartsWith(BakedCreaturesRoot);
        if (!assetPath.StartsWith("Assets/UI/Art/") && !isBakedCreature) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Sprite;
        t.spriteImportMode = SpriteImportMode.Single;
        t.mipmapEnabled = false;
        t.filterMode = FilterMode.Bilinear;
        t.alphaIsTransparency = true;

        if (assetPath.EndsWith("shadow-card.png"))
            t.spriteBorder = new Vector4(20, 20, 20, 20);   // L, B, R, T

        // The baked creature sprites are READ BACK, not just drawn.
        // SilhouetteTests blits each body into a 40x40 RenderTexture and
        // thresholds its alpha to assert bible 10.2 rule 1 - and a crunched
        // or block-compressed source carries alpha that has been through a
        // lossy codec, so the mask it reads would be a measurement of the
        // compressor rather than of the drawing. ReadPixels after a Blit
        // does not need isReadable; it does need the source to be what was
        // authored. This used to key on `Assets/UI/Art/proxies/`, which
        // Phase 9's bake retires along with the rest of `SpeciesProxy`.
        if (isBakedCreature)
        {
            t.crunchedCompression = false;
            t.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
