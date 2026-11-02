using System;
using System.Collections;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Hushlands.Tests
{
    /// <summary>T3 acceptance: PhysX rider stepped manually (SimulationMode.Script) for determinism.</summary>
    public sealed class PhysxRiderTests
    {
        private const float Dt = 0.01f;
        private SimulationMode _previousMode;
        private readonly List<GameObject> _roots = new List<GameObject>();

        private sealed class World
        {
            public PhysxRider Rider;
            public ScriptedInput Input;
            public StretchSlope Slope;
            public List<Chunk> Chunks;
            public CrashCause? Crash;
        }

        [SetUp]
        public void SetUp()
        {
            _previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject g in _roots) Object.DestroyImmediate(g);
            _roots.Clear();
            Physics.simulationMode = _previousMode;
        }

        private static string Chunk(double length, double slopeDeg, string surface, string features = "")
        {
            string L = length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string S = slopeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "chunk test_" + Guid.NewGuid().ToString("N").Substring(0, 6) + " MeadowSea 0 " + L + " " + S + "\n" +
                   "surface 0 " + L + " " + surface + "\n" +
                   "feature Checkpoint 2 0 0 0 0 0 0 0\n" + features + "end\n";
        }

        private World Make(string text)
        {
            return Make(ChunkSerializer.Read(text));
        }

        private World Make(List<Chunk> chunks)
        {
            var w = new World { Chunks = chunks, Slope = new StretchSlope(chunks), Input = new ScriptedInput() };
            var root = new GameObject("TestWorld");
            _roots.Add(root);
            TrackBuilder.Build(chunks, w.Slope, new TrackSettings(), root.transform);
            var go = new GameObject("Rider");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<Rigidbody>();
            w.Rider = go.AddComponent<PhysxRider>();
            w.Rider.Initialize(new RiderSettings(), new SpeedSettings(), new SurfaceSettings(), w.Slope, null, false);
            w.Rider.Crashed += c => w.Crash = c;
            Physics.SyncTransforms();
            w.Rider.ResetTo(new SlopePoint(1d, w.Slope.HeightAt(1d)), new SpeedSettings().startSpeed);
            Physics.SyncTransforms();
            return w;
        }

        private static void Step(World w, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                w.Rider.FixedStep(w.Input, Dt);
                Physics.Simulate(Dt);
                w.Input.TapThisStep = false;
            }
        }

        private static void Tap(World w)
        {
            w.Input.TapThisStep = true;
            Step(w, 1);
        }

        private static bool Grounded(IRider r)
        {
            return r.State == RiderState.Grounded || r.State == RiderState.Tucking;
        }

        private static int StepUntil(World w, Func<bool> done, int maxSteps)
        {
            int n = 0;
            while (!done() && n < maxSteps)
            {
                Step(w, 1);
                n++;
            }
            return n;
        }

        [UnityTest]
        public IEnumerator Ride_StaysOnSlope_InSpeedRange_MatchesKinematic()
        {
            World w = Make(Chunk(1500, 10, "Grass"));
            var s = new SpeedSettings();
            var kin = new KinematicRider(new RiderSettings(), s, w.Slope, new SurfaceTable(new SurfaceSettings()));
            kin.ResetTo(new SlopePoint(1d, w.Slope.HeightAt(1d)), s.startSpeed);
            var idle = new ScriptedInput();
            int airborneSteps = 0;
            for (int i = 0; i < 3000; i++)
            {
                Step(w, 1);
                kin.FixedStep(idle, Dt);
                if (!Grounded(w.Rider)) airborneSteps++;
                Assert.GreaterOrEqual(w.Rider.Speed, s.minSpeed - 0.05f);
                Assert.LessOrEqual(w.Rider.Speed, s.maxSpeed + 0.05f);
            }
            Assert.IsNull(w.Crash);
            Assert.AreEqual(0, airborneSteps, "sled left the slope without jumping");
            Assert.AreEqual(w.Slope.HeightAt(w.Rider.Position.Distance), w.Rider.Position.Height, 0.1f);
            double rel = Math.Abs(w.Rider.Position.Distance - kin.Position.Distance) / kin.Position.Distance;
            Assert.Less(rel, 0.03, "PhysX and kinematic riders diverged");
            Assert.Greater(w.Rider.Speed, s.TargetAt(0) + 0.1f, "slope assist ramp missing");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Surfaces_ChangeSpeed()
        {
            World ice = Make(Chunk(600, 10, "Ice"));
            World mud = Make(Chunk(600, 10, "Mud"));
            for (int i = 0; i < 1000; i++)
            {
                Step(ice, 1);
                Step(mud, 1);
            }
            Assert.Greater(ice.Rider.Speed, mud.Rider.Speed + 0.5f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldOnGround_Tucks_AndIsFaster()
        {
            World tuck = Make(Chunk(600, 10, "Grass"));
            World plain = Make(Chunk(600, 10, "Grass"));
            tuck.Input.IsHeld = true;
            for (int i = 0; i < 1000; i++)
            {
                tuck.Input.HeldSeconds += Dt;
                Step(tuck, 1);
                Step(plain, 1);
            }
            Assert.AreEqual(RiderState.Tucking, tuck.Rider.State);
            Assert.Greater(tuck.Rider.Speed, plain.Rider.Speed + 0.3f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tap_Jumps_AndLandsSafely_NearModelDistance()
        {
            World w = Make(Chunk(400, 10, "Grass"));
            Step(w, 50);
            double x0 = w.Rider.Position.Distance;
            float v = w.Rider.Speed;
            Tap(w);
            Assert.AreEqual(RiderState.Airborne, w.Rider.State);
            StepUntil(w, () => Grounded(w.Rider) || w.Crash.HasValue, 400);
            Assert.IsNull(w.Crash);
            Assert.IsTrue(Grounded(w.Rider));
            var r = new RiderSettings();
            var s = new SpeedSettings();
            var model = new JumpModel(r.jumpVelocity, s.gravity * r.airGravityScale, s.minSpeed, s.maxSpeed);
            double expected = model.JumpDistance(v, Math.Tan(10 * Math.PI / 180));
            Assert.AreEqual(expected, w.Rider.Position.Distance - x0, expected * 0.1);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullFlip_LandsSafely_CountsFlip()
        {
            World w = Make(Chunk(400, 10, "Grass"));
            Step(w, 20);
            Tap(w);
            Step(w, 2);
            w.Input.IsHeld = true;
            w.Input.HeldSeconds = 0f;
            StepUntil(w, () =>
            {
                w.Input.HeldSeconds += Dt;
                return w.Rider.FlipAngle >= 360f || Grounded(w.Rider);
            }, 300);
            w.Input.IsHeld = false;
            w.Input.HeldSeconds = 0f;
            StepUntil(w, () => Grounded(w.Rider) || w.Crash.HasValue, 400);
            Assert.IsNull(w.Crash);
            Assert.AreEqual(1, w.Rider.CompletedFlips);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HalfFlip_CrashesWithBadLanding()
        {
            World w = Make(Chunk(400, 10, "Grass"));
            Step(w, 20);
            Tap(w);
            Step(w, 2);
            w.Input.IsHeld = true;
            w.Input.HeldSeconds = 0f;
            StepUntil(w, () =>
            {
                w.Input.HeldSeconds += Dt;
                return w.Rider.FlipAngle >= 180f;
            }, 300);
            w.Input.IsHeld = false;
            StepUntil(w, () => Grounded(w.Rider) || w.Crash.HasValue, 400);
            Assert.AreEqual(CrashCause.BadLanding, w.Crash);
            Assert.AreEqual(RiderState.Crashed, w.Rider.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Obstacle_WithoutJump_Crashes()
        {
            World w = Make(Chunk(200, 10, "Grass", "feature Obstacle 30 1 0.5 0 0 0 0 0\n"));
            StepUntil(w, () => w.Crash.HasValue, 2000);
            Assert.AreEqual(CrashCause.Obstacle, w.Crash);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gap_WithoutJump_Crashes_WithBot_Clears()
        {
            string text = Chunk(200, 10, "Grass", "feature Gap 30 3 0 0 0 0 0 0\n");
            World fall = Make(text);
            StepUntil(fall, () => fall.Crash.HasValue, 2000);
            Assert.AreEqual(CrashCause.Gap, fall.Crash);

            World jump = Make(text);
            var pilot = new BotPilot(jump.Chunks, new FairnessSettings().botTakeoffSeconds);
            StepUntil(jump, () =>
            {
                pilot.Drive(jump.Input, jump.Rider.Position.Distance, jump.Rider.Speed, Grounded(jump.Rider));
                return jump.Crash.HasValue || jump.Rider.Position.Distance > 60d;
            }, 3000);
            Assert.IsNull(jump.Crash);
            Assert.AreEqual(1, pilot.JumpsTaken);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bot_RidesGeneratedStretch_OnPhysx()
        {
            var content = new ContentSettings();
            var fair = new FairnessSettings();
            JumpModel jump = JumpModel.ForDefaultVehicle(new RiderSettings(), new SpeedSettings(), new VehicleSettings());
            var gen = new ChunkGenerator(content, fair, new BiomeSettings(), jump);
            List<Chunk> stretch = gen.Generate(2, BiomeId.MeadowSea, content.chunksPerStretch);
            World w = Make(stretch);
            var pilot = new BotPilot(stretch, fair.botTakeoffSeconds);
            int steps = StepUntil(w, () =>
            {
                pilot.Drive(w.Input, w.Rider.Position.Distance, w.Rider.Speed, Grounded(w.Rider));
                return w.Crash.HasValue || w.Rider.Position.Distance >= w.Slope.Length;
            }, 40000);
            Assert.IsNull(w.Crash, "crash at " + w.Rider.Position.Distance.ToString("F1") + " after " + steps + " steps");
            Assert.GreaterOrEqual(w.Rider.Position.Distance, w.Slope.Length);
            Assert.AreEqual(pilot.Hazards.Count, pilot.JumpsTaken);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ResetTo_AfterCrash_Rides()
        {
            World w = Make(Chunk(200, 10, "Grass", "feature Obstacle 30 1 0.5 0 0 0 0 0\n"));
            StepUntil(w, () => w.Crash.HasValue, 2000);
            w.Rider.ResetTo(new SlopePoint(5d, w.Slope.HeightAt(5d)), 6f);
            Physics.SyncTransforms();
            Step(w, 50);
            Assert.AreEqual(RiderState.Grounded, w.Rider.State);
            Assert.Greater(w.Rider.Position.Distance, 7d);
            yield return null;
        }
    }
}
