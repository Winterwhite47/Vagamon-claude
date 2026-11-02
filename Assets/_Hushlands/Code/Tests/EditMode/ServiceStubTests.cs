using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class ServiceStubTests
    {
        private static float Deg(float d)
        {
            return d * (float)(Math.PI / 180.0);
        }

        private static KinematicRider MakeRider(float slopeDeg, out ScriptedInput input)
        {
            input = new ScriptedInput();
            return new KinematicRider(new RiderSettings(), new SpeedSettings(),
                new ConstantSlope(10000, Deg(slopeDeg), SurfaceId.Grass), new SurfaceTable(new SurfaceSettings()));
        }

        [Test]
        public void SpeedModel_StaysInCanonRange()
        {
            var m = new SpeedModel(new SpeedSettings());
            float v = 6f;
            for (int i = 0; i < 100000; i++) v = m.Step(v, Deg(30f), 0f, true, i * 0.01, 0.01f);
            Assert.LessOrEqual(v, 18f);
            for (int i = 0; i < 10000; i++) v = m.Step(v, Deg(-20f), 0.5f, false, 0, 0.01f);
            Assert.GreaterOrEqual(v, 6f);
        }

        [Test]
        public void Rider_GroundedMovesForward_TapJumps_Lands()
        {
            KinematicRider r = MakeRider(10f, out ScriptedInput input);
            for (int i = 0; i < 100; i++) r.FixedStep(input, 0.01f);
            Assert.Greater(r.Position.Distance, 5d);
            Assert.AreEqual(RiderState.Grounded, r.State);

            input.TapThisStep = true;
            r.FixedStep(input, 0.01f);
            input.TapThisStep = false;
            Assert.AreEqual(RiderState.Airborne, r.State);
            for (int i = 0; i < 500 && r.State != RiderState.Grounded; i++) r.FixedStep(input, 0.01f);
            Assert.AreEqual(RiderState.Grounded, r.State);
        }

        [Test]
        public void Rider_HoldOnGround_Tucks()
        {
            KinematicRider r = MakeRider(10f, out ScriptedInput input);
            input.IsHeld = true;
            input.HeldSeconds = 1f;
            r.FixedStep(input, 0.01f);
            Assert.AreEqual(RiderState.Tucking, r.State);
        }

        [Test]
        public void Rider_HalfFlipLanding_Crashes()
        {
            KinematicRider r = MakeRider(0f, out ScriptedInput input);
            var settings = new RiderSettings();
            CrashCause? cause = null;
            r.Crashed += c => cause = c;
            input.TapThisStep = true;
            r.FixedStep(input, 0.01f);
            input.TapThisStep = false;
            input.IsHeld = true;
            input.HeldSeconds = 0f;
            float holdSeconds = settings.holdThresholdSeconds + 180f / settings.flipDegreesPerSecond;
            for (float t = 0; t < holdSeconds; t += 0.01f)
            {
                input.HeldSeconds += 0.01f;
                r.FixedStep(input, 0.01f);
            }
            input.IsHeld = false;
            for (int i = 0; i < 500 && r.State != RiderState.Crashed && r.State != RiderState.Grounded; i++) r.FixedStep(input, 0.01f);
            Assert.AreEqual(RiderState.Crashed, r.State);
            Assert.AreEqual(CrashCause.BadLanding, cause);
        }

        [Test]
        public void Assists_OfferedAfterThreeFailures_ClearAfterSuccesses()
        {
            var a = new AssistService(new FailureSettings(), new AssistSettings());
            a.OnFailure(1);
            a.OnFailure(2);
            Assert.IsFalse(a.AssistsOffered);
            a.OnFailure(3);
            Assert.IsTrue(a.AssistsOffered);
            a.Accept();
            Assert.IsTrue(a.AssistsActive);
            a.OnSuccess();
            a.OnSuccess();
            Assert.IsFalse(a.AssistsActive);
        }

        [Test]
        public void Ducklings_NeverLost_CheckpointRestore()
        {
            var d = new DucklingNestService();
            d.Register(1, 0);
            d.Register(2, 1);
            d.Register(3, 2);
            d.Rescue(1);
            d.MarkPassed(2);
            d.OnCrash();
            Assert.IsTrue(d.NestCoversClosed);
            d.RestoreToCheckpoint(1);
            Assert.AreEqual(DucklingGroupStatus.Rescued, d.StatusOf(1));
            Assert.AreEqual(DucklingGroupStatus.Waiting, d.StatusOf(2));
            Assert.AreEqual(DucklingGroupStatus.Ahead, d.StatusOf(3));
            Assert.IsFalse(d.NestCoversClosed);
            foreach (DucklingGroupStatus s in Enum.GetValues(typeof(DucklingGroupStatus)))
            {
                StringAssert.DoesNotContain("Lost", s.ToString());
            }
        }

        [Test]
        public void Failure_RetryRestoresCheckpoint()
        {
            KinematicRider r = MakeRider(10f, out ScriptedInput input);
            var cps = new CheckpointService();
            var assists = new AssistService(new FailureSettings(), new AssistSettings());
            var ducks = new DucklingNestService();
            var f = new FailureService(r, new SpeedModel(new SpeedSettings()), cps, assists, ducks);
            cps.Reach(0, new SlopePoint(50, -5f));
            for (int i = 0; i < 3; i++) f.ReportCrash(CrashCause.Obstacle);
            Assert.IsTrue(assists.AssistsOffered);
            f.InstantRetry();
            Assert.AreEqual(50d, r.Position.Distance, 1e-9);
            Assert.AreEqual(RiderState.Grounded, r.State);
            f.ReportCheckpointCleared();
            Assert.AreEqual(0, f.ConsecutiveFailures);
        }

        [Test]
        public void Camera_HighestPriorityWins_AndBiomeModifies()
        {
            var settings = new CameraSettings();
            var cam = new CameraStateMachine(settings);
            var seen = new List<CameraState>();
            cam.StateChanged += (a, b) => seen.Add(b);
            cam.Request(CameraState.Air);
            cam.Request(CameraState.Danger);
            cam.Request(CameraState.Vista);
            cam.Commit();
            Assert.AreEqual(CameraState.Danger, cam.Current);
            cam.Request(CameraState.Danger);
            cam.Commit();
            Assert.AreEqual(1, seen.Count);

            cam.SetBiome(BiomeId.PineHollow);
            CameraStateProfile p = cam.ResolvedProfile();
            Assert.AreEqual(settings.Get(CameraState.Danger).distance * settings.Get(BiomeId.PineHollow).distanceScale, p.distance, 1e-5f);
        }

        [Test]
        public void Score_And_Save_And_Pickups()
        {
            var score = new ScoreService(new ScoreSettings());
            score.AddCoins(3);
            score.AddFlips(1);
            Assert.AreEqual(3, score.SpiceCoins);
            Assert.AreEqual(3 * 10 + 100, score.Score);

            var save = new MemorySaveService();
            save.Save("best", 123L);
            Assert.IsTrue(save.TryLoad("best", out long best));
            Assert.AreEqual(123L, best);
            Assert.IsFalse(save.TryLoad("best", out string _));

            int expired = 0;
            var lantern = new TimedPickupEffect(new PickupSettings().Get(PickupId.MistLantern), null, () => expired++);
            lantern.Apply();
            Assert.IsTrue(lantern.Active);
            for (int i = 0; i < 1000; i++) lantern.Tick(0.01f);
            Assert.IsFalse(lantern.Active);
            Assert.AreEqual(1, expired);
        }

        [Test]
        public void Mix_FollowsCameraState()
        {
            var mix = new MixDirector(new AudioMixSettings());
            mix.ApplySnapshot(CameraState.Vista, BiomeId.MeadowSea);
            Assert.AreEqual("Vista", mix.CurrentSnapshot.snapshotName);
            mix.SetMistDensity(1f);
            Assert.AreEqual(new AudioMixSettings().mistDenseCutoffHz, mix.LowPassCutoffHz, 1f);
        }
    }
}
