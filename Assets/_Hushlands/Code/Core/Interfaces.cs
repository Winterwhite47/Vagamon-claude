// Hushlands canonical interfaces (see docs/game-bible.md, section "Interfaces").
// All tunable numbers are supplied by config objects; implementations never hard-code them.
using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    // ---------- Input ----------

    /// <summary>Single-lane input: tap and hold only.</summary>
    public interface IInputSource
    {
        bool TapThisStep { get; }
        bool IsHeld { get; }
        float HeldSeconds { get; }
    }

    // ---------- Slope and physics ----------

    public interface ISlopeSpline
    {
        double Length { get; }
        /// <summary>Slope surface height at a distance along the spline.</summary>
        float HeightAt(double distance);
        /// <summary>Slope angle in radians (positive = downhill).</summary>
        float AngleAt(double distance);
        SurfaceId SurfaceAt(double distance);
    }

    public interface ISurfaceProvider
    {
        float FrictionFor(SurfaceId surface);
        float DragFor(SurfaceId surface);
    }

    public interface ISpeedModel
    {
        float MinSpeed { get; }
        float MaxSpeed { get; }
        /// <summary>Target cruise speed after a given run time, including config slope assist.</summary>
        float TargetSpeed(double runSeconds);
        float Step(float currentSpeed, float slopeAngle, float friction, bool tucking, double runSeconds, float dt);
    }

    public interface IRider
    {
        RiderState State { get; }
        SlopePoint Position { get; }
        float Speed { get; }
        float FlipAngle { get; }
        int CompletedFlips { get; }
        event Action<RiderState, RiderState> StateChanged;
        event Action<CrashCause> Crashed;
        void FixedStep(IInputSource input, float dt);
        void ResetTo(SlopePoint point, float speed);
    }

    // ---------- Content ----------

    public interface IChunk
    {
        string Id { get; }
        BiomeId Biome { get; }
        double Length { get; }
        int Difficulty { get; }
        /// <summary>Distance of chunk start along the stretch.</summary>
        double StartDistance { get; }
        /// <summary>Constant downhill slope of the chunk.</summary>
        float SlopeRadians { get; }
        /// <summary>Features, positions relative to chunk start.</summary>
        IReadOnlyList<ChunkFeature> Features { get; }
        IReadOnlyList<SurfaceSpan> Surfaces { get; }
    }

    public interface IChunkGenerator
    {
        /// <summary>Deterministic stretch of chunks for a seed.</summary>
        IReadOnlyList<IChunk> GenerateStretch(int seed, BiomeId biome, int chunkCount);
    }

    public readonly struct FairnessViolation
    {
        public readonly string ChunkId;
        public readonly double Distance;
        public readonly string Rule;

        public FairnessViolation(string chunkId, double distance, string rule)
        {
            ChunkId = chunkId;
            Distance = distance;
            Rule = rule;
        }
    }

    public interface IFairnessChecker
    {
        /// <summary>Checks reveal time, mist-hidden jumps, collapse warning, safe landings, locked-upgrade needs.</summary>
        IReadOnlyList<FairnessViolation> Check(IReadOnlyList<IChunk> stretch);
    }

    public interface IBotValidator
    {
        /// <summary>Returns true if a reference bot clears the stretch with base vehicle and no upgrades.</summary>
        bool Validate(IReadOnlyList<IChunk> stretch, int seed, out string report);
    }

    // ---------- Camera and audio ----------

    public interface ICameraDirector
    {
        CameraState Current { get; }
        event Action<CameraState, CameraState> StateChanged;
        void Request(CameraState state);
        void SetBiome(BiomeId biome);
    }

    public interface IAudioMixDirector
    {
        void ApplySnapshot(CameraState state, BiomeId biome);
        /// <summary>0 = clear, 1 = densest mist; drives the mist low-pass.</summary>
        void SetMistDensity(float normalized);
    }

    // ---------- Failure, checkpoints, assists ----------

    public interface ICheckpointService
    {
        int LastCheckpointIndex { get; }
        SlopePoint LastCheckpointPoint { get; }
        void Reach(int index, SlopePoint point);
    }

    public interface IFailureService
    {
        int ConsecutiveFailures { get; }
        event Action<CrashCause> RunFailed;
        void ReportCrash(CrashCause cause);
        void InstantRetry();
    }

    public interface IAssistService
    {
        bool AssistsOffered { get; }
        bool AssistsActive { get; }
        void OnFailure(int consecutiveFailures);
        void OnSuccess();
    }

    // ---------- Ducklings ----------

    public interface IDucklingNestService
    {
        DucklingGroupStatus StatusOf(int groupId);
        IReadOnlyList<int> Groups { get; }
        void Rescue(int groupId);
        /// <summary>On crash: close nest covers and the basket lid at once; rescued groups stay rescued.</summary>
        void OnCrash();
        /// <summary>Groups at or after the checkpoint and not rescued become Ahead; earlier missed groups keep Waiting.</summary>
        void RestoreToCheckpoint(int checkpointIndex);
        /// <summary>Reopen covers once riding resumes.</summary>
        void OpenCovers();
    }

    // ---------- Pickups, score, progression, save ----------

    public interface IPickupEffect
    {
        PickupId Id { get; }
        float DurationSeconds { get; }
        void Apply();
        void Expire();
    }

    public interface IScoreService
    {
        long Score { get; }
        int SpiceCoins { get; }
        void Add(long points);
        void AddCoins(int coins);
    }

    public interface ISaveService
    {
        void Save<T>(string key, T value);
        bool TryLoad<T>(string key, out T value);
    }

    // ---------- Performance ----------

    public interface IFrameTimeProbe
    {
        int SampleCount { get; }
        double AverageMs { get; }
        double Percentile(double p);
        void Record(double frameMs);
    }
}
