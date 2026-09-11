using System.Globalization;
using Broodline.View;

namespace Broodline.Benchmark
{
    /// One frame's timings, straight from FrameTimingManager.
    ///
    /// Every component is kept rather than reduced to a single number on the
    /// spot. Two device runs have now been lost to a timing field that measured
    /// the frame-rate cap instead of the work, and in both cases the CSV alone
    /// could not say which — the components had already been thrown away. A row
    /// that carries its parts is diagnosable without another build and another
    /// trip to the device.
    public struct FrameSample
    {
        public double MainThreadMs;   // cpuMainThreadFrameTime — includes the present wait
        public double PresentWaitMs;  // cpuMainThreadPresentWaitTime — the cap's sleep
        public double RenderThreadMs; // cpuRenderThreadFrameTime
        public double GpuMs;          // gpuFrameTime
        public double WallMs;         // Time.unscaledDeltaTime

        /// MainThreadMs is ALREADY exclusive of the present wait — do not
        /// subtract PresentWaitMs from it. The iPad Air 4 run settles this at two
        /// operating points: at the 60 fps cap, main 6.0 + wait 12.4 accounts for
        /// a 16.75 ms frame; at 16000 triangles, main 7.05 + wait 27.4 accounts
        /// for a 33.5 ms one. The two are siblings that partition the frame, not
        /// a total and a part of it. Subtracting produced a clamped 0.00 on every
        /// row of that sweep.
        ///
        /// `cpuFrameTime` is the field that does include the wait, which is why
        /// it reads flat at ~16.7 ms whatever the load — the cap, not the cost.
    }

    public struct BenchmarkResult
    {
        public SyntheticCreatureSpec Spec;
        public int EntityCount;

        public double MainThreadP95Ms;   // the CPU cost; excludes the present wait
        public double PresentWaitP95Ms;  // the rest of the frame — sleep at the cap
        public double RenderThreadP95Ms; // diagnostic only, see CostP95Ms
        public double GpuP95Ms;
        public double WallP95Ms;         // kept for reference; always reports the cap
        public long PeakMemoryBytes;
        public int TimingSamples;

        /// One frame at 60 fps is 1000/60 = 16.667 ms, not 16.6. A hardcoded 16.6
        /// makes a flawless 60 fps read as failure.
        public const double Frame60Ms = 1000.0 / 60.0;
        public const double Frame30Ms = 1000.0 / 30.0;

        public static string CsvHeader =>
            "triangles,bones,materials,entities," +
            "cost_p95_ms,main_thread_p95_ms,present_wait_p95_ms," +
            "render_thread_p95_ms,gpu_p95_ms,wall_p95_ms," +
            "peak_mb,timing_samples,holds_60,holds_30,under_600mb";

        /// A frame costs whatever its slowest stage costs — main thread and GPU
        /// run concurrently, so the longer one sets the frame rate.
        ///
        /// The render thread is deliberately NOT part of this. Its measured
        /// series alternates between ~1.3 ms and ~13 ms with no coherent relation
        /// to load, and at the 16000-triangle rows it pins to ~16.6 ms — the
        /// frame interval itself — which says it is carrying a wait on the GPU
        /// rather than reporting work. Since that wait is a proxy for GPU time
        /// and GPU time is measured directly, including it would double-count a
        /// number we already have. It stays in the CSV as a diagnostic.
        public double CostP95Ms => MainThreadP95Ms > GpuP95Ms ? MainThreadP95Ms : GpuP95Ms;

        /// FrameTimingManager is platform-dependent and silent when a field is
        /// unpopulated. A zero is missing data, not instant work, and `0 <=
        /// 16.667` would otherwise make every triangle count read as a pass.
        public bool TimingValid =>
            TimingSamples > 0 && MainThreadP95Ms > 0 && GpuP95Ms > 0;

        public bool Holds60 => TimingValid && CostP95Ms <= Frame60Ms;
        public bool Holds30 => TimingValid && CostP95Ms <= Frame30Ms;
        public bool UnderMemoryCeiling => PeakMemoryBytes <= 600L * 1024 * 1024;

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                Spec.Triangles.ToString(c),
                Spec.Bones.ToString(c),
                Spec.Materials.ToString(c),
                EntityCount.ToString(c),
                CostP95Ms.ToString("F2", c),
                MainThreadP95Ms.ToString("F2", c),
                PresentWaitP95Ms.ToString("F2", c),
                RenderThreadP95Ms.ToString("F2", c),
                GpuP95Ms.ToString("F2", c),
                WallP95Ms.ToString("F2", c),
                (PeakMemoryBytes / (1024.0 * 1024.0)).ToString("F1", c),
                TimingSamples.ToString(c),
                Holds60 ? "1" : "0",
                Holds30 ? "1" : "0",
                UnderMemoryCeiling ? "1" : "0");
        }
    }
}
