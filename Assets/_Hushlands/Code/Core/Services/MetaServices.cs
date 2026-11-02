using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>
    /// Score: distance (new ground only, so retries cannot farm it), flips, coins, rescues, all scaled by the active
    /// multiplier (tea flask). Fractional distance points carry over between steps.
    /// </summary>
    public sealed class ScoreService : IScoreService
    {
        private readonly ScoreSettings _s;
        private double _carry;
        private double _best = double.NegativeInfinity;

        public ScoreService(ScoreSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public long Score { get; private set; }
        public int SpiceCoins { get; private set; }
        public float Multiplier { get; set; } = 1f;
        public int Flips { get; private set; }
        public int Rescues { get; private set; }
        public event Action<long> Changed;

        public void Add(long points)
        {
            if (points < 0) throw new ArgumentOutOfRangeException(nameof(points));
            Grant(points * (double)Multiplier);
        }

        public void AddCoins(int coins)
        {
            if (coins < 0) throw new ArgumentOutOfRangeException(nameof(coins));
            SpiceCoins += coins;
            Grant((double)coins * _s.pointsPerCoin * Multiplier);
        }

        public void AddDistance(double meters)
        {
            if (meters > 0d) Grant(meters * _s.pointsPerMeter * Multiplier);
        }

        /// <summary>Scores only ground beyond the furthest point reached this run.</summary>
        public void TrackDistance(double x)
        {
            if (double.IsNegativeInfinity(_best))
            {
                _best = x;
                return;
            }
            if (x <= _best) return;
            AddDistance(x - _best);
            _best = x;
        }

        public void AddFlips(int flips)
        {
            if (flips <= 0) return;
            Flips += flips;
            Grant((double)flips * _s.pointsPerFlip * Multiplier);
        }

        public void AddRescue()
        {
            Rescues++;
            Grant(_s.pointsPerDucklingGroup * (double)Multiplier);
        }

        private void Grant(double points)
        {
            _carry += points;
            long whole = (long)Math.Floor(_carry);
            if (whole <= 0) return;
            _carry -= whole;
            Score += whole;
            Changed?.Invoke(Score);
        }
    }

    /// <summary>In-memory save stub. T12 replaces with versioned file storage.</summary>
    public sealed class MemorySaveService : ISaveService
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public void Save<T>(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("empty key", nameof(key));
            _data[key] = value;
        }

        public bool TryLoad<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out object raw) && raw is T typed)
            {
                value = typed;
                return true;
            }
            value = default(T);
            return false;
        }
    }

    /// <summary>Timed pickup effect driven by config duration; callbacks supply behaviour (T8).</summary>
    public sealed class TimedPickupEffect : IPickupEffect
    {
        private readonly Action _onApply;
        private readonly Action _onExpire;

        public TimedPickupEffect(PickupEntry entry, Action onApply, Action onExpire)
        {
            Id = entry.pickup;
            DurationSeconds = entry.durationSeconds;
            _onApply = onApply;
            _onExpire = onExpire;
        }

        public PickupId Id { get; }
        public float DurationSeconds { get; }
        public bool Active { get; private set; }
        public float Remaining { get; private set; }

        public void Apply()
        {
            Active = DurationSeconds > 0f;
            Remaining = DurationSeconds;
            _onApply?.Invoke();
            if (!Active) _onExpire?.Invoke();
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            Remaining -= dt;
            if (Remaining <= 0f) Expire();
        }

        public void Expire()
        {
            if (!Active) return;
            Active = false;
            Remaining = 0f;
            _onExpire?.Invoke();
        }
    }
}
