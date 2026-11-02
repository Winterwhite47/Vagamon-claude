using System.Collections;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    /// <summary>T9 on the real slice: a crash shuts every cover and the basket lid on the same frame; audit stays clean.</summary>
    public sealed class DucklingSafetyTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Slice_Crash_HidesDucklings_AuditClean()
        {
            yield return CrashHidesDucklings("Slice_MeadowSea");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LakeSlice_Crash_HidesDucklings_AuditClean()
        {
            yield return CrashHidesDucklings("Slice_MirrorLake");
        }

        private static IEnumerator CrashHidesDucklings(string scene)
        {
            SliceBootstrap.ForceAutopilot = true;
            SaveProvider.UseMemory = true;
            SaveProvider.Reset();
            try
            {
                yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
                yield return null;
                SliceBootstrap boot = Object.FindAnyObjectByType<SliceBootstrap>();
                Assert.IsNotNull(boot.Ducklings);
                Assert.Greater(boot.Ducklings.NestCount, 0);

                for (float t = 0f; t < 6f; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                boot.Refs.Rider.ForceCrash(CrashCause.Obstacle);
                yield return null;
                DucklingView v = boot.Ducklings.LastView;
                Assert.AreEqual(0, v.DrawnInNests + v.DrawnInBasket, "ducklings visible during failure");
                Assert.IsTrue(v.NestCoversVisible && v.BasketLidVisible);

                for (float t = 0f; t < 3f; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                yield return null;
                Assert.AreEqual(FlowPhase.Riding, boot.Refs.Run.Flow.Phase);
                Assert.AreEqual(0, boot.Ducklings.Violations, "duckling safety audit reported violations");
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
