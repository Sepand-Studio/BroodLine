using System.Globalization;

namespace Broodline.Benchmark
{
    public struct BenchmarkResult
    {
        public SyntheticCreatureSpec Spec;
        public int EntityCount;
        public double MedianMs;
        public double P95Ms;
        public long PeakMemoryBytes;

        public static string CsvHeader =>
            "triangles,bones,materials,entities,median_ms,p95_ms,peak_mb,holds_60,holds_30,under_600mb";

        public bool Holds60 => P95Ms <= 16.6;
        public bool Holds30 => P95Ms <= 33.3;
        public bool UnderMemoryCeiling => PeakMemoryBytes <= 600L * 1024 * 1024;

        public string ToCsvRow()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",",
                Spec.Triangles.ToString(c),
                Spec.Bones.ToString(c),
                Spec.Materials.ToString(c),
                EntityCount.ToString(c),
                MedianMs.ToString("F2", c),
                P95Ms.ToString("F2", c),
                (PeakMemoryBytes / (1024.0 * 1024.0)).ToString("F1", c),
                Holds60 ? "1" : "0",
                Holds30 ? "1" : "0",
                UnderMemoryCeiling ? "1" : "0");
        }
    }
}
