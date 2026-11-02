using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public sealed class SpeedModel : ISpeedModel
    {
        private readonly SpeedSettings _s;

        public SpeedModel(SpeedSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public float MinSpeed => _s.minSpeed;
        public float MaxSpeed => _s.maxSpeed;

        public float TargetSpeed(double runSeconds)
        {
            return _s.TargetAt(runSeconds);
        }

        public float Step(float currentSpeed, float slopeAngle, float friction, bool tucking, double runSeconds, float dt)
        {
            float drag = _s.baseDrag * (tucking ? _s.tuckDragMultiplier : 1f);
            float gravityAlong = _s.gravity * (float)Math.Sin(slopeAngle);
            float frictionDecel = _s.gravity * (float)Math.Cos(slopeAngle) * friction;
            float accel = gravityAlong - frictionDecel - drag * currentSpeed * currentSpeed;

            float target = TargetSpeed(runSeconds) + (tucking ? _s.tuckTargetBonus : 0f);
            accel += (target - currentSpeed) * _s.assistGain;

            float next = currentSpeed + accel * dt;
            if (next < _s.minSpeed) next = _s.minSpeed;
            if (next > _s.maxSpeed) next = _s.maxSpeed;
            return next;
        }
    }

    public sealed class SurfaceTable : ISurfaceProvider
    {
        private readonly Dictionary<SurfaceId, SurfaceEntry> _map = new Dictionary<SurfaceId, SurfaceEntry>();

        public SurfaceTable(SurfaceSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            foreach (SurfaceEntry s in settings.entries) _map[s.surface] = s;
        }

        public float FrictionFor(SurfaceId surface)
        {
            return Lookup(surface).friction;
        }

        public float DragFor(SurfaceId surface)
        {
            return Lookup(surface).drag;
        }

        private SurfaceEntry Lookup(SurfaceId id)
        {
            if (_map.TryGetValue(id, out SurfaceEntry e)) return e;
            throw new KeyNotFoundException("Surface not configured: " + id);
        }
    }

    /// <summary>Stub slope: constant angle, single surface. Replaced by spline adapter in T3.</summary>
    public sealed class ConstantSlope : ISlopeSpline
    {
        private readonly float _angle;
        private readonly SurfaceId _surface;

        public ConstantSlope(double length, float angleRadians, SurfaceId surface)
        {
            Length = length;
            _angle = angleRadians;
            _surface = surface;
        }

        public double Length { get; }

        public float HeightAt(double distance)
        {
            return (float)(-distance * Math.Tan(_angle));
        }

        public float AngleAt(double distance)
        {
            return _angle;
        }

        public SurfaceId SurfaceAt(double distance)
        {
            return _surface;
        }
    }

    /// <summary>Scriptable input for tests and the bot.</summary>
    public sealed class ScriptedInput : IInputSource
    {
        public bool TapThisStep { get; set; }
        public bool IsHeld { get; set; }
        public float HeldSeconds { get; set; }

        public void Clear()
        {
            TapThisStep = false;
            IsHeld = false;
            HeldSeconds = 0f;
        }
    }

    /// <summary>
    /// Kinematic reference rider on the 2.5D plane. Stub for T1; the PhysX rider (T3) replaces it in play,
    /// and the bot validator (T2) keeps using it as a deterministic model.
    /// </summary>
    public sealed class KinematicRider : IRider
    {
        private readonly RiderSettings _rider;
        private readonly SpeedSettings _speedSettings;
        private readonly ISpeedModel _speed;
        private readonly ISlopeSpline _slope;
        private readonly ISurfaceProvider _surfaces;
        private double _distance;
        private float _height;
        private float _verticalVelocity;
        private double _runSeconds;

        public KinematicRider(RiderSettings rider, SpeedSettings speed, ISlopeSpline slope, ISurfaceProvider surfaces)
        {
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            _speedSettings = speed ?? throw new ArgumentNullException(nameof(speed));
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _speed = new SpeedModel(speed);
            Speed = speed.startSpeed;
            _height = slope.HeightAt(0d);
        }

        public RiderState State { get; private set; } = RiderState.Grounded;
        public SlopePoint Position => new SlopePoint(_distance, _height);
        public float Speed { get; private set; }
        public float FlipAngle { get; private set; }
        public int CompletedFlips { get; private set; }

        public event Action<RiderState, RiderState> StateChanged;
        public event Action<CrashCause> Crashed;

        public void FixedStep(IInputSource input, float dt)
        {
            if (State == RiderState.Crashed) return;
            _runSeconds += dt;
            float angle = _slope.AngleAt(_distance);
            float friction = _surfaces.FrictionFor(_slope.SurfaceAt(_distance));
            bool grounded = State == RiderState.Grounded || State == RiderState.Tucking;

            if (grounded)
            {
                bool tuck = input.IsHeld && input.HeldSeconds >= _rider.holdThresholdSeconds;
                Speed = _speed.Step(Speed, angle, friction, tuck, _runSeconds, dt);
                _distance += Speed * dt;
                _height = _slope.HeightAt(_distance);
                if (input.TapThisStep)
                {
                    _verticalVelocity = _rider.jumpVelocity;
                    SetState(RiderState.Airborne);
                }
                else
                {
                    SetState(tuck ? RiderState.Tucking : RiderState.Grounded);
                }
                return;
            }

            _distance += Speed * dt;
            _verticalVelocity -= _speedSettings.gravity * _rider.airGravityScale * dt;
            _height += _verticalVelocity * dt;

            if (input.IsHeld)
            {
                FlipAngle += _rider.flipDegreesPerSecond * dt;
                SetState(RiderState.Flipping);
            }
            else if (State == RiderState.Flipping)
            {
                SetState(RiderState.Airborne);
            }

            float ground = _slope.HeightAt(_distance);
            if (_height <= ground) Land(ground);
        }

        public void ResetTo(SlopePoint point, float speed)
        {
            _distance = point.Distance;
            _height = point.Height;
            _verticalVelocity = 0f;
            Speed = speed;
            FlipAngle = 0f;
            SetState(RiderState.Grounded);
        }

        private void Land(float ground)
        {
            _height = ground;
            _verticalVelocity = 0f;
            float rem = FlipAngle % 360f;
            float offBy = Math.Min(rem, 360f - rem);
            int flips = (int)Math.Round(FlipAngle / 360f);
            FlipAngle = 0f;
            if (offBy > _rider.safeLandingToleranceDegrees)
            {
                SetState(RiderState.Crashed);
                Crashed?.Invoke(CrashCause.BadLanding);
                return;
            }
            CompletedFlips += flips;
            SetState(RiderState.Grounded);
        }

        private void SetState(RiderState next)
        {
            if (next == State) return;
            RiderState prev = State;
            State = next;
            StateChanged?.Invoke(prev, next);
        }
    }
}
