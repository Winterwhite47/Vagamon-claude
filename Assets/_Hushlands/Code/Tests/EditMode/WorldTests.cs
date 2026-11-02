using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class WorldTests
    {
        private static List<Chunk> Stretch()
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForDefaultVehicle(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(2, BiomeId.MeadowSea, content.chunksPerStretch);
        }

        private static TerrainSampler Sampler(out StretchSlope slope, out List<Chunk> stretch, out LookSettings ls)
        {
            stretch = Stretch();
            slope = new StretchSlope(stretch);
            ls = new LookSettings();
            return new TerrainSampler(slope, stretch, ls, ls.Get(BiomeId.MeadowSea), 2);
        }

        [Test]
        public void Corridor_EqualsSlopeLine_ExceptGaps()
        {
            TerrainSampler t = Sampler(out StretchSlope slope, out _, out LookSettings ls);
            for (double x = 0; x < slope.Length; x += 0.5)
            {
                double expect = t.InGap(x) ? slope.HeightAt(x) - ls.gapDepth : slope.HeightAt(x);
                Assert.AreEqual(expect, t.Height(x, 0), 1e-4, "corridor at " + x);
                Assert.AreEqual(expect, t.Height(x, ls.corridorHalfWidth), 1e-4);
            }
        }

        [Test]
        public void FrontTerrain_NeverRisesAboveTrack()
        {
            TerrainSampler t = Sampler(out StretchSlope slope, out _, out LookSettings ls);
            for (double x = 0; x < slope.Length; x += 3)
            {
                for (double z = -ls.terrainFrontDepth; z < 0; z += 1)
                    Assert.LessOrEqual(t.Height(x, z), slope.HeightAt(x) + 1e-6, "front terrain hides the rider at " + x + "," + z);
            }
        }

        [Test]
        public void Terrain_IsDeterministic_AndBackdropRises()
        {
            TerrainSampler a = Sampler(out StretchSlope slope, out List<Chunk> stretch, out LookSettings ls);
            var b = new TerrainSampler(slope, stretch, ls, ls.Get(BiomeId.MeadowSea), 2);
            double above = 0;
            int n = 0;
            for (double x = 0; x < slope.Length; x += 7)
            {
                Assert.AreEqual(a.Height(x, 60), b.Height(x, 60));
                above += a.Height(x, 120) - slope.HeightAt(x);
                n++;
            }
            Assert.Greater(above / n, 0d, "backdrop hills should rise behind the track");
        }

        [Test]
        public void Noise_InRange()
        {
            for (int i = 0; i < 2000; i++)
            {
                double v = ValueNoise.Fbm(i * 0.37, i * 0.11, 5);
                Assert.GreaterOrEqual(v, -1d);
                Assert.LessOrEqual(v, 1d);
            }
        }

        [Test]
        public void MeadowSea_UsesCanonPalette_AndIsTuned()
        {
            BiomeLook m = new LookSettings().Get(BiomeId.MeadowSea);
            Assert.IsTrue(m.tuned);
            Assert.AreEqual("#0B3B17", m.grass);
            Assert.AreEqual("#8D3F2D", m.soil);
            Assert.AreEqual("#4A6B82", m.fogAlbedo);
            Assert.AreEqual("#D4AF37", m.accent);
            Assert.Less(m.sunElevationDegrees, 15f, "golden hour sun should be low");
            Assert.Less(m.sunKelvin, 4500f, "golden hour light should be warm");
        }

        [Test]
        public void Mist_NeverHidesRequiredJumps_Visually()
        {
            List<string> errors = MistVisibility.Check(new LookSettings(), new CameraSettings(), new FairnessSettings());
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            var tight = new LookSettings { mistMeanFreePathAtFull = 5f };
            Assert.IsNotEmpty(MistVisibility.Check(tight, new CameraSettings(), new FairnessSettings()));
        }

        [Test]
        public void MistField_ReportsBanks_ForLowPass()
        {
            List<Chunk> s = Stretch();
            var field = new MistField(s, 0.2f);
            Assert.Greater(field.Banks.Count, 0);
            var bank = field.Banks[0];
            double mid = (bank.Key.Start + bank.Key.End) * 0.5;
            Assert.AreEqual(System.Math.Max(0.2f, bank.Value), field.DensityAt(mid), 1e-6f);
            var mix = new MixDirector(new AudioMixSettings());
            mix.SetMistDensity(field.DensityAt(mid));
            Assert.Less(mix.LowPassCutoffHz, new AudioMixSettings().mistClearCutoffHz);
        }

        [Test]
        public void Palette_ParsesHex()
        {
            Assert.IsTrue(Palette.TryParse("#D4AF37", out float r, out float g, out float b));
            Assert.AreEqual(0xD4 / 255f, r, 1e-6f);
            Assert.AreEqual(0xAF / 255f, g, 1e-6f);
            Assert.AreEqual(0x37 / 255f, b, 1e-6f);
            Assert.IsFalse(Palette.TryParse("D4AF37", out _, out _, out _));
            Assert.IsFalse(Palette.TryParse("#GGGGGG", out _, out _, out _));
        }
    }
}
