using System;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class FrameTimeStatsTests
    {
        [Test]
        public void Empty_ReturnsZero()
        {
            var s = new FrameTimeStats(4);
            Assert.AreEqual(0d, s.AverageMs);
            Assert.AreEqual(0d, s.Percentile(95));
        }

        [Test]
        public void Percentile_NearestRank()
        {
            var s = new FrameTimeStats(10);
            for (int i = 1; i <= 10; i++) s.Record(i);
            Assert.AreEqual(5.5d, s.AverageMs, 1e-9);
            Assert.AreEqual(5d, s.Percentile(50));
            Assert.AreEqual(10d, s.Percentile(95));
            Assert.AreEqual(1d, s.Percentile(0));
        }

        [Test]
        public void Budget_Check()
        {
            var s = new FrameTimeStats(100);
            for (int i = 0; i < 99; i++) s.Record(12.0);
            s.Record(30.0);
            Assert.IsTrue(s.WithinBudget(16.6, 95));
            Assert.IsFalse(s.WithinBudget(16.6, 100));
        }

        [Test]
        public void Rejects_Invalid()
        {
            var s = new FrameTimeStats(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Record(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Record(double.NaN));
            s.Record(1);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Percentile(101));
        }
    }
}
