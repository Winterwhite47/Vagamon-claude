using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>
    /// Seeded procedural chunk generator. Fair by construction (hazard sizes are shrunk until the
    /// default-vehicle jump clears them across the takeoff window); FairnessChecker re-verifies independently.
    /// Mirrored by tools/qa/content_sim.py; keep the RNG call order identical.
    /// </summary>
    public sealed class ChunkGenerator : IChunkGenerator
    {
        private readonly ContentSettings _c;
        private readonly FairnessSettings _f;
        private readonly BiomeSettings _biomes;
        private readonly JumpModel _jump;
        private static readonly SurfaceId[] AllSurfaces = (SurfaceId[])Enum.GetValues(typeof(SurfaceId));

        public ChunkGenerator(ContentSettings content, FairnessSettings fairness, BiomeSettings biomes, JumpModel jump)
        {
            _c = content ?? throw new ArgumentNullException(nameof(content));
            _f = fairness ?? throw new ArgumentNullException(nameof(fairness));
            _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
            _jump = jump;
        }

        public IReadOnlyList<IChunk> GenerateStretch(int seed, BiomeId biome, int chunkCount)
        {
            return Generate(seed, biome, chunkCount);
        }

        public List<Chunk> Generate(int seed, BiomeId biome, int chunkCount)
        {
            if (chunkCount <= 0) throw new ArgumentOutOfRangeException(nameof(chunkCount));
            var rng = new Rng(Rng.Mix(seed, (int)biome));
            var chunks = new List<Chunk>(chunkCount);
            int nestGroup = 0;
            for (int i = 0; i < chunkCount; i++)
            {
                chunks.Add(BuildChunk(rng, biome, seed, i, ref nestGroup));
            }
            Chunk.Assemble(chunks);
            ClampMistOverReveals(chunks);
            return chunks;
        }

        private Chunk BuildChunk(Rng rng, BiomeId biome, int seed, int index, ref int nestGroup)
        {
            double length = rng.Range(_c.minChunkLength, _c.maxChunkLength);
            int difficulty = rng.RangeInclusive(_c.minDifficulty, _c.maxDifficulty);
            double slopeDeg = rng.Range(_c.minSlopeDegrees, _c.maxSlopeDegrees);
            var chunk = new Chunk(biome + "_" + seed + "_" + index, biome, difficulty, length,
                (float)(slopeDeg * Math.PI / 180.0));
            double tan = JumpModel.Tan(chunk.SlopeRadians);

            // Surfaces.
            SurfaceId primary = _biomes.Get(biome).primarySurface;
            if (rng.NextDouble() < _c.secondarySurfaceChance && length - _c.tailOutMeters > _c.leadInMeters)
            {
                double split = rng.Range(_c.leadInMeters, length - _c.tailOutMeters);
                SurfaceId secondary = AllSurfaces[rng.RangeInclusive(0, AllSurfaces.Length - 1)];
                chunk.AddSurface(new SurfaceSpan(0d, split, primary));
                chunk.AddSurface(new SurfaceSpan(split, length - split, secondary));
            }
            else
            {
                chunk.AddSurface(new SurfaceSpan(0d, length, primary));
            }

            // Checkpoint at chunk start.
            chunk.AddFeature(ChunkFeature.Make(FeatureKind.Checkpoint, _c.checkpointOffsetMeters, 0d));

            // Hazards.
            var blocked = new List<Span>();
            int maxHazards = (int)Math.Floor(_c.hazardsBase + _c.hazardsPerDifficulty * difficulty);
            int diffRange = _c.maxDifficulty - _c.minDifficulty;
            double dn = diffRange > 0 ? (double)(difficulty - _c.minDifficulty) / diffRange : 0d;
            double cursor = _c.leadInMeters;
            double takeoff = _jump.TakeoffZoneLength(_f.takeoffWindowMaxSeconds);
            double far = _jump.FarthestLanding(tan, _f.takeoffWindowMinSeconds);

            for (int h = 0; h < maxHazards; h++)
            {
                double kindRoll = rng.NextDouble();
                double sizeT = dn * rng.NextDouble();
                double jitter = rng.Range(0d, _c.hazardSpacingJitterMeters);

                FeatureKind kind;
                if (kindRoll < _c.obstacleWeight) kind = FeatureKind.Obstacle;
                else if (kindRoll < _c.obstacleWeight + _c.gapWeight || difficulty < _c.collapseMinDifficulty) kind = FeatureKind.Gap;
                else kind = FeatureKind.CollapseZone;

                ChunkFeature f;
                double lead = takeoff;
                switch (kind)
                {
                    case FeatureKind.Obstacle:
                        f = ChunkFeature.Make(kind, 0d, Lerp(_c.obstacleMinLength, _c.obstacleMaxLength, sizeT));
                        f.height = (float)Lerp(_c.obstacleMinHeight, _c.obstacleMaxHeight, sizeT);
                        break;
                    case FeatureKind.Gap:
                        f = ChunkFeature.Make(kind, 0d, Lerp(_c.gapMinLength, _c.gapMaxLength, sizeT));
                        break;
                    default:
                        f = ChunkFeature.Make(kind, 0d, Lerp(_c.collapseMinLength, _c.collapseMaxLength, sizeT));
                        f.warningLead = _f.collapseWarningSeconds * _jump.MaxSpeed * _c.collapseWarningSlack;
                        lead = Math.Max(takeoff, f.warningLead);
                        break;
                }

                bool clear = false;
                for (int t = 0; t <= _c.sizeShrinkTries; t++)
                {
                    if (_jump.ClearsAcrossWindow(f, tan, _f))
                    {
                        clear = true;
                        break;
                    }
                    f.length *= _c.sizeShrinkFactor;
                    f.height *= _c.sizeShrinkFactor;
                }
                if (!clear) continue;

                f.start = cursor + lead;
                double zoneEnd = f.start + Math.Max(f.length, far) + _f.minSafeLandingLength + _f.riderFootprintMeters;
                if (zoneEnd > length - _c.tailOutMeters) break;
                chunk.AddFeature(f);
                blocked.Add(new Span(f.start - lead, zoneEnd));
                cursor = zoneEnd + jitter;
            }

            // Mist bank (clamped later where it would hide a required jump).
            if (rng.NextDouble() < _c.mistBankChance)
            {
                double ml = Math.Min(rng.Range(_c.mistMinLength, _c.mistMaxLength), length);
                double ms = rng.Range(0d, length - ml);
                var mist = ChunkFeature.Make(FeatureKind.MistBank, ms, ml);
                mist.density = (float)rng.Range(0d, _c.mistMaxDensity);
                chunk.AddFeature(mist);
            }

            // Pickups on clear ground.
            for (int k = 0; k < _c.pickupsPerChunk; k++)
            {
                double rareRoll = rng.NextDouble();
                int rareIndex = rng.RangeInclusive(1, (int)PickupId.GoldenMistbell);
                double pos;
                if (!TryPlace(rng, length, blocked, 0d, out pos)) continue;
                var p = ChunkFeature.Make(FeatureKind.Pickup, pos, 0d);
                p.variant = rareRoll < _c.rarePickupChance ? rareIndex : (int)PickupId.SpiceCoin;
                chunk.AddFeature(p);
            }

            // Duckling nest, well clear of every hazard zone.
            if (rng.NextDouble() < _c.nestChance)
            {
                double pos;
                if (TryPlace(rng, length, blocked, _f.nestHazardSpacing, out pos))
                {
                    var nest = ChunkFeature.Make(FeatureKind.DucklingNest, pos, 0d);
                    nest.variant = nestGroup++;
                    chunk.AddFeature(nest);
                }
            }
            return chunk;
        }

        private bool TryPlace(Rng rng, double length, List<Span> blocked, double spacing, out double pos)
        {
            for (int t = 0; t < _c.placementTries; t++)
            {
                double candidate = rng.Range(_c.leadInMeters, length - _c.tailOutMeters);
                bool ok = true;
                foreach (Span b in blocked)
                {
                    if (b.Expand(spacing).Contains(candidate))
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    pos = candidate;
                    return true;
                }
            }
            pos = 0d;
            return false;
        }

        private void ClampMistOverReveals(List<Chunk> chunks)
        {
            List<HazardZone> zones = HazardZone.Collect(chunks, _jump, _f);
            float safe = _f.maxMistOverRequiredJump * _c.mistSafeFactor;
            foreach (Chunk c in chunks)
            {
                for (int i = 0; i < c.Features.Count; i++)
                {
                    ChunkFeature m = c.Features[i];
                    if (m.kind != FeatureKind.MistBank || m.density <= safe) continue;
                    var abs = new Span(c.StartDistance + m.start, c.StartDistance + m.End);
                    foreach (HazardZone z in zones)
                    {
                        if (z.Feature.IsRequiredJump && z.Reveal.Overlaps(abs))
                        {
                            m.density = safe;
                            c.SetFeature(i, m);
                            break;
                        }
                    }
                }
            }
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}
