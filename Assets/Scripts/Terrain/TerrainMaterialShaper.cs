using System.Collections.Generic;
using UnityEngine;

public static class TerrainMaterialShaper
{
    /// <summary>
    /// Вычисляет и применяет приращение высот на основе изменения альфамап.
    /// Вызывать ПОСЛЕ изменения альфамап и ПЕРЕД SetAlphamaps (или после — с newAlphas в руках).
    /// </summary>
    public static void ApplyAlphaHeightDelta(
        Terrain terrain,
        RectInt alphaBounds,
        float[,,] oldAlphas,     // alphas в границах alphaBounds ДО
        float[,,] newAlphas,     // alphas в границах alphaBounds ПОСЛЕ
        IReadOnlyList<TerrainMaterialProfile> profiles)
    {
        if (terrain == null || oldAlphas == null || newAlphas == null || profiles == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 pos = terrain.transform.position;
        int hmRes = data.heightmapResolution;
        int amW = data.alphamapWidth;
        int amH = data.alphamapHeight;
        int amLayers = data.alphamapLayers;
        int profileCount = Mathf.Min(amLayers, profiles.Count);
        if (profileCount == 0) return;

        // Мировые границы, покрытые alphaBounds
        float wx0 = pos.x + (float)alphaBounds.xMin / (amW - 1) * data.size.x;
        float wx1 = pos.x + (float)alphaBounds.xMax / (amW - 1) * data.size.x;
        float wz0 = pos.z + (float)alphaBounds.yMin / (amH - 1) * data.size.z;
        float wz1 = pos.z + (float)alphaBounds.yMax / (amH - 1) * data.size.z;

        // Переводим в координаты heightmap
        int hx0 = Mathf.Clamp(Mathf.FloorToInt((wx0 - pos.x) / data.size.x * (hmRes - 1)), 0, hmRes - 1);
        int hx1 = Mathf.Clamp(Mathf.CeilToInt((wx1 - pos.x) / data.size.x * (hmRes - 1)), 0, hmRes - 1);
        int hz0 = Mathf.Clamp(Mathf.FloorToInt((wz0 - pos.z) / data.size.z * (hmRes - 1)), 0, hmRes - 1);
        int hz1 = Mathf.Clamp(Mathf.CeilToInt((wz1 - pos.z) / data.size.z * (hmRes - 1)), 0, hmRes - 1);

        int hw = hx1 - hx0 + 1;
        int hh = hz1 - hz0 + 1;
        if (hw <= 0 || hh <= 0) return;

        float[,] heights = data.GetHeights(hx0, hz0, hw, hh);
        bool changed = false;

        for (int y = 0; y < hh; y++)
        {
            int hz = hz0 + y;
            float wz = pos.z + (float)hz / (hmRes - 1) * data.size.z;
            float amZ = (wz - pos.z) / data.size.z * (amH - 1);

            for (int x = 0; x < hw; x++)
            {
                int hx = hx0 + x;
                float wx = pos.x + (float)hx / (hmRes - 1) * data.size.x;
                float amX = (wx - pos.x) / data.size.x * (amW - 1);

                // Билинейно сэмплируем веса старого/нового альфамап, сразу считаем Δ
                float delta = 0f;
                for (int l = 0; l < profileCount; l++)
                {
                    float wOld = SampleBilinear(oldAlphas, alphaBounds, amX, amZ, l);
                    float wNew = SampleBilinear(newAlphas, alphaBounds, amX, amZ, l);
                    float dw = wNew - wOld;
                    if (Mathf.Abs(dw) < 1e-6f) continue;

                    float n = TerrainNoise.Sample(wx, wz, profiles[l]);
                    delta += dw * n * profiles[l].amplitude;
                }

                if (Mathf.Abs(delta) > 1e-6f)
                {
                    // нормируем в 0..1 для SetHeights
                    heights[y, x] += delta / data.size.y;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            data.SetHeights(hx0, hz0, heights);
            data.SyncHeightmap();
        }
    }

    static float SampleBilinear(float[,,] alphas, RectInt bounds, float amX, float amZ, int layer)
    {
        float lx = amX - bounds.xMin;
        float lz = amZ - bounds.yMin;

        int x0 = Mathf.FloorToInt(lx);
        int z0 = Mathf.FloorToInt(lz);
        int x1 = x0 + 1;
        int z1 = z0 + 1;
        float fx = lx - x0;
        float fz = lz - z0;

        x0 = Mathf.Clamp(x0, 0, bounds.width - 1);
        x1 = Mathf.Clamp(x1, 0, bounds.width - 1);
        z0 = Mathf.Clamp(z0, 0, bounds.height - 1);
        z1 = Mathf.Clamp(z1, 0, bounds.height - 1);

        float w00 = alphas[z0, x0, layer];
        float w10 = alphas[z0, x1, layer];
        float w01 = alphas[z1, x0, layer];
        float w11 = alphas[z1, x1, layer];

        return Mathf.Lerp(
            Mathf.Lerp(w00, w10, fx),
            Mathf.Lerp(w01, w11, fx),
            fz);
    }
}