using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Benchmark
{
    public static class WaveBenchmark
    {
        /// 60 Skirmishers + 39 Brood children + 5 creatures.
        /// specs/broodline_combat_engine.md section 9.
        public const int Wave44Composition = 104;

        public static GameObject[] Spawn(SyntheticCreatureSpec spec, int count)
        {
            var made = new GameObject[count];
            int perRow = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (int i = 0; i < count; i++)
            {
                var go = SyntheticCreature.Build(spec);
                go.name = "entity_" + i;
                go.transform.position = new Vector3(
                    (i % perRow) * 1.2f - perRow * 0.6f,
                    0f,
                    (i / perRow) * 1.2f);
                made[i] = go;
            }
            return made;
        }

        /// Destroys spawned entities AND the materials they own.
        /// `SyntheticCreature.Build` calls `new Material(shader)` per entity, and
        /// destroying a GameObject does not reclaim materials it created. Over a
        /// sweep of ~30 combinations at 104 entities this accumulates thousands of
        /// native Material allocations, which inflates the very peak-memory number
        /// the proof exists to measure.
        public static void Despawn(GameObject[] spawned)
        {
            if (spawned == null) return;
            foreach (var go in spawned)
            {
                if (go == null) continue;
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null)
                {
                    foreach (var m in smr.sharedMaterials)
                        if (m != null) DestroySafely(m);
                    if (smr.sharedMesh != null) DestroySafely(smr.sharedMesh);
                }
                DestroySafely(go);
            }
        }

        /// Unity requires DestroyImmediate in EditMode — a deferred Destroy would
        /// leave the object alive for the rest of the test — and forbids it at
        /// runtime, where Destroy defers to end of frame. Task 5's SweepRunner is
        /// a MonoBehaviour, so Despawn must work correctly in both contexts.
        static void DestroySafely(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        /// Frame times in milliseconds, sorted ascending, from a captured run.
        public static BenchmarkResult Summarise(
            List<double> frameMs, SyntheticCreatureSpec spec, int entityCount, long peakBytes)
        {
            frameMs.Sort();
            return new BenchmarkResult
            {
                Spec = spec,
                EntityCount = entityCount,
                MedianMs = Percentile(frameMs, 0.50),
                P95Ms = Percentile(frameMs, 0.95),
                PeakMemoryBytes = peakBytes
            };
        }

        static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            int idx = Mathf.Clamp(Mathf.CeilToInt((float)(p * sorted.Count)) - 1, 0, sorted.Count - 1);
            return sorted[idx];
        }
    }
}
