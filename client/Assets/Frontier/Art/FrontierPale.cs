using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    /// PALE, REVISION 02 - Phase 10 Batch 2. The glider: a small, tucked body
    /// under a broad wing arc. The membranes keep their curved, two-faced
    /// surface and gain the structure that makes a wing read as a wing - a
    /// thick dark leading spar that blends into the articulated tip, three
    /// finger spars fanning to the trailing edge, and a dark wrist knuckle.
    /// The face is pale with a dark eye mask so the eyes read against frost;
    /// the body is slate with a cream belly, tucked feet and two streamers.
    public static class FrontierPale
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("pale",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("head",0,.30f,.55f,0,FrontierBoneRole.Head),
            Bone("wing-l",0,0,.66f,.15f,FrontierBoneRole.Wing,1),Bone("tip-l",2,-.06f,.78f,.80f,FrontierBoneRole.WingTip,1),
            Bone("wing-r",0,0,.66f,-.15f,FrontierBoneRole.Wing,-1),Bone("tip-r",4,-.06f,.78f,-.80f,FrontierBoneRole.WingTip,-1),
            Bone("eye-l",1,.50f,.62f,-.16f,FrontierBoneRole.Eye,-1),Bone("eye-r",1,.50f,.62f,.16f,FrontierBoneRole.Eye,1)
        },Socket(0,-.12f,.74f,0,.58f),Socket(0,-.08f,.42f,-.26f,.53f,true),Socket(1,.42f,.76f,0,.4f),new Vector3(.16f,.26f,.14f));

        public static void Build(FrontierMesh b)
        {
            var frost=Hex("#c6cede");var dark=Hex("#4e6a90");var slate=Hex("#3c5170");var cream=Hex("#f0e5cf");var mask=Hex("#34465f");
            // Body: small slate core, cream belly, and a short tail with two streamers.
            b.Bone=0;b.Polish=.18f;
            b.Sphere(new Vector3(-.02f,.49f,0),new Vector3(.38f,.235f,.25f),dark,20,12);
            b.Polish=.10f;
            b.Sphere(new Vector3(.03f,.34f,0),new Vector3(.29f,.115f,.20f),cream,16,10);
            b.Polish=.18f;
            b.Sweep(new[]{new Vector3(-.30f,.47f,0),new Vector3(-.56f,.45f,0),new Vector3(-.76f,.53f,0)},
                new[]{new Vector2(.13f,.10f),new Vector2(.075f,.06f),new Vector2(.02f,.02f)},dark,10);
            for(int side=-1;side<=1;side+=2)
            {
                b.Sweep(new[]{new Vector3(-.70f,.50f,side*.03f),new Vector3(-.92f,.46f,side*.10f),new Vector3(-1.12f,.50f,side*.17f)},
                    new[]{new Vector2(.03f,.018f),new Vector2(.02f,.012f),new Vector2(.005f,.005f)},slate,8);
                // Tucked feet under the belly.
                b.Sphere(new Vector3(.05f,.27f,side*.11f),new Vector3(.07f,.035f,.045f),slate,8,5);
                for(int toe=-1;toe<=1;toe++)
                    Nail(b,new Vector3(.08f,.26f,side*.11f+toe*.02f),new Vector3(.07f,-.013f,side*.01f+toe*.01f),.011f,slate);
            }

            // Wings: the membrane, its dark leading spar blended shoulder→tip, the wrist, and finger spars.
            for(int side=-1;side<=1;side+=2)
            {
                int shoulder=side==1?2:4,tip=shoulder+1;b.Bone=shoulder;b.Polish=.24f;
                b.Membrane(side,shoulder,tip,frost,Rise);
                b.Polish=.20f;
                for(int i=0;i<8;i++)
                {
                    float t=i/8f,u=(i+1)/8f;
                    Vector3 a=Edge(side,t),c=Edge(side,u);
                    b.SecondBone=tip;b.BoneBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,(t+u)*.5f));
                    b.Cone(a,c,.038f-t*.014f,.036f-u*.014f,dark,8);
                }
                // Wrist knuckle where the tip articulates.
                b.SecondBone=tip;b.BoneBlend=.5f;
                b.Sphere(Edge(side,.55f),new Vector3(.05f,.045f,.05f),slate,10,6);
                // Three finger spars from the wrist down to the trailing edge.
                for(int f=0;f<3;f++)
                {
                    float t0=.30f+f*.16f;
                    Vector3 from=Edge(side,t0);
                    Vector3 to=Trailing(side,t0+.12f);
                    b.BoneBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,t0));
                    b.Cone(from,to,.020f,.006f,dark,6);
                }
                // Fine blue veins sit just above the upper membrane, following
                // the same skin blend as the wing beneath them.
                b.Polish=.15f;
                for(int f=0;f<3;f++)
                {
                    float t=.24f+f*.20f;
                    b.BoneBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.90f,t));
                    var a=MembranePoint(side,t,.18f);
                    var middle=MembranePoint(side,t+.055f,.48f);
                    var end=MembranePoint(side,t+.12f,.84f);
                    var vein=Color.Lerp(frost,dark,.30f);
                    b.Cone(a,middle,.009f,.007f,vein,6);
                    b.Cone(middle,end,.007f,.002f,vein,6);
                }
                b.SecondBone=-1;b.BoneBlend=0;
            }

            // Head: pale, a dark mask across the eyes, a small cream chin, alert brows.
            b.Bone=1;b.Polish=.14f;
            b.Sphere(new Vector3(.31f,.56f,0),new Vector3(.245f,.20f,.215f),frost,20,12);
            b.Sphere(new Vector3(.40f,.62f,0),new Vector3(.15f,.07f,.24f),mask,16,8,Quaternion.Euler(0,0,-8));
            b.Polish=.10f;
            b.Sphere(new Vector3(.46f,.48f,0),new Vector3(.155f,.075f,.16f),cream,16,8);
            Eyes(b,Rig,.090f,frost,Hex("#5c74bd"));
            Smile(b,new Vector3(.46f,.48f,0),new Vector3(.155f,.075f,.16f),.09f);
            b.Bone=1;b.Polish=.14f;
            Nose(b,new Vector3(.60f,.515f,0),new Vector3(.044f,.026f,.070f),slate,mask);
            // Two small ear tufts, dark, so the head has a silhouette of its own.
            for(int side=-1;side<=1;side+=2)
                b.Sweep(new[]{new Vector3(.20f,.70f,side*.12f),new Vector3(.13f,.82f,side*.17f),new Vector3(.08f,.90f,side*.20f)},
                    new[]{new Vector2(.04f,.03f),new Vector2(.025f,.02f),new Vector2(.005f,.005f)},slate,8);
            b.Bone=0;b.Polish=.18f;
        }

        // A flatter arc than the proof's first dome: a glider holds its wings out, not up.
        const float Rise=.13f;
        // The membrane's own leading edge, so spars sit exactly on it.
        static Vector3 Edge(float side,float t)=>new Vector3(.34f-.43f*t*t,FrontierMesh.MembraneY(t,0,Rise)+.02f,side*(.15f+1.18f*t));
        static Vector3 Trailing(float side,float t)
        {
            float leading=.34f-.43f*t*t, trailing=-.40f-.34f*Mathf.Sin(t*Mathf.PI)+.28f*t;
            return new Vector3(trailing+.03f,FrontierMesh.MembraneY(t,1,Rise)+.02f,side*(.15f+1.18f*t));
        }
        static Vector3 MembranePoint(float side,float t,float u)
        {
            float leading=.34f-.43f*t*t,trailing=-.40f-.34f*Mathf.Sin(t*Mathf.PI)+.28f*t;
            return new Vector3(Mathf.Lerp(leading,trailing,u),
                FrontierMesh.MembraneY(t,u,Rise)+.019f+.022f*(1-t),side*(.15f+1.18f*t));
        }
    }
}
