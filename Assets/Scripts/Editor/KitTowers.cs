using UnityEditor;
using UnityEngine;

/// <summary>
/// Towers assembled from the Kenney kit. A tower is a stack/arrangement of kit pieces described in code, saved as a prefab in
/// Assets/Prefabs/Towers. Kit pieces are 1 world unit wide; a tower footprint is 2, so assemblies are scaled up.
/// </summary>
public static class KitTowers
{
    private const string KitDir = "Assets/ThirdParty/Kenney/RetroFantasy";

    /// <summary>Adds kit piece <paramref name="piece"/> under <paramref name="parent"/> at a local position, spun about y.</summary>
    public static GameObject Part(Transform parent, string piece, Vector3 pos, float yaw = 0f, float scale = 1f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{KitDir}/{piece}.fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    /// <summary>A round stone tower: base, two shafts, a flared edge and a battlement ring.</summary>
    public static GameObject RoundKeep(string name)
    {
        var root = new GameObject(name);
        Part(root.transform, "tower-base", new Vector3(0, 0.0f, 0));
        Part(root.transform, "tower", new Vector3(0, 1.0f, 0));
        Part(root.transform, "tower-paint", new Vector3(0, 2.0f, 0));
        Part(root.transform, "tower-edge", new Vector3(0, 3.0f, 0));
        Part(root.transform, "tower-top", new Vector3(0, 3.4f, 0));
        return root;
    }

    /// <summary>A watchtower: stone base, a wooden lookout on poles and a tiled roof.</summary>
    public static GameObject Watchtower(string name)
    {
        var root = new GameObject(name);
        Part(root.transform, "tower-base", new Vector3(0, 0.0f, 0));
        Part(root.transform, "tower", new Vector3(0, 1.0f, 0));
        Part(root.transform, "structure-poles", new Vector3(0, 2.0f, 0));
        Part(root.transform, "wood-floor-railing", new Vector3(0, 2.0f, 0));
        Part(root.transform, "roof", new Vector3(0, 3.0f, 0));
        return root;
    }
}
