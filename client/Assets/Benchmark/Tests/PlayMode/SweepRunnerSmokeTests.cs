using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Broodline.Benchmark;

public class SweepRunnerSmokeTests
{
    /// Proves the coroutine SweepRunner.Start() wraps actually runs to
    /// completion and writes a well-formed CSV — nothing until now has ever
    /// entered Play mode to exercise it. Deliberately tiny grid: this is a
    /// smoke test, not a benchmark, so it must stay fast.
    [UnityTest]
    public IEnumerator RunSweep_TinyGrid_WritesWellFormedCsv()
    {
        var go = new GameObject("SweepRunnerSmokeTest");
        // Keep the GameObject inactive so Unity's own scheduler never invokes
        // the private Start() coroutine (which would kick off the full,
        // expensive default sweep). RunSweep is driven manually below and
        // does not depend on the component being active/enabled.
        go.SetActive(false);
        var runner = go.AddComponent<SweepRunner>();

        var outputPath = Path.Combine(Application.temporaryCachePath, "smoke.csv");

        // RunSweep logs an error when FrameTimingManager returns no usable
        // timings, which a 5-frame editor run may well do. That error is the
        // point of the guard on device; here it is noise, and an unexpected
        // LogError would fail a test whose subject is the CSV's shape.
        LogAssert.ignoreFailingMessages = true;

        yield return runner.RunSweep(
            triangleSteps: new[] { 300 },
            boneSteps: new[] { 4 },
            materialSteps: new[] { 1 },
            entityCount: 4,
            warmupFrames: 2,
            measureFrames: 5,
            outputPath: outputPath);

        Object.Destroy(go);
        LogAssert.ignoreFailingMessages = false;

        Assert.IsTrue(File.Exists(outputPath), "RunSweep must write the output CSV file");
        var lines = File.ReadAllLines(outputPath);

        Assert.IsTrue(lines.Length > 0, "output file must not be empty");
        Assert.IsTrue(lines[0].StartsWith("# device="), "first line must be the device provenance line");

        int headerIndex = System.Array.IndexOf(lines, BenchmarkResult.CsvHeader);
        Assert.GreaterOrEqual(headerIndex, 0, "CSV header line must be present");

        int dataRows = lines.Length - headerIndex - 1;
        Assert.AreEqual(1, dataRows,
            "one triangle/bone/material combination must produce exactly one data row");
    }
}
