using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    /// A creature is a recipe, not a model. Phase 9 design §3.3: a list of
    /// blended primitives, each tagged with the bone that moves it and how
    /// much it grows from runt to apex; sockets as named transforms per
    /// rig_proof.md section 3; the mesh is generated from this and committed.
    ///
    /// CONVENTIONS, so every recipe agrees with every other:
    ///   +X is forward (the snout), +Y is up, +Z is the creature's left.
    ///   A body stands on y = 0 and is about one unit long.
    ///   A part is authored at the origin with +Y pointing out of the body
    ///   surface and +X forward; the socket's transform places and sizes it.
    public enum PrimitiveKind { Sphere, Capsule, Box }

    public struct Primitive
    {
        public PrimitiveKind Kind;
        public Vector3 A;       // centre (Sphere, Box) or first end (Capsule)
        public Vector3 B;       // second end (Capsule)
        public Vector3 Half;    // half extents (Box)
        public float Radius;    // Sphere, Capsule
        public string Bone;
        public float Growth;    // 0 = does not grow; 1 = doubles from runt to apex

        public static Primitive Sphere(Vector3 c, float r, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Sphere, A = c, Radius = r, Bone = bone, Growth = growth };
        public static Primitive Capsule(Vector3 a, Vector3 b, float r, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Capsule, A = a, B = b, Radius = r, Bone = bone, Growth = growth };
        public static Primitive Box(Vector3 c, Vector3 half, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Box, A = c, Half = half, Bone = bone, Growth = growth };
    }

    public sealed class BoneDef
    {
        public string Name;
        public string Parent;   // null on the one root
        public Vector3 Position;
    }

    public sealed class SocketDef
    {
        public string Name;
        public Vector3 Position;
        public Vector3 Euler;
        public float Scale = 1f;
    }

    public static class Sockets
    {
        public const string Dorsal = "sk_dorsal";
        public const string Flank = "sk_flank";
        public const string Crown = "sk_crown";
        public const string Kit = "sk_kit";
    }

    public sealed class BodyRecipe
    {
        public string Id;
        public Primitive[] Primitives;
        public BoneDef[] Bones;
        public SocketDef[] Sockets;
        public float Blend = 0.22f;
        public int Grid = 24;
        public float Padding = 0.25f;
        public bool Raider;
    }

    public sealed class PartRecipe
    {
        public string Id;
        public Primitive[] Primitives;
        public float Blend = 0.06f;
        public int Grid = 16;
        public float Padding = 0.15f;
        public Color Base;
        public Color Under;
    }

    /// What the assembler needs to build one creature. Strings, because this
    /// assembly references no engine enum and the roster carries strings.
    public sealed class CreatureLook
    {
        public string Species;
        public string Trait1;
        public string Trait2;
        public float Growth01;
        public string RaiderType;   // set instead of Species for a raider
    }

    public static class CreaturePaths
    {
        public const string MeshDir = "Creatures/Meshes";
        public const string MaterialDir = "Creatures/Materials";
        public const string BodyDir = "Creatures/Bodies";
        public const string PartDir = "Creatures/Parts";
        public const string RaiderDir = "Creatures/Raiders";
        public static string Body(string id) => BodyDir + "/" + id;
        public static string Part(string id) => PartDir + "/" + id;
        public static string Raider(string id) => RaiderDir + "/" + id;
    }
}
