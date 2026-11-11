namespace Hushlands.Editor
{
    /// <summary>Canonical asset paths (string config, not tuning numbers).</summary>
    public static class HushPaths
    {
        public const string Root = "Assets/_Hushlands";
        public const string ConfigResources = Root + "/Config/Resources/Hushlands";
        public const string ProjectTargetsAsset = ConfigResources + "/ProjectTargetsConfig.asset";
        public const string BootLookAsset = ConfigResources + "/BootLookConfig.asset";
        public const string ConfigSetAsset = ConfigResources + "/HushConfigSet.asset";
        public const string ConfigRoot = Root + "/Config";

        public static string ConfigAsset(string lane, string name)
        {
            return ConfigRoot + "/" + lane + "/" + name + ".asset";
        }
        public const string RenderingFolder = Root + "/Config/Rendering";
        public const string HdrpAsset = RenderingFolder + "/HushlandsHDRP.asset";
        public const string BootVolumeProfile = RenderingFolder + "/BootVolumeProfile.asset";
        public const string BootScene = Root + "/Scenes/Boot.unity";
        public const string SliceScene = Root + "/Scenes/Slice_MeadowSea.unity";
        /// <summary>The full run: every biome in order on one track.</summary>
        public const string JourneyScene = Root + "/Scenes/Slice_Journey.unity";

        public static string SliceSceneFor(Hushlands.Core.BiomeId biome)
        {
            return Root + "/Scenes/Slice_" + biome + ".unity";
        }
        public const string MaterialsFolder = Root + "/Art/Materials";
        public const string SliceAssetsAsset = ConfigResources + "/SliceAssets.asset";
        public const string WindowsBuild = "Builds/Windows/Hushlands.exe";
        public const string ProductName = "Hushlands";
        public const string CompanyName = "Hushlands Team";
    }
}
