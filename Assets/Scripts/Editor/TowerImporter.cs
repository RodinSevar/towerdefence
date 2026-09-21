using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Imports the real Wintermaul tower races and towers from the extracted map files in mpq_files/.
/// Follows the map's own structure: the wisp "Element Chooser" (eC00) builds race buildings; each building trains
/// a constructor unit; each constructor builds that race's towers. Writes TowerData assets to
/// Assets/Data/Towers/Imported and RaceData assets to Assets/Data/Races, then wires the scene.
/// Imported assets are regenerated on every run except for hand-set fields the importer does not own (e.g. icon).
/// </summary>
public static class TowerImporter
{
    private const string ElementChooserId = "eC00";
    private const string TowerDir = "Assets/Data/Towers/Imported";
    private const string RaceDir = "Assets/Data/Races";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // The map only stores fields that differ from the base unit; the base data lives in the game's own files, which
    // we don't have. These are used when a field is missing, and each gap is recorded in TowerData.importNotes.
    private const double AssumedRange = 700;             // WC3 units
    private const double AssumedCooldown = 1.0;          // seconds
    private const double MinCooldown = 0.1;              // a cooldown of 0 would fire every frame
    private const double AssumedProjectileSpeed = 900;   // WC3 units/sec
    private const double InstantProjectileSpeed = 5000;  // at or above this the attack is treated as instant (laser style)
    private const double AssumedDice = 1;
    private const double AssumedSides = 1;

    private static readonly Color[] RaceColors =
    {
        new Color(0.35f, 0.75f, 1.00f), new Color(0.35f, 0.75f, 0.30f), new Color(0.60f, 0.55f, 0.45f),
        new Color(1.00f, 0.90f, 0.20f), new Color(0.75f, 0.75f, 0.80f), new Color(1.00f, 0.40f, 0.10f),
        new Color(0.60f, 0.90f, 1.00f), new Color(0.55f, 0.30f, 0.85f), new Color(0.75f, 0.10f, 0.20f),
        new Color(1.00f, 1.00f, 0.85f), new Color(0.20f, 0.30f, 0.70f),
    };

    [MenuItem("Tools/Import WC3 Towers and Races")]
    public static void ImportMenu()
    {
        Import();
        GameDataBuilder.BuildInOpenScene();
    }

