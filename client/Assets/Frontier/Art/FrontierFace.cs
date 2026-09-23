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

        /// Ember's forward-focused eyes: a narrow almond and a sloping upper
        /// lid, instead of the round socket shared by the smaller companions.
        public static void AlertEyes(FrontierMesh b,FrontierRigDefinition rig,Color lid,Color iris)
        {
            for(int i=0;i<rig.Bones.Length;i++)
            {
                var bone=rig.Bones[i];if(bone.Role!=FrontierBoneRole.Eye)continue;
                var p=bone.Position;
                var rotation=Quaternion.Euler(bone.Side*8f,-bone.Side*28f,0);
                var forward=rotation*Vector3.right;
                b.Bone=rig.Head;b.Polish=.14f;
                b.Sphere(p-forward*.017f,new Vector3(.049f,.069f,.079f),lid,12,8,rotation);
                b.Bone=i;b.Polish=.82f;
                b.Sphere(p,new Vector3(.031f,.055f,.062f),Hex("#fff0d2"),14,8,rotation);
                b.Sphere(p+forward*.026f,new Vector3(.020f,.052f,.047f),iris,12,8,rotation);
                b.Sphere(p+forward*.039f,new Vector3(.013f,.042f,.034f),Hex("#182737"),12,8,rotation);
                b.Sphere(p+forward*.047f+new Vector3(0,.025f,-.015f),new Vector3(.007f,.010f,.012f),Color.white,8,5,rotation);
                b.Bone=rig.Head;b.Polish=.12f;
                b.Sphere(p+new Vector3(-.025f,.057f,0),new Vector3(.08f,.029f,.086f),lid,12,7,rotation);
            }
            b.Bone=rig.Head;b.Polish=.12f;
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

        /// A raised leather nose with two visible nostrils. Coordinates are in
        /// model space; +X points out of the muzzle on every current companion.
        public static void Nose(FrontierMesh b,Vector3 center,Vector3 radius,Color leather,Color nostril)
        {
            float polish=b.Polish;
            b.Polish=.16f;
            b.Sphere(center,radius,leather,14,8);
            for(int side=-1;side<=1;side+=2)
            {
                var p=center+new Vector3(radius.x*.83f,-radius.y*.06f,side*radius.z*.50f);
                b.Polish=.08f;
                b.Sphere(p,new Vector3(radius.x*.13f,radius.y*.36f,radius.z*.20f),nostril,10,6);
            }
            b.Polish=.35f;
            b.Sphere(center+new Vector3(radius.x*.67f,radius.y*.54f,0),
                new Vector3(radius.x*.12f,radius.y*.10f,radius.z*.26f),Color.Lerp(leather,Color.white,.24f),8,5);
            b.Polish=polish;
        }

        /// A short curved nail. It starts inside its toe or fingertip, gains a
        /// rounded shoulder, then tapers to a downward point.
        public static void Nail(FrontierMesh b,Vector3 root,Vector3 direction,float width,Color color,bool tiny=false)
        {
            float polish=b.Polish;
            b.Polish=.42f;
            if(tiny)
                b.Sweep(new[]{root,root+direction*.55f+Vector3.up*width*.22f,
                        root+direction-Vector3.up*width*.75f},
                    new[]{new Vector2(width,width*.82f),new Vector2(width*.61f,width*.52f),
                        new Vector2(.003f,.003f)},color,5);
            else
                b.Sweep(new[]{root,root+direction*.36f+Vector3.up*width*.25f,
                        root+direction*.76f,root+direction-Vector3.up*width*.75f},
                    new[]{new Vector2(width,width*.82f),new Vector2(width*.84f,width*.69f),
                        new Vector2(width*.42f,width*.36f),new Vector2(.003f,.003f)},color,7);
            b.Polish=polish;
        }

        public static FrontierBoneDefinition Bone(string name,int parent,float x,float y,float z,FrontierBoneRole role,float side=0,float phase=0)
            =>new FrontierBoneDefinition(name,parent,new Vector3(x,y,z),role,side,phase);
        public static FrontierSocketDefinition Socket(int bone,float x,float y,float z,float scale=1,bool flank=false)
            =>new FrontierSocketDefinition(bone,new Vector3(x,y,z),flank?new Vector3(-90,0,0):Vector3.zero,scale);
    }
}
