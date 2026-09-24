using System;
using UnityEngine;

namespace Broodline.Frontier
{
    public enum FrontierBoneRole { Root, Head, Eye, Leg, Knee, Arm, Neck, Wing, WingTip, Segment }

    public readonly struct FrontierBoneDefinition
    {
        public readonly string Name;
        public readonly int Parent;
        // Positions in model space; parents always precede children.
        public readonly Vector3 Position;
        public readonly FrontierBoneRole Role;
        public readonly float Side, Phase;
        public FrontierBoneDefinition(string name, int parent, Vector3 position, FrontierBoneRole role, float side = 0, float phase = 0)
        { Name = name; Parent = parent; Position = position; Role = role; Side = side; Phase = phase; }
    }

    public readonly struct FrontierSocketDefinition
    {
        public readonly int Bone;
        public readonly Vector3 Position, Euler;
        public readonly float Scale;
        public FrontierSocketDefinition(int bone, Vector3 position, Vector3 euler, float scale = 1)
        { Bone = bone; Position = position; Euler = euler; Scale = scale; }
    }

    public sealed class FrontierRigDefinition
    {
        public readonly string Id;
        public readonly FrontierBoneDefinition[] Bones;
        public readonly FrontierSocketDefinition Dorsal, Flank, Crown;
        public readonly FrontierSocketDefinition? Kit;
        public readonly Vector3 MotionAllowance;
        public int Head { get; private set; }

        public FrontierRigDefinition(string id, FrontierBoneDefinition[] bones, FrontierSocketDefinition dorsal,
            FrontierSocketDefinition flank, FrontierSocketDefinition crown, Vector3 allowance,
            FrontierSocketDefinition? kit = null)
        {
            Id = id; Bones = bones; Dorsal = dorsal; Flank = flank; Crown = crown; Kit = kit; MotionAllowance = allowance;
            Head = -1;
            var names = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (string.IsNullOrEmpty(bones[i].Name) || !names.Add(bones[i].Name) || bones[i].Parent >= i || bones[i].Parent < -1
                    || (i == 0 ? bones[i].Parent != -1 : bones[i].Parent == -1))
                    throw new ArgumentException("Invalid rig hierarchy: " + id);
                if (bones[i].Role == FrontierBoneRole.Head) Head = i;
            }
            if (Head < 0) throw new ArgumentException("Rig needs a head: " + id);
            foreach (var socket in new[] { dorsal, flank, crown })
                if (socket.Bone < 0 || socket.Bone >= bones.Length || socket.Scale <= 0) throw new ArgumentException("Invalid socket: " + id);
            if (kit.HasValue && (kit.Value.Bone < 0 || kit.Value.Bone >= bones.Length || kit.Value.Scale <= 0))
                throw new ArgumentException("Invalid kit socket: " + id);
        }

        public Vector3 LocalPosition(int index) => Bones[index].Position - (Bones[index].Parent < 0 ? Vector3.zero : Bones[Bones[index].Parent].Position);
        public Matrix4x4[] BindPoses()
        {
            var world = new Matrix4x4[Bones.Length]; var bind = new Matrix4x4[Bones.Length];
            for (int i = 0; i < Bones.Length; i++)
            {
                var local = Matrix4x4.TRS(LocalPosition(i), Quaternion.identity, Vector3.one);
                world[i] = Bones[i].Parent < 0 ? local : world[Bones[i].Parent] * local;
                bind[i] = world[i].inverse;
            }
            return bind;
        }

        public static readonly string[] Companions = { "vetch", "ember", "pale", "skitter", "hollow", "loam" };
        public static FrontierRigDefinition For(string id)
        {
            switch (id)
            {
                case "ember": return FrontierEmber.Rig;
                case "pale": return FrontierPale.Rig;
                case "skitter": return FrontierSkitter.Rig;
                case "hollow": return FrontierHollow.Rig;
                case "loam": return FrontierLoam.Rig;
                case "vetch": return VetchRig();
                case "courser": case "skirmisher": case "lash": case "drift":
                case "breaker": case "bulwark": case "delver": case "brood": case "sunder": return RaiderRig(id);
                default: throw new ArgumentException("No Frontier rig for " + id);
            }
        }

        static FrontierRigDefinition VetchRig()
        {
            var bones = new FrontierBoneDefinition[8];
            for (int i = 0; i < 6; i++) bones[i] = new FrontierBoneDefinition(i == 0 ? "root" : i == 1 ? "head" : "leg-" + i,
                i == 0 ? -1 : 0, FrontierVetch.BindPositions[i], i == 0 ? FrontierBoneRole.Root : i == 1 ? FrontierBoneRole.Head : FrontierBoneRole.Leg,
                i % 2 == 0 ? 1 : -1, i % 2 * Mathf.PI);
            for (int i = 6; i < 8; i++) bones[i] = new FrontierBoneDefinition("eye-" + i, 1,
                FrontierVetch.EyeCenter + Vector3.forward * (i == 6 ? -1 : 1) * FrontierVetch.EyeSpacing, FrontierBoneRole.Eye, i == 6 ? -1 : 1);
            return new FrontierRigDefinition("vetch", bones, new FrontierSocketDefinition(0, FrontierVetch.DorsalPosition, Vector3.zero),
                new FrontierSocketDefinition(0, FrontierVetch.FlankPosition, new Vector3(-90,0,0)),
                new FrontierSocketDefinition(1, bones[1].Position + new Vector3(.06f,.18f,0), Vector3.zero), new Vector3(.06f,.10f,.06f));
        }

