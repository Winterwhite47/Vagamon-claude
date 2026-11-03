using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T14 biome 4: Mirror Lake water, duckling safety near water, fairness on every vehicle, look and scatter.</summary>
    public sealed class MirrorLakeTests
    {
        private static List<Chunk> Stretch(int seed, BiomeId biome)
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(seed, biome, content.chunksPerStretch);
        }

        private static TerrainSampler Terrain(BiomeId biome, out StretchSlope slope, out List<Chunk> s)
        {
            var ls = new LookSettings();
            s = Stretch(2, biome);
            slope = new StretchSlope(s);
            return new TerrainSampler(slope, s, ls, ls.Get(biome), 2);
        }

        [Test]
        public void Water_OnlyAtMirrorLake_BelowTrackEverywhere_BedUnderSurface()
        {
            var ls = new LookSettings();
            BiomeLook look = ls.Get(BiomeId.MirrorLake);
            TerrainSampler t = Terrain(BiomeId.MirrorLake, out StretchSlope slope, out _);
            Assert.IsTrue(t.HasWater);
            for (double x = 0; x < slope.Length; x += 1)
            {
                Assert.Less(t.WaterLevel(x), slope.HeightAt(x), "water above the track at " + x);
                for (double z = look.waterMinDepth; z <= look.waterMaxDepth; z += 10)
                    Assert.LessOrEqual(t.Height(x, z), t.WaterLevel(x) - look.waterBed + 1e-9, "lake bed above water");
                double corridor = t.InGap(x) ? slope.HeightAt(x) - ls.gapDepth : slope.HeightAt(x);
                Assert.AreEqual(corridor, t.Height(x, 0), 1e-4, "corridor untouched by the lake");
            }
            foreach (BiomeId other in new[] { BiomeId.MeadowSea, BiomeId.WindmouthRidge, BiomeId.PineHollow, BiomeId.TerraceCountry })
                Assert.IsFalse(Terrain(other, out _, out _).HasWater, other + " has water");
        }

        [Test]
        public void DucklingNests_StayFarFromWater()
        {
            var ls = new LookSettings();
            var track = new TrackSettings();
            BiomeLook look = ls.Get(BiomeId.MirrorLake);
            Assert.GreaterOrEqual(look.waterMinDepth - track.propSideOffset, ls.nestWaterSpacing,
                "nests (beside the track) must be well clear of the lake band");
            TerrainSampler t = Terrain(BiomeId.MirrorLake, out _, out List<Chunk> s);
            foreach (Chunk c in s)
                foreach (ChunkFeature f in c.Features)
                    if (f.kind == FeatureKind.DucklingNest) Assert.IsFalse(t.InWater(track.propSideOffset));
        }

        [Test]
        public void Stretches_AreFair_AndRideableOnEveryVehicle()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            var vehicles = new VehicleSettings();
            var checker = new FairnessChecker(fair, content, JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), vehicles));
            for (int seed = 1; seed <= 30; seed++)
            {
                List<Chunk> s = Stretch(seed, BiomeId.MirrorLake);
                CollectionAssert.IsEmpty(checker.Check(s), "seed " + seed);
                foreach (VehicleEntry v in vehicles.entries)
                {
                    var bot = new BotValidator(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), fair, vehicles, v.vehicle);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Look_IsTuned_MistNeverHidesJumps_ScatterOnShoreNotInWater()
        {
            var ls = new LookSettings();
            BiomeLook look = ls.Get(BiomeId.MirrorLake);
            Assert.IsTrue(look.tuned);
            Assert.AreEqual(LookSettings.MistBlue, look.fogAlbedo);
            Assert.AreEqual(LookSettings.MistBlue, ls.Material("Water").color);
            CollectionAssert.IsEmpty(MistVisibility.Check(ls, new CameraSettings(), new FairnessSettings(), new HazardSettings().squallFogScale));
            TerrainSampler t = Terrain(BiomeId.MirrorLake, out StretchSlope slope, out _);
            List<PropPlacement> props = PropScatterer.Place(ls, BiomeId.MirrorLake, t, slope.Length, 2);
            Assert.Greater(props.Count, 50);
            foreach (PropPlacement p in props)
            {
                Assert.Greater(p.Z, ls.corridorHalfWidth + ls.corridorBlend);
                Assert.IsFalse(t.InWater(p.Z), "shore props must not stand in the lake");
            }
            var errs = new List<string>();
            ls.Validate(errs);
            CollectionAssert.IsEmpty(errs, string.Join("\n", errs));
        }

        [Test]
        public void AllFiveBiomes_AreTuned()
        {
            foreach (BiomeLook b in new LookSettings().biomes) Assert.IsTrue(b.tuned, b.biome + " still placeholder");
        }
    }
}
