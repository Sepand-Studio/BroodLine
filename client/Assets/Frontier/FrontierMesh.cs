using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broodline.Frontier
{
    /// Small, deterministic mesh authoring vocabulary for the offline art proof.
    /// Vertex colors keep the assembled character on one shared surface material.
    public sealed class FrontierMesh
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Vector3> _normals = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _indices = new List<int>();
        readonly List<BoneWeight> _weights = new List<BoneWeight>();
        public int Bone;

        int Vertex(Vector3 position, Vector3 normal, Color color)
        {
            int index = _vertices.Count;
            _vertices.Add(position);
            _normals.Add(normal.normalized);
            _colors.Add(color);
            _weights.Add(new BoneWeight { boneIndex0 = Bone, weight0 = 1f });
            return index;
        }

        void Triangle(int a, int b, int c)
        {
            _indices.Add(a); _indices.Add(b); _indices.Add(c);
        }

        public void Sphere(Vector3 center, Vector3 radius, Color color, int sides = 16, int rings = 10)
        {
            int start = _vertices.Count;
            for (int y = 0; y <= rings; y++)
            {
                float theta = y * Mathf.PI / rings;
                for (int x = 0; x <= sides; x++)
                {
                    float phi = x * Mathf.PI * 2f / sides;
                    var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    var n = new Vector3(p.x / radius.x, p.y / radius.y, p.z / radius.z);
                    Vertex(center + Vector3.Scale(p, radius), n, color);
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < sides; x++)
                {
                    int a = start + y * (sides + 1) + x, b = a + sides + 1;
                    if (y > 0) Triangle(a, a + 1, b);
                    if (y < rings - 1) Triangle(a + 1, b + 1, b);
                }
        }

        public void Box(Vector3 center, Vector3 size, Color color, float yaw = 0f)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            for (int axis = 0; axis < 3; axis++)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var normal = axes[axis] * sign;
                    var u = axes[(axis + 1) % 3];
                    var v = axes[(axis + 2) % 3] * sign;
                    var face = Vector3.Scale(normal, size) * 0.5f;
                    int start = _vertices.Count;
                    foreach (var uv in new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) })
                        Vertex(center + rotation * (face + Vector3.Scale(u * uv.x + v * uv.y, size) * 0.5f), rotation * normal, color);
                    Triangle(start, start + 1, start + 2);
                    Triangle(start, start + 2, start + 3);
                }
        }

        public void Cone(Vector3 from, Vector3 to, float bottomRadius, float topRadius, Color color, int sides = 10)
        {
            var direction = to - from;
            var rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            int start = _vertices.Count;
            for (int y = 0; y < 2; y++)
                for (int x = 0; x <= sides; x++)
                {
                    float angle = x * Mathf.PI * 2f / sides;
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var normal = (radial + Vector3.up * (bottomRadius - topRadius) / direction.magnitude).normalized;
                    Vertex((y == 0 ? from : to) + rotation * radial * (y == 0 ? bottomRadius : topRadius), rotation * normal, color);
                }
            for (int x = 0; x < sides; x++)
            {
                int a = start + x, b = a + sides + 1;
                Triangle(a, b, a + 1); Triangle(a + 1, b, b + 1);
            }
            for (int cap = 0; cap < 2; cap++)
            {
                var center = cap == 0 ? from : to;
                var n = direction.normalized * (cap == 0 ? -1f : 1f);
                int c = Vertex(center, n, color);
                for (int x = 0; x <= sides; x++)
                {
                    float angle = x * Mathf.PI * 2f / sides;
                    Vertex(center + rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (cap == 0 ? bottomRadius : topRadius), n, color);
                    if (x > 0)
                    {
                        if (cap == 0) Triangle(c, c + x, c + x + 1);
                        else Triangle(c, c + x + 1, c + x);
                    }
                }
            }
        }

        public void Wing(float side, Color color)
        {
            // Two-sided swept membrane, weighted to a single articulated wing bone.
            var points = new[] {
                new Vector3(.32f,.66f,side*.12f), new Vector3(.36f,.79f,side*.48f),
                new Vector3(-.1f,.96f,side*1.03f), new Vector3(-.36f,.75f,side*.72f),
                new Vector3(-.56f,.58f,side*.32f), new Vector3(-.35f,.57f,side*.12f)
            };
            var center = new Vector3(-.05f,.78f,side*.43f);
            for (int i = 0; i < points.Length; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                var normal = Vector3.Cross(a - center, b - center).normalized;
                int c = Vertex(center, normal, Color.Lerp(color, Color.white, .18f));
                int ia = Vertex(a, normal, color), ib = Vertex(b, normal, color);
                Triangle(c, ia, ib);
                int back = Vertex(center, -normal, color * .8f);
                Triangle(back, Vertex(b, -normal, color * .8f), Vertex(a, -normal, color * .8f));
            }
        }

        public Mesh Finish(string name, Vector3[] bonePositions = null)
        {
            var mesh = new Mesh { name = name, indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(_vertices); mesh.SetNormals(_normals); mesh.SetColors(_colors);
            mesh.SetTriangles(_indices, 0);
            if (bonePositions != null)
            {
                mesh.boneWeights = _weights.ToArray();
                var bind = new Matrix4x4[bonePositions.Length];
                for (int i = 0; i < bind.Length; i++) bind[i] = Matrix4x4.Translate(-bonePositions[i]);
                mesh.bindposes = bind;
            }
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
