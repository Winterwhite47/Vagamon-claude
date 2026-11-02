using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public enum FeatureKind
    {
        Obstacle = 0,
        Gap = 1,
        CollapseZone = 2,
        MistBank = 3,
        DucklingNest = 4,
        Pickup = 5,
        Checkpoint = 6,
        Ramp = 7,
        /// <summary>Camera zone: wide scenic framing (T4).</summary>
        Vista = 8,
        /// <summary>Camera zone: covered, intimate framing (T4).</summary>
        Shelter = 9,
    }

    /// <summary>One authored or generated feature. Positions are metres from chunk start.</summary>
    [Serializable]
    public struct ChunkFeature
    {
        public FeatureKind kind;
        public double start;
        public double length;
        /// <summary>Obstacle height in metres.</summary>
        public float height;
        /// <summary>Mist density 0..1 (MistBank).</summary>
        public float density;
        /// <summary>PickupId for pickups, group id for nests, art variant otherwise.</summary>
        public int variant;
        public bool optional;
        public bool requiresUpgrade;
        /// <summary>CollapseZone: distance before start where the warning begins.</summary>
        public double warningLead;

        public double End => start + length;

        public bool IsHazard => kind == FeatureKind.Obstacle || kind == FeatureKind.Gap || kind == FeatureKind.CollapseZone;

        public bool IsRequiredJump => IsHazard && !optional;

        public static ChunkFeature Make(FeatureKind kind, double start, double length)
        {
            var f = new ChunkFeature();
            f.kind = kind;
            f.start = start;
            f.length = length;
            return f;
        }
    }

    [Serializable]
    public struct SurfaceSpan
    {
        public double start;
        public double length;
        public SurfaceId surface;

        public SurfaceSpan(double start, double length, SurfaceId surface)
        {
            this.start = start;
            this.length = length;
            this.surface = surface;
        }

        public double End => start + length;
    }

    /// <summary>Concrete chunk: constant slope, surface spans, features.</summary>
    public sealed class Chunk : IChunk
    {
        private readonly List<ChunkFeature> _features = new List<ChunkFeature>();
        private readonly List<SurfaceSpan> _surfaces = new List<SurfaceSpan>();

        public Chunk(string id, BiomeId biome, int difficulty, double length, float slopeRadians)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("empty id", nameof(id));
            if (!(length > 0d)) throw new ArgumentOutOfRangeException(nameof(length));
            Id = id;
            Biome = biome;
            Difficulty = difficulty;
            Length = length;
            SlopeRadians = slopeRadians;
        }

        public string Id { get; }
        public BiomeId Biome { get; }
        public int Difficulty { get; }
        public double Length { get; }
        public float SlopeRadians { get; }
        public double StartDistance { get; private set; }
        public double EndDistance => StartDistance + Length;
        public IReadOnlyList<ChunkFeature> Features => _features;
        public IReadOnlyList<SurfaceSpan> Surfaces => _surfaces;

        public void AddFeature(ChunkFeature f)
        {
            _features.Add(f);
        }

        public void SetFeature(int index, ChunkFeature f)
        {
            _features[index] = f;
        }

        public void AddSurface(SurfaceSpan s)
        {
            _surfaces.Add(s);
        }

        /// <summary>Lays chunks end to end from distance zero.</summary>
        public static void Assemble(IReadOnlyList<Chunk> chunks)
        {
            double cursor = 0d;
            foreach (Chunk c in chunks)
            {
                c.StartDistance = cursor;
                cursor += c.Length;
            }
        }
    }
}
