using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates the tower data assets, placeholder waves (only if the WC3 import has not been run) and the Tower/Enemy prefabs, and wires TowerManager and WaveManager
/// into the scene. Only creates what is missing: existing assets and prefabs are never overwritten, so tweaks made
/// in the Inspector survive a re-run. Delete an asset to regenerate it.
/// </summary>
public static class GameDataBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string TowerDataDir = "Assets/Data/Towers";
    private const string EnemyDataDir = "Assets/Data/Enemies";
    private const string WaveDataDir = "Assets/Data/Waves";
    private const string PrefabDir = "Assets/Prefabs";
    private const string RaceDir = "Assets/Data/Races";

    [MenuItem("Tools/Build Game Data")]
    public static void BuildInOpenScene()
    {
        Build();
    }

    /// <summary>Entry point for batch mode: opens the main scene, builds data, saves.</summary>
    public static void BuildAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("GameDataBuilder: done.");
    }

    private static void Build()
    {
        foreach (string dir in new[] { TowerDataDir, EnemyDataDir, WaveDataDir, PrefabDir })
            Directory.CreateDirectory(dir);

        // ---- Races: the imported Wintermaul races if present, else one placeholder race ----
        RaceData[] races = FindRaces();
        if (races.Length == 0) races = new[] { CreatePlaceholderRace() };

        // ---- Waves: the imported Wintermaul levels if present, else 5 placeholder waves ----
        WaveSet waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveImporter.WaveSetPath) ?? CreatePlaceholderWaves();

        // ---- Prefabs ----
        Tower towerPrefab = LoadOrCreatePrefab<Tower>($"{PrefabDir}/Tower.prefab", BuildTowerPrefab);
        Enemy enemyPrefab = LoadOrCreatePrefab<Enemy>($"{PrefabDir}/Enemy.prefab", BuildEnemyPrefab);

        // ---- Scene wiring ----
        var towerManager = FindOrCreate<TowerManager>("TowerManager");
        Wire(towerManager, "towerPrefab", towerPrefab);

        var raceManager = FindOrCreate<RaceManager>("RaceManager");
        WireArray(raceManager, "races", races);

        var waveManager = FindOrCreate<WaveManager>("WaveManager");
        Wire(waveManager, "enemyPrefab", enemyPrefab);
        Wire(waveManager, "waveSet", waves);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    private static RaceData[] FindRaces()
    {
        if (!AssetDatabase.IsValidFolder(RaceDir)) return new RaceData[0];
        var paths = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:RaceData", new[] { RaceDir }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        paths.Sort(System.StringComparer.Ordinal); // imported races are prefixed 00_, 01_, ... in map order
        return paths.ConvertAll(p => AssetDatabase.LoadAssetAtPath<RaceData>(p)).ToArray();
    }

    /// <summary>A single race with three simple towers, used until the WC3 import has been run.</summary>
    private static RaceData CreatePlaceholderRace()
    {
        Directory.CreateDirectory(RaceDir);
        TowerData gun = LoadOrCreate<TowerData>($"{TowerDataDir}/Gun.asset", t =>
        {
            t.displayName = "Gun Tower";
            t.attackStyle = TowerAttackStyle.Projectile;
            t.levels = new[]
            {
                new TowerLevel { cost = 10, range = 10f, fireRate = 1f, damage = 10f,
                    visualColor = Color.blue, visualScale = 0.8f,
                    projectileSpeed = 15f, projectileColor = Color.yellow, projectileScale = 0.2f },
                new TowerLevel { cost = 20, range = 12f, fireRate = 1.5f, damage = 25f,
                    visualColor = new Color(0, 0, 0.7f), visualScale = 1.0f,
                    projectileSpeed = 20f, projectileColor = new Color(1f, 0.5f, 0f), projectileScale = 0.3f },
            };
        });
        TowerData laser = LoadOrCreate<TowerData>($"{TowerDataDir}/Laser.asset", t =>
        {
            t.displayName = "Laser Tower";
            t.attackStyle = TowerAttackStyle.Laser;
            t.levels = new[]
            {
                new TowerLevel { cost = 15, range = 12f, fireRate = 2f, damage = 15f,
                    visualColor = Color.yellow, visualScale = 0.8f },
                new TowerLevel { cost = 30, range = 15f, fireRate = 3f, damage = 35f,
                    visualColor = new Color(0.8f, 0.8f, 0f), visualScale = 1.0f },
            };
        });
        TowerData ice = LoadOrCreate<TowerData>($"{TowerDataDir}/Ice.asset", t =>
        {
            t.displayName = "Ice Tower";
            t.attackStyle = TowerAttackStyle.Projectile;
            t.levels = new[]
            {
                new TowerLevel { cost = 12, range = 8f, fireRate = 0.5f, damage = 5f,
                    visualColor = Color.cyan, visualScale = 0.8f,
                    projectileSpeed = 24f, projectileColor = Color.cyan, projectileScale = 0.4f,
                    hasStatusEffect = true, effectType = StatusEffectType.Slow, effectDuration = 2f, effectStrength = 0.5f },
                new TowerLevel { cost = 25, range = 10f, fireRate = 1f, damage = 15f,
                    visualColor = new Color(0, 0.7f, 0.7f), visualScale = 1.0f,
                    projectileSpeed = 30f, projectileColor = Color.cyan, projectileScale = 0.5f,
                    hasStatusEffect = true, effectType = StatusEffectType.Slow, effectDuration = 3f, effectStrength = 0.7f },
            };
        });

        return LoadOrCreate<RaceData>($"{RaceDir}/00_Placeholder.asset", r =>
        {
            r.displayName = "Placeholder";
            r.lumberCost = 0;
            r.towers = new[] { gun, laser, ice };
        });
    }

    private static WaveSet CreatePlaceholderWaves()
    {
        EnemyData basic = LoadOrCreate<EnemyData>($"{EnemyDataDir}/Basic.asset", e =>
        { e.displayName = "Basic Creep"; e.health = 20f; e.speed = 5f; e.goldReward = 10; });
        EnemyData strong = LoadOrCreate<EnemyData>($"{EnemyDataDir}/Strong.asset", e =>
        { e.displayName = "Strong Creep"; e.health = 50f; e.speed = 3f; e.goldReward = 25; });

        return LoadOrCreate<WaveSet>($"{WaveDataDir}/DefaultWaves.asset", w =>
        {
            w.waves = new WaveSet.Wave[5];
            for (int i = 0; i < 5; i++)
            {
                w.waves[i] = new WaveSet.Wave { enemy = i < 2 ? basic : strong, enemyCount = 5 + i * 3 };
            }
        });
    }

    // ---------- prefab construction ----------

    private static GameObject BuildTowerPrefab()
    {
        var root = new GameObject("Tower");
        var tower = root.AddComponent<Tower>();

        // Cube keeps its collider: it is what the player clicks to select the tower.
        Transform visual = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        visual.name = "Visual";
        visual.SetParent(root.transform, false);

        var firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(root.transform, false);
        firePoint.localPosition = Vector3.up * 0.5f;

        GameObject ring = MakeSelectionRing(root.transform);

        WireObject(tower, "visual", visual);
        WireObject(tower, "firePoint", firePoint);
        WireObject(tower, "selectionRing", ring);
        return root;
    }

    private static GameObject BuildEnemyPrefab()
    {
        var root = new GameObject("Enemy");
        var enemy = root.AddComponent<Enemy>();
        root.AddComponent<BoxCollider>().size = new Vector3(0.8f, 0.8f, 0.8f); // used for tower targeting and selection

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "EnemyVisual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * 0.8f;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        GameObject ring = MakeSelectionRing(root.transform);

        WireObject(enemy, "visualRenderer", visual.GetComponent<Renderer>());
        WireObject(enemy, "selectionRing", ring);
        return root;
    }

    private static GameObject MakeSelectionRing(Transform parent)
    {
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SelectionRing";
        ring.transform.SetParent(parent, false);
        ring.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
        ring.transform.localPosition = new Vector3(0, 0.05f, 0);
        Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.SetActive(false);
        return ring;
    }

    // ---------- helpers ----------

    private static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        var asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T LoadOrCreatePrefab<T>(string path, System.Func<GameObject> build) where T : Component
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<T>();

        GameObject temp = build();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(temp, path);
        Object.DestroyImmediate(temp);
        return saved.GetComponent<T>();
    }

    private static T FindOrCreate<T>(string objectName) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;
        return new GameObject(objectName).AddComponent<T>();
    }

    private static void Wire(Object target, string field, Object value) => WireObject(target, field, value);

    internal static void WireObject(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = FindOrThrow(so, target, field);
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireArray(Object target, string field, params Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = FindOrThrow(so, target, field);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty FindOrThrow(SerializedObject so, Object target, string field)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
            throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
        return prop;
    }
}
