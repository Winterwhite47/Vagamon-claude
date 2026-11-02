using System.Collections;
using System.IO;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    /// <summary>T6 measurement: runs the benchmark, writes Logs/scarf_benchmark.txt, checks the result is usable.</summary>
    public sealed class ScarfBenchmarkTests
    {
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator Benchmark_MeasuresBoth_AndChooses()
        {
            var s = new ClothSettings();
            var go = new GameObject("ScarfBenchmark");
            ScarfBenchmark bench = go.AddComponent<ScarfBenchmark>();
            ScarfBenchmarkResult result = default;
            bool finished = false;
            yield return bench.Run(s, r =>
            {
                result = r;
                finished = true;
            });
            Object.Destroy(go);
            Assert.IsTrue(finished);

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/scarf_benchmark.txt", result.ToLog());
            TestContext.WriteLine(result.ToLog());

            Assert.Greater(result.BaselineFrameMs, 0d);
            Assert.IsFalse(double.IsNaN(result.VerletMsPerScarf));
            Assert.IsFalse(double.IsNaN(result.ClothMsPerScarf));
            Assert.Greater(result.VerletSolverMsPerScarf, 0d);
            Assert.LessOrEqual(result.VerletSolverMsPerScarf, s.perScarfBudgetMs, "verlet solver over budget");
        }
    }
}
