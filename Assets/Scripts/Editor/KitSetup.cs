using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives the imported Kenney kit its materials: one URP Lit material per kit texture, remapped onto every FBX by material
/// name (the FBX files only carry names, not textures). Re-run after adding pieces: Tools/Setup Kenney Kit.
/// </summary>
public static class KitSetup
{
    private const string KitDir = "Assets/ThirdParty/Kenney/RetroFantasy";

    // FBX material name -> texture file
    private static readonly Dictionary<string, string> Textures = new Dictionary<string, string>
    {
        { "barrel", "barrel" }, { "bricks", "cobblestoneAlternative" }, { "details", "details" }, { "fence", "fence" },
        { "planks", "planks" }, { "roof", "roof" }, { "stones", "cobblestone" }, { "stonesPainted", "cobblestonePainted" },
        { "tree", "tree" }, { "water", "water" },
    };

    [MenuItem("Tools/Setup Kenney Kit")]
    public static void Run()
    {
        Directory.CreateDirectory($"{KitDir}/Materials");
        var materials = new Dictionary<string, Material>();
        foreach (var pair in Textures)
        {
            string path = $"{KitDir}/Materials/{pair.Key}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{KitDir}/Textures/{pair.Value}.png");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0f);
            if (pair.Key == "tree" || pair.Key == "fence") { m.SetFloat("_Cull", 0); }
            EditorUtility.SetDirty(m);
            materials[pair.Key] = m;
        }
        AssetDatabase.SaveAssets();

        foreach (string fbx in Directory.GetFiles(KitDir, "*.fbx"))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx.Replace("\\", "/"));
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            foreach (var pair in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
        }
        Debug.Log("KitSetup: done.");
    }
}
