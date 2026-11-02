using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    [Serializable]
    public sealed class FairnessSettings : ISettings
    {
        /// <summary>Canon: obstacles visible at least this long before contact at current speed.</summary>
        public float obstacleRevealSeconds = 1.5f;
        /// <summary>Canon: collapse warning lead time.</summary>
        public float collapseWarningSeconds = 1f;
        /// <summary>Mist density above which a required jump counts as hidden (0..1).</summary>
        public float maxMistOverRequiredJump = 0.35f;
        public float minSafeLandingLength = 6f;
        public float minGapBetweenObstacles = 4f;
        /// <summary>Takeoff window (seconds before hazard start) that must clear at every speed.</summary>
        public float takeoffWindowMinSeconds = 0.15f;
        public float takeoffWindowMaxSeconds = 0.35f;
        public float obstacleClearanceMeters = 0.2f;
        /// <summary>Rider must touch down at least this long after passing a gap.</summary>
        public float landingMarginSeconds = 0.12f;
        /// <summary>Camera framing contract: ground visible ahead of the rider (T4 must honour).</summary>
        public float visibleAheadMeters = 40f;
        /// <summary>After a checkpoint, first hazard at least this many seconds away at minimum speed.</summary>
        public float checkpointLeadSeconds = 2f;
        public int speedSamples = 7;
        public float nestHazardSpacing = 6f;
        /// <summary>Reference bot takes off this long before each required jump.</summary>
        public float botTakeoffSeconds = 0.25f;

        public void Validate(List<string> e)
        {
            const string o = nameof(FairnessSettings);
            SettingsCheck.Positive(e, o, nameof(obstacleRevealSeconds), obstacleRevealSeconds);
            SettingsCheck.Positive(e, o, nameof(collapseWarningSeconds), collapseWarningSeconds);
            SettingsCheck.Range(e, o, nameof(maxMistOverRequiredJump), maxMistOverRequiredJump, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(minSafeLandingLength), minSafeLandingLength);
            SettingsCheck.Positive(e, o, nameof(minGapBetweenObstacles), minGapBetweenObstacles);
            SettingsCheck.NonNegative(e, o, nameof(takeoffWindowMinSeconds), takeoffWindowMinSeconds);
            SettingsCheck.Ordered(e, o, nameof(takeoffWindowMinSeconds), takeoffWindowMinSeconds,
                nameof(takeoffWindowMaxSeconds), takeoffWindowMaxSeconds);
            SettingsCheck.NonNegative(e, o, nameof(obstacleClearanceMeters), obstacleClearanceMeters);
            SettingsCheck.NonNegative(e, o, nameof(landingMarginSeconds), landingMarginSeconds);
            SettingsCheck.Positive(e, o, nameof(visibleAheadMeters), visibleAheadMeters);
            SettingsCheck.Positive(e, o, nameof(checkpointLeadSeconds), checkpointLeadSeconds);
            if (speedSamples < 2) e.Add(o + ".speedSamples must be >= 2");
            SettingsCheck.NonNegative(e, o, nameof(nestHazardSpacing), nestHazardSpacing);
            SettingsCheck.Range(e, o, nameof(botTakeoffSeconds), botTakeoffSeconds, takeoffWindowMinSeconds,
                takeoffWindowMaxSeconds);
        }

        /// <summary>Minimum sight distance for the reveal rule at a given speed.</summary>
        public float RevealDistance(float speed)
        {
            return speed * obstacleRevealSeconds;
        }
    }

    [Serializable]
    public sealed class ContentSettings : ISettings
    {
        public float minChunkLength = 40f;
        public float maxChunkLength = 140f;
        public int minDifficulty = 0;
        public int maxDifficulty = 5;
        public int chunksPerStretch = 12;
        /// <summary>Canon acceptance for T2: this many stretches must pass.</summary>
        public int validationStretchCount = 200;

        // --- Geometry ---
        public float minSlopeDegrees = 6f;
        public float maxSlopeDegrees = 14f;
        public float leadInMeters = 14f;
        public float tailOutMeters = 6f;
        public float checkpointOffsetMeters = 2f;
        public float secondarySurfaceChance = 0.35f;

        // --- Hazards ---
        public float hazardsBase = 1f;
        public float hazardsPerDifficulty = 0.5f;
        public float obstacleWeight = 0.45f;
        public float gapWeight = 0.35f;
        public int collapseMinDifficulty = 2;
        public float obstacleMinLength = 0.8f;
        public float obstacleMaxLength = 2.5f;
        public float obstacleMinHeight = 0.35f;
        public float obstacleMaxHeight = 0.9f;
        public float gapMinLength = 1.5f;
        public float gapMaxLength = 5f;
        public float collapseMinLength = 2f;
        public float collapseMaxLength = 4.5f;
        public float collapseWarningSlack = 1.25f;
        public float hazardSpacingJitterMeters = 8f;
        public float sizeShrinkFactor = 0.85f;
        public int sizeShrinkTries = 6;

        // --- Dressing ---
        public float mistBankChance = 0.6f;
        public float mistMinLength = 10f;
        public float mistMaxLength = 40f;
        public float mistMaxDensity = 0.85f;
        /// <summary>Mist over a reveal window is clamped to this fraction of the fairness threshold.</summary>
        public float mistSafeFactor = 0.85f;
        public int pickupsPerChunk = 3;
        public float rarePickupChance = 0.1f;
        public float nestChance = 0.5f;
        public int placementTries = 12;

        public void Validate(List<string> e)
        {
            const string o = nameof(ContentSettings);
            SettingsCheck.Positive(e, o, nameof(minChunkLength), minChunkLength);
            SettingsCheck.Ordered(e, o, nameof(minChunkLength), minChunkLength, nameof(maxChunkLength), maxChunkLength);
            SettingsCheck.NonNegative(e, o, nameof(minDifficulty), minDifficulty);
            SettingsCheck.Ordered(e, o, nameof(minDifficulty), minDifficulty, nameof(maxDifficulty), maxDifficulty);
            SettingsCheck.Positive(e, o, nameof(chunksPerStretch), chunksPerStretch);
            SettingsCheck.Positive(e, o, nameof(validationStretchCount), validationStretchCount);
            SettingsCheck.Range(e, o, nameof(minSlopeDegrees), minSlopeDegrees, 1f, 30f);
            SettingsCheck.Ordered(e, o, nameof(minSlopeDegrees), minSlopeDegrees, nameof(maxSlopeDegrees), maxSlopeDegrees);
            SettingsCheck.Range(e, o, nameof(maxSlopeDegrees), maxSlopeDegrees, 1f, 30f);
            SettingsCheck.Positive(e, o, nameof(leadInMeters), leadInMeters);
            SettingsCheck.NonNegative(e, o, nameof(tailOutMeters), tailOutMeters);
            SettingsCheck.NonNegative(e, o, nameof(checkpointOffsetMeters), checkpointOffsetMeters);
            SettingsCheck.Ordered(e, o, nameof(checkpointOffsetMeters), checkpointOffsetMeters, nameof(leadInMeters), leadInMeters);
            SettingsCheck.Range(e, o, nameof(secondarySurfaceChance), secondarySurfaceChance, 0f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(hazardsBase), hazardsBase);
            SettingsCheck.NonNegative(e, o, nameof(hazardsPerDifficulty), hazardsPerDifficulty);
            SettingsCheck.Range(e, o, nameof(obstacleWeight), obstacleWeight, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(gapWeight), gapWeight, 0f, 1f - obstacleWeight);
            SettingsCheck.Positive(e, o, nameof(obstacleMinLength), obstacleMinLength);
            SettingsCheck.Ordered(e, o, nameof(obstacleMinLength), obstacleMinLength, nameof(obstacleMaxLength), obstacleMaxLength);
            SettingsCheck.Positive(e, o, nameof(obstacleMinHeight), obstacleMinHeight);
            SettingsCheck.Ordered(e, o, nameof(obstacleMinHeight), obstacleMinHeight, nameof(obstacleMaxHeight), obstacleMaxHeight);
            SettingsCheck.Positive(e, o, nameof(gapMinLength), gapMinLength);
            SettingsCheck.Ordered(e, o, nameof(gapMinLength), gapMinLength, nameof(gapMaxLength), gapMaxLength);
            SettingsCheck.Positive(e, o, nameof(collapseMinLength), collapseMinLength);
            SettingsCheck.Ordered(e, o, nameof(collapseMinLength), collapseMinLength, nameof(collapseMaxLength), collapseMaxLength);
            if (collapseWarningSlack < 1f) e.Add(o + ".collapseWarningSlack must be >= 1");
            SettingsCheck.NonNegative(e, o, nameof(hazardSpacingJitterMeters), hazardSpacingJitterMeters);
            SettingsCheck.Range(e, o, nameof(sizeShrinkFactor), sizeShrinkFactor, 0.1f, 0.99f);
            SettingsCheck.NonNegative(e, o, nameof(sizeShrinkTries), sizeShrinkTries);
            SettingsCheck.Range(e, o, nameof(mistBankChance), mistBankChance, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(mistMinLength), mistMinLength);
            SettingsCheck.Ordered(e, o, nameof(mistMinLength), mistMinLength, nameof(mistMaxLength), mistMaxLength);
            SettingsCheck.Range(e, o, nameof(mistMaxDensity), mistMaxDensity, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(mistSafeFactor), mistSafeFactor, 0f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(pickupsPerChunk), pickupsPerChunk);
            SettingsCheck.Range(e, o, nameof(rarePickupChance), rarePickupChance, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(nestChance), nestChance, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(placementTries), placementTries);
        }
    }

    [Serializable]
    public struct BiomeEntry
    {
        public BiomeId biome;
        public int order;
        public float lengthMeters;
        public float baseMistDensity;
        public SurfaceId primarySurface;

        public BiomeEntry(BiomeId biome, int order, float lengthMeters, float baseMistDensity, SurfaceId primarySurface)
        {
            this.biome = biome;
            this.order = order;
            this.lengthMeters = lengthMeters;
            this.baseMistDensity = baseMistDensity;
            this.primarySurface = primarySurface;
        }
    }

    [Serializable]
    public sealed class BiomeSettings : ISettings
    {
        public BiomeEntry[] entries =
        {
            new BiomeEntry(BiomeId.WindmouthRidge, 0, 900f, 0.30f, SurfaceId.Snow),
            new BiomeEntry(BiomeId.MeadowSea, 1, 1200f, 0.20f, SurfaceId.Grass),
            new BiomeEntry(BiomeId.PineHollow, 2, 1100f, 0.35f, SurfaceId.PineNeedles),
            new BiomeEntry(BiomeId.TerraceCountry, 3, 1200f, 0.25f, SurfaceId.TeaTerrace),
            new BiomeEntry(BiomeId.MirrorLake, 4, 1000f, 0.40f, SurfaceId.Boardwalk),
        };

        public BiomeEntry Get(BiomeId id)
        {
            foreach (BiomeEntry b in entries)
            {
                if (b.biome == id) return b;
            }
            throw new KeyNotFoundException("Biome not configured: " + id);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(BiomeSettings);
            var keys = new List<BiomeId>();
            var orders = new HashSet<int>();
            foreach (BiomeEntry b in entries)
            {
                keys.Add(b.biome);
                if (!orders.Add(b.order)) e.Add(o + ": duplicate order " + b.order);
                SettingsCheck.Positive(e, o, b.biome + ".lengthMeters", b.lengthMeters);
                SettingsCheck.Range(e, o, b.biome + ".baseMistDensity", b.baseMistDensity, 0f, 1f);
            }
            SettingsCheck.CoversEnum(e, o, keys);
        }
    }
}
