using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Frontier
{
    public enum FrontierKind { Companion, Raider }

    /// Portrait framing: the three-quarter pose a card or a hero slot shows, and
    /// the room the frame keeps around the body for motion and growth.
    public readonly struct FrontierPortrait
    {
        public readonly float Yaw, Pitch, Padding;
        public FrontierPortrait(float yaw, float pitch, float padding) { Yaw = yaw; Pitch = pitch; Padding = padding; }
    }

    /// THE PRESENTATION-ONLY DEFINITION OF A BODY - Phase 10 Task 2.1, the
    /// approved plan's `CreatureVisualDefinition` as plain C# rather than a
    /// prefab asset, because the art is procedural. Keyed by the same species
    /// and raider ids the sim uses; no server DTO or save format changes. It
    /// names the rig, the builder that authors the mesh, the portrait pose,
    /// the growth range the rig supports and an art revision that portrait
    /// caches key on, so a redesign invalidates every cached picture.
    public sealed class FrontierVisualDefinition
    {
        public readonly string Id;
        public readonly FrontierKind Kind;
        public readonly FrontierRigDefinition Rig;
        public readonly Action<FrontierMesh> Build;
        public readonly FrontierPortrait Portrait;
        public readonly float MinGrowth, MaxGrowth;
        public readonly int ArtRevision;

        public FrontierVisualDefinition(string id, FrontierKind kind, FrontierRigDefinition rig, Action<FrontierMesh> build,
            FrontierPortrait portrait, float minGrowth, float maxGrowth, int artRevision)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            Rig = rig ?? throw new ArgumentNullException(nameof(rig));
            Build = build ?? throw new ArgumentNullException(nameof(build));
            Portrait = portrait; MinGrowth = minGrowth; MaxGrowth = maxGrowth; ArtRevision = artRevision;
            if (rig.Id != id) throw new ArgumentException("rig " + rig.Id + " does not belong to " + id);
        }

        /// A cache key that changes whenever the picture would: body, ordered
        /// traits, growth stage and art revision.
        public string PortraitKey(string first, string second, float growth01)
            => Id + "|" + (first ?? "") + "|" + (second ?? "") + "|" + Mathf.RoundToInt(Mathf.Clamp01(growth01) * 4) + "|" + ArtRevision;
    }

    public static class FrontierVisuals
    {
        static readonly Dictionary<string, FrontierVisualDefinition> ById = Index();

        static Dictionary<string, FrontierVisualDefinition> Index()
        {
            var d = new Dictionary<string, FrontierVisualDefinition>();
            void Add(FrontierVisualDefinition v) => d[v.Id] = v;
            // Revisions invalidate cached portraits whenever authored mesh
            // geometry changes. Vetch remains the accepted revision 02 reference.
            Add(new FrontierVisualDefinition("vetch", FrontierKind.Companion, FrontierRigDefinition.For("vetch"), FrontierVetch.Build, new FrontierPortrait(-32, 12, .12f), 0, 1, 2));
            Add(new FrontierVisualDefinition("ember", FrontierKind.Companion, FrontierEmber.Rig, FrontierEmber.Build, new FrontierPortrait(-30, 8, .14f), 0, 1, 3));
            Add(new FrontierVisualDefinition("pale", FrontierKind.Companion, FrontierPale.Rig, FrontierPale.Build, new FrontierPortrait(-38, 14, .10f), 0, 1, 2));
            Add(new FrontierVisualDefinition("skitter", FrontierKind.Companion, FrontierSkitter.Rig, FrontierSkitter.Build, new FrontierPortrait(-34, 16, .14f), 0, 1, 2));
            Add(new FrontierVisualDefinition("hollow", FrontierKind.Companion, FrontierHollow.Rig, FrontierHollow.Build, new FrontierPortrait(-30, 8, .12f), 0, 1, 2));
            Add(new FrontierVisualDefinition("loam", FrontierKind.Companion, FrontierLoam.Rig, FrontierLoam.Build, new FrontierPortrait(-36, 18, .12f), 0, 1, 3));
            foreach (var raider in FrontierRaiders.Ids)
            {
                var id = raider;
                Add(new FrontierVisualDefinition(id, FrontierKind.Raider, FrontierRigDefinition.For(id), b => FrontierRaiders.Build(b, id), new FrontierPortrait(-30, 10, .12f), 0, 0, 2));
            }
            return d;
        }

        public static FrontierVisualDefinition For(string id)
            => id != null && ById.TryGetValue(id.Trim().ToLowerInvariant(), out var v) ? v : throw new ArgumentException("No Frontier visual definition for " + id);

        public static bool Has(string id) => id != null && ById.ContainsKey(id.Trim().ToLowerInvariant());

        public static IEnumerable<FrontierVisualDefinition> All => ById.Values;
    }
}
