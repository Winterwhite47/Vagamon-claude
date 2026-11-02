using Hushlands.Core;

namespace Hushlands
{
    /// <summary>Opens the player's save. T11: in-memory; T12 replaces with the versioned file save.</summary>
    public static class SaveProvider
    {
        private static ISaveService _session;

        public static ISaveService Open(HushConfigSet config)
        {
            if (_session == null) _session = new MemorySaveService();
            return _session;
        }
    }
}
