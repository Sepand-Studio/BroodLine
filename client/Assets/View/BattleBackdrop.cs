using UnityEngine;

namespace Broodline.View
{
    /// A quiet painted ground layer behind the shared Frontier lane. The
    /// geometry is deliberately flat and below the procedural field, so it
    /// never changes a pocket, path, camera fit or simulation collision.
    public sealed class BattleBackdrop : MonoBehaviour
    {
        public const string ResourcePath = "Art/battle-backdrop";
        public const float GroundY = -.06f;
        public static readonly Color FallbackField = new Color32(232, 245, 236, 255);

        Mesh _mesh;
        Material _material;

        public static BattleBackdrop Create(Transform parent, int laneTiles, int layer)
        {
            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null)
            {
                Debug.LogWarning("[battle-backdrop] missing Resources/" + ResourcePath + "; using camera clear colour.");
                return null;
            }
            var shader = RuntimeShaders.Require(RuntimeShaders.Unlit);

            var go = new GameObject("battle-backdrop") { layer = layer };
            go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<BattleBackdrop>();
            backdrop.Build(texture, shader, laneTiles);
            return backdrop;
        }

        void Build(Texture2D texture, Shader shader, int laneTiles)
        {
            var halfLength = (laneTiles + 20f) * .5f;
            const float halfWidth = 11f; // 44 × 22 at the 24-tile lane: the source's 2:1 aspect.
            var x = laneTiles * .5f;
            _mesh = new Mesh
            {
                name = "Battle painted ground",
                vertices = new[]
                {
                    new Vector3(x - halfLength, GroundY, -halfWidth),
                    new Vector3(x + halfLength, GroundY, -halfWidth),
                    new Vector3(x + halfLength, GroundY, halfWidth),
                    new Vector3(x - halfLength, GroundY, halfWidth)
                },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
                normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };
            _mesh.RecalculateBounds();
            _material = new Material(shader) { name = "Battle painted ground" };
            _material.SetTexture("_BaseMap", texture);
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = _material;
        }

        void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
            }
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }
    }
}
