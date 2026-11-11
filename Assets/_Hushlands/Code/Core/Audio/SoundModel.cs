using System;

namespace Hushlands.Core
{
    /// <summary>Deterministic placeholder sound synthesis (mono, samples in [-1, 1]). Engine-free so it can be tested.</summary>
    public static class SoundSynth
    {
        private const float Peak = 0.9f;

        /// <summary>Low-passed noise that loops without a click (its tail is cross-faded into its head).</summary>
        public static float[] NoiseLoop(SoundSettings s, float lowPassHz, long seed)
        {
            int n = Math.Max(2, (int)Math.Round(s.loopSeconds * s.sampleRate));
            int fade = Math.Max(1, Math.Min(n / 2, (int)Math.Round(n * s.loopFadeFraction)));
            float[] raw = LowPassedNoise(n + fade, s.sampleRate, lowPassHz, seed);
            var loop = new float[n];
            Array.Copy(raw, loop, n);
            // The stretch after the end continues smoothly from the last sample: fade it in over the head.
            for (int i = 0; i < fade; i++)
            {
                double k = (i + 0.5) / fade;
                loop[i] = (float)(raw[n + i] * Math.Cos(k * Math.PI * 0.5) + raw[i] * Math.Sin(k * Math.PI * 0.5));
            }
            Normalize(loop);
            return loop;
        }

        /// <summary>Sum of sines, each a whole number of cycles over the loop (so it loops exactly).</summary>
        public static float[] PadLoop(SoundSettings s)
        {
            int n = Math.Max(2, (int)Math.Round(s.loopSeconds * s.sampleRate));
            double seconds = n / (double)s.sampleRate;
            var loop = new float[n];
            for (int p = 0; p < s.padRatios.Length; p++)
            {
                double cycles = Math.Max(1d, Math.Round(s.padBaseHz * s.padRatios[p] * seconds));
                double amp = 1d / (p + 1);
                for (int i = 0; i < n; i++) loop[i] += (float)(amp * Math.Sin(i / (double)n * cycles * Math.PI * 2d));
            }
            Normalize(loop);
            return loop;
        }

        /// <summary>A short tone that glides in pitch, with a quick attack and a decay to silence.</summary>
        public static float[] Tone(SoundSettings s, SfxEntry e, long seed)
        {
            int n = Math.Max(2, (int)Math.Round(e.seconds * s.sampleRate));
            float[] noise = e.noise > 0f ? LowPassedNoise(n, s.sampleRate, Math.Max(e.startHz, e.endHz) * 4f, seed) : null;
            var data = new float[n];
            double phase = 0d;
            int attack = Math.Max(1, n / 50);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)(n - 1);
                double hz = e.startHz + (e.endHz - e.startHz) * t;
                phase += hz / s.sampleRate * Math.PI * 2d;
                double envelope = Math.Min(1d, i / (double)attack) * (1d - t) * (1d - t);
                double v = Math.Sin(phase) * (1d - e.noise);
                if (noise != null) v += noise[i] * e.noise;
                data[i] = (float)(v * envelope);
            }
            Normalize(data);
            data[n - 1] = 0f;
            return data;
        }

        private static float[] LowPassedNoise(int n, int sampleRate, float lowPassHz, long seed)
        {
            var rng = new Rng(seed);
            double a = 1d - Math.Exp(-2d * Math.PI * Math.Min(lowPassHz, sampleRate * 0.45) / sampleRate);
            var data = new float[n];
            double y = 0d;
            double mean = 0d;
            // Warm the filter so the first sample is not a step from zero.
            for (int i = 0; i < 256; i++) y += a * (rng.Range(-1d, 1d) - y);
            for (int i = 0; i < n; i++)
            {
                y += a * (rng.Range(-1d, 1d) - y);
                data[i] = (float)y;
                mean += y;
            }
            mean /= n;
            for (int i = 0; i < n; i++) data[i] -= (float)mean;
            return data;
        }

        private static void Normalize(float[] data)
        {
            float peak = 0f;
            foreach (float v in data) peak = Math.Max(peak, Math.Abs(v));
            if (peak <= 0f) return;
            float k = Peak / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }
    }

    /// <summary>What the mix listens to each frame.</summary>
    public struct SoundInputs
    {
        public bool Riding;
        public bool Grounded;
        public float Speed;
        public float MinSpeed;
        public float MaxSpeed;
        public float SquallIntensity;
        /// <summary>PacingDirector.Intensity (0 quiet, 1 tense).</summary>
        public float Tension;
        public CameraState State;
    }

    /// <summary>
    /// Bed gains and pitches: wind grows with speed, air and squall; the sled only sounds on the ground; the pad
    /// opens in calm and steps back under tension. Scaled per camera state and smoothed so nothing cuts.
    /// </summary>
    public sealed class SoundMix
    {
        private readonly SoundSettings _s;

        public SoundMix(SoundSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            WindPitch = settings.windPitchMin;
            SledPitch = settings.sledPitchMin;
        }

        public float WindGain { get; private set; }
        public float WindPitch { get; private set; }
        public float SledGain { get; private set; }
        public float SledPitch { get; private set; }
        public float PadGain { get; private set; }

        public void Step(SoundInputs i, float dt)
        {
            BedScaleEntry scale = _s.Get(i.State);
            float span = Math.Max(1e-3f, i.MaxSpeed - i.MinSpeed);
            float pace = Clamp01((i.Speed - i.MinSpeed) / span);
            float speed = i.Riding ? Math.Max(0f, i.Speed) : 0f;

            float wind = _s.windBaseGain + speed * _s.windGainPerSpeed + Clamp01(i.SquallIntensity) * _s.windSquallGain;
            if (i.Riding && !i.Grounded) wind += _s.windAirGain;
            float sled = i.Riding && i.Grounded ? Math.Min(_s.sledMaxGain, speed * _s.sledGainPerSpeed) : 0f;
            float pad = _s.padBaseGain + (1f - Clamp01(i.Tension)) * _s.padCalmGain;

            float k = Math.Min(1f, _s.gainResponse * dt);
            WindGain += (Clamp01(wind * scale.wind) - WindGain) * k;
            SledGain += (Clamp01(sled * scale.sled) - SledGain) * k;
            PadGain += (Clamp01(pad * scale.pad) - PadGain) * k;
            WindPitch += (_s.windPitchMin + (_s.windPitchMax - _s.windPitchMin) * (i.Riding ? pace : 0f) - WindPitch) * k;
            SledPitch += (_s.sledPitchMin + (_s.sledPitchMax - _s.sledPitchMin) * pace - SledPitch) * k;
        }

        /// <summary>Thud gain for a landing at this vertical speed (0 below landingMinSpeed).</summary>
        public float LandingGain(float verticalSpeed)
        {
            if (verticalSpeed < _s.landingMinSpeed) return 0f;
            float span = Math.Max(1e-3f, _s.landingFullSpeed - _s.landingMinSpeed);
            return Clamp01((verticalSpeed - _s.landingMinSpeed) / span) * _s.Get(SfxId.Landing).gain;
        }

        /// <summary>Each pickup kind rings a step higher.</summary>
        public float PickupPitch(PickupId kind)
        {
            return 1f + (int)kind * _s.pickupPitchStep;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
