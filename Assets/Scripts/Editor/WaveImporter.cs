using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Imports the real Wintermaul levels and creep stats from the extracted map files in mpq_files/:
///   war3map.j   - "Set Levels" trigger: creep type and count per level
///   war3map.w3u - custom unit definitions: name, hit points, speed, armor, bounty
///   war3map.wts - string table for the TRIGSTR_ names
/// Writes EnemyData assets to Assets/Data/Enemies/Imported and a WaveSet to Assets/Data/Waves/WintermaulWaves.asset,
/// and points the scene's WaveManager at it. Imported assets are regenerated on every run (they are derived data).
/// </summary>
public static class WaveImporter
{
    private const string EnemyDir = "Assets/Data/Enemies/Imported";
    public const string WaveSetPath = "Assets/Data/Waves/WintermaulWaves.asset";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // The map only stores fields that differ from the base unit; the base unit data lives in the game's own files,
    // which we don't have. These are used when a field is missing, and the gap is recorded in EnemyData.importNotes.
    private const float AssumedSpeed = 300f;   // WC3 units/sec
    private const float AssumedArmor = 0f;
    private const int AssumedBountyDice = 1;

    [MenuItem("Tools/Import WC3 Waves and Creeps")]
    public static void ImportMenu()
    {
        Import();
    }

    /// <summary>Entry point for batch mode: opens the main scene, imports, wires WaveManager, saves.</summary>
    public static void ImportAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        WaveSet waves = Import();

        var waveManager = UnityEngine.Object.FindFirstObjectByType<WaveManager>();
        if (waveManager == null)
            throw new InvalidOperationException("No WaveManager in the scene. Run Tools > Build Game Data first.");
        GameDataBuilder.WireObject(waveManager, "waveSet", waves);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("WaveImporter: done.");
    }

    private static WaveSet Import()
    {
        Directory.CreateDirectory(EnemyDir);
        Directory.CreateDirectory(Path.GetDirectoryName(WaveSetPath));

        var units = Wc3Data.ParseUnits(Wc3Data.MapFile("war3map.w3u"));
        var strings = Wc3Data.ParseStrings(Wc3Data.MapFile("war3map.wts"));
        var levels = ParseLevels(File.ReadAllText(Wc3Data.MapFile("war3map.j")));

        var enemyByType = new Dictionary<string, EnemyData>();
        var waveList = new List<WaveSet.Wave>();
        int maxLevel = 0;
        foreach (int level in levels.Keys) maxLevel = Mathf.Max(maxLevel, level);

        for (int level = 1; level <= maxLevel; level++)
        {
            if (!levels.TryGetValue(level, out var lv))
                throw new InvalidOperationException($"Set Levels has no entry for level {level}.");

            if (!enemyByType.TryGetValue(lv.type, out EnemyData enemy))
            {
                if (!units.TryGetValue(lv.type, out var fields))
                    throw new InvalidOperationException($"Level {level} uses unit '{lv.type}' which is not defined in war3map.w3u.");
                enemy = SaveEnemy(lv.type, fields, strings);
                enemyByType[lv.type] = enemy;
            }
            waveList.Add(new WaveSet.Wave { enemy = enemy, enemyCount = lv.amount });
        }

        var waveSet = AssetDatabase.LoadAssetAtPath<WaveSet>(WaveSetPath);
        if (waveSet == null)
        {
            waveSet = ScriptableObject.CreateInstance<WaveSet>();
            AssetDatabase.CreateAsset(waveSet, WaveSetPath);
        }
        waveSet.waves = waveList.ToArray();
        EditorUtility.SetDirty(waveSet);
        AssetDatabase.SaveAssets();

        Debug.Log($"WaveImporter: imported {waveList.Count} levels using {enemyByType.Count} creep types.");
        return waveSet;
    }

    private static EnemyData SaveEnemy(string id, Dictionary<string, object> f, Dictionary<int, string> strings)
    {
        string path = $"{EnemyDir}/{id}.asset";
        var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
        bool isNew = enemy == null;
        if (isNew) enemy = ScriptableObject.CreateInstance<EnemyData>();

        var notes = new List<string>();

        string name = f.TryGetValue("unam", out var n) ? Wc3Data.ResolveString((string)n, strings) : null;
        enemy.displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();

        enemy.health = (float)Wc3Data.GetNumber(f, "uhpm", 0, notes, "hit points (uhpm)");

        double speed = Wc3Data.GetNumber(f, "umvs", AssumedSpeed, notes, $"speed (umvs), assumed {AssumedSpeed}");
        enemy.speed = (float)(speed / Wc3Data.WorldUnitsPerCell);

        enemy.armor = (float)Wc3Data.GetNumber(f, "udef", AssumedArmor, notes, $"armor (udef), assumed {AssumedArmor}");

        // Bounty = base + dice * (sides + 1) / 2 (average roll)
        double bountyBase = Wc3Data.GetNumber(f, "ubba", 0, notes, "bounty base (ubba), assumed 0");
        double dice = Wc3Data.GetNumber(f, "ubdi", AssumedBountyDice, notes, $"bounty dice (ubdi), assumed {AssumedBountyDice}");
        double sides = Wc3Data.GetNumber(f, "ubsi", 1, notes, "bounty sides (ubsi), assumed 1");
        enemy.goldReward = Mathf.Max(0, Mathf.RoundToInt((float)(bountyBase + dice * (sides + 1) / 2.0)));

        enemy.importNotes = notes.Count == 0 ? "" : "Not defined in the map, assumed: " + string.Join("; ", notes);

        if (isNew) AssetDatabase.CreateAsset(enemy, path);
        EditorUtility.SetDirty(enemy);
        return enemy;
    }

    // ---------- war3map.j: Set Levels ----------

    private struct LevelDef { public string type; public int amount; }

    private static Dictionary<int, LevelDef> ParseLevels(string jass)
    {
        jass = jass.Replace("\r", "");

        // Each condition function tests "udg_Level_Number == N"
        var conditionLevel = new Dictionary<string, int>();
        foreach (Match m in Regex.Matches(jass,
            @"function (Trig_Set_Levels_Func\d+) takes nothing returns boolean\s*return \( udg_Level_Number == (\d+) \)"))
            conditionLevel[m.Groups[1].Value] = int.Parse(m.Groups[2].Value);

        Match body = Regex.Match(jass,
            @"function Trig_Set_Levels_Actions takes nothing returns nothing(.*?)endfunction", RegexOptions.Singleline);
        if (!body.Success) throw new InvalidOperationException("Trig_Set_Levels_Actions not found in war3map.j");

        var levels = new Dictionary<int, LevelDef>();
        foreach (Match m in Regex.Matches(body.Groups[1].Value,
            @"if \( (Trig_Set_Levels_Func\d+)\(\) \) then\s*set udg_(Monster_Type|Monster_Amount) = (\S+)"))
        {
            int level = conditionLevel[m.Groups[1].Value];
            levels.TryGetValue(level, out LevelDef def);
            if (m.Groups[2].Value == "Monster_Type") def.type = m.Groups[3].Value.Trim('\'');
            else def.amount = int.Parse(m.Groups[3].Value);
            levels[level] = def;
        }
        return levels;
    }
}
