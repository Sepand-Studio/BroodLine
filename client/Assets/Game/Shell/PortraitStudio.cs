using Broodline.Creatures;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// One camera, one render texture, one creature, turning. Phase 9 design
    /// §3.8. Far below the origin on the Studio layer so no scene camera
    /// sees it and it sees no scene. Created once by BootController; the
    /// director shows and clears it around the three hero moments.
    public sealed class PortraitStudio : MonoBehaviour
    {
        public const int Size = 512;
        public const float TurnDegreesPerSecond = 28f;
        static readonly Vector3 Far = new Vector3(0f, -400f, 0f);

        Camera _camera;
        RenderTexture _texture;
        GameObject _creature;

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

        public Texture Show(string species, string trait1, string trait2, float growth01)
        {
            _clearPending = false;
            Clear();
            _creature = CreatureAssembler.Build(new CreatureLook { Species = species, Trait1 = trait1, Trait2 = trait2, Growth01 = growth01 });
            _creature.transform.SetParent(transform, false);
            _creature.transform.localPosition = Vector3.zero;
            CreatureAssembler.SetLayerRecursively(_creature, CreatureAssembler.StudioLayer);
            _camera.enabled = true;
            return _texture;
        }

        public void Clear()
        {
            if (_creature != null) Destroy(_creature);
            _creature = null;
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

        void Update()
        {
            if (_creature != null) _creature.transform.Rotate(0f, TurnDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) _texture.Release();
        }
    }
}
