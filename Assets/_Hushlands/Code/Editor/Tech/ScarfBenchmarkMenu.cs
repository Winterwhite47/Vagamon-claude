using System;
using System.IO;
using Hushlands.Core;
using UnityEditor;
using UnityEngine;

namespace Hushlands.Editor
{
    /// <summary>Applies the measured scarf choice (written by ScarfBenchmarkTests) to ClothConfig.</summary>
    public static class ScarfBenchmarkMenu
    {
        public const string LogPath = "Logs/scarf_benchmark.txt";

        [MenuItem("Hushlands/Tech/Apply Scarf Benchmark Result")]
        public static void Apply()
        {
            if (!File.Exists(LogPath)) throw new InvalidOperationException("Run the PlayMode test ScarfBenchmarkTests first.");
            ScarfMethod method = ParseChoice(File.ReadAllText(LogPath));
            HushConfigSet set = AssetDatabase.LoadAssetAtPath<HushConfigSet>(HushPaths.ConfigSetAsset);
            if (set == null || set.cloth == null) throw new InvalidOperationException("Run project bootstrap first.");
            Undo.RecordObject(set.cloth, "Apply scarf benchmark");
            set.cloth.Settings.method = method;
            EditorUtility.SetDirty(set.cloth);
            AssetDatabase.SaveAssets();
            Debug.Log("[Hushlands] Scarf method set to " + method + " from " + LogPath);
        }

        public static ScarfMethod ParseChoice(string log)
        {
            return ScarfMethodSelector.ParseLogChoice(log);
        }
    }
}
