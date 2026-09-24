using UnityEngine;

namespace Broodline.Frontier
{
    public struct FrontierBonePose { public Vector3 Position, Scale; public Quaternion Rotation; }
    public struct FrontierMotionState
    {
        public bool Moving, ReducedMotion;
        public float Growth, Hurt, LookYaw, AttackUntil, HitUntil, GreetingUntil, CelebrationUntil;
    }

    /// Pure presentation math. Reuses the caller's buffer; never samples global time or allocates per frame.
    public static class FrontierPose
    {
        public static void Sample(FrontierRigDefinition rig, FrontierMotionState state, float time, float offset, FrontierBonePose[] pose, bool rest=false)
        {
            bool animate=!rest&&!state.ReducedMotion;
            float phase=time+offset, hurt=Mathf.Clamp01(state.Hurt), growth=Mathf.Clamp01(state.Growth);
            float recoil=animate&&time<state.AttackUntil?Mathf.Sin(Mathf.Clamp01((state.AttackUntil-time)/.28f)*Mathf.PI):0;
            float flinch=animate&&time<state.HitUntil?Mathf.Sin(Mathf.Clamp01((state.HitUntil-time)/.16f)*Mathf.PI):0;
            float greeting=animate&&time<state.GreetingUntil?Mathf.Sin(Mathf.Clamp01((state.GreetingUntil-time)/1.1f)*Mathf.PI*2):0;
            float cheer=animate&&time<state.CelebrationUntil?Mathf.Abs(Mathf.Sin(Mathf.Clamp01((state.CelebrationUntil-time)/1.6f)*Mathf.PI*3)):0;
            float idle=animate&&!state.Moving&&hurt<.6f?Mathf.Sin(phase*(rig.Id=="ember"?1.7f:.7f)):0;
            float period=rig.Id=="ember"?3.6f:rig.Id=="pale"?5.6f:rig.Id=="skitter"?3.1f:rig.Id=="loam"?6.0f:4.8f;
            float blinkTime=Mathf.Repeat(phase+1.7f,period);
            float blink=animate&&blinkTime<.16f?1-Mathf.Sin(blinkTime/.16f*Mathf.PI)*.96f:1;
            float speed=rig.Id=="hollow"?3.2f:rig.Id=="skitter"?12:
                rig.Id=="breaker"||rig.Id=="bulwark"||rig.Id=="sunder"?5:9;
            for(int i=0;i<rig.Bones.Length;i++)
            {
                var def=rig.Bones[i];var p=new FrontierBonePose { Position=rig.LocalPosition(i), Rotation=Quaternion.identity, Scale=Vector3.one };
                float swing=animate?Mathf.Sin(phase*speed+def.Phase):0;
                switch(def.Role)
                {
                    case FrontierBoneRole.Root:
                        float breath=animate?Mathf.Sin(phase*2.4f)*.018f:0;
                        float hover=animate&&rig.Id=="pale"?Mathf.Sin(phase*1.5f)*.035f:
                            rig.Id=="drift"?.24f+Mathf.Sin(phase*2.1f)*.06f:0;
                        float burrow=animate&&rig.Id=="delver"&&state.Moving?-.13f:0;
                        float bounce=animate&&state.Moving&&rig.Id=="ember"?Mathf.Abs(Mathf.Sin(phase*speed))*.022f:0;
                        p.Position+=Vector3.up*(hover+burrow+cheer*.07f+bounce+(rig.Id=="skitter"?Mathf.Abs(greeting)*.035f:0));
                        float g=1+growth*.14f;p.Scale=new Vector3(g,g*(1+breath-recoil*.055f),g);
                        p.Rotation=Quaternion.Euler(0,0,-hurt*9+recoil*4-flinch*3);break;
                    case FrontierBoneRole.Head:
                        p.Position+=Vector3.up*(greeting*(rig.Id=="loam"?.045f:.015f));
                        p.Scale=Vector3.one*(1+growth*.16f);
                        p.Rotation=Quaternion.Euler(idle*3+greeting*7,
                            Mathf.Clamp(state.LookYaw,-24,24)*(rig.Id=="hollow"?.35f:1)+idle*(rig.Id=="ember"?6:3),recoil*9+greeting*5-hurt*(rig.Id=="loam"?12:0));break;
                    case FrontierBoneRole.Eye: p.Scale=new Vector3(1,blink,1);break;
                    case FrontierBoneRole.Leg:
                        p.Rotation=rig.Id=="skitter"?Quaternion.Euler(0,state.Moving?swing*12:idle*def.Side*1.5f,state.Moving?swing*7:0)
                            :Quaternion.Euler(0,0,state.Moving?swing*(rig.Id=="hollow"?9:18):0);break;
                    case FrontierBoneRole.Knee:
                        float bend=state.Moving?Mathf.Max(0,-swing)*18:0;
                        p.Rotation=rig.Id=="skitter"?Quaternion.Euler(bend*def.Side,0,0):Quaternion.Euler(0,0,bend);break;
                    case FrontierBoneRole.Arm:
                        p.Rotation=Quaternion.Euler(greeting*def.Side*24,0,(state.Moving?-swing*12:idle*4)+cheer*12);break;
                    case FrontierBoneRole.Neck:
                        p.Rotation=Quaternion.Euler(0,Mathf.Clamp(state.LookYaw,-24,24)*.65f+idle*2,recoil*6+greeting*4-hurt*12);break;
                    case FrontierBoneRole.Wing:
                        float flap=animate?Mathf.Sin(phase*2.2f):0;
                        p.Rotation=Quaternion.Euler(-def.Side*(flap*12+hurt*12)+(def.Side>0?greeting*16:0),0,idle*2);break;
                    case FrontierBoneRole.WingTip:
                        float tip=animate?Mathf.Sin(phase*2.2f-.5f)*7:0;
                        p.Rotation=Quaternion.Euler(-def.Side*(tip+hurt*10),0,0);break;
                    case FrontierBoneRole.Segment:
                        float wave=animate?Mathf.Sin(phase*(state.Moving?3.5f:1.1f)-def.Phase)*(state.Moving?8:1.3f):0;
                        // Both sockets ride middle; it deforms even though it is not the rig root.
                        p.Rotation=Quaternion.Euler(0,wave+hurt*def.Phase*12+greeting*2,animate?Mathf.Sin(phase*2-def.Phase)*.8f:0);break;
                }
                pose[i]=p;
            }
        }
    }
}
