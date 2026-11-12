using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>
    /// T17 QA: full-journey soak with the whole run session (hazards, ducklings, directors) and the duckling safety
    /// audit on every step, forced spills and retries, the bot after a retry, and the world window.
    /// </summary>
    public sealed class QaTests
    {
        private const float Dt = 0.01f;

        private static List<Chunk> Journey(int seed)
        {
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(new ContentSettings(), new FairnessSettings(), new BiomeSettings(), jump).GenerateJourney(seed);
        }

        private sealed class Soak
        {
            public List<Chunk> Stretch;
            public StretchSlope Slope;
            public KinematicRider Rider;
            public RunSession Session;
            public DucklingNestService Nests = new DucklingNestService();
            public BotPilot Pilot;
            public ScriptedInput Input = new ScriptedInput();
            public readonly List<string> Problems = new List<string>();
            public int Steps;
            public int Spills;
            public readonly List<string> SpillLog = new List<string>();

            public Soak(int seed)
            {
                Stretch = Journey(seed);
                Slope = new StretchSlope(Stretch);
                Rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), Slope, new SurfaceTable(new SurfaceSettings()));
                Session = RunSession.Create(new RunSettingsBundle(), 1.1f, 0.6f, seed, Rider, Stretch, Slope, Nests, null,
                    new RiderCameraSignals(Rider, Stretch));
                Pilot = new BotPilot(Stretch, new FairnessSettings().botTakeoffSeconds);
                Rider.Crashed += c =>
                {
                    Spills++;
                    SpillLog.Add(c + " at " + Rider.Position.Distance.ToString("F1") + " speed " + Rider.Speed.ToString("F1") +
                                 " squall " + Session.Squall.Intensity.ToString("F2") + " slide gap " +
                                 Session.Landslide.Gap(Rider.Position.Distance).ToString("F1") + " chunk " +
                                 Stretch[Slope.IndexAt(Rider.Position.Distance)].Id);
                };
            }

            public void Step()
            {
                bool grounded = Rider.State == RiderState.Grounded || Rider.State == RiderState.Tucking;
                if (Session.Flow.Phase == FlowPhase.Riding) Pilot.Drive(Input, Rider.Position.Distance, Rider.Speed, grounded);
                else
                {
                    // Nobody touches the controls during the hold: the flow moves on by itself.
                    Input.TapThisStep = false;
                    Input.IsHeld = false;
                }
                Rider.FixedStep(Input, Dt);
                Session.Tick(Input, Dt);
                Steps++;
                bool closed = Nests.NestCoversClosed;
                var view = new DucklingView { NestCoversVisible = true, BasketLidVisible = true, DrawnInNests = closed ? 0 : 1 };
                foreach (string p in DucklingSafetyAudit.Check(Nests, Session.Flow.Phase, Rider.State, view))
                    Problems.Add("step " + Steps + " at " + Rider.Position.Distance.ToString("F1") + ": " + p);
                CheckFinite(Session.Pacing.Intensity, "pacing");
                CheckFinite(Session.Weather.FogScale, "weather fog");
                CheckFinite((float)Rider.Position.Distance, "distance");
                CheckFinite(Rider.Speed, "speed");
            }

            private void CheckFinite(float v, string what)
            {
                if (float.IsNaN(v) || float.IsInfinity(v)) Problems.Add("step " + Steps + ": " + what + " is " + v);
            }
        }

        [Test]
        public void Soak_FullJourney_BotRidesClean_DucklingsAlwaysSafe_DirectorsRun()
        {
            foreach (int seed in new[] { 2, 9 })
            {
                var s = new Soak(seed);
                int guard = (int)(s.Slope.Length / new SpeedSettings().minSpeed / Dt) * 2;
                while (s.Rider.Position.Distance < s.Slope.Length && s.Steps < guard) s.Step();
                Assert.GreaterOrEqual(s.Rider.Position.Distance, s.Slope.Length, "seed " + seed + " did not finish");
                CollectionAssert.IsEmpty(s.Problems, "seed " + seed);
                Assert.AreEqual(0, s.Spills, "seed " + seed + ": the bot spilled on a fair journey: " + string.Join("; ", s.SpillLog));
                Assert.Greater(s.Session.Score.Rescues, 0);
                Assert.Greater(s.Session.Squall.Count, 0);
                Assert.Greater(s.Session.Humor.Count, 0, "no humor beat in a whole journey");
                Assert.Greater(s.Session.Weather.Changes, 0, "the sky never changed in a whole journey");
                RunSummary summary = s.Session.End();
                Assert.AreEqual(0, summary.Crashes);
                Assert.IsFalse(s.Session.Humor.Active, "a humor beat outlived the run");
            }
        }

        [Test]
        public void Soak_RepeatedSpills_EveryRetryRecovers_DucklingsAlwaysSafe()
        {
            var s = new Soak(4);
            var causes = (CrashCause[])Enum.GetValues(typeof(CrashCause));
            int forced = 0;
            int nextSpillStep = 300;
            int guard = 400000;
            while (s.Rider.Position.Distance < s.Slope.Length && s.Steps < guard)
            {
                if (s.Steps >= nextSpillStep && s.Session.Flow.Phase == FlowPhase.Riding && s.Rider.State != RiderState.Crashed)
                {
                    s.Rider.ForceCrash(causes[forced % causes.Length]);
                    forced++;
                    nextSpillStep = s.Steps + 1500;
                }
                s.Step();
            }
            Assert.GreaterOrEqual(s.Rider.Position.Distance, s.Slope.Length, "stuck after spills");
            Assert.Greater(forced, 20);
            CollectionAssert.IsEmpty(s.Problems);
            Assert.AreEqual(forced, s.Session.Flow.Retries, "every spill must come back through a retry");
            // After a retry the bot must take the jumps again: only the forced spills happened.
            Assert.AreEqual(forced, s.Spills, "the bot spilled on its own after a retry");
            Assert.IsTrue(s.Session.Flow.AssistsActive || s.Session.Flow.ConsecutiveFailures < new FailureSettings().assistAfterFailures);
        }

        [Test]
        public void BotPilot_RetakesJumpsAfterBeingMovedBack()
        {
            List<Chunk> stretch = Journey(2);
            var pilot = new BotPilot(stretch, new FairnessSettings().botTakeoffSeconds);
            Assert.Greater(pilot.Hazards.Count, 3);
            Span second = pilot.Hazards[1];
            var input = new ScriptedInput();
            const double speed = 12d;
            double x = 0d;
            int taps = 0;
            for (; x < second.End + 1d; x += speed * Dt)
            {
                pilot.Drive(input, x, speed, true);
                if (input.TapThisStep) taps++;
            }
            Assert.AreEqual(2, taps);
            // A retry teleports back before the first jump: both must be taken again.
            for (x = pilot.Hazards[0].Start - 30d; x < second.End + 1d; x += speed * Dt)
            {
                pilot.Drive(input, x, speed, true);
                if (input.TapThisStep) taps++;
            }
            Assert.AreEqual(4, taps, "the bot skipped jumps after a retry");
        }

        [Test]
        public void WorldWindow_SwitchesOnlyNearTheRider_WithoutFlicker()
        {
            var w = new WindowCuller(100d, 50d, 10d);
            for (int i = 0; i < 50; i++) w.Add(i * 20d, i * 20d + 20d);
            var log = new List<KeyValuePair<int, bool>>();
            w.Update(0d, (i, on) => log.Add(new KeyValuePair<int, bool>(i, on)));
            Assert.AreEqual(w.OnCount, log.Count);
            for (int i = 0; i < 50; i++)
            {
                double a = i * 20d, b = a + 20d;
                Assert.AreEqual(b >= -50d && a <= 100d, w.IsOn(i), "item " + i);
            }
            // Wobbling across an edge inside the hysteresis switches nothing.
            log.Clear();
            w.Update(5d, (i, on) => log.Add(new KeyValuePair<int, bool>(i, on)));
            int afterMove = log.Count;
            log.Clear();
            for (int k = 0; k < 20; k++) w.Update(k % 2 == 0 ? 0d : 5d, (i, on) => log.Add(new KeyValuePair<int, bool>(i, on)));
            Assert.AreEqual(0, log.Count, "flicker at the window edge");
            Assert.LessOrEqual(afterMove, 1);
            // A long jump (retry teleport) switches everything right at once.
            w.Update(800d, null);
            for (int i = 0; i < 50; i++)
            {
                double a = i * 20d, b = a + 20d;
                if (b >= 750d && a <= 900d) Assert.IsTrue(w.IsOn(i), "item " + i + " should be on");
                if (b < 740d || a > 910d) Assert.IsFalse(w.IsOn(i), "item " + i + " should be off");
            }
            int count = 0;
            for (int i = 0; i < 50; i++) count += w.IsOn(i) ? 1 : 0;
            Assert.AreEqual(count, w.OnCount);
        }

        [Test]
        public void WorldWindow_EdgeIsHiddenByFog_AndJourneyTerrainIsMostlyOff()
        {
            var ls = new LookSettings();
            var errors = new List<string>();
            ls.Validate(errors);
            CollectionAssert.IsEmpty(errors);
            foreach (BiomeLook b in ls.biomes)
            {
                // Light left after the window distance through the fog (Beer-Lambert), at the thickest weather.
                double t = Math.Exp(-ls.windowAheadMeters / (b.fogMeanFreePath * new DirectorSettings().WorstFogScale() * new HazardSettings().squallFogScale));
                Assert.Less(t, 0.1, b.biome + ": things pop in where they can still be seen");
            }
            // The camera's farthest framing distance is well inside the window.
            var cam = new CameraSettings();
            foreach (CameraStateProfile p in cam.states) Assert.Less(p.distance * 2f, ls.windowAheadMeters);

            double length = new StretchSlope(Journey(2)).Length;
            var w = new WindowCuller(ls.windowAheadMeters, ls.windowBehindMeters, ls.windowHysteresisMeters);
            double tile = ls.terrainCellMeters * 32;
            for (double x = 0d; x < length; x += tile) w.Add(x, x + tile);
            int worst = 0;
            for (double x = 0d; x < length; x += 5d)
            {
                w.Update(x, null);
                worst = Math.Max(worst, w.OnCount);
            }
            Assert.Less(worst, w.Count / 3, "too much of the journey is on at once");
        }

        [Test]
        public void EveryConfigBlock_ValidatesClean()
        {
            var all = new ISettings[]
            {
                new SpeedSettings(), new RiderSettings(), new SurfaceSettings(), new TrackSettings(), new FailureSettings(),
                new AssistSettings(), new MessageSettings(), new DucklingSettings(), new FairnessSettings(), new ContentSettings(),
                new BiomeSettings(), new CameraSettings(), new AudioMixSettings(), new UiMotionSettings(), new PickupSettings(),
                new ScoreSettings(), new VehicleSettings(), new ClothSettings(), new LookSettings(), new HazardSettings(),
                new GoalSettings(), new UiSettings(), new DirectorSettings(), new SoundSettings(),
            };
            foreach (ISettings s in all)
            {
                var e = new List<string>();
                s.Validate(e);
                CollectionAssert.IsEmpty(e, s.GetType().Name);
            }
        }
    }
}
