using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public enum WeatherKind
    {
        Clear = 0,
        Haze = 1,
        Overcast = 2,
    }

    /// <summary>One sky mood. Scales apply on top of the biome look (and the squall, which is its own hazard).</summary>
    [Serializable]
    public struct WeatherEntry
    {
        public WeatherKind kind;
        /// <summary>Relative chance of being picked next.</summary>
        public float weight;
        public float sunLuxScale;
        /// <summary>Fog mean free path multiplier (below 1 = thicker). Mist fairness is checked with it.</summary>
        public float fogScale;
        public float exposureOffsetEV;

        public WeatherEntry(WeatherKind kind, float weight, float sunLuxScale, float fogScale, float exposureOffsetEV)
        {
            this.kind = kind;
            this.weight = weight;
            this.sunLuxScale = sunLuxScale;
            this.fogScale = fogScale;
            this.exposureOffsetEV = exposureOffsetEV;
        }
    }

    /// <summary>A small, gentle sight gag beside the track, shown only in calm moments.</summary>
    [Serializable]
    public struct HumorBeatEntry
    {
        public string id;
        public float weight;
        public string line;

        public HumorBeatEntry(string id, float weight, string line)
        {
            this.id = id;
            this.weight = weight;
            this.line = line;
        }
    }

    /// <summary>How the rider settles after a spill of one cause (calm, never violent).</summary>
    [Serializable]
    public struct ChoreographyEntry
    {
        public CrashCause cause;
        /// <summary>The rider leans back to this angle as they sit down.</summary>
        public float leanDegrees;
        /// <summary>The rider sinks this far into the soft ground.</summary>
        public float sinkMeters;
        public float settleSeconds;
        /// <summary>A soft mist puff covers the rider within this time (0 = no puff).</summary>
        public float coverSeconds;

        public ChoreographyEntry(CrashCause cause, float leanDegrees, float sinkMeters, float settleSeconds, float coverSeconds)
        {
            this.cause = cause;
            this.leanDegrees = leanDegrees;
            this.sinkMeters = sinkMeters;
            this.settleSeconds = settleSeconds;
            this.coverSeconds = coverSeconds;
        }
    }

    /// <summary>Time of day, weather, pacing, humor beats and failure choreography (T15). Presentation only: no rule of play changes.</summary>
    [Serializable]
    public sealed class DirectorSettings : ISettings
    {
        // --- Time of day: the light never stands still ---
        /// <summary>The sun swings this far above and below the biome's elevation.</summary>
        public float dayDriftElevationDegrees = 2.5f;
        public float dayDriftPeriodSeconds = 300f;
        /// <summary>Colour temperature swing, in step with the elevation (lower sun = warmer).</summary>
        public float dayDriftKelvin = 250f;
        public float dayDriftExposureEV = 0.15f;
        /// <summary>The sun never sinks below this (the track always stays lit).</summary>
        public float minSunElevationDegrees = 3f;

        // --- Weather ---
        public WeatherEntry[] weather =
        {
            new WeatherEntry(WeatherKind.Clear, 5f, 1f, 1f, 0f),
            new WeatherEntry(WeatherKind.Haze, 3f, 0.88f, 0.97f, -0.1f),
            new WeatherEntry(WeatherKind.Overcast, 2f, 0.7f, 0.95f, -0.25f),
        };
        public float weatherHoldMinSeconds = 45f;
        public float weatherHoldMaxSeconds = 90f;
        public float weatherTransitionSeconds = 12f;

        // --- Pacing ---
        /// <summary>Hazards within this many seconds ahead count toward tension.</summary>
        public float pacingLookaheadSeconds = 4f;
        /// <summary>This many hazards in the lookahead is full hazard tension.</summary>
        public int pacingFullHazards = 3;
        public float pacingHazardWeight = 0.5f;
        public float pacingDangerWeight = 0.6f;
        public float pacingSquallWeight = 0.3f;
        /// <summary>Added on a spill, then decays.</summary>
        public float pacingFailureHeat = 0.4f;
        public float pacingHeatDecayPerSecond = 0.08f;
        /// <summary>How fast intensity follows its target (1/s).</summary>
        public float pacingResponse = 1.5f;
        public float pacingCalmThreshold = 0.25f;
        public float pacingTenseThreshold = 0.6f;
        /// <summary>Calm only counts after it has lasted this long.</summary>
        public float pacingCalmHoldSeconds = 2f;

        // --- Humor beats ---
        public bool humorEnabled = true;
        public HumorBeatEntry[] humorBeats =
        {
            new HumorBeatEntry("KettlePuff", 3f, "A kettle whistles hello."),
            new HumorBeatEntry("HatTip", 3f, "The signpost tips its hat."),
            new HumorBeatEntry("SnoozingCairn", 2f, "The cairn snores softly."),
        };
        public float humorFirstSeconds = 18f;
        public float humorIntervalMinSeconds = 35f;
        public float humorIntervalMaxSeconds = 70f;
        public float humorDurationSeconds = 5f;
        /// <summary>The beat appears this far ahead of the rider, behind the track.</summary>
        public float humorAheadMeters = 32f;
        public float humorDepthMeters = 6f;
        public float humorPropScale = 1.2f;
        /// <summary>Bob / tip / snore cycles per second.</summary>
        public float humorCycleHz = 1.2f;

        // --- Failure choreography ---
        public ChoreographyEntry[] choreography =
        {
            new ChoreographyEntry(CrashCause.Obstacle, 22f, 0.12f, 0.45f, 0f),
            new ChoreographyEntry(CrashCause.BadLanding, 30f, 0.18f, 0.4f, 0f),
            new ChoreographyEntry(CrashCause.Landslide, 10f, 0.1f, 0.3f, 0.25f),
            new ChoreographyEntry(CrashCause.Gap, 0f, 0f, 0.2f, 0.15f),
        };
        public float puffRadiusMeters = 2.2f;

        public WeatherEntry Get(WeatherKind kind)
        {
            foreach (WeatherEntry w in weather)
            {
                if (w.kind == kind) return w;
            }
            throw new KeyNotFoundException("Weather not configured: " + kind);
        }

        public ChoreographyEntry Get(CrashCause cause)
        {
            foreach (ChoreographyEntry c in choreography)
            {
                if (c.cause == cause) return c;
            }
            throw new KeyNotFoundException("Choreography not configured: " + cause);
        }

        /// <summary>Thickest weather fog (smallest scale), for the mist fairness check.</summary>
        public float WorstFogScale()
        {
            float worst = 1f;
            foreach (WeatherEntry w in weather) worst = Math.Min(worst, w.fogScale);
            return worst;
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(DirectorSettings);
            SettingsCheck.NonNegative(e, o, nameof(dayDriftElevationDegrees), dayDriftElevationDegrees);
            SettingsCheck.Positive(e, o, nameof(dayDriftPeriodSeconds), dayDriftPeriodSeconds);
            SettingsCheck.NonNegative(e, o, nameof(dayDriftKelvin), dayDriftKelvin);
            SettingsCheck.NonNegative(e, o, nameof(dayDriftExposureEV), dayDriftExposureEV);
            SettingsCheck.Positive(e, o, nameof(minSunElevationDegrees), minSunElevationDegrees);

            var kinds = new List<WeatherKind>();
            float total = 0f;
            foreach (WeatherEntry w in weather ?? new WeatherEntry[0])
            {
                kinds.Add(w.kind);
                string t = o + "." + w.kind;
                SettingsCheck.NonNegative(e, t, nameof(w.weight), w.weight);
                SettingsCheck.Range(e, t, nameof(w.sunLuxScale), w.sunLuxScale, 0.3f, 1.5f);
                SettingsCheck.Range(e, t, nameof(w.fogScale), w.fogScale, 0.5f, 1.5f);
                SettingsCheck.Range(e, t, nameof(w.exposureOffsetEV), w.exposureOffsetEV, -1f, 1f);
                total += w.weight;
            }
            SettingsCheck.CoversEnum(e, o, kinds);
            if (total <= 0f) e.Add(o + ": weather weights sum to zero");
            SettingsCheck.Positive(e, o, nameof(weatherHoldMinSeconds), weatherHoldMinSeconds);
            SettingsCheck.Ordered(e, o, nameof(weatherHoldMinSeconds), weatherHoldMinSeconds, nameof(weatherHoldMaxSeconds), weatherHoldMaxSeconds);
            SettingsCheck.Positive(e, o, nameof(weatherTransitionSeconds), weatherTransitionSeconds);
            if (weatherTransitionSeconds > weatherHoldMinSeconds) e.Add(o + ": weather would change before it settles");

            SettingsCheck.Positive(e, o, nameof(pacingLookaheadSeconds), pacingLookaheadSeconds);
            SettingsCheck.Positive(e, o, nameof(pacingFullHazards), pacingFullHazards);
            SettingsCheck.NonNegative(e, o, nameof(pacingHazardWeight), pacingHazardWeight);
            SettingsCheck.NonNegative(e, o, nameof(pacingDangerWeight), pacingDangerWeight);
            SettingsCheck.NonNegative(e, o, nameof(pacingSquallWeight), pacingSquallWeight);
            SettingsCheck.NonNegative(e, o, nameof(pacingFailureHeat), pacingFailureHeat);
            SettingsCheck.Positive(e, o, nameof(pacingHeatDecayPerSecond), pacingHeatDecayPerSecond);
            SettingsCheck.Positive(e, o, nameof(pacingResponse), pacingResponse);
            SettingsCheck.Range(e, o, nameof(pacingCalmThreshold), pacingCalmThreshold, 0f, 1f);
            SettingsCheck.Ordered(e, o, nameof(pacingCalmThreshold), pacingCalmThreshold, nameof(pacingTenseThreshold), pacingTenseThreshold);
            SettingsCheck.NonNegative(e, o, nameof(pacingCalmHoldSeconds), pacingCalmHoldSeconds);

            if (humorBeats == null || humorBeats.Length == 0) e.Add(o + ": no humor beats");
            var ids = new HashSet<string>();
            float beatTotal = 0f;
            foreach (HumorBeatEntry b in humorBeats ?? new HumorBeatEntry[0])
            {
                if (string.IsNullOrEmpty(b.id) || !ids.Add(b.id)) e.Add(o + ": bad or duplicate humor beat '" + b.id + "'");
                if (string.IsNullOrEmpty(b.line)) e.Add(o + ": humor beat " + b.id + " has no line");
                SettingsCheck.NonNegative(e, o, b.id + ".weight", b.weight);
                beatTotal += b.weight;
            }
            if (beatTotal <= 0f) e.Add(o + ": humor weights sum to zero");
            SettingsCheck.NonNegative(e, o, nameof(humorFirstSeconds), humorFirstSeconds);
            SettingsCheck.Positive(e, o, nameof(humorIntervalMinSeconds), humorIntervalMinSeconds);
            SettingsCheck.Ordered(e, o, nameof(humorIntervalMinSeconds), humorIntervalMinSeconds, nameof(humorIntervalMaxSeconds), humorIntervalMaxSeconds);
            SettingsCheck.Positive(e, o, nameof(humorDurationSeconds), humorDurationSeconds);
            if (humorDurationSeconds >= humorIntervalMinSeconds) e.Add(o + ": humor beats would overlap");
            SettingsCheck.Positive(e, o, nameof(humorAheadMeters), humorAheadMeters);
            SettingsCheck.Positive(e, o, nameof(humorDepthMeters), humorDepthMeters);
            SettingsCheck.Positive(e, o, nameof(humorPropScale), humorPropScale);
            SettingsCheck.Positive(e, o, nameof(humorCycleHz), humorCycleHz);

            var causes = new List<CrashCause>();
            foreach (ChoreographyEntry c in choreography ?? new ChoreographyEntry[0])
            {
                causes.Add(c.cause);
                string t = o + "." + c.cause;
                SettingsCheck.Range(e, t, nameof(c.leanDegrees), c.leanDegrees, 0f, 45f);
                SettingsCheck.Range(e, t, nameof(c.sinkMeters), c.sinkMeters, 0f, 0.5f);
                SettingsCheck.Positive(e, t, nameof(c.settleSeconds), c.settleSeconds);
                SettingsCheck.NonNegative(e, t, nameof(c.coverSeconds), c.coverSeconds);
            }
            SettingsCheck.CoversEnum(e, o, causes);
            SettingsCheck.Positive(e, o, nameof(puffRadiusMeters), puffRadiusMeters);
        }
    }
}
