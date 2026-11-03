using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class UiTests
    {
        [Test]
        public void Ease_IsContinuousMonotonicAndBounded()
        {
            float last = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float v = Ease.InOutCubic(i / 1000f);
                Assert.GreaterOrEqual(v, last - 1e-6f);
                Assert.Less(v - last, 0.01f, "jump in easing");
                last = v;
            }
            Assert.AreEqual(0f, Ease.InOutCubic(-1f));
            Assert.AreEqual(1f, Ease.InOutCubic(2f));
        }

        [Test]
        public void Flow_CrossfadesScreens_NoCut()
        {
            var motion = new UiMotionSettings();
            var flow = new UiFlow(motion, new UiSettings(), UiScreen.Title);
            Assert.AreEqual(1f, flow.Alpha(UiScreen.Title));
            flow.Go(UiScreen.Garage, true);
            float maxStep = 0f;
            float prev = flow.Alpha(UiScreen.Garage);
            for (int i = 0; i < 200; i++)
            {
                flow.Tick(0.01f);
                float a = flow.Alpha(UiScreen.Garage);
                maxStep = System.Math.Max(maxStep, a - prev);
                prev = a;
                Assert.AreEqual(1f, flow.Alpha(UiScreen.Garage) + flow.Alpha(UiScreen.Title), 1e-5f, "crossfade must sum to one");
            }
            Assert.Less(maxStep, 0.05f);
            Assert.AreEqual(0f, flow.Alpha(UiScreen.Title));
            Assert.IsTrue(flow.Back());
            Assert.AreEqual(UiScreen.Title, flow.Current);
            Assert.IsFalse(flow.Back());
        }

        [Test]
        public void ReducedMotion_ShorterFades_NoSlide()
        {
            var motion = new UiMotionSettings();
            var flow = new UiFlow(motion, new UiSettings(), UiScreen.Title) { ReducedMotion = true };
            flow.Go(UiScreen.Settings, true);
            Assert.AreEqual(0f, flow.Offset(UiScreen.Settings));
            flow.Tick(motion.reducedMotionTransitionSeconds + 0.01f);
            Assert.IsFalse(flow.Transitioning);
            var full = new UiFlow(motion, new UiSettings(), UiScreen.Title);
            full.Go(UiScreen.Settings, true);
            Assert.Greater(full.Offset(UiScreen.Settings), 0f);
        }

        [Test]
        public void Pause_IsReportedByFlow()
        {
            var flow = new UiFlow(new UiMotionSettings(), new UiSettings(), UiScreen.Riding);
            flow.Go(UiScreen.Pause, true);
            Assert.IsTrue(flow.GameplayPaused);
        }

        [Test]
        public void Menu_WrapsAndSkipsDisabled()
        {
            var m = new UiMenu(new[]
            {
                new UiMenuItem { Label = "a" },
                new UiMenuItem { Label = "b", Enabled = false },
                new UiMenuItem { Label = "c" },
            });
            Assert.AreEqual(0, m.Focus);
            m.Move(1);
            Assert.AreEqual(2, m.Focus);
            m.Move(1);
            Assert.AreEqual(0, m.Focus);
            m.Move(-1);
            Assert.AreEqual(2, m.Focus);
            m.SetFocus(1);
            Assert.AreEqual(2, m.Focus, "disabled row cannot take focus");
        }

        [Test]
        public void Options_PersistAndApply()
        {
            var save = new MemorySaveService();
            var ui = new UiSettings();
            var motion = new UiMotionSettings();
            var o = new AccessibilityOptions(save, ui, motion);
            Assert.IsTrue(o.AutoAssists);
            Assert.AreEqual(1f, o.Volume);
            Assert.AreEqual(1f, o.TextScale);
            int changes = 0;
            o.Changed += () => changes++;
            o.SetReducedMotion(true);
            o.SetHighContrast(true);
            o.SetGentleTiming(true);
            o.SetAutoAssists(false);
            o.CycleTextScale(1);
            o.StepVolume(-1);
            o.StepVolume(-100);
            Assert.AreEqual(7, changes);
            Assert.AreEqual(0f, o.Volume);
            Assert.AreEqual(0.18f * ui.gentleHoldScale, o.HoldThreshold(0.18f), 1e-6f);

            var again = new AccessibilityOptions(save, ui, motion);
            Assert.IsTrue(again.ReducedMotion && again.HighContrast && again.GentleTiming);
            Assert.IsFalse(again.AutoAssists);
            Assert.AreEqual(ui.textScales[1], again.TextScale);
            Assert.AreEqual(0f, again.Volume);
            o.CycleTextScale(-1);
            o.CycleTextScale(-1);
            Assert.AreEqual(ui.textScales[ui.textScales.Length - 1], o.TextScale, "text size cycles");
        }

        [Test]
        public void Text_IsCleanAndContrastMeetsWcag()
        {
            var ui = new UiSettings();
            var errors = new List<string>();
            ui.Validate(errors);
            CollectionAssert.IsEmpty(errors, string.Join("\n", errors));
            Assert.GreaterOrEqual(Contrast.Ratio(ui.text, ui.panel), 4.5);
            Assert.GreaterOrEqual(Contrast.Ratio(ui.contrastText, ui.contrastPanel), 7.0);
            Assert.AreEqual(21.0, Contrast.Ratio("#FFFFFF", "#000000"), 1e-6);
            Assert.AreEqual(1.0, Contrast.Ratio("#777777", "#777777"), 1e-9);
            var bad = new UiSettings { text = "#2A3640" };
            errors.Clear();
            bad.Validate(errors);
            Assert.IsNotEmpty(errors, "low contrast must be rejected");
        }

        [Test]
        public void Names_ArePretty()
        {
            Assert.AreEqual("Wooden hill sled", Names.Pretty(VehicleId.WoodenHillSled.ToString()));
            Assert.AreEqual("Glider skid", Names.Pretty(VehicleId.GliderSkid.ToString()));
        }

        [Test]
        public void AssistPrompt_TapAccepts_WhenAutoOff()
        {
            List<Chunk> chunks = ChunkSerializer.Read("chunk a MeadowSea 0 300 10\nsurface 0 300 Grass\nfeature Checkpoint 5 0 0 0 0 0 0 0\nend\n");
            var slope = new StretchSlope(chunks);
            var rider = new KinematicRider(new RiderSettings(), new SpeedSettings(), slope, new SurfaceTable(new SurfaceSettings()));
            var f = new FailureSettings { autoAcceptAssists = false };
            var flow = new RunFlow(f, new AssistSettings(), new MessageSettings(), 1, rider, rider, new SpeedModel(new SpeedSettings()),
                chunks, slope, new DucklingNestService(), null, null);
            var input = new ScriptedInput();
            for (int i = 0; i < f.assistAfterFailures; i++)
            {
                rider.ForceCrash(CrashCause.Obstacle);
                if (i < f.assistAfterFailures - 1)
                {
                    for (int k = 0; k < 200; k++)
                    {
                        rider.FixedStep(input, 0.01f);
                        flow.Tick(input, 0.01f);
                    }
                }
            }
            Assert.IsFalse(flow.AssistsActive);
            Assert.AreEqual(new MessageSettings().assistAcceptHint, flow.Message.Hint);
            input.TapThisStep = true;
            flow.Tick(input, 0.01f);
            Assert.IsTrue(flow.AssistsActive, "tap during the hold accepts the offered assist");
        }
    }
}
