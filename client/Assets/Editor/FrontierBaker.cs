using System.Collections.Generic;
using System.IO;
using Broodline.Frontier;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Bakes the production card layers from the same builders as live portraits.
/// The existing resource paths remain stable while the old creature pipeline
/// still supplies raiders in live combat.
public static class FrontierBaker
{
    const int Size = 192;
    const int CropPadding = 8;
    const int StudioLayer = 6;
    const string Root = "Assets/UI/Resources/Art/creatures";
    static readonly string[] Traits = { "cinder", "carapace", "chill", "taunt", "splash" };

    [MenuItem("Broodline/Bake Frontier Companion Cards")]
    public static void Bake()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "UI/Resources/Art/creatures/bodies"));
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "UI/Resources/Art/creatures/parts"));
        var host = new GameObject("frontier-card-bake");
        host.transform.position = new Vector3(0, -1200, 0);
        var camera = new GameObject("camera").AddComponent<Camera>();
        camera.transform.SetParent(host.transform, false);
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;
        camera.cullingMask = 1 << StudioLayer;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 20f;
        camera.enabled = false;
        var light = new GameObject("light").AddComponent<Light>();
        light.transform.SetParent(host.transform, false);
        light.type = LightType.Directional;
        light.cullingMask = 1 << StudioLayer;
        light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
        light.intensity = 1.1f;
        var texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        camera.targetTexture = texture;
        FrontierArt art = null;
        var count = 0;
        try
        {
            var shader = Shader.Find("Broodline/FrontierSurface");
            if (shader == null) throw new System.InvalidOperationException("FrontierSurface shader is missing");
            art = new FrontierArt(shader);
            foreach (var species in FrontierRigDefinition.Companions)
            {
                var paths = new List<string>();
                var definition = FrontierVisuals.For(species);
                var bounds = CombinedBounds(art, host.transform, species);
                FrontierPortraitCamera.Frame(camera, host.transform, bounds, definition.Rig, definition.Portrait);
                var bodyPath = Root + "/bodies/" + species + ".png";
                Shoot(art, host.transform, camera, texture, species, null, null, bodyPath, null);
                paths.Add(bodyPath);
                count++;
                foreach (var trait in Traits)
                {
                    var dorsalPath = Root + "/parts/" + species + "-sk_dorsal-" + trait + ".png";
                    var flankPath = Root + "/parts/" + species + "-sk_flank-" + trait + ".png";
                    Shoot(art, host.transform, camera, texture, species, trait, null, dorsalPath, "sk_dorsal");
                    Shoot(art, host.transform, camera, texture, species, null, trait, flankPath, "sk_flank");
                    paths.Add(dorsalPath);
                    paths.Add(flankPath);
                    count += 2;
                }
                NormalizeLayers(paths);
            }
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(host);
            art?.Dispose();
        }
        AssetDatabase.Refresh();
        Debug.Log("[frontier-bake] wrote " + count + " companion card layers");
    }

    static Bounds CombinedBounds(FrontierArt art, Transform host, string species)
    {
        var creature = art.Creature(host, species);
        var bounds = creature.PortraitBounds;
        Object.DestroyImmediate(creature.gameObject);
        foreach (var trait in Traits)
            foreach (var dorsal in new[] { true, false })
            {
                creature = art.Creature(host, species, dorsal ? trait : null, dorsal ? null : trait);
                bounds.Encapsulate(creature.PortraitBounds);
                Object.DestroyImmediate(creature.gameObject);
            }
        return bounds;
    }

    static void Shoot(FrontierArt art, Transform host, Camera camera, RenderTexture target,
        string species, string dorsal, string flank, string path, string partOnly)
    {
        var creature = art.Creature(host, species, dorsal, flank);
        try
        {
            creature.Pose(0f, true);
            SetLayer(creature.gameObject);
            if (partOnly != null)
            {
                creature.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
                (partOnly == "sk_dorsal" ? creature.Flank : creature.Dorsal).gameObject.SetActive(false);
            }
            if (GraphicsSettings.currentRenderPipeline != null)
                camera.SubmitRenderRequest(new RenderPipeline.StandardRequest { destination = target });
            else camera.Render();

            var previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                RenderTexture.active = target;
                image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Object.DestroyImmediate(image);
            }
        }
        finally { Object.DestroyImmediate(creature.gameObject); }
    }

    static void SetLayer(GameObject root)
    {
        root.layer = StudioLayer;
        foreach (Transform child in root.transform) SetLayer(child.gameObject);
    }

    /// Crops every layer for one species through the same square. Body and
    /// traits therefore remain pixel-aligned, while the fallback card no
    /// longer inherits the large empty margin needed to frame every possible
    /// attachment in world space. The padding is part of the crop before it
    /// is scaled, so even the widest layer keeps a visible transparent inset.
    static void NormalizeLayers(IReadOnlyList<string> paths)
    {
        var sources = new List<Texture2D>();
        int minX = Size, minY = Size, maxX = -1, maxY = -1;
        try
        {
            foreach (var path in paths)
            {
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!image.LoadImage(File.ReadAllBytes(Absolute(path))))
                    throw new IOException("Could not decode Frontier card layer " + path);
                if (image.width != Size || image.height != Size)
                    throw new IOException("Frontier card layer is not " + Size + "x" + Size + ": " + path);
                sources.Add(image);
                var pixels = image.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a <= 127) continue;
                    int x = i % Size;
                    int y = i / Size;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                throw new IOException("Frontier card layers are all transparent for " + paths[0]);

            int side = Mathf.Min(Size,
                Mathf.Max(maxX - minX + 1, maxY - minY + 1) + CropPadding * 2);
            float centerX = (minX + maxX + 1) * .5f;
            float centerY = (minY + maxY + 1) * .5f;
            int cropX = Mathf.Clamp(Mathf.RoundToInt(centerX - side * .5f), 0, Size - side);
            int cropY = Mathf.Clamp(Mathf.RoundToInt(centerY - side * .5f), 0, Size - side);

            for (int i = 0; i < sources.Count; i++)
            {
                var crop = new Texture2D(side, side, TextureFormat.RGBA32, false);
                var output = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var target = RenderTexture.GetTemporary(Size, Size, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var previous = RenderTexture.active;
                try
                {
                    crop.SetPixels(sources[i].GetPixels(cropX, cropY, side, side));
                    crop.Apply();
                    crop.filterMode = FilterMode.Bilinear;
                    crop.wrapMode = TextureWrapMode.Clamp;
                    Graphics.Blit(crop, target);
                    RenderTexture.active = target;
                    output.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    output.Apply();
                    File.WriteAllBytes(Absolute(paths[i]), output.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    Object.DestroyImmediate(crop);
                    Object.DestroyImmediate(output);
                }
            }
        }
        finally
        {
            foreach (var source in sources) Object.DestroyImmediate(source);
        }
    }

    static string Absolute(string path) =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
}
