using UnityEngine;

namespace Hushlands
{
    /// <summary>Project-wide technical targets. Single source for T0 numbers (bible: Look, Physics).</summary>
    [CreateAssetMenu(menuName = "Hushlands/Config/Project Targets", fileName = "ProjectTargetsConfig")]
    public sealed class ProjectTargetsConfig : ScriptableObject
    {
        public const string ResourcePath = "Hushlands/ProjectTargetsConfig";

        [Header("Physics")]
        [Min(0.001f)] public float fixedTimestep = 0.01f;
        [Min(0.01f)] public float maximumAllowedTimestep = 0.1f;

        [Header("Display")]
        [Min(1)] public int targetWidth = 3840;
        [Min(1)] public int targetHeight = 2160;
        [Min(1)] public int targetFrameRate = 60;
        [Min(1f)] public float frameBudgetMs = 16.6f;
        [Range(50f, 100f)] public float budgetPercentile = 95f;

        [Header("Capture")]
        [Min(1f)] public float captureWarmupSeconds = 3f;
        [Min(1f)] public float captureSeconds = 20f;
        public string captureFolder = "Captures";

        [Header("Palette (canon)")]
        public Color emerald = new Color32(0x0B, 0x3B, 0x17, 0xFF);
        public Color terracotta = new Color32(0x8D, 0x3F, 0x2D, 0xFF);
        public Color mistBlue = new Color32(0x4A, 0x6B, 0x82, 0xFF);
        public Color gold = new Color32(0xD4, 0xAF, 0x37, 0xFF);

        public static ProjectTargetsConfig Load()
        {
            return Resources.Load<ProjectTargetsConfig>(ResourcePath);
        }
    }
}
