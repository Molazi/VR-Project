using UnityEngine;

public static class TerrainDeformer
{
    public static void Deform(
        Terrain terrain,
        Vector3 worldPos,
        float strength,
        float brushSize,
        float minTerrainHeight,
        float maxTerrainHeight,
        float smoothingStrength)
    {
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 localPos = worldPos - terrain.transform.position;

        float normX = localPos.x / data.size.x;
        float normZ = localPos.z / data.size.z;

        int res = data.heightmapResolution;
        int cx = Mathf.RoundToInt(normX * (res - 1));
        int cy = Mathf.RoundToInt(normZ * (res - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(brushSize / data.size.x * (res - 1)),
            1,
            res / 2
        );

        int x0 = Mathf.Max(0, cx - radius);
        int x1 = Mathf.Min(res - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius);
        int y1 = Mathf.Min(res - 1, cy + radius);

        int w = x1 - x0 + 1;
        int h = y1 - y0 + 1;

        if (w <= 0 || h <= 0)
            return;

        float[,] heights = data.GetHeights(x0, y0, w, h);

        float minHeight = Mathf.Clamp01(minTerrainHeight / data.size.y);
        float maxHeight = Mathf.Clamp01(maxTerrainHeight / data.size.y);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dist = Vector2.Distance(
                    new Vector2(x0 + x, y0 + y),
                    new Vector2(cx, cy)
                );

                float normalizedDistance = Mathf.Clamp01(1f - dist / radius);
                float influence = Mathf.SmoothStep(0f, 1f, normalizedDistance);
                float currentHeight = heights[y, x];

                if (strength < 0f && currentHeight <= minHeight)
                    continue;

                if (strength > 0f && currentHeight >= maxHeight)
                    continue;

                float newHeight = currentHeight + strength * influence;

                if (strength < 0f)
                {
                    newHeight = Mathf.Max(newHeight, minHeight);
                }
                else if (strength > 0f)
                {
                    newHeight = Mathf.Min(newHeight, maxHeight);
                }

                heights[y, x] = newHeight;
            }
        }

        if (smoothingStrength > 0f)
        {
            float clampedSmoothing = Mathf.Clamp01(smoothingStrength);
            int passCount = Mathf.CeilToInt(clampedSmoothing * 3f);

            for (int pass = 0; pass < passCount; pass++)
            {
                float[,] smoothedHeights = new float[h, w];

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        smoothedHeights[y, x] = heights[y, x];
                    }
                }

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dist = Vector2.Distance(
                            new Vector2(x0 + x, y0 + y),
                            new Vector2(cx, cy)
                        );

                        if (dist > radius)
                            continue;

                        float normalizedDistance = Mathf.Clamp01(1f - dist / radius);
                        float brushInfluence = Mathf.SmoothStep(
                            0f,
                            1f,
                            normalizedDistance
                        );

                        float sum = 0f;
                        int count = 0;

                        for (int offsetY = -1; offsetY <= 1; offsetY++)
                        {
                            for (int offsetX = -1; offsetX <= 1; offsetX++)
                            {
                                int neighborX = x + offsetX;
                                int neighborY = y + offsetY;

                                if (neighborX < 0 || neighborX >= w ||
                                    neighborY < 0 || neighborY >= h)
                                {
                                    continue;
                                }

                                sum += heights[neighborY, neighborX];
                                count++;
                            }
                        }

                        float averageHeight = sum / count;
                        float smoothAmount = clampedSmoothing * brushInfluence;

                        float smoothedHeight = Mathf.Lerp(
                            heights[y, x],
                            averageHeight,
                            smoothAmount
                        );

                        if (strength > 0f)
                        {
                            smoothedHeight = Mathf.Min(
                                smoothedHeight,
                                maxHeight
                            );
                        }
                        else if (strength < 0f)
                        {
                            smoothedHeight = Mathf.Max(
                                smoothedHeight,
                                minHeight
                            );
                        }

                        smoothedHeights[y, x] = smoothedHeight;
                    }
                }

                heights = smoothedHeights;
            }
        }

        data.SetHeights(x0, y0, heights);
        data.SyncHeightmap();
    }

    public static void PaintTexture(
        Terrain terrain,
        Vector3 worldPos,
        float brushSize,
        float strength,
        int layerIndex)
    {
        if (terrain == null) return;

        TerrainData data = terrain.terrainData;

        if (layerIndex < 0 || layerIndex >= data.alphamapLayers)
            return;

        Vector3 localPos = worldPos - terrain.transform.position;

        float normX = localPos.x / data.size.x;
        float normZ = localPos.z / data.size.z;

        int w = data.alphamapWidth;
        int h = data.alphamapHeight;

        int cx = Mathf.RoundToInt(normX * (w - 1));
        int cy = Mathf.RoundToInt(normZ * (h - 1));

        int radius = Mathf.Clamp(
            Mathf.CeilToInt(brushSize / data.size.x * w),
            1,
            50
        );

        int x0 = Mathf.Max(0, cx - radius);
        int x1 = Mathf.Min(w - 1, cx + radius);
        int y0 = Mathf.Max(0, cy - radius);
        int y1 = Mathf.Min(h - 1, cy + radius);

        int bw = x1 - x0 + 1;
        int bh = y1 - y0 + 1;

        if (bw <= 0 || bh <= 0)
            return;

        float[,,] alphamaps = data.GetAlphamaps(x0, y0, bw, bh);

        for (int y = 0; y < bh; y++)
        {
            for (int x = 0; x < bw; x++)
            {
                float dist = Vector2.Distance(
                    new Vector2(x0 + x, y0 + y),
                    new Vector2(cx, cy)
                );

                float influence = Mathf.Clamp01(1f - dist / radius);
                float add = influence * strength * 0.1f;
                float sum = 0f;

                for (int l = 0; l < data.alphamapLayers; l++)
                {
                    float value = alphamaps[y, x, l];

                    value += (l == layerIndex)
                        ? add
                        : -add / (data.alphamapLayers - 1);

                    value = Mathf.Clamp01(value);

                    alphamaps[y, x, l] = value;
                    sum += value;
                }

                if (sum > 0.0001f)
                {
                    for (int l = 0; l < data.alphamapLayers; l++)
                    {
                        alphamaps[y, x, l] /= sum;
                    }
                }
            }
        }

        data.SetAlphamaps(x0, y0, alphamaps);
    }
}