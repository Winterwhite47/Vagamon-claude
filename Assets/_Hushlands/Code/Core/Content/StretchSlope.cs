using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Piecewise-constant-slope spline built from assembled chunks. Height is continuous.</summary>
    public sealed class StretchSlope : ISlopeSpline
    {
        private readonly IReadOnlyList<IChunk> _chunks;
        private readonly double[] _startHeight;

        public StretchSlope(IReadOnlyList<IChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0) throw new ArgumentException("empty stretch", nameof(chunks));
            _chunks = chunks;
            _startHeight = new double[chunks.Count];
            double h = 0d;
            for (int i = 0; i < chunks.Count; i++)
            {
                _startHeight[i] = h;
                h -= chunks[i].Length * Math.Tan(chunks[i].SlopeRadians);
            }
            IChunk last = chunks[chunks.Count - 1];
            Length = last.StartDistance + last.Length;
        }

        public double Length { get; }

        public int IndexAt(double distance)
        {
            int lo = 0;
            int hi = _chunks.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_chunks[mid].StartDistance <= distance) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public float HeightAt(double distance)
        {
            int i = IndexAt(distance);
            IChunk c = _chunks[i];
            return (float)(_startHeight[i] - (distance - c.StartDistance) * Math.Tan(c.SlopeRadians));
        }

        public float AngleAt(double distance)
        {
            return _chunks[IndexAt(distance)].SlopeRadians;
        }

        public SurfaceId SurfaceAt(double distance)
        {
            IChunk c = _chunks[IndexAt(distance)];
            double local = distance - c.StartDistance;
            IReadOnlyList<SurfaceSpan> spans = c.Surfaces;
            for (int i = 0; i < spans.Count; i++)
            {
                if (local >= spans[i].start && local < spans[i].End) return spans[i].surface;
            }
            return spans.Count > 0 ? spans[spans.Count - 1].surface : SurfaceId.Grass;
        }
    }
}
