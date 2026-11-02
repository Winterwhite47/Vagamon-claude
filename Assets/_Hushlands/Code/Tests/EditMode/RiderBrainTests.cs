using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class RiderBrainTests
    {
        private const float Dt = 0.01f;
        private RiderSettings _s;
        private RiderBrain _b;
        private ScriptedInput _in;

        [SetUp]
        public void Setup()
        {
            _s = new RiderSettings();
            _b = new RiderBrain(_s);
            _in = new ScriptedInput();
        }

        private RiderIntent Step(bool grounded, bool tap = false, bool held = false, float heldSeconds = 0f)
        {
            _in.TapThisStep = tap;
            _in.IsHeld = held;
            _in.HeldSeconds = heldSeconds;
            return _b.Step(_in, grounded, Dt);
        }

        [Test]
        public void TapOnGround_Jumps_Once()
        {
            Assert.IsTrue(Step(true, tap: true).Jump);
            Assert.IsFalse(Step(false, tap: true).Jump, "no double jump inside coyote window");
        }

        [Test]
        public void Coyote_AllowsLateTap()
        {
            Step(true);
            Step(false);
            Assert.IsTrue(Step(false, tap: true).Jump);
        }

        [Test]
        public void Coyote_Expires()
        {
            Step(true);
            int steps = (int)(_s.coyoteSeconds / Dt) + 2;
            for (int i = 0; i < steps; i++) Step(false);
            Assert.IsFalse(Step(false, tap: true).Jump);
        }

        [Test]
        public void JumpBuffer_FiresOnLanding()
        {
            Step(true, tap: true);
            for (int i = 0; i < 50; i++) Step(false);
            Assert.IsFalse(Step(false, tap: true).Jump);
            Assert.IsTrue(Step(true).Jump);
        }

        [Test]
        public void JumpBuffer_Expires()
        {
            Step(true, tap: true);
            for (int i = 0; i < 50; i++) Step(false);
            Step(false, tap: true);
            int steps = (int)(_s.jumpBufferSeconds / Dt) + 2;
            for (int i = 0; i < steps; i++) Step(false);
            Assert.IsFalse(Step(true).Jump);
        }

        [Test]
        public void HoldOnGround_TucksAfterThreshold()
        {
            Assert.IsFalse(Step(true, held: true, heldSeconds: Dt).Tuck);
            Assert.IsTrue(Step(true, held: true, heldSeconds: _s.holdThresholdSeconds).Tuck);
        }

        [Test]
        public void HoldInAir_FlipsOnlyIfPressStartedInAir()
        {
            Step(true, held: true, heldSeconds: _s.holdThresholdSeconds);
            Assert.IsFalse(Step(false, held: true, heldSeconds: 1f).Flip, "tuck carried off a lip must not flip");
            Step(false);
            Step(false, held: true, heldSeconds: Dt);
            Assert.IsTrue(Step(false, held: true, heldSeconds: _s.holdThresholdSeconds).Flip);
        }

        [Test]
        public void ShortHoldInAir_DoesNotFlip()
        {
            Step(true, tap: true);
            Assert.IsFalse(Step(false, held: true, heldSeconds: Dt).Flip);
        }
    }
}
