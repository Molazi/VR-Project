using UnityEngine;

public static class TerrainDeformer
{
    public static void Deform(Terrain terrain, Vector3 worldPos, float strength, float brushSize)
    {
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;
        Vector3 localPos = worldPos - terrain.transform.position;
        float normX = localPos.x / data.size.x;
        float normZ = localPos.z / data.size.z;
        int res = data.heightmapResolution;
        int cx = Mathf.RoundToInt(normX * (res - 1));
        int cy = Mathf.RoundToInt(normZ * (res - 1));
        int radius = Mathf.Clamp(Mathf.CeilToInt(brushSize / data.size.x * res), 1, 50);
        int x0 = Mathf.Max(0, cx - radius), x1 = Mathf.Min(res - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius), y1 = Mathf.Min(res - 1, cy + radius);
        int w = x1 - x0 + 1, h = y1 - y0 + 1;
        if (w <= 0 || h <= 0) return;
        float[,] heights = data.GetHeights(x0, y0, w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dist = Vector2.Distance(new Vector2(x0 + x, y0 + y), new Vector2(cx, cy));
                float influence = Mathf.Clamp01(1f - dist / radius);
                heights[y, x] += strength * influence;
            }
        data.SetHeights(x0, y0, heights);
        data.SyncHeightmap();
    }

    public static void PaintTexture(Terrain terrain, Vector3 worldPos, float brushSize, float strength, int layerIndex)
    {
        if (terrain == null) return;
        TerrainData data = terrain.terrainData;
        if (layerIndex < 0 || layerIndex >= data.alphamapLayers) return;
        Vector3 localPos = worldPos - terrain.transform.position;
        float normX = localPos.x / data.size.x;
        float normZ = localPos.z / data.size.z;
        int w = data.alphamapWidth, h = data.alphamapHeight;
        int cx = Mathf.RoundToInt(normX * (w - 1));
        int cy = Mathf.RoundToInt(normZ * (h - 1));
        int radius = Mathf.Clamp(Mathf.CeilToInt(brushSize / data.size.x * w), 1, 50);
        int x0 = Mathf.Max(0, cx - radius), x1 = Mathf.Min(w - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius), y1 = Mathf.Min(h - 1, cy + radius);
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        if (bw <= 0 || bh <= 0) return;
        float[,,] alphamaps = data.GetAlphamaps(x0, y0, bw, bh);
        for (int y = 0; y < bh; y++)
            for (int x = 0; x < bw; x++)
            {
                float dist = Vector2.Distance(new Vector2(x0 + x, y0 + y), new Vector2(cx, cy));
                float influence = Mathf.Clamp01(1f - dist / radius);
                float add = influence * strength * 0.1f;
                float sum = 0f;
                for (int l = 0; l < data.alphamapLayers; l++)
                {
                    float v = alphamaps[y, x, l];
                    v += (l == layerIndex) ? add : -add / (data.alphamapLayers - 1);
                    v = Mathf.Clamp01(v);
                    alphamaps[y, x, l] = v;
                    sum += v;
                }
                if (sum > 0.0001f)
                    for (int l = 0; l < data.alphamapLayers; l++)
                        alphamaps[y, x, l] /= sum;
            }
        data.SetAlphamaps(x0, y0, alphamaps);
    }
}