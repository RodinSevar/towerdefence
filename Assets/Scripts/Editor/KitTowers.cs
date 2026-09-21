using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Towers assembled from kit pieces. A tower is a short description in code (a stack of pieces plus a few extras) that
/// becomes a prefab in Assets/Prefabs/Towers and is assigned to its TowerData. Each race recolours the kit's palette texture
/// so its towers share a look. Batch: KitTowers.BuildCrystalCastle.
/// </summary>
public static class KitTowers
{
    private const string TdDir = "Assets/ThirdParty/Kenney/TowerDefenseKit";
    private const string PrefabDir = "Assets/Prefabs/Towers";
    private const string TowerDataDir = "Assets/Data/Towers/Imported";

    // ------------------------------------------------------------------------------------------------ building blocks

    private static readonly System.Collections.Generic.HashSet<string> used = new System.Collections.Generic.HashSet<string>();

    private static GameObject Piece(Transform parent, string name, Vector3 pos, float scale = 1f, float yaw = 0f)
    {
        used.Add(name);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TdDir}/{name}.fbx");
        if (prefab == null) throw new System.InvalidOperationException($"Kit piece '{name}' is not imported.");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        return go;
    }

    private static float Height(GameObject go) => go.GetComponentInChildren<Renderer>().bounds.size.y;

    /// <summary>Stacks pieces upward, each starting where the last ends. Returns the height reached.</summary>
    private static float Stack(Transform parent, params string[] pieces)
    {
        float y = 0;
        foreach (string p in pieces)
        {
            var go = Piece(parent, p, new Vector3(0, y, 0));
            y += Height(go);
        }
        return y;
    }

    /// <summary>A copy of the kit's palette texture with every colour passed through <paramref name="recolor"/>, and a material using it.</summary>
    private static Material RaceMaterial(string name, System.Func<Color, Color> recolor)
    {
        string texPath = $"{TdDir}/Textures/{name}.png";
        string matPath = $"{TdDir}/Materials/{name}.mat";
        var tex = new Texture2D(2, 2);
        tex.LoadImage(File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), TdDir, "Textures", "colormap.png")));
        var pixels = tex.GetPixels();
        for (int i = 0; i < pixels.Length; i++) pixels[i] = recolor(pixels[i]);
        tex.SetPixels(pixels);
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), texPath), tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);

        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, matPath); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Moves colours whose hue lies in [from, to] (0..1, wrapping if from &gt; to) to hue <paramref name="target"/>, keeping saturation and value.</summary>
    private static Color ShiftHue(Color c, float from, float to, float target)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (s < 0.15f) return c; // greys and whites stay
        bool inRange = from <= to ? (h >= from && h <= to) : (h >= from || h <= to);
        if (!inRange) return c;
        var r = Color.HSVToRGB(target, s, v);
        r.a = c.a;
        return r;
    }

    /// <summary>Finishes a tower: applies the race material, fits it to the footprint, saves the prefab and assigns it to the TowerData.</summary>
    private static void Save(string wc3Id, GameObject root, Material material, float fit)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;

        var renderers = root.GetComponentsInChildren<Renderer>();
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        float width = Mathf.Max(b.size.x, b.size.z);

        // wrapper: model centred on the origin with its base at y = 0, scaled so the widest side is `fit` world units
        var wrapper = new GameObject(wc3Id);
        root.transform.SetParent(wrapper.transform, false);
        root.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        wrapper.transform.localScale = Vector3.one * (fit / width);

        Directory.CreateDirectory(PrefabDir);
        var prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, $"{PrefabDir}/{wc3Id}.prefab");
        Object.DestroyImmediate(wrapper);

        var data = AssetDatabase.LoadAssetAtPath<TowerData>($"{TowerDataDir}/{wc3Id}.asset");
        if (data == null) { Debug.LogWarning($"KitTowers: no TowerData for {wc3Id}"); return; }
        data.modelPrefab = prefab;
        EditorUtility.SetDirty(data);
    }

    // ------------------------------------------------------------------------------------------------ Crystal Castle

    /// <summary>Only pieces that a tower uses stay in the project (the rest live in local_assets and can be re-imported).</summary>
    private static void PruneUnused()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { TdDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!used.Contains(Path.GetFileNameWithoutExtension(path))) AssetDatabase.DeleteAsset(path);
        }
    }

    [MenuItem("Tools/Build Crystal Castle Towers")]
    public static void BuildCrystalCastle()
    {
        // The kit's orange and purple accents become azure and cyan; gold and greys stay.
        Material m = RaceMaterial("crystalcastle", c =>
        {
            c = ShiftHue(c, 0.95f, 0.10f, 0.55f); // orange / red -> azure
            c = ShiftHue(c, 0.68f, 0.86f, 0.50f); // purple -> cyan
            c = ShiftHue(c, 0.86f, 0.95f, 0.47f); // magenta -> aqua
            c = ShiftHue(c, 0.22f, 0.48f, 0.53f); // green (crystal stems, grass) -> ice blue
            return c;
        });

        // 10g Crystal Shooter: a small pedestal with a crystal
        {
            var r = new GameObject("hgtw");
            float h = Stack(r.transform, "tower-round-base", "tower-round-bottom-a");
            Piece(r.transform, "detail-crystal-large", new Vector3(0, h - 0.05f, 0), 1.3f);
            Save("hgtw", r, m, 1.2f);
        }
        // 50g Crystal Blaster: a short turret crowned by a crystal
        {
            var r = new GameObject("hctw");
            float h = Stack(r.transform, "tower-round-base", "tower-round-bottom-b", "tower-round-top-a");
            Piece(r.transform, "weapon-turret", new Vector3(0, h - 0.15f, 0), 1.6f);
            Piece(r.transform, "detail-crystal", new Vector3(0.28f, h - 0.2f, 0.2f), 1.5f, 30f);
            Save("hctw", r, m, 1.5f);
        }
        // 200g Crystal Fury: a pointed-roof watchtower
        {
            var r = new GameObject("hwtw");
            Stack(r.transform, "tower-round-base", "tower-round-bottom-c", "tower-round-middle-a", "tower-round-top-b", "tower-round-roof-a");
            Save("hwtw", r, m, 1.6f);
        }
        // 250g Crystal Slower: a fountain bowl filled with crystals
        {
            var r = new GameObject("hhou");
            float h = Stack(r.transform, "tower-round-base", "tower-round-bottom-a", "tower-round-top-c");
            Piece(r.transform, "detail-crystal-large", new Vector3(0, h - 0.30f, 0), 1.4f);
            Piece(r.transform, "detail-crystal", new Vector3(0.16f, h - 0.30f, 0.08f), 1.1f, 40f);
            Piece(r.transform, "detail-crystal", new Vector3(-0.14f, h - 0.30f, -0.10f), 1.1f, 120f);
            Save("hhou", r, m, 1.75f);
        }
        // 400g Crystal Buster: a square bastion carrying a cannon
        {
            var r = new GameObject("oC91");
            float h = Stack(r.transform, "tower-square-bottom-b", "tower-square-middle-b", "tower-square-top-a");
            Piece(r.transform, "weapon-cannon", new Vector3(0, h - 0.25f, 0), 1.5f);
            Save("oC91", r, m, 1.75f);
        }
        // 650g Crystal Dissolver: a tall spire with a floating crystal
        {
            var r = new GameObject("eC93");
            float h = Stack(r.transform, "tower-round-base", "tower-round-bottom-b", "tower-round-middle-b", "tower-round-top-a", "tower-round-roof-c");
            Piece(r.transform, "detail-crystal-large", new Vector3(0, h + 0.10f, 0), 1.7f);
            Save("eC93", r, m, 1.7f);
        }
        // 1500g Ice Cave: a wide crystal shrine
        {
            var r = new GameObject("nmoo");
            float h = Stack(r.transform, "tower-square-bottom-b", "tower-square-middle-a", "tower-square-middle-c", "tower-square-top-c");
            Piece(r.transform, "detail-crystal-large", new Vector3(0, h - 0.32f, 0), 1.8f);
            Piece(r.transform, "detail-crystal-large", new Vector3(0.25f, h - 0.32f, 0.15f), 1.2f, 60f);
            Piece(r.transform, "detail-crystal-large", new Vector3(-0.24f, h - 0.32f, 0.12f), 1.3f, 150f);
            Piece(r.transform, "detail-crystal-large", new Vector3(0.05f, h - 0.32f, -0.25f), 1.2f, 250f);
            Save("nmoo", r, m, 2.0f);
        }

        PruneUnused();
        AssetDatabase.SaveAssets();
        Debug.Log("KitTowers: Crystal Castle towers built.");
    }
}
