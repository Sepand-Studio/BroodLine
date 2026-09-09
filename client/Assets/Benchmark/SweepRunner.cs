using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Broodline.Benchmark
{
    /// Attach to an empty GameObject in Scenes/Benchmark.unity and build to device.
    public class SweepRunner : MonoBehaviour
    {
        [SerializeField] int[] triangleSteps = { 400, 800, 1500, 3000, 6000 };
        [SerializeField] int[] boneSteps = { 12, 24, 48 };
        [SerializeField] int[] materialSteps = { 1, 2 };
        [SerializeField] int warmupFrames = 60;
        [SerializeField] int measureFrames = 300;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            var sb = new StringBuilder();
            // Provenance first. A budget is only meaningful against the device it
            // was measured on, and these files will outlive the memory of which
            // phone produced them.
            sb.AppendLine("# device=" + SystemInfo.deviceModel);
            sb.AppendLine("# gpu=" + SystemInfo.graphicsDeviceName);
            sb.AppendLine("# memory_mb=" + SystemInfo.systemMemorySize);
            sb.AppendLine("# os=" + SystemInfo.operatingSystem);
            sb.AppendLine("# unity=" + Application.unityVersion);
            sb.AppendLine("# entities=" + WaveBenchmark.Wave44Composition);
            sb.AppendLine(BenchmarkResult.CsvHeader);

            foreach (var tris in triangleSteps)
            foreach (var bones in boneSteps)
            foreach (var mats in materialSteps)
            {
                var spec = new SyntheticCreatureSpec { Triangles = tris, Bones = bones, Materials = mats };
                var spawned = WaveBenchmark.Spawn(spec, WaveBenchmark.Wave44Composition);

                for (int i = 0; i < warmupFrames; i++) yield return null;

                var frames = new List<double>(measureFrames);
                long peak = 0;
                for (int i = 0; i < measureFrames; i++)
                {
                    yield return null;
                    frames.Add(Time.unscaledDeltaTime * 1000.0);
                    long used = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    if (used > peak) peak = used;
                }

                var result = WaveBenchmark.Summarise(frames, spec, WaveBenchmark.Wave44Composition, peak);
                sb.AppendLine(result.ToCsvRow());
                Debug.Log("[sweep] " + result.ToCsvRow());

                WaveBenchmark.Despawn(spawned);
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
                yield return null;
            }

            var path = Path.Combine(Application.persistentDataPath, "entity-budget.csv");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[sweep] COMPLETE -> " + path);
        }
    }
}
