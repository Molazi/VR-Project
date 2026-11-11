using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWaterMesh : MonoBehaviour
{
    [Header("Material")]
    public Material waterMaterial;
    public float uvScale = 1f;
    [Header("Water Thickness")]
    [Tooltip("Насколько глубоко вода уходит под землю")]
    public float skirtDepth = 5f;
    [Header("Edge Smoothing")]
    [Tooltip("Количество итераций сглаживания краев воды")]
    public int smoothingIterations = 5;
    [Range(0f, 1f)] public float smoothingStrength = 0.5f;

    public void BuildFromMask(bool[,] mask, Vector3 worldOrigin, float cellSizeX, float cellSizeZ, float waterLevel)
    {
        Mesh mesh = new Mesh { name = "WaterSurface" };
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        Dictionary<Vector2Int, int> topVertIndices = new Dictionary<Vector2Int, int>();

        // 1. Генерация верхней поверхности
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[x, y]) continue;

                Vector2Int[] corners = {
                    new Vector2Int(x, y), new Vector2Int(x + 1, y),
                    new Vector2Int(x + 1, y + 1), new Vector2Int(x, y + 1)
                };

                int[] indices = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!topVertIndices.TryGetValue(corners[i], out indices[i]))
                    {
                        Vector3 worldPos = worldOrigin + new Vector3(corners[i].x * cellSizeX, 0, corners[i].y * cellSizeZ);
                        worldPos.y = waterLevel;
                        Vector3 localPos = transform.InverseTransformPoint(worldPos);

                        indices[i] = verts.Count;
                        topVertIndices[corners[i]] = indices[i];
                        verts.Add(localPos);
                        uvs.Add(new Vector2(localPos.x * uvScale * 0.1f, localPos.z * uvScale * 0.1f));
                    }
                }

                tris.Add(indices[0]); tris.Add(indices[2]); tris.Add(indices[1]);
                tris.Add(indices[0]); tris.Add(indices[3]); tris.Add(indices[2]);
            }
        }

        // 2. Сглаживание границ (Laplacian Smoothing)
        if (smoothingIterations > 0)
        {
            for (int iter = 0; iter < smoothingIterations; iter++)
            {
                Dictionary<Vector2Int, Vector3> newPositions = new Dictionary<Vector2Int, Vector3>();

                foreach (var kvp in topVertIndices)
                {
                    Vector2Int v = kvp.Key;
                    int vx = v.x;
                    int vy = v.y;

                    // Проверяем, является ли вершина граничной
                    int waterCount = 0;
                    if (vx > 0 && vy > 0 && mask[vx - 1, vy - 1]) waterCount++;
                    if (vx < w && vy > 0 && mask[vx, vy - 1]) waterCount++;
                    if (vx > 0 && vy < h && mask[vx - 1, vy]) waterCount++;
                    if (vx < w && vy < h && mask[vx, vy]) waterCount++;

                    if (waterCount > 0 && waterCount < 4) // Если это край
                    {
                        Vector3 sum = Vector3.zero;
                        int count = 0;

                        if (topVertIndices.TryGetValue(new Vector2Int(vx - 1, vy), out int idx1)) { sum += verts[idx1]; count++; }
                        if (topVertIndices.TryGetValue(new Vector2Int(vx + 1, vy), out int idx2)) { sum += verts[idx2]; count++; }
                        if (topVertIndices.TryGetValue(new Vector2Int(vx, vy - 1), out int idx3)) { sum += verts[idx3]; count++; }
                        if (topVertIndices.TryGetValue(new Vector2Int(vx, vy + 1), out int idx4)) { sum += verts[idx4]; count++; }

                        if (count > 0)
                        {
                            Vector3 avg = sum / count;
                            Vector3 current = verts[kvp.Value];
                            newPositions[v] = new Vector3(
                                Mathf.Lerp(current.x, avg.x, smoothingStrength),
                                current.y,
                                Mathf.Lerp(current.z, avg.z, smoothingStrength)
                            );
                        }
                    }
                }

                foreach (var kvp in newPositions) verts[topVertIndices[kvp.Key]] = kvp.Value;
            }
        }

        // 3. Генерация "юбки" по краям (уходит на 5 метров вглубь)
        int vertexCountBeforeSkirt = verts.Count;
        

        foreach (var kvp in topVertIndices)
        {
            Vector3 p = verts[kvp.Value];
            p.y -= skirtDepth;
            verts.Add(p);
            uvs.Add(new Vector2(p.x * uvScale * 0.1f, p.z * uvScale * 0.1f));
        }

        // Стенки юбки
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[x, y]) continue;

                if (x == 0 || !mask[x - 1, y]) AddWall(tris, topVertIndices, new Vector2Int(x, y), new Vector2Int(x, y + 1), vertexCountBeforeSkirt);
                if (x == w - 1 || !mask[x + 1, y]) AddWall(tris, topVertIndices, new Vector2Int(x + 1, y + 1), new Vector2Int(x + 1, y), vertexCountBeforeSkirt);
                if (y == 0 || !mask[x, y - 1]) AddWall(tris, topVertIndices, new Vector2Int(x + 1, y), new Vector2Int(x, y), vertexCountBeforeSkirt);
                if (y == h - 1 || !mask[x, y + 1]) AddWall(tris, topVertIndices, new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1), vertexCountBeforeSkirt);
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = mesh;

        MeshCollider col = GetComponent<MeshCollider>();
        if (!col) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = null;
        col.sharedMesh = mesh;
        col.convex = true;
        col.isTrigger = true;

        if (waterMaterial) GetComponent<MeshRenderer>().material = waterMaterial;
    }

    private void AddWall(List<int> tris, Dictionary<Vector2Int, int> indices, Vector2Int v1, Vector2Int v2, int skirtOffset)
    {
        if (!indices.ContainsKey(v1) || !indices.ContainsKey(v2)) return;

        int top1 = indices[v1];
        int top2 = indices[v2];
        int bot1 = top1 + skirtOffset;
        int bot2 = top2 + skirtOffset;

        tris.Add(top1); tris.Add(bot1); tris.Add(top2);
        tris.Add(top2); tris.Add(bot1); tris.Add(bot2);
    }
}