using System;
using System.Collections.Generic;
using Hushlands.Core;
using NUnit.Framework;

namespace Hushlands.Tests
{
    public sealed class ContentTests
    {
        private ContentSettings _content;
        private FairnessSettings _fair;
        private JumpModel _jump;
        private ChunkGenerator _gen;
        private FairnessChecker _checker;
        private BotValidator _bot;

        [SetUp]
        public void Setup()
        {
            _content = new ContentSettings();
            _fair = new FairnessSettings();
            var rider = new RiderSettings();
            var speed = new SpeedSettings();
            var vehicles = new VehicleSettings();
            _jump = JumpModel.ForDefaultVehicle(rider, speed, vehicles);
            _gen = new ChunkGenerator(_content, _fair, new BiomeSettings(), _jump);
            _checker = new FairnessChecker(_fair, _content, _jump);
            _bot = new BotValidator(rider, speed, new SurfaceSettings(), _fair, vehicles);
        }

        private List<Chunk> Base()
        {
            return _gen.Generate(1, BiomeId.MeadowSea, _content.chunksPerStretch);
        }

        private static int FirstIndex(List<Chunk> chunks, FeatureKind kind, out Chunk chunk)
        {
            foreach (Chunk c in chunks)
            {
                for (int i = 0; i < c.Features.Count; i++)
                {
                    if (c.Features[i].kind == kind)
                    {
                        chunk = c;
                        return i;
                    }
                }
            }
            throw new InvalidOperationException("no " + kind);
        }

        private static bool HasRule(IReadOnlyList<FairnessViolation> v, string rule)
        {
            foreach (FairnessViolation x in v)
            {
                if (x.Rule == rule) return true;
            }
            return false;
        }

        // Golden values from tools/qa/content_sim.py: catches drift between C# and the reference model.
        [Test]
        public void Rng_MatchesReferenceModel()
        {
            Assert.AreEqual(1000004L, Rng.Mix(1, 1));
            var r = new Rng(1000004L);
            Assert.AreEqual(4294279809680864835UL, r.NextULong());
            Assert.AreEqual(10339760255989817UL, r.NextULong());
            Assert.AreEqual(0.7415648787718233, new Rng(42).NextDouble(), 1e-15);
        }

        [Test]
        public void Generator_MatchesReferenceModel()
        {
            List<Chunk> s = Base();
            double total = 0d;
            int hazards = 0;
            int features = 0;
            foreach (Chunk c in s)
            {
                total += c.Length;
                features += c.Features.Count;
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.IsHazard) hazards++;
                }
            }
            Assert.AreEqual(63.27933749458315, s[0].Length, 1e-9);
            Assert.AreEqual(0, s[0].Difficulty);
            Assert.AreEqual(0.20111975073814392, s[0].SlopeRadians, 1e-7);
            Assert.AreEqual(960.8549435189659, total, 1e-6);
            Assert.AreEqual(9, hazards);
            Assert.AreEqual(63, features);
        }

        [Test]
        public void Generator_IsDeterministic()
        {
            string a = ChunkSerializer.Write(_gen.Generate(99, BiomeId.PineHollow, 6));
            string b = ChunkSerializer.Write(_gen.Generate(99, BiomeId.PineHollow, 6));
            string c = ChunkSerializer.Write(_gen.Generate(100, BiomeId.PineHollow, 6));
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void Serializer_RoundTrips()
        {
            List<Chunk> s = Base();
            string text = ChunkSerializer.Write(s);
            List<Chunk> back = ChunkSerializer.Read(text);
            Assert.AreEqual(s.Count, back.Count);
            Assert.AreEqual(text, ChunkSerializer.Write(back));
            for (int i = 0; i < s.Count; i++) Assert.AreEqual(s[i].StartDistance, back[i].StartDistance, 1e-9);
            CollectionAssert.IsEmpty(_checker.Check(back));
        }

        [Test]
        public void Serializer_RejectsBadInput()
        {
            Assert.Throws<FormatException>(() => ChunkSerializer.Read("chunk a MeadowSea 0 50 10\n"));
            Assert.Throws<FormatException>(() => ChunkSerializer.Read("feature Gap 1 2 0 0 0 0 0 0\n"));
            Assert.Throws<FormatException>(() => ChunkSerializer.Read("chunk a Nowhere 0 50 10\nend\n"));
        }

        [Test]
        public void Validation_AllStretchesPass()
        {
            var run = new StretchValidation(_gen, _checker, _bot, _content);
            string log = run.Run(1, _content.validationStretchCount);
            TestContext.WriteLine(log);
            Assert.AreEqual(_content.validationStretchCount, run.Passed, string.Join("\n", run.Failures));
            Assert.Greater(run.HazardCount, _content.validationStretchCount);
        }

        [Test]
        public void Mutation_TallObstacle_CaughtByCheckerAndBot()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.Obstacle, out Chunk c);
            ChunkFeature f = c.Features[i];
            f.height = 3f;
            c.SetFeature(i, f);
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleJumpClearable));
            Assert.IsFalse(_bot.Validate(s, 1, out string _));
        }

        [Test]
        public void Mutation_LongGap_CaughtByCheckerAndBot()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.Gap, out Chunk c);
            ChunkFeature f = c.Features[i];
            f.length = 14d;
            c.SetFeature(i, f);
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleJumpClearable));
            Assert.IsFalse(_bot.Validate(s, 1, out string _));
        }

        [Test]
        public void Mutation_MistOverJump_Caught()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.Gap, out Chunk c);
            var m = ChunkFeature.Make(FeatureKind.MistBank, Math.Max(0d, c.Features[i].start - 10d), 20d);
            m.density = 1f;
            c.AddFeature(m);
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleMistHidesJump));
        }

        [Test]
        public void Mutation_CollapseWarningShort_Caught()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.CollapseZone, out Chunk c);
            ChunkFeature f = c.Features[i];
            f.warningLead = 2d;
            c.SetFeature(i, f);
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleCollapseWarning));
        }

        [Test]
        public void Mutation_UpgradeRequired_Caught()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.Gap, out Chunk c);
            ChunkFeature f = c.Features[i];
            f.requiresUpgrade = true;
            c.SetFeature(i, f);
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleUpgradeRequired));
        }

        [Test]
        public void Mutation_NestOnHazard_And_MissingCheckpoint_Caught()
        {
            List<Chunk> s = Base();
            int i = FirstIndex(s, FeatureKind.Gap, out Chunk c);
            c.AddFeature(ChunkFeature.Make(FeatureKind.DucklingNest, c.Features[i].start + 0.5d, 0d));
            Assert.IsTrue(HasRule(_checker.Check(s), FairnessChecker.RuleNestSafe));

            string text = ChunkSerializer.Write(Base()).Replace("feature Checkpoint", "# feature Checkpoint");
            Assert.IsTrue(HasRule(_checker.Check(ChunkSerializer.Read(text)), FairnessChecker.RuleCheckpointPresent));
        }

        [Test]
        public void CameraContract_RevealDistanceFits()
        {
            Assert.GreaterOrEqual(_fair.visibleAheadMeters, _fair.obstacleRevealSeconds * new SpeedSettings().maxSpeed);
        }

        [Test]
        public void StretchSlope_IsContinuous()
        {
            List<Chunk> s = Base();
            var slope = new StretchSlope(s);
            for (int i = 1; i < s.Count; i++)
            {
                double d = s[i].StartDistance;
                Assert.AreEqual(slope.HeightAt(d - 1e-4), slope.HeightAt(d), 1e-2);
            }
        }
    }
}
