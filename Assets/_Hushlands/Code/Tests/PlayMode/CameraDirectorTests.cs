using System.Collections;
using Hushlands.Core;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    public sealed class CameraDirectorTests
    {
        private sealed class Signals : ICameraSignalSource
        {
            public CameraSignals Value = new CameraSignals { LandslideSeconds = float.PositiveInfinity };

            public CameraSignals Read(float dt)
            {
                return Value;
            }
        }

        [UnityTest]
        public IEnumerator Director_DrivesCinemachineCamera()
        {
            var root = new GameObject("CamTest");
            var rider = new GameObject("Rider");
            rider.transform.SetParent(root.transform);
            var vgo = new GameObject("VCam");
            vgo.transform.SetParent(root.transform);
            CinemachineCamera vcam = vgo.AddComponent<CinemachineCamera>();
            CameraDirector director = vgo.AddComponent<CameraDirector>();
            director.AssignCamera(vcam);
            var settings = new CameraSettings();
            var slope = new ConstantSlope(10000, 0.17f, SurfaceId.Grass);
            var sig = new Signals();
            director.Initialize(settings, slope, rider.transform, sig, null, BiomeId.MeadowSea, false, false);

            rider.transform.position = new Vector3(100f, slope.HeightAt(100), 0f);
            director.Tick(0.01f);
            CameraStateProfile cruise = settings.Get(CameraState.Cruise);
            Assert.AreEqual(-cruise.distance, vcam.transform.position.z, 1e-3f);
            Assert.AreEqual(100f + cruise.lookAhead, vcam.transform.position.x, 1e-3f);
            Assert.AreEqual(cruise.fieldOfView, vcam.Lens.FieldOfView, 1e-3f);

            CameraState seen = CameraState.Cruise;
            director.StateChanged += (a, b) => seen = b;
            sig.Value.Rider = RiderState.Crashed;
            for (int i = 0; i < 200; i++) director.Tick(0.01f);
            Assert.AreEqual(CameraState.FailureHold, seen);
            Assert.AreEqual(settings.Get(CameraState.FailureHold).fieldOfView, vcam.Lens.FieldOfView, 1e-2f);

            Object.DestroyImmediate(root);
            yield return null;
        }
    }
}
