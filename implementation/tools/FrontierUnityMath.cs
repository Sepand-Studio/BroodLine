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
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new Quaternion { q=a.q*b.q };
        public static Quaternion Euler(Vector3 v)=>Euler(v.x,v.y,v.z);
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
        public static int Max(int a,int b)=>Math.Max(a,b);
        public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Clamp01(float x)=>Math.Max(0,Math.Min(1,x));
        public static float Clamp(float x,float a,float b)=>Math.Max(a,Math.Min(b,x));
        public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float InverseLerp(float a,float b,float v)=>Clamp01((v-a)/(b-a));
        public static float SmoothStep(float a,float b,float t) { t=Clamp01(t);return a+(b-a)*t*t*(3-2*t); }
        public static float Repeat(float v,float length)=>v-(float)Math.Floor(v/length)*length;
    }
    public struct BoneWeight { public int boneIndex0,boneIndex1;public float weight0,weight1; }
    public struct Matrix4x4
    {
        System.Numerics.Matrix4x4 m;
        public static Matrix4x4 identity=>new Matrix4x4 { m=System.Numerics.Matrix4x4.Identity };
        public static Matrix4x4 Translate(Vector3 v)=>new Matrix4x4 { m=System.Numerics.Matrix4x4.CreateTranslation(v.x,v.y,v.z) };
        public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 scale)
        {
            var x=q*Vector3.right;var y=q*Vector3.up;var z=q*Vector3.forward;
            return new Matrix4x4 { m=new System.Numerics.Matrix4x4(x.x*scale.x,x.y*scale.x,x.z*scale.x,0,
                y.x*scale.y,y.y*scale.y,y.z*scale.y,0,z.x*scale.z,z.y*scale.z,z.z*scale.z,0,p.x,p.y,p.z,1) };
        }
        public Matrix4x4 inverse { get { System.Numerics.Matrix4x4.Invert(m,out var value);return new Matrix4x4 { m=value }; } }
        public Matrix4x4 transpose=>new Matrix4x4 { m=System.Numerics.Matrix4x4.Transpose(m) };
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>new Matrix4x4 { m=b.m*a.m };
        public Vector3 MultiplyPoint3x4(Vector3 p) { var v=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(p.x,p.y,p.z),m);return new Vector3(v.X,v.Y,v.Z); }
        public Vector3 MultiplyVector(Vector3 p) { var v=System.Numerics.Vector3.TransformNormal(new System.Numerics.Vector3(p.x,p.y,p.z),m);return new Vector3(v.X,v.Y,v.Z); }
    }
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
