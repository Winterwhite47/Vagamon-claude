using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Vertical slice entry point. Builds the biome slice, then wires rider, camera director, run flow, overlay,
    /// mist low-pass and mix bridge. Player input by default; "-hushAutopilot" (or ForceAutopilot) rides with the bot.
    /// </summary>
    public sealed class SliceBootstrap : MonoBehaviour
    {
        /// <summary>Tests and captures can force the bot.</summary>
        public static bool ForceAutopilot;

        [SerializeField] private BiomeId biome = BiomeId.MeadowSea;
        [SerializeField] private int seed = 2;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private MistLowPass mistLowPass;
        [SerializeField] private AudioMixBridge mixBridge;
        [SerializeField] private bool reducedMotion;

        public SliceRefs Refs { get; private set; }
        public bool Autopilot { get; private set; }
        public RetryOverlay Overlay { get; private set; }
        public PickupPresenter Pickups { get; private set; }
        public ScoreHud Hud { get; private set; }
        public DucklingPresenter Ducklings { get; private set; }
        public HazardPresenter Hazards { get; private set; }

        public void Assign(CameraDirector director, MistLowPass lowPass, AudioMixBridge bridge)
        {
            cameraDirector = director;
            mistLowPass = lowPass;
            mixBridge = bridge;
        }

        private void Start()
        {
            HushConfigSet c = HushConfigSet.Load();
            SliceAssets assets = SliceAssets.Load();
            if (c == null || assets == null)
            {
                Debug.LogError("[Hushlands] Slice needs HushConfigSet and SliceAssets in Resources (run project bootstrap).");
                return;
            }

            SliceRefs r = SliceBuilder.Build(c, assets, biome, seed, transform);
            Refs = r;
            Autopilot = ForceAutopilot || AutopilotInput.Requested();

            IInputSource input;
            if (Autopilot)
            {
                AutopilotInput auto = gameObject.AddComponent<AutopilotInput>();
                auto.Initialize(new BotPilot(r.Stretch, c.fairness.Settings.botTakeoffSeconds), r.Rider);
                input = auto;
            }
            else
            {
                HushInputSource player = gameObject.AddComponent<HushInputSource>();
                player.Configure(c.rider);
                input = player;
            }

            r.Rider.Initialize(c.rider.Settings, c.speed.Settings, c.surfaces.Settings, r.Slope, input, true);
            double start = c.content.Settings.checkpointOffsetMeters;
            r.Rider.ResetTo(new SlopePoint(start, r.Slope.HeightAt(start)), c.speed.Settings.startSpeed);

            r.Signals = new RiderCameraSignals(r.Rider, r.Stretch);
            if (cameraDirector != null)
            {
                cameraDirector.Initialize(c.camera.Settings, r.Slope, r.Rider.transform, r.Signals, r.Mix, biome,
                    reducedMotion, true);
            }

            r.Run = gameObject.AddComponent<RunDirector>();
            r.Run.Initialize(c, r.Rider, input, r.Stretch, r.Slope, cameraDirector, r.Signals, seed);

            var overlayGo = new GameObject("RetryOverlay");
            overlayGo.transform.SetParent(transform, false);
            Overlay = overlayGo.AddComponent<RetryOverlay>();
            Overlay.Initialize(r.Run.Flow, c.uiMotion.Settings, c.failure.Settings, c.look.Settings.Get(biome).fogAlbedo, reducedMotion);

            var pickupsGo = new GameObject("Pickups");
            pickupsGo.transform.SetParent(transform, false);
            Pickups = pickupsGo.AddComponent<PickupPresenter>();
            Pickups.Initialize(r.Run.Session.Pickups, c.pickups.Settings, assets, c.track.Settings.pickupDiameter);
            r.Pickups = Pickups.ViewCount;

            var ducksGo = new GameObject("Ducklings");
            ducksGo.transform.SetParent(transform, false);
            Ducklings = ducksGo.AddComponent<DucklingPresenter>();
            Ducklings.Initialize(r.Run.Ducklings, r.Run.Session.Ducklings, r.Run.Flow, r.Rider, r.Sled, r.Slope,
                c.ducklings.Settings, c.track.Settings, assets);
            r.Nests = Ducklings.NestCount;

            var hudGo = new GameObject("ScoreHud");
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<ScoreHud>();
            Hud.Initialize(r.Run.Session.Score, c.uiMotion.Settings, reducedMotion);

            var hazardsGo = new GameObject("Hazards");
            hazardsGo.transform.SetParent(transform, false);
            Hazards = hazardsGo.AddComponent<HazardPresenter>();
            Hazards.Initialize(r.Run.Session, r, c.hazards.Settings, c.cloth.Settings, c.look.Settings, c.track.Settings, assets);

            if (mistLowPass != null) mistLowPass.Initialize(r.Mix, r.Mist, r.Rider.transform, r.Run.Session);
            if (mixBridge != null) mixBridge.Initialize(r.Mix, null);
        }
    }
}
