using System;
using System.Collections.Generic;
using UnityEngine;

public class PaintAction : IUndoableAction
{
    private readonly List<PaintSnapshot> snapshots = new();
    private PaintSnapshot mergedSnapshot;

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

        float[,,] before = data.GetAlphamaps(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        action();

        float[,,] after = data.GetAlphamaps(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        if (AreEqual(before, after))
            return;

        snapshots.Add(
            new PaintSnapshot(
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

        float[,,] before = data.GetAlphamaps(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            PaintSnapshot snapshot = snapshots[i];

            CopyInto(
                before,
                bounds,
                snapshot.Before,
                snapshot.Bounds
            );
        }

        float[,,] after = data.GetAlphamaps(
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );

        mergedSnapshot = new PaintSnapshot(
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

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;

        int cx =
            Mathf.RoundToInt(normX * (width - 1));

        int cy =
            Mathf.RoundToInt(normZ * (height - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(
                brushSize / data.size.x * width
            ),
            1,
            50
        );

        int x0 =
            Mathf.Max(0, cx - radius);

        int x1 =
            Mathf.Min(width - 1, cx + radius);

        int y0 =
            Mathf.Max(0, cy - radius);

        int y1 =
            Mathf.Min(height - 1, cy + radius);

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
        float[,,] destination,
        RectInt destinationBounds,
        float[,,] source,
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

        int layers =
            source.GetLength(2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    destination[
                        offsetY + y,
                        offsetX + x,
                        layer
                    ] = source[
                        y,
                        x,
                        layer
                    ];
                }
            }
        }
    }

    private static bool AreEqual(
        float[,,] first,
        float[,,] second)
    {
        int height =
            first.GetLength(0);

        int width =
            first.GetLength(1);

        int layers =
            first.GetLength(2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int layer = 0; layer < layers; layer++)
                {
                    if (first[y, x, layer] !=
                        second[y, x, layer])
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private class PaintSnapshot
    {
        public Terrain Terrain { get; }
        public RectInt Bounds { get; }
        public float[,,] Before { get; }

        private readonly float[,,] after;

        public PaintSnapshot(
            Terrain terrain,
            RectInt bounds,
            float[,,] before,
            float[,,] after)
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

        private void Restore(float[,,] alphamaps)
        {
            Terrain.terrainData.SetAlphamaps(
                Bounds.x,
                Bounds.y,
                alphamaps
            );
        }
    }
}