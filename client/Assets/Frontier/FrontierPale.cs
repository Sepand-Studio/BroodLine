using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    public static class FrontierPale
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("pale",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("head",0,.30f,.55f,0,FrontierBoneRole.Head),
            Bone("wing-l",0,0,.66f,.15f,FrontierBoneRole.Wing,1),Bone("tip-l",2,-.06f,.85f,.80f,FrontierBoneRole.WingTip,1),
            Bone("wing-r",0,0,.66f,-.15f,FrontierBoneRole.Wing,-1),Bone("tip-r",4,-.06f,.85f,-.80f,FrontierBoneRole.WingTip,-1),
            Bone("eye-l",1,.48f,.60f,-.16f,FrontierBoneRole.Eye,-1),Bone("eye-r",1,.48f,.60f,.16f,FrontierBoneRole.Eye,1)
        },Socket(0,-.12f,.73f,0,.58f),Socket(0,-.08f,.40f,-.255f,.53f,true),Socket(1,.46f,.72f,0,.4f),new Vector3(.16f,.26f,.14f));

        public static void Build(FrontierMesh b)
        {
            var frost=Hex("#c6cede");var dark=Hex("#536f92");b.Polish=.20f;
            b.Sphere(new Vector3(-.02f,.48f,0),new Vector3(.40f,.245f,.265f),dark,20,12);
            b.Sphere(new Vector3(.02f,.32f,0),new Vector3(.30f,.115f,.205f),Hex("#f0e5cf"),16,10);
            b.Sweep(new[]{new Vector3(-.30f,.45f,0),new Vector3(-.55f,.42f,0),new Vector3(-.74f,.50f,0)},
                new[]{new Vector2(.13f,.10f),new Vector2(.07f,.055f),new Vector2(.013f,.013f)},dark);
            for(int side=-1;side<=1;side+=2)
            {
                int shoulder=side==1?2:4,tip=shoulder+1;b.Bone=shoulder;b.Polish=.24f;
                b.Membrane(side,shoulder,tip,frost);
                // The leading support blends across the same shoulder/tip boundary as the membrane.
                for(int i=0;i<8;i++)
                {
                    float t=i/8f,u=(i+1)/8f;
                    Vector3 a=new Vector3(.34f-.43f*t*t,.66f+.24f*Mathf.Sin(t*Mathf.PI*.65f),side*(.15f+1.18f*t));
                    Vector3 c=new Vector3(.34f-.43f*u*u,.66f+.24f*Mathf.Sin(u*Mathf.PI*.65f),side*(.15f+1.18f*u));
                    b.SecondBone=tip;b.BoneBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,(t+u)*.5f));
                    b.Cone(a,c,.027f,.026f,dark,8);
                }
                b.SecondBone=-1;b.BoneBlend=0;
            }
            b.Bone=1;b.Polish=.12f;
            b.Sphere(new Vector3(.31f,.55f,0),new Vector3(.26f,.205f,.225f),frost,20,12);
            b.Sphere(new Vector3(.45f,.48f,0),new Vector3(.165f,.08f,.175f),Hex("#f0e5cf"),16,8);
            Eyes(b,Rig,.073f,dark,Hex("#647bba"));
            Smile(b,new Vector3(.45f,.48f,0),new Vector3(.165f,.08f,.175f),.11f);
            b.Bone=0;
        }
    }
}
