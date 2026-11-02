using System;

namespace Hushlands.Core
{
    /// <summary>Camera pose on the gameplay plane: camera at (X, Y, Z) looking along +Z with a downward pitch.</summary>
    public struct CameraPose
    {
        public double X;
        public double Y;
        public double Z;
        public float FieldOfView;
        public float PitchDegrees;
    }

    /// <summary>
    /// Continuous framing: profile parameters blend with smoothstep (retargeting mid-blend starts from the current
    /// values, so there are no cuts); X tracks the rider exactly so the reveal distance never lags; Y is smoothed.
    /// </summary>
    public sealed class CameraRig
    {
        private readonly CameraSettings _s;
        private Params _from;
        private Params _to;
        private Params _now;
        private float _t;
        private float _duration;
        private double _y;
        private bool _hasY;

        private struct Params
        {
            public float Fov;
            public float Distance;
            public float Height;
            public float LookAhead;
            public float Pitch;

            public static Params Of(CameraStateProfile p)
            {
                return new Params { Fov = p.fieldOfView, Distance = p.distance, Height = p.height, LookAhead = p.lookAhead, Pitch = p.pitchDegrees };
            }

            public static Params Lerp(Params a, Params b, float k)
            {
                return new Params
                {
                    Fov = a.Fov + (b.Fov - a.Fov) * k,
                    Distance = a.Distance + (b.Distance - a.Distance) * k,
                    Height = a.Height + (b.Height - a.Height) * k,
                    LookAhead = a.LookAhead + (b.LookAhead - a.LookAhead) * k,
                    Pitch = a.Pitch + (b.Pitch - a.Pitch) * k,
                };
            }
        }

        public CameraRig(CameraSettings settings, CameraStateProfile initial)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _now = _from = _to = Params.Of(initial);
        }

        public float FieldOfView => _now.Fov;
        public float Distance => _now.Distance;
        public float Height => _now.Height;
        public float LookAhead => _now.LookAhead;
        public bool Blending => _t < _duration;

        public void SetTarget(CameraStateProfile target, float blendSeconds)
        {
            _from = _now;
            _to = Params.Of(target);
            _t = 0f;
            _duration = Math.Max(0f, blendSeconds);
            if (_duration <= 0f) _now = _to;
        }

        /// <summary>Drops vertical smoothing history (after a retry teleport).</summary>
        public void Snap()
        {
            _hasY = false;
        }

        public CameraPose Update(double riderX, ISlopeSpline slope, float dt, bool reducedMotion)
        {
            if (_t < _duration)
            {
                _t = Math.Min(_duration, _t + dt);
                float u = _t / _duration;
                _now = Params.Lerp(_from, _to, u * u * (3f - 2f * u));
            }

            double cx = riderX + _now.LookAhead;
            double targetY = slope.HeightAt(cx) + _now.Height;
            if (!_hasY)
            {
                _y = targetY;
                _hasY = true;
            }
            else
            {
                double tau = _s.verticalSmoothSeconds * (reducedMotion ? _s.reducedMotionSmoothScale : 1f);
                _y += (targetY - _y) * (1d - Math.Exp(-dt / tau));
            }

            return new CameraPose
            {
                X = cx,
                Y = _y,
                Z = -_now.Distance,
                FieldOfView = _now.Fov,
                PitchDegrees = _now.Pitch,
            };
        }
    }
}
