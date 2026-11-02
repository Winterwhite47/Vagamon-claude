using System;

namespace Hushlands.Core
{
    /// <summary>
    /// Camera state logic, engine-free. Per step: Request() any number of states, then Commit(dt).
    /// The highest priority request wins; a higher-priority state takes over at once, a lower one only after
    /// minDwellSeconds in the current state (no flicker). A Cinemachine adapter listens to StateChanged.
    /// </summary>
    public sealed class CameraStateMachine : ICameraDirector
    {
        private readonly CameraSettings _s;
        private CameraState? _pending;

        public CameraStateMachine(CameraSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public CameraState Current { get; private set; } = CameraState.Cruise;
        public BiomeId Biome { get; private set; } = BiomeId.WindmouthRidge;
        public float TimeInState { get; private set; }
        public bool ReducedMotion { get; set; }
        public event Action<CameraState, CameraState> StateChanged;
        public event Action<BiomeId> BiomeChanged;

        public void Request(CameraState state)
        {
            if (_pending == null || _s.Get(state).priority > _s.Get(_pending.Value).priority) _pending = state;
        }

        /// <summary>Commit ignoring dwell (tests, scripted moments).</summary>
        public void Commit()
        {
            Commit(float.PositiveInfinity);
        }

        public void Commit(float dt)
        {
            TimeInState += dt;
            if (_pending == null) return;
            CameraState next = _pending.Value;
            _pending = null;
            if (next == Current) return;
            bool escalate = _s.Get(next).priority > _s.Get(Current).priority;
            if (!escalate && TimeInState < _s.minDwellSeconds) return;
            CameraState prev = Current;
            Current = next;
            TimeInState = 0f;
            StateChanged?.Invoke(prev, next);
        }

        public void SetBiome(BiomeId biome)
        {
            if (biome == Biome) return;
            Biome = biome;
            BiomeChanged?.Invoke(biome);
        }

        public CameraStateProfile ResolvedProfile()
        {
            return FramingMath.Resolve(_s, Current, Biome, ReducedMotion);
        }
    }

    /// <summary>Records the mix request; the AudioMixer adapter (T7/T16) applies it.</summary>
    public sealed class MixDirector : IAudioMixDirector
    {
        private readonly AudioMixSettings _s;

        public MixDirector(AudioMixSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public MixSnapshotEntry CurrentSnapshot { get; private set; }
        public BiomeId CurrentBiome { get; private set; }
        public float LowPassCutoffHz { get; private set; }
        public event Action<MixSnapshotEntry, BiomeId> SnapshotChanged;

        public void ApplySnapshot(CameraState state, BiomeId biome)
        {
            CurrentSnapshot = _s.Get(state);
            CurrentBiome = biome;
            SnapshotChanged?.Invoke(CurrentSnapshot, biome);
        }

        public void SetMistDensity(float normalized)
        {
            LowPassCutoffHz = _s.CutoffFor(normalized);
        }
    }
}
