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
        public SledFlexDriver Flex;
        public Fog Fog;
        public float FogBaseMeanFreePath;
        public RiderCameraSignals Signals;
        public MixDirector Mix;
        public RunDirector Run;
        public Light Sun;
        public Volume Volume;
        public int TerrainTiles;
        public int Props;
        public int WaterPlanes;
        public BiomeLook Look;
        /// <summary>Set for the full journey (several biomes in one run); null for a single-biome slice.</summary>
        public JourneyPlan Plan;
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
            return Build(c, assets, biome, false, seed, parent, vehicle);
        }

        /// <summary>The full run: every biome in journey order on one continuous track, blended at the borders.</summary>
        public static SliceRefs BuildJourney(HushConfigSet c, SliceAssets assets, int seed, Transform parent, VehicleId vehicle)
        {
            return Build(c, assets, default, true, seed, parent, vehicle);
        }

        private static SliceRefs Build(HushConfigSet c, SliceAssets assets, BiomeId biome, bool journey, int seed,
            Transform parent, VehicleId vehicle)
        {
            LookSettings ls = c.look.Settings;
            JumpModel jump = JumpModel.ForAllVehicles(c.rider.Settings, c.speed.Settings, c.vehicles.Settings);
            var gen = new ChunkGenerator(c.content.Settings, c.fairness.Settings, c.biomes.Settings, jump);
            var r = new SliceRefs();
            if (journey)
            {
                r.Stretch = gen.GenerateJourney(seed);
                r.Plan = new JourneyPlan(r.Stretch, c.biomes.Settings.transitionMeters);
                biome = r.Plan.Segments[0].Biome;
            }
            else
            {
                r.Stretch = gen.Generate(seed, biome, c.content.Settings.chunksPerStretch);
            }
            BiomeLook look = ls.Get(biome);
            r.Root = new GameObject(journey ? "Slice_Journey" : "Slice_" + biome);
            if (parent != null) r.Root.transform.SetParent(parent, false);
            r.Slope = new StretchSlope(r.Stretch);
            double length = r.Slope.Length + c.track.Settings.runOutMeters;

            TrackBuilder.Build(r.Stretch, r.Slope, c.track.Settings, r.Root.transform);
            r.Terrain = journey
                ? new TerrainSampler(r.Slope, r.Stretch, ls, r.Plan, seed)
                : new TerrainSampler(r.Slope, r.Stretch, ls, look, seed);
            var world = new GameObject("World").transform;
            world.SetParent(r.Root.transform, false);
            r.TerrainTiles = TerrainMeshBuilder.BuildTerrain(r.Terrain, ls, length, assets.Get("Grass"), world).Count;
            TerrainMeshBuilder.BuildTrackStrip(r.Terrain, ls, length, assets.Get("Track"), world, c.track.Settings.stripLift);

            AddObstacleVisuals(r, assets);
            r.Look = look;
            r.Props = AddScatter(r, ls, biome, length, seed, assets, world);
            r.WaterPlanes = AddWater(r, ls, assets, world);
            r.Mist = journey
                ? new MistField(r.Stretch, c.biomes.Settings, r.Plan)
                : new MistField(r.Stretch, c.biomes.Settings.Get(biome).baseMistDensity);
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

        /// <summary>One still water plane per lake chunk in the backdrop lake band, below that chunk's lowest track point.</summary>
        private static int AddWater(SliceRefs r, LookSettings ls, SliceAssets assets, Transform world)
        {
            if (!r.Terrain.HasWater) return 0;
            var root = new GameObject("Lake").transform;
            root.SetParent(world, false);
            int n = 0;
            foreach (IChunk c in r.Stretch)
            {
                BiomeLook look = ls.Get(c.Biome);
                if (look.waterMaxDepth <= 0f) continue;
                float level = (float)r.Terrain.WaterLevel(c.StartDistance);
                // Plain mesh, not CreatePrimitive(Quad): that drags in MeshCollider, which IL2CPP engine stripping may remove.
                var water = new GameObject("Water_" + c.Id);
                water.transform.SetParent(root, false);
                water.AddComponent<MeshFilter>().sharedMesh = UnitQuad();
                water.AddComponent<MeshRenderer>().sharedMaterial = assets.Get("Water");
                float depth = look.waterMaxDepth - look.waterMinDepth;
                water.transform.SetPositionAndRotation(
                    new Vector3((float)(c.StartDistance + c.Length * 0.5), level, look.waterMinDepth + depth * 0.5f),
                    Quaternion.Euler(90f, 0f, 0f));
                water.transform.localScale = new Vector3((float)c.Length, depth, 1f);
                n++;
            }
            return n;
        }

        private static Mesh _unitQuad;

        /// <summary>1 x 1 quad in its XY plane facing -Z (same layout as Unity's Quad primitive).</summary>
        private static Mesh UnitQuad()
        {
            if (_unitQuad != null) return _unitQuad;
            _unitQuad = new Mesh
            {
                name = "UnitQuad",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) },
                uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                triangles = new[] { 0, 3, 1, 3, 0, 2 },
            };
            _unitQuad.RecalculateNormals();
            _unitQuad.RecalculateBounds();
            return _unitQuad;
        }

        private static int AddScatter(SliceRefs r, LookSettings ls, BiomeId biome, double length, int seed, SliceAssets assets,
            Transform world)
        {
            var root = new GameObject("Scatter").transform;
            root.SetParent(world, false);
            int n = 0;
            List<PropPlacement> placements = r.Plan != null
                ? PropScatterer.Place(ls, r.Plan, r.Terrain, length, seed)
                : PropScatterer.Place(ls, biome, r.Terrain, length, seed);
            foreach (PropPlacement p in placements)
            {
                Material mat = assets.Get(p.Material);
                float y = (float)p.Y;
                if (p.Shape == "Post")
                {
                    GameObject post = Primitive(PrimitiveType.Cylinder, p.Name, root, mat);
                    post.transform.localScale = new Vector3(p.Scale * 0.08f, p.Scale * 0.5f, p.Scale * 0.08f);
                    post.transform.position = new Vector3((float)p.X, y + p.Scale * 0.5f, (float)p.Z);
                    n++;
                    continue;
                }
                if (p.Shape == "Pine")
                {
                    // Placeholder pine: a wood trunk and three narrowing tiers of needles.
                    GameObject trunk = Primitive(PrimitiveType.Cylinder, "Trunk", root, assets.Get("Wood"));
                    trunk.transform.localScale = new Vector3(p.Scale * 0.06f, p.Scale * 0.25f, p.Scale * 0.06f);
                    trunk.transform.position = new Vector3((float)p.X, y + p.Scale * 0.25f, (float)p.Z);
                    for (int tier = 0; tier < 3; tier++)
                    {
                        float w = p.Scale * (0.42f - 0.11f * tier);
                        GameObject needles = Primitive(PrimitiveType.Sphere, p.Name, root, mat);
                        needles.transform.localScale = new Vector3(w, p.Scale * 0.28f, w);
                        needles.transform.position = new Vector3((float)p.X, y + p.Scale * (0.4f + 0.22f * tier), (float)p.Z);
                    }
                    n++;
                    continue;
                }
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

            r.Flex = go.AddComponent<SledFlexDriver>();
            r.Flex.Initialize(cloth, r.Rider, sled);
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
