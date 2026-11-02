using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Scene glue for a run: rider physics step, then flow (checkpoints, failure hold, retry, assists).
    /// The camera ticks itself in LateUpdate. UI listens to Flow.MessageShown / PhaseChanged (T13).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RunDirector : MonoBehaviour
    {
        private PhysxRider _rider;
        private IInputSource _input;
        private RunFlow _flow;

        public RunFlow Flow => _flow;
        public DucklingNestService Ducklings { get; private set; }

        public void Initialize(HushConfigSet config, PhysxRider rider, IInputSource input, IReadOnlyList<IChunk> stretch,
            ISlopeSpline slope, CameraDirector camera, RiderCameraSignals signals, long seed)
        {
            _rider = rider;
            _input = input;
            Ducklings = new DucklingNestService();
            _flow = new RunFlow(config.failure.Settings, config.assist.Settings, config.messages.Settings, seed,
                rider, rider, new SpeedModel(config.speed.Settings), stretch, slope, Ducklings,
                camera != null ? camera.Core : null, signals);
        }

        private void FixedUpdate()
        {
            if (_flow == null) return;
            _flow.Tick(_input, Time.fixedDeltaTime);
        }
    }
}
