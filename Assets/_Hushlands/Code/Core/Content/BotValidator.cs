using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hushlands.Core
{
    /// <summary>
    /// Reference bot: default vehicle, no upgrades, no assists. Rides the stretch with KinematicRider,
    /// taps botTakeoffSeconds before each required jump, and fails on any collision, pit or bad landing.
    /// </summary>
    public sealed class BotValidator : IBotValidator
    {
        private readonly RiderSettings _rider;
        private readonly SpeedSettings _speed;
        private readonly SurfaceSettings _surfaces;
        private readonly FairnessSettings _fair;

        public BotValidator(RiderSettings rider, SpeedSettings speed, SurfaceSettings surfaces,
            FairnessSettings fairness, VehicleSettings vehicles)
        {
            if (rider == null) throw new ArgumentNullException(nameof(rider));
            if (vehicles == null) throw new ArgumentNullException(nameof(vehicles));
            _rider = rider.WithJumpScale(vehicles.Get(vehicles.defaultVehicle).jumpScale);
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));
            _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _fair = fairness ?? throw new ArgumentNullException(nameof(fairness));
        }

        public float MinSpeedSeen { get; private set; }
        public float MaxSpeedSeen { get; private set; }
        public int JumpsTaken { get; private set; }

        public bool Validate(IReadOnlyList<IChunk> stretch, int seed, out string report)
        {
            if (stretch == null || stretch.Count == 0)
            {
                report = "seed=" + seed + " empty stretch";
                return false;
            }

            var slope = new StretchSlope(stretch);
            var rider = new KinematicRider(_rider, _speed, slope, new SurfaceTable(_surfaces));
            var input = new ScriptedInput();
            List<Span> hazards = new List<Span>();
            List<ChunkFeature> kinds = new List<ChunkFeature>();
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (!f.IsRequiredJump) continue;
                    hazards.Add(new Span(c.StartDistance + f.start, c.StartDistance + f.End));
                    kinds.Add(f);
                }
            }
            SortTogether(hazards, kinds);

            string failure = null;
            rider.Crashed += cause => failure = "rider crash " + cause;

            double maxSteps = Math.Ceiling(slope.Length / _speed.minSpeed / _rider.physicsStep) * 2d;
            int next = 0;
            int steps = 0;
            int jumpedFor = -1;
            MinSpeedSeen = float.MaxValue;
            MaxSpeedSeen = 0f;
            JumpsTaken = 0;

            while (failure == null && rider.Position.Distance < slope.Length && steps < maxSteps)
            {
                double d = rider.Position.Distance;
                while (next < hazards.Count && d > hazards[next].End) next++;

                bool grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                input.TapThisStep = false;
                if (grounded && next < hazards.Count && jumpedFor != next &&
                    hazards[next].Start - d <= rider.Speed * _fair.botTakeoffSeconds)
                {
                    input.TapThisStep = true;
                    jumpedFor = next;
                    JumpsTaken++;
                }

                rider.FixedStep(input, _rider.physicsStep);
                steps++;
                if (rider.Speed < MinSpeedSeen) MinSpeedSeen = rider.Speed;
                if (rider.Speed > MaxSpeedSeen) MaxSpeedSeen = rider.Speed;

                double nd = rider.Position.Distance;
                bool nowGrounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                for (int i = next; i < hazards.Count && hazards[i].Start <= nd; i++)
                {
                    if (!hazards[i].Contains(nd)) continue;
                    if (kinds[i].kind == FeatureKind.Obstacle)
                    {
                        double above = rider.Position.Height - slope.HeightAt(nd);
                        if (above < kinds[i].height) failure = "hit obstacle at " + Fmt(nd);
                    }
                    else if (nowGrounded)
                    {
                        failure = "grounded over " + kinds[i].kind + " at " + Fmt(nd);
                    }
                }
            }

            if (failure == null && rider.Position.Distance < slope.Length) failure = "timeout";
            bool ok = failure == null;
            report = "seed=" + seed + " chunks=" + stretch.Count + " length=" + Fmt(slope.Length) +
                     " jumps=" + JumpsTaken + "/" + hazards.Count + " steps=" + steps +
                     " speed=" + Fmt(MinSpeedSeen) + ".." + Fmt(MaxSpeedSeen) + (ok ? " ok" : " FAIL " + failure);
            return ok;
        }

        private static void SortTogether(List<Span> spans, List<ChunkFeature> features)
        {
            var idx = new List<int>();
            for (int i = 0; i < spans.Count; i++) idx.Add(i);
            idx.Sort((a, b) => spans[a].Start.CompareTo(spans[b].Start));
            var s2 = new List<Span>();
            var f2 = new List<ChunkFeature>();
            foreach (int i in idx)
            {
                s2.Add(spans[i]);
                f2.Add(features[i]);
            }
            spans.Clear();
            spans.AddRange(s2);
            features.Clear();
            features.AddRange(f2);
        }

        private static string Fmt(double x)
        {
            return x.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
