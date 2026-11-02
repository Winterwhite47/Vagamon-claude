using UnityEngine;

namespace Hushlands
{
    /// <summary>Look values for the T0 boot scene and HDRP asset defaults. Replaced per biome in T7+.</summary>
    [CreateAssetMenu(menuName = "Hushlands/Config/Boot Look", fileName = "BootLookConfig")]
    public sealed class BootLookConfig : ScriptableObject
    {
        public const string ResourcePath = "Hushlands/BootLookConfig";

        [Header("Sun")]
        [Min(0f)] public float sunIlluminanceLux = 60000f;
        [Range(-90f, 90f)] public float sunPitch = 12f;
        [Range(-180f, 180f)] public float sunYaw = -35f;
        public Color sunTint = new Color(1f, 0.86f, 0.68f, 1f);

        [Header("Mist (HDRP fog)")]
        [Min(0.1f)] public float fogMeanFreePath = 180f;
        public float fogBaseHeight = 0f;
        public float fogMaximumHeight = 120f;
        [Range(0f, 1f)] public float fogAnisotropy = 0.6f;

        [Header("Exposure")]
        public float fixedExposureEV = 13.5f;

        [Header("Camera")]
        [Range(10f, 90f)] public float fieldOfView = 42f;
        public Vector3 cameraPosition = new Vector3(0f, 6f, -18f);
        public Vector3 cameraEuler = new Vector3(10f, 0f, 0f);

        [Header("Upscaling")]
        [Range(25f, 100f)] public float minScreenPercent = 50f;
        [Range(25f, 100f)] public float maxScreenPercent = 100f;

        public static BootLookConfig Load()
        {
            return Resources.Load<BootLookConfig>(ResourcePath);
        }
    }
}
