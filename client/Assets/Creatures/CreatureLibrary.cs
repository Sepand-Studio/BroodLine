using UnityEngine;

namespace Broodline.Creatures
{
    /// The committed prefabs, by lowercase id. Null when there is none;
    /// callers decide how loud to be.
    public static class CreatureLibrary
    {
        public static GameObject BodyPrefab(string species) => Load(CreaturePaths.Body(Lower(species)));
        public static GameObject PartPrefab(string trait) => Load(CreaturePaths.Part(Lower(trait)));
        public static GameObject RaiderPrefab(string type) => Load(CreaturePaths.Raider(Lower(type)));

        static string Lower(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
        static GameObject Load(string path) => Resources.Load<GameObject>(path);
    }
}
