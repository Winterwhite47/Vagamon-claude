using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T15: time of day, weather, pacing, humor beats and failure choreography (presentation only).</summary>
    public sealed class DirectorTests
    {
        private const float Dt = 0.01f;

        private static List<Chunk> Stretch(int seed)
        {
            var content = new ContentSettings();
            JumpModel jump = JumpModel.ForAllVehicles(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            return new ChunkGenerator(content, new FairnessSettings(), new BiomeSettings(), jump).Generate(seed, BiomeId.MeadowSea, content.chunksPerStretch);
        }

        [Test]
        public void Settings_AreValid_AndLinesAreCalm()
        {
            var s = new DirectorSettings();
            var errors = new List<string>();
            s.Validate(errors);
            CollectionAssert.IsEmpty(errors);
            var messages = new MessageSettings();
            foreach (HumorBeatEntry b in s.humorBeats) Assert.IsTrue(messages.IsClean(b.line), b.id);
        }

        [Test]
        public void Settings_RejectBrokenValues()
        {
            var s = new DirectorSettings();
            s.weather = new[] { s.weather[0] };
            s.humorDurationSeconds = s.humorIntervalMinSeconds;
            s.choreography[0].leanDegrees = 90f;
            var errors = new List<string>();
            s.Validate(errors);
            Assert.GreaterOrEqual(errors.Count, 3, string.Join("; ", errors));
        }

        [Test]
        public void MistNeverHidesJumps_InTheThickestWeatherAndSquall()
        {
            var s = new DirectorSettings();
            float worst = new HazardSettings().squallFogScale * s.WorstFogScale();
            Assert.Less(worst, 1f);
            CollectionAssert.IsEmpty(MistVisibility.Check(new LookSettings(), new CameraSettings(), new FairnessSettings(), worst));
        }

        [Test]
        public void DayDrift_IsBounded_Smooth_AndSeeded()
        {
            var s = new DirectorSettings();
            var a = new DayDrift(s, 5);
            var b = new DayDrift(s, 5);
            var c = new DayDrift(s, 6);
            bool differs = false;
            double last = a.Swing(0d);
            for (double t = 0d; t < s.dayDriftPeriodSeconds * 2d; t += 0.5)
            {
                double v = a.Swing(t);
                Assert.That(v, Is.InRange(-1d, 1d));
                Assert.AreEqual(v, b.Swing(t));
                Assert.Less(Math.Abs(v - last), 0.02, "light jumps at " + t);
                differs |= Math.Abs(v - c.Swing(t)) > 1e-6;
                last = v;
            }
            Assert.IsTrue(differs);
            Assert.AreEqual(a.Swing(3d), a.Swing(3d + s.dayDriftPeriodSeconds), 1e-9);
        }

        [Test]
        public void Atmosphere_BlendsBiomes_KeepsSunUp_AndWeatherOnlyDimsAndThickens()
        {
            var s = new DirectorSettings();
            var ls = new LookSettings();
            foreach (BiomeLook from in ls.biomes)
            {
                foreach (BiomeLook to in ls.biomes)
                {
                    AtmosphereState plain = AtmosphereModel.Evaluate(from, to, 0d, s, 0d, 1f, 1f, 0f);
                    Assert.AreEqual(from.sunLux, plain.SunLux, 1e-3f);
                    Assert.AreEqual(from.fogMeanFreePath, plain.FogMeanFreePath, 1e-4f);
                    Assert.AreEqual(from.exposureEV, plain.ExposureEV, 1e-5f);
                    AtmosphereState end = AtmosphereModel.Evaluate(from, to, 1d, s, 0d, 1f, 1f, 0f);
                    Assert.AreEqual(to.sunKelvin, end.SunKelvin, 1e-2f);
                    Assert.AreEqual(to.sunAzimuthDegrees, end.SunAzimuthDegrees, 1e-4f);
                    foreach (double swing in new[] { -1d, 0d, 1d })
                    {
                        foreach (WeatherEntry w in s.weather)
                        {
                            for (double t = 0d; t <= 1d; t += 0.25)
                            {
                                AtmosphereState a = AtmosphereModel.Evaluate(from, to, t, s, swing, w.sunLuxScale, w.fogScale, w.exposureOffsetEV);
                                Assert.GreaterOrEqual(a.SunElevationDegrees, s.minSunElevationDegrees);
                                Assert.Greater(a.SunLux, 0f);
                                Assert.That(a.SunKelvin, Is.InRange(1500f, 20000f));
                                float baseFog = from.fogMeanFreePath + (to.fogMeanFreePath - from.fogMeanFreePath) * (float)t;
                                Assert.AreEqual(baseFog * w.fogScale, a.FogMeanFreePath, 1e-3f);
                                Assert.Less(a.FogBaseHeight, a.FogMaximumHeight);
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void Weather_IsSeeded_ChangesOnlyWhenCalm_AndFadesSmoothly()
        {
            var s = new DirectorSettings();
            var a = new WeatherDirector(s, 11);
            var b = new WeatherDirector(s, 11);
            var tense = new WeatherDirector(s, 11);
            var seen = new HashSet<WeatherKind>();
            float lastLux = a.SunLuxScale;
            for (int i = 0; i < 200000; i++)
            {
                a.Step(Dt, true);
                b.Step(Dt, true);
                tense.Step(Dt, false);
                Assert.AreEqual(a.Current, b.Current);
                Assert.AreEqual(a.Blend, b.Blend);
                Assert.That(a.Blend, Is.InRange(0f, 1f));
                Assert.Less(Math.Abs(a.SunLuxScale - lastLux), 0.002f, "light cut at step " + i);
                lastLux = a.SunLuxScale;
                Assert.GreaterOrEqual(a.FogScale, s.WorstFogScale() - 1e-6f);
                Assert.LessOrEqual(a.FogScale, 1f + 1e-6f);
                seen.Add(a.Current);
            }
            Assert.Greater(a.Changes, 5);
            Assert.AreEqual(s.weather.Length, seen.Count, "every sky mood should come round");
            Assert.AreEqual(0, tense.Changes, "weather must wait for calm");
            Assert.AreEqual(WeatherKind.Clear, tense.Current);
        }

        [Test]
        public void Pacing_RisesNearHazardsDangerAndSpills_AndCalmNeedsAQuietSpell()
        {
            var s = new DirectorSettings();
            List<Chunk> stretch = Stretch(2);
            var p = new PacingDirector(s, stretch);
            double firstHazard = double.MaxValue;
            foreach (Chunk c in stretch)
                foreach (ChunkFeature f in c.Features)
                    if (f.IsHazard) firstHazard = Math.Min(firstHazard, c.StartDistance + f.start);
            Assert.Less(firstHazard, double.MaxValue);
            Assert.IsFalse(p.Calm, "calm must be earned");

            // Far before the first hazard, standing still: nothing ahead.
            Assert.AreEqual(0, p.HazardsAhead(firstHazard - 500d, 10f));
            Assert.AreEqual(1, p.HazardsAhead(firstHazard - 1d, 10f) > 0 ? 1 : 0);
            for (int i = 0; i < 400; i++) p.Step(firstHazard - 500d, 10f, false, 0f, Dt);
            Assert.IsTrue(p.Calm);
            Assert.Less(p.Intensity, s.pacingCalmThreshold);

            for (int i = 0; i < 400; i++) p.Step(firstHazard - 1d, 10f, true, 1f, Dt);
            Assert.IsTrue(p.Tense);
            Assert.IsFalse(p.Calm);
            Assert.LessOrEqual(p.Intensity, 1f);

            var q = new PacingDirector(s, new List<Chunk>());
            for (int i = 0; i < 400; i++) q.Step(0d, 10f, false, 0f, Dt);
            Assert.IsTrue(q.Calm);
            q.NoteFailure();
            Assert.IsFalse(q.Calm);
            q.Step(0d, 10f, false, 0f, Dt);
            Assert.Greater(q.Intensity, 0f);
            for (int i = 0; i < 3000; i++) q.Step(0d, 10f, false, 0f, Dt);
            Assert.IsTrue(q.Calm, "heat must decay");
        }

        [Test]
        public void Humor_OnlyInCalm_Sparse_Seeded_NeverRepeatsBackToBack()
        {
            var s = new DirectorSettings();
            var h = new HumorDirector(s, 3);
            var twin = new HumorDirector(s, 3);
            var waiting = new HumorDirector(s, 3);
            var starts = new List<double>();
            var ids = new List<string>();
            int ended = 0;
            double time = 0d;
            h.BeatStarted += b =>
            {
                starts.Add(time);
                ids.Add(b.Id);
                Assert.AreEqual(time * 10d + s.humorAheadMeters, b.Distance, 1e-6);
                Assert.IsFalse(string.IsNullOrEmpty(b.Line));
            };
            h.BeatEnded += () => ended++;
            var twinIds = new List<string>();
            twin.BeatStarted += b => twinIds.Add(b.Id);
            for (int i = 0; i < 100000; i++)
            {
                time = i * (double)Dt;
                h.Step(time * 10d, true, false, Dt);
                twin.Step(time * 10d, true, false, Dt);
                waiting.Step(time * 10d, false, false, Dt);
            }
            Assert.Greater(starts.Count, 8);
            CollectionAssert.AreEqual(ids, twinIds);
            Assert.GreaterOrEqual(starts[0], s.humorFirstSeconds - 0.02);
            for (int i = 1; i < starts.Count; i++)
            {
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], s.humorIntervalMinSeconds - 0.02, "beats too close");
                Assert.AreNotEqual(ids[i - 1], ids[i], "same gag twice in a row");
            }
            Assert.GreaterOrEqual(ended, starts.Count - 1);
            Assert.AreEqual(0, waiting.Count, "humor must wait for calm");
            var all = new HashSet<string>(ids);
            Assert.AreEqual(s.humorBeats.Length, all.Count);
        }

        [Test]
        public void Humor_EndsAtOnce_WhenTenseOrCancelled_AndCanBeSwitchedOff()
        {
            var s = new DirectorSettings();
            var h = new HumorDirector(s, 3);
            int ended = 0;
            h.BeatEnded += () => ended++;
            for (int i = 0; i < 100000 && !h.Active; i++) h.Step(0d, true, false, Dt);
            Assert.IsTrue(h.Active);
            h.Step(0d, true, true, Dt);
            Assert.IsFalse(h.Active);
            Assert.AreEqual(1, ended);
            h.Cancel();
            Assert.AreEqual(1, ended, "cancel when idle is a no-op");
            for (int i = 0; i < 100000 && !h.Active; i++) h.Step(0d, true, false, Dt);
            Assert.IsTrue(h.Active);
            h.Cancel();
            Assert.IsFalse(h.Active);
            Assert.AreEqual(2, ended);

            var off = new DirectorSettings { humorEnabled = false };
            var none = new HumorDirector(off, 3);
            for (int i = 0; i < 20000; i++) none.Step(0d, true, false, Dt);
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void Choreography_IsGentle_Monotone_AndSettlesInsideTheHold()
        {
            var s = new DirectorSettings();
            var failure = new FailureSettings();
            foreach (CrashCause cause in (CrashCause[])Enum.GetValues(typeof(CrashCause)))
            {
                ChoreographyEntry c = s.Get(cause);
                Assert.LessOrEqual(c.settleSeconds, failure.failureHoldSeconds, cause + " must settle before the hold ends");
                Assert.LessOrEqual(c.coverSeconds, failure.minHoldBeforeSkipSeconds, cause + " puff must close before a skip");
                ChoreographyPose start = FailureChoreography.Evaluate(s, cause, 0f);
                Assert.AreEqual(0f, start.LeanDegrees);
                Assert.AreEqual(0f, start.SinkMeters);
                Assert.AreEqual(0f, start.Cover);
                ChoreographyPose last = start;
                for (float t = 0f; t <= failure.failureHoldSeconds; t += 0.01f)
                {
                    ChoreographyPose p = FailureChoreography.Evaluate(s, cause, t);
                    Assert.GreaterOrEqual(p.LeanDegrees, last.LeanDegrees - 1e-5f);
                    Assert.GreaterOrEqual(p.SinkMeters, last.SinkMeters - 1e-6f);
                    Assert.GreaterOrEqual(p.Cover, last.Cover - 1e-6f);
                    Assert.LessOrEqual(p.LeanDegrees, 45f);
                    Assert.That(p.Cover, Is.InRange(0f, 1f));
                    last = p;
                }
                ChoreographyPose end = FailureChoreography.Evaluate(s, cause, float.MaxValue);
                Assert.AreEqual(c.leanDegrees, end.LeanDegrees, 1e-5f);
                Assert.AreEqual(c.sinkMeters, end.SinkMeters, 1e-6f);
                Assert.AreEqual(c.coverSeconds > 0f ? 1f : 0f, end.Cover);
            }
            // Nothing unkind is ever shown at a gap or under the slide: the puff closes over the rider.
            Assert.Greater(s.Get(CrashCause.Gap).coverSeconds, 0f);
            Assert.Greater(s.Get(CrashCause.Landslide).coverSeconds, 0f);
        }
    }
}
