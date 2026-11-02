namespace Hushlands.Editor
{
    /// <summary>Canonical asset paths (string config, not tuning numbers).</summary>
    public static class HushPaths
    {
        public const string Root = "Assets/_Hushlands";
        public const string ConfigResources = Root + "/Config/Resources/Hushlands";
        public const string ProjectTargetsAsset = ConfigResources + "/ProjectTargetsConfig.asset";
        public const string BootLookAsset = ConfigResources + "/BootLookConfig.asset";
        public const string RenderingFolder = Root + "/Config/Rendering";
        public const string HdrpAsset = RenderingFolder + "/HushlandsHDRP.asset";
        public const string BootVolumeProfile = RenderingFolder + "/BootVolumeProfile.asset";
        public const string BootScene = Root + "/Scenes/Boot.unity";
        public const string WindowsBuild = "Builds/Windows/Hushlands.exe";
        public const string ProductName = "Hushlands";
        public const string CompanyName = "Hushlands Team";
    }
}
