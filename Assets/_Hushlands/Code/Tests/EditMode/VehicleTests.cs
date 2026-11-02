using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class VehicleTests
    {
        [Test]
        public void FairnessUsesWeakestJumper()
        {
            var v = new VehicleSettings();
            var r = new RiderSettings();
            float min = float.MaxValue;
            foreach (VehicleEntry e in v.entries) min = System.Math.Min(min, e.jumpScale);
            JumpModel j = JumpModel.ForAllVehicles(r, new SpeedSettings(), v);
            Assert.AreEqual(r.jumpVelocity * min, j.JumpVelocity, 1e-5);
            Assert.Less(min, 1f, "a vehicle weaker than the sled exists, so this rule matters");
        }

        [Test]
        public void EveryVehicle_RidesValidationStretches()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            var vehicles = new VehicleSettings();
            var rider = new RiderSettings();
            var speed = new SpeedSettings();
            JumpModel jump = JumpModel.ForAllVehicles(rider, speed, vehicles);
            var gen = new ChunkGenerator(content, fair, new BiomeSettings(), jump);
            foreach (VehicleEntry v in vehicles.entries)
            {
                var bot = new BotValidator(rider, speed, new SurfaceSettings(), fair, vehicles, v.vehicle);
                for (int seed = 1; seed <= 25; seed++)
                {
                    List<Chunk> s = gen.Generate(seed, (BiomeId)(seed % 5), content.chunksPerStretch);
                    Assert.IsTrue(bot.Validate(s, seed, out string report), v.vehicle + " " + report);
                }
            }
        }

        [Test]
        public void Loadout_AppliesScales_WithoutMutatingConfig()
        {
            var vehicles = new VehicleSettings();
            var rider = new RiderSettings();
            var speed = new SpeedSettings();
            var surfaces = new SurfaceSettings();
            VehicleEntry glider = vehicles.Get(VehicleId.GliderSkid);
            VehicleLoadout l = VehicleLoadout.Build(glider, rider, speed, surfaces);
            Assert.AreEqual(rider.jumpVelocity * glider.jumpScale, l.Rider.jumpVelocity, 1e-5f);
            Assert.AreEqual(rider.flipDegreesPerSecond * glider.flipScale, l.Rider.flipDegreesPerSecond, 1e-3f);
            Assert.AreEqual(speed.baseDrag * glider.dragScale, l.Speed.baseDrag, 1e-6f);
            Assert.AreEqual(new SurfaceTable(surfaces).FrictionFor(SurfaceId.Mud) * glider.frictionScale,
                l.Surfaces.FrictionFor(SurfaceId.Mud), 1e-6f);
            Assert.AreEqual(new RiderSettings().jumpVelocity, rider.jumpVelocity, "shared config mutated");
            Assert.AreEqual(new SpeedSettings().baseDrag, speed.baseDrag, "shared config mutated");
        }

        [Test]
        public void Garage_UnlocksByLifetimeCoins_SelectionPersists()
        {
            var v = new VehicleSettings();
            var save = new MemorySaveService();
            var g = new Garage(v, save);
            Assert.AreEqual(VehicleId.WoodenHillSled, g.Selected);
            Assert.IsFalse(g.Select(VehicleId.TeaCrateBoard), "locked vehicle selectable");
            var unlocked = new List<VehicleId>();
            g.Unlocked += unlocked.Add;
            int need = v.Get(VehicleId.TeaCrateBoard).unlockLifetimeCoins;
            g.AddLifetimeCoins(need - 1);
            Assert.IsFalse(g.IsUnlocked(VehicleId.TeaCrateBoard));
            Assert.AreEqual(1, g.CoinsToNextUnlock());
            g.AddLifetimeCoins(1);
            CollectionAssert.AreEqual(new[] { VehicleId.TeaCrateBoard }, unlocked);
            Assert.IsTrue(g.Select(VehicleId.TeaCrateBoard));
            var again = new Garage(v, save);
            Assert.AreEqual(VehicleId.TeaCrateBoard, again.Selected);
        }

        [Test]
        public void Garage_IgnoresCorruptSelection()
        {
            var save = new MemorySaveService();
            save.Save(Garage.KeySelected, 999);
            Assert.AreEqual(VehicleId.WoodenHillSled, new Garage(new VehicleSettings(), save).Selected);
            save.Save(Garage.KeySelected, (int)VehicleId.RainUmbrellaSlider);
            Assert.AreEqual(VehicleId.WoodenHillSled, new Garage(new VehicleSettings(), save).Selected, "locked selection must fall back");
        }

        [Test]
        public void Vehicles_DefaultUnlocked_ThresholdsRise()
        {
            var v = new VehicleSettings();
            int last = -1;
            foreach (VehicleEntry e in v.entries)
            {
                Assert.Greater(e.unlockLifetimeCoins, last);
                last = e.unlockLifetimeCoins;
            }
            Assert.IsTrue(v.Get(v.defaultVehicle).unlockedByDefault);
        }
    }
}
