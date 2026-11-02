using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class RunFlowTests
    {
        private const float Dt = 0.01f;

        private sealed class FakeCamera : IFlowCamera
        {
            public int Snaps;

            public void SnapToRider()
            {
                Snaps++;
            }
        }

        private sealed class Rig
        {
            public List<Chunk> Chunks;
            public StretchSlope Slope;
            public KinematicRider Rider;
            public ScriptedInput Input = new ScriptedInput();
            public DucklingNestService Ducks = new DucklingNestService();
            public FakeCamera Camera = new FakeCamera();
            public RiderCameraSignals Signals;
            public RunFlow Flow;
            public FailureSettings Failure = new FailureSettings();
            public AssistSettings Assist = new AssistSettings();
            public RiderSettings RiderSettings = new RiderSettings();
        }

        private const string Track =
            "chunk a MeadowSea 0 120 10\nsurface 0 120 Grass\n" +
            // First checkpoint sits past the ~2 m a retried rider rolls before the next scripted crash.
            "feature Checkpoint 5 0 0 0 0 0 0 0\n" +
            "feature DucklingNest 20 0 0 0 0 0 0 0\nend\n" +
            "chunk b MeadowSea 0 120 10\nsurface 0 120 Grass\n" +
            "feature Checkpoint 2 0 0 0 0 0 0 0\n" +
            "feature DucklingNest 60 0 0 0 1 0 0 0\nend\n" +
            "chunk c MeadowSea 0 400 10\nsurface 0 400 Grass\n" +
            "feature Checkpoint 2 0 0 0 0 0 0 0\nend\n";

        private static Rig Make()
        {
            var r = new Rig();
            r.Chunks = ChunkSerializer.Read(Track);
            r.Slope = new StretchSlope(r.Chunks);
            r.Rider = new KinematicRider(r.RiderSettings, new SpeedSettings(), r.Slope, new SurfaceTable(new SurfaceSettings()));
            r.Signals = new RiderCameraSignals(r.Rider, r.Chunks);
            r.Flow = new RunFlow(r.Failure, r.Assist, new MessageSettings(), 7, r.Rider, r.Rider,
                new SpeedModel(new SpeedSettings()), r.Chunks, r.Slope, r.Ducks, r.Camera, r.Signals);
            return r;
        }

        private static void Ride(Rig r, double untilDistance)
        {
            int guard = 0;
            while (r.Rider.Position.Distance < untilDistance && guard++ < 100000)
            {
                r.Rider.FixedStep(r.Input, Dt);
                r.Flow.Tick(r.Input, Dt);
            }
        }

        private static void Wait(Rig r, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                r.Rider.FixedStep(r.Input, Dt);
                r.Flow.Tick(r.Input, Dt);
                r.Input.TapThisStep = false;
            }
        }

        [Test]
        public void Crash_Holds_ThenRetriesAtLastCheckpoint()
        {
            Rig r = Make();
            Ride(r, 150);
            Assert.AreEqual(1, r.Flow.Checkpoints.LastCheckpointIndex);
            r.Rider.ForceCrash(CrashCause.Obstacle);
            Assert.AreEqual(FlowPhase.FailureHold, r.Flow.Phase);
            Assert.IsTrue(r.Signals.FailureHold, "camera FailureHold signal");
            Assert.IsTrue(r.Flow.Message.Text.Length > 0);
            Assert.AreEqual(new MessageSettings().retryHint, r.Flow.Message.Hint);

            Wait(r, r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds + 0.05f);
            Assert.AreEqual(FlowPhase.Riding, r.Flow.Phase);
            Assert.AreEqual(1, r.Flow.Retries);
            Assert.AreEqual(1, r.Camera.Snaps);
            Assert.IsFalse(r.Signals.FailureHold);
            double cp = r.Flow.Tracker.Points[1];
            Assert.Less(r.Rider.Position.Distance, cp + 15d, "retried far from checkpoint");
            Assert.GreaterOrEqual(r.Rider.Position.Distance, cp);
            Assert.AreNotEqual(RiderState.Crashed, r.Rider.State);
        }

        [Test]
        public void Crash_BeforeFirstCheckpointPassed_RetriesFromStart()
        {
            Rig r = Make();
            r.Rider.ForceCrash(CrashCause.Gap);
            Wait(r, r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds + 0.05f);
            Assert.AreEqual(FlowPhase.Riding, r.Flow.Phase);
            Assert.Less(r.Rider.Position.Distance, 10d);
        }

        [Test]
        public void Tap_SkipsHold_AfterMinimum_InstantRetry()
        {
            Rig r = Make();
            Ride(r, 50);
            r.Rider.ForceCrash(CrashCause.Obstacle);
            r.Input.TapThisStep = true;
            r.Flow.Tick(r.Input, Dt);
            Assert.AreEqual(FlowPhase.FailureHold, r.Flow.Phase, "tap before minimum must not skip");
            Wait(r, r.Failure.minHoldBeforeSkipSeconds);
            r.Input.TapThisStep = true;
            r.Flow.Tick(r.Input, Dt);
            r.Input.TapThisStep = false;
            Assert.AreEqual(FlowPhase.Retrying, r.Flow.Phase);
            Wait(r, r.Failure.retryTransitionSeconds + 0.05f);
            Assert.AreEqual(FlowPhase.Riding, r.Flow.Phase);
            Assert.Less(r.Failure.minHoldBeforeSkipSeconds + r.Failure.retryTransitionSeconds,
                r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds);
        }

        [Test]
        public void SecondCrash_DuringHold_IsIgnored()
        {
            Rig r = Make();
            r.Rider.ForceCrash(CrashCause.Obstacle);
            r.Rider.ForceCrash(CrashCause.Landslide);
            Assert.AreEqual(1, r.Flow.ConsecutiveFailures);
        }

        [Test]
        public void ThreeFailures_FreeAssistsOn_ClearAfterCheckpoints()
        {
            Rig r = Make();
            for (int i = 0; i < r.Failure.assistAfterFailures; i++)
            {
                Assert.IsFalse(r.Flow.AssistsActive);
                r.Rider.ForceCrash(CrashCause.BadLanding);
                Wait(r, r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds + 0.05f);
            }
            Assert.IsTrue(r.Flow.AssistsActive);
            Assert.AreEqual(r.Assist.landingToleranceBonusDegrees, r.Rider.LandingBonusDegrees);

            Ride(r, r.Flow.Tracker.Points[2] + 1d);
            Assert.IsFalse(r.Flow.AssistsActive, "assists should clear after successful checkpoints");
            Assert.AreEqual(0f, r.Rider.LandingBonusDegrees);
            Assert.AreEqual(0, r.Flow.ConsecutiveFailures);
        }

        [Test]
        public void AssistOffer_ShowsAssistMessage_Once()
        {
            Rig r = Make();
            var offers = 0;
            r.Flow.MessageShown += m => { if (m.AssistOffered) offers++; };
            for (int i = 0; i < r.Failure.assistAfterFailures + 2; i++)
            {
                r.Rider.ForceCrash(CrashCause.Obstacle);
                Wait(r, r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds + 0.05f);
            }
            Assert.AreEqual(1, offers);
        }

        [Test]
        public void Assists_SlowCruise_SoRiderSeesMoreReveal()
        {
            var speed = new SpeedSettings();
            var model = new SpeedModel(speed);
            float plain = model.TargetSpeed(600);
            model.TargetScale = new AssistSettings().speedScale;
            Assert.Less(model.TargetSpeed(600), plain);
            Assert.GreaterOrEqual(model.TargetSpeed(0), speed.minSpeed);
            var fair = new FairnessSettings();
            float assisted = speed.maxSpeed * new AssistSettings().speedScale;
            Assert.GreaterOrEqual(fair.visibleAheadMeters / assisted, fair.obstacleRevealSeconds + new AssistSettings().extraRevealSeconds);
        }

        [Test]
        public void Ducklings_RestoredOnRetry_RescuedKept_NeverLost()
        {
            Rig r = Make();
            Ride(r, 130);
            r.Ducks.Rescue(0);
            Ride(r, 190);
            r.Ducks.MarkPassed(1);
            r.Rider.ForceCrash(CrashCause.Obstacle);
            Assert.IsTrue(r.Ducks.NestCoversClosed);
            Wait(r, r.Failure.failureHoldSeconds + r.Failure.retryTransitionSeconds + 0.05f);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Ducks.StatusOf(0));
            Assert.AreEqual(DucklingGroupStatus.Ahead, r.Ducks.StatusOf(1), "nest after the retry checkpoint is rescuable again");
            Assert.IsFalse(r.Ducks.NestCoversClosed);
        }

        [Test]
        public void Messages_CleanVaried_NoImmediateRepeat()
        {
            var s = new MessageSettings();
            var picker = new MessagePicker(s, 3);
            foreach (CrashCause c in System.Enum.GetValues(typeof(CrashCause)))
            {
                string last = null;
                for (int i = 0; i < 50; i++)
                {
                    string m = picker.Pick(c);
                    Assert.IsTrue(s.IsClean(m), m);
                    if (s.For(c).Length > 1) Assert.AreNotEqual(last, m);
                    last = m;
                }
            }
            Assert.IsFalse(s.IsClean("The duckling fell in the lake"));
            Assert.IsFalse(s.IsClean("You crashed"));
        }
    }
}
