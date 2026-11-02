namespace Hushlands
{
    /// <summary>State carried across a mist-covered scene reload ("Ride again", vehicle change).</summary>
    public static class SceneHandoff
    {
        /// <summary>Start the next scene fully covered by mist and fade it away (no visible cut).</summary>
        public static bool CoverOnStart;
        /// <summary>Skip the title and ride at once.</summary>
        public static bool AutoRide;
    }
}
