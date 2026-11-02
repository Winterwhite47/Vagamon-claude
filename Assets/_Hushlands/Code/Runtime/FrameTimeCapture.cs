using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Records frame times after a warmup, writes CSV + summary + 4K screenshot.
    /// Activated by command-line flag "-hushCapture" or by enabling in the inspector.
    /// </summary>
    public sealed class FrameTimeCapture : MonoBehaviour
    {
        public const string CommandLineFlag = "-hushCapture";

        [SerializeField] private ProjectTargetsConfig config;
        [SerializeField] private bool captureInEditor;

        private FrameTimeStats _stats;
        private float _elapsed;
        private bool _active;
        private bool _done;

        public FrameTimeStats Stats => _stats;
        public bool Done => _done;
        public string LastSummaryPath { get; private set; }

        public void Configure(ProjectTargetsConfig targets, bool forceActive)
        {
            config = targets;
            captureInEditor = forceActive;
        }

        private void Start()
        {
            if (config == null) config = ProjectTargetsConfig.Load();
            _active = captureInEditor || HasFlag(CommandLineFlag);
            if (!_active || config == null) return;
            int expected = Mathf.CeilToInt(config.captureSeconds * config.targetFrameRate * 2f);
            _stats = new FrameTimeStats(expected);
        }

        private void Update()
        {
            if (!_active || _done || config == null) return;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < config.captureWarmupSeconds) return;
            _stats.Record(Time.unscaledDeltaTime * 1000.0);
            if (_elapsed >= config.captureWarmupSeconds + config.captureSeconds) Finish();
        }

        private void Finish()
        {
            _done = true;
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string root = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", config.captureFolder);
            Directory.CreateDirectory(root);
            string summaryPath = Path.Combine(root, "frametime_" + stamp + ".txt");
            string screenshotPath = Path.Combine(root, "frame_" + stamp + ".png");

            double p = config.budgetPercentile;
            var sb = new StringBuilder();
            sb.AppendLine("Hushlands frame-time capture");
            sb.AppendLine("utc=" + stamp);
            sb.AppendLine("resolution=" + Screen.width + "x" + Screen.height);
            sb.AppendLine("gpu=" + SystemInfo.graphicsDeviceName);
            sb.AppendLine("cpu=" + SystemInfo.processorType);
            sb.AppendLine("samples=" + _stats.SampleCount);
            sb.AppendLine("avg_ms=" + _stats.AverageMs.ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("p50_ms=" + _stats.Percentile(50).ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("p" + p.ToString("F0", CultureInfo.InvariantCulture) + "_ms=" + _stats.Percentile(p).ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("p99_ms=" + _stats.Percentile(99).ToString("F3", CultureInfo.InvariantCulture));
            sb.AppendLine("budget_ms=" + config.frameBudgetMs.ToString("F1", CultureInfo.InvariantCulture));
            sb.AppendLine("within_budget=" + _stats.WithinBudget(config.frameBudgetMs, p));
            File.WriteAllText(summaryPath, sb.ToString());
            LastSummaryPath = summaryPath;

            int superSize = Mathf.Max(1, Mathf.CeilToInt((float)config.targetWidth / Mathf.Max(1, Screen.width)));
            ScreenCapture.CaptureScreenshot(screenshotPath, superSize);
            Debug.Log("[Hushlands] Capture written: " + summaryPath);

            if (HasFlag(CommandLineFlag) && !Application.isEditor) Invoke(nameof(QuitAfterCapture), 1f);
        }

        private void QuitAfterCapture()
        {
            Application.Quit(_stats.WithinBudget(config.frameBudgetMs, config.budgetPercentile) ? 0 : 2);
        }

        private static bool HasFlag(string flag)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
