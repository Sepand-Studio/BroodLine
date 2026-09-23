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
                var definition = FrontierVisuals.For(species);
                var bounds = CombinedBounds(art, host.transform, species);
                FrontierPortraitCamera.Frame(camera, host.transform, bounds, definition.Rig, definition.Portrait);
                Shoot(art, host.transform, camera, texture, species, null, null,
                    Root + "/bodies/" + species + ".png", null);
                count++;
                foreach (var trait in Traits)
                {
                    Shoot(art, host.transform, camera, texture, species, trait, null,
                        Root + "/parts/" + species + "-sk_dorsal-" + trait + ".png", "sk_dorsal");
                    Shoot(art, host.transform, camera, texture, species, null, trait,
                        Root + "/parts/" + species + "-sk_flank-" + trait + ".png", "sk_flank");
                    count += 2;
                }
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
}
