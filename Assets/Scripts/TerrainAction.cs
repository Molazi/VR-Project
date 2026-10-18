using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainAction : IUndoableAction
{
    private readonly List<TerrainSnapshot> snapshots = new();
    private TerrainSnapshot mergedSnapshot;

    public bool HasChanges => mergedSnapshot != null || snapshots.Count > 0;

    public void Record(Terrain terrain, Vector3 worldPos, float brushSize, Action action)
    {
        if (terrain == null || action == null)
            return;

        TerrainData data = terrain.terrainData;
        RectInt heightBounds = GetHeightBounds(terrain, worldPos, brushSize);
        RectInt alphaBounds = GetAlphaBounds(terrain, worldPos, brushSize);

        if (heightBounds.width <= 0 || heightBounds.height <= 0 ||
            alphaBounds.width <= 0 || alphaBounds.height <= 0)
            return;

        float[,] beforeHeights = data.GetHeights(
            heightBounds.x, heightBounds.y,
            heightBounds.width, heightBounds.height
        );

        float[,,] beforeAlphas = data.GetAlphamaps(
            alphaBounds.x, alphaBounds.y,
            alphaBounds.width, alphaBounds.height
        );

        action();

        float[,] afterHeights = data.GetHeights(
            heightBounds.x, heightBounds.y,
            heightBounds.width, heightBounds.height
        );

        float[,,] afterAlphas = data.GetAlphamaps(
            alphaBounds.x, alphaBounds.y,
            alphaBounds.width, alphaBounds.height
        );

        if (AreEqual(beforeHeights, afterHeights) &&
            AreEqual(beforeAlphas, afterAlphas))
            return;

        snapshots.Add(new TerrainSnapshot(
            terrain,
            heightBounds,
            beforeHeights,
            afterHeights,
            alphaBounds,
            beforeAlphas,
            afterAlphas
        ));
    }

    public void Complete()
    {
        if (snapshots.Count == 0)
            return;

        if (snapshots.Count == 1)
        {
            mergedSnapshot = snapshots[0];
            snapshots.Clear();
            return;
        }

        Terrain terrain = snapshots[0].Terrain;
        RectInt heightBounds = snapshots[0].HeightBounds;
        RectInt alphaBounds = snapshots[0].AlphaBounds;

        for (int i = 1; i < snapshots.Count; i++)
        {
            heightBounds = Union(heightBounds, snapshots[i].HeightBounds);
            alphaBounds = Union(alphaBounds, snapshots[i].AlphaBounds);
        }

        TerrainData data = terrain.terrainData;

        float[,] beforeHeights = data.GetHeights(
            heightBounds.x, heightBounds.y,
            heightBounds.width, heightBounds.height
        );

        float[,,] beforeAlphas = data.GetAlphamaps(
            alphaBounds.x, alphaBounds.y,
            alphaBounds.width, alphaBounds.height
        );

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            TerrainSnapshot snapshot = snapshots[i];

            CopyInto(
                beforeHeights, heightBounds,
                snapshot.BeforeHeights, snapshot.HeightBounds
            );

            CopyInto(
                beforeAlphas, alphaBounds,
                snapshot.BeforeAlphas, snapshot.AlphaBounds
            );
        }

        float[,] afterHeights = data.GetHeights(
            heightBounds.x, heightBounds.y,
            heightBounds.width, heightBounds.height
        );

        float[,,] afterAlphas = data.GetAlphamaps(
            alphaBounds.x, alphaBounds.y,
            alphaBounds.width, alphaBounds.height
        );

        mergedSnapshot = new TerrainSnapshot(
            terrain,
            heightBounds,
            beforeHeights,
            afterHeights,
            alphaBounds,
            beforeAlphas,
            afterAlphas
        );

        snapshots.Clear();
    }

    public void Undo()
    {
        if (mergedSnapshot != null)
            mergedSnapshot.RestoreBefore();
    }

    public void Redo()
    {
        if (mergedSnapshot != null)
            mergedSnapshot.RestoreAfter();
    }

    private static RectInt GetHeightBounds(Terrain terrain, Vector3 worldPos, float brushSize)
    {
        TerrainData data = terrain.terrainData;
        Vector3 localPos = worldPos - terrain.transform.position;

        int res = data.heightmapResolution;
        int cx = Mathf.RoundToInt(localPos.x / data.size.x * (res - 1));
        int cy = Mathf.RoundToInt(localPos.z / data.size.z * (res - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(brushSize / data.size.x * (res - 1)),
            1, res / 2
        );

        int x0 = Mathf.Max(0, cx - radius);
        int x1 = Mathf.Min(res - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius);
        int y1 = Mathf.Min(res - 1, cy + radius);

        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private static RectInt GetAlphaBounds(Terrain terrain, Vector3 worldPos, float brushSize)
    {
        TerrainData data = terrain.terrainData;
        Vector3 localPos = worldPos - terrain.transform.position;

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int cx = Mathf.RoundToInt(localPos.x / data.size.x * (width - 1));
        int cy = Mathf.RoundToInt(localPos.z / data.size.z * (height - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(brushSize / data.size.x * width),
            1, 50
        );

        int x0 = Mathf.Max(0, cx - radius);
        int x1 = Mathf.Min(width - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius);
        int y1 = Mathf.Min(height - 1, cy + radius);

        return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private static RectInt Union(RectInt first, RectInt second)
    {
        int xMin = Mathf.Min(first.xMin, second.xMin);
        int yMin = Mathf.Min(first.yMin, second.yMin);
        int xMax = Mathf.Max(first.xMax, second.xMax);
        int yMax = Mathf.Max(first.yMax, second.yMax);

        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static void CopyInto(
        float[,] destination, RectInt destinationBounds,
        float[,] source, RectInt sourceBounds)
    {
        int offsetX = sourceBounds.xMin - destinationBounds.xMin;
        int offsetY = sourceBounds.yMin - destinationBounds.yMin;

        for (int y = 0; y < sourceBounds.height; y++)
        {
            for (int x = 0; x < sourceBounds.width; x++)
            {
                destination[offsetY + y, offsetX + x] = source[y, x];
            }
        }
    }

    private static void CopyInto(
        float[,,] destination, RectInt destinationBounds,
        float[,,] source, RectInt sourceBounds)
    {
        int offsetX = sourceBounds.xMin - destinationBounds.xMin;
        int offsetY = sourceBounds.yMin - destinationBounds.yMin;
        int layers = source.GetLength(2);

        for (int y = 0; y < sourceBounds.height; y++)
        {
            for (int x = 0; x < sourceBounds.width; x++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    destination[offsetY + y, offsetX + x, layer] =
                        source[y, x, layer];
                }
            }
        }
    }

    private static bool AreEqual(float[,] first, float[,] second)
    {
        int height = first.GetLength(0);
        int width = first.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (first[y, x] != second[y, x])
                    return false;
            }
        }

        return true;
    }

    private static bool AreEqual(float[,,] first, float[,,] second)
    {
        int height = first.GetLength(0);
        int width = first.GetLength(1);
        int layers = first.GetLength(2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    if (first[y, x, layer] != second[y, x, layer])
                        return false;
                }
            }
        }

        return true;
    }

    private class TerrainSnapshot
    {
        public Terrain Terrain { get; }
        public RectInt HeightBounds { get; }
        public float[,] BeforeHeights { get; }
        public RectInt AlphaBounds { get; }
        public float[,,] BeforeAlphas { get; }

        private readonly float[,] afterHeights;
        private readonly float[,,] afterAlphas;

        public TerrainSnapshot(
            Terrain terrain,
            RectInt heightBounds,
            float[,] beforeHeights,
            float[,] afterHeights,
            RectInt alphaBounds,
            float[,,] beforeAlphas,
            float[,,] afterAlphas)
        {
            Terrain = terrain;
            HeightBounds = heightBounds;
            BeforeHeights = beforeHeights;
            this.afterHeights = afterHeights;
            AlphaBounds = alphaBounds;
            BeforeAlphas = beforeAlphas;
            this.afterAlphas = afterAlphas;
        }

        public void RestoreBefore()
        {
            Restore(BeforeHeights, BeforeAlphas);
        }

        public void RestoreAfter()
        {
            Restore(afterHeights, afterAlphas);
        }

        private void Restore(float[,] heights, float[,,] alphas)
        {
            TerrainData data = Terrain.terrainData;

            data.SetHeights(
                HeightBounds.x, HeightBounds.y, heights
            );

            data.SetAlphamaps(
                AlphaBounds.x, AlphaBounds.y, alphas
            );

            data.SyncHeightmap();
        }
    }
}