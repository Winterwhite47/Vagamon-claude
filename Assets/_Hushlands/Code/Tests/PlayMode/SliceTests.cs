using System.Collections;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    /// <summary>T7 slice smoke test: the Meadow Sea slice builds and the autopilot rides it under PhysX.</summary>
    public sealed class SliceTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator MeadowSlice_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_MeadowSea");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator WindmouthSlice_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_WindmouthRidge");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PineSlice_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_PineHollow");
        }

        private static IEnumerator SliceBuildsAndRides(string scene)
        {
            SliceBootstrap.ForceAutopilot = true;
            SaveProvider.UseMemory = true;
            SaveProvider.Reset();
            try
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                SliceBootstrap boot = Object.FindAnyObjectByType<SliceBootstrap>();
                Assert.IsNotNull(boot);
                SliceRefs r = boot.Refs;
                Assert.IsNotNull(r, "slice did not build");
                Assert.IsTrue(boot.Autopilot);
                Assert.Greater(r.TerrainTiles, 0);
                Assert.Greater(r.Obstacles + r.Nests + r.Pickups, 0);
                Assert.AreEqual(r.Mist.Banks.Count, r.MistVolumes);
                Assert.IsNotNull(r.Sun);
                Assert.IsNotNull(boot.Overlay);
                Assert.IsNotNull(Object.FindAnyObjectByType<Unity.Cinemachine.CinemachineBrain>());

                double start = r.Rider.Position.Distance;
                float t = 0f;
                while (t < 15f)
                {
                    yield return new WaitForFixedUpdate();
                    t += Time.fixedDeltaTime;
                }
                Assert.Greater(r.Rider.Position.Distance - start, 60d, "autopilot did not ride");
                Assert.AreEqual(0, r.Run.Flow.Retries, "autopilot crashed on a fair stretch");
                Assert.AreNotEqual(FlowPhase.FailureHold, r.Run.Flow.Phase);
            }
            finally
            {
                SliceBootstrap.ForceAutopilot = false;
                SaveProvider.UseMemory = false;
                SaveProvider.Reset();
            }
        }
    }
}
