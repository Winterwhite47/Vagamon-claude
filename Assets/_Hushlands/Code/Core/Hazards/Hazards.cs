using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Riders that can be crashed from outside (landslide catch, tests).</summary>
    public interface ICrashable
    {
        void ForceCrash(CrashCause cause);
    }

    /// <summary>
    /// Landslide front chasing the rider along the run. Speed = pace x rider target + gain x (gap - preferred), clamped.
    /// At or above cruise target the rider pulls ahead to an equilibrium gap; it only closes on a rider stalled at the
    /// 6 m/s floor, slowly, with the Danger camera on long before. Reset behind the retry point with a calm delay.
    /// </summary>
    public sealed class LandslideChaser
    {
        private readonly HazardSettings _s;
        private float _delay;

        public LandslideChaser(HazardSettings settings, double riderStart)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            Reset(riderStart, settings.landslideStartGap);
        }

        public double Front { get; private set; }
        public float Speed { get; private set; }
        public bool Active => _s.landslideEnabled && _delay <= 0f;

        public void Reset(double riderX, float gap)
        {
            Front = riderX - gap;
            Speed = 0f;
            _delay = _s.landslideStartDelaySeconds;
        }

        public double Gap(double riderX)
        {
            return riderX - Front;
        }

        /// <summary>
        /// Seconds until contact at the current closing rate; 0 inside landslideDangerGap; infinity when not gaining.
        /// Feeds the camera Danger signal.
        /// </summary>
        public float SecondsToContact(double riderX, float riderSpeed)
        {
            if (!Active) return float.PositiveInfinity;
            double gap = Gap(riderX);
            if (gap <= _s.landslideDangerGap) return 0f;
            double closing = Speed - riderSpeed;
            if (closing <= 1e-6) return float.PositiveInfinity;
            return (float)(gap / closing);
        }

        /// <summary>Advances; returns true if the front reached the rider.</summary>
        public bool Step(double riderX, float riderTargetSpeed, float dt, float footprint)
        {
            if (!_s.landslideEnabled) return false;
            if (_delay > 0f)
            {
                _delay -= dt;
                return false;
            }
            double gap = Gap(riderX);
            double v = _s.landslidePaceRatio * riderTargetSpeed + _s.landslideGapGain * (gap - _s.landslidePreferredGap);
            if (v < _s.landslideMinSpeed) v = _s.landslideMinSpeed;
            if (v > _s.landslideMaxSpeed) v = _s.landslideMaxSpeed;
            Speed = (float)v;
            Front += v * dt;
            return Gap(riderX) <= footprint;
        }
    }

    public enum CollapseState
    {
        Intact = 0,
        Warning = 1,
        Open = 2,
    }

    /// <summary>
    /// Collapse zones: Intact until the rider is within warningLead, then Warning for collapseWarningSeconds (cracks,
    /// Danger camera), then Open. Content guarantees warningLead >= 1 s at max speed + takeoff zone, so the warning
    /// always completes before the rider can take off. Physics treats the zone as a gap throughout.
    /// </summary>
    public sealed class CollapseController
    {
        private readonly FairnessSettings _f;
        private readonly HazardSettings _h;
        private readonly List<Zone> _zones = new List<Zone>();

        public sealed class Zone
        {
            public double Start;
            public double End;
            public double WarningLead;
            public CollapseState State;
            public float WarningTime;
        }

        public CollapseController(FairnessSettings fairness, HazardSettings hazards, IReadOnlyList<IChunk> stretch)
        {
            _f = fairness ?? throw new ArgumentNullException(nameof(fairness));
            _h = hazards ?? throw new ArgumentNullException(nameof(hazards));
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind != FeatureKind.CollapseZone) continue;
                    _zones.Add(new Zone { Start = c.StartDistance + f.start, End = c.StartDistance + f.End, WarningLead = f.warningLead });
                }
            }
            _zones.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        public IReadOnlyList<Zone> Zones => _zones;
        public bool AnyWarning { get; private set; }
        public event Action<Zone> WarningStarted;
        public event Action<Zone> Opened;

        public void Step(double riderX, float dt)
        {
            AnyWarning = false;
            if (!_h.collapseEnabled) return;
            foreach (Zone z in _zones)
            {
                switch (z.State)
                {
                    case CollapseState.Intact:
                        if (riderX >= z.Start - z.WarningLead && riderX < z.End)
                        {
                            z.State = CollapseState.Warning;
                            z.WarningTime = 0f;
                            WarningStarted?.Invoke(z);
                        }
                        break;
                    case CollapseState.Warning:
                        z.WarningTime += dt;
                        if (z.WarningTime >= _f.collapseWarningSeconds)
                        {
                            z.State = CollapseState.Open;
                            Opened?.Invoke(z);
                        }
                        break;
                }
                if (z.State == CollapseState.Warning) AnyWarning = true;
            }
        }

        /// <summary>After a retry, zones ahead of the restart point are whole again.</summary>
        public void ResetAhead(double riderX)
        {
            foreach (Zone z in _zones)
            {
                if (z.End > riderX)
                {
                    z.State = CollapseState.Intact;
                    z.WarningTime = 0f;
                }
            }
        }
    }

    /// <summary>
    /// Seeded squalls: intensity ramps 0 -> 1 -> 0 over squallDurationSeconds with squallRampSeconds edges.
    /// Drives a small headwind, gusts on the scarf, denser fog (within the mist-fairness check) and extra low-pass.
    /// </summary>
    public sealed class SquallDirector
    {
        private readonly HazardSettings _s;
        private readonly Rng _rng;
        private double _time;
        private double _nextStart;

        public SquallDirector(HazardSettings settings, long seed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _rng = new Rng(seed);
            _nextStart = settings.squallFirstSeconds;
            CurrentStart = double.NegativeInfinity;
        }

        public double CurrentStart { get; private set; }
        public float Intensity { get; private set; }
        public float SpeedPenalty => _s.squallSpeedPenalty * Intensity;
        public float FogScale => 1f - (1f - _s.squallFogScale) * Intensity;
        public float MistBonus => _s.squallMistBonus * Intensity;
        public int Count { get; private set; }

        public void Step(float dt)
        {
            if (!_s.squallEnabled)
            {
                Intensity = 0f;
                return;
            }
            _time += dt;
            if (_time >= _nextStart)
            {
                CurrentStart = _nextStart;
                Count++;
                _nextStart = CurrentStart + _s.squallDurationSeconds +
                             _rng.Range(_s.squallIntervalMinSeconds, _s.squallIntervalMaxSeconds);
            }
            double t = _time - CurrentStart;
            if (t < 0d || t > _s.squallDurationSeconds)
            {
                Intensity = 0f;
                return;
            }
            double ramp = _s.squallRampSeconds;
            double k = Math.Min(1d, Math.Min(t / ramp, (_s.squallDurationSeconds - t) / ramp));
            Intensity = (float)(k * k * (3d - 2d * k));
        }
    }
}
