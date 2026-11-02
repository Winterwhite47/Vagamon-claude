using System;
using System.Collections.Generic;

namespace Hushlands.Core
{
    /// <summary>Independent fairness rules from the bible. Every violation names its rule.</summary>
    public sealed class FairnessChecker : IFairnessChecker
    {
        public const string RuleCameraReveal = "camera.reveal";
        public const string RuleChunkBounds = "chunk.bounds";
        public const string RuleContiguous = "stretch.contiguous";
        public const string RuleSurfaceCover = "surface.cover";
        public const string RuleFeatureBounds = "feature.bounds";
        public const string RuleJumpClearable = "jump.clearable";
        public const string RuleUpgradeRequired = "upgrade.required";
        public const string RuleZoneInChunk = "zone.within.chunk";
        public const string RuleLandingSafe = "landing.safe";
        public const string RuleHazardSpacing = "hazard.spacing";
        public const string RuleMistHidesJump = "mist.hides.jump";
        public const string RuleCollapseWarning = "collapse.warning";
        public const string RuleCheckpointPresent = "checkpoint.present";
        public const string RuleCheckpointSafe = "checkpoint.safe";
        public const string RuleNestSafe = "nest.safe";
        public const string RulePickupOnHazard = "pickup.on.hazard";

        private const double Epsilon = 1e-6;
        private readonly FairnessSettings _f;
        private readonly ContentSettings _c;
        private readonly JumpModel _jump;

        public FairnessChecker(FairnessSettings fairness, ContentSettings content, JumpModel jump)
        {
            _f = fairness ?? throw new ArgumentNullException(nameof(fairness));
            _c = content ?? throw new ArgumentNullException(nameof(content));
            _jump = jump;
        }

        public IReadOnlyList<FairnessViolation> Check(IReadOnlyList<IChunk> stretch)
        {
            var v = new List<FairnessViolation>();
            if (stretch == null || stretch.Count == 0)
            {
                v.Add(new FairnessViolation("*", 0d, RuleChunkBounds));
                return v;
            }

            if (_f.visibleAheadMeters + Epsilon < _f.obstacleRevealSeconds * _jump.MaxSpeed)
                v.Add(new FairnessViolation("*", 0d, RuleCameraReveal));

            CheckChunks(stretch, v);
            List<HazardZone> zones = HazardZone.Collect(stretch, _jump, _f);
            List<KeyValuePair<Span, float>> mist = CollectMist(stretch);
            CheckHazards(zones, mist, v);
            CheckPlacements(stretch, zones, v);
            return v;
        }

