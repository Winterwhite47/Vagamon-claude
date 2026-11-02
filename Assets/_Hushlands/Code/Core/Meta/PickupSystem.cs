using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Riders whose cruise target can be boosted (wind feather).</summary>
    public interface IBoostable
    {
        void SetBoost(float metersPerSecond);
    }

    /// <summary>One pickup instance on the plane. Position can move (magnet).</summary>
    public sealed class PickupInstance
    {
        public PickupInstance(int index, PickupId kind, double x, double y)
        {
            Index = index;
            Kind = kind;
            X = x;
            Y = y;
            HomeX = x;
            HomeY = y;
        }

        public int Index { get; }
        public PickupId Kind { get; }
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public double HomeX { get; }
        public double HomeY { get; }
        public bool Collected { get; internal set; }
    }

    /// <summary>
    /// Pickups from the stretch, collection, magnet pull and timed effects.
    /// Coins add currency; the golden mistbell adds its coin value plus a score bonus; lantern, feather, tea and magnet
    /// are timed (picking one up again refreshes the full duration). Effects end on a crash; collected pickups stay
    /// collected after a retry (no farming).
    /// </summary>
    public sealed class PickupSystem
    {
        private readonly PickupSettings _s;
        private readonly ScoreService _score;
        private readonly IBoostable _boost;
        private readonly List<PickupInstance> _items = new List<PickupInstance>();
        private readonly Dictionary<PickupId, float> _remaining = new Dictionary<PickupId, float>();

        public PickupSystem(PickupSettings settings, ScoreService score, IBoostable boost, IReadOnlyList<IChunk> stretch,
            ISlopeSpline slope, float hover)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _score = score ?? throw new ArgumentNullException(nameof(score));
            _boost = boost;
            if (stretch == null) throw new ArgumentNullException(nameof(stretch));
            if (slope == null) throw new ArgumentNullException(nameof(slope));
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind != FeatureKind.Pickup) continue;
                    double x = c.StartDistance + f.start;
                    PickupId kind = Enum.IsDefined(typeof(PickupId), f.variant) ? (PickupId)f.variant : PickupId.SpiceCoin;
                    _items.Add(new PickupInstance(_items.Count, kind, x, slope.HeightAt(x) + hover));
                }
            }
        }

        public IReadOnlyList<PickupInstance> Items => _items;
        public int CollectedCount { get; private set; }
        public bool WindActive => Active(PickupId.WindFeather);
        public bool MagnetActive => Active(PickupId.MagnetBell);
        public bool LanternActive => Active(PickupId.MistLantern);
        public bool TeaActive => Active(PickupId.TeaFlask);
        public float MistScale => LanternActive ? _s.lanternMistScale : 1f;
        /// <summary>Cruise bonus from pickups (wind feather); RunSession composes it with the squall headwind.</summary>
        public float BoostBonus => WindActive ? _s.windSpeedBonus : 0f;
        public event Action<PickupInstance> Collected;
        public event Action<PickupId, bool> EffectChanged;

        public bool Active(PickupId id)
        {
            return _remaining.TryGetValue(id, out float t) && t > 0f;
        }

        public float Remaining(PickupId id)
        {
            return _remaining.TryGetValue(id, out float t) ? Math.Max(0f, t) : 0f;
        }

        /// <summary>Per fixed step while riding. Rider position is the body point on the plane.</summary>
        public void Step(double riderX, double riderY, float dt)
        {
            TickEffects(dt);
            double collect2 = _s.collectRadius * _s.collectRadius;
            double magnet2 = _s.magnetRadius * _s.magnetRadius;
            bool magnet = MagnetActive;
            double pull = _s.magnetPullSpeed * dt;
            foreach (PickupInstance p in _items)
            {
                if (p.Collected) continue;
                double dx = riderX - p.X;
                double dy = riderY - p.Y;
                double d2 = dx * dx + dy * dy;
                if (magnet && p.Kind == PickupId.SpiceCoin && d2 <= magnet2 && d2 > 1e-12)
                {
                    double d = Math.Sqrt(d2);
                    double step = Math.Min(pull, d);
                    p.X += dx / d * step;
                    p.Y += dy / d * step;
                    dx = riderX - p.X;
                    dy = riderY - p.Y;
                    d2 = dx * dx + dy * dy;
                }
                if (d2 <= collect2) Collect(p);
            }
        }

        /// <summary>A crash ends every timed effect.</summary>
        public void OnCrash()
        {
            var ids = new List<PickupId>(_remaining.Keys);
            foreach (PickupId id in ids)
            {
                if (_remaining[id] > 0f) End(id);
            }
        }

        private void Collect(PickupInstance p)
        {
            p.Collected = true;
            CollectedCount++;
            PickupEntry e = _s.Get(p.Kind);
            switch (p.Kind)
            {
                case PickupId.SpiceCoin:
                    _score.AddCoins(e.value);
                    break;
                case PickupId.GoldenMistbell:
                    _score.AddCoins(e.value);
                    _score.Add(_s.mistbellScoreBonus);
                    break;
                default:
                    Begin(p.Kind, e.durationSeconds);
                    break;
            }
            Collected?.Invoke(p);
        }

        private void Begin(PickupId id, float duration)
        {
            bool was = Active(id);
            _remaining[id] = duration;
            Apply();
            if (!was) EffectChanged?.Invoke(id, true);
        }

        private void End(PickupId id)
        {
            _remaining[id] = 0f;
            Apply();
            EffectChanged?.Invoke(id, false);
        }

        private void TickEffects(float dt)
        {
            if (_remaining.Count == 0) return;
            var ids = new List<PickupId>(_remaining.Keys);
            foreach (PickupId id in ids)
            {
                float t = _remaining[id];
                if (t <= 0f) continue;
                t -= dt;
                if (t <= 0f) End(id);
                else _remaining[id] = t;
            }
        }

        private void Apply()
        {
            _boost?.SetBoost(BoostBonus);
            _score.Multiplier = TeaActive ? _s.teaScoreMultiplier : 1f;
        }
    }
}
