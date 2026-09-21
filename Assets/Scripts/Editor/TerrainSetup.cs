using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sets up the terrain: imports the map's terrain data, creates the ground / cliff / water materials (textures wired by tile
/// id from Assets/Textures/Terrain), and puts a "Terrain" object with a wired TerrainBuilder into the scene, replacing the
/// old image-based generator. Re-running refreshes the materials and wiring.
/// </summary>
public static class TerrainSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string MaterialDir = "Assets/Materials";
    private const string TextureDir = "Assets/Textures/Terrain/";
    private const float TileSize = 8f; // world units per texture repeat

    [MenuItem("Tools/Build Terrain")]
    public static void BuildMenu()
    {
        Build();
    }

    /// <summary>Batch entry point: builds and saves the scene.</summary>
    public static void BuildAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("TerrainSetup: done.");
    }

    private static void Build()
    {
        TerrainMapData data = TerrainImporter.Import();
        Directory.CreateDirectory(MaterialDir);

        Material ground = LoadOrCreateMaterial("TerrainGround", "Wintermaul/TerrainBlend", m =>
        {
            for (int i = 0; i < data.groundTileIds.Length && i < 7; i++) SetTile(m, i, data.groundTileIds[i]);
            m.SetFloat("_IsWall", 0f);
            m.SetFloat("_TileSize", TileSize);
            m.SetFloat("_Smoothness", 0.12f);
        });
        Material wall = LoadOrCreateMaterial("TerrainWall", "Wintermaul/TerrainBlend", m =>
        {
            for (int i = 0; i < data.cliffTileIds.Length && i < 7; i++) SetTile(m, i, data.cliffTileIds[i]);
            m.SetFloat("_IsWall", 1f);
            m.SetFloat("_TileSize", TileSize);
            m.SetFloat("_Smoothness", 0.08f);
        });
        Material water = LoadOrCreateMaterial("TerrainWater", "Wintermaul/TerrainWater", m =>
        {
            m.SetTexture("_WaterTex", Load("water"));
            m.SetTexture("_WaterNrm", Load("water_n"));
        });

        // The old image-based generator object, if it was ever saved into the scene
        GameObject old;
        while ((old = GameObject.Find("MapGenerator")) != null) Object.DestroyImmediate(old);

        GameObject terrain = GameObject.Find("Terrain");
        if (terrain == null) terrain = new GameObject("Terrain");
        terrain.tag = "Ground";
        var builder = terrain.GetComponent<TerrainBuilder>() ?? terrain.AddComponent<TerrainBuilder>();
        GameDataBuilder.WireObject(builder, "data", data);
        GameDataBuilder.WireObject(builder, "groundMaterial", ground);
        GameDataBuilder.WireObject(builder, "wallMaterial", wall);
        GameDataBuilder.WireObject(builder, "waterMaterial", water);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static Texture2D Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}terrain_{name}.png");
    }

    /// <summary>Assigns slot <paramref name="slot"/> from the textures named after the map's tile id (normal map if it exists).</summary>
    private static void SetTile(Material m, int slot, string tileId)
    {
        Texture2D albedo = Load(tileId);
        if (albedo == null) { Debug.LogWarning($"TerrainSetup: no texture for tile '{tileId}'."); return; }
        m.SetTexture("_Tex" + slot, albedo);
        Texture2D normal = Load(tileId + "_n");
        if (normal != null) m.SetTexture("_Nrm" + slot, normal);
    }

    private static Material LoadOrCreateMaterial(string name, string shaderName, System.Action<Material> configure)
    {
        string path = $"{MaterialDir}/{name}.mat";
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException($"Shader '{shaderName}' not found (does it compile?).");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        configure(material);
        EditorUtility.SetDirty(material);
        return material;
    }
}
