using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>One biome's stretch of the run.</summary>
    public readonly struct JourneySegment
    {
        public readonly BiomeId Biome;
        public readonly double Start;
        public readonly double End;

        public JourneySegment(BiomeId biome, double start, double end)
        {
            Biome = biome;
            Start = start;
            End = end;
        }
    }

    /// <summary>Where along the run two biomes blend, and by how much (t = 0 all From, 1 all To).</summary>
    public readonly struct BiomeBlend
    {
        public readonly BiomeId From;
        public readonly BiomeId To;
        public readonly double T;

        public BiomeBlend(BiomeId from, BiomeId to, double t)
        {
            From = from;
            To = to;
            T = t;
        }

        /// <summary>The biome that dominates here (camera profile, audio snapshot).</summary>
        public BiomeId Dominant => T < 0.5 ? From : To;
    }

    /// <summary>
    /// Biome segments of a run built from its chunks. Blends are centred on each border and span transitionMeters,
    /// smoothstepped so terrain, light and mist change continuously (no cuts).
    /// </summary>
    public sealed class JourneyPlan
    {
        private readonly List<JourneySegment> _segments = new List<JourneySegment>();
        private readonly double _transition;

        public JourneyPlan(IReadOnlyList<IChunk> stretch, double transitionMeters)
        {
            if (stretch == null || stretch.Count == 0) throw new ArgumentException("empty stretch", nameof(stretch));
            _transition = Math.Max(0d, transitionMeters);
            BiomeId current = stretch[0].Biome;
            double start = stretch[0].StartDistance;
            foreach (IChunk c in stretch)
            {
                if (c.Biome == current) continue;
                _segments.Add(new JourneySegment(current, start, c.StartDistance));
                current = c.Biome;
                start = c.StartDistance;
            }
            IChunk last = stretch[stretch.Count - 1];
            _segments.Add(new JourneySegment(current, start, last.StartDistance + last.Length));
        }

        public IReadOnlyList<JourneySegment> Segments => _segments;

        public JourneySegment SegmentAt(double x)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (x < _segments[i].End) return _segments[i];
            }
            return _segments[_segments.Count - 1];
        }

        public BiomeBlend BlendAt(double x)
        {
            for (int i = 0; i + 1 < _segments.Count; i++)
            {
                double border = _segments[i].End;
                double half = _transition * 0.5;
                if (x < border - half) return new BiomeBlend(_segments[i].Biome, _segments[i].Biome, 0d);
                if (x <= border + half && half > 0d)
                {
                    double u = (x - (border - half)) / _transition;
                    return new BiomeBlend(_segments[i].Biome, _segments[i + 1].Biome, u * u * (3d - 2d * u));
                }
            }
            BiomeId lastBiome = _segments[_segments.Count - 1].Biome;
            return new BiomeBlend(lastBiome, lastBiome, 0d);
        }
    }
}