    /// <summary>Entry point for batch mode: opens the main scene, imports, wires the scene, saves.</summary>
    public static void ImportAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Import();
        GameDataBuilder.BuildInOpenScene(); // wires RaceManager to the new race assets
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("TowerImporter: done.");
    }

    private static void Import()
    {
        Directory.CreateDirectory(TowerDir);
        Directory.CreateDirectory(RaceDir);

        var units = Wc3Data.ParseUnits(Wc3Data.MapFile("war3map.w3u"));
        var strings = Wc3Data.ParseStrings(Wc3Data.MapFile("war3map.wts"));

        if (!units.TryGetValue(ElementChooserId, out var chooser) || Wc3Data.GetString(chooser, "ubui") == null)
            throw new InvalidOperationException($"Element Chooser '{ElementChooserId}' (the wisp) or its build list was not found.");

        // The placeholder race and towers from Tools > Build Game Data are replaced by the real ones.
        foreach (string placeholder in new[]
            { $"{RaceDir}/00_Placeholder.asset", "Assets/Data/Towers/Gun.asset", "Assets/Data/Towers/Laser.asset", "Assets/Data/Towers/Ice.asset" })
            AssetDatabase.DeleteAsset(placeholder);

        string[] buildingIds = Split(Wc3Data.GetString(chooser, "ubui"));
        int towerCount = 0;
        for (int i = 0; i < buildingIds.Length; i++)
        {
            string buildingId = buildingIds[i];
            if (!units.TryGetValue(buildingId, out var building))
                throw new InvalidOperationException($"Race building '{buildingId}' is not defined in war3map.w3u.");

            string constructorId = Wc3Data.GetString(building, "utra");
            if (constructorId == null || !units.TryGetValue(constructorId, out var constructor))
                throw new InvalidOperationException($"Building '{buildingId}' trains no known constructor unit.");

            Color raceColor = RaceColors[i % RaceColors.Length];

            var towers = new List<TowerData>();
            foreach (string towerId in Split(Wc3Data.GetString(constructor, "ubui")))
            {
                if (!units.TryGetValue(towerId, out var towerFields))
                    throw new InvalidOperationException($"Tower '{towerId}' (built by '{constructorId}') is not defined in war3map.w3u.");
                towers.Add(SaveTower(towerId, towerFields, strings, raceColor));
            }
            towerCount += towers.Count;

            SaveRace(i, buildingId, building, constructor, strings, raceColor, towers);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"TowerImporter: imported {buildingIds.Length} races with {towerCount} towers.");
    }

    private static string[] Split(string commaList)
    {
        return string.IsNullOrEmpty(commaList)
            ? new string[0]
            : Array.FindAll(commaList.Split(','), s => !string.IsNullOrWhiteSpace(s));
    }

    private static T LoadOrCreate<T>(string path, out bool isNew) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        isNew = asset == null;
        return isNew ? ScriptableObject.CreateInstance<T>() : asset;
    }

    private static void SaveRace(int index, string buildingId, Dictionary<string, object> building,
        Dictionary<string, object> constructor, Dictionary<int, string> strings, Color color, List<TowerData> towers)
    {
        // Numeric prefix keeps the map's order when the assets are listed alphabetically.
        string path = $"{RaceDir}/{index:00}_{buildingId}.asset";
        var race = LoadOrCreate<RaceData>(path, out bool isNew);

        race.displayName = (Wc3Data.ResolveString(Wc3Data.GetString(building, "unam"), strings) ?? buildingId).Trim();
        race.constructorName = (Wc3Data.ResolveString(Wc3Data.GetString(constructor, "unam"), strings) ?? "").Trim();
        race.description = Wc3Data.ToRichText(Wc3Data.ResolveString(Wc3Data.GetString(building, "utub"), strings)) ?? "";
        race.lumberCost = building.TryGetValue("ulum", out var lumber) ? Convert.ToInt32(lumber) : 1;
        race.color = color;
        race.towers = towers.ToArray();

        if (isNew) AssetDatabase.CreateAsset(race, path);
        EditorUtility.SetDirty(race);
    }

    private static TowerData SaveTower(string id, Dictionary<string, object> f, Dictionary<int, string> strings, Color raceColor)
    {
        string path = $"{TowerDir}/{id}.asset";
        var tower = LoadOrCreate<TowerData>(path, out bool isNew);
        var notes = new List<string>();

        string name = Wc3Data.ResolveString(Wc3Data.GetString(f, "unam"), strings);
        tower.wc3Id = id;
        tower.displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
        tower.description = Wc3Data.ToRichText(Wc3Data.ResolveString(Wc3Data.GetString(f, "utub"), strings)) ?? "";

        // Economy
        int gold = (int)Wc3Data.GetNumber(f, "ugol", 0, notes, "gold cost (ugol), assumed 0");
        tower.lumberCost = (int)Wc3Data.GetNumber(f, "ulum", 0, notes, null);
        tower.sellValue = (int)Wc3Data.GetNumber(f, "upoi", -1, notes, null); // the map sells for the unit's point value
        tower.buildTime = (float)Wc3Data.GetNumber(f, "ubld", 0, notes, null);

        // Attack: damage = base + dice * (sides + 1) / 2 (average roll)
        double baseDamage = Wc3Data.GetNumber(f, "ua1b", 0, notes, "damage base (ua1b), assumed 0");
        double dice = Wc3Data.GetNumber(f, "ua1d", AssumedDice, notes, $"damage dice (ua1d), assumed {AssumedDice}");
        double sides = Wc3Data.GetNumber(f, "ua1s", AssumedSides, notes, $"damage sides (ua1s), assumed {AssumedSides}");
        double damage = baseDamage + dice * (sides + 1) / 2.0;

        double rangeUnits = Wc3Data.GetNumber(f, "ua1r", AssumedRange, notes, $"range (ua1r), assumed {AssumedRange}");
        double cooldown = Wc3Data.GetNumber(f, "ua1c", AssumedCooldown, notes, $"cooldown (ua1c), assumed {AssumedCooldown}");
        if (cooldown < MinCooldown)
        {
            notes.Add($"cooldown {cooldown:0.##}s clamped to {MinCooldown}s");
            cooldown = MinCooldown;
        }
        double projectileSpeed = Wc3Data.GetNumber(f, "ua1z", AssumedProjectileSpeed, notes,
            $"projectile speed (ua1z), assumed {AssumedProjectileSpeed}");
        tower.attackStyle = projectileSpeed >= InstantProjectileSpeed ? TowerAttackStyle.Laser : TowerAttackStyle.Projectile;

        // Things the map defines that the game does not simulate yet
        tower.abilityIds = Split(Wc3Data.GetString(f, "uabi"));
        if (tower.abilityIds.Length > 0)
            notes.Add("stock abilities not simulated: " + string.Join(",", tower.abilityIds));
        if (f.ContainsKey("ua2b") || f.ContainsKey("ua2r") || f.ContainsKey("ua2c"))
            notes.Add("second attack (ua2*) ignored");
        if (Wc3Data.GetString(f, "ua1w") == "msplash")
            notes.Add("missile splash damage not simulated");

        tower.levels = new[]
        {
            new TowerLevel
            {
                cost = gold,
                range = (float)(rangeUnits / Wc3Data.WorldUnitsPerCell),
                fireRate = (float)(1.0 / cooldown),
                damage = (float)damage,
                visualColor = raceColor,
                visualScale = Mathf.Lerp(0.7f, 1.4f, Mathf.Clamp01(gold / 750f)),
                projectileSpeed = (float)(projectileSpeed / Wc3Data.WorldUnitsPerCell),
                projectileColor = Color.Lerp(raceColor, Color.white, 0.5f),
                projectileScale = 0.25f,
            }
        };

        tower.importNotes = notes.Count == 0 ? "" : "Not defined or not simulated: " + string.Join("; ", notes);

        if (isNew) AssetDatabase.CreateAsset(tower, path);
        EditorUtility.SetDirty(tower);
        return tower;
    }
}
