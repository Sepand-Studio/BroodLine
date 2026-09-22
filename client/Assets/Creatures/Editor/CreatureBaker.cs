using System.IO;
using Broodline.Creatures;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Editor
{
    /// Renders every body, and every part in each socket on each body, from
    /// ONE fixed camera into aligned 192x192 PNGs with a transparent
    /// background. The card stacks body / dorsal / flank, so both combat
    /// traits are visible on every card at zero runtime cost - bible 10.4's
    /// single most important functional requirement, met in 2D.
    ///
    /// 192 is the proxies' convention: authored at 3x, drawn at 64, read at
    /// 40 by SilhouetteTests. Everything is on the Studio layer so no scene
    /// camera sees it and it sees no scene.
    public static class CreatureBaker
    {
        public const int Size = 192;
        const string OutRoot = "Assets/UI/Resources/Art/creatures/";
        static readonly Vector3 Far = new Vector3(0f, -500f, 0f);

        /// `Application.dataPath` is `client/Assets`; the repo root is two up.
        static string Project(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        static string Results(string file) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "implementation", "results", file));

        [MenuItem("Broodline/Bake Creature Sprites")]
        public static void Bake()
        {
            Directory.CreateDirectory(Project(OutRoot + "bodies"));
            Directory.CreateDirectory(Project(OutRoot + "parts"));

            var rig = new GameObject("bake-rig");
            var camera = new GameObject("bake-camera").AddComponent<Camera>();
            var light = new GameObject("bake-light").AddComponent<Light>();
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            int n = 0;
            try
            {
                camera.transform.SetParent(rig.transform, false);
                light.transform.SetParent(rig.transform, false);
                rig.transform.position = Far;

                camera.orthographic = true;
                camera.orthographicSize = 0.9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << CreatureAssembler.StudioLayer;
                camera.targetTexture = rt;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 20f;
                // Three-quarter front-left, a little above: the snout (+X) reads, the flank (-Z) faces us.
                camera.transform.localPosition = new Vector3(3.2f, 2.2f, -3.6f);
                camera.transform.LookAt(rig.transform.position + new Vector3(0f, 0.42f, 0f));

                light.type = LightType.Directional;
                light.cullingMask = 1 << CreatureAssembler.StudioLayer;
                light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                light.intensity = 1.1f;

                foreach (var body in SpeciesRecipes.All)
                {
                    Shoot(rig, camera, rt, new CreatureLook { Species = body.Id }, Project(OutRoot + "bodies/" + body.Id + ".png"), partOnly: null); n++;
                    foreach (var socket in new[] { Sockets.Dorsal, Sockets.Flank })
                        foreach (var part in PartRecipes.All)
                        {
                            var look = socket == Sockets.Dorsal
                                ? new CreatureLook { Species = body.Id, Trait1 = part.Id }
                                : new CreatureLook { Species = body.Id, Trait2 = part.Id };
                            Shoot(rig, camera, rt, look, Project(OutRoot + "parts/" + body.Id + "-" + socket + "-" + part.Id + ".png"), partOnly: socket); n++;
                        }
                }

                ContactSheet(rig, camera, rt);
                Shoot(rig, camera, rt, new CreatureLook { Species = "vetch", Trait1 = "cinder", Trait2 = "carapace" },
                      Results("cinderplate.png"), partOnly: null, composite: true);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(rig);
            }
            AssetDatabase.Refresh();
            Debug.Log("[bake] wrote " + n + " sprites under " + OutRoot);
        }

        /// One frame. `partOnly` hides the body so the part PNG holds only the
        /// part, at its socket, from the same camera - aligned with the body PNG.
        static void Shoot(GameObject rig, Camera camera, RenderTexture rt, CreatureLook look, string path, string partOnly, bool composite = false)
        {
            var creature = CreatureAssembler.Build(look);
            try
            {
                creature.transform.SetParent(rig.transform, false);
                creature.transform.localPosition = Vector3.zero;
                creature.transform.localRotation = Quaternion.identity;
                CreatureAssembler.SetLayerRecursively(creature, CreatureAssembler.StudioLayer);
                if (partOnly != null && !composite)
                {
                    var smr = creature.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) smr.enabled = false;
                    foreach (var s in new[] { Sockets.Dorsal, Sockets.Flank })
                        if (s != partOnly) { var t = creature.transform.Find(s); if (t != null) t.gameObject.SetActive(false); }
                }
                // `Tick(0, 0)` IS NOT A REST POSE, AND THE SPRITES MOVED BECAUSE
                // OF IT. `CreatureMotion.Awake` seeds its breath phase from
                // `Random.value`, and `Tick` reads `sin((time + phase) * ...)`,
                // so at time 0 the root still carries a random breath of up to
                // +-3.5%. Measured: two bakes of an UNCHANGED Vetch produced a
                // 100px sprite and then a 101px one, and Ember 137px then
                // 131px, and every body PNG turned up dirty in git after a
                // generate that changed nothing.
                //
                // It matters beyond the churn. `SilhouetteTests` reads these
                // PNGs at 40px against an 8% floor, and with six species its
                // closest pair (ember/hollow) measures 9.3% - a bake that
                // wobbles is a gate that wobbles, and this project's own rule
                // is that a stale or lucky pass is worse than a failure.
                //
                // Flattening the breath back out of the root after the tick
                // makes the sprite the creature at its AUTHORED size and the
                // bake reproducible. `CreatureMotion` is untouched: a wave is
                // still not a chorus line.
                var motion = creature.GetComponent<CreatureMotion>();
                motion.Tick(0f, 0f);
                var rest = creature.transform.Find("root") ?? creature.transform;
                rest.localScale = Vector3.one * motion.GrowthScale;

                camera.Render();
                var prev = RenderTexture.active;
                Texture2D tex = null;
                try
                {
                    RenderTexture.active = rt;
                    tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                }
                finally
                {
                    // A throw anywhere above (a bad ReadPixels rect, a disk
                    // error on WriteAllBytes) must not leave the GLOBAL
                    // RenderTexture.active pointed at `rt` for whatever the
                    // next Shoot or caller does next, and must not leak the
                    // scratch Texture2D - this is the one place in the task
                    // that touches global graphics state.
                    RenderTexture.active = prev;
                    if (tex != null) Object.DestroyImmediate(tex);
                }
            }
            finally { Object.DestroyImmediate(creature); }
        }

        /// rig_proof.md section 4 item 2, rendered: every part in both sockets
        /// on Vetch and Pale (48 tiles when Pale exists), for the eyes-on pass.
        static void ContactSheet(GameObject rig, Camera camera, RenderTexture rt)
        {
            var bodies = new System.Collections.Generic.List<BodyRecipe>();
            foreach (var id in new[] { "vetch", "pale" }) { var b = SpeciesRecipes.For(id); if (b != null) bodies.Add(b); }
            int cols = PartRecipes.All.Count, rows = bodies.Count * 2;
            if (cols == 0 || rows == 0) return;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGBA32, false);
            try
            {
                int row = 0;
                foreach (var body in bodies)
                    foreach (var socket in new[] { Sockets.Dorsal, Sockets.Flank })
                    {
                        int col = 0;
                        foreach (var part in PartRecipes.All)
                        {
                            var look = socket == Sockets.Dorsal
                                ? new CreatureLook { Species = body.Id, Trait1 = part.Id }
                                : new CreatureLook { Species = body.Id, Trait2 = part.Id };
                            var tmp = Path.Combine(Application.temporaryCachePath, "tile.png");
                            Shoot(rig, camera, rt, look, tmp, partOnly: null, composite: true);
                            Texture2D tile = null;
                            try
                            {
                                tile = new Texture2D(2, 2);
                                tile.LoadImage(File.ReadAllBytes(tmp));
                                sheet.SetPixels(col * Size, (rows - 1 - row) * Size, Size, Size, tile.GetPixels());
                            }
                            finally { if (tile != null) Object.DestroyImmediate(tile); }
                            col++;
                        }
                        row++;
                    }
                sheet.Apply();
                File.WriteAllBytes(Results("creature-contact-sheet.png"), sheet.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(sheet); }
        }
    }
}
