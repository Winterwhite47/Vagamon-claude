using UnityEditor;

namespace Hushlands.Editor
{
    /// <summary>
    /// First open (fresh clone, CI): if the generated config set is missing, run the idempotent ProjectBootstrap once,
    /// on the first editor tick (asset creation is not allowed inside the load callback itself).
    /// Tests and scenes depend on those generated assets, so nothing relies on a manual menu click.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorAutoBootstrap
    {
        static EditorAutoBootstrap()
        {
            EditorApplication.delayCall += RunIfMissing;
        }

        private static void RunIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<HushConfigSet>(HushPaths.ConfigSetAsset) != null) return;
            ProjectBootstrap.Run();
        }
    }
}
