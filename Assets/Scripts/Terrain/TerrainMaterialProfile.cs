using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TerrainMaterialProfile
{
    [Header("Метаданные")]
    public string name = "Новый материал";
    [TextArea] public string description = "";

    // ========== ВИЗУАЛЬНЫЕ ПАРАМЕТРЫ (TerrainLayer) ==========
    [Header("Визуал (TerrainLayer)")]
    public Texture2D diffuseTexture;
    public Texture2D normalMapTexture;
    public Vector2 tileSize = new Vector2(15, 15);
    public Vector2 tileOffset = Vector2.zero;
    [Range(0f, 1f)] public float smoothness = 0.2f;
    [Range(0f, 1f)] public float metallic = 0f;
    public Color specular = new Color(0.2f, 0.2f, 0.2f);
    [Range(0f, 2f)] public float normalScale = 1f;

    // ========== ГЕОМЕТРИЧЕСКИЕ ПАРАМЕТРЫ (шум) ==========
    [Header("Геометрия (шум деформации)")]
    public NoiseType noiseType = NoiseType.Perlin;
    [Tooltip("Высота неровностей в метрах")] public float amplitude = 0.1f;
    [Tooltip("Частота шума, 1/м")] public float frequency = 0.1f;
    public int seed = 1;
    [Range(1, 8)] public int octaves = 4;
    public float lacunarity = 2f;
    public float gain = 0.5f;
    [Tooltip("Резкость пиков для Ridged (1 = обычный, >1 = острее)")] public float sharpness = 1f;

    public TerrainMaterialProfile Clone()
    {
        return (TerrainMaterialProfile)MemberwiseClone();
    }

    public void ApplyToLayer(TerrainLayer layer)
    {
        if (layer == null) return;
        layer.name = name;
        if (diffuseTexture) layer.diffuseTexture = diffuseTexture;

        // ИСПРАВЛЕНИЕ: Не затираем normal map пустотой, если её нет
        if (normalMapTexture) layer.normalMapTexture = normalMapTexture;

        layer.tileSize = tileSize;
        layer.tileOffset = tileOffset;
        layer.smoothness = smoothness;
        layer.metallic = metallic;
        layer.specular = specular;
        layer.normalScale = normalScale;
    }
}

[System.Serializable]
public class MaterialProfileLibrary
{
    public List<TerrainMaterialProfile> profiles = new();

    public TerrainMaterialProfile Get(int index)
    {
        return (index >= 0 && index < profiles.Count) ? profiles[index] : null;
    }

    public void ApplyAllToTerrain(Terrain terrain)
    {
        if (terrain == null) return;
        TerrainLayer[] layers = terrain.terrainData.terrainLayers;
        int count = Mathf.Min(layers.Length, profiles.Count);
        for (int i = 0; i < count; i++)
            profiles[i].ApplyToLayer(layers[i]);
    }

    public static MaterialProfileLibrary CreateDefault()
    {
        var lib = new MaterialProfileLibrary();

        // ВАЖНО: Порядок должен строго совпадать с порядком слоёв в Terrain (Terrain Layers).
        // По умолчанию в вашем проекте: 0 - Трава, 1 - Земля, 2 - Камень.
        // Мы добавляем 4-й профиль (Песок), его нужно будет добавить 4-м слоем в террейн.

        lib.profiles.Add(new TerrainMaterialProfile
        {
            name = "Трава",
            description = "Слабые высокочастотные бугорки.",
            noiseType = NoiseType.Perlin,
            amplitude = 0.05f,
            frequency = 0.35f,
            seed = 44,
            octaves = 3,
            lacunarity = 2.2f,
            gain = 0.45f,
            smoothness = 0.25f,
            metallic = 0f,
            normalScale = 0.6f,
            tileSize = new Vector2(20, 20),
        });

        lib.profiles.Add(new TerrainMaterialProfile
        {
            name = "Земля",
            description = "Небольшие неровности средней частоты.",
            noiseType = NoiseType.Perlin,
            amplitude = 0.15f,
            frequency = 0.15f,
            seed = 33,
            octaves = 4,
            lacunarity = 2f,
            gain = 0.5f,
            smoothness = 0.2f,
            metallic = 0f,
            normalScale = 0.9f,
            tileSize = new Vector2(15, 15),
        });

        lib.profiles.Add(new TerrainMaterialProfile
        {
            name = "Камень",
            description = "Жёсткий, рваный рельеф. Ridged-шум с высокой амплитудой.",
            noiseType = NoiseType.Ridged,
            amplitude = 0.8f,
            frequency = 0.12f,
            seed = 11,
            octaves = 5,
            lacunarity = 2.1f,
            gain = 0.55f,
            sharpness = 1.8f,
            smoothness = 0.15f,
            metallic = 0.05f,
            normalScale = 1.4f,
            tileSize = new Vector2(10, 10),
        });

        lib.profiles.Add(new TerrainMaterialProfile
        {
            name = "Песок",
            description = "Плавные волны, низкая амплитуда.",
            noiseType = NoiseType.Perlin,
            amplitude = 0.08f,
            frequency = 0.05f,
            seed = 22,
            octaves = 3,
            lacunarity = 2f,
            gain = 0.5f,
            smoothness = 0.4f,
            metallic = 0f,
            normalScale = 0.5f,
            tileSize = new Vector2(25, 25),
        });

        return lib;
    }

    public string GetDocumentationTable()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Таблица параметров материалов ===\n");
        sb.AppendLine("--- ОБЩИЕ ПАРАМЕТРЫ ---");
        sb.AppendLine("name           : Название материала (отображается в UI)");
        sb.AppendLine("description    : Описание материала\n");
        sb.AppendLine("--- ВИЗУАЛ (TerrainLayer) ---");
        sb.AppendLine("diffuseTexture : Основная текстура (albedo) материала");
        sb.AppendLine("normalMapTexture: Карта нормалей для рельефных деталей");
        sb.AppendLine("tileSize       : Размер тайла текстуры в метрах (X, Y)");
        sb.AppendLine("smoothness     : Гладкость/блеск [0..1]. Высокая = отражения");
        sb.AppendLine("normalScale    : Усиление карты нормалей. 1 = обычная, >1 = резче\n");
        sb.AppendLine("--- ГЕОМЕТРИЯ (шум деформации) ---");
        sb.AppendLine("noiseType      : Тип шума: Perlin / Ridged / Billow");
        sb.AppendLine("amplitude      : Амплитуда в метрах. Высота неровностей.");
        sb.AppendLine("frequency      : Частота в 1/м. Больше = мельче детали.");
        sb.AppendLine("seed           : Сид для уникальности шума.");
        sb.AppendLine("octaves        : Кол-во октав fBm. Больше = мельче детализация.");
        return sb.ToString();
    }
}