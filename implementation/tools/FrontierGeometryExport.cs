using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

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
