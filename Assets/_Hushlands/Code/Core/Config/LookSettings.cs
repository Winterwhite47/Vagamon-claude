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
        /// <summary>Added to the ClothSettings wind (exposed ridges blow harder).</summary>
        public float windExtraX;
        public float windExtraZ;
        /// <summary>Backdrop terraces: tread height step in metres (0 = smooth hills).</summary>
        public float terraceStep;
        /// <summary>Fraction of each step that is riser (the rest is flat tread).</summary>
        public float terraceRiser;
        /// <summary>Backdrop lake band (depth from the track, z). waterMaxDepth 0 = no water.</summary>
        public float waterMinDepth;
        public float waterMaxDepth;
        /// <summary>Water sits this far below the lowest track point of its chunk.</summary>
        public float waterDrop;
        /// <summary>Lake bed below the water surface.</summary>
        public float waterBed;
    }

    /// <summary>Backdrop props scattered behind the track (never in the corridor, never in front of the rider).</summary>
    [Serializable]
    public struct PropScatter
    {
        public BiomeId biome;
        public string name;
        public string material;
        /// <summary>Props per metre of run.</summary>
        public float density;
        public float minDepth;
        public float maxDepth;
        public float minScale;
        public float maxScale;
        /// <summary>Stacked stones per prop (1 = single rock, more = cairn).</summary>
        public int minStack;
        public int maxStack;
        /// <summary>Placeholder shape: "Rock" (boulder / cairn stack) or "Pine" (trunk and three tiers).</summary>
        public string shape;
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
            new BiomeLook
            {
                // Windmouth Ridge (T14): high, quiet dawn on a frosted ridge; cool low light, long mist, ridge wind.
                biome = BiomeId.WindmouthRidge, tuned = true,
                sunElevationDegrees = 14f, sunAzimuthDegrees = 28f, sunLux = 45000f, sunKelvin = 5200f,
                exposureEV = 13.2f,
                fogMeanFreePath = 240f, fogBaseHeight = -40f, fogMaximumHeight = 80f, fogAnisotropy = 0.65f,
                fogAlbedo = MistBlue, grass = "#DDE6EA", soil = "#6E5A4E", accent = Gold,
                hillAmplitude = 30f, hillWavelength = 180f, detailAmplitude = 2.2f, detailWavelength = 14f,
                windExtraX = -1.5f, windExtraZ = 0.6f,
            },
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
            new BiomeLook
            {
                // Pine Hollow (T14): late-afternoon light falling through a pine valley; cool green shade, soft mist.
                biome = BiomeId.PineHollow, tuned = true,
                sunElevationDegrees = 22f, sunAzimuthDegrees = -15f, sunLux = 28000f, sunKelvin = 4300f,
                exposureEV = 12.4f,
                fogMeanFreePath = 200f, fogBaseHeight = -40f, fogMaximumHeight = 50f, fogAnisotropy = 0.8f,
                fogAlbedo = MistBlue, grass = Emerald, soil = "#5B4636", accent = Gold,
                hillAmplitude = 22f, hillWavelength = 110f, detailAmplitude = 1.4f, detailWavelength = 12f,
                windExtraX = 0.4f, windExtraZ = -0.2f,
            },
            new BiomeLook
            {
                // Terrace Country (T14): soft morning over stepped tea slopes; low mist lifting off the terraces.
                biome = BiomeId.TerraceCountry, tuned = true,
                sunElevationDegrees = 18f, sunAzimuthDegrees = -50f, sunLux = 40000f, sunKelvin = 4800f,
                exposureEV = 13.0f,
                fogMeanFreePath = 230f, fogBaseHeight = -40f, fogMaximumHeight = 70f, fogAnisotropy = 0.7f,
                fogAlbedo = MistBlue, grass = "#2F5A22", soil = Terracotta, accent = Gold,
                hillAmplitude = 26f, hillWavelength = 150f, detailAmplitude = 1.0f, detailWavelength = 16f,
                terraceStep = 3f, terraceRiser = 0.25f,
            },
            new BiomeLook
            {
                // Mirror Lake (T14): still evening by a lake that mirrors the last warm light; lake mist, low sun.
                biome = BiomeId.MirrorLake, tuned = true,
                sunElevationDegrees = 6f, sunAzimuthDegrees = 40f, sunLux = 18000f, sunKelvin = 3300f,
                exposureEV = 11.8f,
                fogMeanFreePath = 260f, fogBaseHeight = -40f, fogMaximumHeight = 40f, fogAnisotropy = 0.75f,
                fogAlbedo = MistBlue, grass = "#2F5A3A", soil = "#6B5B4B", accent = Gold,
                hillAmplitude = 18f, hillWavelength = 200f, detailAmplitude = 0.8f, detailWavelength = 20f,
                waterMinDepth = 20f, waterMaxDepth = 140f, waterDrop = 2.5f, waterBed = 1.5f,
            },
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
            new MaterialSpec("Pine", "#14331C", 0.2f, 0f),
            new MaterialSpec("TeaLeaf", "#2F5A22", 0.3f, 0f),
            new MaterialSpec("Water", MistBlue, 0.97f, 0f),
            new MaterialSpec("Reed", "#7C8A4A", 0.25f, 0f),
        };

        public PropScatter[] scatters =
        {
            new PropScatter
            {
                biome = BiomeId.WindmouthRidge, name = "Rock", material = "Stone", density = 0.06f,
                minDepth = 14f, maxDepth = 200f, minScale = 0.8f, maxScale = 3.2f, minStack = 1, maxStack = 1, shape = "Rock",
            },
            new PropScatter
            {
                biome = BiomeId.WindmouthRidge, name = "Cairn", material = "Stone", density = 0.008f,
                minDepth = 12f, maxDepth = 30f, minScale = 0.35f, maxScale = 0.6f, minStack = 3, maxStack = 5, shape = "Rock",
            },
            new PropScatter
            {
                biome = BiomeId.PineHollow, name = "Pine", material = "Pine", density = 0.25f,
                minDepth = 12f, maxDepth = 200f, minScale = 6f, maxScale = 14f, minStack = 1, maxStack = 1, shape = "Pine",
            },
            new PropScatter
            {
                biome = BiomeId.PineHollow, name = "Log", material = "Wood", density = 0.02f,
                minDepth = 12f, maxDepth = 40f, minScale = 0.4f, maxScale = 0.8f, minStack = 1, maxStack = 1, shape = "Rock",
            },
            new PropScatter
            {
                biome = BiomeId.TerraceCountry, name = "TeaBush", material = "TeaLeaf", density = 0.5f,
                minDepth = 12f, maxDepth = 150f, minScale = 0.7f, maxScale = 1.2f, minStack = 1, maxStack = 1, shape = "Rock",
            },
            new PropScatter
            {
                biome = BiomeId.TerraceCountry, name = "Post", material = "Wood", density = 0.02f,
                minDepth = 12f, maxDepth = 60f, minScale = 1.4f, maxScale = 2.2f, minStack = 1, maxStack = 1, shape = "Post",
            },
            new PropScatter
            {
                biome = BiomeId.MirrorLake, name = "Reed", material = "Reed", density = 0.15f,
                minDepth = 12f, maxDepth = 19f, minScale = 0.8f, maxScale = 1.6f, minStack = 1, maxStack = 1, shape = "Post",
            },
            new PropScatter
            {
                biome = BiomeId.MirrorLake, name = "ShoreStone", material = "Stone", density = 0.03f,
                minDepth = 12f, maxDepth = 19f, minScale = 0.5f, maxScale = 1.4f, minStack = 1, maxStack = 1, shape = "Rock",
            },
        };

        // --- Duckling safety near water (T14) ---
        /// <summary>Nests must sit at least this far (in depth) from any water band.</summary>
        public float nestWaterSpacing = 10f;

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

        // --- World window (T17): only the backdrop near the rider is switched on ---
        public float windowAheadMeters = 700f;
        public float windowBehindMeters = 350f;
        /// <summary>Extra distance before something switches off again (no flicker at the edge).</summary>
        public float windowHysteresisMeters = 30f;
        /// <summary>Scatter props are grouped in buckets this long so they switch together.</summary>
        public float windowBucketMeters = 64f;

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
                SettingsCheck.NonNegative(e, t, nameof(b.terraceStep), b.terraceStep);
                if (b.waterMaxDepth > 0f)
                {
                    if (b.waterMinDepth < corridorHalfWidth + corridorBlend) e.Add(t + ": water must stay out of the track corridor");
                    SettingsCheck.Ordered(e, t, nameof(b.waterMinDepth), b.waterMinDepth, nameof(b.waterMaxDepth), b.waterMaxDepth);
                    if (b.waterMaxDepth > terrainBackDepth) e.Add(t + ": water beyond the terrain");
                    SettingsCheck.Positive(e, t, nameof(b.waterDrop), b.waterDrop);
                    SettingsCheck.Positive(e, t, nameof(b.waterBed), b.waterBed);
                }
                if (b.terraceStep > 0f) SettingsCheck.Range(e, t, nameof(b.terraceRiser), b.terraceRiser, 0.05f, 1f);
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
            foreach (PropScatter p in scatters ?? new PropScatter[0])
            {
                string t = o + ".scatter." + p.biome + "." + p.name;
                if (Array.FindIndex(materials, m => m.name == p.material) < 0) e.Add(t + ": unknown material " + p.material);
                SettingsCheck.NonNegative(e, t, nameof(p.density), p.density);
                if (p.minDepth < corridorHalfWidth + corridorBlend) e.Add(t + ": props must stay outside the track corridor");
                SettingsCheck.Ordered(e, t, nameof(p.minDepth), p.minDepth, nameof(p.maxDepth), p.maxDepth);
                if (p.maxDepth > terrainBackDepth) e.Add(t + ": props beyond the terrain");
                SettingsCheck.Positive(e, t, nameof(p.minScale), p.minScale);
                SettingsCheck.Ordered(e, t, nameof(p.minScale), p.minScale, nameof(p.maxScale), p.maxScale);
                if (p.minStack < 1 || p.maxStack < p.minStack) e.Add(t + ": bad stack range");
                if (p.shape != "Rock" && p.shape != "Pine" && p.shape != "Post") e.Add(t + ": unknown shape '" + p.shape + "'");
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
            SettingsCheck.Positive(e, o, nameof(windowAheadMeters), windowAheadMeters);
            SettingsCheck.Positive(e, o, nameof(windowBehindMeters), windowBehindMeters);
            SettingsCheck.NonNegative(e, o, nameof(windowHysteresisMeters), windowHysteresisMeters);
            SettingsCheck.Positive(e, o, nameof(windowBucketMeters), windowBucketMeters);
            foreach (BiomeLook b in biomes)
            {
                // Whatever switches on at the window edge must already be lost in the fog (under 10% of its light left).
                if (windowAheadMeters < b.fogMeanFreePath * 2.3f) e.Add(o + "." + b.biome + ": world window shorter than the fog hides");
            }
            SettingsCheck.Positive(e, o, nameof(mistMeanFreePathAtFull), mistMeanFreePathAtFull);
            SettingsCheck.Positive(e, o, nameof(mistVolumeHeight), mistVolumeHeight);
            SettingsCheck.Positive(e, o, nameof(mistVolumeDepth), mistVolumeDepth);
            SettingsCheck.Range(e, o, nameof(minRevealTransmittance), minRevealTransmittance, 0f, 1f);
        }
    }
}
