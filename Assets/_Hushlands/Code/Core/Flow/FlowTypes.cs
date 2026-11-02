using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Riders that accept assist effects (landing tolerance, cruise speed scale).</summary>
    public interface IAssistable
    {
        void SetLandingBonus(float degrees);
        void SetSpeedScale(float scale);
    }

    /// <summary>Camera hooks the flow needs (implemented by CameraDirectorCore).</summary>
    public interface IFlowCamera
    {
        void SnapToRider();
    }

    public enum FlowPhase
    {
        Riding = 0,
        FailureHold = 1,
        Retrying = 2,
    }

    public readonly struct FlowMessage
    {
        public readonly string Text;
        public readonly string Hint;
        public readonly bool AssistOffered;

        public FlowMessage(string text, string hint, bool assistOffered)
        {
            Text = text;
            Hint = hint;
            AssistOffered = assistOffered;
        }
    }

    /// <summary>Picks a line per cause, never the same line twice in a row for that cause.</summary>
    public sealed class MessagePicker
    {
        private readonly MessageSettings _s;
        private readonly Rng _rng;
        private readonly Dictionary<CrashCause, int> _last = new Dictionary<CrashCause, int>();
        private int _lastAssist = -1;

        public MessagePicker(MessageSettings settings, long seed)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _rng = new Rng(seed);
        }

        public string Pick(CrashCause cause)
        {
            string[] lines = _s.For(cause);
            int last = _last.TryGetValue(cause, out int l) ? l : -1;
            int i = PickIndex(lines.Length, last);
            _last[cause] = i;
            return lines[i];
        }

        public string PickAssist()
        {
            int i = PickIndex(_s.assistOffer.Length, _lastAssist);
            _lastAssist = i;
            return _s.assistOffer[i];
        }

        private int PickIndex(int count, int last)
        {
            if (count <= 1) return 0;
            int i = _rng.RangeInclusive(0, count - 2);
            return i >= last && last >= 0 ? i + 1 : i;
        }
    }

    /// <summary>Ordered checkpoints of a stretch; reports the latest one the rider has passed.</summary>
    public sealed class CheckpointTracker
    {
        private readonly List<double> _points = new List<double>();
        private readonly ISlopeSpline _slope;
        private int _next;

        public CheckpointTracker(IReadOnlyList<IChunk> stretch, ISlopeSpline slope)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.Checkpoint) _points.Add(c.StartDistance + f.start);
                }
            }
            _points.Sort();
        }

        public IReadOnlyList<double> Points => _points;

        public SlopePoint PointAt(int index)
        {
            double d = _points[index];
            return new SlopePoint(d, _slope.HeightAt(d));
        }

        /// <summary>Index of the last checkpoint at or before <paramref name="distance"/>; -1 before the first.</summary>
        public int IndexBefore(double distance)
        {
            int idx = -1;
            for (int i = 0; i < _points.Count && _points[i] <= distance; i++) idx = i;
            return idx;
        }

        /// <summary>Returns the newly passed checkpoint index, or -1.</summary>
        public int Update(double distance)
        {
            int reached = -1;
            while (_next < _points.Count && distance >= _points[_next])
            {
                reached = _next;
                _next++;
            }
            return reached;
        }
    }
}
