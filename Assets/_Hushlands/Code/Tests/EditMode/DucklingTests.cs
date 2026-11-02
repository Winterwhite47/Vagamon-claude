using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class DucklingTests
    {
        private const float Dt = 0.01f;

        private const string Track =
            "chunk a MeadowSea 0 300 10\nsurface 0 300 Grass\n" +
            "feature Checkpoint 5 0 0 0 0 0 0 0\n" +
            "feature DucklingNest 40 0 0 0 0 0 0 0\n" +
            "feature Checkpoint 100 0 0 0 0 0 0 0\n" +
            "feature DucklingNest 140 0 0 0 1 0 0 0\n" +
            "feature DucklingNest 200 0 0 0 2 0 0 0\nend\n";

        private sealed class Rig
        {
            public List<Chunk> Chunks;
            public StretchSlope Slope;
            public KinematicRider Rider;
            public ScriptedInput Input = new ScriptedInput();
            public DucklingNestService Nests = new DucklingNestService();
            public RunSession Session;
        }

        private static Rig Make()
        {
            var r = new Rig { Chunks = ChunkSerializer.Read(Track) };
            r.Slope = new StretchSlope(r.Chunks);
            r.Rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), r.Slope, new SurfaceTable(new SurfaceSettings()));
            r.Session = RunSession.Create(new RunSettingsBundle(), 1.1f, 0.6f, 1, r.Rider, r.Chunks, r.Slope, r.Nests, null,
                new RiderCameraSignals(r.Rider, r.Chunks));
            return r;
        }

        private static List<string> Step(Rig r, int steps, bool jumpAt = false, double jumpX = 0)
        {
            var problems = new List<string>();
            for (int i = 0; i < steps; i++)
            {
                r.Input.TapThisStep = jumpAt && r.Rider.Position.Distance >= jumpX && r.Rider.State == RiderState.Grounded;
                if (r.Input.TapThisStep) jumpAt = false;
                r.Rider.FixedStep(r.Input, Dt);
                r.Session.Tick(r.Input, Dt);
                bool closed = r.Nests.NestCoversClosed;
                var view = new DucklingView { NestCoversVisible = true, BasketLidVisible = true, DrawnInNests = closed ? 0 : 1 };
                problems.AddRange(DucklingSafetyAudit.Check(r.Nests, r.Session.Flow.Phase, r.Rider.State, view));
            }
            return problems;
        }

        private static void RideTo(Rig r, double x)
        {
            int guard = 0;
            while (r.Rider.Position.Distance < x && guard++ < 100000) Step(r, 1);
        }

        [Test]
        public void RidingPast_OnGround_Rescues_AndScores()
        {
            Rig r = Make();
            RideTo(r, 45);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(0));
            Assert.AreEqual(1, r.Session.Score.Rescues);
            Assert.GreaterOrEqual(r.Session.Score.Score, new ScoreSettings().pointsPerDucklingGroup);
            Assert.AreEqual(new DucklingSettings().ducklingsPerGroup, r.Session.Ducklings.RescuedDucklings);
            Assert.IsTrue(new MessageSettings().IsClean(r.Session.Ducklings.LastRescueLine));
        }

        [Test]
        public void JumpingOverNest_MissesIt_ItWaits()
        {
            Rig r = Make();
            var jump = new JumpModel(new RiderSettings().jumpVelocity, 9.81, 6, 18);
            // Take off just before the rescue window (nest 40 m, radius 2.5 m) so the arc stays high across all of it.
            RideTo(r, 36.5);
            Step(r, 1, true, 36.5);
            RideTo(r, 50);
            Assert.Greater(jump.JumpDistance(6, System.Math.Tan(10 * System.Math.PI / 180)), 10);
            Assert.AreEqual(DucklingGroupStatus.Waiting, r.Nests.StatusOf(0));
            Assert.AreEqual(0, r.Session.Score.Rescues);
        }

        [Test]
        public void Crash_ClosesCoversAtOnce_RetryRestoresAhead_RescuedKept_AuditClean()
        {
            Rig r = Make();
            RideTo(r, 45);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(0));
            RideTo(r, 150);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(1));
            r.Rider.ForceCrash(CrashCause.Obstacle);
            Assert.IsTrue(r.Nests.NestCoversClosed, "covers must shut on the crash step");
            List<string> problems = Step(r, 200);
            CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(FlowPhase.Riding, r.Session.Flow.Phase);
            Assert.IsFalse(r.Nests.NestCoversClosed);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(0));
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(1), "rescued group after the checkpoint stays rescued");
            Assert.AreEqual(DucklingGroupStatus.Ahead, r.Nests.StatusOf(2));
            Assert.AreEqual(2, r.Session.Score.Rescues, "re-riding must not re-score rescues");
        }

        [Test]
        public void MissedNest_AfterRetryCheckpoint_CanBeRescuedAgain()
        {
            Rig r = Make();
            RideTo(r, 136.5);
            Step(r, 1, true, 136.5);
            RideTo(r, 160);
            Assert.AreEqual(DucklingGroupStatus.Waiting, r.Nests.StatusOf(1));
            r.Rider.ForceCrash(CrashCause.Gap);
            Step(r, 200);
            Assert.AreEqual(DucklingGroupStatus.Ahead, r.Nests.StatusOf(1), "never lost: ahead again after retry");
            RideTo(r, 145);
            Assert.AreEqual(DucklingGroupStatus.Rescued, r.Nests.StatusOf(1));
        }

        [Test]
        public void Audit_FlagsUnsafeStates()
        {
            var nests = new DucklingNestService();
            nests.Register(0, 0);
            var safeView = new DucklingView { NestCoversVisible = true, BasketLidVisible = true };
            Assert.IsNotEmpty(DucklingSafetyAudit.Check(nests, FlowPhase.FailureHold, RiderState.Crashed, safeView),
                "covers open during failure must be flagged");
            nests.OnCrash();
            CollectionAssert.IsEmpty(DucklingSafetyAudit.Check(nests, FlowPhase.FailureHold, RiderState.Crashed, safeView));
            var visible = new DucklingView { NestCoversVisible = true, BasketLidVisible = true, DrawnInBasket = 2 };
            Assert.IsNotEmpty(DucklingSafetyAudit.Check(nests, FlowPhase.FailureHold, RiderState.Crashed, visible));
            var outside = new DucklingView { NestCoversVisible = true, BasketLidVisible = true, DrawnOutsideSafePlaces = 1 };
            nests.OpenCovers();
            Assert.IsNotEmpty(DucklingSafetyAudit.Check(nests, FlowPhase.Riding, RiderState.Grounded, outside));
        }

        [Test]
        public void StatusEnum_HasNoUnsafeStates()
        {
            foreach (string n in System.Enum.GetNames(typeof(DucklingGroupStatus)))
            {
                foreach (string bad in new[] { "Lost", "Injur", "Dead", "Drown", "Fall", "Hurt" })
                    StringAssert.DoesNotContain(bad, n);
            }
        }

        [Test]
        public void ContentNests_NeverNearHazards_AcrossValidationStretches()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            JumpModel jump = JumpModel.ForDefaultVehicle(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            var gen = new ChunkGenerator(content, fair, new BiomeSettings(), jump);
            var checker = new FairnessChecker(fair, content, jump);
            for (int seed = 1; seed <= 50; seed++)
            {
                foreach (FairnessViolation v in checker.Check(gen.Generate(seed, BiomeId.MeadowSea, content.chunksPerStretch)))
                    Assert.AreNotEqual(FairnessChecker.RuleNestSafe, v.Rule, "seed " + seed);
            }
        }
    }
}
