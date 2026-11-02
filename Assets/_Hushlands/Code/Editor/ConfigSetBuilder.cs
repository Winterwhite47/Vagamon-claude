using UnityEditor;
using UnityEngine;

namespace Hushlands.Editor
{
    /// <summary>Creates every config asset (defaults from settings classes) and links them in HushConfigSet.</summary>
    public static class ConfigSetBuilder
    {
        public static HushConfigSet EnsureConfigSet(ProjectTargetsConfig targets, BootLookConfig look)
        {
            HushConfigSet set = ProjectBootstrap.EnsureAsset<HushConfigSet>(HushPaths.ConfigSetAsset);
            set.targets = targets;
            set.bootLook = look;
            set.look = Make<LookConfig>("Rendering");
            set.speed = Make<SpeedConfig>("Gameplay");
            set.rider = Make<RiderConfig>("Gameplay");
            set.surfaces = Make<SurfaceConfig>("Gameplay");
            set.track = Make<TrackConfig>("Gameplay");
            set.failure = Make<FailureConfig>("Gameplay");
            set.assist = Make<AssistConfig>("Gameplay");
            set.messages = Make<MessageConfig>("Gameplay");
            set.ducklings = Make<DucklingConfig>("Gameplay");
            set.fairness = Make<FairnessConfig>("Content");
            set.hazards = Make<HazardConfig>("Gameplay");
            set.content = Make<ContentConfig>("Content");
            set.biomes = Make<BiomeConfig>("Content");
            set.camera = Make<CameraConfig>("Camera");
            set.audioMix = Make<AudioMixConfig>("Audio");
            set.uiMotion = Make<UiMotionConfig>("UI");
            set.pickups = Make<PickupConfig>("Meta");
            set.score = Make<ScoreConfig>("Meta");
            set.vehicles = Make<VehicleConfig>("Meta");
            set.goals = Make<GoalConfig>("Meta");
            set.cloth = Make<ClothConfig>("Characters");
            EditorUtility.SetDirty(set);

            foreach (string error in set.ValidateAll()) Debug.LogError("[Hushlands] Config: " + error, set);
            return set;
        }

        private static T Make<T>(string lane) where T : ScriptableObject
        {
            ProjectBootstrap.EnsureFolder(HushPaths.ConfigRoot + "/" + lane);
            return ProjectBootstrap.EnsureAsset<T>(HushPaths.ConfigAsset(lane, typeof(T).Name));
        }
    }
}
