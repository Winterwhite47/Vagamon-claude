using System;
using System.Collections.Generic;
using System.IO;
using Hushlands.Core;
using UnityEditor;
using UnityEngine;

namespace Hushlands.Editor
{
    /// <summary>Content tool entry points. Batch: -executeMethod Hushlands.Editor.ContentToolMenu.ValidateBatch</summary>
    public static class ContentToolMenu
    {
        public const string LogPath = "Logs/content_validation.txt";
        public const string SamplePath = HushPaths.Root + "/Content/Chunks/sample_meadow.chunk";

        [MenuItem("Hushlands/Content/Validate Stretches")]
        public static void Validate()
        {
            HushConfigSet set = Require();
            StretchValidation run = Build(set);
            string log = run.Run(1, set.content.Settings.validationStretchCount);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? ".");
            File.WriteAllText(LogPath, log);
            string summary = "[Hushlands] Content validation " + run.Passed + "/" + (run.Passed + run.Failed) +
                             " passed, hazards=" + run.HazardCount + ", log=" + LogPath;
            if (run.Failed == 0) Debug.Log(summary);
            else Debug.LogError(summary + "\n" + string.Join("\n", run.Failures));
        }

        public static void ValidateBatch()
        {
            HushConfigSet set = Require();
            StretchValidation run = Build(set);
            string log = run.Run(1, set.content.Settings.validationStretchCount);
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? ".");
            File.WriteAllText(LogPath, log);
            EditorApplication.Exit(run.Failed == 0 ? 0 : 1);
        }

        [MenuItem("Hushlands/Content/Export Sample Stretch")]
        public static void ExportSample()
        {
            HushConfigSet set = Require();
            JumpModel jump = JumpModel.ForAllVehicles(set.rider.Settings, set.speed.Settings, set.vehicles.Settings);
            var gen = new ChunkGenerator(set.content.Settings, set.fairness.Settings, set.biomes.Settings, jump);
            List<Chunk> stretch = gen.Generate(1, BiomeId.MeadowSea, set.content.Settings.chunksPerStretch);
            ProjectBootstrap.EnsureFolder(Path.GetDirectoryName(SamplePath)?.Replace('\\', '/'));
            File.WriteAllText(SamplePath, "# Generated: seed 1, MeadowSea\n" + ChunkSerializer.Write(stretch));
            AssetDatabase.ImportAsset(SamplePath);
            Debug.Log("[Hushlands] Wrote " + SamplePath);
        }

        private static StretchValidation Build(HushConfigSet s)
        {
            return StretchValidation.FromSettings(s.content.Settings, s.fairness.Settings, s.biomes.Settings,
                s.rider.Settings, s.speed.Settings, s.surfaces.Settings, s.vehicles.Settings);
        }

        private static HushConfigSet Require()
        {
            HushConfigSet set = AssetDatabase.LoadAssetAtPath<HushConfigSet>(HushPaths.ConfigSetAsset);
            if (set == null) throw new InvalidOperationException("Run Hushlands/Setup/Run Project Bootstrap first.");
            return set;
        }
    }
}
