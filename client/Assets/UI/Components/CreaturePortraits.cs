using System;
using UnityEngine;

namespace Broodline.UI.Components
{
    /// UI-facing portrait seam. Game supplies a renderer; UI keeps no Art
    /// assembly reference. A miss returns null and calls ready after rendering.
    public interface ICreaturePortraitSource
    {
        event Action<Texture2D> Evicted;

        Texture2D Request(string species, string first, string second, float growth01,
            Action<Texture2D> ready);
    }

    public static class CreaturePortraits
    {
        public static ICreaturePortraitSource Source { get; set; }
    }
}
