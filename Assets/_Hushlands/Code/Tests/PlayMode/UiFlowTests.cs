using System.Collections;
using Hushlands.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Hushlands.Tests
{
    /// <summary>T13 on the slice: title holds the world still, Ride starts, pause freezes and resumes, run end shows summary.</summary>
    public sealed class UiFlowTests
    {
        private static IEnumerator Frames(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator Title_Ride_Pause_Resume_Summary()
        {
            SliceBootstrap.ForceAutopilot = false;
            SaveProvider.UseMemory = true;
            SaveProvider.Reset();
            SceneHandoff.AutoRide = false;
            SceneHandoff.CoverOnStart = false;
            try
            {
                yield return SceneManager.LoadSceneAsync("Slice_MeadowSea", LoadSceneMode.Single);
                yield return null;
                SliceBootstrap boot = Object.FindAnyObjectByType<SliceBootstrap>();
                Assert.IsNotNull(boot.Ui);
                Assert.AreEqual(UiScreen.Title, boot.Ui.Flow.Current);
                double x0 = boot.Refs.Rider.Position.Distance;
                yield return Frames(1f);
                Assert.AreEqual(x0, boot.Refs.Rider.Position.Distance, 1e-3, "world must hold still on the title");
                Assert.Greater(boot.Ui.Flow.Alpha(UiScreen.Title), 0.99f);

                boot.Ui.Inject(UiCommand.Submit);
                yield return Frames(1.5f);
                Assert.AreEqual(UiScreen.Riding, boot.Ui.Flow.Current);
                Assert.Greater(boot.Refs.Rider.Position.Distance, x0 + 3d, "Ride did not start");

                boot.Ui.Inject(UiCommand.Back);
                yield return Frames(0.2f);
                Assert.AreEqual(UiScreen.Pause, boot.Ui.Flow.Current);
                double xp = boot.Refs.Rider.Position.Distance;
                yield return Frames(1f);
                Assert.AreEqual(xp, boot.Refs.Rider.Position.Distance, 1e-3, "pause must freeze the ride");

                boot.Ui.Inject(UiCommand.Submit);
                yield return Frames(1f);
                Assert.AreEqual(UiScreen.Riding, boot.Ui.Flow.Current);
                Assert.Greater(boot.Refs.Rider.Position.Distance, xp + 1d, "resume did not continue");

                boot.Refs.Run.FinishRun();
                yield return Frames(1.5f);
                Assert.AreEqual(UiScreen.Summary, boot.Ui.Flow.Current);
                Assert.AreEqual(1, boot.Progression.Profile.Runs);
            }
            finally
            {
                Physics.simulationMode = SimulationMode.FixedUpdate;
                SaveProvider.UseMemory = false;
                SaveProvider.Reset();
            }
        }
    }
}
