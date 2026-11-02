using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T14 biome 1: Windmouth Ridge content flavour, look, scatter and fairness.</summary>
    public sealed class WindmouthTests
    {
        private static List<Chunk> Stretch(int seed, BiomeId biome)
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(seed, biome, content.chunksPerStretch);
        }

        [Test]
        public void Profile_OnlyAffectsWindmouth()
        {
            var c = new ContentSettings();
            ContentKnobs w = c.KnobsFor(BiomeId.WindmouthRidge);
            ContentKnobs m = c.KnobsFor(BiomeId.MeadowSea);
            Assert.AreEqual(c.minSlopeDegrees, m.MinSlope);
            Assert.AreEqual(c.obstacleWeight, m.ObstacleWeight);
            Assert.Greater(w.ObstacleWeight, m.ObstacleWeight, "ridge has more rock outcrops");
            Assert.Less(w.GapWeight, m.GapWeight, "ridge has fewer crevasses");
            Assert.Greater(w.MinSlope, m.MinSlope, "ridge is steeper");
        }

        [Test]
        public void Stretches_UseProfileSlopesAndSurfaces()
        {
            var c = new ContentSettings();
            ContentKnobs k = c.KnobsFor(BiomeId.WindmouthRidge);
            var allowed = new HashSet<SurfaceId>(k.Secondary) { new BiomeSettings().Get(BiomeId.WindmouthRidge).primarySurface };
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (Chunk ch in Stretch(seed, BiomeId.WindmouthRidge))
                {
                    double deg = ch.SlopeRadians * 180.0 / System.Math.PI;
                    Assert.That(deg, Is.InRange(k.MinSlope - 1e-3, k.MaxSlope + 1e-3));
                    foreach (SurfaceSpan s in ch.Surfaces) Assert.IsTrue(allowed.Contains(s.surface), s.surface.ToString());
                }
            }
        }

        [Test]
        public void Stretches_AreFair_AndRideableOnEveryVehicle()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            var vehicles = new VehicleSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), vehicles);
            var checker = new FairnessChecker(fair, content, jump);
            for (int seed = 1; seed <= 30; seed++)
            {
                List<Chunk> s = Stretch(seed, BiomeId.WindmouthRidge);
                CollectionAssert.IsEmpty(checker.Check(s), "seed " + seed);
                foreach (VehicleEntry v in vehicles.entries)
                {
                    var bot = new BotValidator(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), fair, vehicles, v.vehicle);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Look_IsTuned_CanonMist_MistNeverHidesJumps()
        {
            var ls = new LookSettings();
            BiomeLook w = ls.Get(BiomeId.WindmouthRidge);
            Assert.IsTrue(w.tuned);
            Assert.AreEqual(LookSettings.MistBlue, w.fogAlbedo);
            Assert.AreEqual(LookSettings.Gold, w.accent);
            Assert.Less(w.windExtraX, 0f, "ridge wind streams the scarf back");
            List<string> errors = MistVisibility.Check(ls, new CameraSettings(), new FairnessSettings(), new HazardSettings().squallFogScale);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            var errs = new List<string>();
            ls.Validate(errs);
            CollectionAssert.IsEmpty(errs, string.Join("\n", errs));
        }

        [Test]
        public void Scatter_StaysBehindTrack_OutsideCorridor_OnGround_Deterministic()
        {
            var ls = new LookSettings();
            List<Chunk> s = Stretch(2, BiomeId.WindmouthRidge);
            var slope = new StretchSlope(s);
            var terrain = new TerrainSampler(slope, s, ls, ls.Get(BiomeId.WindmouthRidge), 2);
            List<PropPlacement> a = PropScatterer.Place(ls, BiomeId.WindmouthRidge, terrain, slope.Length, 2);
            List<PropPlacement> b = PropScatterer.Place(ls, BiomeId.WindmouthRidge, terrain, slope.Length, 2);
            Assert.Greater(a.Count, 10);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X);
                Assert.Greater(a[i].Z, ls.corridorHalfWidth + ls.corridorBlend, "prop inside the corridor or in front");
                Assert.AreEqual(terrain.Height(a[i].X, a[i].Z), a[i].Y, 1e-9);
            }
            Assert.AreEqual(0, PropScatterer.Place(ls, BiomeId.MeadowSea, terrain, slope.Length, 2).Count,
                "Meadow Sea look (approved at T7) gets no scatter");
        }
    }
}
