using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Minimal chunk record. T2 extends with obstacles, jumps, landings and mist spans.</summary>
    public sealed class ChunkData : IChunk
    {
        public ChunkData(string id, BiomeId biome, double startDistance, double length, int difficulty)
        {
            Id = id;
            Biome = biome;
            StartDistance = startDistance;
            Length = length;
            Difficulty = difficulty;
        }

        public string Id { get; }
        public BiomeId Biome { get; }
        public double StartDistance { get; }
        public double Length { get; }
        public int Difficulty { get; }
    }

    /// <summary>Deterministic stub generator: seeded lengths and difficulties within config bounds.</summary>
    public sealed class StubChunkGenerator : IChunkGenerator
    {
        private readonly ContentSettings _s;

        public StubChunkGenerator(ContentSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public IReadOnlyList<IChunk> GenerateStretch(int seed, BiomeId biome, int chunkCount)
        {
            if (chunkCount <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCount));
            var rng = new Random(seed);
            var list = new List<IChunk>(chunkCount);
            double cursor = 0d;
            for (int i = 0; i < chunkCount; i++)
            {
                double length = _s.minChunkLength + rng.NextDouble() * (_s.maxChunkLength - _s.minChunkLength);
                int difficulty = rng.Next(_s.minDifficulty, _s.maxDifficulty + 1);
                list.Add(new ChunkData(biome + "_" + seed + "_" + i, biome, cursor, length, difficulty));
                cursor += length;
            }
            return list;
        }
    }

    /// <summary>Structural fairness stub: bounds and contiguity. T2 adds reveal, mist, collapse and landing rules.</summary>
    public sealed class StructuralFairnessChecker : IFairnessChecker
    {
        public const string RuleLength = "chunk.length.bounds";
        public const string RuleDifficulty = "chunk.difficulty.bounds";
        public const string RuleContiguous = "stretch.contiguous";

        private readonly ContentSettings _content;

        public StructuralFairnessChecker(ContentSettings content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public IReadOnlyList<FairnessViolation> Check(IReadOnlyList<IChunk> stretch)
        {
            var v = new List<FairnessViolation>();
            double expectedStart = 0d;
            const double contiguityEpsilon = 1e-6;
            foreach (IChunk c in stretch)
            {
                if (c.Length < _content.minChunkLength || c.Length > _content.maxChunkLength)
                    v.Add(new FairnessViolation(c.Id, expectedStart, RuleLength));
                if (c.Difficulty < _content.minDifficulty || c.Difficulty > _content.maxDifficulty)
                    v.Add(new FairnessViolation(c.Id, expectedStart, RuleDifficulty));
                if (c is ChunkData d && Math.Abs(d.StartDistance - expectedStart) > contiguityEpsilon)
                    v.Add(new FairnessViolation(c.Id, d.StartDistance, RuleContiguous));
                expectedStart += c.Length;
            }
            return v;
        }
    }

    /// <summary>Bot stub: runs the kinematic rider down a constant slope for the stretch length with no input.</summary>
    public sealed class StubBotValidator : IBotValidator
    {
        private readonly RiderSettings _rider;
        private readonly SpeedSettings _speed;
        private readonly SurfaceSettings _surfaces;
        private readonly float _testSlopeRadians;

        public StubBotValidator(RiderSettings rider, SpeedSettings speed, SurfaceSettings surfaces, float testSlopeRadians)
        {
            _rider = rider ?? throw new ArgumentNullException(nameof(rider));
            _speed = speed ?? throw new ArgumentNullException(nameof(speed));
            _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _testSlopeRadians = testSlopeRadians;
        }

        public bool Validate(IReadOnlyList<IChunk> stretch, int seed, out string report)
        {
            double length = 0d;
            foreach (IChunk c in stretch) length += c.Length;
            if (length <= 0d)
            {
                report = "empty stretch";
                return false;
            }

            var rider = new KinematicRider(_rider, _speed, new ConstantSlope(length, _testSlopeRadians, SurfaceId.Grass),
                new SurfaceTable(_surfaces));
            var input = new ScriptedInput();
            bool crashed = false;
            rider.Crashed += _ => crashed = true;
            double maxSeconds = length / _speed.minSpeed * 2d;
            int maxSteps = (int)Math.Ceiling(maxSeconds / _rider.physicsStep);
            int steps = 0;
            while (rider.Position.Distance < length && !crashed && steps < maxSteps)
            {
                rider.FixedStep(input, _rider.physicsStep);
                steps++;
            }
            bool ok = !crashed && rider.Position.Distance >= length;
            report = "seed=" + seed + " length=" + length.ToString("F1") + " steps=" + steps + " ok=" + ok;
            return ok;
        }
    }
}
