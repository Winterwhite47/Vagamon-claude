using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Per-biome lighting, mist and terrain. Colours are hex strings so canon palette values stay exact.</summary>
    [Serializable]
    public struct BiomeLook
    {
        public BiomeId biome;
        /// <summary>False = placeholder values; T14 tunes each biome under its own approval.</summary>
        public bool tuned;
        public float sunElevationDegrees;
        public float sunAzimuthDegrees;
        public float sunLux;
        public float sunKelvin;
        public float exposureEV;
        public float fogMeanFreePath;
        public float fogBaseHeight;
        public float fogMaximumHeight;
        public float fogAnisotropy;
        public string fogAlbedo;
        public string grass;
        public string soil;
        public string accent;
        public float hillAmplitude;
        public float hillWavelength;
        public float detailAmplitude;
        public float detailWavelength;
    }

    [Serializable]
    public struct MaterialSpec
    {
        public string name;
        public string color;
        public float smoothness;
        public float metallic;

        public MaterialSpec(string name, string color, float smoothness, float metallic)
        {
            this.name = name;
            this.color = color;
            this.smoothness = smoothness;
            this.metallic = metallic;
        }
    }

    [Serializable]
    public sealed class LookSettings : ISettings
    {
        public const string Emerald = "#0B3B17";
        public const string Terracotta = "#8D3F2D";
        public const string MistBlue = "#4A6B82";
        public const string Gold = "#D4AF37";

        public BiomeLook[] biomes =
        {
            Placeholder(BiomeId.WindmouthRidge, 18f, "#DDE6EA", "#6E5A4E"),
            new BiomeLook
            {
                // Golden hour: low warm sun from behind-left, long soft light through mist (T7 vertical slice).
                biome = BiomeId.MeadowSea, tuned = true,
                sunElevationDegrees = 9f, sunAzimuthDegrees = -38f, sunLux = 32000f, sunKelvin = 3600f,
                exposureEV = 12.6f,
                fogMeanFreePath = 220f, fogBaseHeight = -40f, fogMaximumHeight = 60f, fogAnisotropy = 0.72f,
                fogAlbedo = MistBlue, grass = Emerald, soil = Terracotta, accent = Gold,
                hillAmplitude = 14f, hillWavelength = 140f, detailAmplitude = 1.6f, detailWavelength = 18f,
            },
            Placeholder(BiomeId.PineHollow, 30f, "#1F3A24", "#5B4636"),
            Placeholder(BiomeId.TerraceCountry, 35f, "#3E6B2A", Terracotta),
            Placeholder(BiomeId.MirrorLake, 22f, "#2F5A3A", "#6B5B4B"),
        };

        public MaterialSpec[] materials =
        {
            new MaterialSpec("Grass", Emerald, 0.25f, 0f),
            new MaterialSpec("Track", Terracotta, 0.2f, 0f),
            new MaterialSpec("Stone", "#7A7468", 0.3f, 0f),
            new MaterialSpec("Wood", "#6B3A24", 0.35f, 0f),
            new MaterialSpec("Jacket", MistBlue, 0.4f, 0f),
            new MaterialSpec("Scarf", Gold, 0.5f, 0f),
            new MaterialSpec("Nest", "#A08250", 0.2f, 0f),
            new MaterialSpec("Pickup", Gold, 0.8f, 1f),
            new MaterialSpec("Skin", "#C99A7A", 0.45f, 0f),
            new MaterialSpec("Lantern", "#F2C46D", 0.6f, 0f),
            new MaterialSpec("Feather", "#E8EEF2", 0.3f, 0f),
            new MaterialSpec("Tea", Terracotta, 0.7f, 0f),
            new MaterialSpec("Bell", "#B8B2A3", 0.85f, 1f),
            new MaterialSpec("Duckling", "#E9C46A", 0.35f, 0f),
        };

        // --- Terrain mesh ---
        public float terrainCellMeters = 2f;
        /// <summary>Terrain extent toward the camera (negative z) and behind the track (positive z).</summary>
        public float terrainFrontDepth = 30f;
        public float terrainBackDepth = 220f;
        /// <summary>Exactly flat (equal to the slope line) within this half-width of the track.</summary>
        public float corridorHalfWidth = 3f;
        public float corridorBlend = 8f;
        /// <summary>Front terrain drops this far below the track so it never hides the rider.</summary>
        public float frontDrop = 2.5f;
        public float gapDepth = 3f;
        public float trackStripHalfWidth = 1.5f;

        // --- Mist volumes ---
        /// <summary>Local fog mean free path for a mist bank of density 1 (denser = shorter).</summary>
        public float mistMeanFreePathAtFull = 45f;
        public float mistVolumeHeight = 14f;
        public float mistVolumeDepth = 40f;
        /// <summary>
        /// "Mist never hides required jumps": light from a jump through global fog over the camera distance plus the
        /// front half of a mist bank at the fairness threshold must keep at least this transmittance.
        /// </summary>
        public float minRevealTransmittance = 0.6f;

        public BiomeLook Get(BiomeId id)
        {
            foreach (BiomeLook b in biomes)
            {
                if (b.biome == id) return b;
            }
            throw new KeyNotFoundException("Look not configured: " + id);
        }

        public MaterialSpec Material(string name)
        {
            foreach (MaterialSpec m in materials)
            {
                if (m.name == name) return m;
            }
            throw new KeyNotFoundException("Material not configured: " + name);
        }

        private static BiomeLook Placeholder(BiomeId id, float elevation, string grass, string soil)
        {
            return new BiomeLook
            {
                biome = id, tuned = false,
                sunElevationDegrees = elevation, sunAzimuthDegrees = -30f, sunLux = 60000f, sunKelvin = 5200f,
                exposureEV = 13.5f, fogMeanFreePath = 180f, fogBaseHeight = -40f, fogMaximumHeight = 60f,
                fogAnisotropy = 0.6f, fogAlbedo = MistBlue, grass = grass, soil = soil, accent = Gold,
                hillAmplitude = 12f, hillWavelength = 120f, detailAmplitude = 1.2f, detailWavelength = 16f,
            };
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(LookSettings);
            var keys = new List<BiomeId>();
            foreach (BiomeLook b in biomes)
            {
                keys.Add(b.biome);
                string t = o + "." + b.biome;
                SettingsCheck.Range(e, t, nameof(b.sunElevationDegrees), b.sunElevationDegrees, -5f, 90f);
                SettingsCheck.Positive(e, t, nameof(b.sunLux), b.sunLux);
                SettingsCheck.Range(e, t, nameof(b.sunKelvin), b.sunKelvin, 1500f, 20000f);
                SettingsCheck.Positive(e, t, nameof(b.fogMeanFreePath), b.fogMeanFreePath);
                SettingsCheck.Ordered(e, t, nameof(b.fogBaseHeight), b.fogBaseHeight, nameof(b.fogMaximumHeight), b.fogMaximumHeight);
                SettingsCheck.Range(e, t, nameof(b.fogAnisotropy), b.fogAnisotropy, -1f, 1f);
                SettingsCheck.NonNegative(e, t, nameof(b.hillAmplitude), b.hillAmplitude);
                SettingsCheck.Positive(e, t, nameof(b.hillWavelength), b.hillWavelength);
                SettingsCheck.NonNegative(e, t, nameof(b.detailAmplitude), b.detailAmplitude);
                SettingsCheck.Positive(e, t, nameof(b.detailWavelength), b.detailWavelength);
                foreach (string hex in new[] { b.fogAlbedo, b.grass, b.soil, b.accent })
                {
                    if (!Palette.TryParse(hex, out _, out _, out _)) e.Add(t + ": bad colour '" + hex + "'");
                }
            }
            SettingsCheck.CoversEnum(e, o, keys);
            var names = new HashSet<string>();
            foreach (MaterialSpec m in materials)
            {
                if (string.IsNullOrEmpty(m.name) || !names.Add(m.name)) e.Add(o + ": bad or duplicate material name '" + m.name + "'");
                if (!Palette.TryParse(m.color, out _, out _, out _)) e.Add(o + ": bad colour for " + m.name);
                SettingsCheck.Range(e, o, m.name + ".smoothness", m.smoothness, 0f, 1f);
                SettingsCheck.Range(e, o, m.name + ".metallic", m.metallic, 0f, 1f);
            }
            SettingsCheck.Positive(e, o, nameof(terrainCellMeters), terrainCellMeters);
            SettingsCheck.Positive(e, o, nameof(terrainFrontDepth), terrainFrontDepth);
            SettingsCheck.Positive(e, o, nameof(terrainBackDepth), terrainBackDepth);
            SettingsCheck.Positive(e, o, nameof(corridorHalfWidth), corridorHalfWidth);
            SettingsCheck.Positive(e, o, nameof(corridorBlend), corridorBlend);
            SettingsCheck.Positive(e, o, nameof(frontDrop), frontDrop);
            SettingsCheck.Positive(e, o, nameof(gapDepth), gapDepth);
            SettingsCheck.Positive(e, o, nameof(trackStripHalfWidth), trackStripHalfWidth);
            SettingsCheck.Ordered(e, o, nameof(trackStripHalfWidth), trackStripHalfWidth, nameof(corridorHalfWidth), corridorHalfWidth);
            SettingsCheck.Positive(e, o, nameof(mistMeanFreePathAtFull), mistMeanFreePathAtFull);
            SettingsCheck.Positive(e, o, nameof(mistVolumeHeight), mistVolumeHeight);
            SettingsCheck.Positive(e, o, nameof(mistVolumeDepth), mistVolumeDepth);
            SettingsCheck.Range(e, o, nameof(minRevealTransmittance), minRevealTransmittance, 0f, 1f);
        }
    }
}
