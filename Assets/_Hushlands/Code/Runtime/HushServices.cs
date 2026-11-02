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
            Generator = new StubChunkGenerator(c.content.Settings);
            Fairness = new StructuralFairnessChecker(c.content.Settings);
            Bot = new StubBotValidator(c.rider.Settings, c.speed.Settings, c.surfaces.Settings,
                c.content.Settings.botTestSlopeDegrees * (float)(Math.PI / 180.0));
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
        public IChunkGenerator Generator { get; }
        public IFairnessChecker Fairness { get; }
        public IBotValidator Bot { get; }
    }
}
