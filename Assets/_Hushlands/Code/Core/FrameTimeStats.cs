using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Engine-free frame-time accumulator used by captures and budget checks.</summary>
    public sealed class FrameTimeStats : IFrameTimeProbe
    {
        private readonly List<double> _samples;

        public FrameTimeStats(int capacity)
        {
            _samples = new List<double>(capacity);
        }

        public int SampleCount => _samples.Count;

        public double AverageMs
        {
            get
            {
                if (_samples.Count == 0) return 0d;
                double sum = 0d;
                for (int i = 0; i < _samples.Count; i++) sum += _samples[i];
                return sum / _samples.Count;
            }
        }

        public void Record(double frameMs)
        {
            if (double.IsNaN(frameMs) || frameMs < 0d) throw new ArgumentOutOfRangeException(nameof(frameMs));
            _samples.Add(frameMs);
        }

        /// <summary>Nearest-rank percentile, p in [0,100].</summary>
        public double Percentile(double p)
        {
            if (_samples.Count == 0) return 0d;
            if (p < 0d || p > 100d) throw new ArgumentOutOfRangeException(nameof(p));
            var sorted = new List<double>(_samples);
            sorted.Sort();
            int rank = (int)Math.Ceiling(p / 100d * sorted.Count);
            int index = Math.Max(0, Math.Min(sorted.Count - 1, rank - 1));
            return sorted[index];
        }

        public bool WithinBudget(double budgetMs, double percentile)
        {
            return Percentile(percentile) <= budgetMs;
        }
    }
}
