using System;
using Hushlands.Core;

namespace Hushlands
{
    /// <summary>Composition root: builds the T1 service stubs from a config set.</summary>
    public sealed class HushServices
    {
        public HushServices(HushConfigSet c, ISlopeSpline slope)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (slope == null) throw new ArgumentNullException(nameof(slope));

            Input = new ScriptedInput();
            Surfaces = new SurfaceTable(c.surfaces.Settings);
            Speed = new SpeedModel(c.speed.Settings);
            Rider = new KinematicRider(c.rider.Settings, c.speed.Settings, slope, Surfaces);
            Checkpoints = new CheckpointService();
            Assists = new AssistService(c.failure.Settings, c.assist.Settings);
            Ducklings = new DucklingNestService();
            Failure = new FailureService(Rider, Speed, Checkpoints, Assists, Ducklings);
            Camera = new CameraStateMachine(c.camera.Settings);
            Mix = new MixDirector(c.audioMix.Settings);
            Score = new ScoreService(c.score.Settings);
            Save = new MemorySaveService();
            Jump = JumpModel.ForAllVehicles(c.rider.Settings, c.speed.Settings, c.vehicles.Settings);
            Generator = new ChunkGenerator(c.content.Settings, c.fairness.Settings, c.biomes.Settings, Jump);
            Fairness = new FairnessChecker(c.fairness.Settings, c.content.Settings, Jump);
            Bot = new BotValidator(c.rider.Settings, c.speed.Settings, c.surfaces.Settings, c.fairness.Settings,
                c.vehicles.Settings);
        }

        public ScriptedInput Input { get; }
        public ISurfaceProvider Surfaces { get; }
        public ISpeedModel Speed { get; }
        public IRider Rider { get; }
        public ICheckpointService Checkpoints { get; }
        public AssistService Assists { get; }
        public DucklingNestService Ducklings { get; }
        public FailureService Failure { get; }
        public CameraStateMachine Camera { get; }
        public MixDirector Mix { get; }
        public ScoreService Score { get; }
        public ISaveService Save { get; }
        public JumpModel Jump { get; }
        public ChunkGenerator Generator { get; }
        public IFairnessChecker Fairness { get; }
        public IBotValidator Bot { get; }
    }
}
