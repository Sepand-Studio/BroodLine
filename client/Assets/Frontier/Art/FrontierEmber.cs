using UnityEngine;
using static Broodline.Frontier.FrontierFace;

namespace Broodline.Frontier
{
    /// EMBER, REVISION 02 - Phase 10 Batch 2. The tall narrow biped kept; a
    /// real neck under a sleeker head; a swept flame mane that runs from the
    /// brow over the crown and down the nape as one shape rather than three
    /// blades; a cream chest and throat; a long tapered tail for balance; and
    /// planted three-toed feet with dark claws. Coral hide, deeper coral
    /// shade, warm gold-to-orange crest. +X is forward.
    public static class FrontierEmber
    {
        public static readonly FrontierRigDefinition Rig=new FrontierRigDefinition("ember",new[]{
            Bone("root",-1,0,0,0,FrontierBoneRole.Root),
            Bone("neck",0,.09f,.93f,0,FrontierBoneRole.Neck),
            Bone("head",1,.20f,1.13f,0,FrontierBoneRole.Head),
            Bone("leg-l",0,-.03f,.42f,.17f,FrontierBoneRole.Leg,1),Bone("knee-l",3,.06f,.21f,.19f,FrontierBoneRole.Knee,1),
            Bone("leg-r",0,-.03f,.42f,-.17f,FrontierBoneRole.Leg,-1,Mathf.PI),Bone("knee-r",5,.06f,.21f,-.19f,FrontierBoneRole.Knee,-1,Mathf.PI),
            Bone("arm-l",0,.07f,.82f,.22f,FrontierBoneRole.Arm,1),Bone("arm-r",0,.07f,.82f,-.22f,FrontierBoneRole.Arm,-1),
            Bone("eye-l",2,.37f,1.19f,-.165f,FrontierBoneRole.Eye,-1),Bone("eye-r",2,.37f,1.19f,.165f,FrontierBoneRole.Eye,1)
        },Socket(0,-.22f,.90f,0,.70f),Socket(0,-.09f,.62f,-.255f,.62f,true),Socket(2,.30f,1.36f,0,.42f),new Vector3(.16f,.15f,.14f));

