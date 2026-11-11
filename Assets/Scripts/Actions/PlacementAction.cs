using UnityEngine;

public class PlacementAction : IUndoableAction
{
    private readonly GameObject prefab;
    private readonly Vector3 position;
    private readonly Quaternion rotation;
    private readonly Vector3 scale;

    private GameObject instance;

    public PlacementAction(GameObject prefab, GameObject instance)
    {
        this.prefab = prefab;
        this.instance = instance;

        position = instance.transform.position;
        rotation = instance.transform.rotation;
        scale = instance.transform.localScale;
    }

    public void Undo()
    {
        if (instance == null)
            return;

        Object.Destroy(instance);
        instance = null;
    }

    public void Redo()
    {
        if (prefab == null || instance != null)
            return;

        instance = Object.Instantiate(prefab, position, rotation);
        instance.transform.localScale = scale;
    }
}