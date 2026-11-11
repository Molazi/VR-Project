using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterAction : IUndoableAction
{
    private readonly List<WaterState> before;
    private readonly List<WaterState> after;
    private readonly Action<IReadOnlyList<WaterState>> restore;

    public WaterAction(IReadOnlyList<WaterState> before, IReadOnlyList<WaterState> after, Action<IReadOnlyList<WaterState>> restore)
    {
        this.before = CloneStates(before);
        this.after = CloneStates(after);
        this.restore = restore;
    }

    public void Undo() => restore(before);
    public void Redo() => restore(after);

    private static List<WaterState> CloneStates(IReadOnlyList<WaterState> states)
    {
        List<WaterState> result = new();
        foreach (WaterState state in states) result.Add(state.Clone());
        return result;
    }
}

public class WaterState
{
    public Vector3 center;
    public float waterLevel;

    // Данные для сетки (Grid)
    public bool[,] mask;
    public Vector3 worldOrigin;
    public float cellSizeX;
    public float cellSizeZ;

    public WaterState(Vector3 center, float waterLevel, bool[,] mask, Vector3 worldOrigin, float cellSizeX, float cellSizeZ)
    {
        this.center = center;
        this.waterLevel = waterLevel;
        this.mask = mask;
        this.worldOrigin = worldOrigin;
        this.cellSizeX = cellSizeX;
        this.cellSizeZ = cellSizeZ;
    }

    public WaterState Clone()
    {
        bool[,] maskCopy = new bool[mask.GetLength(0), mask.GetLength(1)];
        Array.Copy(mask, maskCopy, mask.Length);
        return new WaterState(center, waterLevel, maskCopy, worldOrigin, cellSizeX, cellSizeZ);
    }
}