using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Slow swing of the light around each biome's own time of day (in [-1, 1]; seeded phase).</summary>
    public sealed class DayDrift
    {
        private readonly DirectorSettings _s;
        private readonly double _phase;

        public DayDrift(DirectorSettings settings, long seed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _phase = new Rng(seed).Range(0d, Math.PI * 2d);
        }

        /// <summary>-1 (lowest, warmest) to 1 (highest, coolest) at this run time.</summary>
        public double Swing(double runSeconds)
        {
            return Math.Sin(_phase + runSeconds / _s.dayDriftPeriodSeconds * Math.PI * 2d);
        }
    }

    /// <summary>
    /// Seeded sky moods that cross-fade. A change only begins while play is calm (the pacing director says so),
    /// so the light never shifts in the middle of a jump.
    /// </summary>
    public sealed class WeatherDirector
    {
        private readonly DirectorSettings _s;
        private readonly Rng _rng;
        private double _hold;
        private double _fade;

        public WeatherDirector(DirectorSettings settings, long seed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _rng = new Rng(seed);
            Current = WeatherKind.Clear;
            Previous = WeatherKind.Clear;
            Blend = 1f;
            _hold = _rng.Range(_s.weatherHoldMinSeconds, _s.weatherHoldMaxSeconds);
        }

        public WeatherKind Current { get; private set; }
        public WeatherKind Previous { get; private set; }
        /// <summary>0 = all Previous, 1 = all Current.</summary>
        public float Blend { get; private set; }
        public int Changes { get; private set; }

        public float SunLuxScale => Mix(_s.Get(Previous).sunLuxScale, _s.Get(Current).sunLuxScale);
        public float FogScale => Mix(_s.Get(Previous).fogScale, _s.Get(Current).fogScale);
        public float ExposureOffsetEV => Mix(_s.Get(Previous).exposureOffsetEV, _s.Get(Current).exposureOffsetEV);

        public void Step(float dt, bool calm)
        {
            if (Blend < 1f)
            {
                _fade += dt;
                double k = Math.Min(1d, _fade / _s.weatherTransitionSeconds);
                Blend = (float)(k * k * (3d - 2d * k));
                return;
            }
            _hold -= dt;
            if (_hold > 0d || !calm) return;
            Previous = Current;
            Current = Pick();
            _hold = _rng.Range(_s.weatherHoldMinSeconds, _s.weatherHoldMaxSeconds);
            if (Current == Previous) return;
            Changes++;
            _fade = 0d;
            Blend = 0f;
        }

        private WeatherKind Pick()
        {
            double total = 0d;
            foreach (WeatherEntry w in _s.weather) total += w.weight;
            double roll = _rng.Range(0d, total);
            foreach (WeatherEntry w in _s.weather)
            {
                roll -= w.weight;
                if (roll < 0d) return w.kind;
            }
            return _s.weather[_s.weather.Length - 1].kind;
        }

        private float Mix(float a, float b)
        {
            return a + (b - a) * Blend;
        }
    }

    /// <summary>
    /// Reads how tense the ride is (hazards just ahead, danger, squall, a recent spill) so calm-only things
    /// (weather changes, humor beats) wait their turn. It never changes content or rules.
    /// </summary>
    public sealed class PacingDirector
    {
        private readonly DirectorSettings _s;
        private readonly List<double> _hazards = new List<double>();
        private float _heat;
        private float _calmFor;

        public PacingDirector(DirectorSettings settings, IReadOnlyList<IChunk> stretch)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (stretch != null)
            {
                foreach (IChunk c in stretch)
                {
                    foreach (ChunkFeature f in c.Features)
                    {
                        if (f.IsHazard) _hazards.Add(c.StartDistance + f.start);
                    }
                }
            }
            _hazards.Sort();
        }

        /// <summary>0 (quiet) to 1 (as tense as it gets).</summary>
        public float Intensity { get; private set; }
        public bool Calm => _calmFor >= _s.pacingCalmHoldSeconds;
        public bool Tense => Intensity >= _s.pacingTenseThreshold;

        public int HazardsAhead(double distance, float speed)
        {
            double end = distance + Math.Max(0f, speed) * _s.pacingLookaheadSeconds;
            int n = 0;
            foreach (double h in _hazards)
            {
                if (h > end) break;
                if (h >= distance) n++;
            }
            return n;
        }

        public void NoteFailure()
        {
            _heat = Math.Min(1f, _heat + _s.pacingFailureHeat);
            _calmFor = 0f;
        }

        public void Step(double distance, float speed, bool danger, float squallIntensity, float dt)
        {
            _heat = Math.Max(0f, _heat - _s.pacingHeatDecayPerSecond * dt);
            float hazards = Math.Min(1f, HazardsAhead(distance, speed) / (float)_s.pacingFullHazards);
            float target = hazards * _s.pacingHazardWeight + (danger ? _s.pacingDangerWeight : 0f) +
                           Math.Max(0f, Math.Min(1f, squallIntensity)) * _s.pacingSquallWeight + _heat;
            target = Math.Min(1f, target);
            float k = Math.Min(1f, _s.pacingResponse * dt);
            Intensity += (target - Intensity) * k;
            _calmFor = Intensity < _s.pacingCalmThreshold ? _calmFor + dt : 0f;
        }
    }

    public readonly struct HumorBeat
    {
        public readonly string Id;
        public readonly string Line;
        /// <summary>Where along the run the beat sits (ahead of the rider when it began).</summary>
        public readonly double Distance;

        public HumorBeat(string id, string line, double distance)
        {
            Id = id;
            Line = line;
            Distance = distance;
        }
    }

    /// <summary>Seeded, sparse sight gags. A beat only begins while play is calm and ends early if the ride turns tense.</summary>
    public sealed class HumorDirector
    {
        private readonly DirectorSettings _s;
        private readonly Rng _rng;
        private double _wait;
        private string _lastId;

        public HumorDirector(DirectorSettings settings, long seed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _rng = new Rng(seed);
            _wait = settings.humorFirstSeconds;
        }

        public bool Active { get; private set; }
        public HumorBeat Current { get; private set; }
        public float Elapsed { get; private set; }
        public int Count { get; private set; }
        public event Action<HumorBeat> BeatStarted;
        public event Action BeatEnded;

        public void Step(double distance, bool calm, bool tense, float dt)
        {
            if (!_s.humorEnabled)
            {
                Cancel();
                return;
            }
            if (Active)
            {
                Elapsed += dt;
                if (tense || Elapsed >= _s.humorDurationSeconds) Cancel();
                return;
            }
            _wait -= dt;
            if (_wait > 0d || !calm) return;
            HumorBeatEntry pick = Pick();
            _lastId = pick.id;
            _wait = _rng.Range(_s.humorIntervalMinSeconds, _s.humorIntervalMaxSeconds);
            Current = new HumorBeat(pick.id, pick.line, distance + _s.humorAheadMeters);
            Active = true;
            Elapsed = 0f;
            Count++;
            BeatStarted?.Invoke(Current);
        }

        /// <summary>Ends the current beat at once (a spill, a retry, the end of the run).</summary>
        public void Cancel()
        {
            if (!Active) return;
            Active = false;
            BeatEnded?.Invoke();
        }

        private HumorBeatEntry Pick()
        {
            // Never the same gag twice in a row (when there is a choice).
            double total = 0d;
            foreach (HumorBeatEntry b in _s.humorBeats)
            {
                if (b.id != _lastId || _s.humorBeats.Length == 1) total += b.weight;
            }
            double roll = _rng.Range(0d, total);
            HumorBeatEntry last = _s.humorBeats[0];
            foreach (HumorBeatEntry b in _s.humorBeats)
            {
                if (b.id == _lastId && _s.humorBeats.Length > 1) continue;
                last = b;
                roll -= b.weight;
                if (roll < 0d) return b;
            }
            return last;
        }
    }

    /// <summary>The rider's settled pose a moment after a spill.</summary>
    public readonly struct ChoreographyPose
    {
        public readonly float LeanDegrees;
        public readonly float SinkMeters;
        /// <summary>0 (no puff) to 1 (rider fully inside a soft mist puff).</summary>
        public readonly float Cover;

        public ChoreographyPose(float leanDegrees, float sinkMeters, float cover)
        {
            LeanDegrees = leanDegrees;
            SinkMeters = sinkMeters;
            Cover = cover;
        }
    }

    /// <summary>
    /// Calm failure choreography: the rider eases back and sits down (never thrown), or a mist puff closes over
    /// them before anything unkind could be seen (gap, landslide). Pure function of cause and time since the spill.
    /// </summary>
    public static class FailureChoreography
    {
        public static ChoreographyPose Evaluate(DirectorSettings s, CrashCause cause, float seconds)
        {
            ChoreographyEntry c = s.Get(cause);
            float k = EaseOut(seconds / c.settleSeconds);
            float cover = c.coverSeconds > 0f ? EaseOut(seconds / c.coverSeconds) : 0f;
            return new ChoreographyPose(c.leanDegrees * k, c.sinkMeters * k, cover);
        }

        private static float EaseOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }

    /// <summary>Everything the sky and air need this frame.</summary>
    public struct AtmosphereState
    {
        public float SunElevationDegrees;
        public float SunAzimuthDegrees;
        public float SunLux;
        public float SunKelvin;
        public float ExposureEV;
        /// <summary>Fog mean free path before the squall scales it.</summary>
        public float FogMeanFreePath;
        public float FogBaseHeight;
        public float FogMaximumHeight;
        public float FogAnisotropy;
        public float WindExtraX;
        public float WindExtraZ;
    }

    /// <summary>Biome blend, then the day's slow swing, then the weather. Fog is only ever thickened by the weather scale.</summary>
    public static class AtmosphereModel
    {
        public static AtmosphereState Evaluate(BiomeLook from, BiomeLook to, double t, DirectorSettings s, double swing,
            float luxScale, float fogScale, float exposureOffset)
        {
            var a = new AtmosphereState();
            float elevation = Lerp(from.sunElevationDegrees, to.sunElevationDegrees, t) + (float)swing * s.dayDriftElevationDegrees;
            a.SunElevationDegrees = Math.Max(s.minSunElevationDegrees, elevation);
            a.SunAzimuthDegrees = Lerp(from.sunAzimuthDegrees, to.sunAzimuthDegrees, t);
            a.SunLux = Lerp(from.sunLux, to.sunLux, t) * luxScale;
            a.SunKelvin = Lerp(from.sunKelvin, to.sunKelvin, t) + (float)swing * s.dayDriftKelvin;
            a.ExposureEV = Lerp(from.exposureEV, to.exposureEV, t) + (float)swing * s.dayDriftExposureEV + exposureOffset;
            a.FogMeanFreePath = Lerp(from.fogMeanFreePath, to.fogMeanFreePath, t) * fogScale;
            a.FogBaseHeight = Lerp(from.fogBaseHeight, to.fogBaseHeight, t);
            a.FogMaximumHeight = Lerp(from.fogMaximumHeight, to.fogMaximumHeight, t);
            a.FogAnisotropy = Lerp(from.fogAnisotropy, to.fogAnisotropy, t);
            a.WindExtraX = Lerp(from.windExtraX, to.windExtraX, t);
            a.WindExtraZ = Lerp(from.windExtraZ, to.windExtraZ, t);
            return a;
        }

        private static float Lerp(float a, float b, double t)
        {
            return a + (b - a) * (float)t;
        }
    }
}
