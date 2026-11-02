using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Flushes the file save when the app closes.</summary>
    public sealed class SaveFlusher : MonoBehaviour
    {
        private void OnApplicationQuit()
        {
            if (SaveProvider.UseMemory) return;
            if (SaveProvider.Open(null) is FileSaveService file && file.Dirty) file.Flush();
        }
    }
}
