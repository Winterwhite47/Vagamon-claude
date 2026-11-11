using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    /// <summary>T16: synthesized placeholder audio and the mix that follows the ride and the camera.</summary>
    public sealed class SoundTests
    {
        private const float Dt = 0.01f;

        private static SoundInputs Cruise(float speed)
        {
            var sp = new SpeedSettings();
            return new SoundInputs
            {
                Riding = true, Grounded = true, Speed = speed, MinSpeed = sp.minSpeed, MaxSpeed = sp.maxSpeed,
                State = CameraState.Cruise,
            };
        }

        private static SoundMix Settle(SoundSettings s, SoundInputs i)
        {
            var m = new SoundMix(s);
            for (int k = 0; k < 1000; k++) m.Step(i, Dt);
            return m;
        }

        private static float MaxStep(float[] d)
        {
            float worst = 0f;
            for (int i = 1; i < d.Length; i++) worst = Math.Max(worst, Math.Abs(d[i] - d[i - 1]));
            return worst;
        }

        private static void AssertClean(float[] d, string name)
        {
            double mean = 0d;
            float peak = 0f;
            foreach (float v in d)
            {
                Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), name + " has a bad sample");
                peak = Math.Max(peak, Math.Abs(v));
                mean += v;
            }
            Assert.LessOrEqual(peak, 1f, name + " clips");
            Assert.Greater(peak, 0.5f, name + " is nearly silent");
            Assert.Less(Math.Abs(mean / d.Length), 0.02, name + " has a DC offset");
        }

        [Test]
        public void Settings_AreValid_AndRejectBrokenValues()
        {
            var s = new SoundSettings();
            var errors = new List<string>();
            s.Validate(errors);
            CollectionAssert.IsEmpty(errors);
            s.sfx = new[] { s.sfx[0] };
            s.bedScales[0].sled = 5f;
            s.padRatios = new float[0];
            s.Validate(errors);
            Assert.GreaterOrEqual(errors.Count, 3, string.Join("; ", errors));
        }

        [Test]
        public void Loops_AreClean_Deterministic_AndSeamless()
        {
            var s = new SoundSettings();
            float[] wind = SoundSynth.NoiseLoop(s, s.windLowPassHz, 1);
            float[] sled = SoundSynth.NoiseLoop(s, s.sledLowPassHz, 2);
            float[] pad = SoundSynth.PadLoop(s);
            int n = (int)Math.Round(s.loopSeconds * s.sampleRate);
            foreach (KeyValuePair<string, float[]> loop in new Dictionary<string, float[]> { { "wind", wind }, { "sled", sled }, { "pad", pad } })
            {
                Assert.AreEqual(n, loop.Value.Length);
                AssertClean(loop.Value, loop.Key);
                // The wrap from the last sample to the first is no bigger than the steps inside the loop.
                float wrap = Math.Abs(loop.Value[0] - loop.Value[n - 1]);
                Assert.LessOrEqual(wrap, MaxStep(loop.Value) + 1e-6f, loop.Key + " clicks at the loop point");
            }
            CollectionAssert.AreEqual(wind, SoundSynth.NoiseLoop(s, s.windLowPassHz, 1));
            CollectionAssert.AreNotEqual(wind, SoundSynth.NoiseLoop(s, s.windLowPassHz, 3));
            // The wind bed is darker than the sled bed (lower cutoff = smaller sample-to-sample steps).
            Assert.Less(MaxStep(wind), MaxStep(sled));
        }

        [Test]
        public void Tones_AreClean_StartAndEndSilent_AndLastAsConfigured()
        {
            var s = new SoundSettings();
            foreach (SfxEntry e in s.sfx)
            {
                float[] d = SoundSynth.Tone(s, e, 9);
                Assert.AreEqual((int)Math.Round(e.seconds * s.sampleRate), d.Length, e.id.ToString());
                AssertClean(d, e.id.ToString());
                Assert.Less(Math.Abs(d[0]), 0.05f, e.id + " starts with a click");
                Assert.AreEqual(0f, d[d.Length - 1], e.id + " ends with a click");
                Assert.Less(Math.Abs(d[d.Length - 2]), 0.01f, e.id + " ends abruptly");
                CollectionAssert.AreEqual(d, SoundSynth.Tone(s, e, 9));
            }
        }

        [Test]
        public void Mix_WindGrowsWithSpeedAirAndSquall_SledOnlyOnTheGround()
        {
            var s = new SoundSettings();
            var sp = new SpeedSettings();
            SoundMix slow = Settle(s, Cruise(sp.minSpeed));
            SoundMix fast = Settle(s, Cruise(sp.maxSpeed));
            Assert.Greater(fast.WindGain, slow.WindGain);
            Assert.Greater(fast.SledGain, slow.SledGain);
            Assert.Greater(fast.SledPitch, slow.SledPitch);
            Assert.AreEqual(s.sledPitchMax, fast.SledPitch, 1e-3f);
            Assert.AreEqual(s.sledPitchMin, slow.SledPitch, 1e-3f);
            Assert.LessOrEqual(fast.SledGain, s.sledMaxGain + 1e-5f);

            SoundInputs air = Cruise(12f);
            air.Grounded = false;
            air.State = CameraState.Air;
            SoundMix inAir = Settle(s, air);
            Assert.AreEqual(0f, inAir.SledGain, 1e-4f, "the sled must be silent in the air");
            Assert.Greater(inAir.WindGain, Settle(s, Cruise(12f)).WindGain);

            SoundInputs squall = Cruise(12f);
            squall.SquallIntensity = 1f;
            Assert.Greater(Settle(s, squall).WindGain, Settle(s, Cruise(12f)).WindGain);
        }

        [Test]
        public void Mix_PadStepsBackUnderTension_AndEverythingDucksInTheFailureHold()
        {
            var s = new SoundSettings();
            SoundInputs calm = Cruise(12f);
            SoundInputs tense = Cruise(12f);
            tense.Tension = 1f;
            tense.State = CameraState.Danger;
            Assert.Greater(Settle(s, calm).PadGain, Settle(s, tense).PadGain);

            SoundInputs hold = Cruise(0f);
            hold.State = CameraState.FailureHold;
            SoundMix held = Settle(s, hold);
            Assert.AreEqual(0f, held.SledGain, 1e-4f);
            Assert.Less(held.WindGain, Settle(s, calm).WindGain);
            Assert.Greater(held.PadGain, 0f, "the hold stays gentle, not silent");

            SoundInputs title = Cruise(15f);
            title.Riding = false;
            SoundMix idle = Settle(s, title);
            Assert.AreEqual(0f, idle.SledGain, 1e-4f, "no sled on the title screen");
            Assert.AreEqual(s.windBaseGain, idle.WindGain, 1e-3f);
        }

        [Test]
        public void Mix_NeverClips_NeverJumps_InAnyCameraState()
        {
            var s = new SoundSettings();
            var sp = new SpeedSettings();
            var m = new SoundMix(s);
            var states = (CameraState[])Enum.GetValues(typeof(CameraState));
            var rng = new Rng(4);
            float lastWind = 0f, lastSled = 0f, lastPad = 0f;
            for (int i = 0; i < 20000; i++)
            {
                SoundInputs input = Cruise((float)rng.Range(0d, sp.maxSpeed * 1.5));
                input.State = states[rng.RangeInclusive(0, states.Length - 1)];
                input.Grounded = rng.Range(0d, 1d) < 0.5;
                input.Riding = rng.Range(0d, 1d) < 0.8;
                input.SquallIntensity = (float)rng.Range(-0.5, 1.5);
                input.Tension = (float)rng.Range(-0.5, 1.5);
                m.Step(input, Dt);
                foreach (float g in new[] { m.WindGain, m.SledGain, m.PadGain })
                    Assert.That(g, Is.InRange(0f, 1f));
                Assert.That(m.WindPitch, Is.InRange(s.windPitchMin - 1e-4f, s.windPitchMax + 1e-4f));
                Assert.That(m.SledPitch, Is.InRange(s.sledPitchMin - 1e-4f, s.sledPitchMax + 1e-4f));
                float limit = s.gainResponse * Dt + 1e-5f;
                Assert.LessOrEqual(Math.Abs(m.WindGain - lastWind), limit);
                Assert.LessOrEqual(Math.Abs(m.SledGain - lastSled), limit);
                Assert.LessOrEqual(Math.Abs(m.PadGain - lastPad), limit);
                lastWind = m.WindGain;
                lastSled = m.SledGain;
                lastPad = m.PadGain;
            }
        }

        [Test]
        public void Landing_SoftIsSilent_HardIsFull_AndPickupsRingHigherByKind()
        {
            var s = new SoundSettings();
            var m = new SoundMix(s);
            Assert.AreEqual(0f, m.LandingGain(s.landingMinSpeed * 0.5f));
            Assert.AreEqual(s.Get(SfxId.Landing).gain, m.LandingGain(s.landingFullSpeed * 2f), 1e-6f);
            float last = -1f;
            for (float v = s.landingMinSpeed; v <= s.landingFullSpeed; v += 0.5f)
            {
                Assert.GreaterOrEqual(m.LandingGain(v), last);
                last = m.LandingGain(v);
            }
            float pitch = 0f;
            foreach (PickupId id in (PickupId[])Enum.GetValues(typeof(PickupId)))
            {
                Assert.Greater(m.PickupPitch(id), pitch);
                pitch = m.PickupPitch(id);
                Assert.That(pitch, Is.InRange(0.5f, 2f));
            }
        }
    }
}
