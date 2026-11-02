using System;

namespace Hushlands.Core
{
    /// <summary>
    /// Engine-free camera director: signals -> rules -> state machine (priority + dwell) -> rig (blended pose).
    /// Audio mix snapshots follow camera state and biome (canon). The Cinemachine adapter only applies the pose.
    /// </summary>
    public sealed class CameraDirectorCore : ICameraDirector, IFlowCamera
    {
        private readonly CameraSettings _s;
        private readonly CameraStateMachine _sm;
        private readonly CameraRig _rig;
        private readonly ICameraSignalSource _signals;
        private readonly IAudioMixDirector _mix;
        private readonly ISlopeSpline _slope;

        public CameraDirectorCore(CameraSettings settings, ISlopeSpline slope, ICameraSignalSource signals,
            IAudioMixDirector mix, BiomeId biome, bool reducedMotion)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _signals = signals ?? throw new ArgumentNullException(nameof(signals));
            _mix = mix;
            _sm = new CameraStateMachine(settings) { ReducedMotion = reducedMotion };
            _sm.SetBiome(biome);
            _rig = new CameraRig(settings, _sm.ResolvedProfile());
            _sm.StateChanged += OnStateChanged;
            _sm.BiomeChanged += OnBiomeChanged;
            _mix?.ApplySnapshot(_sm.Current, _sm.Biome);
        }

        public CameraState Current => _sm.Current;
        public BiomeId Biome => _sm.Biome;
        public bool ReducedMotion => _sm.ReducedMotion;
        public CameraRig Rig => _rig;
        public CameraPose LastPose { get; private set; }
        public event Action<CameraState, CameraState> StateChanged;

        public void Request(CameraState state)
        {
            _sm.Request(state);
        }

        public void SetBiome(BiomeId biome)
        {
            _sm.SetBiome(biome);
        }

        public void SetReducedMotion(bool on)
        {
            if (_sm.ReducedMotion == on) return;
            _sm.ReducedMotion = on;
            _rig.SetTarget(_sm.ResolvedProfile(), _s.Get(_sm.Current).blendInSeconds);
        }

        public void SnapToRider()
        {
            _rig.Snap();
        }

        public CameraPose Tick(double riderX, float dt)
        {
            CameraStateRules.Apply(_sm, _s, _signals.Read(dt));
            _sm.Commit(dt);
            LastPose = _rig.Update(riderX, _slope, dt, _sm.ReducedMotion);
            return LastPose;
        }

        private void OnStateChanged(CameraState prev, CameraState next)
        {
            _rig.SetTarget(_sm.ResolvedProfile(), _s.Get(next).blendInSeconds);
            _mix?.ApplySnapshot(next, _sm.Biome);
            StateChanged?.Invoke(prev, next);
        }

        private void OnBiomeChanged(BiomeId biome)
        {
            _rig.SetTarget(_sm.ResolvedProfile(), _s.biomeBlendSeconds);
            _mix?.ApplySnapshot(_sm.Current, biome);
        }
    }
}
