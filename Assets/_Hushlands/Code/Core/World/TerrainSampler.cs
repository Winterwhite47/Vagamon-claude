using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>
    /// Visual terrain around the gameplay plane (x = distance, z = depth; camera at negative z).
    /// Rules: inside the corridor the ground equals the slope line exactly; at gaps it dips into a ravine;
    /// in front of the track (z &lt; 0) it never rises above the track (never hides the rider);
    /// behind the track rolling hills rise as a backdrop. Deterministic per seed.
    /// </summary>
    public sealed class TerrainSampler
    {
        private readonly ISlopeSpline _slope;
        private readonly LookSettings _s;
        private readonly BiomeLook _look;
        private readonly List<Span> _gaps = new List<Span>();
        private readonly int _seed;

        public TerrainSampler(ISlopeSpline slope, IReadOnlyList<IChunk> stretch, LookSettings settings, BiomeLook look, int seed)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _look = look;
            _seed = seed;
            if (stretch == null) return;
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.Gap || f.kind == FeatureKind.CollapseZone)
                        _gaps.Add(new Span(c.StartDistance + f.start, c.StartDistance + f.End));
                }
            }
        }

        public double Track(double x)
        {
            return _slope.HeightAt(x);
        }

        public bool InGap(double x)
        {
            foreach (Span g in _gaps)
            {
                if (x > g.Start && x < g.End) return true;
            }
            return false;
        }

        public double Height(double x, double z)
        {
            double track = Track(x);
            double az = Math.Abs(z);
            if (az <= _s.corridorHalfWidth) return InGap(x) ? track - _s.gapDepth : track;

            double w = Smooth((az - _s.corridorHalfWidth) / _s.corridorBlend);
            if (z < 0d)
            {
                // Front: gentle undulation that only ever goes down from the track line.
                double dip = Math.Abs(ValueNoise.Fbm(x / _look.detailWavelength, z / _look.detailWavelength, _seed + 7)) * _look.detailAmplitude;
                return track - w * (_s.frontDrop + dip);
            }
            double hills = (ValueNoise.Fbm(x / _look.hillWavelength, z / _look.hillWavelength, _seed) * 0.5 + 0.5) * _look.hillAmplitude;
            double detail = ValueNoise.Fbm(x / _look.detailWavelength, z / _look.detailWavelength, _seed + 13) * _look.detailAmplitude;
            return track + w * (hills + detail);
        }

        private static double Smooth(double t)
        {
            if (t <= 0d) return 0d;
            if (t >= 1d) return 1d;
            return t * t * (3d - 2d * t);
        }
    }

    /// <summary>Deterministic 2D lattice value noise in [-1, 1] with 4-octave fBm.</summary>
    public static class ValueNoise
    {
        public static double Fbm(double x, double y, int seed)
        {
            double sum = 0d;
            double amp = 0.5;
            double norm = 0d;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * Noise(x, y, seed + o * 101);
                norm += amp;
                x *= 2d;
                y *= 2d;
                amp *= 0.5;
            }
            return sum / norm;
        }

        public static double Noise(double x, double y, int seed)
        {
            double fx = Math.Floor(x);
            double fy = Math.Floor(y);
            int ix = (int)fx;
            int iy = (int)fy;
            double tx = x - fx;
            double ty = y - fy;
            double sx = tx * tx * (3d - 2d * tx);
            double sy = ty * ty * (3d - 2d * ty);
            double a = Hash(ix, iy, seed);
            double b = Hash(ix + 1, iy, seed);
            double c = Hash(ix, iy + 1, seed);
            double d = Hash(ix + 1, iy + 1, seed);
            double ab = a + (b - a) * sx;
            double cd = c + (d - c) * sx;
            return ab + (cd - ab) * sy;
        }

        private static double Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / (double)uint.MaxValue * 2d - 1d;
            }
        }
    }

    /// <summary>Mist density along the run (biome base plus content mist banks), for audio low-pass and mist volumes.</summary>
    public sealed class MistField
    {
        private readonly List<KeyValuePair<Span, float>> _banks = new List<KeyValuePair<Span, float>>();
        private readonly float _base;

        public MistField(IReadOnlyList<IChunk> stretch, float baseDensity)
        {
            _base = baseDensity;
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.MistBank)
                        _banks.Add(new KeyValuePair<Span, float>(new Span(c.StartDistance + f.start, c.StartDistance + f.End), f.density));
                }
            }
        }

        public IReadOnlyList<KeyValuePair<Span, float>> Banks => _banks;

        public float DensityAt(double x)
        {
            float d = _base;
            foreach (KeyValuePair<Span, float> b in _banks)
            {
                if (b.Key.Contains(x) && b.Value > d) d = b.Value;
            }
            return d;
        }
    }
}

