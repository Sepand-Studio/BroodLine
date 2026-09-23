// Offline geometry adapter, deliberately outside Assets. This exercises mesh arithmetic,
// not Unity APIs, skinning, import, shaders or Play mode. Never ship this shim in Unity.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x; this.y=y; }
        public static Vector2 zero => new Vector2(0,0);
        public float sqrMagnitude => x*x+y*y;
        public Vector2 normalized => this/(float)Math.Sqrt(sqrMagnitude);
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
        public static Vector2 operator /(Vector2 a,float b)=>a*(1/b);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero=>new Vector3(0,0,0);
        public static Vector3 one=>new Vector3(1,1,1);
        public static Vector3 right=>new Vector3(1,0,0);
        public static Vector3 up=>new Vector3(0,1,0);
        public static Vector3 forward=>new Vector3(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;
        public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized=>magnitude>0?this*(1/magnitude):zero;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator -(Vector3 a)=>a*-1;
        public static Vector3 operator *(Vector3 a,float b)=>new Vector3(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b)=>a*(1/b);
        public static Vector3 Scale(Vector3 a,Vector3 b)=>new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        public static Vector3 Cross(Vector3 a,Vector3 b)=>new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
    }
    public struct Quaternion
    {
        System.Numerics.Quaternion q;
        public static Quaternion identity=>new Quaternion { q=System.Numerics.Quaternion.Identity };
        public static Quaternion Euler(float x,float y,float z)=>new Quaternion { q=System.Numerics.Quaternion.CreateFromYawPitchRoll(y*Mathf.PI/180,x*Mathf.PI/180,z*Mathf.PI/180) };
        public static Quaternion FromToRotation(Vector3 from,Vector3 to)
        {
            from=from.normalized;to=to.normalized;
            float dot=Vector3.Dot(from,to);
            if(dot<-.99999f) return Euler(180,0,0);
            var cross=Vector3.Cross(from,to);
            return new Quaternion { q=System.Numerics.Quaternion.Normalize(new System.Numerics.Quaternion(cross.x,cross.y,cross.z,1+dot)) };
        }
        public static Vector3 operator *(Quaternion q,Vector3 v)
        { var p=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(v.x,v.y,v.z),q.q);return new Vector3(p.X,p.Y,p.Z); }
    }
    public struct Color
    {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color white=>new Color(1,1,1);
        public static Color black=>new Color(0,0,0);
        public static Color Lerp(Color a,Color b,float t) { t=Math.Max(0,Math.Min(1,t));return new Color(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t); }
        public static Color operator *(Color a,float b)=>new Color(a.r*b,a.g*b,a.b*b,a.a*b);
    }
    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s,out Color c)
        { int n=Convert.ToInt32(s.TrimStart('#'),16);c=new Color(((n>>16)&255)/255f,((n>>8)&255)/255f,(n&255)/255f);return true; }
    }
    public static class Mathf
    {
        public const float PI=(float)Math.PI;
        public static float Sin(float x)=>(float)Math.Sin(x);
        public static float Cos(float x)=>(float)Math.Cos(x);
        public static float Sqrt(float x)=>(float)Math.Sqrt(x);
        public static float Abs(float x)=>Math.Abs(x);
        public static float Max(float a,float b)=>Math.Max(a,b);
    }
    public struct BoneWeight { public int boneIndex0;public float weight0; }
    public struct Matrix4x4 { public static Matrix4x4 Translate(Vector3 v)=>new Matrix4x4(); }
    public class Mesh
    {
        public string name; public Rendering.IndexFormat indexFormat; public BoneWeight[] boneWeights; public Matrix4x4[] bindposes;
        public Vector3[] vertices,normals;public Color[] colors;public Vector2[] uv;public int[] triangles;
        public void SetVertices(List<Vector3> x)=>vertices=x.ToArray();
        public void SetNormals(List<Vector3> x)=>normals=x.ToArray();
        public void SetColors(List<Color> x)=>colors=x.ToArray();
        public void SetUVs(int channel,List<Vector2> x)=>uv=x.ToArray();
        public void SetTriangles(List<int> x,int submesh)=>triangles=x.ToArray();
        public void RecalculateBounds() { }
    }
}
namespace UnityEngine.Rendering { public enum IndexFormat { UInt16,UInt32 } }

