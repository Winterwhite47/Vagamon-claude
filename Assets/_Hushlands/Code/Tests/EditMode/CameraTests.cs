using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class CameraTests
    {
        private const float Dt = 0.01f;

        private sealed class FakeSignals : ICameraSignalSource
        {
            public CameraSignals Value = new CameraSignals { LandslideSeconds = float.PositiveInfinity };

            public CameraSignals Read(float dt)
            {
                return Value;
            }
        }

        private static CameraDirectorCore Director(FakeSignals sig, MixDirector mix = null, bool reduced = false)
        {
            return new CameraDirectorCore(new CameraSettings(), new ConstantSlope(10000, 0.17f, SurfaceId.Grass), sig, mix,
                BiomeId.MeadowSea, reduced);
        }

        private static void Run(CameraDirectorCore d, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt) d.Tick(0d, Dt);
        }

        [Test]
        public void Contract_AllStatesBiomesAndReducedMotion_ShowRevealDistance()
        {
            List<string> errors = FramingMath.CheckContract(new CameraSettings(), new FairnessSettings().visibleAheadMeters);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            Assert.GreaterOrEqual(new CameraSettings().maxSlopeForFramingDegrees, new ContentSettings().maxSlopeDegrees);
        }

        [Test]
        public void Contract_RejectsTightFraming()
        {
            var s = new CameraSettings();
            s.states[0].distance = 12f;
            Assert.IsNotEmpty(FramingMath.CheckContract(s, new FairnessSettings().visibleAheadMeters));
        }

        [Test]
        public void Crash_GoesToFailureHold_Immediately()
        {
            var sig = new FakeSignals();
            CameraDirectorCore d = Director(sig);
            Run(d, 0.1f);
            sig.Value.Rider = RiderState.Crashed;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.FailureHold, d.Current);
        }

        [Test]
        public void SmallHop_StaysCruise_LongAir_GoesAir()
        {
            var sig = new FakeSignals();
            CameraDirectorCore d = Director(sig);
            sig.Value.Rider = RiderState.Airborne;
            sig.Value.AirborneSeconds = 0.1f;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Cruise, d.Current);
            sig.Value.AirborneSeconds = new CameraSettings().airDelaySeconds;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Air, d.Current);
        }

        [Test]
        public void Danger_BeatsAir_AndBoost()
        {
            var sig = new FakeSignals();
            CameraDirectorCore d = Director(sig);
            sig.Value.Rider = RiderState.Airborne;
            sig.Value.AirborneSeconds = 1f;
            sig.Value.Speed = 17f;
            sig.Value.LandslideSeconds = 1f;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Danger, d.Current);
        }

        [Test]
        public void Boost_BySpeed_Vista_ByZone()
        {
            var sig = new FakeSignals();
            CameraDirectorCore d = Director(sig);
            sig.Value.InVista = true;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Vista, d.Current);
            sig.Value.Speed = new CameraSettings().boostSpeedThreshold;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Boost, d.Current);
        }

        [Test]
        public void DeEscalation_WaitsForDwell()
        {
            var s = new CameraSettings();
            var sig = new FakeSignals();
            CameraDirectorCore d = Director(sig);
            sig.Value.Rider = RiderState.Airborne;
            sig.Value.AirborneSeconds = 1f;
            d.Tick(0d, Dt);
            Assert.AreEqual(CameraState.Air, d.Current);
            sig.Value.Rider = RiderState.Grounded;
            sig.Value.AirborneSeconds = 0f;
            Run(d, s.minDwellSeconds * 0.5f);
            Assert.AreEqual(CameraState.Air, d.Current, "dropped out of Air before dwell");
            Run(d, s.minDwellSeconds);
            Assert.AreEqual(CameraState.Cruise, d.Current);
        }

        [Test]
        public void Blend_IsContinuous_EvenWhenRetargetedMidway()
        {
            var s = new CameraSettings();
            var rig = new CameraRig(s, s.Get(CameraState.Cruise));
            var slope = new ConstantSlope(10000, 0.2f, SurfaceId.Grass);
            rig.SetTarget(s.Get(CameraState.Vista), s.Get(CameraState.Vista).blendInSeconds);
            float last = rig.FieldOfView;
            float maxStep = 0f;
            for (int i = 0; i < 300; i++)
            {
                if (i == 60) rig.SetTarget(s.Get(CameraState.Danger), s.Get(CameraState.Danger).blendInSeconds);
                rig.Update(i * 0.1, slope, Dt, false);
                maxStep = Math.Max(maxStep, Math.Abs(rig.FieldOfView - last));
                last = rig.FieldOfView;
            }
            Assert.Less(maxStep, 0.5f, "camera cut");
            Assert.AreEqual(s.Get(CameraState.Danger).fieldOfView, rig.FieldOfView, 1e-3f);
            Assert.AreEqual(s.Get(CameraState.Danger).distance, rig.Distance, 1e-3f);
        }

        [Test]
        public void ReducedMotion_LocksFramingToCruise()
        {
            var s = new CameraSettings();
            CameraStateProfile boost = FramingMath.Resolve(s, CameraState.Boost, BiomeId.MeadowSea, true);
            CameraStateProfile cruise = FramingMath.Resolve(s, CameraState.Cruise, BiomeId.MeadowSea, true);
            Assert.AreEqual(cruise.fieldOfView, boost.fieldOfView);
            Assert.AreEqual(cruise.distance, boost.distance);
            Assert.AreEqual(CameraState.Boost, boost.state);
        }

        [Test]
        public void Mix_FollowsCameraStateAndBiome()
        {
            var sig = new FakeSignals();
            var mix = new MixDirector(new AudioMixSettings());
            CameraDirectorCore d = Director(sig, mix);
            Assert.AreEqual("Cruise", mix.CurrentSnapshot.snapshotName);
            sig.Value.Rider = RiderState.Crashed;
            d.Tick(0d, Dt);
            Assert.AreEqual("FailureHold", mix.CurrentSnapshot.snapshotName);
            d.SetBiome(BiomeId.PineHollow);
            Assert.AreEqual(BiomeId.PineHollow, mix.CurrentBiome);
        }

        [Test]
        public void LiveRide_KeepsRevealFraming_ThroughJumpsAndBlends()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            var cam = new CameraSettings();
            var rider = new RiderSettings();
            var speed = new SpeedSettings();
            JumpModel jump = JumpModel.ForAllVehicles(rider, speed, new VehicleSettings());
            List<Chunk> stretch = new ChunkGenerator(content, fair, new BiomeSettings(), jump).Generate(2, BiomeId.MeadowSea, content.chunksPerStretch);
            var slope = new StretchSlope(stretch);
            var kin = new KinematicRider(rider, speed, slope, new SurfaceTable(new SurfaceSettings()));
            var pilot = new BotPilot(stretch, fair.botTakeoffSeconds);
            var input = new ScriptedInput();
            var signals = new RiderCameraSignals(kin, stretch);
            var d = new CameraDirectorCore(cam, slope, signals, null, BiomeId.MeadowSea, false);
            var states = new HashSet<CameraState>();
            int steps = 0;
            while (kin.Position.Distance < slope.Length - cam.Get(CameraState.Vista).lookAhead && steps < 200000)
            {
                bool grounded = kin.State == RiderState.Grounded || kin.State == RiderState.Tucking;
                pilot.Drive(input, kin.Position.Distance, kin.Speed, grounded);
                kin.FixedStep(input, Dt);
                // Exercise blends: landslide pressure and a vista window come and go.
                signals.LandslideSeconds = steps % 900 < 150 ? 1f : float.PositiveInfinity;
                signals.BoostActive = steps % 1300 > 1200;
                CameraPose pose = d.Tick(kin.Position.Distance, Dt);
                steps++;
                states.Add(d.Current);
                if (cam.Get(d.Current).revealExempt) continue;

                CameraRig r = d.Rig;
                double halfH = r.Distance * Math.Tan(r.FieldOfView * Math.PI / 360.0);
                double halfW = halfH * cam.minAspect;
                Assert.GreaterOrEqual(halfW + r.LookAhead, fair.visibleAheadMeters, "reveal framing lost at step " + steps);
                double riderX = (kin.Position.Distance - pose.X) / halfW;
                double riderY = (kin.Position.Height - pose.Y) / halfH;
                Assert.GreaterOrEqual(riderX, -cam.maxRiderBehindCenter, "rider left the frame at step " + steps);
                Assert.LessOrEqual(Math.Abs(riderY), cam.safeScreenExtent, "rider vertical out of frame at step " + steps);
            }
            Assert.IsTrue(states.Contains(CameraState.Air));
            Assert.IsTrue(states.Contains(CameraState.Danger));
            Assert.IsTrue(states.Contains(CameraState.Boost));
        }
    }
}
