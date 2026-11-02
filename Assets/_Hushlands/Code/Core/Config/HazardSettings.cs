using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Landslide chase, collapse timing and squall weather (T10).</summary>
    [Serializable]
    public sealed class HazardSettings : ISettings
    {
        // --- Landslide ---
        public bool landslideEnabled = true;
        /// <summary>Calm start: no chase for this long after a run start or retry.</summary>
        public float landslideStartDelaySeconds = 4f;
        public float landslideStartGap = 30f;
        /// <summary>The slide eases toward this gap behind the rider (tension without unfair catches).</summary>
        public float landslidePreferredGap = 22f;
        /// <summary>Base pace as a fraction of the rider's cruise target.</summary>
        public float landslidePaceRatio = 0.95f;
        /// <summary>Pull toward the preferred gap (1/s).</summary>
        public float landslideGapGain = 0.15f;
        /// <summary>Above the rider's 6 m/s floor, so only a rider stalled at minimum speed is slowly closed on.</summary>
        public float landslideMinSpeed = 6.3f;
        public float landslideMaxSpeed = 17.5f;
        public float landslideRetryGap = 30f;
        /// <summary>Closer than this counts as Danger even when the slide is not gaining.</summary>
        public float landslideDangerGap = 10f;

        // --- Collapse zones ---
        /// <summary>A collapse zone starts cracking when the rider is within its content warningLead.</summary>
        public bool collapseEnabled = true;

        // --- Squall ---
        public bool squallEnabled = true;
        public float squallFirstSeconds = 25f;
        public float squallIntervalMinSeconds = 40f;
        public float squallIntervalMaxSeconds = 70f;
        public float squallDurationSeconds = 12f;
        public float squallRampSeconds = 3f;
        /// <summary>Headwind at full intensity (m/s off the cruise target, never below minSpeed).</summary>
        public float squallSpeedPenalty = 1f;
        /// <summary>Global fog mean free path multiplier at full intensity (mist fairness is checked with it).</summary>
        public float squallFogScale = 0.85f;
        public float squallMistBonus = 0.15f;
        public float squallWindX = -3.5f;
        public float squallWindY = -0.5f;
        public float squallWindZ = 1.5f;

        public void Validate(List<string> e)
        {
            const string o = nameof(HazardSettings);
            SettingsCheck.NonNegative(e, o, nameof(landslideStartDelaySeconds), landslideStartDelaySeconds);
            SettingsCheck.Positive(e, o, nameof(landslideStartGap), landslideStartGap);
            SettingsCheck.Positive(e, o, nameof(landslidePreferredGap), landslidePreferredGap);
            SettingsCheck.Range(e, o, nameof(landslidePaceRatio), landslidePaceRatio, 0.5f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(landslideGapGain), landslideGapGain);
            SettingsCheck.Positive(e, o, nameof(landslideMinSpeed), landslideMinSpeed);
            SettingsCheck.Ordered(e, o, nameof(landslideMinSpeed), landslideMinSpeed, nameof(landslideMaxSpeed), landslideMaxSpeed);
            SettingsCheck.Positive(e, o, nameof(landslideRetryGap), landslideRetryGap);
            SettingsCheck.Positive(e, o, nameof(landslideDangerGap), landslideDangerGap);
            SettingsCheck.NonNegative(e, o, nameof(squallFirstSeconds), squallFirstSeconds);
            SettingsCheck.Positive(e, o, nameof(squallIntervalMinSeconds), squallIntervalMinSeconds);
            SettingsCheck.Ordered(e, o, nameof(squallIntervalMinSeconds), squallIntervalMinSeconds, nameof(squallIntervalMaxSeconds), squallIntervalMaxSeconds);
            SettingsCheck.Positive(e, o, nameof(squallDurationSeconds), squallDurationSeconds);
            SettingsCheck.Positive(e, o, nameof(squallRampSeconds), squallRampSeconds);
            if (squallRampSeconds * 2f > squallDurationSeconds) e.Add(o + ": squall ramps longer than its duration");
            if (squallDurationSeconds >= squallIntervalMinSeconds) e.Add(o + ": squalls would overlap");
            SettingsCheck.NonNegative(e, o, nameof(squallSpeedPenalty), squallSpeedPenalty);
            SettingsCheck.Range(e, o, nameof(squallFogScale), squallFogScale, 0.1f, 1f);
            SettingsCheck.Range(e, o, nameof(squallMistBonus), squallMistBonus, 0f, 1f);
        }
    }
}
