using UnityEditor;
using UnityEngine;

/// <summary>Builds the low-poly models (mesh assets) and puts them into the game's prefabs. Batch: ModelSetup.Apply.</summary>
public static class ModelSetup
{
    private const string TowerPrefab = "Assets/Prefabs/Tower.prefab";

    [MenuItem("Tools/Apply Low-Poly Models")]
    public static void Apply()
    {
        Mesh tower = ModelShot.SaveMesh(LowPolyModels.GuardTower());
        Material material = ModelShot.LowPolyMaterial();

        GameObject root = PrefabUtility.LoadPrefabContents(TowerPrefab);
        try
        {
            var visual = new SerializedObject(root.GetComponent<Tower>()).FindProperty("visual").objectReferenceValue as Transform;
            visual.GetComponent<MeshFilter>().sharedMesh = tower;
            visual.GetComponent<MeshRenderer>().sharedMaterial = material;

            // pick box around the model (it is 1 unit wide, 1.62 tall, base at y = 0)
            var box = visual.GetComponent<BoxCollider>();
            if (box != null) { box.center = new Vector3(0, 0.81f, 0); box.size = new Vector3(1f, 1.62f, 1f); }

            PrefabUtility.SaveAsPrefabAsset(root, TowerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("ModelSetup: tower prefab updated.");
    }
}
