using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>
    /// Crash -> FailureHold (calm message; a tap may skip after minHoldBeforeSkipSeconds) -> Retrying (mist wipe; the
    /// rider moves to the last checkpoint at retryTeleportFraction, ducklings restored) -> Riding.
    /// Free assists switch on after assistAfterFailures consecutive failures and clear after successful checkpoints.
    /// </summary>
    public sealed class RunFlow
    {
        private readonly FailureSettings _f;
        private readonly AssistSettings _a;
        private readonly MessageSettings _m;
        private readonly MessagePicker _picker;
        private readonly IRider _rider;
        private readonly IAssistable _assistable;
        private readonly CheckpointService _checkpoints;
        private readonly CheckpointTracker _tracker;
        private readonly DucklingNestService _ducklings;
        private readonly AssistService _assists;
        private readonly FailureService _failure;
        private readonly IFlowCamera _camera;
        private readonly RiderCameraSignals _signals;
        private bool _teleported;

        public RunFlow(FailureSettings failure, AssistSettings assist, MessageSettings messages, long messageSeed,
            IRider rider, IAssistable assistable, ISpeedModel speed, IReadOnlyList<IChunk> stretch, ISlopeSpline slope,
            DucklingNestService ducklings, IFlowCamera camera, RiderCameraSignals signals)
        {
            _f = failure ?? throw new ArgumentNullException(nameof(failure));
            _a = assist ?? throw new ArgumentNullException(nameof(assist));
            _m = messages ?? throw new ArgumentNullException(nameof(messages));
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            if (speed == null) throw new ArgumentNullException(nameof(speed));
            if (stretch == null) throw new ArgumentNullException(nameof(stretch));
            _ducklings = ducklings ?? throw new ArgumentNullException(nameof(ducklings));
            _assistable = assistable;
            _camera = camera;
            _signals = signals;
            _picker = new MessagePicker(messages, messageSeed);
            _checkpoints = new CheckpointService();
            _tracker = new CheckpointTracker(stretch, slope);
            _assists = new AssistService(failure, assist);
            _failure = new FailureService(rider, speed, _checkpoints, _assists, ducklings);

            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.DucklingNest)
                        ducklings.Register(f.variant, _tracker.IndexBefore(c.StartDistance + f.start));
                }
            }

            _checkpoints.Begin(rider.Position);
            int already = _tracker.Update(rider.Position.Distance);
            if (already >= 0) _checkpoints.Reach(already, _tracker.PointAt(already));
            _rider.Crashed += OnCrash;
            ApplyAssists();
        }

        public FlowPhase Phase { get; private set; } = FlowPhase.Riding;
        public float PhaseTime { get; private set; }
        public FlowMessage Message { get; private set; }
        public int Retries { get; private set; }
        public int ConsecutiveFailures => _failure.ConsecutiveFailures;
        public bool AssistsActive => _assists.AssistsActive;
        public ICheckpointService Checkpoints => _checkpoints;
        public CheckpointTracker Tracker => _tracker;

        public event Action<FlowPhase, FlowPhase> PhaseChanged;
        public event Action<FlowMessage> MessageShown;
        public event Action<SlopePoint> Retried;
        public event Action<int> CheckpointReached;

        public void Tick(IInputSource input, float dt)
        {
            PhaseTime += dt;
            switch (Phase)
            {
                case FlowPhase.Riding:
                    int reached = _tracker.Update(_rider.Position.Distance);
                    if (reached >= 0)
                    {
                        _checkpoints.Reach(reached, _tracker.PointAt(reached));
                        _failure.ReportCheckpointCleared();
                        ApplyAssists();
                        CheckpointReached?.Invoke(reached);
                    }
                    break;
                case FlowPhase.FailureHold:
                    if (input != null && input.TapThisStep && _assists.AssistsOffered && !_assists.AssistsActive) AcceptAssist();
                    bool skip = input != null && input.TapThisStep && PhaseTime >= _f.minHoldBeforeSkipSeconds;
                    if (skip || PhaseTime >= _f.failureHoldSeconds)
                    {
                        _teleported = false;
                        SetPhase(FlowPhase.Retrying);
                    }
                    break;
                case FlowPhase.Retrying:
                    if (!_teleported && PhaseTime >= _f.retryTransitionSeconds * _f.retryTeleportFraction)
                    {
                        _failure.InstantRetry();
                        _camera?.SnapToRider();
                        _teleported = true;
                        Retries++;
                        Retried?.Invoke(_checkpoints.LastCheckpointPoint);
                    }
                    if (_teleported && PhaseTime >= _f.retryTransitionSeconds)
                    {
                        if (_signals != null) _signals.FailureHold = false;
                        _ducklings.OpenCovers();
                        SetPhase(FlowPhase.Riding);
                    }
                    break;
            }
        }

        private void OnCrash(CrashCause cause)
        {
            if (Phase != FlowPhase.Riding) return;
            bool wasOffered = _assists.AssistsOffered;
            _failure.ReportCrash(cause);
            bool newlyOffered = _assists.AssistsOffered && !wasOffered;
            if (newlyOffered && _f.autoAcceptAssists) _assists.Accept();
            ApplyAssists();

            string text = newlyOffered ? _picker.PickAssist() : _picker.Pick(cause);
            string hint = newlyOffered && !_assists.AssistsActive ? _m.assistAcceptHint : _m.retryHint;
            Message = new FlowMessage(text, hint, newlyOffered);
            if (_signals != null) _signals.FailureHold = true;
            SetPhase(FlowPhase.FailureHold);
            MessageShown?.Invoke(Message);
        }

        /// <summary>Player accepted an offered assist (when autoAcceptAssists is off).</summary>
        public void AcceptAssist()
        {
            _assists.Accept();
            ApplyAssists();
        }

        private void ApplyAssists()
        {
            if (_assistable == null) return;
            bool on = _assists.AssistsActive;
            _assistable.SetLandingBonus(on ? _a.landingToleranceBonusDegrees : 0f);
            _assistable.SetSpeedScale(on ? _a.speedScale : 1f);
        }

        private void SetPhase(FlowPhase next)
        {
            if (next == Phase) return;
            FlowPhase prev = Phase;
            Phase = next;
            PhaseTime = 0f;
            PhaseChanged?.Invoke(prev, next);
        }
    }
}
