using System;
using System.Collections.Generic;
using UnityEngine;

public class WaterAction : IUndoableAction
{
    private readonly List<WaterState> before;
    private readonly List<WaterState> after;
    private readonly Action<IReadOnlyList<WaterState>> restore;

    public WaterAction(
        IReadOnlyList<WaterState> before,
        IReadOnlyList<WaterState> after,
        Action<IReadOnlyList<WaterState>> restore)
    {
        this.before = CloneStates(before);
        this.after = CloneStates(after);
        this.restore = restore;
    }

    public void Undo()
    {
        restore(before);
    }

    public void Redo()
    {
        restore(after);
    }

    private static List<WaterState> CloneStates(
        IReadOnlyList<WaterState> states)
    {
        List<WaterState> result = new();

        foreach (WaterState state in states)
        {
            result.Add(state.Clone());
        }

        return result;
    }
}

public class WaterState
{
    public Vector3 center;
    public Vector3[] contour;
    public float waterLevel;

    public WaterState(
        Vector3 center,
        Vector3[] contour,
        float waterLevel)
    {
        this.center = center;
        this.contour = contour;
        this.waterLevel = waterLevel;
    }

    public WaterState Clone()
    {
        Vector3[] contourCopy =
            new Vector3[contour.Length];

        Array.Copy(
            contour,
            contourCopy,
            contour.Length
        );

        return new WaterState(
            center,
            contourCopy,
            waterLevel
        );
    }
}