using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    public static class FrontierSkitter
    {
        public static readonly FrontierRigDefinition Rig=MakeRig();
        static FrontierRigDefinition MakeRig()
        {
            var bones=new FrontierBoneDefinition[16];
            bones[0]=Bone("root",-1,0,0,0,FrontierBoneRole.Root);
            bones[1]=Bone("head",0,.29f,.43f,0,FrontierBoneRole.Head);
            for(int pair=0;pair<3;pair++)for(int side=-1;side<=1;side+=2)
            {
                int index=2+pair*4+(side==1?0:2);float x=.21f-pair*.23f;
                float phase=(pair+(side==1?0:1))%2*Mathf.PI;
                bones[index]=Bone("leg-"+pair+"-"+side,0,x,.40f,side*.19f,FrontierBoneRole.Leg,side,phase);
                bones[index+1]=Bone("knee-"+pair+"-"+side,index,x+(1-pair)*.18f,.29f,side*.48f,FrontierBoneRole.Knee,side,phase);
            }
            bones[14]=Bone("eye-l",1,.435f,.485f,-.105f,FrontierBoneRole.Eye,-1);
            bones[15]=Bone("eye-r",1,.435f,.485f,.105f,FrontierBoneRole.Eye,1);
            return new FrontierRigDefinition("skitter",bones,Socket(0,-.06f,.62f,0,.51f),Socket(0,-.14f,.47f,-.255f,.40f,true),
                Socket(1,.40f,.58f,0,.32f),new Vector3(.13f,.12f,.17f));
        }
        public static void Build(FrontierMesh b)
        {
            var amber=Hex("#e8b34a");var dark=Hex("#705533");var light=Hex("#f3d89e");b.Polish=.28f;
            b.Sphere(new Vector3(-.055f,.43f,0),new Vector3(.34f,.205f,.26f),amber,20,12);
            b.Sphere(new Vector3(-.09f,.59f,0),new Vector3(.27f,.05f,.18f),Hex("#cc9136"),18,8);
            b.Sphere(new Vector3(.06f,.33f,0),new Vector3(.25f,.10f,.21f),light,16,8);
            b.Bone=1;b.Polish=.14f;
            b.Sphere(new Vector3(.30f,.44f,0),new Vector3(.18f,.14f,.16f),amber,18,10);
            var muzzle=new Vector3(.435f,.385f,0);var mr=new Vector3(.10f,.055f,.115f);
            b.Sphere(muzzle,mr,light,14,8);Smile(b,muzzle,mr,.072f);Eyes(b,Rig,.060f,amber,Hex("#8d6430"));
            for(int i=2;i<14;i+=2)
            {
                var hip=Rig.Bones[i].Position;var knee=Rig.Bones[i+1].Position;float side=Rig.Bones[i].Side;
                var foot=new Vector3(knee.x+.055f,.04f,side*.62f);b.Bone=i;b.Polish=.17f;
                b.Sweep(new[]{hip,hip+(knee-hip)*.5f+Vector3.up*.03f,knee},
                    new[]{new Vector2(.057f,.052f),new Vector2(.048f,.046f),new Vector2(.040f,.038f)},amber,10);
                b.Sphere(knee,Vector3.one*.058f,dark,10,6);b.Bone=i+1;
                b.Sweep(new[]{knee,foot},new[]{new Vector2(.042f,.038f),new Vector2(.023f,.023f)},dark,10);
                b.Sphere(foot,new Vector3(.078f,.041f,.041f),dark,10,6);
            }
            b.Bone=0;
        }
    }
}
