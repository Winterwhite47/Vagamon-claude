using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Screen-space framing of a side-on 2.5D camera. Screen coords are normalised to [-1, 1].</summary>
    public readonly struct FramingReport
    {
        public readonly double AheadMeters;
        public readonly double RiderScreenX;
        public readonly double RiderScreenY;
        public readonly double GroundAheadScreenY;

        public FramingReport(double ahead, double riderX, double riderY, double groundY)
        {
            AheadMeters = ahead;
            RiderScreenX = riderX;
            RiderScreenY = riderY;
            GroundAheadScreenY = groundY;
        }
    }

    public static class FramingMath
    {
        /// <summary>
        /// Camera centred on (rider + lookAhead, slope + height) at <paramref name="distance"/> from the plane,
        /// on a downhill slope of <paramref name="slopeRadians"/>.
        /// </summary>
        public static FramingReport Evaluate(double verticalFovDegrees, double distance, double height, double lookAhead,
            double aspect, double slopeRadians)
        {
            double halfH = distance * Math.Tan(verticalFovDegrees * Math.PI / 360.0);
            double halfW = halfH * aspect;
            double tan = Math.Tan(slopeRadians);
            double ahead = halfW + lookAhead;
            double riderX = -lookAhead / halfW;
            double riderY = (lookAhead * tan - height) / halfH;
            double groundY = -(halfW * tan + height) / halfH;
            return new FramingReport(ahead, riderX, riderY, groundY);
        }

        /// <summary>Profile after biome modifier (and reduced-motion lock to Cruise).</summary>
        public static CameraStateProfile Resolve(CameraSettings s, CameraState state, BiomeId biome, bool reducedMotion)
        {
            CameraStateProfile p = s.Get(reducedMotion && state != CameraState.FailureHold ? CameraState.Cruise : state);
            p.state = state;
            BiomeCameraModifier m = s.Get(biome);
            p.fieldOfView += m.fovOffset;
            p.distance *= m.distanceScale;
            p.height += m.heightOffset;
            return p;
        }

        /// <summary>Every non-exempt state x biome x motion setting must meet the reveal contract.</summary>
        public static List<string> CheckContract(CameraSettings s, double visibleAheadMeters)
        {
            var errors = new List<string>();
            double slope = s.maxSlopeForFramingDegrees * Math.PI / 180.0;
            foreach (CameraState state in (CameraState[])Enum.GetValues(typeof(CameraState)))
            {
                if (s.Get(state).revealExempt) continue;
                foreach (BiomeId biome in (BiomeId[])Enum.GetValues(typeof(BiomeId)))
                {
                    for (int rm = 0; rm < 2; rm++)
                    {
                        CameraStateProfile p = Resolve(s, state, biome, rm == 1);
                        FramingReport r = Evaluate(p.fieldOfView, p.distance, p.height, p.lookAhead, s.minAspect, slope);
                        string tag = state + "/" + biome + (rm == 1 ? "/reduced" : "");
                        if (r.AheadMeters < visibleAheadMeters) errors.Add(tag + ": shows " + r.AheadMeters.ToString("F1") + " m ahead");
                        if (r.RiderScreenX < -s.maxRiderBehindCenter) errors.Add(tag + ": rider too far left");
                        if (Math.Abs(r.RiderScreenY) > s.safeScreenExtent) errors.Add(tag + ": rider off vertical safe zone");
                        if (r.GroundAheadScreenY < -s.safeScreenExtent) errors.Add(tag + ": ground ahead below frame");
                    }
                }
            }
            return errors;
        }
    }
}