namespace Hushlands.Core
{
    /// <summary>Visibility of a required jump through fog and mist (Beer-Lambert), used to enforce the mist fairness rule visually.</summary>
    public static class MistVisibility
    {
        public static double MeanFreePath(LookSettings s, float density)
        {
            double d = density < 1e-3f ? 1e-3 : density;
            return s.mistMeanFreePathAtFull / d;
        }

        public static double RevealTransmittance(LookSettings s, BiomeLook look, float densityThreshold, double cameraDistance,
            float fogScale = 1f)
        {
            double global = System.Math.Exp(-cameraDistance / (look.fogMeanFreePath * fogScale));
            double local = System.Math.Exp(-(s.mistVolumeDepth * 0.5) / MeanFreePath(s, densityThreshold));
            return global * local;
        }

        /// <summary>Worst case over tuned biomes at the farthest camera distance of any state (and densest squall fog).</summary>
        public static System.Collections.Generic.List<string> Check(LookSettings s, CameraSettings cam, FairnessSettings fair,
            float fogScale = 1f)
        {
            var errors = new System.Collections.Generic.List<string>();
            foreach (BiomeLook b in s.biomes)
            {
                if (!b.tuned) continue;
                double far = 0d;
                BiomeCameraModifier m = cam.Get(b.biome);
                foreach (CameraStateProfile p in cam.states) far = System.Math.Max(far, p.distance * m.distanceScale);
                double t = RevealTransmittance(s, b, fair.maxMistOverRequiredJump, far, fogScale);
                if (t < s.minRevealTransmittance)
                    errors.Add("look." + b.biome + ": mist hides jumps (transmittance " + t.ToString("F2") + ")");
            }
            return errors;
        }
    }
}

namespace Hushlands.Core
{
    public readonly struct PropPlacement
    {
        public readonly string Name;
        public readonly string Material;
        public readonly double X;
        public readonly double Z;
        public readonly double Y;
        public readonly float Scale;
        public readonly int Stack;
        public readonly string Shape;

        public PropPlacement(string name, string material, double x, double z, double y, float scale, int stack, string shape)
        {
            Shape = shape;
            Name = name;
            Material = material;
            X = x;
            Z = z;
            Y = y;
            Scale = scale;
            Stack = stack;
        }
    }

    /// <summary>Deterministic backdrop scatter: only behind the track (z > 0) and outside the corridor and its blend.</summary>
    public static class PropScatterer
    {
        public static System.Collections.Generic.List<PropPlacement> Place(LookSettings s, BiomeId biome, TerrainSampler terrain,
            double length, int seed)
        {
            var list = new System.Collections.Generic.List<PropPlacement>();
            var rng = new Rng(Rng.Mix(seed, 991 + (int)biome));
            foreach (PropScatter p in s.scatters)
            {
                if (p.biome != biome) continue;
                int count = (int)System.Math.Floor(length * p.density);
                for (int i = 0; i < count; i++)
                {
                    double x = rng.Range(0d, length);
                    double z = rng.Range(p.minDepth, p.maxDepth);
                    float scale = (float)rng.Range(p.minScale, p.maxScale);
                    int stack = rng.RangeInclusive(p.minStack, p.maxStack);
                    list.Add(new PropPlacement(p.name, p.material, x, z, terrain.Height(x, z), scale, stack, p.shape));
                }
            }
            return list;
        }
    }
}
