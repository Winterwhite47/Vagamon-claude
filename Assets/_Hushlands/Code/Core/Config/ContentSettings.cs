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

        public void Validate(List<string> e)
        {
            const string o = nameof(FairnessSettings);
            SettingsCheck.Positive(e, o, nameof(obstacleRevealSeconds), obstacleRevealSeconds);
            SettingsCheck.Positive(e, o, nameof(collapseWarningSeconds), collapseWarningSeconds);
            SettingsCheck.Range(e, o, nameof(maxMistOverRequiredJump), maxMistOverRequiredJump, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(minSafeLandingLength), minSafeLandingLength);
            SettingsCheck.Positive(e, o, nameof(minGapBetweenObstacles), minGapBetweenObstacles);
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
        public float maxChunkLength = 120f;
        public int minDifficulty = 0;
        public int maxDifficulty = 5;
        public int chunksPerStretch = 12;
        /// <summary>Canon acceptance for T2: this many stretches must pass.</summary>
        public int validationStretchCount = 200;
        /// <summary>Slope angle used by the stub bot validator until T2 provides real chunk geometry.</summary>
        public float botTestSlopeDegrees = 12f;

        public void Validate(List<string> e)
        {
            const string o = nameof(ContentSettings);
            SettingsCheck.Positive(e, o, nameof(minChunkLength), minChunkLength);
            SettingsCheck.Ordered(e, o, nameof(minChunkLength), minChunkLength, nameof(maxChunkLength), maxChunkLength);
            SettingsCheck.NonNegative(e, o, nameof(minDifficulty), minDifficulty);
            SettingsCheck.Ordered(e, o, nameof(minDifficulty), minDifficulty, nameof(maxDifficulty), maxDifficulty);
            SettingsCheck.Positive(e, o, nameof(chunksPerStretch), chunksPerStretch);
            SettingsCheck.Positive(e, o, nameof(validationStretchCount), validationStretchCount);
            SettingsCheck.Range(e, o, nameof(botTestSlopeDegrees), botTestSlopeDegrees, 1f, 45f);
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
