using System;

namespace Hushlands.Core
{
    /// <summary>
    /// Analytic jump on a constant downhill slope, matching KinematicRider:
    /// height above slope h(t) = (vy + v*tan(theta))*t - g*t^2/2, horizontal speed constant in air.
    /// </summary>
    public readonly struct JumpModel
    {
        public readonly double JumpVelocity;
        public readonly double Gravity;
        public readonly double MinSpeed;
        public readonly double MaxSpeed;

        public JumpModel(double jumpVelocity, double gravity, double minSpeed, double maxSpeed)
        {
            JumpVelocity = jumpVelocity;
            Gravity = gravity;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
        }

        /// <summary>Default-vehicle jump (canon: never require locked upgrades).</summary>
        public static JumpModel ForDefaultVehicle(RiderSettings rider, SpeedSettings speed, VehicleSettings vehicles)
        {
            float scale = vehicles.Get(vehicles.defaultVehicle).jumpScale;
            return new JumpModel(rider.jumpVelocity * scale, speed.gravity * rider.airGravityScale, speed.minSpeed, speed.maxSpeed);
        }

        public double RiseRate(double speed, double tanSlope)
        {
            return JumpVelocity + speed * tanSlope;
        }

        public double HeightAt(double t, double speed, double tanSlope)
        {
            return RiseRate(speed, tanSlope) * t - 0.5 * Gravity * t * t;
        }

        public double AirTime(double speed, double tanSlope)
        {
            return 2.0 * RiseRate(speed, tanSlope) / Gravity;
        }

        /// <summary>Distance from takeoff to touchdown.</summary>
        public double JumpDistance(double speed, double tanSlope)
        {
            return speed * AirTime(speed, tanSlope);
        }

        public double SpeedSample(int i, int count)
        {
            if (count < 2) return MinSpeed;
            return MinSpeed + (MaxSpeed - MinSpeed) * i / (count - 1);
        }

        /// <summary>
        /// True if taking off <paramref name="lead"/> seconds before the hazard at <paramref name="speed"/>
        /// clears it: while any part of the rider body (half-length <paramref name="footprint"/>) is over the span,
        /// height above slope exceeds the obstacle (zero for gaps), and touchdown comes landingMargin after it.
        /// </summary>
        public bool Clears(ChunkFeature hazard, double speed, double tanSlope, double lead, double clearance,
            double landingMargin, double footprint)
        {
            double tEnter = Math.Max(0d, lead - footprint / speed);
            double tExit = lead + (hazard.length + footprint) / speed;
            if (tExit + landingMargin > AirTime(speed, tanSlope)) return false;
            if (hazard.kind != FeatureKind.Obstacle) return true;
            double need = hazard.height + clearance;
            // Concave parabola: minimum over [tEnter, tExit] is at an endpoint.
            return HeightAt(tEnter, speed, tanSlope) >= need && HeightAt(tExit, speed, tanSlope) >= need;
        }

        public bool ClearsAcrossWindow(ChunkFeature hazard, double tanSlope, FairnessSettings f)
        {
            for (int i = 0; i < f.speedSamples; i++)
            {
                double v = SpeedSample(i, f.speedSamples);
                if (!Clears(hazard, v, tanSlope, f.takeoffWindowMinSeconds, f.obstacleClearanceMeters,
                        f.landingMarginSeconds, f.riderFootprintMeters)) return false;
                if (!Clears(hazard, v, tanSlope, f.takeoffWindowMaxSeconds, f.obstacleClearanceMeters,
                        f.landingMarginSeconds, f.riderFootprintMeters)) return false;
            }
            return true;
        }

        /// <summary>Furthest touchdown from hazard start: max speed, shortest lead (latest takeoff).</summary>
        public double FarthestLanding(double tanSlope, double leadMin)
        {
            double v = MaxSpeed;
            return v * (AirTime(v, tanSlope) - leadMin);
        }

        /// <summary>Earliest takeoff point measured back from hazard start.</summary>
        public double TakeoffZoneLength(double leadMax)
        {
            return MaxSpeed * leadMax;
        }

        public static double Tan(float slopeRadians)
        {
            return Math.Tan(slopeRadians);
        }
    }
}
