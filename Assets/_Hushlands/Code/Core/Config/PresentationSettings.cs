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

        public CameraStateProfile(CameraState state, float fov, float distance, float height, float lookAhead, float blendIn, int priority)
        {
            this.state = state;
            fieldOfView = fov;
            this.distance = distance;
            this.height = height;
            this.lookAhead = lookAhead;
            blendInSeconds = blendIn;
            this.priority = priority;
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
        public CameraStateProfile[] states =
        {
            new CameraStateProfile(CameraState.Cruise, 42f, 12f, 3.5f, 4f, 0.8f, 0),
            new CameraStateProfile(CameraState.Air, 46f, 14f, 4.5f, 5f, 0.5f, 2),
            new CameraStateProfile(CameraState.Boost, 50f, 11f, 3f, 7f, 0.4f, 3),
            new CameraStateProfile(CameraState.Vista, 38f, 22f, 8f, 10f, 1.6f, 1),
            new CameraStateProfile(CameraState.Danger, 44f, 10f, 3f, 9f, 0.3f, 4),
            new CameraStateProfile(CameraState.Shelter, 40f, 9f, 2.5f, 3f, 1.0f, 1),
            new CameraStateProfile(CameraState.FailureHold, 36f, 8f, 3f, 0f, 0.6f, 5),
        };

        public BiomeCameraModifier[] biomes =
        {
            new BiomeCameraModifier(BiomeId.WindmouthRidge, 2f, 1.1f, 1f),
            new BiomeCameraModifier(BiomeId.MeadowSea, 0f, 1f, 0f),
            new BiomeCameraModifier(BiomeId.PineHollow, -2f, 0.9f, -0.5f),
            new BiomeCameraModifier(BiomeId.TerraceCountry, 0f, 1.05f, 0.5f),
            new BiomeCameraModifier(BiomeId.MirrorLake, 1f, 1.1f, 0.5f),
        };

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
            }
            SettingsCheck.CoversEnum(e, o, keys);
            var bkeys = new List<BiomeId>();
            foreach (BiomeCameraModifier m in biomes)
            {
                bkeys.Add(m.biome);
                SettingsCheck.Positive(e, o, m.biome + ".distanceScale", m.distanceScale);
            }
            SettingsCheck.CoversEnum(e, o, bkeys);
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
        }
    }
}
