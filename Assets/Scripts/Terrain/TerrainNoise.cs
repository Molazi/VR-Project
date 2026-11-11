using UnityEngine;

public enum NoiseType { Perlin, Ridged, Billow }

public static class TerrainNoise
{
    public static float Sample(float worldX, float worldZ, TerrainMaterialProfile p)
    {
        // Применяем растяжение (для барханов)
        float fx = worldX * p.frequency * p.noiseScale.x;
        float fz = worldZ * p.frequency * p.noiseScale.y;
        
        float sum = 0f, amp = 1f, norm = 0f;
        float freq = 1f;
        int seedX = p.seed * 13;
        int seedZ = p.seed * 7;

        for (int i = 0; i < Mathf.Max(1, p.octaves); i++)
        {
            float px = fx * freq + seedX;
            float pz = fz * freq + seedZ;
            float n = Mathf.PerlinNoise(px, pz);

            switch (p.noiseType)
            {
                case NoiseType.Ridged:
                    n = 1f - Mathf.Abs(2f * n - 1f);
                    n = Mathf.Pow(n, Mathf.Max(0.1f, p.sharpness));
                    break;
                case NoiseType.Billow:
                    n = Mathf.Abs(2f * n - 1f); // Пузырчатый шум
                    break;
            }

            sum += n * amp;
            norm += amp;
            amp *= p.gain;
            freq *= p.lacunarity;
        }

        float result = norm > 0f ? sum / norm : 0f;
        return (result - 0.5f) * 2f; // Значения от -1 до 1
    }
}