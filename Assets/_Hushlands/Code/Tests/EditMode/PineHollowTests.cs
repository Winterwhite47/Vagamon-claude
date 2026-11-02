using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T14 biome 2: Pine Hollow content (shelters), fairness on every vehicle, look and scatter.</summary>
    public sealed class PineHollowTests
    {
        private static List<Chunk> Stretch(int seed, BiomeId biome)
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(seed, biome, content.chunksPerStretch);
        }

        [Test]
        public void Shelters_OnlyInPineHollow_InsideChunks()
        {
            int shelters = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                foreach (Chunk c in Stretch(seed, BiomeId.PineHollow))
                {
                    foreach (ChunkFeature f in c.Features)
                    {
                        if (f.kind != FeatureKind.Shelter) continue;
                        shelters++;
                        Assert.GreaterOrEqual(f.start, 0d);
                        Assert.LessOrEqual(f.End, c.Length + 1e-6, "shelter past chunk end");
                    }
                }
                foreach (BiomeId other in new[] { BiomeId.MeadowSea, BiomeId.WindmouthRidge })
                    foreach (Chunk c in Stretch(seed, other))
                        foreach (ChunkFeature f in c.Features)
                            Assert.AreNotEqual(FeatureKind.Shelter, f.kind, other + " got a shelter");
            }
            Assert.Greater(shelters, 40);
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
                List<Chunk> s = Stretch(seed, BiomeId.PineHollow);
                CollectionAssert.IsEmpty(checker.Check(s), "seed " + seed);
                foreach (VehicleEntry v in vehicles.entries)
                {
                    var bot = new BotValidator(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), fair, vehicles, v.vehicle);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Camera_TakesShelterFraming_UnderCanopy_StillShowsRevealDistance()
        {
            List<Chunk> s = Stretch(2, BiomeId.PineHollow);
            var slope = new StretchSlope(s);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var signals = new RiderCameraSignals(rider, s);
            var cam = new CameraSettings();
            var director = new CameraDirectorCore(cam, slope, signals, null, BiomeId.PineHollow, false);
            var pilot = new BotPilot(s, new FairnessSettings().botTakeoffSeconds);
            var input = new ScriptedInput();
            bool sheltered = false;
            while (rider.Position.Distance < slope.Length - 30)
            {
                bool grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                pilot.Drive(input, rider.Position.Distance, rider.Speed, grounded);
                rider.FixedStep(input, 0.01f);
                director.Tick(rider.Position.Distance, 0.01f);
                sheltered |= director.Current == CameraState.Shelter;
            }
            Assert.IsTrue(sheltered, "Shelter framing never used");
            CollectionAssert.IsEmpty(FramingMath.CheckContract(cam, new FairnessSettings().visibleAheadMeters));
        }

        [Test]
        public void Look_IsTuned_MistNeverHidesJumps_ScatterBehindTrack()
        {
            var ls = new LookSettings();
            BiomeLook p = ls.Get(BiomeId.PineHollow);
            Assert.IsTrue(p.tuned);
            Assert.AreEqual(LookSettings.Emerald, p.grass);
            Assert.AreEqual(LookSettings.MistBlue, p.fogAlbedo);
            CollectionAssert.IsEmpty(MistVisibility.Check(ls, new CameraSettings(), new FairnessSettings(), new HazardSettings().squallFogScale));
            List<Chunk> s = Stretch(2, BiomeId.PineHollow);
            var slope = new StretchSlope(s);
            var terrain = new TerrainSampler(slope, s, ls, p, 2);
            List<PropPlacement> props = PropScatterer.Place(ls, BiomeId.PineHollow, terrain, slope.Length, 2);
            int pines = 0;
            foreach (PropPlacement pp in props)
            {
                Assert.Greater(pp.Z, ls.corridorHalfWidth + ls.corridorBlend);
                if (pp.Shape == "Pine") pines++;
            }
            Assert.Greater(pines, 100);
            var errs = new List<string>();
            ls.Validate(errs);
            CollectionAssert.IsEmpty(errs, string.Join("\n", errs));
        }
    }
}