        static FrontierRigDefinition RaiderRig(string id)
        {
            float s = FrontierRaiders.Scale(id);
            bool hauler = id == "breaker" || id == "bulwark" || id == "sunder";
            bool lifter = id == "lash" || id == "drift";
            bool segment = id == "delver" || id == "brood";
            Vector3[] p;
            FrontierBoneRole[] roles;
            int[] parents;
            if (hauler)
            {
                p = new[] { Vector3.zero, new Vector3(.58f,.68f,0), new Vector3(.38f,.42f,.30f),
                    new Vector3(.38f,.42f,-.30f), new Vector3(-.48f,.42f,.30f), new Vector3(-.48f,.42f,-.30f),
                    new Vector3(.72f,.72f,0) };
                roles = new[] { FrontierBoneRole.Root, FrontierBoneRole.Head, FrontierBoneRole.Leg,
                    FrontierBoneRole.Leg, FrontierBoneRole.Leg, FrontierBoneRole.Leg, FrontierBoneRole.Arm };
                parents = new[] { -1, 0, 0, 0, 0, 0, 0 };
            }
            else if (lifter)
            {
                p = new[] { Vector3.zero, new Vector3(.31f,1.18f,0), new Vector3(-.13f,.58f,.21f),
                    new Vector3(-.13f,.58f,-.21f), new Vector3(-.29f,1.02f,.18f), new Vector3(-.29f,1.02f,-.18f),
                    new Vector3(.10f,.86f,.24f), new Vector3(.10f,.86f,-.24f) };
                roles = new[] { FrontierBoneRole.Root, FrontierBoneRole.Head, FrontierBoneRole.Leg,
                    FrontierBoneRole.Leg, FrontierBoneRole.Wing, FrontierBoneRole.Wing, FrontierBoneRole.Arm, FrontierBoneRole.Arm };
                parents = new[] { -1, 0, 0, 0, 0, 0, 0, 0 };
            }
            else if (segment)
            {
                p = new[] { Vector3.zero, new Vector3(.59f,.35f,0), new Vector3(.27f,.32f,0),
                    new Vector3(-.04f,.32f,0), new Vector3(-.35f,.32f,0), new Vector3(-.66f,.32f,0),
                    new Vector3(.57f,.29f,.24f), new Vector3(.57f,.29f,-.24f) };
                roles = new[] { FrontierBoneRole.Root, FrontierBoneRole.Head, FrontierBoneRole.Segment,
                    FrontierBoneRole.Segment, FrontierBoneRole.Segment, FrontierBoneRole.Segment,
                    FrontierBoneRole.Arm, FrontierBoneRole.Arm };
                parents = new[] { -1, 0, 0, 2, 3, 4, 1, 1 };
            }
            else
            {
                p = new[] { Vector3.zero, new Vector3(.5f,.47f,0), new Vector3(.32f,.24f,.32f),
                    new Vector3(.32f,.24f,-.32f), new Vector3(-.34f,.24f,.32f), new Vector3(-.34f,.24f,-.32f) };
                roles = new[] { FrontierBoneRole.Root, FrontierBoneRole.Head, FrontierBoneRole.Leg,
                    FrontierBoneRole.Leg, FrontierBoneRole.Leg, FrontierBoneRole.Leg };
                parents = new[] { -1, 0, 0, 0, 0, 0 };
            }
            for (int i = 1; i < p.Length; i++) p[i] *= s;
            var bones = new FrontierBoneDefinition[p.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                string name = i == 0 ? "root" : i == 1 ? "head" : i == 6 && hauler ? "kit" : roles[i].ToString().ToLowerInvariant() + "-" + i;
                bones[i] = new FrontierBoneDefinition(name, parents[i], p[i], roles[i], i % 2 == 0 ? 1 : -1, i % 2 * Mathf.PI);
            }
            var kitBone = hauler ? 6 : lifter ? 4 : segment ? 2 : 0;
            return new FrontierRigDefinition(id, bones,
                new FrontierSocketDefinition(0, new Vector3(-.13f,.84f,0)*s, Vector3.zero,.8f),
                new FrontierSocketDefinition(0, new Vector3(-.07f,.47f,-.49f)*s, new Vector3(-90,0,0),.8f),
                new FrontierSocketDefinition(1, p[1]+new Vector3(.06f,.18f,0)*s, Vector3.zero),
                new Vector3(.14f,.18f,.18f)*s,
                new FrontierSocketDefinition(kitBone, p[kitBone], Vector3.zero, 1f));
        }
    }
}
