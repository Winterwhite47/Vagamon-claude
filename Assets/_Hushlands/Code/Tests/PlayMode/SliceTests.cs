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

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TerraceSlice_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_TerraceCountry");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LakeSlice_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_MirrorLake");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Journey_Builds_AndAutopilotRides()
        {
            yield return SliceBuildsAndRides("Slice_Journey");
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
                if (boot.Journey)
                {
                    Assert.IsNotNull(r.Plan, "journey has no plan");
                    Assert.AreEqual(System.Enum.GetValues(typeof(BiomeId)).Length, r.Plan.Segments.Count);
                    Assert.Greater(r.WaterPlanes, 0, "the lake segment has no water");
                    Assert.AreEqual(r.Plan.Segments[0].Biome, boot.Atmosphere.Dominant);
                }
                else
                {
                    Assert.IsNull(r.Plan);
                }
                Assert.IsNotNull(boot.Atmosphere);
                Assert.AreEqual(boot.Biome, boot.Atmosphere.Dominant);
                Assert.Greater(boot.Atmosphere.State.SunLux, 0f);
                Assert.IsNotNull(boot.Humor);
                Assert.IsNotNull(boot.FailureShow);
                Assert.IsFalse(boot.FailureShow.Active);
                Assert.IsNotNull(boot.Sound);
                Assert.IsNotNull(boot.Sound.Mix);
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
                Assert.Greater(boot.Sound.Mix.WindGain, 0f, "no wind bed while riding");
                Assert.LessOrEqual(boot.Sound.Mix.WindGain, 1f);
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
