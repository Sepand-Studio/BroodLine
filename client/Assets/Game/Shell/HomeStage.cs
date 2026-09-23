using System.Collections.Generic;
using Broodline.Creatures;
using Broodline.Frontier;
using Broodline.View;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// THE HOME BASE, PAINTED - Phase 10 Task 1.4. `LaneStage`'s pattern: an
    /// off-screen rig far below the origin on the Studio layer, a disabled
    /// camera that renders one frame on demand into a portrait RenderTexture,
    /// and normalized anchors for the facility markers so the UI never sees a
    /// camera. The base is built once, lazily, from `FrontierArt.Base`.
    public sealed class HomeStage : MonoBehaviour
    {
        public const int Width = 720, Height = 1280;
        public const float FieldOfView = 45f;

        static readonly Vector3 Far = new Vector3(0f, -1600f, 0f);

        Camera _camera;
        RenderTexture _texture;
        FrontierArt _art;
        GameObject _base;

        public static HomeStage Create(Transform host)
        {
            var go = new GameObject("home-stage");
            go.transform.SetParent(host, false);
            go.transform.position = Far;
            go.layer = CreatureAssembler.StudioLayer;
            var stage = go.AddComponent<HomeStage>();

            var cam = new GameObject("camera").AddComponent<Camera>();
            cam.transform.SetParent(go.transform, false);
            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FrontierArt.Hex("#263345");   // --surface-deep, the frame's own fill
            cam.cullingMask = 1 << CreatureAssembler.StudioLayer;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            cam.gameObject.layer = CreatureAssembler.StudioLayer;
            // Three-quarter view: the base's long axis runs across the frame
            // and the Ark sits just above centre, leaving the lower third for
            // the drive plot and the upper third for sky and canopy.
            // Portrait frames are narrow: at 45 degrees vertical and a 9:16
            // aspect the horizontal half-angle is ~13 degrees, so the camera
            // sits ~19 units out to hold the platform's 9-unit span with a
            // margin. HomeStageTests pins every plot inside 5-95% of the frame.
            cam.transform.localPosition = new Vector3(10f, 13f, -10f);
            cam.transform.LookAt(go.transform.position + new Vector3(0f, .3f, -.3f));

            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Directional;
            light.cullingMask = 1 << CreatureAssembler.StudioLayer;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;

            stage._camera = cam;
            stage._texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = stage._texture;
            cam.enabled = false;
            return stage;
        }

        /// Builds the base on first use, paints one frame, returns the texture.
        public Texture Show()
        {
            if (_base == null)
            {
                _art = new FrontierArt(RuntimeShaders.Require(RuntimeShaders.Frontier));
                _base = _art.Base(transform);
                _base.transform.localPosition = Vector3.zero;
                CreatureAssembler.SetLayerRecursively(_base, CreatureAssembler.StudioLayer);
            }
            _camera.Render();
            return _texture;
        }

        /// Each plot's position as a fraction of the frame, top-left origin,
        /// in `FrontierArt.Plots` order.
        public IReadOnlyList<(string Id, float X01, float Y01)> PlotAnchors()
        {
            var anchors = new List<(string, float, float)>(FrontierArt.Plots.Count);
            foreach (var (id, at) in FrontierArt.Plots)
            {
                var v = _camera.WorldToViewportPoint(transform.position + at + Vector3.up * .55f);
                anchors.Add((id, Mathf.Clamp01(v.x), Mathf.Clamp01(1f - v.y)));
            }
            return anchors;
        }

        /// Triangles in the built base, for the budget test.
        public int TriangleCount()
        {
            if (_base == null) return 0;
            int n = 0;
            foreach (var f in _base.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null) n += f.sharedMesh.triangles.Length / 3;
            return n;
        }

        void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) _texture.Release();
            _art?.Dispose();
        }
    }
}
