using System;
using Hushlands.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Cinemachine 3 adapter for CameraDirectorCore: one procedural CinemachineCamera whose transform and lens
    /// follow the core pose every LateUpdate (after rider interpolation). The CinemachineBrain renders it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraDirector : MonoBehaviour, ICameraDirector
    {
        [SerializeField] private CinemachineCamera vcam;
        [SerializeField] private bool autoTick = true;

        private CameraDirectorCore _core;
        private Transform _rider;

        public CameraDirectorCore Core => _core;
        public CinemachineCamera VirtualCamera => vcam;
        public CameraState Current => _core != null ? _core.Current : CameraState.Cruise;

        public event Action<CameraState, CameraState> StateChanged;

        public void AssignCamera(CinemachineCamera cam)
        {
            vcam = cam;
        }

        public void Initialize(CameraSettings settings, ISlopeSpline slope, Transform rider, ICameraSignalSource signals,
            IAudioMixDirector mix, BiomeId biome, bool reducedMotion, bool tickAutomatically)
        {
            if (vcam == null) throw new InvalidOperationException("CameraDirector needs a CinemachineCamera");
            _rider = rider ? rider : throw new ArgumentNullException(nameof(rider));
            if (_core != null) _core.StateChanged -= Forward;
            _core = new CameraDirectorCore(settings, slope, signals, mix, biome, reducedMotion);
            _core.StateChanged += Forward;
            autoTick = tickAutomatically;
        }

        public void Request(CameraState state)
        {
            _core?.Request(state);
        }

        public void SetBiome(BiomeId biome)
        {
            _core?.SetBiome(biome);
        }

        public void SetReducedMotion(bool on)
        {
            _core?.SetReducedMotion(on);
        }

        public void SnapToRider()
        {
            _core?.SnapToRider();
        }

        private void LateUpdate()
        {
            if (autoTick && _core != null) Tick(Time.deltaTime);
        }

        public CameraPose Tick(float dt)
        {
            CameraPose pose = _core.Tick(_rider.position.x, dt);
            vcam.transform.SetPositionAndRotation(new Vector3((float)pose.X, (float)pose.Y, (float)pose.Z),
                Quaternion.Euler(pose.PitchDegrees, 0f, 0f));
            vcam.Lens.FieldOfView = pose.FieldOfView;
            return pose;
        }

        private void Forward(CameraState prev, CameraState next)
        {
            StateChanged?.Invoke(prev, next);
        }

        private void OnDestroy()
        {
            if (_core != null) _core.StateChanged -= Forward;
        }
    }
}
