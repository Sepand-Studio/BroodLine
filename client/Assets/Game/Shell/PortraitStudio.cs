using System;
using Broodline.Creatures;
using Broodline.Frontier;
using Broodline.View;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// One camera, one render texture, one creature. Phase 9 design
    /// §3.8. Far below the origin on the Studio layer so no scene camera
    /// sees it and it sees no scene. Created once by BootController; the
    /// director shows and clears it around the three hero moments.
    public sealed class PortraitStudio : MonoBehaviour
    {
        public enum Setting { None, Habitat, Workshop }
        public const int Size = 512;
        static readonly Vector3 Far = new Vector3(0f, -400f, 0f);

        Camera _camera;
        RenderTexture _texture;
        GameObject _creature;
        GameObject _plinth;
        FrontierArt _art;

        public static PortraitStudio Create(Transform host)
        {
            var go = new GameObject("portrait-studio");
            go.transform.SetParent(host, false);
            go.transform.position = Far;
            go.layer = CreatureAssembler.StudioLayer;
            var studio = go.AddComponent<PortraitStudio>();

            var cam = new GameObject("camera").AddComponent<Camera>();
            cam.transform.SetParent(go.transform, false);
            cam.orthographic = true;
            cam.orthographicSize = 0.9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << CreatureAssembler.StudioLayer;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            cam.transform.localPosition = new Vector3(3.2f, 2.2f, -3.6f);
            cam.transform.LookAt(go.transform.position + new Vector3(0f, 0.42f, 0f));
            cam.gameObject.layer = CreatureAssembler.StudioLayer;

            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Directional;
            light.cullingMask = 1 << CreatureAssembler.StudioLayer;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            light.intensity = 1.1f;

            studio._camera = cam;
            studio._texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = studio._texture;
            cam.enabled = false;   // costs nothing until Show
            return studio;
        }

        bool _clearPending;

        /// Clear WHEN THE NEXT SCREEN PRESENTS, not now - Phase 10 Task 1.7.
        /// `ScreenHost.ScreenChanging` calls `ClearIfPending`; a `Show` in
        /// between cancels it, because a new creature is a new reason to run.
        public void ClearLater() => _clearPending = true;

        public void ClearIfPending()
        {
            if (!_clearPending) return;
            _clearPending = false;
            Clear();
        }

        public Texture Show(string species, string trait1, string trait2, float growth01,
            Setting setting = Setting.None)
        {
            _clearPending = false;
            Clear();
            if (_art == null) _art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));
            FrontierCreature creature;
            try { creature = _art.Creature(transform, species, trait1, trait2); }
            catch (ArgumentException error)
            {
                Debug.LogWarning("[portrait-studio] unsupported companion appearance: " + error.Message);
                return null;
            }
            creature.Growth = Mathf.Clamp01(growth01);
            creature.Pose(0f, true);
            _creature = creature.gameObject;
            CreatureAssembler.SetLayerRecursively(_creature, CreatureAssembler.StudioLayer);
            if (setting != Setting.None)
            {
                _plinth = _art.PortraitPlinth(transform, setting == Setting.Workshop);
                CreatureAssembler.SetLayerRecursively(_plinth, CreatureAssembler.StudioLayer);
            }
            FrontierPortraitCamera.Frame(_camera, transform, creature.PortraitBounds,
                creature.Rig, FrontierVisuals.For(species).Portrait);
            _camera.enabled = true;
            return _texture;
        }

        public void Clear()
        {
            if (_creature != null)
            {
                _creature.SetActive(false);
                if (Application.isPlaying) Destroy(_creature);
                else DestroyImmediate(_creature);
            }
            _creature = null;
            if (_plinth != null)
            {
                _plinth.SetActive(false);
                if (Application.isPlaying) Destroy(_plinth);
                else DestroyImmediate(_plinth);
            }
            _plinth = null;
            if (_camera != null) _camera.enabled = false;

            // Disabling the camera does not reset the texture it was
            // painting: the last frame rendered stays in GPU memory until
            // something writes over it, and `Show` hands the same `Texture`
            // reference out to whatever `CreatureStage` is displaying it -
            // so without this, a cleared studio still shows the last
            // creature. Same save-and-restore of the global
            // `RenderTexture.active` as `CreatureBaker.Shoot`, the other
            // place in this project that touches it directly.
            if (_texture != null)
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _texture;
                GL.Clear(true, true, new Color(0f, 0f, 0f, 0f));
                RenderTexture.active = prev;
            }
        }

        // FrontierCreature animates its own idle and reactions. Holding the
        // authored three-quarter angle keeps its face and trait sockets
        // visible inside the tighter hero framing.

        void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) _texture.Release();
            _art?.Dispose();
        }
    }
}
