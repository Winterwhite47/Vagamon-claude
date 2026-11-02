using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    [Serializable]
    public sealed class SpeedSettings : ISettings
    {
        public float minSpeed = 6f;
        public float maxSpeed = 18f;
        public float startSpeed = 6f;
        /// <summary>Slope assist: target speed rises by rampAmount every rampIntervalSeconds.</summary>
        public float rampAmount = 0.45f;
        public float rampIntervalSeconds = 10f;
        /// <summary>How quickly speed converges to target (1/s).</summary>
        public float assistGain = 0.8f;
        public float gravity = 9.81f;
        public float baseDrag = 0.02f;
        public float tuckDragMultiplier = 0.55f;
        public float tuckTargetBonus = 1.5f;

        public float TargetAt(double runSeconds)
        {
            double steps = Math.Max(0d, runSeconds) / rampIntervalSeconds;
            double target = minSpeed + steps * rampAmount;
            return (float)Math.Min(maxSpeed, target);
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(SpeedSettings);
            SettingsCheck.Positive(e, o, nameof(minSpeed), minSpeed);
            SettingsCheck.Ordered(e, o, nameof(minSpeed), minSpeed, nameof(maxSpeed), maxSpeed);
            SettingsCheck.Range(e, o, nameof(startSpeed), startSpeed, minSpeed, maxSpeed);
            SettingsCheck.NonNegative(e, o, nameof(rampAmount), rampAmount);
            SettingsCheck.Positive(e, o, nameof(rampIntervalSeconds), rampIntervalSeconds);
            SettingsCheck.Positive(e, o, nameof(assistGain), assistGain);
            SettingsCheck.Positive(e, o, nameof(gravity), gravity);
            SettingsCheck.NonNegative(e, o, nameof(baseDrag), baseDrag);
            SettingsCheck.Range(e, o, nameof(tuckDragMultiplier), tuckDragMultiplier, 0f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(tuckTargetBonus), tuckTargetBonus);
        }
    }

    [Serializable]
    public sealed class RiderSettings : ISettings
    {
        public float physicsStep = 0.01f;
        public float mass = 70f;
        public float jumpVelocity = 6.5f;
        /// <summary>Hold longer than this on ground = tuck; shorter release = tap.</summary>
        public float holdThresholdSeconds = 0.18f;
        public float flipDegreesPerSecond = 540f;
        public float safeLandingToleranceDegrees = 35f;
        public float coyoteSeconds = 0.08f;
        public float jumpBufferSeconds = 0.1f;
        public float airGravityScale = 1f;

        // --- PhysX body (T3) ---
        /// <summary>Capsule lies along the slope (X): total length and radius.</summary>
        public float colliderLength = 1.0f;
        public float colliderRadius = 0.25f;
        public float groundProbeDistance = 0.45f;
        /// <summary>Keeps the sled on the slope over convex slope changes when not jumping.</summary>
        public float groundSnapDistance = 0.3f;
        /// <summary>Gap below the body that still counts as touching down when landing.</summary>
        public float groundContactSkin = 0.05f;
        /// <summary>Ground orientation follow rate (1/s).</summary>
        public float groundAlignSharpness = 20f;
        /// <summary>Ignore ground for this long after takeoff so the probe does not re-ground instantly.</summary>
        public float takeoffGroundIgnoreSeconds = 0.1f;

        /// <summary>Copy with jump velocity scaled by a vehicle's jumpScale.</summary>
        public RiderSettings WithJumpScale(float scale)
        {
            var copy = (RiderSettings)MemberwiseClone();
            copy.jumpVelocity *= scale;
            return copy;
        }

        public void Validate(List<string> e)
        {
            const string o = nameof(RiderSettings);
            SettingsCheck.Positive(e, o, nameof(physicsStep), physicsStep);
            SettingsCheck.Positive(e, o, nameof(mass), mass);
            SettingsCheck.Positive(e, o, nameof(jumpVelocity), jumpVelocity);
            SettingsCheck.Positive(e, o, nameof(holdThresholdSeconds), holdThresholdSeconds);
            SettingsCheck.Positive(e, o, nameof(flipDegreesPerSecond), flipDegreesPerSecond);
            SettingsCheck.Range(e, o, nameof(safeLandingToleranceDegrees), safeLandingToleranceDegrees, 0f, 90f);
            SettingsCheck.NonNegative(e, o, nameof(coyoteSeconds), coyoteSeconds);
            SettingsCheck.NonNegative(e, o, nameof(jumpBufferSeconds), jumpBufferSeconds);
            SettingsCheck.Positive(e, o, nameof(airGravityScale), airGravityScale);
            SettingsCheck.Positive(e, o, nameof(colliderLength), colliderLength);
            SettingsCheck.Positive(e, o, nameof(colliderRadius), colliderRadius);
            if (colliderRadius * 2f > colliderLength) e.Add(o + ": colliderLength must be >= 2 * colliderRadius");
            SettingsCheck.Positive(e, o, nameof(groundProbeDistance), groundProbeDistance);
            SettingsCheck.NonNegative(e, o, nameof(groundSnapDistance), groundSnapDistance);
            SettingsCheck.Positive(e, o, nameof(groundContactSkin), groundContactSkin);
            SettingsCheck.Ordered(e, o, nameof(groundSnapDistance), groundSnapDistance, nameof(groundProbeDistance), groundProbeDistance);
            SettingsCheck.Positive(e, o, nameof(groundAlignSharpness), groundAlignSharpness);
            SettingsCheck.NonNegative(e, o, nameof(takeoffGroundIgnoreSeconds), takeoffGroundIgnoreSeconds);
        }
    }

    [Serializable]
    public struct SurfaceEntry
    {
        public SurfaceId surface;
        public float friction;
        public float drag;

        public SurfaceEntry(SurfaceId surface, float friction, float drag)
        {
            this.surface = surface;
            this.friction = friction;
            this.drag = drag;
        }
    }

    [Serializable]
    public sealed class SurfaceSettings : ISettings
    {
        public SurfaceEntry[] entries =
        {
            new SurfaceEntry(SurfaceId.Grass, 0.06f, 0.020f),
            new SurfaceEntry(SurfaceId.Snow, 0.03f, 0.015f),
            new SurfaceEntry(SurfaceId.Mud, 0.12f, 0.040f),
            new SurfaceEntry(SurfaceId.PineNeedles, 0.07f, 0.025f),
            new SurfaceEntry(SurfaceId.TeaTerrace, 0.08f, 0.025f),
            new SurfaceEntry(SurfaceId.Boardwalk, 0.05f, 0.018f),
            new SurfaceEntry(SurfaceId.Ice, 0.015f, 0.010f),
            new SurfaceEntry(SurfaceId.Stone, 0.09f, 0.030f),
        };

        public void Validate(List<string> e)
        {
            const string o = nameof(SurfaceSettings);
            var keys = new List<SurfaceId>();
            foreach (SurfaceEntry s in entries)
            {
                keys.Add(s.surface);
                SettingsCheck.Range(e, o, s.surface + ".friction", s.friction, 0f, 1f);
                SettingsCheck.NonNegative(e, o, s.surface + ".drag", s.drag);
            }
            SettingsCheck.CoversEnum(e, o, keys);
        }
    }

    /// <summary>Collider geometry built from chunks for the PhysX rider.</summary>
    [Serializable]
    public sealed class TrackSettings : ISettings
    {
        public float groundThickness = 1f;
        public float trackWidth = 4f;
        public float pitDepth = 4f;
        public float obstacleWidth = 2f;
        /// <summary>Extra ground length past the stretch end so the rider never runs off the mesh.</summary>
        public float runOutMeters = 30f;

        public void Validate(List<string> e)
        {
            const string o = nameof(TrackSettings);
            SettingsCheck.Positive(e, o, nameof(groundThickness), groundThickness);
            SettingsCheck.Positive(e, o, nameof(trackWidth), trackWidth);
            SettingsCheck.Positive(e, o, nameof(pitDepth), pitDepth);
            SettingsCheck.Positive(e, o, nameof(obstacleWidth), obstacleWidth);
            SettingsCheck.NonNegative(e, o, nameof(runOutMeters), runOutMeters);
        }
    }

    [Serializable]
    public sealed class FailureSettings : ISettings
    {
        public float failureHoldSeconds = 0.9f;
        public float retryTransitionSeconds = 0.6f;
        /// <summary>Free assists offered after this many consecutive failures (canon: 3).</summary>
        public int assistAfterFailures = 3;

        public void Validate(List<string> e)
        {
            const string o = nameof(FailureSettings);
            SettingsCheck.NonNegative(e, o, nameof(failureHoldSeconds), failureHoldSeconds);
            SettingsCheck.NonNegative(e, o, nameof(retryTransitionSeconds), retryTransitionSeconds);
            SettingsCheck.Positive(e, o, nameof(assistAfterFailures), assistAfterFailures);
        }
    }

    [Serializable]
    public sealed class AssistSettings : ISettings
    {
        public float speedScale = 0.85f;
        public float extraRevealSeconds = 0.5f;
        public float landingToleranceBonusDegrees = 15f;
        /// <summary>Successful checkpoints before assists switch off again.</summary>
        public int successesToClear = 2;

        public void Validate(List<string> e)
        {
            const string o = nameof(AssistSettings);
            SettingsCheck.Range(e, o, nameof(speedScale), speedScale, 0.1f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(extraRevealSeconds), extraRevealSeconds);
            SettingsCheck.Range(e, o, nameof(landingToleranceBonusDegrees), landingToleranceBonusDegrees, 0f, 45f);
            SettingsCheck.Positive(e, o, nameof(successesToClear), successesToClear);
        }
    }

    [Serializable]
    public sealed class DucklingSettings : ISettings
    {
        public int ducklingsPerGroup = 4;
        public float nestCoverCloseSeconds = 0.25f;
        public float rescueRadius = 2.5f;

        public void Validate(List<string> e)
        {
            const string o = nameof(DucklingSettings);
            SettingsCheck.Positive(e, o, nameof(ducklingsPerGroup), ducklingsPerGroup);
            SettingsCheck.Positive(e, o, nameof(nestCoverCloseSeconds), nestCoverCloseSeconds);
            SettingsCheck.Positive(e, o, nameof(rescueRadius), rescueRadius);
        }
    }
}
