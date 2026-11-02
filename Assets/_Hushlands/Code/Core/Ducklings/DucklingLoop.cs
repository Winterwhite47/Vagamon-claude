using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>A nest on the run (absolute distance; drawn beside the track).</summary>
    public readonly struct NestSite
    {
        public readonly int Group;
        public readonly double X;

        public NestSite(int group, double x)
        {
            Group = group;
            X = x;
        }
    }

    /// <summary>
    /// Rescue loop. Riding past a nest on the ground (within rescueRadius, at most maxRescueHeight above the slope)
    /// rescues its group: ducklings hop into the covered sled basket and the score grows. Passing a nest otherwise
    /// marks it Waiting (never lost; a retry from a checkpoint before it makes it Ahead again).
    /// Crash and retry handling (covers, restore) belongs to DucklingNestService via the failure flow.
    /// </summary>
    public sealed class DucklingLoop
    {
        private readonly DucklingSettings _s;
        private readonly DucklingNestService _nests;
        private readonly ScoreService _score;
        private readonly List<NestSite> _sites = new List<NestSite>();
        private readonly MessageSettings _messages;
        private int _rescueLine = -1;

        public DucklingLoop(DucklingSettings settings, DucklingNestService nests, ScoreService score,
            IReadOnlyList<IChunk> stretch, MessageSettings messages)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _nests = nests ?? throw new ArgumentNullException(nameof(nests));
            _score = score;
            _messages = messages;
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.DucklingNest) _sites.Add(new NestSite(f.variant, c.StartDistance + f.start));
                }
            }
            _sites.Sort((a, b) => a.X.CompareTo(b.X));
        }

        public IReadOnlyList<NestSite> Sites => _sites;
        public int RescuedDucklings => _nests.Count(DucklingGroupStatus.Rescued) * _s.ducklingsPerGroup;
        public string LastRescueLine { get; private set; }
        public event Action<NestSite> Rescued;
        public event Action<NestSite> Missed;

        /// <summary>Per step while Riding and not crashed.</summary>
        public void Step(double riderX, double heightAboveSlope, bool grounded)
        {
            if (_nests.NestCoversClosed) return;
            bool low = grounded || heightAboveSlope <= _s.maxRescueHeight;
            foreach (NestSite site in _sites)
            {
                if (_nests.StatusOf(site.Group) != DucklingGroupStatus.Ahead) continue;
                double dx = riderX - site.X;
                if (Math.Abs(dx) <= _s.rescueRadius && low)
                {
                    _nests.Rescue(site.Group);
                    _score?.AddRescue();
                    if (_messages != null) LastRescueLine = PickRescueLine();
                    Rescued?.Invoke(site);
                }
                else if (dx > _s.rescueRadius)
                {
                    _nests.MarkPassed(site.Group);
                    Missed?.Invoke(site);
                }
            }
        }

        private string PickRescueLine()
        {
            string[] lines = _messages.rescue;
            if (lines.Length == 1) return lines[0];
            int i = (_rescueLine + 1) % lines.Length;
            _rescueLine = i;
            return lines[i];
        }
    }

    /// <summary>What the duckling presenter currently shows; fed to the safety audit every step.</summary>
    public struct DucklingView
    {
        public bool NestCoversVisible;
        public bool BasketLidVisible;
        /// <summary>Ducklings drawn anywhere other than inside an open nest or an open basket. Must always be zero.</summary>
        public int DrawnOutsideSafePlaces;
        public int DrawnInNests;
        public int DrawnInBasket;
    }

    /// <summary>
    /// Absolute duckling-safety rules, checked every step (and in T17 capture review):
    /// covers and basket lid shut whenever the rider is crashed or the run is in failure hold / retry;
    /// no duckling drawn outside a nest or basket; no drawn duckling while covers are shut;
    /// every group status is one of Ahead / Rescued / Waiting.
    /// </summary>
    public static class DucklingSafetyAudit
    {
        public static List<string> Check(DucklingNestService nests, FlowPhase phase, RiderState rider, DucklingView view)
        {
            var v = new List<string>();
            bool failing = phase != FlowPhase.Riding || rider == RiderState.Crashed;
            if (failing && !nests.NestCoversClosed) v.Add("covers open during failure");
            if (nests.NestCoversClosed && (!view.NestCoversVisible || !view.BasketLidVisible)) v.Add("covers not drawn while closed");
            if (nests.NestCoversClosed && view.DrawnInNests + view.DrawnInBasket > 0) v.Add("ducklings visible while covers closed");
            if (view.DrawnOutsideSafePlaces != 0) v.Add("duckling drawn outside nest or basket");
            foreach (int g in nests.Groups)
            {
                if (!Enum.IsDefined(typeof(DucklingGroupStatus), nests.StatusOf(g))) v.Add("unknown status for group " + g);
            }
            return v;
        }
    }
}
