using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Everything the camera reacts to, gathered once per step.</summary>
    public struct CameraSignals
    {
        public RiderState Rider;
        public float Speed;
        public float AirborneSeconds;
        public bool BoostActive;
        public bool InVista;
        public bool InShelter;
        /// <summary>Seconds until the landslide reaches the rider; infinity when none.</summary>
        public float LandslideSeconds;
        public bool CollapseWarningActive;
        public bool FailureHold;
    }

    public interface ICameraSignalSource
    {
        CameraSignals Read(float dt);
    }

    /// <summary>Maps signals to state requests; the state machine's priorities and dwell pick the winner.</summary>
    public static class CameraStateRules
    {
        public static void Apply(CameraStateMachine sm, CameraSettings s, CameraSignals x)
        {
            sm.Request(CameraState.Cruise);
            if (x.FailureHold || x.Rider == RiderState.Crashed) sm.Request(CameraState.FailureHold);
            if (x.LandslideSeconds < s.dangerLandslideSeconds || x.CollapseWarningActive) sm.Request(CameraState.Danger);
            if (x.BoostActive || x.Speed >= s.boostSpeedThreshold) sm.Request(CameraState.Boost);
            if ((x.Rider == RiderState.Airborne || x.Rider == RiderState.Flipping) && x.AirborneSeconds >= s.airDelaySeconds)
                sm.Request(CameraState.Air);
            if (x.InVista) sm.Request(CameraState.Vista);
            if (x.InShelter) sm.Request(CameraState.Shelter);
        }
    }

    /// <summary>
    /// Signals from a rider and the stretch's Vista/Shelter zones. Boost, landslide, collapse warning and
    /// failure hold are set by their owning systems (T5, T8, T10).
    /// </summary>
    public sealed class RiderCameraSignals : ICameraSignalSource
    {
        private readonly IRider _rider;
        private readonly List<Span> _vista = new List<Span>();
        private readonly List<Span> _shelter = new List<Span>();
        private float _air;

        public RiderCameraSignals(IRider rider, IReadOnlyList<IChunk> stretch)
        {
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            LandslideSeconds = float.PositiveInfinity;
            if (stretch == null) return;
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    var span = new Span(c.StartDistance + f.start, c.StartDistance + f.End);
                    if (f.kind == FeatureKind.Vista) _vista.Add(span);
                    else if (f.kind == FeatureKind.Shelter) _shelter.Add(span);
                }
            }
        }

        public bool BoostActive { get; set; }
        public float LandslideSeconds { get; set; }
        public bool CollapseWarningActive { get; set; }
        public bool FailureHold { get; set; }

        public CameraSignals Read(float dt)
        {
            RiderState state = _rider.State;
            bool air = state == RiderState.Airborne || state == RiderState.Flipping;
            _air = air ? _air + dt : 0f;
            double d = _rider.Position.Distance;
            return new CameraSignals
            {
                Rider = state,
                Speed = _rider.Speed,
                AirborneSeconds = _air,
                BoostActive = BoostActive,
                InVista = Inside(_vista, d),
                InShelter = Inside(_shelter, d),
                LandslideSeconds = LandslideSeconds,
                CollapseWarningActive = CollapseWarningActive,
                FailureHold = FailureHold,
            };
        }

        private static bool Inside(List<Span> spans, double d)
        {
            foreach (Span s in spans)
            {
                if (s.Contains(d)) return true;
            }
            return false;
        }
    }
}
