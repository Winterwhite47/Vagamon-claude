using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Scene glue for a run: flow (checkpoints, failure hold, retry, assists), pickups and score.
    /// The camera ticks itself in LateUpdate. UI listens to Flow.MessageShown / PhaseChanged (T13).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RunDirector : MonoBehaviour
    {
        private PhysxRider _rider;
        private IInputSource _input;
        private RunSession _session;

        public RunSession Session => _session;
        public RunFlow Flow => _session?.Flow;
        public DucklingNestService Ducklings { get; private set; }

        public void Initialize(HushConfigSet config, PhysxRider rider, IInputSource input, IReadOnlyList<IChunk> stretch,
            ISlopeSpline slope, CameraDirector camera, RiderCameraSignals signals, long seed)
        {
            _rider = rider;
            _input = input;
            Ducklings = new DucklingNestService();
            TrackSettings t = config.track.Settings;
            float bodyOffset = config.rider.Settings.colliderRadius + t.riderBodyHeight * 0.5f;
            _session = RunSession.Create(config.RunBundle(), t.pickupHover, bodyOffset, seed, rider, stretch, slope,
                Ducklings, camera != null ? camera.Core : null, signals);
        }

        private void FixedUpdate()
        {
            if (_session == null) return;
            _session.Tick(_input, Time.fixedDeltaTime);
        }
    }
}
