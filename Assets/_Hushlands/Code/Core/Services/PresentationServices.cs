using System;

namespace Hushlands.Core
{
    /// <summary>Camera state logic, engine-free. A Cinemachine adapter (T4) listens to StateChanged.</summary>
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
        public event Action<CameraState, CameraState> StateChanged;
        public event Action<BiomeId> BiomeChanged;

        /// <summary>Within one step the highest-priority request wins; call Commit once per step.</summary>
        public void Request(CameraState state)
        {
            if (_pending == null || _s.Get(state).priority > _s.Get(_pending.Value).priority) _pending = state;
        }

        public void Commit()
        {
            if (_pending == null) return;
            CameraState next = _pending.Value;
            _pending = null;
            if (next == Current) return;
            CameraState prev = Current;
            Current = next;
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
            CameraStateProfile p = _s.Get(Current);
            BiomeCameraModifier m = _s.Get(Biome);
            p.fieldOfView += m.fovOffset;
            p.distance *= m.distanceScale;
            p.height += m.heightOffset;
            return p;
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
