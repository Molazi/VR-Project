using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWaterMesh : MonoBehaviour
{
    public Material waterMaterial;
    public float uvScale = 1f;

    public void BuildFromBoundary(Vector3 center, Vector3[] boundaryPoints, float waterLevel)
    {
        Mesh mesh = CreateFlatDisk(center, boundaryPoints);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshCollider col = GetComponent<MeshCollider>();
        if (!col) col = gameObject.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = true;
        col.isTrigger = true;
        if (waterMaterial) GetComponent<MeshRenderer>().material = waterMaterial;
    }

    private Mesh CreateFlatDisk(Vector3 center, Vector3[] boundaryPoints)
    {
        Mesh mesh = new Mesh(); mesh.name = "WaterSurface";
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        verts.Add(Vector3.zero);
        List<Vector3> local = new List<Vector3>();
        foreach (var p in boundaryPoints)
        {
            Vector3 lp = p - center; lp.y = 0;
            local.Add(lp); verts.Add(lp);
        }

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in local)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }
        float rx = maxX - minX, rz = maxZ - minZ;
        if (rx < 0.001f) rx = 1; if (rz < 0.001f) rz = 1;

        uvs.Add(Vector2.zero);
        for (int i = 0; i < local.Count; i++)
            uvs.Add(new Vector2((local[i].x - minX) / rx * uvScale, (local[i].z - minZ) / rz * uvScale));
        Vector2 centerUV = Vector2.zero;
        for (int i = 1; i < uvs.Count; i++) centerUV += uvs[i];
        centerUV /= (uvs.Count - 1);
        uvs[0] = centerUV;

        int n = local.Count;
        for (int i = 1; i <= n; i++)
        {
            int next = (i == n) ? 1 : i + 1;
            tris.Add(0); tris.Add(next); tris.Add(i);
        }

        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        Vector3[] norms = new Vector3[verts.Count];
        for (int i = 0; i < norms.Length; i++) norms[i] = Vector3.up;
        mesh.normals = norms;
        mesh.RecalculateBounds();
        return mesh;
    }
}