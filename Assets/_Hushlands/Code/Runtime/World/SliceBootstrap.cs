using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Vertical slice entry point. Builds the biome slice, then wires rider, camera director, run flow, overlay,
    /// mist low-pass and mix bridge. Player input by default; "-hushAutopilot" (or ForceAutopilot) rides with the bot.
    /// </summary>
    public sealed class SliceBootstrap : MonoBehaviour, IGameControl
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
        public ISaveService Save { get; private set; }
        public Garage Garage { get; private set; }
        public ProgressionService Progression { get; private set; }
        public AccessibilityOptions Options { get; private set; }
        public UiDirector Ui { get; private set; }
        public bool Riding { get; private set; }

        private HushConfigSet _config;
        private HushInputSource _playerInput;

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

            _config = c;
            Save = SaveProvider.Open(c);
            Options = new AccessibilityOptions(Save, c.ui.Settings, c.uiMotion.Settings);
            reducedMotion = Options.ReducedMotion;
            Progression = new ProgressionService(Save, c.vehicles.Settings, c.goals.Settings);
            Garage = Progression.Garage;
            if (GetComponent<SaveFlusher>() == null) gameObject.AddComponent<SaveFlusher>();
            SliceRefs r = SliceBuilder.Build(c, assets, biome, seed, transform, Garage.Selected);
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
                _playerInput = gameObject.AddComponent<HushInputSource>();
                _playerInput.Configure(c.rider);
                input = _playerInput;
            }

            // Gentle timing lengthens the tap window in both the input sampler and the rider's input rules.
            RiderSettings riderSettings = r.Loadout.Rider.Copy();
            riderSettings.holdThresholdSeconds = Options.HoldThreshold(c.rider.Settings.holdThresholdSeconds);
            r.Rider.Initialize(riderSettings, r.Loadout.Speed, r.Loadout.Surfaces, r.Slope, input, true);
            double start = c.content.Settings.checkpointOffsetMeters;
            r.Rider.ResetTo(new SlopePoint(start, r.Slope.HeightAt(start)), c.speed.Settings.startSpeed);

            r.Signals = new RiderCameraSignals(r.Rider, r.Stretch);
            if (cameraDirector != null)
            {
                cameraDirector.Initialize(c.camera.Settings, r.Slope, r.Rider.transform, r.Signals, r.Mix, biome,
                    reducedMotion, true);
            }

            r.Run = gameObject.AddComponent<RunDirector>();
            RunSettingsBundle bundle = c.RunBundle();
            bundle.Failure = c.failure.Settings.Copy();
            bundle.Failure.autoAcceptAssists = Options.AutoAssists;
            r.Run.Initialize(c, r.Rider, input, r.Stretch, r.Slope, cameraDirector, r.Signals, seed, bundle);
            r.Run.AttachProgression(Progression);

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

            bool autoRide = Autopilot || FrameTimeCaptureRequested() || SceneHandoff.AutoRide;
            bool cover = SceneHandoff.CoverOnStart;
            SceneHandoff.AutoRide = false;
            SceneHandoff.CoverOnStart = false;

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            Ui = uiGo.AddComponent<UiDirector>();
            Ui.Initialize(c.ui.Settings, c.uiMotion.Settings, c.vehicles.Settings, Options, Progression, r.Run, this, Hud,
                autoRide ? UiScreen.Riding : UiScreen.Title, cover);
            ApplyOptions();
            if (autoRide) StartRide();
            else SetPaused(true);
        }

        private static bool FrameTimeCaptureRequested()
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
            {
                if (string.Equals(a, FrameTimeCapture.CommandLineFlag, System.StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public void StartRide()
        {
            if (_playerInput != null) _playerInput.SuppressUntilRelease();
            Riding = true;
            SetPaused(false);
        }

        public void SetPaused(bool paused)
        {
            if (Refs == null) return;
            // Not timeScale: input is processed in FixedUpdate, which timeScale 0 would stop (the pause could never end).
            Physics.simulationMode = paused ? SimulationMode.Script : SimulationMode.FixedUpdate;
            Refs.Rider.Paused = paused;
            Refs.Run.Paused = paused;
        }

        public void Reload(bool autoRide)
        {
            Physics.simulationMode = SimulationMode.FixedUpdate;
            if (Save is FileSaveService file && file.Dirty) file.Flush();
        }

        public void ApplyOptions()
        {
            if (Options == null) return;
            AudioListener.volume = Options.Volume;
            if (cameraDirector != null) cameraDirector.SetReducedMotion(Options.ReducedMotion);
            if (Overlay != null) Overlay.ReducedMotion = Options.ReducedMotion;
            if (Hud != null) Hud.ReducedMotion = Options.ReducedMotion;
            if (_playerInput != null && _config != null)
                _playerInput.HoldThresholdOverride = Options.HoldThreshold(_config.rider.Settings.holdThresholdSeconds);
        }
    }
}
