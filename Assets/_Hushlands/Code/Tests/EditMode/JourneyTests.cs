using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>The full run: every biome in order on one track, fair on every vehicle, blended without cuts.</summary>
    public sealed class JourneyTests
    {
        private static readonly BiomeId[] Order =
        {
            BiomeId.WindmouthRidge, BiomeId.MeadowSea, BiomeId.PineHollow, BiomeId.TerraceCountry, BiomeId.MirrorLake,
        };

        private static ChunkGenerator Generator(ContentSettings content, FairnessSettings fair, BiomeSettings biomes, VehicleSettings vehicles)
        {
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), vehicles);
            return new ChunkGenerator(content, fair, biomes, jump);
        }

        private static List<Chunk> Journey(int seed)
        {
            return Generator(new ContentSettings(), new FairnessSettings(), new BiomeSettings(), new VehicleSettings()).GenerateJourney(seed);
        }

        private static TerrainSampler Terrain(int seed, out StretchSlope slope, out List<Chunk> s, out JourneyPlan plan, out LookSettings ls)
        {
            ls = new LookSettings();
            s = Journey(seed);
            slope = new StretchSlope(s);
            plan = new JourneyPlan(s, new BiomeSettings().transitionMeters);
            return new TerrainSampler(slope, s, ls, plan, seed);
        }

        [Test]
        public void Segments_FollowCanonOrder_WithCanonLengths()
        {
            var biomes = new BiomeSettings();
            var content = new ContentSettings();
            List<Chunk> s = Journey(2);
            var plan = new JourneyPlan(s, biomes.transitionMeters);
            Assert.AreEqual(Order.Length, plan.Segments.Count);
            double cursor = 0d;
            for (int i = 0; i < Order.Length; i++)
            {
                JourneySegment seg = plan.Segments[i];
                Assert.AreEqual(Order[i], seg.Biome);
                Assert.AreEqual(cursor, seg.Start, 1e-9, "segments must be contiguous");
                cursor = seg.End;
                double want = biomes.Get(seg.Biome).lengthMeters;
                // A whole number of chunks: within one chunk of the canon length for any draw.
                int count = Math.Max(1, (int)Math.Round(want / ((content.minChunkLength + content.maxChunkLength) * 0.5)));
                Assert.GreaterOrEqual(seg.End - seg.Start, count * content.minChunkLength - 1e-6);
                Assert.LessOrEqual(seg.End - seg.Start, count * content.maxChunkLength + 1e-6);
                Assert.Greater(seg.End - seg.Start, biomes.transitionMeters, "segment shorter than its blend");
            }
            Assert.AreEqual(new StretchSlope(s).Length, cursor, 1e-9);
        }

        [Test]
        public void Journey_IsDeterministic_AndSegmentsMatchStandaloneStreams()
        {
            var content = new ContentSettings();
            ChunkGenerator gen = Generator(content, new FairnessSettings(), new BiomeSettings(), new VehicleSettings());
            List<Chunk> a = gen.GenerateJourney(7);
            List<Chunk> b = gen.GenerateJourney(7);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Length, b[i].Length);
                Assert.AreEqual(a[i].Features.Count, b[i].Features.Count);
            }
            // Each biome draws from its standalone stream: the first chunks have the same shape as that biome's slice.
            int at = 0;
            foreach (BiomeId biome in Order)
            {
                List<Chunk> alone = gen.Generate(7, biome, 1);
                Assert.AreEqual(biome, a[at].Biome);
                Assert.AreEqual(alone[0].Length, a[at].Length, 1e-12, biome + " first chunk");
                Assert.AreEqual(alone[0].Features.Count, a[at].Features.Count, biome + " first chunk features");
                while (at < a.Count && a[at].Biome == biome) at++;
            }
            Assert.AreEqual(a.Count, at);
        }

        [Test]
        public void NestGroups_AreUniqueAcrossTheRun()
        {
            var seen = new HashSet<int>();
            int nests = 0;
            foreach (Chunk c in Journey(2))
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind != FeatureKind.DucklingNest) continue;
                    nests++;
                    Assert.IsTrue(seen.Add(f.variant), "duplicate nest group " + f.variant);
                }
            }
            Assert.Greater(nests, 0);
        }

        [Test]
        public void Journeys_AreFair_AndRideableOnEveryVehicle()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            var vehicles = new VehicleSettings();
            ChunkGenerator gen = Generator(content, fair, new BiomeSettings(), vehicles);
            var checker = new FairnessChecker(fair, content, JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), vehicles));
            for (int seed = 1; seed <= 12; seed++)
            {
                List<Chunk> s = gen.GenerateJourney(seed);
                CollectionAssert.IsEmpty(checker.Check(s), "seed " + seed);
                foreach (VehicleEntry v in vehicles.entries)
                {
                    var bot = new BotValidator(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), fair, vehicles, v.vehicle);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), "seed " + seed + " " + v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Blend_IsContinuous_Monotone_AndFlipsAtTheBorder()
        {
            var biomes = new BiomeSettings();
            var plan = new JourneyPlan(Journey(2), biomes.transitionMeters);
            double half = biomes.transitionMeters * 0.5;
            for (int i = 0; i + 1 < plan.Segments.Count; i++)
            {
                double border = plan.Segments[i].End;
                BiomeBlend before = plan.BlendAt(border - half - 1d);
                Assert.AreEqual(plan.Segments[i].Biome, before.Dominant);
                Assert.AreEqual(0d, before.T);
                BiomeBlend after = plan.BlendAt(border + half + 1d);
                Assert.AreEqual(plan.Segments[i + 1].Biome, after.Dominant);
                Assert.AreEqual(0d, after.T);
                Assert.AreEqual(plan.Segments[i].Biome, plan.BlendAt(border - 0.01).Dominant);
                Assert.AreEqual(plan.Segments[i + 1].Biome, plan.BlendAt(border + 0.01).Dominant);
                Assert.AreEqual(0.5, plan.BlendAt(border).T, 1e-9);
                double last = 0d;
                for (double x = border - half; x <= border + half; x += 0.5)
                {
                    BiomeBlend b = plan.BlendAt(x);
                    Assert.AreEqual(plan.Segments[i].Biome, b.From);
                    Assert.AreEqual(plan.Segments[i + 1].Biome, b.To);
                    Assert.GreaterOrEqual(b.T, last);
                    Assert.Less(b.T - last, 0.02, "blend jumps at " + x);
                    last = b.T;
                }
                Assert.AreEqual(1d, plan.BlendAt(border + half).T, 1e-9);
            }
            Assert.AreEqual(plan.Segments[0].Biome, plan.SegmentAt(-5d).Biome);
            Assert.AreEqual(BiomeId.MirrorLake, plan.SegmentAt(1e9).Biome);
            Assert.AreEqual(BiomeId.MirrorLake, plan.BlendAt(1e9).Dominant);
        }

        [Test]
        public void Terrain_KeepsCorridorAndFrontRules_AndMatchesEachBiomeAwayFromBorders()
        {
            TerrainSampler t = Terrain(2, out StretchSlope slope, out List<Chunk> s, out JourneyPlan plan, out LookSettings ls);
            double half = new BiomeSettings().transitionMeters * 0.5;
            for (double x = 0; x < slope.Length; x += 3.7)
            {
                double track = slope.HeightAt(x);
                Assert.AreEqual(t.InGap(x) ? track - ls.gapDepth : track, t.Height(x, 0), 1e-4, "corridor at " + x);
                for (double z = -ls.terrainFrontDepth; z < 0; z += 4.3)
                    Assert.LessOrEqual(t.Height(x, z), track + 1e-6, "front terrain hides the rider at " + x);
            }
            // Away from the borders the journey backdrop is exactly that biome's own backdrop.
            foreach (JourneySegment seg in plan.Segments)
            {
                var alone = new TerrainSampler(slope, s, ls, ls.Get(seg.Biome), 2);
                for (double x = seg.Start + half + 1d; x < seg.End - half - 1d; x += 41.3)
                {
                    foreach (double z in new[] { 15d, 60d, 130d })
                        Assert.AreEqual(alone.Height(x, z), t.Height(x, z), 1e-9, seg.Biome + " at " + x + "," + z);
                }
            }
        }

        [Test]
        public void Terrain_HasNoStepAcrossBorders()
        {
            TerrainSampler t = Terrain(2, out StretchSlope slope, out _, out JourneyPlan plan, out LookSettings ls);
            double half = new BiomeSettings().transitionMeters * 0.5;
            double cell = ls.terrainCellMeters;
            for (int i = 0; i + 1 < plan.Segments.Count; i++)
            {
                double border = plan.Segments[i].End;
                // Worst step between neighbouring terrain columns in the blend vs. well inside the two biomes.
                double inside = 0d;
                double across = 0d;
                foreach (double z in new[] { 14d, 30d, 70d, 120d })
                {
                    for (double x = border - half - 200d; x < border - half - 100d; x += cell)
                        inside = Math.Max(inside, Math.Abs(Relief(t, slope, x + cell, z) - Relief(t, slope, x, z)));
                    for (double x = border + half + 100d; x < border + half + 200d; x += cell)
                        inside = Math.Max(inside, Math.Abs(Relief(t, slope, x + cell, z) - Relief(t, slope, x, z)));
                    for (double x = border - half - cell; x <= border + half + cell; x += cell)
                        across = Math.Max(across, Math.Abs(Relief(t, slope, x + cell, z) - Relief(t, slope, x, z)));
                }
                Assert.Less(across, Math.Max(inside * 2d, 3d), "terrain cut at border " + i);
            }
        }

        private static double Relief(TerrainSampler t, StretchSlope slope, double x, double z)
        {
            return t.Height(x, z) - slope.HeightAt(x);
        }

        [Test]
        public void Water_OnlyInTheLakeSegment_BelowTheTrack_NestsStayClear()
        {
            TerrainSampler t = Terrain(2, out StretchSlope slope, out List<Chunk> s, out JourneyPlan plan, out LookSettings ls);
            var track = new TrackSettings();
            BiomeLook lake = ls.Get(BiomeId.MirrorLake);
            Assert.IsTrue(t.HasWater);
            JourneySegment lakeSeg = plan.Segments[plan.Segments.Count - 1];
            double mid = (lake.waterMinDepth + lake.waterMaxDepth) * 0.5;
            double half = new BiomeSettings().transitionMeters * 0.5;
            for (double x = 0; x < slope.Length; x += 2.3)
            {
                bool inLake = x >= lakeSeg.Start;
                Assert.AreEqual(inLake, t.InWater(x, mid), "water at " + x);
                Assert.IsFalse(t.InWater(x, 0d), "water in the corridor at " + x);
                Assert.IsFalse(t.InWater(x, track.propSideOffset), "water beside the track at " + x);
                if (!inLake) continue;
                Assert.Less(t.WaterLevel(x), slope.HeightAt(x), "water above the track at " + x);
                if (x > lakeSeg.Start + half)
                    Assert.LessOrEqual(t.Height(x, mid), t.WaterLevel(x) - lake.waterBed + 1e-9, "lake bed above water at " + x);
            }
            foreach (Chunk c in s)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.DucklingNest)
                        Assert.IsFalse(t.InWater(c.StartDistance + f.start, track.propSideOffset), "nest in water");
                }
            }
        }

        [Test]
        public void Scatter_StaysInItsBiome_BehindTheTrack_OutOfTheWater()
        {
            TerrainSampler t = Terrain(2, out StretchSlope slope, out _, out JourneyPlan plan, out LookSettings ls);
            double length = slope.Length + new TrackSettings().runOutMeters;
            List<PropPlacement> props = PropScatterer.Place(ls, plan, t, length, 2);
            Assert.Greater(props.Count, 0);
            var names = new Dictionary<string, BiomeId>();
            foreach (PropScatter p in ls.scatters) names[p.name] = p.biome;
            foreach (PropPlacement p in props)
            {
                Assert.GreaterOrEqual(p.Z, ls.corridorHalfWidth + ls.corridorBlend, p.Name + " in the corridor");
                Assert.GreaterOrEqual(p.X, 0d);
                Assert.Less(p.X, length);
                Assert.AreEqual(names[p.Name], plan.SegmentAt(p.X).Biome, p.Name + " outside its biome at " + p.X);
                Assert.IsFalse(t.InWater(p.X, p.Z), p.Name + " in the water");
                Assert.AreEqual(t.Height(p.X, p.Z), p.Y, 1e-9);
            }
            CollectionAssert.AreEqual(props.ConvertAll(q => q.X), PropScatterer.Place(ls, plan, t, length, 2).ConvertAll(q => q.X));
        }

        [Test]
        public void Mist_BaseBlendsBetweenBiomes_BanksStillCount()
        {
            var biomes = new BiomeSettings();
            var fair = new FairnessSettings();
            List<Chunk> s = Journey(2);
            var plan = new JourneyPlan(s, biomes.transitionMeters);
            var mist = new MistField(s, biomes, plan);
            double half = biomes.transitionMeters * 0.5;
            for (int i = 0; i < plan.Segments.Count; i++)
            {
                JourneySegment seg = plan.Segments[i];
                Assert.AreEqual(biomes.Get(seg.Biome).baseMistDensity, mist.BaseAt((seg.Start + seg.End) * 0.5), 1e-6);
                if (i + 1 == plan.Segments.Count) continue;
                float a = biomes.Get(seg.Biome).baseMistDensity;
                float b = biomes.Get(plan.Segments[i + 1].Biome).baseMistDensity;
                float last = mist.BaseAt(seg.End - half);
                for (double x = seg.End - half; x <= seg.End + half; x += 1d)
                {
                    float d = mist.BaseAt(x);
                    Assert.GreaterOrEqual(d, Math.Min(a, b) - 1e-6f);
                    Assert.LessOrEqual(d, Math.Max(a, b) + 1e-6f);
                    Assert.Less(Math.Abs(d - last), 0.02f, "mist jumps at " + x);
                    last = d;
                }
            }
            foreach (KeyValuePair<Span, float> bank in mist.Banks)
            {
                double x = (bank.Key.Start + bank.Key.End) * 0.5;
                Assert.GreaterOrEqual(mist.DensityAt(x), bank.Value);
                Assert.GreaterOrEqual(mist.DensityAt(x), mist.BaseAt(x));
            }
            // Base density only drives the audio low-pass; what the eye sees over a jump is the bank and fog check.
            CollectionAssert.IsEmpty(MistVisibility.Check(new LookSettings(), new CameraSettings(), fair));
        }

        [Test]
        public void Plan_RejectsAnEmptyStretch()
        {
            Assert.Throws<ArgumentException>(() => new JourneyPlan(new List<Chunk>(), 80d));
            Assert.Throws<ArgumentNullException>(() => new TerrainSampler(new StretchSlope(Journey(2)), Journey(2), new LookSettings(), (JourneyPlan)null, 2));
        }
    }
}
