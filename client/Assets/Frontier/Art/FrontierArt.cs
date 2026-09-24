using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Frontier
{
    /// Authored procedural proof assets; owns all transient meshes and materials.
    /// No imported PNG, model, paid asset, or network call is required by this scene.
    public sealed partial class FrontierArt : IDisposable
    {
        readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        readonly List<Mesh> _environmentMeshes = new List<Mesh>();
        readonly Material _surface;
        bool _disposed;

        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        static readonly Color Cream = Hex("#f4dfb9"), Ink = Hex("#253345"), Gold = Hex("#c99a49");
        static readonly Color Coral = Hex("#e5867a"), Frost = Hex("#c6cede");
        public FrontierArt(Shader shader)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader), "Build the Frontier Proof scene to assign its surface shader.");
            _surface = new Material(shader) { name = "Frontier shared surface" };
        }

        public FrontierCreature Creature(Transform parent, string id, string first = null, string second = null, float phase = 0)
        {
            // Snapshot species/trait ids are presentation input. Keep the
            // case-insensitive contract the previous CreatureAssembler had.
            id = id?.Trim().ToLowerInvariant();
            first = first?.Trim().ToLowerInvariant();
            second = second?.Trim().ToLowerInvariant();
            var rig = FrontierRigDefinition.For(id);
            string key = "body-" + id;
            if (!_meshes.TryGetValue(key, out var mesh))
            {
                // The builder comes from the visual definition - Phase 10 Task
                // 2.1 - so a species is one entry there, not a branch here.
                var author = new FrontierMesh();
                FrontierVisuals.For(id).Build(author);
                mesh = author.FinishRig(key, rig); _meshes.Add(key, mesh);
            }
            var go = new GameObject(id); go.transform.SetParent(parent, false);
            var bones = new Transform[rig.Bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var definition = rig.Bones[i];
                bones[i] = new GameObject(definition.Name).transform;
                bones[i].SetParent(definition.Parent < 0 ? go.transform : bones[definition.Parent], false);
                bones[i].localPosition = rig.LocalPosition(i);
            }
            var body = new GameObject("body"); body.transform.SetParent(go.transform, false);
            var renderer = body.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh; renderer.sharedMaterial = _surface;
            renderer.bones = bones; renderer.rootBone = bones[0];
            // Bounds include limb motion and the proof's growth curve.
            renderer.localBounds = new Bounds(mesh.bounds.center * 1.2f, mesh.bounds.size * 1.8f + rig.MotionAllowance * 2);
            var creature = go.AddComponent<FrontierCreature>();
            creature.Initialize(rig, bones, renderer, phase);
            Mount(creature.Dorsal, first); Mount(creature.Flank, second);
            var portrait = mesh.bounds;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                var partBounds = filter.sharedMesh.bounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            portrait.Encapsulate(go.transform.InverseTransformPoint(filter.transform.TransformPoint(
                                partBounds.center + Vector3.Scale(partBounds.extents, new Vector3(x,y,z)))));
            }
            creature.PortraitBounds = portrait;
            return creature;
        }

        void Mount(Transform socket, string trait)
        {
            if (string.IsNullOrEmpty(trait)) return;
            if (!FrontierParts.Has(trait))
            {
                Debug.LogWarning("[frontier-art] no visual part for trait '" + trait + "'; body remains visible");
                return;
            }
            string key = "part-" + trait;
            if (!_meshes.TryGetValue(key, out var mesh))
            {
                var b = new FrontierMesh();
                FrontierParts.Build(b, trait);
                mesh = b.Finish(key); _meshes.Add(key, mesh);
            }
            var part = Draw(socket, trait, mesh);
            part.transform.localScale = Vector3.one;
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
            FrontierTerrain.AddDetail(b, length, pockets, habitat);
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