        public static void Build(FrontierMesh b)
        {
            var coral=Hex("#e5867a");var shade=Hex("#b8605f");var deep=Hex("#8f4550");var cream=Hex("#f6e3c1");
            var gold=Hex("#f2c05c");var orange=Hex("#e0923a");var ember=Hex("#c9642f");var claw=Hex("#3b2a2f");
            var lean=Quaternion.Euler(0,0,-9);

            // Torso: a slim upright egg leaning into the walk, hips a little wider.
            b.Bone=0;b.Polish=.12f;
            b.Sphere(new Vector3(.0f,.66f,0),new Vector3(.235f,.34f,.215f),coral,20,12,lean);
            b.Sphere(new Vector3(-.03f,.44f,0),new Vector3(.24f,.17f,.235f),coral,18,10);
            b.Polish=.09f;
            b.Sphere(new Vector3(.155f,.63f,0),new Vector3(.09f,.27f,.15f),cream,16,10,lean);
            // Tail: long, tapered, lifted at the tip; darker underside carried by the shade tone.
            b.Polish=.12f;
            b.Sweep(new[]{new Vector3(-.16f,.42f,0),new Vector3(-.42f,.33f,0),new Vector3(-.70f,.33f,0),new Vector3(-.95f,.44f,0),new Vector3(-1.10f,.60f,0)},
                new[]{new Vector2(.15f,.15f),new Vector2(.115f,.10f),new Vector2(.075f,.065f),new Vector2(.04f,.035f),new Vector2(.01f,.01f)},shade,10);
            for(int i=0;i<3;i++)
            {
                float x=-.62f-i*.16f,y=.40f+i*.06f;
                b.Sweep(new[]{new Vector3(x,y,0),new Vector3(x-.05f,y+.10f,0),new Vector3(x-.13f,y+.15f,0)},
                    new[]{new Vector2(.035f,.02f),new Vector2(.022f,.013f),new Vector2(.005f,.005f)},i==1?gold:orange,8);
            }

            // Neck: its own bone, so the head turns from the shoulders.
            b.Bone=1;
            b.Sweep(new[]{new Vector3(.08f,.88f,0),new Vector3(.13f,1.00f,0),new Vector3(.19f,1.10f,0)},
                new[]{new Vector2(.115f,.105f),new Vector2(.10f,.09f),new Vector2(.095f,.09f)},coral,12);
            b.Polish=.09f;
            b.Sphere(new Vector3(.16f,.98f,0),new Vector3(.07f,.11f,.09f),cream,12,8);

            // Head: sleek, a broad cream muzzle, expressive eyes with brows.
            b.Bone=2;b.Polish=.12f;
            b.Sphere(new Vector3(.22f,1.15f,0),new Vector3(.215f,.185f,.185f),coral,22,12);
            var muzzle=new Vector3(.40f,1.07f,0);var mr=new Vector3(.17f,.09f,.135f);
            b.Polish=.09f;b.Sphere(muzzle,mr,cream,18,10);
            Smile(b,muzzle,mr,.10f);
            for(int side=-1;side<=1;side+=2) b.Sphere(new Vector3(.545f,1.095f,side*.05f),new Vector3(.012f,.010f,.016f),deep,8,5);
            Eyes(b,Rig,.10f,coral,Hex("#c58730"));
            // Cheek flush: two soft spots that read as warmth, not freckles.
            b.Bone=2;b.Polish=.12f;
            for(int side=-1;side<=1;side+=2) b.Sphere(new Vector3(.31f,1.08f,side*.175f),new Vector3(.045f,.035f,.02f),shade,10,6);

            // The mane: one swept flame from the brow over the crown and down the
            // nape, built from overlapping blades so it reads as a single crest.
            b.Polish=.34f;
            for(int i=0;i<5;i++)
            {
                float t=i/4f;
                var rootP=new Vector3(.33f-t*.42f,1.25f-t*.11f,0);
                float lift=.24f-t*.08f, sweep=.30f+t*.04f;
                b.Sweep(new[]{rootP,rootP+new Vector3(-sweep*.25f,lift*.6f,0),rootP+new Vector3(-sweep*.65f,lift*.95f,0),rootP+new Vector3(-sweep,lift*.85f,0)},
                    new[]{new Vector2(.11f-t*.03f,.06f-t*.015f),new Vector2(.09f-t*.025f,.05f-t*.012f),new Vector2(.05f,.03f),new Vector2(.008f,.008f)},
                    i==0?gold:i==4?ember:orange,8);
            }

            // Arms: shoulder to a cream hand held forward, ready to wave.
            b.Polish=.12f;
            for(int side=-1;side<=1;side+=2)
            {
                int arm=side==1?7:8;b.Bone=arm;var s=Rig.Bones[arm].Position;
                b.Sweep(new[]{s,s+new Vector3(.02f,-.15f,side*.07f),s+new Vector3(.18f,-.17f,side*.09f)},
                    new[]{new Vector2(.085f,.08f),new Vector2(.06f,.055f),new Vector2(.045f,.045f)},coral,10);
                var hand=s+new Vector3(.20f,-.175f,side*.10f);
                b.Polish=.09f;b.Sphere(hand,new Vector3(.075f,.055f,.065f),cream,12,7);b.Polish=.12f;
                for(int finger=-1;finger<=1;finger++) b.Cone(hand+new Vector3(.05f,0,finger*.03f),hand+new Vector3(.11f,-.02f,finger*.04f),.014f,.005f,claw,6);
            }

            // Legs: thigh, knee, shin, and a planted foot with three toes and claws.
            for(int side=-1;side<=1;side+=2)
            {
                int hip=side==1?3:5,knee=hip+1;
                var p=Rig.Bones[hip].Position;var k=Rig.Bones[knee].Position;
                b.Bone=hip;b.Polish=.12f;
                b.Sphere(p,new Vector3(.15f,.14f,.13f),coral,12,8);
                b.Sweep(new[]{p,k},new[]{new Vector2(.125f,.115f),new Vector2(.08f,.075f)},coral,12);
                b.Sphere(k,new Vector3(.085f,.08f,.08f),coral,12,7);
                b.Bone=knee;
                var ankle=new Vector3(.02f,.07f,side*.21f);
                b.Cone(k,ankle,.072f,.058f,shade,12);
                b.Sphere(ankle+new Vector3(-.02f,.0f,0),new Vector3(.07f,.06f,.07f),shade,10,6);
                b.Sphere(ankle+new Vector3(.12f,-.015f,0),new Vector3(.22f,.062f,.12f),shade,16,8);
                for(int toe=-1;toe<=1;toe++)
                {
                    var tip=ankle+new Vector3(.30f,-.03f,toe*.075f);
                    b.Sphere(ankle+new Vector3(.24f,-.025f,toe*.07f),new Vector3(.075f,.038f,.036f),cream,10,6);
                    b.Cone(tip,tip+new Vector3(.055f,-.01f,toe*.01f),.022f,.004f,claw,6);
                }
                b.Sphere(ankle+new Vector3(-.10f,-.02f,0),new Vector3(.06f,.035f,.05f),shade,8,5);
            }
            b.Bone=0;b.Polish=.12f;
        }
    }
}
