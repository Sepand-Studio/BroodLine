using UnityEngine;

namespace Broodline.Creatures
{
    /// Body + two parts + growth, one GameObject. Phase 9 design §3.5.
    public static class CreatureAssembler
    {
        public const int StudioLayer = 6;

        public static GameObject Build(CreatureLook look)
        {
            if (look == null) return Missing("null");

            var isRaider = !string.IsNullOrEmpty(look.RaiderType);
            var recipe = isRaider ? RaiderRecipes.For(look.RaiderType) : SpeciesRecipes.For(look.Species);
            var prefab = isRaider ? CreatureLibrary.RaiderPrefab(look.RaiderType) : CreatureLibrary.BodyPrefab(look.Species);
            if (recipe == null || prefab == null) return Missing(isRaider ? look.RaiderType : look.Species);

            var go = Object.Instantiate(prefab);
            go.name = recipe.Id;
            if (go.GetComponent<CreatureMotion>() == null) go.AddComponent<CreatureMotion>();

            if (!isRaider)
            {
                Mount(go, Sockets.Dorsal, look.Trait1);
                Mount(go, Sockets.Flank, look.Trait2);
            }
            ApplyGrowth(go, recipe, look.Growth01);
            return go;
        }

        static void Mount(GameObject creature, string socketName, string trait)
        {
            var socket = creature.transform.Find(socketName);
            if (socket == null) return;
            var prefab = CreatureLibrary.PartPrefab(trait);
            if (prefab == null) return;   // a trait this build has no part for: the socket stays empty, the card still names it
            var part = Object.Instantiate(prefab, socket, false);
            part.name = PartRecipes.For(trait).Id;
        }

        /// bible 10.2 rule 3, as per-bone scale over one rig. A bone's growth
        /// is the mean growth of the primitives it moves; the root grows by
        /// the recipe's smallest so the profile holds while the extremities
        /// swell.
        public static void ApplyGrowth(GameObject creature, BodyRecipe recipe, float growth01)
        {
            growth01 = Mathf.Clamp01(growth01);
            foreach (var bone in recipe.Bones)
            {
                float sum = 0f; int n = 0;
                foreach (var p in recipe.Primitives) if (p.Bone == bone.Name) { sum += p.Growth; n++; }
                float g = n == 0 ? 0f : sum / n;
                if (bone.Parent == null)
                {
                    // The root's scale is CreatureMotion's to write every frame
                    // (breath and flinch); growth goes in as its multiplier.
                    var motion = creature.GetComponent<CreatureMotion>();
                    if (motion != null) motion.GrowthScale = 1f + g * growth01;
                    continue;
                }
                var t = FindDeep(creature.transform, bone.Name);
                if (t != null) t.localScale = Vector3.one * (1f + g * growth01);
            }
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static GameObject Missing(string id)
        {
            Debug.LogError("[creatures] no body for '" + id + "' - run generate-creatures.sh, or the roster names a species this build does not carry");
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "missing-" + id;
            go.GetComponent<Renderer>().material.color = Color.magenta;
            return go;
        }
    }
}
