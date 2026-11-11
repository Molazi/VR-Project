using UnityEngine;

[CreateAssetMenu(fileName = "MaterialProfiles", menuName = "Terrain/Material Profile Library")]
public class MaterialProfileLibraryAsset : ScriptableObject
{
    public MaterialProfileLibrary library = new MaterialProfileLibrary();

    void OnEnable()
    {
        // Если библиотека пуста или первый элемент пустой (без имени) — заполняем дефолтными 4 профилями
        if (library == null || library.profiles == null || library.profiles.Count == 0 || string.IsNullOrEmpty(library.profiles[0].name))
        {
            ResetToDefault();
        }
    }

    [ContextMenu("Reset to Default")]
    public void ResetToDefault()
    {
        library = MaterialProfileLibrary.CreateDefault();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }
}