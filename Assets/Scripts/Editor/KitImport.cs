using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imports selected pieces of a Kenney / KayKit pack from the local cache (local_assets/, git-ignored) into
/// Assets/ThirdParty, so only the pieces the game uses are committed. Materials are made from the pack's texture(s):
/// an FBX material named X uses Textures/X.png if it exists, otherwise Textures/colormap.png.
///
/// Batch: Unity -batchmode -projectPath . -executeMethod KitImport.Run -quit -pack kenney_tower-defense-kit -dest Kenney/TowerDefenseKit -pieces tower-,weapon-
/// (-pieces is a comma-separated list of exact names or name prefixes ending in '-' or '_'.)
/// </summary>
public static class KitImport
{
    private const string Cache = "local_assets";

    public static void Run()
    {
        string pack = Arg("-pack"), dest = Arg("-dest"), pieces = Arg("-pieces");
        Import(pack, dest, pieces.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    public static void Import(string pack, string dest, string[] pieces)
    {
        string modelDir = Directory.GetDirectories(Path.Combine(Cache, pack), "FBX*", SearchOption.AllDirectories).FirstOrDefault()
                          ?? Directory.GetDirectories(Path.Combine(Cache, pack), "fbx", SearchOption.AllDirectories).FirstOrDefault();
        if (modelDir == null) throw new InvalidOperationException($"No FBX folder in {Cache}/{pack}");
        string texDir = Path.Combine(modelDir, "Textures");
        if (!Directory.Exists(texDir)) texDir = Directory.GetDirectories(Path.Combine(Cache, pack), "*textures*", SearchOption.AllDirectories).FirstOrDefault();

        string target = "Assets/ThirdParty/" + dest;
        Directory.CreateDirectory(target + "/Textures");
        Directory.CreateDirectory(target + "/Materials");

        var files = Directory.GetFiles(modelDir, "*.fbx")
            .Where(f => Matches(Path.GetFileNameWithoutExtension(f), pieces)).ToArray();
        foreach (string f in files) File.Copy(f, $"{target}/{Path.GetFileName(f)}", true);
        if (texDir != null)
            foreach (string t in Directory.GetFiles(texDir, "*.png")) File.Copy(t, $"{target}/Textures/{Path.GetFileName(t)}", true);
        foreach (string l in Directory.GetFiles(Path.Combine(Cache, pack), "License*", SearchOption.AllDirectories).Take(1))
            File.Copy(l, $"{target}/License.txt", true);
        AssetDatabase.Refresh();

        // one material per texture; the FBX material name selects it
        foreach (string tex in Directory.GetFiles($"{target}/Textures", "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(tex);
            string path = $"{target}/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{target}/Textures/{name}.png"));
            m.SetFloat("_Smoothness", 0f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();

        var fallback = AssetDatabase.LoadAssetAtPath<Material>($"{target}/Materials/colormap.mat");
        foreach (string f in files)
        {
            string assetPath = $"{target}/{Path.GetFileName(f)}";
            var importer = (ModelImporter)AssetImporter.GetAtPath(assetPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
            importer.SaveAndReimport();

            // remap every material the model uses to its named material, or the pack's colormap
            var used = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Material>().Select(m => m.name).Distinct().ToList();
            var so = new SerializedObject(importer);
            foreach (string mat in used)
            {
                var named = AssetDatabase.LoadAssetAtPath<Material>($"{target}/Materials/{mat}.mat") ?? fallback;
                if (named != null) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat), named);
            }
            importer.SaveAndReimport();
        }
        Debug.Log($"KitImport: {files.Length} pieces from {pack} into {target}");
    }

    private static bool Matches(string name, string[] pieces) =>
        pieces.Any(p => (p.EndsWith("-") || p.EndsWith("_")) ? name.StartsWith(p) : name == p);

    private static string Arg(string key)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, key);
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing " + key);
        return args[i + 1];
    }
}
