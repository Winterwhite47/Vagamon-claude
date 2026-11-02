using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T14 biome 3: Terrace Country vistas, terraces, fairness on every vehicle, look and scatter.</summary>
    public sealed class TerraceCountryTests
    {
        private static List<Chunk> Stretch(int seed, BiomeId biome)
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(seed, biome, content.chunksPerStretch);
        }

        [Test]
        public void Vistas_OnlyInTerraceCountry_InsideChunks()
        {
            int vistas = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (Chunk c in Stretch(seed, BiomeId.TerraceCountry))
                    foreach (ChunkFeature f in c.Features)
                        if (f.kind == FeatureKind.Vista)
                        {
                            vistas++;
                            Assert.LessOrEqual(f.End, c.Length + 1e-6);
                        }
                foreach (BiomeId other in new[] { BiomeId.MeadowSea, BiomeId.WindmouthRidge, BiomeId.PineHollow })
                    foreach (Chunk c in Stretch(seed, other))
                        foreach (ChunkFeature f in c.Features)
                            Assert.AreNotEqual(FeatureKind.Vista, f.kind, other + " got a vista");
            }
            Assert.Greater(vistas, 30);
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
                List<Chunk> s = Stretch(seed, BiomeId.TerraceCountry);
                CollectionAssert.IsEmpty(checker.Check(s), "seed " + seed);
                foreach (VehicleEntry v in vehicles.entries)
                {
                    var bot = new BotValidator(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), fair, vehicles, v.vehicle);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Camera_UsesVista_StillShowsRevealDistance()
        {
            List<Chunk> s = Stretch(2, BiomeId.TerraceCountry);
            var slope = new StretchSlope(s);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var director = new CameraDirectorCore(new CameraSettings(), slope, new RiderCameraSignals(rider, s), null,
                BiomeId.TerraceCountry, false);
            var pilot = new BotPilot(s, new FairnessSettings().botTakeoffSeconds);
            var input = new ScriptedInput();
            bool vista = false;
            while (rider.Position.Distance < slope.Length - 30)
            {
                bool grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                pilot.Drive(input, rider.Position.Distance, rider.Speed, grounded);
                rider.FixedStep(input, 0.01f);
                director.Tick(rider.Position.Distance, 0.01f);
                vista |= director.Current == CameraState.Vista;
            }
            Assert.IsTrue(vista, "Vista framing never used");
        }

        [Test]
        public void Terraces_AreFlatTreads_ContinuousAndRising()
        {
            double last = TerrainSampler.Terrace(0, 3, 0.25);
            int flat = 0;
            for (int i = 1; i <= 3000; i++)
            {
                double h = i * 0.01;
                double t = TerrainSampler.Terrace(h, 3, 0.25);
                Assert.GreaterOrEqual(t, last - 1e-9, "terraces must not dip");
                Assert.Less(t - last, 0.1, "terrace jump");
                if (System.Math.Abs(t - last) < 1e-12) flat++;
                last = t;
            }
            Assert.Greater(flat, 2000, "most of each step should be flat tread");
            Assert.AreEqual(0f, new LookSettings().Get(BiomeId.MeadowSea).terraceStep, "Meadow Sea stays smooth");
        }

        [Test]
        public void Look_IsTuned_MistNeverHidesJumps_ScatterBehindTrack()
        {
            var ls = new LookSettings();
            BiomeLook t = ls.Get(BiomeId.TerraceCountry);
            Assert.IsTrue(t.tuned);
            Assert.AreEqual(LookSettings.Terracotta, t.soil);
            Assert.AreEqual(LookSettings.MistBlue, t.fogAlbedo);
            CollectionAssert.IsEmpty(MistVisibility.Check(ls, new CameraSettings(), new FairnessSettings(), new HazardSettings().squallFogScale));
            List<Chunk> s = Stretch(2, BiomeId.TerraceCountry);
            var slope = new StretchSlope(s);
            var terrain = new TerrainSampler(slope, s, ls, t, 2);
            List<PropPlacement> props = PropScatterer.Place(ls, BiomeId.TerraceCountry, terrain, slope.Length, 2);
            Assert.Greater(props.Count, 200);
            foreach (PropPlacement p in props) Assert.Greater(p.Z, ls.corridorHalfWidth + ls.corridorBlend);
            var errs = new List<string>();
            ls.Validate(errs);
            CollectionAssert.IsEmpty(errs, string.Join("\n", errs));
        }
    }
}
