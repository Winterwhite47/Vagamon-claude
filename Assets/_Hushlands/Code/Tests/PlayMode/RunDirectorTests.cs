using System.Collections;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    /// <summary>T5 on PhysX: obstacle crash -> failure hold -> retry at checkpoint -> riding again.</summary>
    public sealed class RunDirectorTests
    {
        private const float Dt = 0.01f;

        [UnityTest]
        public IEnumerator PhysxCrash_RetriesAtCheckpoint_AndRidesOn()
        {
            HushConfigSet config = HushConfigSet.Load();
            Assert.IsNotNull(config, "run project bootstrap first");
            SimulationMode previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            List<Chunk> chunks = ChunkSerializer.Read(
                "chunk a MeadowSea 0 200 10\nsurface 0 200 Grass\n" +
                "feature Checkpoint 5 0 0 0 0 0 0 0\n" +
                "feature Obstacle 60 1 0.5 0 0 0 0 0\nend\n");
            var slope = new StretchSlope(chunks);
            var root = new GameObject("RunTest");
            TrackBuilder.Build(chunks, slope, config.track.Settings, root.transform);
            var riderGo = new GameObject("Rider");
            riderGo.transform.SetParent(root.transform);
            riderGo.AddComponent<Rigidbody>();
            PhysxRider rider = riderGo.AddComponent<PhysxRider>();
            var input = new ScriptedInput();
            rider.Initialize(config.rider.Settings, config.speed.Settings, config.surfaces.Settings, slope, input, false);
            Physics.SyncTransforms();
            rider.ResetTo(new SlopePoint(1d, slope.HeightAt(1d)), config.speed.Settings.startSpeed);
            Physics.SyncTransforms();

            RunDirector director = root.AddComponent<RunDirector>();
            var signals = new RiderCameraSignals(rider, chunks);
            director.Initialize(config, rider, input, chunks, slope, null, signals, 1);
            RunFlow flow = director.Flow;

            int crashes = 0;
            flow.MessageShown += m => crashes++;
            int steps = 0;
            while (crashes == 0 && steps++ < 3000)
            {
                rider.FixedStep(input, Dt);
                Physics.Simulate(Dt);
                flow.Tick(input, Dt);
            }
            Assert.AreEqual(1, crashes);
            Assert.AreEqual(FlowPhase.FailureHold, flow.Phase);

            steps = 0;
            while (flow.Phase != FlowPhase.Riding && steps++ < 1000)
            {
                rider.FixedStep(input, Dt);
                Physics.Simulate(Dt);
                flow.Tick(input, Dt);
            }
            Assert.AreEqual(FlowPhase.Riding, flow.Phase);
            Assert.AreEqual(RiderState.Grounded, rider.State);
            Assert.Less(rider.Position.Distance, 20d);
            for (int i = 0; i < 100; i++)
            {
                rider.FixedStep(input, Dt);
                Physics.Simulate(Dt);
                flow.Tick(input, Dt);
            }
            Assert.AreNotEqual(RiderState.Crashed, rider.State);

            Object.DestroyImmediate(root);
            Physics.simulationMode = previous;
            yield return null;
        }
    }
}
