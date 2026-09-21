using System;
using System.Collections.Generic;

namespace Broodline.Creatures
{
    public static class RaiderRecipes
    {
        // Courser, Lash, Skirmisher: Task 15.
        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe>();

        public static BodyRecipe For(string raiderType)
        {
            if (string.IsNullOrWhiteSpace(raiderType)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, raiderType.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
