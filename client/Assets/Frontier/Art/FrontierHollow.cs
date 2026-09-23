using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    public static class FrontierHollow
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("hollow",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("neck",0,.10f,.91f,0,FrontierBoneRole.Neck),
            Bone("head",1,.65f,1.13f,0,FrontierBoneRole.Head),
            Bone("leg-l",0,-.10f,.77f,.135f,FrontierBoneRole.Leg,1),Bone("knee-l",3,-.19f,.39f,.17f,FrontierBoneRole.Knee,1),
            Bone("leg-r",0,-.10f,.77f,-.135f,FrontierBoneRole.Leg,-1,Mathf.PI),Bone("knee-r",5,-.19f,.39f,-.17f,FrontierBoneRole.Knee,-1,Mathf.PI),
            Bone("eye-l",2,.82f,1.185f,-.11f,FrontierBoneRole.Eye,-1),Bone("eye-r",2,.82f,1.185f,.11f,FrontierBoneRole.Eye,1)
        },Socket(0,-.20f,1.04f,0,.52f),Socket(0,-.22f,.85f,-.23f,.46f,true),Socket(2,.80f,1.29f,0,.35f),new Vector3(.20f,.17f,.12f));
        public static void Build(FrontierMesh b)
        {
            var violet=Hex("#7a6ac0");var dark=Hex("#454660");var pale=Hex("#e2d5db");b.Polish=.16f;
            b.Sphere(new Vector3(-.15f,.86f,0),new Vector3(.28f,.20f,.225f),violet,20,12);
            b.Sphere(new Vector3(-.02f,.78f,0),new Vector3(.19f,.10f,.17f),pale,16,8);
            b.Sweep(new[]{new Vector3(-.36f,.86f,0),new Vector3(-.56f,.93f,0),new Vector3(-.69f,1.03f,0)},
                new[]{new Vector2(.09f,.10f),new Vector2(.055f,.07f),new Vector2(.009f,.012f)},dark);
            b.Bone=1;
            b.Sweep(new[]{new Vector3(.07f,.90f,0),new Vector3(.25f,1.10f,0),new Vector3(.48f,1.16f,0),new Vector3(.66f,1.13f,0)},
                new[]{new Vector2(.12f,.115f),new Vector2(.10f,.095f),new Vector2(.079f,.079f),new Vector2(.08f,.08f)},violet,16);
            b.Sweep(new[]{new Vector3(.11f,.86f,-.005f),new Vector3(.28f,1.045f,-.005f),new Vector3(.55f,1.087f,-.005f)},
                new[]{new Vector2(.045f,.065f),new Vector2(.035f,.060f),new Vector2(.026f,.055f)},pale,10);
            b.Bone=2;
            b.Sphere(new Vector3(.68f,1.145f,0),new Vector3(.235f,.16f,.16f),violet,20,10);
            var muzzle=new Vector3(.85f,1.087f,0);var mr=new Vector3(.15f,.062f,.115f);
            b.Sphere(muzzle,mr,pale,16,8);Smile(b,muzzle,mr,.072f);Eyes(b,Rig,.064f,violet,Hex("#a6ab68"));
            for(int hip=3;hip<=5;hip+=2)
            {
                int knee=hip+1;float side=Rig.Bones[hip].Side;b.Bone=hip;
                b.Cone(Rig.Bones[hip].Position,Rig.Bones[knee].Position,.062f,.045f,violet,12);
                b.Sphere(Rig.Bones[knee].Position,Vector3.one*.057f,dark,10,6);b.Bone=knee;
                var ankle=new Vector3(-.09f,.07f,side*.18f);
                b.Cone(Rig.Bones[knee].Position,ankle,.042f,.032f,dark,12);
                b.Sphere(ankle+new Vector3(.05f,-.025f,0),new Vector3(.15f,.045f,.067f),dark,12,7);
                for(int toe=-1;toe<=1;toe+=2)b.Cone(ankle,new Vector3(.10f,.025f,side*.18f+toe*.05f),.023f,.012f,pale,8);
            }
            b.Bone=0;
        }
    }
}
