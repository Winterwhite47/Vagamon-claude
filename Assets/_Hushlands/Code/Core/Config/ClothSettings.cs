using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    public enum ScarfMethod
    {
        Verlet = 0,
        UnityCloth = 1,
    }

    /// <summary>Scarf (verlet or Unity Cloth), sled flex, and the benchmark that chooses between them (T6).</summary>
    [Serializable]
    public sealed class ClothSettings : ISettings
    {
        /// <summary>Chosen by the benchmark (ScarfMethodSelector); Verlet until measured.</summary>
        public ScarfMethod method = ScarfMethod.Verlet;

        // --- Verlet scarf ---
        public int scarfSegments = 10;
        public float scarfLength = 1.2f;
        public float scarfWidth = 0.12f;
        public float maxSubstepSeconds = 1f / 120f;
        public int maxSubsteps = 4;
        public int constraintIterations = 6;
        /// <summary>Velocity kept per substep (1 = none lost).</summary>
        public float damping = 0.985f;
        public float gravityScale = 1f;
        /// <summary>Relative-air drag coefficient (1/s).</summary>
        public float airDrag = 2.5f;
        public float windX = -0.6f;
        public float windY = 0f;
        public float windZ = 0.4f;
        /// <summary>Rider body sphere the scarf may not enter.</summary>
        public float bodyRadius = 0.3f;
        /// <summary>Fraction of anchor motion carried by every particle (like Cloth world velocity scale); drag acts on true world velocity.</summary>
        public float anchorCarry = 0.85f;
        /// <summary>Quality bar: worst segment stretch allowed during play.</summary>
        public float maxStretchRatio = 0.06f;

        // --- Unity Cloth alternative ---
        public float clothStretchingStiffness = 0.9f;
        public float clothBendingStiffness = 0.5f;
        public float clothDamping = 0.1f;
        public float clothWorldVelocityScale = 0.5f;
        public float clothWorldAccelerationScale = 1f;

        // --- Sled flex (runners bend on landing, spring back) ---
        public float flexStiffness = 220f;
        public float flexDamping = 14f;
        /// <summary>Bend velocity (deg/s) added per m/s of landing vertical speed.</summary>
        public float flexImpulsePerMps = 9f;
        public float flexMaxDegrees = 6f;

        // --- Benchmark / selection ---
        public float perScarfBudgetMs = 0.05f;
        /// <summary>Costs within this ratio count as a tie; ties go to Verlet (deterministic, no worker threads).</summary>
        public float tieToleranceRatio = 0.1f;
        public int benchmarkScarves = 32;
        public int benchmarkWarmupFrames = 60;
        public int benchmarkSampleFrames = 240;

        public void Validate(List<string> e)
        {
            const string o = nameof(ClothSettings);
            if (scarfSegments < 2) e.Add(o + ".scarfSegments must be >= 2");
            SettingsCheck.Positive(e, o, nameof(scarfLength), scarfLength);
            SettingsCheck.Positive(e, o, nameof(scarfWidth), scarfWidth);
            SettingsCheck.Positive(e, o, nameof(maxSubstepSeconds), maxSubstepSeconds);
            SettingsCheck.Positive(e, o, nameof(maxSubsteps), maxSubsteps);
            SettingsCheck.Positive(e, o, nameof(constraintIterations), constraintIterations);
            SettingsCheck.Range(e, o, nameof(damping), damping, 0.5f, 1f);
            SettingsCheck.NonNegative(e, o, nameof(gravityScale), gravityScale);
            SettingsCheck.NonNegative(e, o, nameof(airDrag), airDrag);
            SettingsCheck.NonNegative(e, o, nameof(bodyRadius), bodyRadius);
            SettingsCheck.Range(e, o, nameof(anchorCarry), anchorCarry, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(maxStretchRatio), maxStretchRatio, 0f, 0.5f);
            SettingsCheck.Range(e, o, nameof(clothStretchingStiffness), clothStretchingStiffness, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(clothBendingStiffness), clothBendingStiffness, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(clothDamping), clothDamping, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(clothWorldVelocityScale), clothWorldVelocityScale, 0f, 1f);
            SettingsCheck.Range(e, o, nameof(clothWorldAccelerationScale), clothWorldAccelerationScale, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(flexStiffness), flexStiffness);
            SettingsCheck.Positive(e, o, nameof(flexDamping), flexDamping);
            SettingsCheck.NonNegative(e, o, nameof(flexImpulsePerMps), flexImpulsePerMps);
            SettingsCheck.Positive(e, o, nameof(flexMaxDegrees), flexMaxDegrees);
            SettingsCheck.Positive(e, o, nameof(perScarfBudgetMs), perScarfBudgetMs);
            SettingsCheck.Range(e, o, nameof(tieToleranceRatio), tieToleranceRatio, 0f, 1f);
            SettingsCheck.Positive(e, o, nameof(benchmarkScarves), benchmarkScarves);
            SettingsCheck.NonNegative(e, o, nameof(benchmarkWarmupFrames), benchmarkWarmupFrames);
            SettingsCheck.Positive(e, o, nameof(benchmarkSampleFrames), benchmarkSampleFrames);
        }
    }
}
