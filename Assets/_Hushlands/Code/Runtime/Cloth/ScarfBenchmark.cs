using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    public struct ScarfBenchmarkResult
    {
        public int Scarves;
        public double BaselineFrameMs;
        public double VerletFrameMs;
        public double ClothFrameMs;
        /// <summary>Main-thread solver time per scarf measured directly with a stopwatch.</summary>
        public double VerletSolverMsPerScarf;
        /// <summary>(frame median with scarves - baseline) / scarves; covers worker-thread cost on the critical path.</summary>
        public double VerletMsPerScarf;
        public double ClothMsPerScarf;
        public ScarfChoice Choice;
        public string Device;

        public string ToLog()
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Hushlands T6 scarf benchmark");
            sb.AppendLine("device=" + Device);
            sb.AppendLine("scarves=" + Scarves);
            sb.AppendLine("baseline_frame_ms=" + BaselineFrameMs.ToString("F4", c));
            sb.AppendLine("verlet_frame_ms=" + VerletFrameMs.ToString("F4", c));
            sb.AppendLine("cloth_frame_ms=" + ClothFrameMs.ToString("F4", c));
            sb.AppendLine("verlet_solver_ms_per_scarf=" + VerletSolverMsPerScarf.ToString("F5", c));
            sb.AppendLine("verlet_ms_per_scarf=" + VerletMsPerScarf.ToString("F5", c));
            sb.AppendLine("cloth_ms_per_scarf=" + ClothMsPerScarf.ToString("F5", c));
            sb.AppendLine("choice=" + Choice.Method);
            sb.AppendLine("within_budget=" + Choice.WithinBudget);
            sb.AppendLine("reason=" + Choice.Reason);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Measures verlet vs Unity Cloth scarf cost with moving anchors and picks a method with ScarfMethodSelector.
    /// Frame medians are taken with vSync off and no frame cap; per-scarf cost = (median - baseline) / count.
    /// </summary>
    public sealed class ScarfBenchmark : MonoBehaviour
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<VerletScarf> _verlet = new List<VerletScarf>();
        private readonly List<Transform> _anchors = new List<Transform>();
        private double _solverTicks;
        private bool _tickVerlet;
        private float _time;

        public IEnumerator Run(ClothSettings s, Action<ScarfBenchmarkResult> done)
        {
            int vsync = QualitySettings.vSyncCount;
            int cap = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var result = new ScarfBenchmarkResult
            {
                Scarves = s.benchmarkScarves,
                Device = SystemInfo.processorType + " / " + SystemInfo.graphicsDeviceName,
            };
            try
            {
                var frames = new List<double>(s.benchmarkSampleFrames);

                yield return Measure(s, frames);
                result.BaselineFrameMs = Median(frames);

                SpawnVerlet(s);
                _solverTicks = 0d;
                yield return Measure(s, frames);
                result.VerletFrameMs = Median(frames);
                result.VerletSolverMsPerScarf = _solverTicks * 1000d / Stopwatch.Frequency /
                                                Math.Max(1, s.benchmarkSampleFrames + s.benchmarkWarmupFrames) / s.benchmarkScarves;
                Clear();

                SpawnCloth(s);
                yield return Measure(s, frames);
                result.ClothFrameMs = Median(frames);
                Clear();

                result.VerletMsPerScarf = Math.Max(0d, (result.VerletFrameMs - result.BaselineFrameMs) / s.benchmarkScarves);
                result.ClothMsPerScarf = Math.Max(0d, (result.ClothFrameMs - result.BaselineFrameMs) / s.benchmarkScarves);
                // Verlet runs on the main thread, so its direct solver time is a floor on its cost.
                result.VerletMsPerScarf = Math.Max(result.VerletMsPerScarf, result.VerletSolverMsPerScarf);
                result.Choice = ScarfMethodSelector.Choose(result.VerletMsPerScarf, result.ClothMsPerScarf, s);
            }
            finally
            {
                Clear();
                QualitySettings.vSyncCount = vsync;
                Application.targetFrameRate = cap;
            }
            done?.Invoke(result);
        }

        private IEnumerator Measure(ClothSettings s, List<double> frames)
        {
            frames.Clear();
            for (int i = 0; i < s.benchmarkWarmupFrames; i++) yield return null;
            for (int i = 0; i < s.benchmarkSampleFrames; i++)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000d);
            }
        }

        private Transform MakeAnchor(int i)
        {
            var a = new GameObject("BenchAnchor_" + i).transform;
            a.SetParent(transform, false);
            a.localPosition = new Vector3(i * 2f, 0f, 0f);
            _spawned.Add(a.gameObject);
            _anchors.Add(a);
            return a;
        }

        private void SpawnVerlet(ClothSettings s)
        {
            for (int i = 0; i < s.benchmarkScarves; i++)
            {
                Transform a = MakeAnchor(i);
                VerletScarf v = a.gameObject.AddComponent<VerletScarf>();
                v.Initialize(s, -Physics.gravity.y, a, null, null, false);
                _verlet.Add(v);
            }
            _tickVerlet = true;
        }

        private void SpawnCloth(ClothSettings s)
        {
            for (int i = 0; i < s.benchmarkScarves; i++)
            {
                Transform a = MakeAnchor(i);
                var go = new GameObject("BenchCloth_" + i);
                go.transform.SetParent(a, false);
                go.AddComponent<SkinnedMeshRenderer>();
                var body = new GameObject("Body").AddComponent<SphereCollider>();
                body.transform.SetParent(a, false);
                body.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                body.radius = s.bodyRadius;
                go.AddComponent<UnityClothScarf>().Initialize(s, body);
            }
        }

        private void Update()
        {
            // Anchors ride a fast arc so both methods simulate real motion.
            _time += Time.unscaledDeltaTime;
            for (int i = 0; i < _anchors.Count; i++)
            {
                Transform a = _anchors[i];
                if (a == null) continue;
                Vector3 p = a.localPosition;
                p.y = Mathf.Sin(_time * 3f + i) * 1.5f;
                p.z = Mathf.Cos(_time * 2f + i) * 0.5f;
                a.localPosition = p;
            }
        }

        private void LateUpdate()
        {
            if (!_tickVerlet) return;
            long start = Stopwatch.GetTimestamp();
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _verlet.Count; i++) _verlet[i].Tick(dt);
            _solverTicks += Stopwatch.GetTimestamp() - start;
        }

        private void Clear()
        {
            _tickVerlet = false;
            foreach (GameObject g in _spawned)
            {
                if (g != null) Destroy(g);
            }
            _spawned.Clear();
            _verlet.Clear();
            _anchors.Clear();
        }

        private static double Median(List<double> v)
        {
            if (v.Count == 0) return 0d;
            var c = new List<double>(v);
            c.Sort();
            return c[c.Count / 2];
        }
    }
}
