using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    [Serializable]
    public struct CameraStateProfile
    {
        public CameraState state;
        public float fieldOfView;
        public float distance;
        public float height;
        public float lookAhead;
        public float blendInSeconds;
        /// <summary>Higher wins when several states are requested in one step.</summary>
        public int priority;
        /// <summary>Downward pitch in degrees (small; framing math treats the view as side-on).</summary>
        public float pitchDegrees;
        /// <summary>Exempt from the reveal framing contract (only states with no required jumps ahead).</summary>
        public bool revealExempt;

        public CameraStateProfile(CameraState state, float fov, float distance, float height, float lookAhead, float blendIn,
            int priority, float pitch, bool revealExempt)
        {
            this.state = state;
            fieldOfView = fov;
            this.distance = distance;
            this.height = height;
            this.lookAhead = lookAhead;
            blendInSeconds = blendIn;
            this.priority = priority;
            pitchDegrees = pitch;
            this.revealExempt = revealExempt;
        }
    }

    [Serializable]
    public struct BiomeCameraModifier
    {
        public BiomeId biome;
        public float fovOffset;
        public float distanceScale;
        public float heightOffset;

        public BiomeCameraModifier(BiomeId biome, float fovOffset, float distanceScale, float heightOffset)
        {
            this.biome = biome;
            this.fovOffset = fovOffset;
            this.distanceScale = distanceScale;
            this.heightOffset = heightOffset;
        }
    }

    [Serializable]
    public sealed class CameraSettings : ISettings
    {
        // Side-on 2.5D framing tuned to the reveal contract: >= FairnessSettings.visibleAheadMeters ahead (D-021).
        public CameraStateProfile[] states =
        {
            new CameraStateProfile(CameraState.Cruise, 40f, 44f, 3f, 15f, 0.8f, 0, 4f, false),
            new CameraStateProfile(CameraState.Air, 44f, 42f, 4f, 15f, 0.5f, 2, 4f, false),
            new CameraStateProfile(CameraState.Boost, 48f, 38f, 3f, 18f, 0.4f, 3, 3f, false),
            new CameraStateProfile(CameraState.Vista, 36f, 60f, 8f, 20f, 1.6f, 1, 6f, false),
            new CameraStateProfile(CameraState.Danger, 46f, 38f, 3f, 16f, 0.3f, 4, 3f, false),
            new CameraStateProfile(CameraState.Shelter, 38f, 46f, 2.5f, 15f, 1.0f, 1, 3f, false),
            new CameraStateProfile(CameraState.FailureHold, 34f, 25f, 3f, 0f, 0.6f, 5, 6f, true),
        };

        public BiomeCameraModifier[] biomes =
        {
            new BiomeCameraModifier(BiomeId.WindmouthRidge, 2f, 1.1f, 1f),
            new BiomeCameraModifier(BiomeId.MeadowSea, 0f, 1f, 0f),
            new BiomeCameraModifier(BiomeId.PineHollow, 2f, 0.95f, -0.5f),
            new BiomeCameraModifier(BiomeId.TerraceCountry, 0f, 1.05f, 0.5f),
            new BiomeCameraModifier(BiomeId.MirrorLake, 1f, 1.1f, 0.5f),
        };

        // --- Director (T4) ---
        /// <summary>A lower-priority state must wait this long before replacing the current one.</summary>
        public float minDwellSeconds = 0.6f;
        /// <summary>Airborne this long before the Air framing kicks in (ignores small hops).</summary>
        public float airDelaySeconds = 0.25f;
        public float boostSpeedThreshold = 15f;
        /// <summary>Landslide closer than this (seconds at current speed) = Danger.</summary>
        public float dangerLandslideSeconds = 2.5f;
        public float biomeBlendSeconds = 3f;
        /// <summary>Vertical follow smoothing time constant.</summary>
        public float verticalSmoothSeconds = 0.35f;
        public float reducedMotionSmoothScale = 1.5f;

        // --- Framing contract ---
        /// <summary>Narrowest supported aspect (width / height).</summary>
        public float minAspect = 1.6f;
        public float maxSlopeForFramingDegrees = 14f;
        /// <summary>Rider and ground-ahead must stay inside this normalised screen half-extent.</summary>
        public float safeScreenExtent = 0.9f;
        public float maxRiderBehindCenter = 0.8f;
        public float maxPitchDegrees = 10f;

        public CameraStateProfile Get(CameraState s)
        {
            foreach (CameraStateProfile p in states)
            {
                if (p.state == s) return p;
            }
            throw new KeyNotFoundException("Camera state not configured: " + s);
        }

        public BiomeCameraModifier Get(BiomeId b)
        {
            foreach (BiomeCameraModifier m in biomes)
            {
                if (m.biome == b) return m;
            }
            throw new KeyNotFoundException("Biome camera not configured: " + b);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(CameraSettings);
            var keys = new List<CameraState>();
            foreach (CameraStateProfile p in states)
            {
                keys.Add(p.state);
                SettingsCheck.Range(e, o, p.state + ".fieldOfView", p.fieldOfView, 10f, 90f);
                SettingsCheck.Positive(e, o, p.state + ".distance", p.distance);
                SettingsCheck.NonNegative(e, o, p.state + ".blendInSeconds", p.blendInSeconds);
                SettingsCheck.NonNegative(e, o, p.state + ".lookAhead", p.lookAhead);
                SettingsCheck.Range(e, o, p.state + ".pitchDegrees", p.pitchDegrees, 0f, maxPitchDegrees);
            }
            SettingsCheck.CoversEnum(e, o, keys);
            var bkeys = new List<BiomeId>();
            foreach (BiomeCameraModifier m in biomes)
            {
                bkeys.Add(m.biome);
                SettingsCheck.Positive(e, o, m.biome + ".distanceScale", m.distanceScale);
            }
            SettingsCheck.CoversEnum(e, o, bkeys);
            SettingsCheck.NonNegative(e, o, nameof(minDwellSeconds), minDwellSeconds);
            SettingsCheck.NonNegative(e, o, nameof(airDelaySeconds), airDelaySeconds);
            SettingsCheck.Positive(e, o, nameof(boostSpeedThreshold), boostSpeedThreshold);
            SettingsCheck.Positive(e, o, nameof(dangerLandslideSeconds), dangerLandslideSeconds);
            SettingsCheck.Positive(e, o, nameof(biomeBlendSeconds), biomeBlendSeconds);
            SettingsCheck.Positive(e, o, nameof(verticalSmoothSeconds), verticalSmoothSeconds);
            if (reducedMotionSmoothScale < 1f) e.Add(o + ".reducedMotionSmoothScale must be >= 1");
            SettingsCheck.Range(e, o, nameof(minAspect), minAspect, 1f, 4f);
            SettingsCheck.Range(e, o, nameof(maxSlopeForFramingDegrees), maxSlopeForFramingDegrees, 0f, 45f);
            SettingsCheck.Range(e, o, nameof(safeScreenExtent), safeScreenExtent, 0.1f, 1f);
            SettingsCheck.Range(e, o, nameof(maxRiderBehindCenter), maxRiderBehindCenter, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(maxPitchDegrees), maxPitchDegrees, 0f, 30f);
        }
    }

    [Serializable]
    public struct MixSnapshotEntry
    {
        public CameraState state;
        public string snapshotName;
        public float transitionSeconds;

        public MixSnapshotEntry(CameraState state, string snapshotName, float transitionSeconds)
        {
            this.state = state;
            this.snapshotName = snapshotName;
            this.transitionSeconds = transitionSeconds;
        }
    }

    [Serializable]
    public sealed class AudioMixSettings : ISettings
    {
        public MixSnapshotEntry[] snapshots =
        {
            new MixSnapshotEntry(CameraState.Cruise, "Cruise", 1.0f),
            new MixSnapshotEntry(CameraState.Air, "Air", 0.4f),
            new MixSnapshotEntry(CameraState.Boost, "Boost", 0.3f),
            new MixSnapshotEntry(CameraState.Vista, "Vista", 2.0f),
            new MixSnapshotEntry(CameraState.Danger, "Danger", 0.3f),
            new MixSnapshotEntry(CameraState.Shelter, "Shelter", 1.2f),
            new MixSnapshotEntry(CameraState.FailureHold, "FailureHold", 0.5f),
        };
        /// <summary>Low-pass cutoff when mist is clear (0) and densest (1).</summary>
        public float mistClearCutoffHz = 22000f;
        public float mistDenseCutoffHz = 1800f;

        public MixSnapshotEntry Get(CameraState s)
        {
            foreach (MixSnapshotEntry m in snapshots)
            {
                if (m.state == s) return m;
            }
            throw new KeyNotFoundException("Snapshot not configured: " + s);
        }

        /// <summary>Log-space interpolation so the low-pass sweep sounds even.</summary>
        public float CutoffFor(float mist01)
        {
            float t = mist01 < 0f ? 0f : (mist01 > 1f ? 1f : mist01);
            double a = Math.Log(mistClearCutoffHz);
            double b = Math.Log(mistDenseCutoffHz);
            return (float)Math.Exp(a + (b - a) * t);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(AudioMixSettings);
            var keys = new List<CameraState>();
            foreach (MixSnapshotEntry m in snapshots)
            {
                keys.Add(m.state);
                if (string.IsNullOrEmpty(m.snapshotName)) e.Add(o + ": empty snapshot name for " + m.state);
                SettingsCheck.NonNegative(e, o, m.state + ".transitionSeconds", m.transitionSeconds);
            }
            SettingsCheck.CoversEnum(e, o, keys);
            SettingsCheck.Positive(e, o, nameof(mistDenseCutoffHz), mistDenseCutoffHz);
            SettingsCheck.Ordered(e, o, nameof(mistDenseCutoffHz), mistDenseCutoffHz, nameof(mistClearCutoffHz), mistClearCutoffHz);
        }
    }

    [Serializable]
    public sealed class UiMotionSettings : ISettings
    {
        public float screenTransitionSeconds = 0.9f;
        public float reducedMotionTransitionSeconds = 0.25f;
        public float mistCrossfadeSeconds = 1.4f;
        public bool reducedMotionDefault = false;
        /// <summary>Near-zero HUD: score shows on change, then fades out after this long.</summary>
        public float hudIdleSeconds = 2.5f;

        public float TransitionSeconds(bool reducedMotion)
        {
            return reducedMotion ? reducedMotionTransitionSeconds : screenTransitionSeconds;
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(UiMotionSettings);
            SettingsCheck.Positive(e, o, nameof(screenTransitionSeconds), screenTransitionSeconds);
            SettingsCheck.NonNegative(e, o, nameof(reducedMotionTransitionSeconds), reducedMotionTransitionSeconds);
            SettingsCheck.Ordered(e, o, nameof(reducedMotionTransitionSeconds), reducedMotionTransitionSeconds,
                nameof(screenTransitionSeconds), screenTransitionSeconds);
            SettingsCheck.Positive(e, o, nameof(mistCrossfadeSeconds), mistCrossfadeSeconds);
            SettingsCheck.Positive(e, o, nameof(hudIdleSeconds), hudIdleSeconds);
        }
    }
}
