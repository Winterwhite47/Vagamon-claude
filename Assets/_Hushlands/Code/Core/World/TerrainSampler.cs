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
        private readonly IReadOnlyList<IChunk> _stretch;
        private readonly JourneyPlan _plan;

        /// <summary>
        /// Journey terrain: each biome's backdrop, blended across the borders of the plan. The corridor and front
        /// rules hold for every biome, so they hold for any blend of two.
        /// </summary>
        public TerrainSampler(ISlopeSpline slope, IReadOnlyList<IChunk> stretch, LookSettings settings, JourneyPlan plan, int seed)
            : this(slope, stretch, settings, FirstLook(settings, plan), seed)
        {
            _plan = plan;
        }

        private static BiomeLook FirstLook(LookSettings settings, JourneyPlan plan)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            return settings.Get(plan.Segments[0].Biome);
        }

        public TerrainSampler(ISlopeSpline slope, IReadOnlyList<IChunk> stretch, LookSettings settings, BiomeLook look, int seed)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _look = look;
            _seed = seed;
            _stretch = stretch;
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

        public bool HasWater
        {
            get
            {
                if (_plan == null) return _look.waterMaxDepth > 0f;
                foreach (JourneySegment seg in _plan.Segments)
                {
                    if (_s.Get(seg.Biome).waterMaxDepth > 0f) return true;
                }
                return false;
            }
        }

        /// <summary>Water surface for the chunk containing x: below that chunk's lowest track point by waterDrop.</summary>
        public double WaterLevel(double x)
        {
            if (_plan != null) return JourneyWaterLevel(x);
            if (_stretch == null || _stretch.Count == 0) return Track(x) - _look.waterDrop;
            IChunk c = _stretch[_stretch.Count - 1];
            foreach (IChunk ch in _stretch)
            {
                if (x < ch.StartDistance + ch.Length)
                {
                    c = ch;
                    break;
                }
            }
            return _slope.HeightAt(c.StartDistance + c.Length) - _look.waterDrop;
        }

        public bool InWater(double z)
        {
            return HasWater && z >= _look.waterMinDepth && z <= _look.waterMaxDepth;
        }

        /// <summary>True where a water plane lies: the chunk at x belongs to a lake biome and z is inside its band.</summary>
        public bool InWater(double x, double z)
        {
            if (_plan == null) return InWater(z);
            BiomeLook look = _s.Get(ChunkAt(x).Biome);
            return look.waterMaxDepth > 0f && z >= look.waterMinDepth && z <= look.waterMaxDepth;
        }

        private IChunk ChunkAt(double x)
        {
            foreach (IChunk ch in _stretch)
            {
                if (x < ch.StartDistance + ch.Length) return ch;
            }
            return _stretch[_stretch.Count - 1];
        }

        /// <summary>
        /// Water level of the lake chunk at x, or of the nearest lake chunk when x is on the dry side of a border
        /// (so the lake bed fades in along the blend without a step).
        /// </summary>
        private double JourneyWaterLevel(double x)
        {
            IChunk best = null;
            double bestDistance = double.MaxValue;
            foreach (IChunk ch in _stretch)
            {
                if (_s.Get(ch.Biome).waterMaxDepth <= 0f) continue;
                double end = ch.StartDistance + ch.Length;
                double d = x < ch.StartDistance ? ch.StartDistance - x : (x < end ? 0d : x - end + 1e-9);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = ch;
                }
            }
            if (best == null) return Track(x);
            return _slope.HeightAt(best.StartDistance + best.Length) - _s.Get(best.Biome).waterDrop;
        }

        private double JourneyHeight(double x, double z)
        {
            BiomeBlend b = _plan.BlendAt(x);
            BiomeLook from = _s.Get(b.From);
            double h = DryHeight(x, z, from);
            if (b.From == b.To || b.T <= 0d) return Sink(h, x, z, from, 1d);
            BiomeLook to = _s.Get(b.To);
            h += (DryHeight(x, z, to) - h) * b.T;
            h = Sink(h, x, z, from, 1d - b.T);
            return Sink(h, x, z, to, b.T);
        }

        /// <summary>Lowers the ground to the lake bed inside a lake biome's band, by that biome's share of the blend.</summary>
        private double Sink(double h, double x, double z, BiomeLook look, double weight)
        {
            if (weight <= 0d || look.waterMaxDepth <= 0f || z < look.waterMinDepth || z > look.waterMaxDepth) return h;
            double bed = Math.Min(h, WaterLevel(x) - look.waterBed);
            return h + (bed - h) * weight;
        }

        public double Height(double x, double z)
        {
            if (_plan != null) return JourneyHeight(x, z);
            double h = DryHeight(x, z);
            // Lake band behind the track: ground drops below the water surface (never in the corridor or in front).
            if (InWater(z)) h = Math.Min(h, WaterLevel(x) - _look.waterBed);
            return h;
        }

        private double DryHeight(double x, double z)
        {
            return DryHeight(x, z, _look);
        }

        private double DryHeight(double x, double z, BiomeLook look)
        {
            double track = Track(x);
            double az = Math.Abs(z);
            if (az <= _s.corridorHalfWidth) return InGap(x) ? track - _s.gapDepth : track;

            double w = Smooth((az - _s.corridorHalfWidth) / _s.corridorBlend);
            if (z < 0d)
            {
                // Front: gentle undulation that only ever goes down from the track line.
                double dip = Math.Abs(ValueNoise.Fbm(x / look.detailWavelength, z / look.detailWavelength, _seed + 7)) * look.detailAmplitude;
                return track - w * (_s.frontDrop + dip);
            }
            double hills = (ValueNoise.Fbm(x / look.hillWavelength, z / look.hillWavelength, _seed) * 0.5 + 0.5) * look.hillAmplitude;
            if (look.terraceStep > 0f) hills = Terrace(hills, look.terraceStep, look.terraceRiser);
            double detail = ValueNoise.Fbm(x / look.detailWavelength, z / look.detailWavelength, _seed + 13) * look.detailAmplitude;
            return track + w * (hills + detail);
        }

        /// <summary>Flat treads with smooth short risers; continuous and never higher than the next step.</summary>
        public static double Terrace(double h, double step, double riser)
        {
            double level = Math.Floor(h / step);
            double frac = h / step - level;
            double t = (frac - (1d - riser)) / riser;
            return step * (level + Smooth(t));
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
        private readonly JourneyPlan _plan;
        private readonly BiomeSettings _biomes;

        /// <summary>Journey mist: each biome's base density, blended across the borders of the plan.</summary>
        public MistField(IReadOnlyList<IChunk> stretch, BiomeSettings biomes, JourneyPlan plan)
            : this(stretch, 0f)
        {
            _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

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

        /// <summary>Biome base density at x (without mist banks).</summary>
        public float BaseAt(double x)
        {
            if (_plan == null) return _base;
            BiomeBlend b = _plan.BlendAt(x);
            float from = _biomes.Get(b.From).baseMistDensity;
            if (b.From == b.To) return from;
            return from + (_biomes.Get(b.To).baseMistDensity - from) * (float)b.T;
        }

        public float DensityAt(double x)
        {
            float d = BaseAt(x);
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
            PlaceRange(s, biome, terrain, 0d, length, seed, list);
            return list;
        }

        /// <summary>Journey scatter: each biome's props over its own segment (the last one runs on to length).</summary>
        public static System.Collections.Generic.List<PropPlacement> Place(LookSettings s, JourneyPlan plan, TerrainSampler terrain,
            double length, int seed)
        {
            var list = new System.Collections.Generic.List<PropPlacement>();
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                JourneySegment seg = plan.Segments[i];
                double end = i == plan.Segments.Count - 1 ? System.Math.Max(seg.End, length) : seg.End;
                PlaceRange(s, seg.Biome, terrain, seg.Start, end, seed, list);
            }
            return list;
        }

        private static void PlaceRange(LookSettings s, BiomeId biome, TerrainSampler terrain, double from, double to, int seed,
            System.Collections.Generic.List<PropPlacement> list)
        {
            double length = to - from;
            var rng = new Rng(Rng.Mix(seed, 991 + (int)biome));
            foreach (PropScatter p in s.scatters)
            {
                if (p.biome != biome) continue;
                int count = (int)System.Math.Floor(length * p.density);
                for (int i = 0; i < count; i++)
                {
                    double x = from + rng.Range(0d, length);
                    double z = rng.Range(p.minDepth, p.maxDepth);
                    float scale = (float)rng.Range(p.minScale, p.maxScale);
                    int stack = rng.RangeInclusive(p.minStack, p.maxStack);
                    list.Add(new PropPlacement(p.name, p.material, x, z, terrain.Height(x, z), scale, stack, p.shape));
                }
            }
        }
    }
}
