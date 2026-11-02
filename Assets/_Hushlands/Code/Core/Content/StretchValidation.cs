using System;
using System.Collections.Generic;
using System.Text;

namespace Hushlands.Core
{
    /// <summary>Runs generator + fairness checker + bot over N seeded stretches and produces a log.</summary>
    public sealed class StretchValidation
    {
        private static readonly BiomeId[] Biomes = (BiomeId[])Enum.GetValues(typeof(BiomeId));
        private readonly ChunkGenerator _generator;
        private readonly IFairnessChecker _fairness;
        private readonly IBotValidator _bot;
        private readonly ContentSettings _content;

        public StretchValidation(ChunkGenerator generator, IFairnessChecker fairness, IBotValidator bot, ContentSettings content)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _fairness = fairness ?? throw new ArgumentNullException(nameof(fairness));
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public static StretchValidation FromSettings(ContentSettings content, FairnessSettings fairness, BiomeSettings biomes,
            RiderSettings rider, SpeedSettings speed, SurfaceSettings surfaces, VehicleSettings vehicles)
        {
            JumpModel jump = JumpModel.ForDefaultVehicle(rider, speed, vehicles);
            return new StretchValidation(new ChunkGenerator(content, fairness, biomes, jump),
                new FairnessChecker(fairness, content, jump),
                new BotValidator(rider, speed, surfaces, fairness, vehicles), content);
        }

        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public int HazardCount { get; private set; }
        public List<string> Failures { get; } = new List<string>();

        /// <summary>Stretch i uses seed firstSeed+i and biome i mod 5.</summary>
        public string Run(int firstSeed, int count)
        {
            Passed = 0;
            Failed = 0;
            HazardCount = 0;
            Failures.Clear();
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int seed = firstSeed + i;
                BiomeId biome = Biomes[i % Biomes.Length];
                List<Chunk> stretch = _generator.Generate(seed, biome, _content.chunksPerStretch);
                foreach (Chunk c in stretch)
                {
                    foreach (ChunkFeature f in c.Features)
                    {
                        if (f.IsRequiredJump) HazardCount++;
                    }
                }
                IReadOnlyList<FairnessViolation> violations = _fairness.Check(stretch);
                bool botOk = _bot.Validate(stretch, seed, out string botReport);
                bool ok = violations.Count == 0 && botOk;
                if (ok) Passed++;
                else Failed++;
                sb.Append(ok ? "PASS " : "FAIL ").Append(biome).Append(' ').Append(botReport);
                foreach (FairnessViolation v in violations)
                {
                    sb.Append(" [").Append(v.Rule).Append(" @").Append(v.ChunkId).Append(']');
                }
                sb.Append('\n');
                if (!ok) Failures.Add(biome + " seed=" + seed);
            }
            sb.Append("summary: ").Append(Passed).Append('/').Append(count).Append(" passed, hazards=")
                .Append(HazardCount).Append('\n');
            return sb.ToString();
        }
    }
}
