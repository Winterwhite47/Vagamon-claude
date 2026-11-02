using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Hushlands
{
    /// <summary>Everything a built slice exposes to its bootstrap and tests.</summary>
    public sealed class SliceRefs
    {
        public GameObject Root;
        public List<Chunk> Stretch;
        public StretchSlope Slope;
        public TerrainSampler Terrain;
        public MistField Mist;
        public PhysxRider Rider;
        public Transform Sled;
        public VehicleLoadout Loadout;
        public GameObject Deck;
        public VerletScarf Scarf;
        public Fog Fog;
        public float FogBaseMeanFreePath;
        public RiderCameraSignals Signals;
        public MixDirector Mix;
        public RunDirector Run;
        public Light Sun;
        public Volume Volume;
        public int TerrainTiles;
        public int Props;
        public BiomeLook Look;
        public int MistVolumes;
        public int Obstacles;
        public int Nests;
        public int Pickups;
    }

    /// <summary>
    /// Builds a playable biome slice procedurally from config: generated stretch, PhysX track, terrain, track strip,
    /// obstacle/nest/pickup props, mist volumes, sun and HDRP volume, rider with sled flex and scarf.
    /// Camera, overlay and input are wired by SliceBootstrap. All geometry is original procedural placeholder art.
    /// </summary>
    public static class SliceBuilder
    {
        public static SliceRefs Build(HushConfigSet c, SliceAssets assets, BiomeId biome, int seed, Transform parent,
            VehicleId vehicle)
        {
            LookSettings ls = c.look.Settings;
            BiomeLook look = ls.Get(biome);
            var r = new SliceRefs { Root = new GameObject("Slice_" + biome) };
            if (parent != null) r.Root.transform.SetParent(parent, false);

            JumpModel jump = JumpModel.ForAllVehicles(c.rider.Settings, c.speed.Settings, c.vehicles.Settings);
            var gen = new ChunkGenerator(c.content.Settings, c.fairness.Settings, c.biomes.Settings, jump);
            r.Stretch = gen.Generate(seed, biome, c.content.Settings.chunksPerStretch);
            r.Slope = new StretchSlope(r.Stretch);
            double length = r.Slope.Length + c.track.Settings.runOutMeters;

            TrackBuilder.Build(r.Stretch, r.Slope, c.track.Settings, r.Root.transform);
            r.Terrain = new TerrainSampler(r.Slope, r.Stretch, ls, look, seed);
            var world = new GameObject("World").transform;
            world.SetParent(r.Root.transform, false);
            r.TerrainTiles = TerrainMeshBuilder.BuildTerrain(r.Terrain, ls, length, assets.Get("Grass"), world).Count;
            TerrainMeshBuilder.BuildTrackStrip(r.Terrain, ls, length, assets.Get("Track"), world, c.track.Settings.stripLift);

            AddObstacleVisuals(r, assets);
            r.Look = look;
            r.Props = AddScatter(r, ls, biome, length, seed, assets, world);
            r.Mist = new MistField(r.Stretch, c.biomes.Settings.Get(biome).baseMistDensity);
            r.MistVolumes = AddMistVolumes(r, ls, look, world);
            AddLighting(r, look, world);
            r.Loadout = VehicleLoadout.Build(c.vehicles.Settings.Get(vehicle), c.rider.Settings, c.speed.Settings, c.surfaces.Settings);
            BuildRider(r, c, assets, c.vehicles.Settings.Get(vehicle));
            r.Mix = new MixDirector(c.audioMix.Settings);
            return r;
        }

        private static void AddObstacleVisuals(SliceRefs r, SliceAssets assets)
        {
            foreach (HazardMarker h in r.Root.GetComponentsInChildren<HazardMarker>())
            {
                if (h.cause != CrashCause.Obstacle) continue;
                var box = h.GetComponent<BoxCollider>();
                GameObject v = Primitive(PrimitiveType.Cube, "Visual", h.transform, assets.Get("Stone"));
                v.transform.localScale = box.size;
                r.Obstacles++;
            }
        }

        private static int AddScatter(SliceRefs r, LookSettings ls, BiomeId biome, double length, int seed, SliceAssets assets,
            Transform world)
        {
            var root = new GameObject("Scatter").transform;
            root.SetParent(world, false);
            int n = 0;
            foreach (PropPlacement p in PropScatterer.Place(ls, biome, r.Terrain, length, seed))
            {
                Material mat = assets.Get(p.Material);
                float y = (float)p.Y;
                for (int k = 0; k < p.Stack; k++)
                {
                    // Single rocks are flattened boulders; stacks shrink upward into a cairn.
                    float size = p.Stack > 1 ? p.Scale * (1f - 0.18f * k) : p.Scale;
                    GameObject g = Primitive(PrimitiveType.Sphere, p.Name, root, mat);
                    g.transform.localScale = new Vector3(size, size * 0.6f, size * 0.8f);
                    g.transform.position = new Vector3((float)p.X, y + size * 0.25f, (float)p.Z);
                    y += size * 0.5f;
                }
                n++;
            }
            return n;
        }

        private static int AddMistVolumes(SliceRefs r, LookSettings ls, BiomeLook look, Transform world)
        {
            Palette.TryParse(look.fogAlbedo, out float cr, out float cg, out float cb);
            int n = 0;
            foreach (KeyValuePair<Span, float> bank in r.Mist.Banks)
            {
                double mid = (bank.Key.Start + bank.Key.End) * 0.5;
                var go = new GameObject("Mist_" + n);
                go.transform.SetParent(world, false);
                go.transform.position = new Vector3((float)mid, r.Slope.HeightAt(mid) + ls.mistVolumeHeight * 0.5f, 0f);
                var fog = go.AddComponent<LocalVolumetricFog>();
                fog.parameters.albedo = new Color(cr, cg, cb, 1f);
                fog.parameters.meanFreePath = (float)MistVisibility.MeanFreePath(ls, bank.Value);
                fog.parameters.size = new Vector3((float)(bank.Key.End - bank.Key.Start), ls.mistVolumeHeight, ls.mistVolumeDepth);
                n++;
            }
            return n;
        }

        private static void AddLighting(SliceRefs r, BiomeLook look, Transform world)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(world, false);
            sunGo.transform.rotation = Quaternion.Euler(look.sunElevationDegrees, look.sunAzimuthDegrees, 0f);
            r.Sun = sunGo.AddComponent<Light>();
            r.Sun.type = LightType.Directional;
            r.Sun.lightUnit = LightUnit.Lux;
            r.Sun.intensity = look.sunLux;
            r.Sun.useColorTemperature = true;
            r.Sun.colorTemperature = look.sunKelvin;
            r.Sun.color = Color.white;
            r.Sun.shadows = LightShadows.Soft;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "SliceProfile_" + look.biome;
            VisualEnvironment env = profile.Add<VisualEnvironment>(true);
            env.skyType.Override((int)SkyType.PhysicallyBased);
            profile.Add<PhysicallyBasedSky>(true);
            Fog fog = profile.Add<Fog>(true);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(look.fogMeanFreePath);
            fog.baseHeight.Override(look.fogBaseHeight);
            fog.maximumHeight.Override(look.fogMaximumHeight);
            fog.anisotropy.Override(look.fogAnisotropy);
            Palette.TryParse(look.fogAlbedo, out float fr, out float fg, out float fb);
            fog.albedo.Override(new Color(fr, fg, fb, 1f));
            r.Fog = fog;
            r.FogBaseMeanFreePath = look.fogMeanFreePath;
            Exposure exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(look.exposureEV);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            var volGo = new GameObject("Volume");
            volGo.transform.SetParent(world, false);
            r.Volume = volGo.AddComponent<Volume>();
            r.Volume.isGlobal = true;
            r.Volume.sharedProfile = profile;
        }

        private static void BuildRider(SliceRefs r, HushConfigSet c, SliceAssets assets, VehicleEntry vehicle)
        {
            TrackSettings t = c.track.Settings;
            var go = new GameObject("Rider");
            go.transform.SetParent(r.Root.transform, false);
            go.AddComponent<Rigidbody>();
            r.Rider = go.AddComponent<PhysxRider>();
            RiderSettings rs = c.rider.Settings;

            // Hero placeholder: wooden hill sled, rider in a mist-blue jacket, gold scarf.
            var sled = new GameObject("Sled").transform;
            sled.SetParent(go.transform, false);
            r.Sled = sled;
            GameObject deck = Primitive(PrimitiveType.Cube, "Deck_" + vehicle.vehicle, sled, assets.Get("Wood"));
            deck.transform.localPosition = new Vector3(0f, rs.colliderRadius, 0f);
            deck.transform.localScale = new Vector3(rs.colliderLength * vehicle.deckLengthScale, t.sledDeckThickness,
                t.sledWidth * vehicle.deckWidthScale);
            // Per-vehicle tint on an instance of the wood material (placeholder until authored vehicle models).
            Palette.TryParse(vehicle.color, out float vr, out float vg, out float vb);
            MeshRenderer deckRenderer = deck.GetComponent<MeshRenderer>();
            deckRenderer.material.SetColor("_BaseColor", new Color(vr, vg, vb, 1f));
            r.Deck = deck;
            GameObject body = Primitive(PrimitiveType.Capsule, "Body", go.transform, assets.Get("Jacket"));
            body.transform.localPosition = new Vector3(0f, rs.colliderRadius + t.riderBodyHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(t.riderBodyWidth, t.riderBodyHeight * 0.5f, t.riderBodyWidth);
            GameObject head = Primitive(PrimitiveType.Sphere, "Head", go.transform, assets.Get("Skin"));
            head.transform.localPosition = new Vector3(0f, rs.colliderRadius + t.riderBodyHeight + t.riderHeadDiameter * 0.5f, 0f);
            head.transform.localScale = Vector3.one * t.riderHeadDiameter;

            var neck = new GameObject("ScarfAnchor").transform;
            neck.SetParent(go.transform, false);
            neck.localPosition = new Vector3(0f, rs.colliderRadius + t.riderBodyHeight, 0f);
            ClothSettings cloth = c.cloth.Settings;
            var bones = new Transform[cloth.scarfSegments + 1];
            var scarfRoot = new GameObject("Scarf").transform;
            scarfRoot.SetParent(r.Root.transform, false);
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject("ScarfBone_" + i).transform;
                bones[i].SetParent(scarfRoot, false);
            }
            r.Scarf = scarfRoot.gameObject.AddComponent<VerletScarf>();
            r.Scarf.Initialize(cloth, c.speed.Settings.gravity, neck, body.transform, bones, true);
            scarfRoot.gameObject.AddComponent<ScarfLine>().Initialize(bones, assets.Get("Scarf"), cloth.scarfWidth);

            go.AddComponent<SledFlexDriver>().Initialize(cloth, r.Rider, sled);
        }

        internal static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material mat)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            // Immediate: a deferred destroy would leave the collider in the rider's compound body for a frame.
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }
    }
}
