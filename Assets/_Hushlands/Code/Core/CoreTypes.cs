// Hushlands core value types. Engine-free (noEngineReferences).
namespace Hushlands.Core
{
    /// <summary>Position on the 2.5D gameplay plane: distance along spline and height above slope.</summary>
    public readonly struct SlopePoint
    {
        public readonly double Distance;
        public readonly float Height;

        public SlopePoint(double distance, float height)
        {
            Distance = distance;
            Height = height;
        }
    }

    public enum BiomeId
    {
        WindmouthRidge = 0,
        MeadowSea = 1,
        PineHollow = 2,
        TerraceCountry = 3,
        MirrorLake = 4,
    }

    public enum SurfaceId
    {
        Grass = 0,
        Snow = 1,
        Mud = 2,
        PineNeedles = 3,
        TeaTerrace = 4,
        Boardwalk = 5,
        Ice = 6,
        Stone = 7,
    }

    public enum RiderState
    {
        Grounded = 0,
        Tucking = 1,
        Airborne = 2,
        Flipping = 3,
        Crashed = 4,
    }

    /// <summary>The seven camera states from the bible.</summary>
    public enum CameraState
    {
        Cruise = 0,
        Air = 1,
        Boost = 2,
        Vista = 3,
        Danger = 4,
        Shelter = 5,
        FailureHold = 6,
    }

    public enum VehicleId
    {
        WoodenHillSled = 0,
        TeaCrateBoard = 1,
        BananaLeafToboggan = 2,
        BambooSled = 3,
        GliderSkid = 4,
        RainUmbrellaSlider = 5,
    }

    public enum PickupId
    {
        SpiceCoin = 0,
        MistLantern = 1,
        WindFeather = 2,
        TeaFlask = 3,
        MagnetBell = 4,
        GoldenMistbell = 5,
    }

    /// <summary>Duckling group status. There is deliberately no "Lost" or "Injured" value.</summary>
    public enum DucklingGroupStatus
    {
        Ahead = 0,
        Rescued = 1,
        Waiting = 2,
    }

    public enum CrashCause
    {
        Obstacle = 0,
        BadLanding = 1,
        Landslide = 2,
        Gap = 3,
    }
}
