using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Every settings block a run needs (defaults for tests; scenes fill it from HushConfigSet).</summary>
    public sealed class RunSettingsBundle
    {
        public FailureSettings Failure = new FailureSettings();
        public AssistSettings Assist = new AssistSettings();
        public MessageSettings Messages = new MessageSettings();
        public PickupSettings Pickups = new PickupSettings();
        public ScoreSettings Score = new ScoreSettings();
        public DucklingSettings Ducklings = new DucklingSettings();
        public HazardSettings Hazards = new HazardSettings();
        public FairnessSettings Fairness = new FairnessSettings();
        public SpeedSettings Speed = new SpeedSettings();
        public DirectorSettings Director = new DirectorSettings();
    }

    /// <summary>
    /// One run: flow (crash, retry, assists) + pickups + score + ducklings + hazards (landslide, collapse, squall).
    /// Everything but the flow advances only while Riding. Wind feather and squall headwind compose into one cruise
    /// bonus; camera signals get Boost, Danger (landslide gaining or close) and collapse warnings.
    /// </summary>
    public sealed class RunSession
    {
        private readonly IRider _rider;
        private readonly RiderCameraSignals _signals;
        private readonly float _bodyOffset;
        private readonly ISlopeSpline _slope;
        private readonly IBoostable _boost;
        private readonly ICrashable _crash;
        private readonly SpeedModel _targetModel;
        private readonly RunSettingsBundle _b;
        private int _flipsCounted;
        private double _run;

        public RunSession(RunSettingsBundle b, RunFlow flow, PickupSystem pickups, ScoreService score, DucklingLoop ducklings,
            IRider rider, ISlopeSpline slope, RiderCameraSignals signals, float bodyOffset, IReadOnlyList<IChunk> stretch, long seed)
        {
            _b = b ?? throw new ArgumentNullException(nameof(b));
            Flow = flow ?? throw new ArgumentNullException(nameof(flow));
            Pickups = pickups ?? throw new ArgumentNullException(nameof(pickups));
            Score = score ?? throw new ArgumentNullException(nameof(score));
            Ducklings = ducklings ?? throw new ArgumentNullException(nameof(ducklings));
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _signals = signals;
            _bodyOffset = bodyOffset;
            _boost = rider as IBoostable;
            _crash = rider as ICrashable;
            _targetModel = new SpeedModel(b.Speed);
            _flipsCounted = rider.CompletedFlips;

            Landslide = new LandslideChaser(b.Hazards, rider.Position.Distance);
            Collapse = new CollapseController(b.Fairness, b.Hazards, stretch);
            Squall = new SquallDirector(b.Hazards, Rng.Mix(seed, 77));
            Pacing = new PacingDirector(b.Director, stretch);
            Weather = new WeatherDirector(b.Director, Rng.Mix(seed, 78));
            Humor = new HumorDirector(b.Director, Rng.Mix(seed, 79));
            Day = new DayDrift(b.Director, Rng.Mix(seed, 80));

            double start = rider.Position.Distance;
            Stats = new RunStats { StartX = start, FurthestX = start, CleanFromX = start };
            _rider.Crashed += _ =>
            {
                Pickups.OnCrash();
                Pacing.NoteFailure();
                Humor.Cancel();
                Stats.Crashes++;
                Stats.LongestClean = Math.Max(Stats.LongestClean, Stats.CurrentClean);
                Stats.CurrentClean = 0d;
            };
            Flow.Retried += p =>
            {
                Landslide.Reset(p.Distance, b.Hazards.landslideRetryGap);
                Collapse.ResetAhead(p.Distance);
                Stats.CleanFromX = p.Distance;
            };
            Score.TrackDistance(start);
        }

        public RunFlow Flow { get; }
        public PickupSystem Pickups { get; }
        public ScoreService Score { get; }
        public DucklingLoop Ducklings { get; }
        public LandslideChaser Landslide { get; }
        public CollapseController Collapse { get; }
        public SquallDirector Squall { get; }
        public PacingDirector Pacing { get; }
        public WeatherDirector Weather { get; }
        public HumorDirector Humor { get; }
        public DayDrift Day { get; }
        /// <summary>Seconds actually ridden (holds and retries do not count).</summary>
        public double RunSeconds => _run;
        public RunStats Stats { get; }
        public bool Ended { get; private set; }

        /// <summary>Freezes the run and returns its summary (missed groups = nests left waiting).</summary>
        public RunSummary End()
        {
            Ended = true;
            Humor.Cancel();
            Stats.LongestClean = Math.Max(Stats.LongestClean, Stats.CurrentClean);
            return new RunSummary(Score.Score, Stats.Distance, Score.SpiceCoins, Score.Flips, Score.Rescues,
                Ducklings.WaitingGroups, Stats.Crashes, Stats.LongestClean);
        }

        public void Tick(IInputSource input, float dt)
        {
            if (Ended) return;
            Flow.Tick(input, dt);
            if (Flow.Phase != FlowPhase.Riding || _rider.State == RiderState.Crashed) return;
            _run += dt;
            SlopePoint p = _rider.Position;
            if (p.Distance > Stats.FurthestX) Stats.FurthestX = p.Distance;
            Stats.CurrentClean = Math.Max(Stats.CurrentClean, p.Distance - Stats.CleanFromX);

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

            Squall.Step(dt);
            float bonus = Pickups.BoostBonus - Squall.SpeedPenalty;
            _boost?.SetBoost(bonus);
            _targetModel.TargetBonus = bonus;
            Collapse.Step(p.Distance, dt);

            // Paced on the slower of the cruise target and the rider's real speed: on long runs the target ramps to the
            // cap while terrain and surfaces hold the rider below it, and a slide paced on the target alone would catch them.
            float pace = Math.Min(_targetModel.TargetSpeed(_run), _rider.Speed);
            bool caught = Landslide.Step(p.Distance, pace, dt, _b.Fairness.riderFootprintMeters);
            Stats.Flips = Score.Flips;
            Stats.Coins = Score.SpiceCoins;
            Stats.Rescues = Score.Rescues;
            if (_signals != null)
            {
                _signals.BoostActive = Pickups.WindActive;
                _signals.CollapseWarningActive = Collapse.AnyWarning;
                _signals.LandslideSeconds = Landslide.SecondsToContact(p.Distance, _rider.Speed);
            }
            bool danger = Collapse.AnyWarning || (Landslide.Active && Landslide.Gap(p.Distance) < _b.Hazards.landslideDangerGap);
            Pacing.Step(p.Distance, _rider.Speed, danger, Squall.Intensity, dt);
            Weather.Step(dt, Pacing.Calm);
            Humor.Step(p.Distance, Pacing.Calm, Pacing.Tense, dt);
            if (caught) _crash?.ForceCrash(CrashCause.Landslide);
        }

        /// <summary>Flow + pickups + score + ducklings + hazards. The rider may implement IAssistable, IBoostable, ICrashable.</summary>
        public static RunSession Create(RunSettingsBundle b, float pickupHover, float bodyOffset, long seed, IRider rider,
            IReadOnlyList<IChunk> stretch, ISlopeSpline slope, DucklingNestService ducklings, IFlowCamera camera,
            RiderCameraSignals signals)
        {
            var flow = new RunFlow(b.Failure, b.Assist, b.Messages, seed, rider, rider as IAssistable, new SpeedModel(b.Speed),
                stretch, slope, ducklings, camera, signals);
            var score = new ScoreService(b.Score);
            var pickups = new PickupSystem(b.Pickups, score, null, stretch, slope, pickupHover);
            var loop = new DucklingLoop(b.Ducklings, ducklings, score, stretch, b.Messages);
            return new RunSession(b, flow, pickups, score, loop, rider, slope, signals, bodyOffset, stretch, seed);
        }
    }
}
