using System;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class ClothTests
    {
        private const float Gravity = 9.81f;

        /// <summary>Mirror of tools/qa/scarf_sim.py: ramp to 18 m/s, jumps, hitches, retry reset.</summary>
        [Test]
        public void Scarf_StaysWithinStretch_TrailsBehind_UnderSpeedJumpsAndHitches()
        {
            var s = new ClothSettings();
            var solver = new VerletScarfSolver(s, Gravity);
            double slope = 12 * Math.PI / 180;
            double x = 0, y = 0, vy = 0, v = 6;
            bool air = false;
            const float neck = 0.9f;
            const float bodyOffset = -0.35f;
            solver.Reset(0f, neck, 0f, -1f, 0f, 0f);
            float[] pattern = new float[136];
            for (int i = 0; i < pattern.Length; i++)
                pattern[i] = i >= 50 && i < 55 ? 1f / 30f : (i == 105 ? 0.1f : 1f / 60f);
            float worst = 0f;
            double t = 0;
            bool reset = false;
            int k = 0;
            while (t < 30)
            {
                float dt = pattern[k++ % pattern.Length];
                t += dt;
                if (t >= 15 && !reset)
                {
                    reset = true;
                    x = 5;
                    y = -5 * Math.Tan(slope);
                    vy = 0;
                    v = 6;
                    air = false;
                    solver.Reset((float)x, (float)y + neck, 0f, -1f, 0f, 0f);
                }
                v = Math.Min(18, v + dt);
                x += v * dt;
                double ground = -x * Math.Tan(slope);
                if (!air && (int)(t / 4) != (int)((t - dt) / 4))
                {
                    vy = 6.5;
                    air = true;
                }
                if (air)
                {
                    vy -= Gravity * dt;
                    y += vy * dt;
                    if (y <= ground)
                    {
                        y = ground;
                        air = false;
                    }
                }
                else y = ground;
                solver.Step(dt, (float)x, (float)y + neck, 0f, (float)x, (float)y + neck + bodyOffset, 0f);
                Assert.IsTrue(solver.AllFinite(), "NaN at t=" + t);
                worst = Math.Max(worst, solver.MaxStretch());
                if (v > 8) Assert.Less(solver.X(solver.Count - 1), (float)x, "scarf tip ahead of rider");
                for (int i = 1; i < solver.Count; i++)
                {
                    float dx = solver.X(i) - (float)x;
                    float dy = solver.Y(i) - ((float)y + neck + bodyOffset);
                    float dz = solver.Z(i);
                    Assert.GreaterOrEqual(Math.Sqrt(dx * dx + dy * dy + dz * dz), s.bodyRadius - 1e-3, "inside body");
                }
            }
            Assert.LessOrEqual(worst, s.maxStretchRatio, "worst stretch " + worst);
        }

        [Test]
        public void Scarf_IsDeterministic_AndHandlesZeroDt()
        {
            var s = new ClothSettings();
            var a = new VerletScarfSolver(s, Gravity);
            var b = new VerletScarfSolver(s, Gravity);
            a.Reset(0, 1, 0, -1, 0, 0);
            b.Reset(0, 1, 0, -1, 0, 0);
            for (int i = 0; i < 500; i++)
            {
                float x = i * 0.2f;
                a.Step(1f / 60f, x, 1, 0, x, 0.6f, 0);
                b.Step(1f / 60f, x, 1, 0, x, 0.6f, 0);
                a.Step(0f, x, 1, 0, x, 0.6f, 0);
            }
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a.X(i), b.X(i));
        }

        [Test]
        public void Scarf_RootPinnedToAnchor()
        {
            var s = new ClothSettings();
            var solver = new VerletScarfSolver(s, Gravity);
            solver.Reset(0, 1, 0, -1, 0, 0);
            solver.Step(1f / 60f, 3f, 2f, 1f, 3f, 1.6f, 1f);
            Assert.AreEqual(3f, solver.X(0), 1e-5f);
            Assert.AreEqual(2f, solver.Y(0), 1e-5f);
            Assert.AreEqual(1f, solver.Z(0), 1e-5f);
        }

        [Test]
        public void SledFlex_BendsOnLanding_StaysBounded_Settles()
        {
            var s = new ClothSettings();
            var flex = new SledFlex(s);
            flex.Land(6f);
            float peak = 0f;
            for (int i = 0; i < 100; i++)
            {
                flex.Step(0.01f);
                peak = Math.Max(peak, Math.Abs(flex.BendDegrees));
            }
            Assert.Greater(peak, 0.5f);
            Assert.LessOrEqual(peak, s.flexMaxDegrees);
            Assert.Less(Math.Abs(flex.BendDegrees), 0.2f);
            flex.Land(1000f);
            for (int i = 0; i < 50; i++) flex.Step(0.01f);
            Assert.LessOrEqual(Math.Abs(flex.BendDegrees), s.flexMaxDegrees);
        }

        [Test]
        public void Selector_PicksByMeasuredCost_TiesGoToVerlet()
        {
            var s = new ClothSettings();
            float b = s.perScarfBudgetMs;
            Assert.AreEqual(ScarfMethod.Verlet, ScarfMethodSelector.Choose(b * 0.2, b * 0.8, s).Method);
            Assert.AreEqual(ScarfMethod.UnityCloth, ScarfMethodSelector.Choose(b * 0.8, b * 0.2, s).Method);
            Assert.AreEqual(ScarfMethod.Verlet, ScarfMethodSelector.Choose(b * 0.50, b * 0.48, s).Method);
            ScarfChoice over = ScarfMethodSelector.Choose(b * 3, b * 0.5, s);
            Assert.AreEqual(ScarfMethod.UnityCloth, over.Method);
            Assert.IsTrue(over.WithinBudget);
            ScarfChoice both = ScarfMethodSelector.Choose(b * 3, b * 4, s);
            Assert.AreEqual(ScarfMethod.Verlet, both.Method);
            Assert.IsFalse(both.WithinBudget, "over budget must be flagged for rework");
            Assert.IsFalse(ScarfMethodSelector.Choose(double.NaN, 1, s).WithinBudget);
        }

        [Test]
        public void BenchmarkLog_ParsesChoice()
        {
            Assert.AreEqual(ScarfMethod.UnityCloth, ScarfMethodSelector.ParseLogChoice("x=1\nchoice=UnityCloth\n"));
            Assert.Throws<FormatException>(() => ScarfMethodSelector.ParseLogChoice("nothing"));
        }
    }
}
