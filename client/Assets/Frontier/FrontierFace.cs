using UnityEngine;

namespace Broodline.Frontier
{
    /// Shared eye construction, with each species authoring its own proportions and placement.
    public static class FrontierFace
    {
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value,out var c);return c; }
        public static void Eyes(FrontierMesh b, FrontierRigDefinition rig, float size, Color skin, Color iris)
        {
            int head=rig.Head; b.Polish=.12f;
            for(int i=0;i<rig.Bones.Length;i++)
            {
                var bone=rig.Bones[i];if(bone.Role!=FrontierBoneRole.Eye)continue;
                var eye=bone.Position;var rotation=Quaternion.Euler(0,-bone.Side*28,0);var forward=rotation*Vector3.right;
                b.Bone=head;
                b.Sphere(eye-forward*size*.30f,new Vector3(size*.64f,size*1.14f,size*.94f),Hex("#f4dfb9"),12,8,rotation);
                b.Sphere(eye+new Vector3(-size*.40f,size*.85f,0),new Vector3(size*.8f,size*.45f,size),skin,12,7,rotation);
                b.Bone=i;b.Polish=.85f;
                b.Sphere(eye,new Vector3(size*.46f,size,size*.78f),Hex("#fff2d5"),14,8,rotation);
                b.Sphere(eye+forward*size*.34f,new Vector3(size*.28f,size*.87f,size*.66f),iris,14,8,rotation);
                b.Sphere(eye+forward*size*.55f,new Vector3(size*.17f,size*.69f,size*.43f),Hex("#182737"),12,8,rotation);
                b.Sphere(eye+forward*size*.68f+new Vector3(0,size*.38f,-size*.17f),new Vector3(size*.08f,size*.18f,size*.14f),Color.white,8,5,rotation);
            }
            b.Bone=head;b.Polish=.12f;
        }

        public static void Smile(FrontierMesh b,Vector3 center,Vector3 radius,float halfWidth)
        {
            Vector3 last=Vector3.zero;
            for(int i=0;i<=10;i++)
            {
                float t=i/5f-1,y=-radius.y*.22f+t*t*radius.y*.35f,z=t*halfWidth;
                var p=center+new Vector3(radius.x*Mathf.Sqrt(Mathf.Max(0,1-y*y/(radius.y*radius.y)-z*z/(radius.z*radius.z)))+.003f,y,z);
                if(i>0)b.Cone(last,p,.006f,.006f,Hex("#79634f"),6);
                last=p;
            }
        }

        public static FrontierBoneDefinition Bone(string name,int parent,float x,float y,float z,FrontierBoneRole role,float side=0,float phase=0)
            =>new FrontierBoneDefinition(name,parent,new Vector3(x,y,z),role,side,phase);
        public static FrontierSocketDefinition Socket(int bone,float x,float y,float z,float scale=1,bool flank=false)
            =>new FrontierSocketDefinition(bone,new Vector3(x,y,z),flank?new Vector3(-90,0,0):Vector3.zero,scale);
    }
}
