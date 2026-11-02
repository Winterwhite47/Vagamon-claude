using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Absolute-distance interval.</summary>
    public readonly struct Span
    {
        public readonly double Start;
        public readonly double End;

        public Span(double start, double end)
        {
            Start = start;
            End = end;
        }

        public bool Overlaps(Span o)
        {
            return Start < o.End && o.Start < End;
        }

        public bool Contains(double d)
        {
            return d >= Start && d <= End;
        }

        public Span Expand(double by)
        {
            return new Span(Start - by, End + by);
        }
    }

    /// <summary>Shared zone geometry for generator, checker and tests. All values absolute.</summary>
    public sealed class HazardZone
    {
        public HazardZone(IChunk chunk, int featureIndex, ChunkFeature feature, JumpModel jump, FairnessSettings fair)
        {
            Chunk = chunk;
            FeatureIndex = featureIndex;
            Feature = feature;
            double tan = JumpModel.Tan(chunk.SlopeRadians);
            double start = chunk.StartDistance + feature.start;
            Body = new Span(start, start + feature.length);
            Takeoff = new Span(start - jump.TakeoffZoneLength(fair.takeoffWindowMaxSeconds), start);
            double far = start + jump.FarthestLanding(tan, fair.takeoffWindowMinSeconds);
            if (far < Body.End) far = Body.End;
            Landing = new Span(Body.End, far + fair.minSafeLandingLength);
            Whole = new Span(Takeoff.Start, Landing.End);
            Reveal = new Span(start - fair.obstacleRevealSeconds * jump.MaxSpeed, Body.End);
        }

        public IChunk Chunk { get; }
        public int FeatureIndex { get; }
        public ChunkFeature Feature { get; }
        public Span Body { get; }
        public Span Takeoff { get; }
        public Span Landing { get; }
        public Span Whole { get; }
        public Span Reveal { get; }

        public static List<HazardZone> Collect(IReadOnlyList<IChunk> stretch, JumpModel jump, FairnessSettings fair)
        {
            var zones = new List<HazardZone>();
            foreach (IChunk c in stretch)
            {
                for (int i = 0; i < c.Features.Count; i++)
                {
                    ChunkFeature f = c.Features[i];
                    if (f.IsHazard) zones.Add(new HazardZone(c, i, f, jump, fair));
                }
            }
            zones.Sort((a, b) => a.Body.Start.CompareTo(b.Body.Start));
            return zones;
        }
    }
}
