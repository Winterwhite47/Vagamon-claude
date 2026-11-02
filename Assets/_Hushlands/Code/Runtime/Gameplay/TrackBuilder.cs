using System;
using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>
    /// Builds PhysX colliders for an assembled stretch on the gameplay plane (x = distance, y = height, z = 0):
    /// ground boxes with holes at gaps and collapse zones, obstacle boxes, and pit triggers under holes.
    /// Collapse zones are built already open; the timed collapse arrives in T10 (D-014).
    /// </summary>
    public static class TrackBuilder
    {
        private readonly struct Piece
        {
            public readonly IChunk Chunk;
            public readonly double StartHeight;

            public Piece(IChunk chunk, double startHeight)
            {
                Chunk = chunk;
                StartHeight = startHeight;
            }

            public Vector2 Surface(double distance)
            {
                double h = StartHeight - (distance - Chunk.StartDistance) * Math.Tan(Chunk.SlopeRadians);
                return new Vector2((float)distance, (float)h);
            }

            public Vector2 Normal
            {
                get
                {
                    float a = Chunk.SlopeRadians;
                    return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                }
            }

            public Quaternion Rotation => Quaternion.Euler(0f, 0f, -Chunk.SlopeRadians * Mathf.Rad2Deg);
        }

        public static GameObject Build(IReadOnlyList<IChunk> stretch, StretchSlope slope, TrackSettings track, Transform parent)
        {
            if (stretch == null || stretch.Count == 0) throw new ArgumentException("empty stretch", nameof(stretch));
            if (slope == null) throw new ArgumentNullException(nameof(slope));
            var root = new GameObject("Track");
            if (parent != null) root.transform.SetParent(parent, false);

            for (int ci = 0; ci < stretch.Count; ci++)
            {
                IChunk c = stretch[ci];
                var piece = new Piece(c, slope.HeightAt(c.StartDistance));
                bool last = ci == stretch.Count - 1;

                var holes = new List<Span>();
                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind == FeatureKind.Gap || f.kind == FeatureKind.CollapseZone)
                        holes.Add(new Span(c.StartDistance + f.start, c.StartDistance + f.End));
                }
                holes.Sort((a, b) => a.Start.CompareTo(b.Start));

                double cursor = c.StartDistance;
                double end = c.StartDistance + c.Length + (last ? track.runOutMeters : 0d);
                foreach (Span h in holes)
                {
                    if (h.Start > cursor) AddBox(root.transform, "Ground_" + c.Id, piece, cursor, h.Start, -track.groundThickness, track.groundThickness, track.trackWidth, false, null);
                    AddBox(root.transform, "Pit_" + c.Id, piece, h.Start, h.End, -(track.groundThickness + track.pitDepth), track.pitDepth, track.trackWidth, true, CrashCause.Gap);
                    cursor = h.End;
                }
                if (end > cursor) AddBox(root.transform, "Ground_" + c.Id, piece, cursor, end, -track.groundThickness, track.groundThickness, track.trackWidth, false, null);

                foreach (ChunkFeature f in c.Features)
                {
                    if (f.kind != FeatureKind.Obstacle) continue;
                    // Height along the normal so the vertical extent above the slope equals f.height.
                    float normalHeight = f.height * Mathf.Cos(c.SlopeRadians);
                    double s = c.StartDistance + f.start;
                    AddBox(root.transform, "Obstacle_" + c.Id, piece, s, s + f.length, 0f, normalHeight, track.obstacleWidth, false, CrashCause.Obstacle);
                }
            }
            return root;
        }

        /// <summary>Box along the slope from distance a to b; its near face sits at normal offset <paramref name="offset"/> from the surface.</summary>
        private static void AddBox(Transform root, string name, Piece p, double a, double b, float offset, float thickness,
            float width, bool trigger, CrashCause? hazard)
        {
            Vector2 pa = p.Surface(a);
            Vector2 pb = p.Surface(b);
            Vector2 mid = (pa + pb) * 0.5f + p.Normal * (offset + thickness * 0.5f);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(new Vector3(mid.x, mid.y, 0f), p.Rotation);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = trigger;
            box.size = new Vector3(Vector2.Distance(pa, pb), thickness, width);
            if (hazard.HasValue) go.AddComponent<HazardMarker>().cause = hazard.Value;
            else go.AddComponent<GroundMarker>();
        }
    }
}
