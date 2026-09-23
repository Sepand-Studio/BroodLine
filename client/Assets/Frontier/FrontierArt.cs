using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Authored procedural proof assets; owns all transient meshes and materials.
    /// No imported PNG, model, paid asset, or network call is required by this scene.
    public sealed class FrontierArt : IDisposable
    {
        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly List<Mesh> _environmentMeshes = new List<Mesh>();
        readonly Material _surface;
        bool _disposed;

        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        static readonly Color Cream = Hex("#f4dfb9"), Ink = Hex("#253345"), Gold = Hex("#c99a49");
        static readonly Color Teal = Hex("#6ba7c0"), Coral = Hex("#e5867a"), Frost = Hex("#c6cede");
        static readonly Vector3[] GroundBones = {
            Vector3.zero, new Vector3(.5f,.47f,0), new Vector3(.32f,.24f,.32f),
            new Vector3(.32f,.24f,-.32f), new Vector3(-.34f,.24f,.32f), new Vector3(-.34f,.24f,-.32f)
        };
        static readonly Vector3[] WingBones = {
            Vector3.zero, new Vector3(.32f,.53f,0), new Vector3(0,.64f,.18f), new Vector3(0,.64f,-.18f)
        };
        static readonly Vector3[] EmberBones = {
            Vector3.zero, new Vector3(.16f,1.01f,0), new Vector3(0,.26f,.16f), new Vector3(0,.26f,-.16f)
        };

        public FrontierArt(Shader shader)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader), "Build the Frontier Proof scene to assign its surface shader.");
            _surface = new Material(shader) { name = "Frontier shared surface" };
        }

        public FrontierCreature Creature(Transform parent, string id, string first = null, string second = null, float phase = 0)
        {
            var positions = id == "pale" ? WingBones : id == "ember" ? EmberBones : GroundBones;
            int limbEnd = positions.Length;
            if (id == "vetch" || id == "ember" || id == "pale")
            {
                var withEyes = new Vector3[limbEnd + 2]; Array.Copy(positions, withEyes, limbEnd);
                var center = id == "vetch" ? new Vector3(.66f,.59f,0) : id == "pale" ? new Vector3(.46f,.59f,0) : new Vector3(.28f,1.065f,0);
                float spacing = id == "vetch" ? .245f : id == "pale" ? .14f : .145f;
                withEyes[limbEnd] = center + Vector3.back * spacing;
                withEyes[limbEnd + 1] = center + Vector3.forward * spacing;
                positions = withEyes;
            }
            string key = "body-" + id;
            if (!_meshes.TryGetValue(key, out var mesh))
            {
                var author = new FrontierMesh();
                if (id == "vetch") Vetch(author);
                else if (id == "pale") Pale(author);
                else if (id == "ember") Ember(author);
                else if (id == "courser" || id == "skirmisher" || id == "lash") Raider(author, id);
                else throw new ArgumentException("No proof body for " + id);
                mesh = author.Finish(key, positions); _meshes.Add(key, mesh);
            }
            var go = new GameObject(id); go.transform.SetParent(parent, false);
            var bones = new Transform[positions.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bool eye = i >= limbEnd;
                bones[i] = new GameObject(i == 0 ? "root" : i == 1 ? "head" : eye ? "eye-" + (i - limbEnd) : "limb-" + i).transform;
                bones[i].SetParent(i == 0 ? go.transform : eye ? bones[1] : bones[0], false);
                bones[i].localPosition = eye ? positions[i] - positions[1] : positions[i];
            }
            var body = new GameObject("body"); body.transform.SetParent(go.transform, false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = _surface;
            renderer.bones = bones; renderer.rootBone = bones[0];
            // Bounds include limb motion and the proof's growth curve.
            renderer.localBounds = new Bounds(new Vector3(0,.65f,0), new Vector3(3,2.5f,3));
            var creature = go.AddComponent<FrontierCreature>();
            creature.Initialize(id, bones, renderer, phase, limbEnd);
            Mount(creature.Dorsal, first); Mount(creature.Flank, second);
            return creature;
        }

        void Mount(Transform socket, string trait)
        {
            if (string.IsNullOrEmpty(trait)) return;
            string key = "part-" + trait;
            if (!_meshes.TryGetValue(key, out var mesh))
            {
                var b = new FrontierMesh();
                switch (trait)
                {
                    case "cinder":
                        for (int i = 0; i < 3; i++)
                            b.Cone(new Vector3(-.25f+i*.23f,0,0), new Vector3(-.32f+i*.23f,.22f+i*.09f,0), .105f, .015f, Hex("#f1934d"), 6);
                        break;
                    case "carapace":
                        for (int i = 0; i < 3; i++) b.Sphere(new Vector3((i-1)*.2f,.035f,0), new Vector3(.145f,.07f,.17f), i == 1 ? Cream : Hex("#aac4c6"), 8, 4);
                        break;
                    case "chill":
                        for (int i = 0; i < 3; i++) b.Cone(new Vector3((i-1)*.18f,0,0), new Vector3((i-1)*.23f,.25f-Mathf.Abs(i-1)*.08f,0), .09f,.005f,Frost,5);
                        break;
                    case "taunt":
                        for (int i = -1; i <= 1; i += 2) b.Sphere(new Vector3(i*.13f,.09f,0),new Vector3(.11f,.15f,.065f),Gold,10,6);
                        break;
                    case "splash":
                        for (int i = -1; i <= 1; i += 2) b.Sphere(new Vector3(i*.14f,.07f,0),new Vector3(.12f,.12f,.13f),Coral,10,6);
                        break;
                    default: throw new ArgumentException("No proof part for " + trait);
                }
                mesh = b.Finish(key); _meshes.Add(key, mesh);
            }
            var part = Draw(socket, trait, mesh);
            part.transform.localScale = Vector3.one * .8f;
        }

        static void Eyes(FrontierMesh b, Vector3 center, float spacing, float size, int eyeStart)
        {
            int previousBone = b.Bone;
            for (int side = -1; side <= 1; side += 2)
            {
                b.Bone = eyeStart + (side == -1 ? 0 : 1);
                var eye = center + new Vector3(0,0,spacing*side);
                b.Sphere(eye, new Vector3(size*.8f,size,size*.7f), Cream,12,8);
                b.Sphere(eye+new Vector3(size*.58f,0,side*size*.16f),new Vector3(size*.36f,size*.72f,size*.47f),Gold,12,8);
                b.Sphere(eye+new Vector3(size*.78f,0,side*size*.2f),new Vector3(size*.23f,size*.55f,size*.34f),Ink,12,8);
                b.Sphere(eye+new Vector3(size*.91f,size*.28f,side*size*.23f),Vector3.one*size*.17f,Color.white,8,5);
            }
            b.Bone = previousBone;
        }

        static void Vetch(FrontierMesh b)
        {
            b.Sphere(new Vector3(0,.48f,0),new Vector3(.65f,.35f,.49f),Teal,20,12);
            b.Sphere(new Vector3(.13f,.31f,0),new Vector3(.57f,.18f,.4f),Cream,16,8);
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    float y = .75f - Mathf.Abs(z)*.12f - Mathf.Abs(x)*.035f;
                    b.Sphere(new Vector3(x*.31f,y,z*.29f),new Vector3(.21f,.09f,.19f),Color.Lerp(Teal,Cream,.13f+(x+1)*.07f),8,4);
                }
            b.Bone = 1;
            b.Sphere(new Vector3(.51f,.47f,0),new Vector3(.29f,.25f,.34f),Teal,18,10);
            b.Sphere(new Vector3(.66f,.37f,0),new Vector3(.2f,.12f,.29f),Cream,16,8);
            Eyes(b,new Vector3(.66f,.59f,0),.245f,.094f,6);
            for (int side = -1; side <= 1; side += 2)
                b.Sphere(new Vector3(.822f,.426f,side*.095f),new Vector3(.018f,.012f,.016f),Ink,8,4);
            for (int i = 2; i < GroundBones.Length; i++)
            {
                b.Bone = i; var p = GroundBones[i];
                b.Sphere(p,new Vector3(.19f,.24f,.17f),Teal,12,8);
                b.Sphere(p+new Vector3(.045f,-.13f,0),new Vector3(.205f,.09f,.18f),Color.Lerp(Teal,Ink,.45f),12,6);
                for (int toe=-1;toe<=1;toe++) b.Sphere(p+new Vector3(.20f,-.125f,toe*.065f),new Vector3(.053f,.035f,.034f),Cream,8,4);
            }
        }

        static void Pale(FrontierMesh b)
        {
            b.Sphere(new Vector3(0,.46f,0),new Vector3(.38f,.22f,.22f),Hex("#7f98b6"));
            b.Sphere(new Vector3(.08f,.37f,0),new Vector3(.29f,.12f,.17f),Cream);
            b.Bone=1;
            b.Sphere(new Vector3(.32f,.53f,0),new Vector3(.22f,.17f,.20f),Frost);
            Eyes(b,new Vector3(.46f,.59f,0),.14f,.055f,4);
            for(int side=-1;side<=1;side+=2)
            {
                b.Bone=side==1?2:3; b.Wing(side,Frost);
                b.Cone(new Vector3(.32f,.66f,side*.12f),new Vector3(-.1f,.96f,side*1.03f),.042f,.018f,Hex("#7f98b6"),8);
            }
        }

        static void Ember(FrontierMesh b)
        {
            b.Sphere(new Vector3(0,.66f,0),new Vector3(.22f,.35f,.20f),Coral);
            b.Sphere(new Vector3(.14f,.63f,0),new Vector3(.1f,.25f,.14f),Cream);
            b.Cone(new Vector3(-.13f,.42f,0),new Vector3(-.62f,.32f,0),.14f,.015f,Coral);
            b.Bone=1;
            b.Sphere(new Vector3(.16f,1.01f,0),new Vector3(.23f,.19f,.19f),Coral);
            b.Sphere(new Vector3(.31f,.945f,0),new Vector3(.13f,.07f,.14f),Cream);
            Eyes(b,new Vector3(.28f,1.065f,0),.145f,.065f,4);
            for(int i=0;i<3;i++) b.Cone(new Vector3(.14f-i*.11f,1.16f-i*.025f,0),new Vector3(.03f-i*.16f,1.45f-i*.1f,0),.075f,.006f,Hex("#eda745"),7);
            for(int i=2;i<4;i++)
            {
                b.Bone=i;var p=EmberBones[i];
                b.Sphere(p,new Vector3(.12f,.26f,.10f),Coral);
                b.Sphere(p+new Vector3(.08f,-.18f,0),new Vector3(.19f,.08f,.12f),Ink);
            }
        }

        static void Raider(FrontierMesh b,string id)
        {
            var body=Hex("#414556");
            b.Sphere(new Vector3(0,.45f,0),new Vector3(.54f,.25f,.3f),body,10,6);
            b.Bone=1;
            b.Sphere(new Vector3(.52f,.39f,0),new Vector3(.25f,.19f,.24f),Ink,8,5);
            b.Cone(new Vector3(.60f,.48f,0),new Vector3(.94f,.41f,0),.12f,.008f,Hex("#83929d"),5);
            for(int side=-1;side<=1;side+=2) b.Sphere(new Vector3(.69f,.46f,side*.16f),Vector3.one*.035f,Hex("#e57b57"),8,4);
            for(int i=2;i<6;i++)
            {
                b.Bone=i;var p=GroundBones[i];
                b.Cone(p+Vector3.up*.2f,new Vector3(p.x+.08f,.02f,p.z*1.2f),.10f,.025f,body,6);
            }
            b.Bone=0;
            for(int i=0;i<3;i++) b.Cone(new Vector3(-.28f+i*.22f,.63f,0),new Vector3(-.42f+i*.22f,.9f,0),.11f,.006f,Hex("#647185"),5);
            if(id=="lash")
                for(int side=-1;side<=1;side+=2) b.Cone(new Vector3(.15f,.60f,side*.19f),new Vector3(.85f,.65f,side*.52f),.07f,.016f,body,6);
        }

        GameObject Draw(Transform parent,string name,Mesh mesh)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=_surface;
            return go;
        }

        public GameObject Environment(Transform parent,int length,int[] pockets,bool habitat)
        {
            var b=new FrontierMesh();
            var grass=Hex("#6b9361");var sand=Hex("#d2bd94");var rock=Hex("#8a9485");
            if(habitat)
            {
                b.Cone(new Vector3(0,-.36f,0),new Vector3(0,-.015f,0),1.5f,1.4f,rock,12);
                b.Cone(new Vector3(0,-.025f,0),Vector3.zero,1.38f,1.34f,grass,16);
            }
            else
            {
                b.Box(new Vector3(length*.5f,-.25f,0),new Vector3(length+8,.45f,12),grass);
                b.Box(new Vector3(length*.5f,-.018f,0),new Vector3(length+4,.06f,1.8f),sand);
                // Broken stone edging and grass tufts soften the straight route without changing it.
                for (int i = -1; i <= length + 1; i++)
                {
                    int side = i % 2 == 0 ? -1 : 1;
                    var edge = new Vector3(i + .2f, .035f, side * 1.04f);
                    b.Sphere(edge, new Vector3(.22f,.075f,.13f), i % 3 == 0 ? rock : Hex("#b3b29a"),8,4);
                    if (i % 3 == 0) Foliage(b, edge + new Vector3(.28f,0,side*.2f), side);
                }
                foreach(int pocket in pockets)
                    b.Cone(new Vector3(pocket,-.04f,1.5f),new Vector3(pocket,.045f,1.5f),.82f,.75f,Hex("#c7c4aa"),10);
            }
            var rng=new System.Random(20260923);
            int count=habitat?9:34;
            for(int i=0;i<count;i++)
            {
                float x=habitat?(float)rng.NextDouble()*5-2.5f:(float)rng.NextDouble()*(length+4)-2;
                float z=habitat?1.8f+(float)rng.NextDouble()*2:(i%2==0?-1:1)*(3.2f+(float)rng.NextDouble()*1.6f);
                float size=.5f+(float)rng.NextDouble()*.6f;
                var p=new Vector3(x,0,z);
                if(i%3==0)
                {
                    b.Box(p+Vector3.up*size*.35f,new Vector3(size*1.4f,size*.8f,size),rock,i*37f);
                    b.Sphere(p+new Vector3(0,size*.77f,0),new Vector3(size*.7f,.10f,size*.5f),grass,10,5);
                }
                else
                {
                    b.Cone(p,p+Vector3.up*size*1.4f,.09f,.04f,Hex("#806247"),7);
                    var leaf = i%2==0?Hex("#3e735b"):Hex("#87a65c");
                    b.Sphere(p+Vector3.up*size*1.55f,new Vector3(size*.62f,size*.65f,size*.68f),leaf,10,7);
                    b.Sphere(p+new Vector3(-size*.35f,size*1.28f,size*.1f),new Vector3(size*.48f,size*.42f,size*.47f),Color.Lerp(leaf,grass,.35f),10,6);
                    b.Sphere(p+new Vector3(size*.3f,size*1.72f,-size*.17f),new Vector3(size*.4f,size*.48f,size*.45f),Color.Lerp(leaf,Cream,.12f),10,6);
                }
                Foliage(b, p + new Vector3(size*.8f,0,-size*.3f), i);
            }
            if(habitat)
                for(int i=0;i<7;i++)
                {
                    float angle = i * Mathf.PI * 2 / 7;
                    Foliage(b,new Vector3(Mathf.Cos(angle)*1.17f,0,Mathf.Sin(angle)*1.17f),i);
                }
            if(!habitat) Ark(b,new Vector3(length+1,0,0));
            var mesh=b.Finish(habitat?"frontier-habitat":"frontier-defile");_environmentMeshes.Add(mesh);
            return Draw(parent,mesh.name,mesh);
        }

        static void Foliage(FrontierMesh b, Vector3 p, int variation)
        {
            var green = Hex("#467b57");
            for(int blade=-1;blade<=1;blade++)
                b.Cone(p + new Vector3(blade*.05f,0,0),p + new Vector3(blade*.16f,.22f-Mathf.Abs(blade)*.04f,blade*.04f),.035f,.004f,green,5);
            if(variation%3!=0)return;
            var flower=p+new Vector3(.13f,.18f,.09f);
            b.Cone(p+new Vector3(.13f,0,.09f),flower,.015f,.01f,green,5);
            for(int petal=0;petal<5;petal++)
            {
                float angle=petal*Mathf.PI*2/5;
                b.Sphere(flower+new Vector3(Mathf.Cos(angle)*.055f,0,Mathf.Sin(angle)*.055f),new Vector3(.043f,.025f,.043f),Cream,6,4);
            }
            b.Sphere(flower+Vector3.up*.013f,Vector3.one*.025f,Gold,6,4);
        }

        static void Ark(FrontierMesh b,Vector3 p)
        {
            b.Cone(p,p+Vector3.up*.3f,1.15f,1.1f,Ink,8);
            b.Cone(p+Vector3.up*.3f,p+Vector3.up*.42f,1.13f,1.13f,Gold,8);
            for(int side=-1;side<=1;side+=2)
                for(int i=-1;i<=1;i++) b.Sphere(p+new Vector3(i*.62f,.22f,side*.92f),new Vector3(.32f,.26f,.16f),Hex("#806247"),10,7);
            b.Box(p+new Vector3(-.25f,.88f,0),new Vector3(.9f,.9f,1.15f),Cream);
            b.Box(p+new Vector3(-.25f,1.37f,0),new Vector3(1.12f,.13f,1.35f),Hex("#806247"));
            b.Sphere(p+new Vector3(.42f,.9f,0),new Vector3(.52f,.55f,.52f),Hex("#8668cf"),16,10);
            b.Cone(p+new Vector3(.42f,.43f,0),p+new Vector3(.42f,.49f,0),.61f,.61f,Gold,12);
            for(int side=-1;side<=1;side+=2)
            {
                b.Cone(p+new Vector3(.42f,.48f,side*.5f),p+new Vector3(.42f,1.52f,side*.1f),.045f,.035f,Gold,7);
                b.Box(p+new Vector3(-.05f,.94f,side*.587f),new Vector3(.34f,.55f,.035f),Hex("#6b4ec2"));
            }
            b.Sphere(p+new Vector3(.42f,1.53f,0),Vector3.one*.10f,Gold,12,8);
        }

        public void Dispose()
        {
            if(_disposed)return;_disposed=true;
            foreach(var mesh in _meshes.Values) Release(mesh);
            foreach(var mesh in _environmentMeshes) Release(mesh);
            Release(_surface);_meshes.Clear();_environmentMeshes.Clear();
        }
        public static void Release(UnityEngine.Object value)
        {
            if(value==null)return;
            if(Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
