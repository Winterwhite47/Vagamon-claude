using System.Collections;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    public sealed class BootSceneTests
    {
        [UnityTest]
        public IEnumerator BootScene_Loads_AndAppliesFixedStep()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            ProjectTargetsConfig targets = ProjectTargetsConfig.Load();
            Assert.IsNotNull(targets);
            Assert.AreEqual(targets.fixedTimestep, Time.fixedDeltaTime, 1e-6f);
            Assert.IsNotNull(Object.FindAnyObjectByType<FrameTimeCapture>());
            Assert.IsNotNull(Camera.main);
        }

        [UnityTest]
        public IEnumerator FrameTimeProbe_RecordsFrames()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            IFrameTimeProbe probe = new FrameTimeStats(64);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                probe.Record(Time.unscaledDeltaTime * 1000.0);
            }
            Assert.AreEqual(30, probe.SampleCount);
            Assert.Greater(probe.AverageMs, 0d);
        }
    }
}
