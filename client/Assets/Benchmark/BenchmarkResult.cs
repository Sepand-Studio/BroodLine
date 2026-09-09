using System.Globalization;

namespace Broodline.Benchmark
{
    public struct BenchmarkResult
    {
        public SyntheticCreatureSpec Spec;
        public int EntityCount;
        public double CpuP95Ms;      // FrameTimingManager, real CPU work
        public double GpuP95Ms;      // FrameTimingManager, real GPU work
        public double WallP95Ms;     // Time.unscaledDeltaTime, kept for reference
        public long PeakMemoryBytes;

        /// One frame at 60 fps is 1000/60 = 16.667 ms, not 16.6. A hardcoded 16.6
        /// makes a flawless 60 fps read as failure — which is what the first
        /// device run reported.
        public const double Frame60Ms = 1000.0 / 60.0;
        public const double Frame30Ms = 1000.0 / 30.0;

        public static string CsvHeader =>
            "triangles,bones,materials,entities,cpu_p95_ms,gpu_p95_ms,wall_p95_ms,peak_mb,holds_60,holds_30,under_600mb";

        public bool Holds60 => System.Math.Max(CpuP95Ms, GpuP95Ms) <= Frame60Ms;
        public bool Holds30 => System.Math.Max(CpuP95Ms, GpuP95Ms) <= Frame30Ms;
        public bool UnderMemoryCeiling => PeakMemoryBytes <= 600L * 1024 * 1024;

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                Spec.Triangles.ToString(c),
                Spec.Bones.ToString(c),
                Spec.Materials.ToString(c),
                EntityCount.ToString(c),
                CpuP95Ms.ToString("F2", c),
                GpuP95Ms.ToString("F2", c),
                WallP95Ms.ToString("F2", c),
                (PeakMemoryBytes / (1024.0 * 1024.0)).ToString("F1", c),
                Holds60 ? "1" : "0",
                Holds30 ? "1" : "0",
                UnderMemoryCeiling ? "1" : "0");
        }
    }
}
