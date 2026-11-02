using NUnit.Framework;
using UnityEngine;

namespace Hushlands.Tests
{
    public sealed class InputSampleTests
    {
        private const float Dt = 0.01f;
        private const float Threshold = 0.18f;

        private static HushInputSource Make(out GameObject go)
        {
            go = new GameObject("input");
            go.SetActive(false);
            return go.AddComponent<HushInputSource>();
        }

        [Test]
        public void QuickPressRelease_IsTap()
        {
            HushInputSource s = Make(out GameObject go);
            s.Sample(true, false, true, Dt, Threshold);
            Assert.IsFalse(s.TapThisStep);
            s.Sample(false, true, false, Dt, Threshold);
            Assert.IsTrue(s.TapThisStep);
            s.Sample(false, false, false, Dt, Threshold);
            Assert.IsFalse(s.TapThisStep);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void PressAndReleaseSameStep_IsTap()
        {
            HushInputSource s = Make(out GameObject go);
            s.Sample(true, true, false, Dt, Threshold);
            Assert.IsTrue(s.TapThisStep);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void LongHold_IsNotTap_AndCountsSeconds()
        {
            HushInputSource s = Make(out GameObject go);
            s.Sample(true, false, true, Dt, Threshold);
            for (int i = 0; i < 30; i++) s.Sample(false, false, true, Dt, Threshold);
            Assert.IsTrue(s.IsHeld);
            Assert.AreEqual(0.31f, s.HeldSeconds, 1e-4f);
            s.Sample(false, true, false, Dt, Threshold);
            Assert.IsFalse(s.TapThisStep);
            Assert.IsFalse(s.IsHeld);
            Object.DestroyImmediate(go);
        }
    }
}
