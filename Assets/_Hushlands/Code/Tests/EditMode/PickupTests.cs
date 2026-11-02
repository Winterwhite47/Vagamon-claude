using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class PickupTests
    {
        private const float Dt = 0.01f;
        private const float Hover = 1.1f;

        private sealed class Boost : IBoostable
        {
            public float Value;

            public void SetBoost(float metersPerSecond)
            {
                Value = metersPerSecond;
            }
        }

        private static List<Chunk> Track(params (double x, PickupId id)[] pickups)
        {
            string text = "chunk a MeadowSea 0 400 10\nsurface 0 400 Grass\nfeature Checkpoint 2 0 0 0 0 0 0 0\n";
            foreach (var p in pickups)
                text += "feature Pickup " + p.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 0 0 0 " + (int)p.id + " 0 0 0\n";
            return ChunkSerializer.Read(text + "end\n");
        }

        private static PickupSystem Make(List<Chunk> chunks, out ScoreService score, out Boost boost, out StretchSlope slope)
        {
            slope = new StretchSlope(chunks);
            score = new ScoreService(new ScoreSettings());
            boost = new Boost();
            return new PickupSystem(new PickupSettings(), score, boost, chunks, slope, Hover);
        }

        private static void RideTo(PickupSystem sys, StretchSlope slope, double from, double to)
        {
            for (double x = from; x <= to; x += 0.1) sys.Step(x, slope.HeightAt(x) + Hover, Dt);
        }

        [Test]
        public void Coin_CollectedOnTouch_AddsCoinAndScore()
        {
            PickupSystem sys = Make(Track((50, PickupId.SpiceCoin)), out ScoreService score, out _, out StretchSlope slope);
            RideTo(sys, slope, 40, 49);
            Assert.AreEqual(0, score.SpiceCoins);
            RideTo(sys, slope, 49, 52);
            Assert.AreEqual(1, score.SpiceCoins);
            Assert.AreEqual(new ScoreSettings().pointsPerCoin, score.Score);
            Assert.IsTrue(sys.Items[0].Collected);
        }

        [Test]
        public void Coin_OutOfReach_NotCollected()
        {
            PickupSystem sys = Make(Track((50, PickupId.SpiceCoin)), out ScoreService score, out _, out StretchSlope slope);
            for (double x = 40; x < 60; x += 0.1) sys.Step(x, slope.HeightAt(x) + Hover + 3, Dt);
            Assert.AreEqual(0, score.SpiceCoins);
        }

        [Test]
        public void Magnet_PullsCoins_FromBeyondReach()
        {
            var s = new PickupSettings();
            PickupSystem sys = Make(Track((20, PickupId.MagnetBell), (60, PickupId.SpiceCoin)), out ScoreService score, out _, out StretchSlope slope);
            RideTo(sys, slope, 15, 21);
            Assert.IsTrue(sys.MagnetActive);
            // Ride past the coin 3 m above it: out of touch reach, inside magnet radius.
            for (double x = 21; x < 70; x += 0.1) sys.Step(x, slope.HeightAt(x) + Hover + 3, Dt);
            Assert.Less(3.0, s.magnetRadius);
            Assert.AreEqual(1, score.SpiceCoins);
        }

        [Test]
        public void WindFeather_Boosts_ThenEnds()
        {
            var s = new PickupSettings();
            PickupSystem sys = Make(Track((20, PickupId.WindFeather)), out _, out Boost boost, out StretchSlope slope);
            RideTo(sys, slope, 15, 21);
            Assert.IsTrue(sys.WindActive);
            Assert.AreEqual(s.windSpeedBonus, boost.Value);
            float d = s.Get(PickupId.WindFeather).durationSeconds;
            for (float t = 0; t < d + 0.1f; t += Dt) sys.Step(1000, 0, Dt);
            Assert.IsFalse(sys.WindActive);
            Assert.AreEqual(0f, boost.Value);
        }

        [Test]
        public void Boost_NeverExceedsMaxSpeed()
        {
            var speed = new SpeedSettings();
            var m = new SpeedModel(speed) { TargetBonus = 100f };
            Assert.AreEqual(speed.maxSpeed, m.TargetSpeed(0));
            float v = speed.maxSpeed;
            for (int i = 0; i < 1000; i++) v = m.Step(v, 0.4f, 0f, true, 0, Dt);
            Assert.LessOrEqual(v, speed.maxSpeed);
        }

        [Test]
        public void TeaFlask_DoublesScore()
        {
            var ps = new PickupSettings();
            PickupSystem sys = Make(Track((20, PickupId.TeaFlask), (30, PickupId.SpiceCoin)), out ScoreService score, out _, out StretchSlope slope);
            RideTo(sys, slope, 15, 32);
            Assert.AreEqual(new ScoreSettings().pointsPerCoin * ps.teaScoreMultiplier, score.Score, 1e-6);
        }

        [Test]
        public void GoldenMistbell_GivesCoinsAndBonus()
        {
            var ps = new PickupSettings();
            PickupSystem sys = Make(Track((30, PickupId.GoldenMistbell)), out ScoreService score, out _, out StretchSlope slope);
            RideTo(sys, slope, 25, 32);
            int coins = ps.Get(PickupId.GoldenMistbell).value;
            Assert.AreEqual(coins, score.SpiceCoins);
            Assert.AreEqual(coins * new ScoreSettings().pointsPerCoin + ps.mistbellScoreBonus, score.Score);
        }

        [Test]
        public void Lantern_ThinsMist_RefreshExtends()
        {
            var ps = new PickupSettings();
            PickupSystem sys = Make(Track((20, PickupId.MistLantern), (40, PickupId.MistLantern)), out _, out _, out StretchSlope slope);
            RideTo(sys, slope, 15, 21);
            Assert.AreEqual(ps.lanternMistScale, sys.MistScale);
            RideTo(sys, slope, 21, 41);
            Assert.AreEqual(ps.Get(PickupId.MistLantern).durationSeconds, sys.Remaining(PickupId.MistLantern), 0.3f, "second lantern should refresh to full");
        }

        [Test]
        public void Crash_EndsEffects()
        {
            PickupSystem sys = Make(Track((20, PickupId.WindFeather), (21, PickupId.TeaFlask)), out ScoreService score, out Boost boost, out StretchSlope slope);
            RideTo(sys, slope, 15, 23);
            Assert.IsTrue(sys.WindActive && sys.TeaActive);
            sys.OnCrash();
            Assert.IsFalse(sys.WindActive || sys.TeaActive);
            Assert.AreEqual(0f, boost.Value);
            Assert.AreEqual(1f, score.Multiplier);
        }

        [Test]
        public void Distance_ScoresNewGroundOnly()
        {
            var score = new ScoreService(new ScoreSettings());
            score.TrackDistance(0);
            for (double x = 0; x <= 100; x += 0.013) score.TrackDistance(x);
            long after = score.Score;
            Assert.AreEqual(100, after, 1);
            score.TrackDistance(40);
            for (double x = 40; x <= 100; x += 0.5) score.TrackDistance(x);
            Assert.AreEqual(after, score.Score, "retry must not re-score ground");
        }

        [Test]
        public void Session_ScoresFlipsAndPickups_OnlyWhileRiding_NoRespawn()
        {
            List<Chunk> chunks = Track((30, PickupId.SpiceCoin), (31, PickupId.WindFeather));
            var slope = new StretchSlope(chunks);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var signals = new RiderCameraSignals(rider, chunks);
            var ducks = new DucklingNestService();
            float bodyOffset = 0.6f;
            RunSession session = RunSession.Create(new FailureSettings(), new AssistSettings(), new MessageSettings(),
                new PickupSettings(), new ScoreSettings(), Hover, bodyOffset, 1, rider, rider, rider,
                new SpeedModel(new SpeedSettings()), chunks, slope, ducks, null, signals);
            var input = new ScriptedInput();
            while (rider.Position.Distance < 40)
            {
                rider.FixedStep(input, Dt);
                session.Tick(input, Dt);
            }
            Assert.AreEqual(1, session.Score.SpiceCoins, "rider body should touch hovering coin");
            Assert.IsTrue(signals.BoostActive, "wind feather drives camera Boost");
            long before = session.Score.Score;
            rider.ForceCrash(CrashCause.Obstacle);
            Assert.IsFalse(session.Pickups.WindActive);
            for (int i = 0; i < 200; i++)
            {
                rider.FixedStep(input, Dt);
                session.Tick(input, Dt);
            }
            Assert.AreEqual(FlowPhase.Riding, session.Flow.Phase);
            while (rider.Position.Distance < 40)
            {
                rider.FixedStep(input, Dt);
                session.Tick(input, Dt);
            }
            Assert.AreEqual(1, session.Score.SpiceCoins, "collected pickups must not respawn");
            Assert.GreaterOrEqual(session.Score.Score, before);
        }
    }
}
