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
        private ProgressionService _progression;
        private double _endX;

        public RunSession Session => _session;
        public ProgressionService Progression => _progression;
        public bool Finished { get; private set; }
        public RunSummary Summary { get; private set; }
        public event System.Action<RunSummary> RunEnded;
        public bool Paused { get; set; }
        public RunFlow Flow => _session?.Flow;
        public DucklingNestService Ducklings { get; private set; }

        public void Initialize(HushConfigSet config, PhysxRider rider, IInputSource input, IReadOnlyList<IChunk> stretch,
            ISlopeSpline slope, CameraDirector camera, RiderCameraSignals signals, long seed, RunSettingsBundle bundle = null)
        {
            _rider = rider;
            _input = input;
            Ducklings = new DucklingNestService();
            TrackSettings t = config.track.Settings;
            float bodyOffset = config.rider.Settings.colliderRadius + t.riderBodyHeight * 0.5f;
            _session = RunSession.Create(bundle ?? config.RunBundle(), t.pickupHover, bodyOffset, seed, rider, stretch, slope,
                Ducklings, camera != null ? camera.Core : null, signals);
            _endX = slope.Length;
        }

        /// <summary>Progression to report to when the run ends (reaching the end of the stretch, or quitting).</summary>
        public void AttachProgression(ProgressionService progression)
        {
            _progression = progression;
        }

        public void FinishRun()
        {
            if (Finished || _session == null) return;
            Finished = true;
            Summary = _session.End();
            _progression?.Complete(Summary, _session.Stats);
            RunEnded?.Invoke(Summary);
        }

        private void OnApplicationQuit()
        {
            FinishRun();
        }

        private void FixedUpdate()
        {
            if (_session == null || Finished || Paused) return;
            _session.Tick(_input, Time.fixedDeltaTime);
            if (_session.Flow.Phase == FlowPhase.Riding && _rider.Position.Distance >= _endX) FinishRun();
            else if (_progression != null) _progression.Goals.Evaluate(_session.Stats);
        }
    }
}
