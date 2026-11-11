using UnityEngine;
using System.Collections.Generic;

public static class WaterGenerator
{
    public struct Result
    {
        public bool success;
        public float waterLevel;
        public bool[,] mask;
        public Vector3 origin;
        public float cellSize;
    }

    public static Result Generate(Terrain terrain, Vector3 clickPoint, float gridStep = 0.03f, float maxRadius = 35f)
    {
        Result res = new Result();
        res.success = false;

        if (terrain == null) return res;

        int size = Mathf.CeilToInt(maxRadius / gridStep);
        int w = size * 2 + 1;
        int h = size * 2 + 1;

        float[,] heights = new float[w, h];
        int startX = size;
        int startZ = size;

        // Собираем высоты вокруг клика с мелким шагом
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector3 worldPos = clickPoint + new Vector3((x - size) * gridStep, 0, (z - size) * gridStep);
                heights[x, z] = terrain.SampleHeight(worldPos) + terrain.transform.position.y;
            }
        }

        // Ищем уровень воды
        float waterLevel = FindWaterLevel(heights, startX, startZ, out bool hitEdge);
        if (!hitEdge) return res;

        // Опускаем воду на 2 см ниже края, чтобы она не переливалась
        waterLevel -= 0.02f;

        res.waterLevel = waterLevel;
        res.mask = new bool[w, h];
        res.origin = clickPoint - new Vector3(size * gridStep, 0, size * gridStep);
        res.cellSize = gridStep;

        // Заливаем яму (BFS)
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        bool[,] visited = new bool[w, h];

        queue.Enqueue(new Vector2Int(startX, startZ));
        visited[startX, startZ] = true;

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int curr = queue.Dequeue();

            // Если стенка выше уровня воды — останавливаемся
            if (heights[curr.x, curr.y] > waterLevel) continue;

            res.mask[curr.x, curr.y] = true;

            for (int i = 0; i < 4; i++)
            {
                int nx = curr.x + dx[i];
                int nz = curr.y + dz[i];
                if (nx >= 0 && nx < w && nz >= 0 && nz < h && !visited[nx, nz])
                {
                    visited[nx, nz] = true;
                    queue.Enqueue(new Vector2Int(nx, nz));
                }
            }
        }

        res.success = true;
        return res;
    }

    private static float FindWaterLevel(float[,] heights, int startX, int startZ, out bool hitEdge)
    {
        hitEdge = false;
        int w = heights.GetLength(0);
        int h = heights.GetLength(1);

        float[,] bottlenecks = new float[w, h];
        bool[,] visited = new bool[w, h];

        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                bottlenecks[x, z] = float.PositiveInfinity;

        var heap = new MinHeap(w * h);
        bottlenecks[startX, startZ] = heights[startX, startZ];
        heap.Push(startX + startZ * w, heights[startX, startZ]);

        int[] dx = { 1, -1, 0, 0 };
        int[] dz = { 0, 0, 1, -1 };

        while (heap.TryPop(out int idx, out float b))
        {
            int cx = idx % w;
            int cz = idx / w;

            if (visited[cx, cz]) continue;
            visited[cx, cz] = true;

            if (cx == 0 || cx == w - 1 || cz == 0 || cz == h - 1)
            {
                hitEdge = true;
                return b;
            }

            for (int i = 0; i < 4; i++)
            {
                int nx = cx + dx[i];
                int nz = cz + dz[i];
                if (nx < 0 || nx >= w || nz < 0 || nz >= h) continue;
                if (visited[nx, nz]) continue;

                float nHeight = heights[nx, nz];
                float nb = Mathf.Max(b, nHeight);

                if (nb < bottlenecks[nx, nz])
                {
                    bottlenecks[nx, nz] = nb;
                    heap.Push(nx + nz * w, nb);
                }
            }
        }
        return float.PositiveInfinity;
    }

    private class MinHeap
    {
        private readonly List<(int idx, float pri)> data;
        public MinHeap(int capacity) { data = new List<(int idx, float pri)>(capacity); }
        public void Push(int idx, float pri)
        {
            data.Add((idx, pri));
            int i = data.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (data[p].pri <= data[i].pri) break;
                (data[p], data[i]) = (data[i], data[p]);
                i = p;
            }
        }
        public bool TryPop(out int idx, out float pri)
        {
            if (data.Count == 0) { idx = 0; pri = 0; return false; }
            (idx, pri) = data[0];
            data[0] = data[^1];
            data.RemoveAt(data.Count - 1);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = i * 2 + 2, m = i;
                if (l < data.Count && data[l].pri < data[m].pri) m = l;
                if (r < data.Count && data[r].pri < data[m].pri) m = r;
                if (m == i) break;
                (data[i], data[m]) = (data[m], data[i]);
                i = m;
            }
            return true;
        }
    }
}