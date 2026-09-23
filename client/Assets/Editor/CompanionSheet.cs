using System.Collections.Generic;
using Broodline.Frontier;
using Broodline.View;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, like ScreenFixtures.

/// UNITY-RENDERED COMPANION SHEET FOR THE CAPTURE CORPUS - Phase 10 Batch 2.
/// The browser comparison page establishes geometry; this establishes what the
/// real shader, lights and skinning make of it. Each body is built by
/// `FrontierArt`, posed at rest (or walking) by `FrontierCreature.Pose`, and
/// rendered once by an off-screen camera into a texture the fixture shows.
/// Deterministic: fixed camera, fixed light, `Pose(t, rest)` at a fixed time.
public static class CompanionSheet
{
    public const int Tile = 256;

    public static Texture2D Render(string species, string first, string second, float growth, bool walking, float yaw, float pitch)
    {
        var root = new GameObject("companion-sheet");
        root.transform.position = new Vector3(0, -3200, 0);
        try
        {
            var shader = Shader.Find(RuntimeShaders.Frontier);
            using (var art = new FrontierArt(shader))
            {
                var creature = art.Creature(root.transform, species, first, second);
                creature.Growth = growth;
                creature.Moving = walking;
                creature.Pose(walking ? .35f : 0f, rest: !walking);

                var cam = new GameObject("camera").AddComponent<Camera>();
                cam.transform.SetParent(root.transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.fieldOfView = 28f;
                cam.nearClipPlane = .05f; cam.farClipPlane = 50f;
                var bounds = creature.PortraitBounds;
                var centre = root.transform.TransformPoint(bounds.center);
                float radius = bounds.extents.magnitude * 1.05f;
                float distance = radius / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
                var dir = Quaternion.Euler(pitch, yaw, 0) * Vector3.right;
                cam.transform.position = centre + dir * distance;
                cam.transform.LookAt(centre);

                var light = new GameObject("light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                light.intensity = 1.1f;

                var rt = new RenderTexture(Tile, Tile, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var texture = new Texture2D(Tile, Tile, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, Tile, Tile), 0, 0);
                texture.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                return texture;
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// The fixture: six companions at rest in three-quarter view, then the same
    /// six walking with Cinder and Carapace mounted, two rows of three each.
    public static VisualElement Build()
    {
        var page = new VisualElement();
        page.style.paddingLeft = 12; page.style.paddingRight = 12; page.style.paddingTop = 12;
        var title = new Label("Companions · Unity render · rest, then walking with Cinder + Carapace");
        title.AddToClassList("t-secondary");
        page.Add(title);
        foreach (var (walking, parts) in new[] { (false, false), (true, true) })
        {
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap;
            foreach (var species in FrontierRigDefinition.Companions)
            {
                var cell = new VisualElement();
                cell.style.width = 135; cell.style.height = 150; cell.style.alignItems = Align.Center;
                var image = new Image { image = Render(species, parts ? "cinder" : null, parts ? "carapace" : null, .3f, walking, -35f, 12f) };
                image.style.width = 128; image.style.height = 128;
                image.style.backgroundColor = new Color(.149f, .2f, .271f);   // --surface-deep
                cell.Add(image);
                var name = new Label(char.ToUpperInvariant(species[0]) + species.Substring(1));
                name.AddToClassList("t-micro");
                cell.Add(name);
                grid.Add(cell);
            }
            page.Add(grid);
        }
        return page;
    }
}
