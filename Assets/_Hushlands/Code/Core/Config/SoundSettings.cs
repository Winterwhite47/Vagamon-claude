using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public enum SfxId
    {
        Pickup = 0,
        Rescue = 1,
        Landing = 2,
        CollapseWarning = 3,
        Retry = 4,
        Humor = 5,
    }

    /// <summary>A short synthesized sound: a sine that glides from startHz to endHz and fades (noise 0..1 mixes in air).</summary>
    [Serializable]
    public struct SfxEntry
    {
        public SfxId id;
        public float startHz;
        public float endHz;
        public float seconds;
        public float gain;
        public float noise;

        public SfxEntry(SfxId id, float startHz, float endHz, float seconds, float gain, float noise)
        {
            this.id = id;
            this.startHz = startHz;
            this.endHz = endHz;
            this.seconds = seconds;
            this.gain = gain;
            this.noise = noise;
        }
    }

    /// <summary>How loud each bed is in a camera state (the mix follows the camera, canon).</summary>
    [Serializable]
    public struct BedScaleEntry
    {
        public CameraState state;
        public float wind;
        public float sled;
        public float pad;

        public BedScaleEntry(CameraState state, float wind, float sled, float pad)
        {
            this.state = state;
            this.wind = wind;
            this.sled = sled;
            this.pad = pad;
        }
    }

    /// <summary>
    /// Original synthesized placeholder audio (T16): wind and sled beds from filtered noise, a soft pad, short tones.
    /// Authored audio replaces the clips later; the mix rules stay.
    /// </summary>
    [Serializable]
    public sealed class SoundSettings : ISettings
    {
        public bool enabled = true;
        public int sampleRate = 44100;
        public float loopSeconds = 4f;
        /// <summary>Tail of each noise loop cross-faded into its head so the loop has no click.</summary>
        public float loopFadeFraction = 0.15f;
        public float windLowPassHz = 500f;
        public float sledLowPassHz = 2600f;
        public float padBaseHz = 110f;
        /// <summary>Pad partials as ratios of the base (an open fifth and octaves: calm, no tension).</summary>
        public float[] padRatios = { 1f, 1.5f, 2f, 3f };

        // --- Beds ---
        public float windBaseGain = 0.12f;
        public float windGainPerSpeed = 0.012f;
        public float windAirGain = 0.12f;
        public float windSquallGain = 0.25f;
        public float windPitchMin = 0.8f;
        public float windPitchMax = 1.3f;
        public float sledGainPerSpeed = 0.028f;
        public float sledMaxGain = 0.45f;
        public float sledPitchMin = 0.7f;
        public float sledPitchMax = 1.35f;
        public float padBaseGain = 0.1f;
        /// <summary>The pad opens up when the ride is calm and steps back as tension rises.</summary>
        public float padCalmGain = 0.1f;
        /// <summary>How fast bed gains follow their targets (1/s).</summary>
        public float gainResponse = 5f;
        public BedScaleEntry[] bedScales =
        {
            new BedScaleEntry(CameraState.Cruise, 1f, 1f, 1f),
            new BedScaleEntry(CameraState.Air, 1.25f, 0f, 1f),
            new BedScaleEntry(CameraState.Boost, 1.35f, 1.1f, 1f),
            new BedScaleEntry(CameraState.Vista, 0.8f, 0.8f, 1.3f),
            new BedScaleEntry(CameraState.Danger, 1.1f, 1f, 0.5f),
            new BedScaleEntry(CameraState.Shelter, 0.5f, 0.9f, 1.1f),
            new BedScaleEntry(CameraState.FailureHold, 0.4f, 0f, 0.6f),
        };

        // --- Short sounds ---
        public SfxEntry[] sfx =
        {
            new SfxEntry(SfxId.Pickup, 1320f, 1980f, 0.22f, 0.3f, 0f),
            new SfxEntry(SfxId.Rescue, 1800f, 2600f, 0.14f, 0.3f, 0f),
            new SfxEntry(SfxId.Landing, 90f, 55f, 0.2f, 0.5f, 0.35f),
            new SfxEntry(SfxId.CollapseWarning, 60f, 45f, 0.9f, 0.45f, 0.5f),
            new SfxEntry(SfxId.Retry, 300f, 700f, 0.5f, 0.2f, 0.8f),
            new SfxEntry(SfxId.Humor, 880f, 1175f, 0.3f, 0.2f, 0f),
        };
        /// <summary>Each pickup kind rings a little higher than the last.</summary>
        public float pickupPitchStep = 0.07f;
        /// <summary>Landings softer than this are silent; at landingFullSpeed the thud is at full gain.</summary>
        public float landingMinSpeed = 1f;
        public float landingFullSpeed = 8f;
        public int voices = 4;

        public SfxEntry Get(SfxId id)
        {
            foreach (SfxEntry s in sfx)
            {
                if (s.id == id) return s;
            }
            throw new KeyNotFoundException("Sound not configured: " + id);
        }

        public BedScaleEntry Get(CameraState state)
        {
            foreach (BedScaleEntry b in bedScales)
            {
                if (b.state == state) return b;
            }
            throw new KeyNotFoundException("Bed scale not configured: " + state);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(SoundSettings);
            SettingsCheck.Range(e, o, nameof(sampleRate), sampleRate, 8000f, 96000f);
            SettingsCheck.Range(e, o, nameof(loopSeconds), loopSeconds, 1f, 20f);
            SettingsCheck.Range(e, o, nameof(loopFadeFraction), loopFadeFraction, 0.01f, 0.5f);
            SettingsCheck.Positive(e, o, nameof(windLowPassHz), windLowPassHz);
            SettingsCheck.Positive(e, o, nameof(sledLowPassHz), sledLowPassHz);
            SettingsCheck.Positive(e, o, nameof(padBaseHz), padBaseHz);
            if (padRatios == null || padRatios.Length == 0) e.Add(o + ": no pad partials");
            foreach (float r in padRatios ?? new float[0])
            {
                SettingsCheck.Positive(e, o, nameof(padRatios), r);
                if (padBaseHz * r * 2f > sampleRate) e.Add(o + ": pad partial above Nyquist");
            }
            SettingsCheck.NonNegative(e, o, nameof(windBaseGain), windBaseGain);
            SettingsCheck.NonNegative(e, o, nameof(windGainPerSpeed), windGainPerSpeed);
            SettingsCheck.NonNegative(e, o, nameof(windAirGain), windAirGain);
            SettingsCheck.NonNegative(e, o, nameof(windSquallGain), windSquallGain);
            SettingsCheck.Positive(e, o, nameof(windPitchMin), windPitchMin);
            SettingsCheck.Ordered(e, o, nameof(windPitchMin), windPitchMin, nameof(windPitchMax), windPitchMax);
            SettingsCheck.NonNegative(e, o, nameof(sledGainPerSpeed), sledGainPerSpeed);
            SettingsCheck.Range(e, o, nameof(sledMaxGain), sledMaxGain, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(sledPitchMin), sledPitchMin);
            SettingsCheck.Ordered(e, o, nameof(sledPitchMin), sledPitchMin, nameof(sledPitchMax), sledPitchMax);
            SettingsCheck.NonNegative(e, o, nameof(padBaseGain), padBaseGain);
            SettingsCheck.NonNegative(e, o, nameof(padCalmGain), padCalmGain);
            SettingsCheck.Positive(e, o, nameof(gainResponse), gainResponse);
            var states = new List<CameraState>();
            foreach (BedScaleEntry b in bedScales ?? new BedScaleEntry[0])
            {
                states.Add(b.state);
                string t = o + "." + b.state;
                SettingsCheck.Range(e, t, nameof(b.wind), b.wind, 0f, 2f);
                SettingsCheck.Range(e, t, nameof(b.sled), b.sled, 0f, 2f);
                SettingsCheck.Range(e, t, nameof(b.pad), b.pad, 0f, 2f);
            }
            SettingsCheck.CoversEnum(e, o, states);
            var ids = new List<SfxId>();
            foreach (SfxEntry s in sfx ?? new SfxEntry[0])
            {
                ids.Add(s.id);
                string t = o + "." + s.id;
                SettingsCheck.Positive(e, t, nameof(s.startHz), s.startHz);
                SettingsCheck.Positive(e, t, nameof(s.endHz), s.endHz);
                if (Math.Max(s.startHz, s.endHz) * 2f > sampleRate) e.Add(t + ": above Nyquist");
                SettingsCheck.Range(e, t, nameof(s.seconds), s.seconds, 0.02f, 3f);
                SettingsCheck.Range(e, t, nameof(s.gain), s.gain, 0f, 1f);
                SettingsCheck.Range(e, t, nameof(s.noise), s.noise, 0f, 1f);
            }
            SettingsCheck.CoversEnum(e, o, ids);
            SettingsCheck.NonNegative(e, o, nameof(pickupPitchStep), pickupPitchStep);
            SettingsCheck.NonNegative(e, o, nameof(landingMinSpeed), landingMinSpeed);
            SettingsCheck.Ordered(e, o, nameof(landingMinSpeed), landingMinSpeed, nameof(landingFullSpeed), landingFullSpeed);
            SettingsCheck.Range(e, o, nameof(voices), voices, 1f, 16f);
        }
    }
}
