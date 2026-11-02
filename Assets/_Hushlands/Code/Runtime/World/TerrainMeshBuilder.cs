using System.Collections.Generic;
using Hushlands.Core;
using UnityEngine;

namespace Hushlands
{
    /// <summary>Tiles of procedural terrain from TerrainSampler, plus the track strip (no strip over gaps).</summary>
    public static class TerrainMeshBuilder
    {
        private const int ColumnsPerTile = 32;

        public static List<GameObject> BuildTerrain(TerrainSampler sampler, LookSettings s, double length, Material mat, Transform parent)
        {
            var tiles = new List<GameObject>();
            float cell = s.terrainCellMeters;
            int rows = Mathf.CeilToInt((s.terrainFrontDepth + s.terrainBackDepth) / cell) + 1;
            int columns = Mathf.CeilToInt((float)length / cell) + 1;
            for (int c0 = 0; c0 < columns - 1; c0 += ColumnsPerTile)
            {
                int cols = Mathf.Min(ColumnsPerTile, columns - 1 - c0) + 1;
                var verts = new Vector3[cols * rows];
                var uvs = new Vector2[cols * rows];
                for (int i = 0; i < cols; i++)
                {
                    double x = (c0 + i) * cell;
                    for (int j = 0; j < rows; j++)
                    {
                        double z = -s.terrainFrontDepth + j * cell;
                        verts[i * rows + j] = new Vector3((float)x, (float)sampler.Height(x, z), (float)z);
                        uvs[i * rows + j] = new Vector2((float)x / cell, (float)z / cell);
                    }
                }
                tiles.Add(MakeMesh("Terrain_" + c0, verts, uvs, Grid(cols, rows), mat, parent));
            }
            return tiles;
        }

        public static GameObject BuildTrackStrip(TerrainSampler sampler, LookSettings s, double length, Material mat, Transform parent,
            float lift)
        {
            float cell = s.terrainCellMeters * 0.5f;
            int columns = Mathf.CeilToInt((float)length / cell) + 1;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float hw = s.trackStripHalfWidth;
            for (int i = 0; i < columns - 1; i++)
            {
                double x0 = i * cell;
                double x1 = (i + 1) * cell;
                if (sampler.InGap(x0) || sampler.InGap(x1)) continue;
                int b = verts.Count;
                float y0 = (float)sampler.Track(x0) + lift;
                float y1 = (float)sampler.Track(x1) + lift;
                verts.Add(new Vector3((float)x0, y0, -hw));
                verts.Add(new Vector3((float)x0, y0, hw));
                verts.Add(new Vector3((float)x1, y1, -hw));
                verts.Add(new Vector3((float)x1, y1, hw));
                tris.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            }
            return MakeMesh("TrackStrip", verts.ToArray(), null, tris.ToArray(), mat, parent);
        }

        private static int[] Grid(int cols, int rows)
        {
            var t = new int[(cols - 1) * (rows - 1) * 6];
            int k = 0;
            for (int i = 0; i < cols - 1; i++)
            {
                for (int j = 0; j < rows - 1; j++)
                {
                    int a = i * rows + j;
                    int b = (i + 1) * rows + j;
                    t[k++] = a;
                    t[k++] = a + 1;
                    t[k++] = b;
                    t[k++] = b;
                    t[k++] = a + 1;
                    t[k++] = b + 1;
                }
            }
            return t;
        }

        private static GameObject MakeMesh(string name, Vector3[] verts, Vector2[] uvs, int[] tris, Material mat, Transform parent)
        {
            var mesh = new Mesh { name = name };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            if (uvs != null) mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }
    }
}
