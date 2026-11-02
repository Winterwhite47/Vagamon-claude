namespace Hushlands
{
    /// <summary>What the UI may ask the game to do (implemented by SliceBootstrap).</summary>
    public interface IGameControl
    {
        void StartRide();
        void SetPaused(bool paused);
        /// <summary>Reload under a mist wipe; autoRide skips the title (Ride again).</summary>
        void Reload(bool autoRide);
        void ApplyOptions();
    }
}
