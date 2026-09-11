using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Broodline.View;

namespace Broodline.Benchmark
{
    /// Attach to an empty GameObject in Scenes/Benchmark.unity and build to device.
    public class SweepRunner : MonoBehaviour
    {
        // The scene serializes these, so the two must be kept in step; a changed
        // default alone does nothing to a scene that already stores its own.
        [SerializeField] int[] triangleSteps = { 400, 1500, 6000, 10000, 16000 };
        [SerializeField] int[] boneSteps = { 12, 24, 48, 80 };
        [SerializeField] int[] materialSteps = { 1, 2 };
        [SerializeField] int warmupFrames = 60;
        [SerializeField] int measureFrames = 300;

        IEnumerator Start()
        {
            yield return RunSweep(triangleSteps, boneSteps, materialSteps,
                WaveBenchmark.Wave44Composition, warmupFrames, measureFrames,
                Path.Combine(Application.persistentDataPath, "entity-budget.csv"));
        }

        public IEnumerator RunSweep(int[] triangleSteps, int[] boneSteps, int[] materialSteps,
                                    int entityCount, int warmupFrames, int measureFrames,
                                    string outputPath)
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            // A sweep runs for tens of minutes on a device nobody is touching,
            // which is precisely how long it takes iOS to lock the screen. A lock
            // suspends the app: the run appears alive and never finishes, and a
            // lock landing inside a combination's 300 measured frames puts a
            // multi-second stall in the middle of its p95.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var sb = new StringBuilder();
            // Provenance first. A budget is only meaningful against the device it
            // was measured on, and these files will outlive the memory of which
            // phone produced them.
            sb.AppendLine("# device=" + SystemInfo.deviceModel);
            sb.AppendLine("# gpu=" + SystemInfo.graphicsDeviceName);
            sb.AppendLine("# memory_mb=" + SystemInfo.systemMemorySize);
            sb.AppendLine("# os=" + SystemInfo.operatingSystem);
            sb.AppendLine("# unity=" + Application.unityVersion);
            sb.AppendLine("# entities=" + entityCount);
            sb.AppendLine(BenchmarkResult.CsvHeader);

            int invalidRows = 0;
            double mainMin = double.MaxValue, mainMax = 0;
            double gpuMin = double.MaxValue, gpuMax = 0;

            foreach (var tris in triangleSteps)
            foreach (var bones in boneSteps)
            foreach (var mats in materialSteps)
            {
                var spec = new SyntheticCreatureSpec { Triangles = tris, Bones = bones, Materials = mats };
                var spawned = WaveBenchmark.Spawn(spec, entityCount);

                for (int i = 0; i < warmupFrames; i++) yield return null;

                var samples = new List<FrameSample>(measureFrames);
                long peak = 0;
                var ft = new FrameTiming[1];
                for (int i = 0; i < measureFrames; i++)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, ft) > 0)
                    {
                        samples.Add(new FrameSample
                        {
                            MainThreadMs = ft[0].cpuMainThreadFrameTime,
                            PresentWaitMs = ft[0].cpuMainThreadPresentWaitTime,
                            RenderThreadMs = ft[0].cpuRenderThreadFrameTime,
                            GpuMs = ft[0].gpuFrameTime,
                            WallMs = Time.unscaledDeltaTime * 1000.0
                        });
                    }
                    long used = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    if (used > peak) peak = used;
                }

                var result = WaveBenchmark.Summarise(samples, spec, entityCount, peak);
                sb.AppendLine(result.ToCsvRow());

                // Written after every combination, not once at the end. A run
                // that dies at combination 39 of 40 otherwise leaves nothing at
                // all, and there is no way to see progress on a device that is
                // deliberately unplugged from the console.
                File.WriteAllText(outputPath, sb.ToString());

                // A sweep whose timings never arrived writes a CSV that looks
                // exactly like a passing one. Say so on the row and in the log,
                // loudly enough that nobody quotes the budget by mistake.
                if (!result.TimingValid)
                {
                    invalidRows++;
                    Debug.LogError("[sweep] INVALID TIMING samples=" + result.TimingSamples +
                                   " main=" + result.MainThreadP95Ms +
                                   " wait=" + result.PresentWaitP95Ms +
                                   " gpu=" + result.GpuP95Ms +
                                   " — FrameTimingManager reported nothing usable. " +
                                   "This row is not a measurement.");
                }

                if (result.MainThreadP95Ms < mainMin) mainMin = result.MainThreadP95Ms;
                if (result.MainThreadP95Ms > mainMax) mainMax = result.MainThreadP95Ms;
                if (result.GpuP95Ms < gpuMin) gpuMin = result.GpuP95Ms;
                if (result.GpuP95Ms > gpuMax) gpuMax = result.GpuP95Ms;

                Debug.Log("[sweep] " + result.ToCsvRow());

                WaveBenchmark.Despawn(spawned);
                yield return Resources.UnloadUnusedAssets();
                System.GC.Collect();
                yield return null;
            }

            // The failure that cost two device runs: a timing field that reports
            // the frame-rate cap rather than the work is perfectly steady while
            // the load it supposedly measures grows fifteenfold. If the GPU
            // series responded to the sweep and the CPU series did not, the CPU
            // number is not a measurement of this workload.
            if (gpuMax > gpuMin * 2 && mainMax - mainMin < 1.0)
                Debug.LogError("[sweep] CPU SERIES DOES NOT RESPOND TO LOAD — main thread spans " +
                               mainMin.ToString("F2") + "-" + mainMax.ToString("F2") +
                               " ms while gpu spans " + gpuMin.ToString("F2") + "-" +
                               gpuMax.ToString("F2") + " ms. Suspect the cap is being measured, " +
                               "not the cost. The budget from this run is NOT valid.");

            if (invalidRows > 0)
                Debug.LogError("[sweep] " + invalidRows + " row(s) carried no usable timing. " +
                               "The budget from this run is NOT valid.");
            Debug.Log("[sweep] COMPLETE -> " + outputPath);
        }
    }
}