class FrontierGeometryExport
{
    static UnityEngine.Mesh Hybrid(UnityEngine.Mesh body)
    {
        var vertices=body.vertices.ToList(); var normals=body.normals.ToList(); var colors=body.colors.ToList();
        var uv=body.uv.ToList(); var indices=body.triangles.ToList(); var weights=body.boneWeights.ToList();
        foreach(string trait in new[]{"cinder","carapace"})
        {
            var b=new Broodline.Frontier.FrontierMesh(); Broodline.Frontier.FrontierParts.Build(b,trait);
            var part=b.Finish(trait,new UnityEngine.Vector3[1]);
            var offset=trait=="cinder"?Broodline.Frontier.FrontierVetch.DorsalPosition:Broodline.Frontier.FrontierVetch.FlankPosition;
            var rotation=trait=="cinder"?UnityEngine.Quaternion.identity:UnityEngine.Quaternion.Euler(-90,0,0);
            int start=vertices.Count;
            vertices.AddRange(part.vertices.Select(p=>rotation*p+offset));normals.AddRange(part.normals.Select(p=>rotation*p));
            colors.AddRange(part.colors);uv.AddRange(part.uv);weights.AddRange(part.boneWeights);indices.AddRange(part.triangles.Select(i=>i+start));
        }
        return new UnityEngine.Mesh { name="Cinderplate assembly",vertices=vertices.ToArray(),normals=normals.ToArray(),colors=colors.ToArray(),uv=uv.ToArray(),triangles=indices.ToArray(),boneWeights=weights.ToArray() };
    }
    static object Export(UnityEngine.Mesh m)
    {
        var v=m.vertices;var n=m.normals;var t=m.triangles;
        for(int i=0;i<v.Length;i++)
            if(float.IsNaN(v[i].sqrMagnitude)||float.IsInfinity(v[i].sqrMagnitude)||Math.Abs(n[i].magnitude-1)>.001f)
                throw new Exception(m.name+": invalid vertex/normal "+i);
        for(int i=0;i<t.Length;i+=3)
        {
            var cross=UnityEngine.Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);
            if(cross.sqrMagnitude<1e-18f||UnityEngine.Vector3.Dot(cross,n[t[i]]+n[t[i+1]]+n[t[i+2]])<=0)
                throw new Exception(m.name+": degenerate/inverted triangle "+i/3);
        }
        if(t.Length/3>10000) throw new Exception(m.name+": exceeds body budget");
        if(m.boneWeights.Any(w=>w.boneIndex0<0||w.boneIndex0>7||w.weight0!=1)) throw new Exception("Invalid bone weights");
        Console.WriteLine(m.name+": "+t.Length/3+" triangles; finite geometry, normals, winding, bone indices passed (offline adapter)");
        return new { name=m.name, positions=v.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(), normals=n.SelectMany(p=>new[]{p.x,p.y,p.z}).ToArray(),
            colors=m.colors.SelectMany(c=>new[]{c.r,c.g,c.b}).ToArray(), polish=m.uv.Select(p=>p.x).ToArray(), indices=t };
    }
    static void Main(string[] args)
    {
        var before=new Broodline.Frontier.FrontierMesh();
        Broodline.Frontier.PreviousVetch.Build(before);
        var after=new Broodline.Frontier.FrontierMesh();
        Broodline.Frontier.FrontierVetch.Build(after);
        var positions=new UnityEngine.Vector3[8];
        var redesigned=after.Finish("Redesigned Vetch",positions);
        var result=new[]{Export(before.Finish("Previous Vetch",positions)),Export(redesigned),Export(Hybrid(redesigned))};
        File.WriteAllText(args[0],new JavaScriptSerializer { MaxJsonLength=10000000 }.Serialize(result));
    }
}
