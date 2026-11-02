using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>
    /// One run: flow (crash, retry, assists) + pickups + score. Scoring and pickups only advance while Riding.
    /// Wind feather drives the camera Boost signal; flips score on safe landings.
    /// </summary>
    public sealed class RunSession
    {
        private readonly IRider _rider;
        private readonly RiderCameraSignals _signals;
        private readonly float _bodyOffset;
        private int _flipsCounted;

        /// <param name="bodyOffset">Height of the rider's body centre above the sled point; pickups are touched there.</param>
        private readonly ISlopeSpline _slope;

        public RunSession(RunFlow flow, PickupSystem pickups, ScoreService score, DucklingLoop ducklings, IRider rider,
            ISlopeSpline slope, RiderCameraSignals signals, float bodyOffset)
        {
            _bodyOffset = bodyOffset;
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            Ducklings = ducklings ?? throw new ArgumentNullException(nameof(ducklings));
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
            Score = score ?? throw new ArgumentNullException(nameof(score));
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            _signals = signals;
            _flipsCounted = rider.CompletedFlips;
            _rider.Crashed += _ => Pickups.OnCrash();
            Score.TrackDistance(rider.Position.Distance);
        }

        public RunFlow Flow { get; }
        public PickupSystem Pickups { get; }
        public ScoreService Score { get; }
        public DucklingLoop Ducklings { get; }

        public void Tick(IInputSource input, float dt)
        {
            Flow.Tick(input, dt);
            if (Flow.Phase != FlowPhase.Riding || _rider.State == RiderState.Crashed) return;
            SlopePoint p = _rider.Position;
            Pickups.Step(p.Distance, p.Height + _bodyOffset, dt);
            bool grounded = _rider.State == RiderState.Grounded || _rider.State == RiderState.Tucking;
            Ducklings.Step(p.Distance, p.Height - _slope.HeightAt(p.Distance), grounded);
            Score.TrackDistance(p.Distance);
            int flips = _rider.CompletedFlips;
            if (flips > _flipsCounted)
            {
                Score.AddFlips(flips - _flipsCounted);
                _flipsCounted = flips;
            }
            if (_signals != null) _signals.BoostActive = Pickups.WindActive;
        }

        /// <summary>Convenience for scenes and tests: flow + pickups + score from config blocks.</summary>
        public static RunSession Create(FailureSettings failure, AssistSettings assist, MessageSettings messages,
            PickupSettings pickups, ScoreSettings score, DucklingSettings duckSettings, float pickupHover, float bodyOffset, long seed, IRider rider, IAssistable assistable,
            IBoostable boostable, ISpeedModel speed, IReadOnlyList<IChunk> stretch, ISlopeSpline slope,
            DucklingNestService ducklings, IFlowCamera camera, RiderCameraSignals signals)
        {
            var flow = new RunFlow(failure, assist, messages, seed, rider, assistable, speed, stretch, slope, ducklings, camera, signals);
            var scoreService = new ScoreService(score);
            var system = new PickupSystem(pickups, scoreService, boostable, stretch, slope, pickupHover);
            var loop = new DucklingLoop(duckSettings, ducklings, scoreService, stretch, messages);
            return new RunSession(flow, system, scoreService, loop, rider, slope, signals, bodyOffset);
        }
    }
}
