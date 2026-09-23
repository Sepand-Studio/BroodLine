using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    public static class FrontierLoam
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("loam",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("middle",0,0,.26f,0,FrontierBoneRole.Segment,0,0),
            Bone("rear",1,-.31f,.235f,0,FrontierBoneRole.Segment,0,-.8f),Bone("tail",2,-.60f,.205f,0,FrontierBoneRole.Segment,0,-1.6f),
            Bone("front",1,.31f,.28f,0,FrontierBoneRole.Segment,0,.8f),Bone("head",4,.63f,.285f,0,FrontierBoneRole.Head),
            Bone("eye-l",5,.83f,.37f,-.16f,FrontierBoneRole.Eye,-1),Bone("eye-r",5,.83f,.37f,.16f,FrontierBoneRole.Eye,1)
        },Socket(1,-.10f,.53f,0,.64f),Socket(1,.08f,.30f,-.32f,.55f,true),Socket(5,.79f,.49f,0,.4f),new Vector3(.20f,.16f,.20f));
        public static void Build(FrontierMesh b)
        {
            var moss=Hex("#7cc492");var shadow=Hex("#50846a");var cream=Hex("#e5d7a5");b.Polish=.10f;
            // Each overlapping volume follows its own segment. Broad folds cover joint seams.
            for(int i=1;i<=4;i++)
            {
                b.Bone=i;var p=Rig.Bones[i].Position;float scale=i==3?.78f:i==2?.92f:1;
                b.Sphere(p,new Vector3(.255f,.26f,.32f)*scale,Color.Lerp(moss,shadow,i==3?.22f:.06f),20,12);
                b.Sphere(p+new Vector3(.02f,-.17f*scale,0),new Vector3(.24f,.09f,.26f)*scale,cream,16,8);
                b.Sphere(p+new Vector3(-.11f,.04f,0),new Vector3(.065f,.245f,.315f)*scale,shadow,16,10);
                b.Sphere(p+new Vector3(-.105f,.062f,0),new Vector3(.075f,.224f,.29f)*scale,moss,16,10);
            }
            b.Bone=5;
            b.Sphere(new Vector3(.64f,.285f,0),new Vector3(.30f,.235f,.27f),moss,22,12);
            var muzzle=new Vector3(.825f,.205f,0);var mr=new Vector3(.19f,.11f,.22f);
            b.Sphere(muzzle,mr,cream,18,10);Smile(b,muzzle,mr,.15f);Eyes(b,Rig,.081f,moss,Hex("#91774a"));
            for(int side=-1;side<=1;side+=2)
                b.Sphere(new Vector3(.980f,.261f,side*.066f),new Vector3(.009f,.009f,.013f),shadow,8,5);
            b.Bone=0;
        }
    }
}
