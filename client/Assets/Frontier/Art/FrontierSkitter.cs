using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    /// SKITTER, REVISION 02 - Phase 10 Batch 2. The inquisitive scout: a
    /// compact amber body under a soft dorsal saddle, a bigger head with big
    /// eyes and ear tufts, a light chest ruff, and six clearly separated legs
    /// that are limbs rather than sticks - a rounded shoulder, a thigh, a
    /// rounded knee, a shin and a dark paw with toes. Warm-blooded, not
    /// chitinous (bible 10.7): no shell, no mandibles, fur-soft joints.
    public static class FrontierSkitter
    {
        public static readonly FrontierRigDefinition Rig=MakeRig();
        static FrontierRigDefinition MakeRig()
        {
            var bones=new FrontierBoneDefinition[16];
            bones[0]=Bone("root",-1,0,0,0,FrontierBoneRole.Root);
            bones[1]=Bone("head",0,.30f,.47f,0,FrontierBoneRole.Head);
            for(int pair=0;pair<3;pair++)for(int side=-1;side<=1;side+=2)
            {
                int index=2+pair*4+(side==1?0:2);float x=.20f-pair*.22f;
                float phase=(pair+(side==1?0:1))%2*Mathf.PI;
                bones[index]=Bone("leg-"+pair+"-"+side,0,x,.40f,side*.20f,FrontierBoneRole.Leg,side,phase);
                bones[index+1]=Bone("knee-"+pair+"-"+side,index,x+(1-pair)*.17f,.30f,side*.47f,FrontierBoneRole.Knee,side,phase);
            }
            bones[14]=Bone("eye-l",1,.455f,.53f,-.115f,FrontierBoneRole.Eye,-1);
            bones[15]=Bone("eye-r",1,.455f,.53f,.115f,FrontierBoneRole.Eye,1);
            return new FrontierRigDefinition("skitter",bones,Socket(0,-.06f,.64f,0,.51f),Socket(0,-.14f,.47f,-.27f,.40f,true),
                Socket(1,.40f,.66f,0,.32f),new Vector3(.13f,.12f,.17f));
        }
        public static void Build(FrontierMesh b)
        {
            var amber=Hex("#e8b34a");var saddle=Hex("#cf9538");var dark=Hex("#5b4327");var paw=Hex("#3f2f1e");var light=Hex("#f6e2b0");
            // Body: a compact egg, a slightly darker saddle on the back, a light chest ruff.
            b.Bone=0;b.Polish=.16f;
            b.Sphere(new Vector3(-.05f,.44f,0),new Vector3(.36f,.225f,.27f),amber,20,12);
            b.Polish=.22f;
            b.Sphere(new Vector3(-.10f,.56f,0),new Vector3(.30f,.13f,.24f),saddle,18,9,upperOnly:true);
            b.Polish=.10f;
            b.Sphere(new Vector3(.09f,.36f,0),new Vector3(.27f,.13f,.23f),light,16,8);
            b.Sphere(new Vector3(.20f,.47f,0),new Vector3(.16f,.15f,.20f),light,14,8);
            // A short tuft of a tail.
            b.Polish=.16f;
            b.Sweep(new[]{new Vector3(-.36f,.46f,0),new Vector3(-.50f,.50f,0),new Vector3(-.60f,.58f,0)},
                new[]{new Vector2(.06f,.05f),new Vector2(.04f,.035f),new Vector2(.006f,.006f)},saddle,8);
            // Head: bigger, rounder, with a light muzzle, big eyes and ear tufts.
            b.Bone=1;b.Polish=.14f;
            b.Sphere(new Vector3(.31f,.48f,0),new Vector3(.20f,.165f,.18f),amber,18,10);
            var muzzle=new Vector3(.46f,.415f,0);var mr=new Vector3(.115f,.06f,.12f);
            b.Polish=.10f;b.Sphere(muzzle,mr,light,14,8);Smile(b,muzzle,mr,.075f);
            Nose(b,new Vector3(.570f,.445f,0),new Vector3(.041f,.027f,.054f),dark,paw);
            Eyes(b,Rig,.078f,amber,Hex("#7a4f24"));
            b.Bone=1;b.Polish=.14f;
            for(int side=-1;side<=1;side+=2)
            {
                b.Sweep(new[]{new Vector3(.24f,.60f,side*.11f),new Vector3(.18f,.72f,side*.16f),new Vector3(.14f,.80f,side*.19f)},
                    new[]{new Vector2(.05f,.035f),new Vector2(.035f,.025f),new Vector2(.006f,.006f)},saddle,8);
                b.Sweep(new[]{new Vector3(.22f,.66f,side*.145f),new Vector3(.18f,.73f,side*.173f),new Vector3(.16f,.76f,side*.182f)},
                    new[]{new Vector2(.020f,.012f),new Vector2(.013f,.009f),new Vector2(.003f,.003f)},light,6);
                b.Sphere(new Vector3(.36f,.44f,side*.16f),new Vector3(.04f,.03f,.02f),saddle,8,5);
            }
            // Six legs: shoulder pad, thigh, knee, shin, paw with three toes.
            for(int i=2;i<14;i+=2)
            {
                var hip=Rig.Bones[i].Position;var knee=Rig.Bones[i+1].Position;float side=Rig.Bones[i].Side;
                var foot=new Vector3(knee.x+.05f,.045f,side*.60f);b.Bone=i;b.Polish=.16f;
                b.Sphere(hip,new Vector3(.085f,.075f,.07f),amber,10,7);
                b.Sweep(new[]{hip,hip+(knee-hip)*.5f+Vector3.up*.035f,knee},
                    new[]{new Vector2(.062f,.056f),new Vector2(.052f,.048f),new Vector2(.042f,.040f)},amber,10);
                b.Sphere(knee,Vector3.one*.06f,dark,10,6);b.Bone=i+1;
                b.Sweep(new[]{knee,foot+Vector3.up*.02f},new[]{new Vector2(.040f,.036f),new Vector2(.028f,.026f)},dark,10);
                b.Sphere(foot,new Vector3(.06f,.035f,.05f),paw,10,6);
                for(int toe=-1;toe<=1;toe++)
                {
                    var tip=foot+new Vector3(.05f,-.005f,toe*.032f+side*.01f);
                    b.Sphere(tip,new Vector3(.034f,.022f,.019f),paw,8,5);
                    Nail(b,tip+new Vector3(.027f,.003f,0),new Vector3(.038f,-.005f,toe*.005f),.009f,light,true);
                }
            }
            b.Bone=0;b.Polish=.16f;
        }
    }
}
