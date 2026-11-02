using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class ProgressionTests
    {
        private static ProgressionService Make(ISaveService save = null)
        {
            return new ProgressionService(save ?? new MemorySaveService(), new VehicleSettings(), new GoalSettings());
        }

        [Test]
        public void Profile_AppliesRun_BestsAndTotals()
        {
            ProgressionService p = Make();
            p.Profile.ApplyRun(new RunSummary(1200, 800, 30, 2, 1, 0, 1, 500));
            Assert.AreEqual(1, p.Profile.Runs);
            Assert.AreEqual(1200, p.Profile.BestScore);
            Assert.IsTrue(p.Profile.NewBest);
            p.Profile.ApplyRun(new RunSummary(900, 1000, 10, 0, 0, 0, 0, 1000));
            Assert.AreEqual(1200, p.Profile.BestScore);
            Assert.IsFalse(p.Profile.NewBest);
            Assert.AreEqual(1000, p.Profile.BestDistance, 1e-9);
            Assert.AreEqual(1800, p.Profile.LifetimeDistance, 1e-9);
            Assert.AreEqual(40, p.Profile.LifetimeCoins);
        }

        [Test]
        public void WaitingGroups_ComeHome_NeverLost()
        {
            ProgressionService p = Make();
            p.Profile.ApplyRun(new RunSummary(0, 100, 0, 0, 0, 3, 0, 0));
            Assert.AreEqual(3, p.Profile.GroupsWaiting);
            p.Profile.ApplyRun(new RunSummary(0, 100, 0, 0, 2, 0, 0, 0));
            Assert.AreEqual(1, p.Profile.GroupsWaiting);
            Assert.AreEqual(2, p.Profile.Homecomings);
            p.Profile.ApplyRun(new RunSummary(0, 100, 0, 0, 5, 1, 0, 0));
            Assert.AreEqual(1, p.Profile.GroupsWaiting, "one came home, one new group is waiting");
            Assert.AreEqual(3, p.Profile.Homecomings);
            Assert.GreaterOrEqual(p.Profile.GroupsWaiting, 0);
        }

        [Test]
        public void Goals_ThreeActive_CompleteRewardAndRotate()
        {
            ProgressionService p = Make();
            var g = new GoalSettings();
            IReadOnlyList<GoalStatus> active = p.Goals.Active(null);
            Assert.AreEqual(g.activeSlots, active.Count);
            Assert.AreEqual("first_ride", active[0].Def.id);
            var stats = new RunStats { StartX = 0, FurthestX = 350, Flips = 1 };
            var completed = new List<string>();
            p.Goals.Completed += s => completed.Add(s.Def.id);
            p.Goals.Evaluate(stats);
            CollectionAssert.AreEquivalent(new[] { "first_ride", "first_flip" }, completed);
            Assert.AreEqual(g.goals[0].rewardCoins + g.goals[2].rewardCoins, p.Garage.LifetimeCoins);
            IReadOnlyList<GoalStatus> now = p.Goals.Active(stats);
            Assert.AreEqual("spice_run", now[0].Def.id);
            Assert.AreEqual("first_rescue", now[1].Def.id);
            Assert.AreEqual("steady", now[2].Def.id);
        }

        [Test]
        public void Goals_PersistAcrossReload_AndRepeatScaled()
        {
            var save = new MemorySaveService();
            ProgressionService p = Make(save);
            var g = new GoalSettings();
            var huge = new RunStats { StartX = 0, FurthestX = 1e7, CurrentClean = 1e7, Flips = 1000, Coins = 100000, Rescues = 1000 };
            for (int i = 0; i < 5; i++) p.Goals.Evaluate(huge);
            Assert.Greater(p.Goals.CompletedCount, g.goals.Length);
            var reloaded = Make(save);
            IReadOnlyList<GoalStatus> a = p.Goals.Active(null);
            IReadOnlyList<GoalStatus> b = reloaded.Goals.Active(null);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Def.id, b[i].Def.id);
                Assert.AreEqual(a[i].Target, b[i].Target);
                Assert.GreaterOrEqual(a[i].Target, a[i].Def.target, "repeat cycles never get easier");
            }
        }

        [Test]
        public void Goals_ConfigClean_NoVehicleRequirements()
        {
            var errors = new List<string>();
            new GoalSettings().Validate(errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            foreach (string n in System.Enum.GetNames(typeof(GoalKind)))
                StringAssert.DoesNotContain("Vehicle", n);
        }

        [Test]
        public void RunEnd_FlowsIntoProfileAndUnlocks()
        {
            List<Chunk> chunks = ChunkSerializer.Read(
                "chunk a MeadowSea 0 400 10\nsurface 0 400 Grass\nfeature Checkpoint 5 0 0 0 0 0 0 0\n" +
                "feature DucklingNest 60 0 0 0 0 0 0 0\nfeature Pickup 30 0 0 0 0 0 0 0\nend\n");
            var slope = new StretchSlope(chunks);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            RunSession session = RunSession.Create(new RunSettingsBundle(), 1.1f, 0.6f, 1, rider, chunks, slope,
                new DucklingNestService(), null, new RiderCameraSignals(rider, chunks));
            var input = new ScriptedInput();
            while (rider.Position.Distance < 350)
            {
                rider.FixedStep(input, 0.01f);
                session.Tick(input, 0.01f);
            }
            RunSummary sum = session.End();
            Assert.Greater(sum.Distance, 340);
            Assert.AreEqual(1, sum.Rescues);
            Assert.AreEqual(1, sum.Coins);
            Assert.AreEqual(0, sum.Crashes);
            Assert.Greater(sum.LongestClean, 340);

            ProgressionService p = Make();
            p.Complete(sum, session.Stats);
            Assert.AreEqual(1, p.Profile.Runs);
            Assert.AreEqual(1, p.Profile.GroupsRescued);
            Assert.Greater(p.Garage.LifetimeCoins, sum.Coins, "goal rewards add coins");
            long beforeTicks = session.Score.Score;
            session.Tick(input, 0.01f);
            Assert.AreEqual(beforeTicks, session.Score.Score, "ended run must not keep scoring");
        }
    }
}
