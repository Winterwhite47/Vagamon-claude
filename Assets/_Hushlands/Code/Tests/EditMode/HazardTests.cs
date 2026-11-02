using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class HazardTests
    {
        private const float Dt = 0.01f;

        [Test]
        public void Landslide_RiderAtCruise_PullsAhead_NeverCaught()
        {
            var h = new HazardSettings();
            var speed = new SpeedSettings();
            var slide = new LandslideChaser(h, 0);
            double x = 0;
            double minGap = double.MaxValue;
            for (int i = 0; i < 60000; i++)
            {
                float target = speed.TargetAt(i * Dt);
                x += target * Dt;
                Assert.IsFalse(slide.Step(x, target, Dt, 0.5f), "caught at step " + i);
                if (slide.Active) minGap = Math.Min(minGap, slide.Gap(x));
            }
            Assert.Greater(minGap, h.landslideDangerGap);
        }

        [Test]
        public void Landslide_CatchesStalledRider_WithDangerFirst()
        {
            var h = new HazardSettings();
            var speed = new SpeedSettings();
            var slide = new LandslideChaser(h, 0);
            double x = 0;
            float firstDanger = -1f;
            float t = 0f;
            bool caught = false;
            while (t < 600f && !caught)
            {
                t += Dt;
                x += speed.minSpeed * Dt;
                caught = slide.Step(x, speed.TargetAt(t), Dt, 0.5f);
                if (firstDanger < 0f && slide.SecondsToContact(x, speed.minSpeed) < new CameraSettings().dangerLandslideSeconds)
                    firstDanger = t;
            }
            Assert.IsTrue(caught, "a stalled rider must eventually be caught");
            Assert.Greater(firstDanger, 0f);
            Assert.Greater(t - firstDanger, 5f, "Danger must show well before the catch");
        }

        [Test]
        public void Landslide_ResetsBehindRetryPoint_WithCalmDelay()
        {
            var h = new HazardSettings();
            var slide = new LandslideChaser(h, 0);
            for (int i = 0; i < 1000; i++) slide.Step(100, 8, Dt, 0.5f);
            slide.Reset(200, h.landslideRetryGap);
            Assert.AreEqual(200 - h.landslideRetryGap, slide.Front, 1e-9);
            Assert.IsFalse(slide.Active);
            Assert.AreEqual(float.PositiveInfinity, slide.SecondsToContact(200, 6));
        }

        [Test]
        public void Collapse_WarnsForOneSecond_ThenOpens_BeforeTakeoffAtMaxSpeed()
        {
            var fair = new FairnessSettings();
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            List<Chunk> s = null;
            for (int seed = 1; seed < 100 && s == null; seed++)
            {
                List<Chunk> cand = new ChunkGenerator(content, fair, new BiomeSettings(), jump).Generate(seed, BiomeId.MeadowSea, content.chunksPerStretch);
                foreach (Chunk c in cand)
                    foreach (ChunkFeature f in c.Features)
                        if (f.kind == FeatureKind.CollapseZone) s = cand;
            }
            Assert.IsNotNull(s);
            var ctl = new CollapseController(fair, new HazardSettings(), s);
            CollapseController.Zone z = ctl.Zones[0];
            double takeoffStart = z.Start - jump.TakeoffZoneLength(fair.takeoffWindowMaxSeconds);
            double x = z.Start - z.WarningLead - 5;
            float warnAt = -1f;
            float t = 0f;
            double openedAt = double.NaN;
            while (x < z.End)
            {
                ctl.Step(x, Dt);
                if (warnAt < 0f && z.State == CollapseState.Warning) warnAt = t;
                if (double.IsNaN(openedAt) && z.State == CollapseState.Open) openedAt = x;
                x += jump.MaxSpeed * Dt;
                t += Dt;
            }
            Assert.GreaterOrEqual(warnAt, 0f);
            Assert.IsFalse(double.IsNaN(openedAt));
            Assert.LessOrEqual(openedAt, takeoffStart + jump.MaxSpeed * Dt, "zone must open before the takeoff zone at max speed");
            ctl.ResetAhead(z.Start - 50);
            Assert.AreEqual(CollapseState.Intact, z.State);
        }

        [Test]
        public void Squall_IsSmooth_Bounded_Deterministic()
        {
            var h = new HazardSettings();
            var a = new SquallDirector(h, 5);
            var b = new SquallDirector(h, 5);
            float last = 0f;
            float maxStep = 0f;
            float peak = 0f;
            for (int i = 0; i < 20000; i++)
            {
                a.Step(Dt);
                b.Step(Dt);
                Assert.AreEqual(a.Intensity, b.Intensity);
                Assert.That(a.Intensity, Is.InRange(0f, 1f));
                maxStep = Math.Max(maxStep, Math.Abs(a.Intensity - last));
                last = a.Intensity;
                peak = Math.Max(peak, a.Intensity);
            }
            Assert.Greater(a.Count, 1);
            Assert.AreEqual(1f, peak, 1e-3f);
            Assert.Less(maxStep, 0.02f, "squall must ramp, not snap");
            Assert.LessOrEqual(new SquallDirector(h, 1).SpeedPenalty, h.squallSpeedPenalty);
        }

        [Test]
        public void Squall_FogStaysWithinMistFairness()
        {
            var h = new HazardSettings();
            List<string> errors = MistVisibility.Check(new LookSettings(), new CameraSettings(), new FairnessSettings(), h.squallFogScale);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Session_BotRidesGoldenStretch_WithHazards_NeverCaught()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            List<Chunk> stretch = new ChunkGenerator(content, fair, new BiomeSettings(), jump).Generate(2, BiomeId.MeadowSea, content.chunksPerStretch);
            var slope = new StretchSlope(stretch);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var signals = new RiderCameraSignals(rider, stretch);
            RunSession session = RunSession.Create(new RunSettingsBundle(), 1.1f, 0.6f, 2, rider, stretch, slope,
                new DucklingNestService(), null, signals);
            var pilot = new BotPilot(stretch, fair.botTakeoffSeconds);
            var input = new ScriptedInput();
            bool sawWarning = false;
            int guard = 0;
            while (rider.Position.Distance < slope.Length && guard++ < 300000)
            {
                bool grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                pilot.Drive(input, rider.Position.Distance, rider.Speed, grounded);
                rider.FixedStep(input, Dt);
                session.Tick(input, Dt);
                sawWarning |= signals.CollapseWarningActive;
            }
            Assert.AreEqual(0, session.Flow.Retries, "bot crashed (landslide or hazard)");
            Assert.IsTrue(sawWarning, "collapse warning should drive the Danger signal");
            Assert.Greater(session.Squall.Count, 0);
        }

        [Test]
        public void Session_LandslideCatch_IsALandslideCrash_ThenRetryResetsIt()
        {
            List<Chunk> chunks = ChunkSerializer.Read("chunk a MeadowSea 0 400 10\nsurface 0 400 Grass\nfeature Checkpoint 5 0 0 0 0 0 0 0\nend\n");
            var slope = new StretchSlope(chunks);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var b = new RunSettingsBundle();
            b.Hazards.landslideStartDelaySeconds = 0f;
            b.Hazards.landslideStartGap = 0.4f;
            RunSession session = RunSession.Create(b, 1.1f, 0.6f, 1, rider, chunks, slope, new DucklingNestService(), null,
                new RiderCameraSignals(rider, chunks));
            CrashCause? cause = null;
            rider.Crashed += c => cause = c;
            var input = new ScriptedInput();
            rider.FixedStep(input, Dt);
            session.Tick(input, Dt);
            Assert.AreEqual(CrashCause.Landslide, cause);
            for (int i = 0; i < 200; i++)
            {
                rider.FixedStep(input, Dt);
                session.Tick(input, Dt);
            }
            Assert.AreEqual(FlowPhase.Riding, session.Flow.Phase);
            Assert.GreaterOrEqual(session.Landslide.Gap(rider.Position.Distance), b.Hazards.landslideRetryGap - 3);
        }
    }
}
