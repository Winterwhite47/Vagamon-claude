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
            var pilot = new BotPilot(stretch, _fair.botTakeoffSeconds);
            IReadOnlyList<Span> hazards = pilot.Hazards;
            List<ChunkFeature> kinds = pilot.Kinds;
            double foot = _fair.riderFootprintMeters;

            string failure = null;
            rider.Crashed += cause => failure = "rider crash " + cause;

            double maxSteps = Math.Ceiling(slope.Length / _speed.minSpeed / _rider.physicsStep) * 2d;
            int steps = 0;
            MinSpeedSeen = float.MaxValue;
            MaxSpeedSeen = 0f;

            while (failure == null && rider.Position.Distance < slope.Length && steps < maxSteps)
            {
                bool grounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                pilot.Drive(input, rider.Position.Distance, rider.Speed, grounded);
                rider.FixedStep(input, _rider.physicsStep);
                steps++;
                if (rider.Speed < MinSpeedSeen) MinSpeedSeen = rider.Speed;
                if (rider.Speed > MaxSpeedSeen) MaxSpeedSeen = rider.Speed;

                double nd = rider.Position.Distance;
                bool nowGrounded = rider.State == RiderState.Grounded || rider.State == RiderState.Tucking;
                for (int i = Math.Max(0, pilot.NextIndex - 1); i < hazards.Count && hazards[i].Start - foot <= nd; i++)
                {
                    if (kinds[i].kind == FeatureKind.Obstacle)
                    {
                        if (!hazards[i].Expand(foot).Contains(nd)) continue;
                        double above = rider.Position.Height - slope.HeightAt(nd);
                        if (above < kinds[i].height) failure = "hit obstacle at " + Fmt(nd);
                    }
                    else if (nowGrounded && hazards[i].Contains(nd))
                    {
                        failure = "grounded over " + kinds[i].kind + " at " + Fmt(nd);
                    }
                }
            }
            JumpsTaken = pilot.JumpsTaken;

            if (failure == null && rider.Position.Distance < slope.Length) failure = "timeout";
            bool ok = failure == null;
            report = "seed=" + seed + " chunks=" + stretch.Count + " length=" + Fmt(slope.Length) +
                     " jumps=" + JumpsTaken + "/" + hazards.Count + " steps=" + steps +
                     " speed=" + Fmt(MinSpeedSeen) + ".." + Fmt(MaxSpeedSeen) + (ok ? " ok" : " FAIL " + failure);
            return ok;
        }

        private static string Fmt(double x)
        {
            return x.ToString("F1", CultureInfo.InvariantCulture);
        }
    }
}
