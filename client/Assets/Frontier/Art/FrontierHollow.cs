using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    /// HOLLOW, REVISION 02 - Phase 10 Batch 2. The watchful sniper: a tiny
    /// torso high on two stilt legs and a long forward neck, kept; a tapered
    /// head with a brow ridge and two plumes on the crown, focused eyes, a
    /// pale throat stripe the length of the neck, dark socks and long toes,
    /// and a short plume tail. Everything about it is a line aimed forward;
    /// nothing about it is Ember's mane or bulk.
    public static class FrontierHollow
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("hollow",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("neck",0,.10f,.91f,0,FrontierBoneRole.Neck),
            Bone("head",1,.66f,1.15f,0,FrontierBoneRole.Head),
            Bone("leg-l",0,-.10f,.77f,.135f,FrontierBoneRole.Leg,1),Bone("knee-l",3,-.19f,.39f,.17f,FrontierBoneRole.Knee,1),
            Bone("leg-r",0,-.10f,.77f,-.135f,FrontierBoneRole.Leg,-1,Mathf.PI),Bone("knee-r",5,-.19f,.39f,-.17f,FrontierBoneRole.Knee,-1,Mathf.PI),
            Bone("eye-l",2,.84f,1.20f,-.11f,FrontierBoneRole.Eye,-1),Bone("eye-r",2,.84f,1.20f,.11f,FrontierBoneRole.Eye,1)
        },Socket(0,-.20f,1.05f,0,.52f),Socket(0,-.22f,.85f,-.235f,.46f,true),Socket(2,.72f,1.32f,0,.35f),new Vector3(.20f,.17f,.12f));
        public static void Build(FrontierMesh b)
        {
            var violet=Hex("#7a6ac0");var deep=Hex("#5b4c9c");var dark=Hex("#3d3b58");var pale=Hex("#ebe0e6");var plume=Hex("#a99bd9");
            // Torso: small, a pale chest, a short plume tail.
            b.Bone=0;b.Polish=.16f;
            b.Sphere(new Vector3(-.15f,.86f,0),new Vector3(.29f,.205f,.23f),violet,20,12);
            b.Polish=.10f;
            b.Sphere(new Vector3(-.03f,.79f,0),new Vector3(.20f,.11f,.175f),pale,16,8);
            b.Polish=.16f;
            b.Sweep(new[]{new Vector3(-.38f,.88f,0),new Vector3(-.56f,.96f,0),new Vector3(-.68f,1.07f,0)},
                new[]{new Vector2(.09f,.10f),new Vector2(.055f,.07f),new Vector2(.01f,.012f)},deep,10);
            for(int i=-1;i<=1;i++)
                b.Sweep(new[]{new Vector3(-.60f,1.00f,i*.035f),new Vector3(-.76f,1.12f,i*.07f),new Vector3(-.88f,1.24f,i*.10f)},
                    new[]{new Vector2(.028f,.02f),new Vector2(.02f,.014f),new Vector2(.005f,.005f)},plume,8);
            // Neck: long, forward, with a pale throat stripe along its underside.
            b.Bone=1;
            b.Sweep(new[]{new Vector3(.07f,.90f,0),new Vector3(.25f,1.10f,0),new Vector3(.48f,1.17f,0),new Vector3(.66f,1.15f,0)},
                new[]{new Vector2(.12f,.115f),new Vector2(.10f,.095f),new Vector2(.08f,.08f),new Vector2(.08f,.08f)},violet,14);
            b.Polish=.10f;
            b.Sweep(new[]{new Vector3(.11f,.85f,0),new Vector3(.28f,1.04f,0),new Vector3(.52f,1.10f,0),new Vector3(.64f,1.09f,0)},
                new[]{new Vector2(.045f,.06f),new Vector2(.038f,.058f),new Vector2(.03f,.052f),new Vector2(.02f,.03f)},pale,10);
            // Head: tapered forward, a brow ridge over focused eyes, two plumes on the crown.
            b.Bone=2;b.Polish=.16f;
            b.Sphere(new Vector3(.70f,1.16f,0),new Vector3(.24f,.15f,.15f),violet,20,10);
            var muzzle=new Vector3(.89f,1.10f,0);var mr=new Vector3(.16f,.06f,.105f);
            b.Polish=.10f;b.Sphere(muzzle,mr,pale,16,8);Smile(b,muzzle,mr,.065f);
            Nose(b,new Vector3(1.035f,1.125f,0),new Vector3(.037f,.022f,.051f),deep,dark);
            b.Polish=.16f;
            b.Sphere(new Vector3(.80f,1.27f,0),new Vector3(.14f,.045f,.15f),deep,14,6);
            Eyes(b,Rig,.070f,violet,Hex("#b9c26a"));
            b.Bone=2;b.Polish=.16f;
            for(int side=-1;side<=1;side+=2)
            {
                b.Sweep(new[]{new Vector3(.62f,1.28f,side*.05f),new Vector3(.50f,1.42f,side*.09f),new Vector3(.36f,1.50f,side*.13f)},
                    new[]{new Vector2(.035f,.025f),new Vector2(.025f,.018f),new Vector2(.005f,.005f)},plume,8);
                b.Sweep(new[]{new Vector3(.56f,1.37f,side*.09f),new Vector3(.45f,1.46f,side*.12f),new Vector3(.37f,1.50f,side*.135f)},
                    new[]{new Vector2(.009f,.007f),new Vector2(.007f,.005f),new Vector2(.002f,.002f)},pale,6);
            }
            // Stilt legs: thigh, a rounded knee, a dark sock down to long toes.
            for(int hip=3;hip<=5;hip+=2)
            {
                int knee=hip+1;float side=Rig.Bones[hip].Side;b.Bone=hip;
                b.Sphere(Rig.Bones[hip].Position,new Vector3(.09f,.08f,.07f),violet,10,7);
                b.Cone(Rig.Bones[hip].Position,Rig.Bones[knee].Position,.064f,.046f,violet,12);
                b.Sphere(Rig.Bones[knee].Position,Vector3.one*.06f,deep,10,6);b.Bone=knee;
                var ankle=new Vector3(-.09f,.075f,side*.18f);
                b.Cone(Rig.Bones[knee].Position,ankle,.044f,.034f,dark,12);
                b.Sphere(ankle+new Vector3(.03f,-.03f,0),new Vector3(.11f,.04f,.06f),dark,12,7);
                for(int toe=-1;toe<=1;toe++)
                {
                    var tip=new Vector3(.16f,.02f,side*.18f+toe*.055f);
                    b.Cone(ankle+new Vector3(.02f,-.03f,0),tip,.022f,.012f,dark,8);
                    Nail(b,tip,new Vector3(.045f,-.025f,toe*.007f),.010f,pale);
                }
                b.Cone(ankle+new Vector3(-.02f,-.03f,0),new Vector3(-.16f,.02f,side*.18f),.018f,.006f,dark,8);
            }
            b.Bone=0;b.Polish=.16f;
        }
    }
}
