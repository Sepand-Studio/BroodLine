using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using UnityEngine;
using Broodline.Frontier;

class FrontierCompanionExport
{
    static readonly string[] Traits={"cinder","carapace","chill","taunt","splash"};
    static void Build(FrontierMesh b,string id)
    {
        switch(id)
        {
            case "vetch":FrontierVetch.Build(b);break;case "ember":FrontierEmber.Build(b);break;
            case "pale":FrontierPale.Build(b);break;case "skitter":FrontierSkitter.Build(b);break;
            case "hollow":FrontierHollow.Build(b);break;case "loam":FrontierLoam.Build(b);break;
        }
    }
    static void Check(Mesh m,int bones=0)
    {
        if(m.vertices.Length!=m.normals.Length||m.uv.Length!=m.vertices.Length||m.colors.Length!=m.vertices.Length)throw new Exception("Missing mesh channel "+m.name);
        for(int i=0;i<m.vertices.Length;i++)
        {
            if(!Finite(m.vertices[i])||Math.Abs(m.normals[i].magnitude-1)>.001f||m.uv[i].x<0||m.uv[i].x>1)throw new Exception("Invalid vertex "+m.name+" "+i);
            if(bones>0)
            {
                var w=m.boneWeights[i];
                if(w.boneIndex0<0||w.boneIndex0>=bones||w.boneIndex1<0||w.boneIndex1>=bones||Math.Abs(w.weight0+w.weight1-1)>.0001f)
                    throw new Exception("Invalid skin weights "+m.name);
            }
        }
        for(int i=0;i<m.triangles.Length;i+=3)
        {
            int a=m.triangles[i],b=m.triangles[i+1],c=m.triangles[i+2];
            var cross=Vector3.Cross(m.vertices[b]-m.vertices[a],m.vertices[c]-m.vertices[a]);
            if(cross.sqrMagnitude<1e-18f||Vector3.Dot(cross,m.normals[a]+m.normals[b]+m.normals[c])<=0)throw new Exception("Inverted/degenerate triangle "+m.name+" "+i/3+" at "+string.Join(",",V(m.vertices[a]))+" / "+string.Join(",",V(m.vertices[b]))+" / "+string.Join(",",V(m.vertices[c])));
        }
    }
    static bool Finite(Vector3 p)=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
    static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
    static object Data(Mesh m)=>new { positions=m.vertices.SelectMany(V).ToArray(),normals=m.normals.SelectMany(V).ToArray(),
        colors=m.colors.SelectMany(c=>new[]{c.r,c.g,c.b}).ToArray(),polish=m.uv.Select(p=>p.x).ToArray(),indices=m.triangles };
    static Matrix4x4[] World(FrontierRigDefinition rig,FrontierBonePose[] pose)
    {
        var world=new Matrix4x4[pose.Length];
        for(int i=0;i<pose.Length;i++)
        {
            var p=pose[i];var local=Matrix4x4.TRS(p.Position,p.Rotation,p.Scale);
            world[i]=rig.Bones[i].Parent<0?local:world[rig.Bones[i].Parent]*local;
        }
        return world;
    }
    static Mesh Skin(Mesh source,Matrix4x4[] world)
    {
        var skin=world.Select((m,i)=>m*source.bindposes[i]).ToArray();var normal=skin.Select(m=>m.inverse.transpose).ToArray();
        var vertices=new Vector3[source.vertices.Length];var normals=new Vector3[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var w=source.boneWeights[i];
            vertices[i]=skin[w.boneIndex0].MultiplyPoint3x4(source.vertices[i])*w.weight0+skin[w.boneIndex1].MultiplyPoint3x4(source.vertices[i])*w.weight1;
            normals[i]=(normal[w.boneIndex0].MultiplyVector(source.normals[i])*w.weight0+normal[w.boneIndex1].MultiplyVector(source.normals[i])*w.weight1).normalized;
        }
        return new Mesh { name=source.name,vertices=vertices,normals=normals,colors=source.colors,uv=source.uv,triangles=source.triangles };
    }
    static Matrix4x4 SocketMatrix(FrontierRigDefinition rig,FrontierSocketDefinition socket,Matrix4x4[] world)
        =>world[socket.Bone]*Matrix4x4.TRS(socket.Position-rig.Bones[socket.Bone].Position,Quaternion.Euler(socket.Euler),Vector3.one*socket.Scale);
    static object SocketData(FrontierRigDefinition rig,FrontierSocketDefinition socket,Matrix4x4[] world)
    {
        var m=SocketMatrix(rig,socket,world);
        return new { origin=V(m.MultiplyPoint3x4(Vector3.zero)),x=V(m.MultiplyVector(Vector3.right)),y=V(m.MultiplyVector(Vector3.up)),z=V(m.MultiplyVector(Vector3.forward)) };
    }
    static void Enclose(IEnumerable<Vector3> vertices,ref Vector3 min,ref Vector3 max)
    {
        foreach(var v in vertices)
        {
            min=new Vector3(Math.Min(min.x,v.x),Math.Min(min.y,v.y),Math.Min(min.z,v.z));
            max=new Vector3(Math.Max(max.x,v.x),Math.Max(max.y,v.y),Math.Max(max.z,v.z));
        }
    }
    static void FrameCheck(IEnumerable<Vector3> vertices,Vector3 min,Vector3 max,Vector3 allowance,string context)
    {
        // The camera encloses this box then applies a further 16% projection margin.
        var center=(min+max)*.5f*1.34f;var extent=((max-min)*.5f+allowance)*1.34f;
        foreach(var p in vertices)
            if(!Finite(p)||Math.Abs(p.x-center.x)>extent.x*1.16f||Math.Abs(p.y-center.y)>extent.y*1.16f||Math.Abs(p.z-center.z)>extent.z*1.16f)
                throw new Exception("Motion framing allowance exceeded: "+context);
    }
    static void Main(string[] args)
    {
        var parts=new Dictionary<string,Mesh>();
        foreach(var name in Traits){var b=new FrontierMesh();FrontierParts.Build(b,name);parts[name]=b.Finish(name);Check(parts[name]);}
        var rows=new List<object>();int combinations=0,poses=0;
        foreach(var id in FrontierRigDefinition.Companions)
        {
            var rig=FrontierRigDefinition.For(id);var author=new FrontierMesh();Build(author,id);var mesh=author.FinishRig(id,rig);Check(mesh,rig.Bones.Length);
            var pose=new FrontierBonePose[rig.Bones.Length];FrontierPose.Sample(rig,new FrontierMotionState(),0,0,pose,true);var rest=World(rig,pose);
            var baked=Skin(mesh,rest);
            for(int i=0;i<mesh.vertices.Length;i++)if((baked.vertices[i]-mesh.vertices[i]).magnitude>.0001f)throw new Exception("Bind pose drift "+id);
            Mesh previous=null;
            if(id=="vetch")
            {
                var old=new FrontierMesh();BaselineVetch.Build(old);previous=old.Finish("baseline-vetch");
                if(!previous.vertices.SequenceEqual(mesh.vertices)||!previous.triangles.SequenceEqual(mesh.triangles)||!previous.colors.SequenceEqual(mesh.colors))throw new Exception("Accepted Vetch geometry changed");
            }
            else if(id=="ember"||id=="pale"){var old=new FrontierMesh();PreviousCompanions.Build(old,id);previous=old.Finish("baseline-"+id);}
            if(previous!=null)Check(previous);
            int worst=0;
            var fit=new List<Tuple<string,string,Vector3,Vector3>>();
            foreach(var first in new[]{""}.Concat(Traits))foreach(var second in new[]{""}.Concat(Traits))
            {
                int count=mesh.triangles.Length/3+(first==""?0:parts[first].triangles.Length/3)+(second==""?0:parts[second].triangles.Length/3);
                if(count>10000)throw new Exception("Assembled budget exceeded "+id+" "+first+"/"+second+": "+count);
                worst=Math.Max(worst,count);combinations++;
                Vector3 min=Vector3.one*100,max=Vector3.one*-100;Enclose(mesh.vertices,ref min,ref max);
                if(first!="")Enclose(parts[first].vertices.Select(p=>SocketMatrix(rig,rig.Dorsal,rest).MultiplyPoint3x4(p)),ref min,ref max);
                if(second!="")Enclose(parts[second].vertices.Select(p=>SocketMatrix(rig,rig.Flank,rest).MultiplyPoint3x4(p)),ref min,ref max);
                fit.Add(Tuple.Create(first,second,min,max));
            }
            foreach(float growth in new[]{0f,1f})foreach(string action in new[]{"idle","walk","attack","hit","exhausted","greet","celebrate"})foreach(float time in new[]{0f,.08f,.22f,.6f,1.2f,2.7f})
            {
                var state=new FrontierMotionState { Growth=growth,Moving=action=="walk",Hurt=action=="exhausted"?1:0,
                    AttackUntil=action=="attack"?.28f:0,HitUntil=action=="hit"?.16f:0,GreetingUntil=action=="greet"?1.1f:0,CelebrationUntil=action=="celebrate"?1.6f:0 };
                FrontierPose.Sample(rig,state,time,0,pose);var world=World(rig,pose);var posed=Skin(mesh,world);poses++;
                foreach(var pair in fit)
                {
                    string context=id+"/"+action+"/"+growth+"/"+pair.Item1+"/"+pair.Item2;
                    FrameCheck(posed.vertices,pair.Item3,pair.Item4,rig.MotionAllowance,context);
                    if(pair.Item1!="")FrameCheck(parts[pair.Item1].vertices.Select(p=>SocketMatrix(rig,rig.Dorsal,world).MultiplyPoint3x4(p)),pair.Item3,pair.Item4,rig.MotionAllowance,context);
                    if(pair.Item2!="")FrameCheck(parts[pair.Item2].vertices.Select(p=>SocketMatrix(rig,rig.Flank,world).MultiplyPoint3x4(p)),pair.Item3,pair.Item4,rig.MotionAllowance,context);
                }
            }
            FrontierPose.Sample(rig,new FrontierMotionState { Growth=1 },0,0,pose,true);var grown=World(rig,pose);
            rows.Add(new { id,baseline=previous==null?null:Data(previous),baselineLabel=previous==null?"Production baseline not yet exported":"Frozen source baseline · cef49fa",
                current=Data(mesh),grown=Data(Skin(mesh,grown)),dorsal=SocketData(rig,rig.Dorsal,rest),flank=SocketData(rig,rig.Flank,rest),
                grownDorsal=SocketData(rig,rig.Dorsal,grown),grownFlank=SocketData(rig,rig.Flank,grown),triangles=mesh.triangles.Length/3,worstAssembled=worst });
            Console.WriteLine(id+": "+mesh.triangles.Length/3+" body / "+worst+" worst assembled; hierarchy, geometry and sampled framing passed");
        }
        Console.WriteLine(combinations+" assemblies; "+poses+" sampled poses; "+poses*36+" pose/attachment framing combinations. Offline math adapter, not Unity validation.");
        File.WriteAllText(args[0],new JavaScriptSerializer { MaxJsonLength=50000000 }.Serialize(new { species=rows,parts=parts.ToDictionary(p=>p.Key,p=>Data(p.Value)) }));
    }
}
