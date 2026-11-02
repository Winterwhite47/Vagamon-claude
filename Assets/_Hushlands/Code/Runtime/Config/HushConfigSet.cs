using System.Collections.Generic;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Single entry point to every config asset. Lives in Resources.</summary>
    [CreateAssetMenu(menuName = "Hushlands/Config/Config Set", fileName = "HushConfigSet")]
    public sealed class HushConfigSet : ScriptableObject
    {
        public const string ResourcePath = "Hushlands/HushConfigSet";

        public ProjectTargetsConfig targets;
        public BootLookConfig bootLook;
        public SpeedConfig speed;
        public RiderConfig rider;
        public SurfaceConfig surfaces;
        public TrackConfig track;
        public FailureConfig failure;
        public AssistConfig assist;
        public DucklingConfig ducklings;
        public FairnessConfig fairness;
        public ContentConfig content;
        public BiomeConfig biomes;
        public CameraConfig camera;
        public AudioMixConfig audioMix;
        public UiMotionConfig uiMotion;
        public PickupConfig pickups;
        public ScoreConfig score;
        public VehicleConfig vehicles;

        public static HushConfigSet Load()
        {
            return Resources.Load<HushConfigSet>(ResourcePath);
        }

        /// <summary>Missing references and per-block validation errors.</summary>
        public List<string> ValidateAll()
        {
            var errors = new List<string>();
            if (targets == null) errors.Add("targets missing");
            if (bootLook == null) errors.Add("bootLook missing");
            Collect(errors, nameof(speed), speed == null ? null : speed.Validate());
            Collect(errors, nameof(rider), rider == null ? null : rider.Validate());
            Collect(errors, nameof(surfaces), surfaces == null ? null : surfaces.Validate());
            Collect(errors, nameof(track), track == null ? null : track.Validate());
            Collect(errors, nameof(failure), failure == null ? null : failure.Validate());
            Collect(errors, nameof(assist), assist == null ? null : assist.Validate());
            Collect(errors, nameof(ducklings), ducklings == null ? null : ducklings.Validate());
            Collect(errors, nameof(fairness), fairness == null ? null : fairness.Validate());
            Collect(errors, nameof(content), content == null ? null : content.Validate());
            Collect(errors, nameof(biomes), biomes == null ? null : biomes.Validate());
            Collect(errors, nameof(camera), camera == null ? null : camera.Validate());
            Collect(errors, nameof(audioMix), audioMix == null ? null : audioMix.Validate());
            Collect(errors, nameof(uiMotion), uiMotion == null ? null : uiMotion.Validate());
            Collect(errors, nameof(pickups), pickups == null ? null : pickups.Validate());
            Collect(errors, nameof(score), score == null ? null : score.Validate());
            Collect(errors, nameof(vehicles), vehicles == null ? null : vehicles.Validate());
            if (targets != null && rider != null && !Mathf.Approximately(targets.fixedTimestep, rider.Settings.physicsStep))
                errors.Add("rider.physicsStep must equal targets.fixedTimestep");
            if (rider != null && fairness != null &&
                rider.Settings.colliderLength * 0.5f > fairness.Settings.riderFootprintMeters + 1e-4f)
                errors.Add("fairness.riderFootprintMeters must cover half the rider collider length");
            return errors;
        }

        private static void Collect(List<string> errors, string field, List<string> result)
        {
            if (result == null) errors.Add(field + " missing");
            else errors.AddRange(result);
        }
    }
}
