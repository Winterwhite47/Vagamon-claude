using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class SettingsTests
    {
        private static IEnumerable<ISettings> AllDefaults()
        {
            yield return new SpeedSettings();
            yield return new RiderSettings();
            yield return new SurfaceSettings();
            yield return new TrackSettings();
            yield return new FailureSettings();
            yield return new AssistSettings();
            yield return new MessageSettings();
            yield return new DucklingSettings();
            yield return new FairnessSettings();
            yield return new ContentSettings();
            yield return new BiomeSettings();
            yield return new CameraSettings();
            yield return new AudioMixSettings();
            yield return new UiMotionSettings();
            yield return new PickupSettings();
            yield return new ScoreSettings();
            yield return new VehicleSettings();
            yield return new ClothSettings();
            yield return new LookSettings();
            yield return new HazardSettings();
            yield return new GoalSettings();
            yield return new UiSettings();
        }

        [TestCaseSource(nameof(AllDefaults))]
        public void Defaults_AreValid(ISettings s)
        {
            var errors = new List<string>();
            s.Validate(errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Canon_Values()
        {
            var speed = new SpeedSettings();
            Assert.AreEqual(6f, speed.minSpeed);
            Assert.AreEqual(18f, speed.maxSpeed);
            Assert.AreEqual(0.45f, speed.rampAmount, 1e-6f);
            Assert.AreEqual(10f, speed.rampIntervalSeconds);
            var fair = new FairnessSettings();
            Assert.AreEqual(1.5f, fair.obstacleRevealSeconds, 1e-6f);
            Assert.AreEqual(1f, fair.collapseWarningSeconds, 1e-6f);
            Assert.AreEqual(3, new FailureSettings().assistAfterFailures);
            Assert.AreEqual(0.01f, new RiderSettings().physicsStep, 1e-6f);
            Assert.AreEqual(200, new ContentSettings().validationStretchCount);
            Assert.AreEqual(VehicleId.WoodenHillSled, new VehicleSettings().defaultVehicle);
        }

        [Test]
        public void Ramp_FollowsSlopeAssist()
        {
            var s = new SpeedSettings();
            Assert.AreEqual(6f, s.TargetAt(0), 1e-5f);
            Assert.AreEqual(6.45f, s.TargetAt(10), 1e-5f);
            Assert.AreEqual(6.9f, s.TargetAt(20), 1e-5f);
            Assert.AreEqual(18f, s.TargetAt(100000), 1e-5f);
            Assert.AreEqual(6f, s.TargetAt(-5), 1e-5f);
        }

        [Test]
        public void Validation_CatchesBadValues()
        {
            var s = new SpeedSettings { minSpeed = 20f };
            var e = new List<string>();
            s.Validate(e);
            Assert.IsNotEmpty(e);

            var surf = new SurfaceSettings { entries = new[] { new SurfaceEntry(SurfaceId.Grass, 0.1f, 0.1f) } };
            e.Clear();
            surf.Validate(e);
            Assert.IsTrue(e.Exists(x => x.Contains("missing entry")));

            var veh = new VehicleSettings { defaultVehicle = VehicleId.GliderSkid };
            e.Clear();
            veh.Validate(e);
            Assert.IsTrue(e.Exists(x => x.Contains("unlocked by default")));
        }

        [Test]
        public void MistLowPass_Monotonic()
        {
            var a = new AudioMixSettings();
            Assert.AreEqual(a.mistClearCutoffHz, a.CutoffFor(0f), 1f);
            Assert.AreEqual(a.mistDenseCutoffHz, a.CutoffFor(1f), 1f);
            Assert.Greater(a.CutoffFor(0.3f), a.CutoffFor(0.6f));
            Assert.AreEqual(a.CutoffFor(1f), a.CutoffFor(2f), 1e-3f);
        }

        [Test]
        public void ReducedMotion_ShorterTransition()
        {
            var u = new UiMotionSettings();
            Assert.Less(u.TransitionSeconds(true), u.TransitionSeconds(false));
        }
    }
}
