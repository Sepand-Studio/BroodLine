using System;
using Broodline.Sim.Combat;

namespace Broodline.Frontier
{
    /// The proof uses only combat traits the current engine implements.
    public static class FrontierFormation
    {
        /// Moving onto an occupied stone swaps companions, preserving a legal formation.
        public static bool Place(int[] pockets, int companion, int pocket)
        {
            Create(pockets); // Validate before mutating any placement.
            if (companion < 0 || companion >= pockets.Length) throw new ArgumentOutOfRangeException(nameof(companion));
            if (pocket < 0 || pocket >= Lane.Defile().PocketCount) throw new ArgumentOutOfRangeException(nameof(pocket));
            int previous = pockets[companion];
            if (previous == pocket) return false;
            int occupant = Array.IndexOf(pockets, pocket);
            if (occupant >= 0) pockets[occupant] = previous;
            pockets[companion] = pocket;
            return true;
        }

        public static CreatureSpec[] Create(int[] pockets)
        {
            if (pockets == null || pockets.Length != 3) throw new ArgumentException("Three pockets are required.");
            var species = new[] { Species.Vetch, Species.Ember, Species.Pale };
            var first = new[] { Trait.Taunt, Trait.Splash, Trait.Chill };
            var result = new CreatureSpec[3];
            int pocketCount = Lane.Defile().PocketCount;
            for (int i = 0; i < 3; i++)
            {
                if (pockets[i] < 0 || pockets[i] >= pocketCount)
                    throw new ArgumentOutOfRangeException(nameof(pockets));
                for (int j = 0; j < i; j++)
                    if (pockets[i] == pockets[j]) throw new ArgumentException("Companions need distinct pockets.");
                result[i] = new CreatureSpec { Species = species[i], Pocket = pockets[i], Trait1 = first[i], Tier1 = 1,
                    Trait2 = i == 2 ? Trait.Taunt : Trait.Carapace, Tier2 = 1, Instinct = Instinct.Vanguard };
            }
            return result;
        }
    }
}
