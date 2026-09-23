using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    public static class FrontierEmber
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("ember",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),Bone("head",0,.14f,1.06f,0,FrontierBoneRole.Head),
            Bone("leg-l",0,-.02f,.48f,.18f,FrontierBoneRole.Leg,1),Bone("knee-l",2,.075f,.25f,.20f,FrontierBoneRole.Knee,1),
            Bone("leg-r",0,-.02f,.48f,-.18f,FrontierBoneRole.Leg,-1,Mathf.PI),Bone("knee-r",4,.075f,.25f,-.20f,FrontierBoneRole.Knee,-1,Mathf.PI),
            Bone("arm-l",0,.04f,.85f,.23f,FrontierBoneRole.Arm,1),Bone("arm-r",0,.04f,.85f,-.23f,FrontierBoneRole.Arm,-1),
            Bone("eye-l",1,.365f,1.14f,-.18f,FrontierBoneRole.Eye,-1),Bone("eye-r",1,.365f,1.14f,.18f,FrontierBoneRole.Eye,1)
        },Socket(0,-.20f,.94f,0,.70f),Socket(0,-.10f,.65f,-.265f,.65f,true),Socket(1,.39f,1.21f,0,.45f),new Vector3(.14f,.15f,.13f));

        public static void Build(FrontierMesh b)
        {
            var coral=Hex("#e5867a");var shade=Hex("#af5962");var cream=Hex("#f4dfb9");b.Polish=.12f;
            b.Sphere(new Vector3(-.045f,.70f,0),new Vector3(.255f,.37f,.25f),coral,20,12);
            b.Sphere(new Vector3(.16f,.70f,0),new Vector3(.095f,.27f,.185f),cream,16,10);
            b.Sweep(new[]{new Vector3(-.20f,.48f,0),new Vector3(-.46f,.38f,0),new Vector3(-.71f,.39f,0),new Vector3(-.86f,.51f,0)},
                new[]{new Vector2(.15f,.14f),new Vector2(.11f,.09f),new Vector2(.06f,.045f),new Vector2(.012f,.012f)},shade);
            b.Bone=1;
            b.Sphere(new Vector3(.17f,1.065f,0),new Vector3(.30f,.255f,.25f),coral,22,12);
            var muzzle=new Vector3(.39f,.98f,0);var mr=new Vector3(.19f,.09f,.195f);
            b.Sphere(muzzle,mr,cream,18,10);Smile(b,muzzle,mr,.13f);Eyes(b,Rig,.09f,coral,Hex("#af6835"));
            // Swept blades form a continuous crest with dark roots and warm tips.
            b.Polish=.34f;
            for(int i=0;i<3;i++)
            {
                float x=.12f-i*.15f,y=1.25f-i*.03f;
                b.Sweep(new[]{new Vector3(x,y,0),new Vector3(x-.02f,y+.14f,0),new Vector3(x-.12f,y+.29f-i*.035f,0),new Vector3(x-.24f,y+.34f-i*.045f,0)},
                    new[]{new Vector2(.12f,.067f),new Vector2(.095f,.054f),new Vector2(.055f,.033f),new Vector2(.008f,.009f)},i==0?Hex("#eeb85c"):Hex("#d99049"));
            }
            b.Polish=.12f;
            for(int side=-1;side<=1;side+=2)
            {
                int hip=side==1?2:4,knee=hip+1,arm=side==1?6:7;
                var p=Rig.Bones[hip].Position;var k=Rig.Bones[knee].Position;b.Bone=hip;
                b.Sweep(new[]{p,k},new[]{new Vector2(.12f,.115f),new Vector2(.085f,.08f)},coral);
                b.Sphere(k,new Vector3(.093f,.09f,.09f),coral,12,7);b.Bone=knee;
                b.Cone(k,new Vector3(.01f,.085f,side*.22f),.075f,.055f,shade,12);
                b.Sphere(new Vector3(.125f,.072f,side*.22f),new Vector3(.23f,.075f,.13f),shade,16,8);
                for(int toe=-1;toe<=1;toe++)b.Sphere(new Vector3(.31f,.063f,side*.22f+toe*.068f),new Vector3(.055f,.036f,.032f),cream,8,5);
                b.Bone=arm;var shoulder=Rig.Bones[arm].Position;
                b.Sweep(new[]{shoulder,shoulder+new Vector3(-.025f,-.16f,side*.09f),shoulder+new Vector3(.17f,-.13f,side*.10f)},
                    new[]{new Vector2(.08f,.075f),new Vector2(.055f,.05f),new Vector2(.035f,.04f)},coral);
                b.Sphere(shoulder+new Vector3(.18f,-.13f,side*.10f),new Vector3(.08f,.06f,.066f),cream,12,7);
            }
            b.Bone=0;
        }
    }
}
