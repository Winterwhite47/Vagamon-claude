using UnityEngine;

namespace Hushlands
{
    /// <summary>Applies runtime targets from config before the first scene loads.</summary>
    public static class BootApplicator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            ProjectTargetsConfig config = ProjectTargetsConfig.Load();
            if (config == null)
            {
                Debug.LogError("[Hushlands] ProjectTargetsConfig missing from Resources.");
                return;
            }
            Time.fixedDeltaTime = config.fixedTimestep;
            Time.maximumDeltaTime = config.maximumAllowedTimestep;
            Application.targetFrameRate = config.targetFrameRate;
            QualitySettings.vSyncCount = 0;

            HushConfigSet set = HushConfigSet.Load();
            if (set != null && set.speed != null) Physics.gravity = new Vector3(0f, -set.speed.Settings.gravity, 0f);
        }
    }
}