        private void CheckChunks(IReadOnlyList<IChunk> stretch, List<FairnessViolation> v)
        {
            double expected = 0d;
            foreach (IChunk c in stretch)
            {
                if (c.Length < _c.minChunkLength - Epsilon || c.Length > _c.maxChunkLength + Epsilon ||
                    c.Difficulty < _c.minDifficulty || c.Difficulty > _c.maxDifficulty)
                    v.Add(new FairnessViolation(c.Id, c.StartDistance, RuleChunkBounds));
                if (Math.Abs(c.StartDistance - expected) > Epsilon)
                    v.Add(new FairnessViolation(c.Id, c.StartDistance, RuleContiguous));
                expected = c.StartDistance + c.Length;

                double cover = 0d;
                bool contiguous = true;
                foreach (SurfaceSpan s in c.Surfaces)
                {
                    if (Math.Abs(s.start - cover) > Epsilon || !(s.length > 0d)) contiguous = false;
                    cover = s.End;
                }
                if (!contiguous || Math.Abs(cover - c.Length) > Epsilon)
                    v.Add(new FairnessViolation(c.Id, c.StartDistance, RuleSurfaceCover));

                bool hasCheckpoint = false;
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.start < -Epsilon || f.End > c.Length + Epsilon || f.length < 0d)
                        v.Add(new FairnessViolation(c.Id, c.StartDistance + f.start, RuleFeatureBounds));
                    if (f.kind == FeatureKind.Checkpoint) hasCheckpoint = true;
                    if (f.requiresUpgrade && !f.optional)
                        v.Add(new FairnessViolation(c.Id, c.StartDistance + f.start, RuleUpgradeRequired));
                }
                if (!hasCheckpoint) v.Add(new FairnessViolation(c.Id, c.StartDistance, RuleCheckpointPresent));
            }
        }

        private void CheckHazards(List<HazardZone> zones, List<KeyValuePair<Span, float>> mist, List<FairnessViolation> v)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                HazardZone z = zones[i];
                if (!z.Feature.IsRequiredJump) continue;
                string id = z.Chunk.Id;
                double tan = JumpModel.Tan(z.Chunk.SlopeRadians);

                if (!_jump.ClearsAcrossWindow(z.Feature, tan, _f.takeoffWindowMinSeconds, _f.takeoffWindowMaxSeconds,
                        _f.obstacleClearanceMeters, _f.landingMarginSeconds, _f.speedSamples))
                    v.Add(new FairnessViolation(id, z.Body.Start, RuleJumpClearable));

                if (z.Whole.Start < z.Chunk.StartDistance - Epsilon ||
                    z.Whole.End > z.Chunk.StartDistance + z.Chunk.Length + Epsilon)
                    v.Add(new FairnessViolation(id, z.Body.Start, RuleZoneInChunk));

                if (z.Feature.kind == FeatureKind.CollapseZone &&
                    z.Feature.warningLead + Epsilon < _f.collapseWarningSeconds * _jump.MaxSpeed)
                    v.Add(new FairnessViolation(id, z.Body.Start, RuleCollapseWarning));

                foreach (KeyValuePair<Span, float> m in mist)
                {
                    if (m.Key.Overlaps(z.Reveal) && m.Value > _f.maxMistOverRequiredJump + Epsilon)
                    {
                        v.Add(new FairnessViolation(id, z.Body.Start, RuleMistHidesJump));
                        break;
                    }
                }

                if (i + 1 < zones.Count)
                {
                    HazardZone n = zones[i + 1];
                    if (z.Landing.End > n.Takeoff.Start + Epsilon)
                        v.Add(new FairnessViolation(id, z.Body.Start, RuleLandingSafe));
                    if (n.Body.Start - z.Body.End < _f.minGapBetweenObstacles - Epsilon)
                        v.Add(new FairnessViolation(id, z.Body.Start, RuleHazardSpacing));
                }
            }
        }

        private void CheckPlacements(IReadOnlyList<IChunk> stretch, List<HazardZone> zones, List<FairnessViolation> v)
        {
            double checkpointLead = _f.checkpointLeadSeconds * _jump.MinSpeed;
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    double d = c.StartDistance + f.start;
                    switch (f.kind)
                    {
                        case FeatureKind.Checkpoint:
                            foreach (HazardZone z in zones)
                            {
                                if (z.Whole.Contains(d) || (z.Body.Start > d && z.Body.Start - d < checkpointLead - Epsilon))
                                {
                                    v.Add(new FairnessViolation(c.Id, d, RuleCheckpointSafe));
                                    break;
                                }
                            }
                            break;
                        case FeatureKind.DucklingNest:
                            foreach (HazardZone z in zones)
                            {
                                if (z.Whole.Expand(_f.nestHazardSpacing - Epsilon).Contains(d))
                                {
                                    v.Add(new FairnessViolation(c.Id, d, RuleNestSafe));
                                    break;
                                }
                            }
                            break;
                        case FeatureKind.Pickup:
                            foreach (HazardZone z in zones)
                            {
                                if (z.Body.Contains(d))
                                {
                                    v.Add(new FairnessViolation(c.Id, d, RulePickupOnHazard));
                                    break;
                                }
                            }
                            break;
                    }
                }
            }
        }

        private static List<KeyValuePair<Span, float>> CollectMist(IReadOnlyList<IChunk> stretch)
        {
            var list = new List<KeyValuePair<Span, float>>();
            foreach (IChunk c in stretch)
            {
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.MistBank)
                        list.Add(new KeyValuePair<Span, float>(new Span(c.StartDistance + f.start, c.StartDistance + f.End), f.density));
                }
            }
            return list;
        }
    }
}
