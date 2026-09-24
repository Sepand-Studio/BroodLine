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
        readonly List<Vector2> _surface = new List<Vector2>();
        public int Bone;
        public int SecondBone = -1;
        public float BoneBlend;
        // Per-vertex polish: skin, shell and eyes share a draw call, but not a finish.
        public float Polish = .18f;

        int Vertex(Vector3 position, Vector3 normal, Color color)
        {
            int index = _vertices.Count;
            _vertices.Add(position);
            _normals.Add(normal.normalized);
            _colors.Add(color);
            float blend = SecondBone < 0 ? 0 : Mathf.Clamp01(BoneBlend);
            _weights.Add(new BoneWeight { boneIndex0 = Bone, weight0 = 1 - blend, boneIndex1 = SecondBone < 0 ? 0 : SecondBone, weight1 = blend });
            _surface.Add(new Vector2(Polish, 0));
            return index;
        }

        void Triangle(int a, int b, int c)
        {
            _indices.Add(a); _indices.Add(b); _indices.Add(c);
        }

        void SmoothNormals(int startVertex, int startIndex)
        {
            for (int i = startVertex; i < _vertices.Count; i++) _normals[i] = Vector3.zero;
            for (int i = startIndex; i < _indices.Count; i += 3)
            {
                int a = _indices[i], b = _indices[i+1], c = _indices[i+2];
                var n = Vector3.Cross(_vertices[b]-_vertices[a],_vertices[c]-_vertices[a]);
                _normals[a] += n; _normals[b] += n; _normals[c] += n;
            }
            for (int i = startVertex; i < _vertices.Count; i++) _normals[i] = _normals[i].normalized;
        }

        /// Rounded, swept volume for crests, necks and tails; no open tube ends.
        public void Sweep(Vector3[] points, Vector2[] radii, Color color, int sides = 12)
        {
            int start = _vertices.Count, firstIndex = _indices.Count;
            var previousTangent=(points[1]-points[0]).normalized;
            var rotation=Quaternion.FromToRotation(Vector3.up,previousTangent);
            for (int row = 0; row < points.Length; row++)
            {
                var tangent = (points[Mathf.Min(row+1,points.Length-1)] - points[Mathf.Max(row-1,0)]).normalized;
                rotation=Quaternion.FromToRotation(previousTangent,tangent)*rotation;
                previousTangent=tangent;
                for (int j = 0; j < sides; j++)
                {
                    float angle = j*Mathf.PI*2/sides;
                    Vertex(points[row] + rotation * new Vector3(Mathf.Cos(angle)*radii[row].x,0,Mathf.Sin(angle)*radii[row].y), Vector3.up, color);
                }
            }
            for (int row = 0; row < points.Length-1; row++)
                for (int j = 0; j < sides; j++)
                {
                    int a=start+row*sides+j, b=start+row*sides+(j+1)%sides;
                    Triangle(a,a+sides,b); Triangle(b,a+sides,b+sides);
                }
            for (int cap = 0; cap < 2; cap++)
            {
                int row=cap==0?0:points.Length-1, c=Vertex(points[row],Vector3.up,color), offset=start+row*sides;
                for (int j=0;j<sides;j++)
                    if(cap==0)Triangle(c,offset+j,offset+(j+1)%sides);
                    else Triangle(c,offset+(j+1)%sides,offset+j);
            }
            SmoothNormals(start,firstIndex);
        }

        /// A closed curved membrane, with smoothly blended shoulder/tip weights.
        /// `rise` is how far the wing arcs upward from shoulder to tip (0.24 was
        /// the proof's original dome; a glider wants less). `MembraneY` is the
        /// same curve, exposed so a species can put spars exactly on the surface.
        public static float MembraneY(float t, float u, float rise) => .66f+rise*Mathf.Sin(t*Mathf.PI*.65f)+.085f*Mathf.Sin(u*Mathf.PI)*(1-t);
        public void Membrane(float side, int shoulder, int tip, Color color, float rise = .24f,
            float heightOffset = 0f, float span = 1.18f)
        {
            const int spans=10, chords=8;
            int start=_vertices.Count, firstIndex=_indices.Count, previousBone=Bone;
            int previousSecond=SecondBone; float previousBlend=BoneBlend;
            for(int face=0;face<2;face++)
                for(int s=0;s<=spans;s++)
                    for(int c=0;c<=chords;c++)
                    {
                        float t=s/(float)spans, u=c/(float)chords;
                        float leading=.34f-.43f*t*t, trailing=-.40f-.34f*Mathf.Sin(t*Mathf.PI)+.28f*t;
                        float x=Mathf.Lerp(leading,trailing,u), z=side*(.15f+span*t);
                        float y=MembraneY(t,u,rise)+heightOffset;
                        y+=(face==0?1:-1)*(.014f+.022f*(1-t));
                        Bone=shoulder;SecondBone=tip;BoneBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,t));
                        Vertex(new Vector3(x,y,z),Vector3.up,Color.Lerp(color,Color.white,.12f*Mathf.Sin(u*Mathf.PI)));
                    }
            int layer=(spans+1)*(chords+1);
            for(int face=0;face<2;face++)
                for(int s=0;s<spans;s++)
                    for(int c=0;c<chords;c++)
                    {
                        int a=start+face*layer+s*(chords+1)+c,b=a+chords+1;
                        bool reverse=(side>0)^(face==1);
                        if(reverse){Triangle(a,a+1,b);Triangle(a+1,b+1,b);}
                        else {Triangle(a,b,a+1);Triangle(a+1,b,b+1);}
                    }
            // Follow top perimeter winding; each side joins the top edge to its lower copy.
            var perimeter=new List<int>();
            for(int c=0;c<=chords;c++)perimeter.Add(start+c);
            for(int s=1;s<=spans;s++)perimeter.Add(start+s*(chords+1)+chords);
            for(int c=chords-1;c>=0;c--)perimeter.Add(start+spans*(chords+1)+c);
            for(int s=spans-1;s>0;s--)perimeter.Add(start+s*(chords+1));
            for(int i=0;i<perimeter.Count;i++)
            {
                int a=perimeter[i],b=perimeter[(i+1)%perimeter.Count];
                if(side<0){Triangle(a,b,a+layer);Triangle(b,b+layer,a+layer);}
                else {Triangle(a,a+layer,b);Triangle(b,a+layer,b+layer);}
            }
            SmoothNormals(start,firstIndex);
            Bone=previousBone;SecondBone=previousSecond;BoneBlend=previousBlend;
        }

        public Mesh FinishRig(string name, FrontierRigDefinition rig)
        {
            var positions=new Vector3[rig.Bones.Length];
            for(int i=0;i<positions.Length;i++)positions[i]=rig.Bones[i].Position;
            var mesh=Finish(name,positions);mesh.bindposes=rig.BindPoses();return mesh;
        }

        public void Sphere(Vector3 center, Vector3 radius, Color color, int sides = 16, int rings = 10, Quaternion? orientation = null, bool upperOnly = false)
        {
            int start = _vertices.Count;
            var rotation = orientation ?? Quaternion.identity;
            for (int y = 0; y <= rings; y++)
            {
                float theta = y * Mathf.PI / rings * (upperOnly ? .5f : 1f);
                for (int x = 0; x <= sides; x++)
                {
                    float phi = x * Mathf.PI * 2f / sides;
                    var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    var n = new Vector3(p.x / radius.x, p.y / radius.y, p.z / radius.z);
                    Vertex(center + rotation * Vector3.Scale(p, radius), rotation * n, color);
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < sides; x++)
                {
                    int a = start + y * (sides + 1) + x, b = a + sides + 1;
                    if (y > 0) Triangle(a, a + 1, b);
                    if (upperOnly || y < rings - 1) Triangle(a + 1, b + 1, b);
                }
        }

        /// A beveled polygon following the upper surface of an ellipsoid.
        /// Polygon coordinates are normalized X/Z in clockwise order viewed from above.
        /// Concentric curved rings preserve a continuous dome instead of stacked spheres.
        public void ShellPlate(Vector3 center, Vector3 radius, Vector2[] outline, Color color, int edgeSteps = 1)
        {
            if (edgeSteps > 1)
            {
                var curved = new Vector2[outline.Length * edgeSteps];
                for (int i = 0; i < outline.Length; i++)
                    for (int j = 0; j < edgeSteps; j++)
                        curved[i * edgeSteps + j] = outline[i] + (outline[(i + 1) % outline.Length] - outline[i]) * (j / (float)edgeSteps);
                outline = curved;
            }
            var centroid = Vector2.zero;
            foreach (var p in outline) centroid += p;
            centroid /= outline.Length;
            int start = _vertices.Count;
            var scales = new[] { 1f, .93f, .72f, .38f };
            var lifts = new[] { .003f, .028f, .046f, .052f };
            for (int ring = 0; ring < scales.Length; ring++)
                foreach (var corner in outline)
                {
                    var p = centroid + (corner - centroid) * scales[ring];
                    float h = Mathf.Sqrt(Mathf.Max(.001f, 1 - p.sqrMagnitude));
                    var normal = new Vector3(p.x / radius.x, h / radius.y, p.y / radius.z).normalized;
                    var position = center + new Vector3(p.x * radius.x, h * radius.y, p.y * radius.z) + normal * lifts[ring];
                    Vertex(position, normal, Color.Lerp(color, Color.black, ring == 0 ? .27f : ring == 1 ? .07f : 0));
                }
            int count = outline.Length;
            for (int ring = 0; ring < scales.Length - 1; ring++)
                for (int i = 0; i < count; i++)
                {
                    int a = start + ring * count + i, b = start + ring * count + (i + 1) % count;
                    Triangle(a, b, a + count); Triangle(b, b + count, a + count);
                }
            float height = Mathf.Sqrt(Mathf.Max(.001f, 1 - centroid.sqrMagnitude));
            var n = new Vector3(centroid.x / radius.x, height / radius.y, centroid.y / radius.z).normalized;
            int top = Vertex(center + new Vector3(centroid.x * radius.x, height * radius.y, centroid.y * radius.z) + n * .054f, n, color);
            int last = start + (scales.Length - 1) * count;
            for (int i = 0; i < count; i++) Triangle(last + i, last + (i + 1) % count, top);
            // Average actual bevel normals, including the curved transition at the lip.
            for (int i = start; i < _vertices.Count; i++) _normals[i] = Vector3.zero;
            int firstIndex = _indices.Count - count * (3 * (scales.Length - 1) * 2 + 3);
            for (int i = firstIndex; i < _indices.Count; i += 3)
            {
                int a = _indices[i], b = _indices[i + 1], c = _indices[i + 2];
                var face = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
                _normals[a] += face; _normals[b] += face; _normals[c] += face;
            }
            for (int i = start; i < _vertices.Count; i++) _normals[i] = _normals[i].normalized;
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
            mesh.SetUVs(0, _surface);
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
