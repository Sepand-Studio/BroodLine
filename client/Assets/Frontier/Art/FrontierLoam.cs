using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    /// LOAM, REVISION 03 - Phase 10 Batch 2. The gentle, legless ground-hugger:
    /// five connected segments that taper from a broad chest to a rounded tail,
    /// each with a raised dorsal fold that covers the seam to the next, a warm
    /// cream underside band, a lighter mossy back with darker spots, and a
    /// blunt wide snout with heavy, kind brows. Both combat sockets ride the
    /// middle segment, which is a Segment bone and deforms with the undulation.
    public static class FrontierLoam
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("loam",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("middle",0,0,.25f,0,FrontierBoneRole.Segment,0,0),
            Bone("rear",1,-.33f,.22f,0,FrontierBoneRole.Segment,0,-.8f),Bone("tail",2,-.62f,.19f,0,FrontierBoneRole.Segment,0,-1.6f),
            Bone("front",1,.33f,.27f,0,FrontierBoneRole.Segment,0,.8f),Bone("head",4,.66f,.27f,0,FrontierBoneRole.Head),
            Bone("eye-l",5,.78f,.46f,-.25f,FrontierBoneRole.Eye,-1),Bone("eye-r",5,.78f,.46f,.25f,FrontierBoneRole.Eye,1)
        },Socket(1,-.08f,.55f,0,.64f),Socket(1,.06f,.30f,-.34f,.55f,true),Socket(5,.78f,.50f,0,.4f),new Vector3(.20f,.16f,.20f));

        public static void Build(FrontierMesh b)
        {
            var moss=Hex("#7cc492");var back=Hex("#5f9f75");var shadow=Hex("#46795d");var cream=Hex("#ead9a8");var spot=Hex("#3f6d52");
            // Four body segments, tapering toward the tail; each carries its own
            // fold ridge that overlaps the seam behind it.
            float[] scale={0,1f,.93f,.80f,1.05f};
            for(int i=1;i<=4;i++)
            {
                b.Bone=i;var p=Rig.Bones[i].Position;float s=scale[i];
                b.Polish=.10f;
                b.Sphere(p,new Vector3(.27f,.27f,.33f)*s,moss,14,9);
                b.Polish=.14f;
                b.Sphere(p+new Vector3(0,.10f*s,0),new Vector3(.24f,.20f,.30f)*s,back,12,7,upperOnly:true);
                b.Polish=.08f;
                b.Sphere(p+new Vector3(.02f,-.16f*s,0),new Vector3(.25f,.10f,.27f)*s,cream,12,6);
                // The fold: a raised ridge at the back of the segment, broad and soft.
                b.Polish=.12f;
                b.Sphere(p+new Vector3(-.13f*s,.05f*s,0),new Vector3(.07f,.25f,.335f)*s,shadow,12,7);
                b.Sphere(p+new Vector3(-.12f*s,.075f*s,0),new Vector3(.08f,.235f,.31f)*s,moss,12,7);
                // Darker spots along the back and a side nub where a leg is not.
                for(int side=-1;side<=1;side+=2)
                {
                    b.Sphere(p+new Vector3(.05f*s,.22f*s,side*.14f*s),new Vector3(.05f,.03f,.06f)*s,spot,8,5);
                    b.Sphere(p+new Vector3(.0f,-.06f*s,side*.31f*s),new Vector3(.07f,.06f,.05f)*s,back,8,5);
                }
            }
            // The tail end: a rounded cap behind the last segment.
            b.Bone=3;b.Polish=.10f;
            b.Sphere(Rig.Bones[3].Position+new Vector3(-.20f,-.02f,0),new Vector3(.14f,.16f,.20f),moss,12,8);
            // A flattened head flows into the first body fold. Raised eye humps
            // and a wide two-layer lip replace the founder's round face/chin.
            b.Bone=5;b.Polish=.10f;
            b.Sphere(new Vector3(.64f,.24f,0),new Vector3(.37f,.175f,.36f),moss,18,10);
            b.Polish=.14f;
            b.Sphere(new Vector3(.61f,.34f,0),new Vector3(.30f,.115f,.32f),back,14,7,upperOnly:true);
            b.Polish=.08f;
            b.Sphere(new Vector3(.87f,.105f,0),new Vector3(.22f,.072f,.285f),cream,18,8);
            b.Sphere(new Vector3(.89f,.193f,0),new Vector3(.23f,.075f,.305f),moss,18,8);
            for(int side=-1;side<=1;side+=2)
            {
                b.Sphere(new Vector3(.68f,.407f,side*.25f),new Vector3(.14f,.095f,.13f),moss,12,8);
                b.Sphere(new Vector3(1.075f,.23f,side*.13f),new Vector3(.038f,.026f,.052f),back,10,6);
                b.Sphere(new Vector3(1.108f,.237f,side*.135f),new Vector3(.012f,.009f,.014f),shadow,8,5);
                b.Cone(new Vector3(1.025f,.125f,side*.12f),new Vector3(.975f,.14f,side*.22f),.006f,.003f,shadow,6);
            }
            Eyes(b,Rig,.07f,moss,Hex("#8d7346"));
            // Soft upper lids cap the raised eyes instead of continuing the
            // founder's long horizontal brow.
            b.Bone=5;b.Polish=.10f;
            for(int side=-1;side<=1;side+=2)
                b.Sphere(new Vector3(.76f,.523f,side*.25f),new Vector3(.10f,.034f,.11f),back,10,6,Quaternion.Euler(side*10,0,-8));
            b.Bone=0;b.Polish=.10f;
        }
    }
}
