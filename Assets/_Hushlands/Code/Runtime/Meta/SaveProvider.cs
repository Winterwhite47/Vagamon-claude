using System.IO;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Opens the player's save: a FileSaveService at persistentDataPath, shared for the app session.
    /// Tests set UseMemory so they never touch the player's file.
    /// </summary>
    public static class SaveProvider
    {
        public const string FileName = "hushlands.save";
        public static bool UseMemory;
        private static ISaveService _session;

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        public static ISaveService Open(HushConfigSet config)
        {
            if (UseMemory) return _session is MemorySaveService ? _session : (_session = new MemorySaveService());
            if (_session is FileSaveService) return _session;
            var file = new FileSaveService(DefaultPath);
            if (file.CorruptionDetected)
                Debug.Log("[Hushlands] Save was damaged; " + (file.RecoveredFromBackup ? "restored from backup." : "starting fresh."));
            _session = file;
            return _session;
        }

        public static void Reset()
        {
            _session = null;
        }
    }

}
