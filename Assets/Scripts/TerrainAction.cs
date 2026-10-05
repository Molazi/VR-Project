using System;
using System.Collections.Generic;
using UnityEngine;

public class TerrainAction : IUndoableAction
{
    private readonly List<TerrainSnapshot> snapshots = new();
    private TerrainSnapshot mergedSnapshot;

    public bool HasChanges =>
        mergedSnapshot != null || snapshots.Count > 0;

    public void Record(
        Terrain terrain,
        Vector3 worldPos,
        float brushSize,
        Action action)
    {
        RectInt bounds = GetBounds(
            terrain,
            worldPos,
            brushSize
        );

        TerrainData data = terrain.terrainData;

        float[,] before = data.GetHeights(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        action();

        float[,] after = data.GetHeights(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        if (AreEqual(before, after))
            return;

        snapshots.Add(
            new TerrainSnapshot(
                terrain,
                bounds,
                before,
                after
            )
        );
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
        RectInt bounds = snapshots[0].Bounds;

        for (int i = 1; i < snapshots.Count; i++)
        {
            bounds = Union(
                bounds,
                snapshots[i].Bounds
            );
        }

        TerrainData data = terrain.terrainData;

        float[,] before = data.GetHeights(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            TerrainSnapshot snapshot = snapshots[i];

            CopyInto(
                before,
                bounds,
                snapshot.Before,
                snapshot.Bounds
            );
        }

        float[,] after = data.GetHeights(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        mergedSnapshot = new TerrainSnapshot(
            terrain,
            bounds,
            before,
            after
        );

        snapshots.Clear();
    }

    public void Undo()
    {
        if (mergedSnapshot == null)
            return;

        mergedSnapshot.RestoreBefore();
    }

    public void Redo()
    {
        if (mergedSnapshot == null)
            return;

        mergedSnapshot.RestoreAfter();
    }

    private static RectInt GetBounds(
        Terrain terrain,
        Vector3 worldPos,
        float brushSize)
    {
        TerrainData data = terrain.terrainData;
        Vector3 localPos =
            worldPos - terrain.transform.position;

        float normX =
            localPos.x / data.size.x;

        float normZ =
            localPos.z / data.size.z;

        int res =
            data.heightmapResolution;

        int cx =
            Mathf.RoundToInt(normX * (res - 1));

        int cy =
            Mathf.RoundToInt(normZ * (res - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(
                brushSize / data.size.x * (res - 1)
            ),
            1,
            res / 2
        );

        int x0 =
            Mathf.Max(0, cx - radius);

        int x1 =
            Mathf.Min(res - 1, cx + radius);

        int y0 =
            Mathf.Max(0, cy - radius);

        int y1 =
            Mathf.Min(res - 1, cy + radius);

        return new RectInt(
            x0,
            y0,
            x1 - x0 + 1,
            y1 - y0 + 1
        );
    }

    private static RectInt Union(
        RectInt first,
        RectInt second)
    {
        int xMin =
            Mathf.Min(first.xMin, second.xMin);

        int yMin =
            Mathf.Min(first.yMin, second.yMin);

        int xMax =
            Mathf.Max(first.xMax, second.xMax);

        int yMax =
            Mathf.Max(first.yMax, second.yMax);

        return new RectInt(
            xMin,
            yMin,
            xMax - xMin,
            yMax - yMin
        );
    }

    private static void CopyInto(
        float[,] destination,
        RectInt destinationBounds,
        float[,] source,
        RectInt sourceBounds)
    {
        int offsetX =
            sourceBounds.xMin -
            destinationBounds.xMin;

        int offsetY =
            sourceBounds.yMin -
            destinationBounds.yMin;

        int width =
            sourceBounds.width;

        int height =
            sourceBounds.height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                destination[
                    offsetY + y,
                    offsetX + x
                ] = source[y, x];
            }
        }
    }

    private static bool AreEqual(
        float[,] first,
        float[,] second)
    {
        int height =
            first.GetLength(0);

        int width =
            first.GetLength(1);

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

    private class TerrainSnapshot
    {
        public Terrain Terrain { get; }
        public RectInt Bounds { get; }
        public float[,] Before { get; }

        private readonly float[,] after;

        public TerrainSnapshot(
            Terrain terrain,
            RectInt bounds,
            float[,] before,
            float[,] after)
        {
            Terrain = terrain;
            Bounds = bounds;
            Before = before;
            this.after = after;
        }

        public void RestoreBefore()
        {
            Restore(Before);
        }

        public void RestoreAfter()
        {
            Restore(after);
        }

        private void Restore(float[,] heights)
        {
            TerrainData data =
                Terrain.terrainData;

            data.SetHeights(
                Bounds.x,
                Bounds.y,
                heights
            );

            data.SyncHeightmap();
        }
    }
}
