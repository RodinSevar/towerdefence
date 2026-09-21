using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rebuilds the scene's spawners and creep routes from the real map's triggers (war3map.j):
///   - Core Funtion:      creates the level's creeps at 15 regions (which region, how many)
///   - Startup Movement:  puts creeps into unit groups by spawn region, then runs the Move_* triggers that give each
///                        group its FIRST move order
///   - "enter region" triggers: when a creep of a group enters a region, it is ordered to the next region
///   - Unit Board Ship:   the exit region (entering it costs a life)
/// The importer follows those orders group by group to produce each group's route as a list of <see cref="RouteStep"/>.
/// Coordinates are the map's world units divided by 64 (one Unity cell). The "Spawners" hierarchy is replaced.
/// </summary>
public static class RouteImporter
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const float SpawnHeight = 0.5f;

    private class MapRect
    {
        public string name;
        public Vector2 min, max; // Unity units (x, z)
        public Vector2 Center => (min + max) * 0.5f;
        public bool Contains(Vector2 p) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y;
    }

    private class Transition
    {
        public string trigger;
        public string region;          // rect whose entry fires the trigger
        public List<string> groups;    // groups it applies to (empty = any creep)
        public string orderRect;       // rect the creep is then ordered to
    }

    // The map names its groups by side; these are the players' spawns (region -> player), as in the map's layout.
    private static readonly Dictionary<string, (string player, Color color)> Players = new Dictionary<string, (string, Color)>
    {
        { "Left_Spawn",           ("Top Left (Red)", Color.red) },
        { "Left_Spawn_2",         ("Top Left (Red)", Color.red) },
        { "Middle_Left_Spawn",    ("Top Middle (Blue)", Color.blue) },
        { "Middle_Right_Spawn",   ("Top Middle (Blue)", Color.blue) },
        { "Right_Spawn",          ("Top Right (Teal)", new Color(0f, 0.8f, 0.8f)) },
        { "Right_Spawn_2",        ("Top Right (Teal)", new Color(0f, 0.8f, 0.8f)) },
        { "orange_upper",         ("Middle Left (Orange)", new Color(1f, 0.5f, 0f)) },
        { "orange_lower",         ("Middle Left (Orange)", new Color(1f, 0.5f, 0f)) },
        { "yellow_left",          ("Center (Yellow)", Color.yellow) },
        { "yellow_right",         ("Center (Yellow)", Color.yellow) },
        { "purp_upper",           ("Middle Right (Purple)", new Color(0.6f, 0f, 0.8f)) },
        { "purp_lower",           ("Middle Right (Purple)", new Color(0.6f, 0f, 0.8f)) },
        { "Bottom_Left_Spawn",    ("Bottom Left (Green)", Color.green) },
        { "Bottom_Right_Spawn",   ("Bottom Right (Pink)", new Color(1f, 0.4f, 0.7f)) },
        { "Left_Move_2",          ("Bottom Middle (Gray)", Color.gray) },
    };

    public static void ImportMenu()
    {
        Import();
    }

    /// <summary>Entry point for batch mode: opens the main scene, rebuilds the spawners, saves.</summary>
    public static void ImportAndSaveScene()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Import();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("RouteImporter: done.");
    }

    private static void Import()
    {
        string jass = File.ReadAllText(Wc3Data.MapFile("war3map.j")).Replace("\r", "");
        var rects = ParseRects(jass);
        var funcs = ParseFunctions(jass);

        // ---- spawn regions and creep counts (Core Funtion) ----
        var spawns = new List<(string region, int amount)>();
        foreach (Match m in RxMatches(Body(funcs, "Trig_Core_Funtion_Actions"),
            @"CreateNUnitsAtLoc\( (\S+), udg_Monster_Type, Player\(11\), GetRectCenter\(gg_rct_(\w+)\)"))
        {
            string amount = m.Groups[1].Value;
            spawns.Add((m.Groups[2].Value, amount == "udg_Monster_Amount" ? -1 : int.Parse(amount)));
        }
        if (spawns.Count == 0) throw new InvalidOperationException("No CreateNUnitsAtLoc calls found in Trig_Core_Funtion_Actions.");

        // ---- unit groups by spawn region, and each group's first order (Startup Movement) ----
        string startup = Body(funcs, "Trig_Startup_Movement_Actions");
        var regionToGroup = new Dictionary<string, string>();
        foreach (Match m in RxMatches(startup, @"GetUnitsInRectOfPlayer\(gg_rct_(\w+), Player\(11\)\), function (\w+)"))
        {
            var g = RxMatch(Body(funcs, m.Groups[2].Value), @"GroupAddUnitSimple\( GetEnumUnit\(\), udg_(\w+) \)");
            if (g.Success) regionToGroup[m.Groups[1].Value] = g.Groups[1].Value;
        }

        var firstOrder = new Dictionary<string, string>(); // group -> order rect
        foreach (Match m in RxMatches(startup, @"TriggerExecute\( gg_trg_(\w+) \)"))
        {
            string moveActions = Body(funcs, "Trig_" + m.Groups[1].Value + "_Actions");
            var forGroup = RxMatch(moveActions, @"ForGroupBJ\( udg_(\w+), function (\w+) \)");
            if (!forGroup.Success) continue;
            var order = RxMatch(Body(funcs, forGroup.Groups[2].Value), @"IssuePointOrderLocBJ\( GetEnumUnit\(\), ""move"", GetRectCenter\(gg_rct_(\w+)\) \)");
            if (order.Success) firstOrder[forGroup.Groups[1].Value] = order.Groups[1].Value;
        }

        // ---- "enter region" triggers: which region, which groups, ordered where ----
        var transitions = new List<Transition>();
        var exitRegions = new HashSet<string>();
        var triggerRegion = new Dictionary<string, string>();
        foreach (Match m in RxMatches(jass, @"TriggerRegisterEnterRectSimple\( gg_trg_(\w+), gg_rct_(\w+) \)"))
            triggerRegion[m.Groups[1].Value] = m.Groups[2].Value;
        var triggerAction = new Dictionary<string, string>();
        foreach (Match m in RxMatches(jass, @"TriggerAddAction\( gg_trg_(\w+), function (\w+) \)"))
            triggerAction[m.Groups[1].Value] = m.Groups[2].Value;
        var triggerCondition = new Dictionary<string, string>();
        foreach (Match m in RxMatches(jass, @"TriggerAddCondition\( gg_trg_(\w+), Condition\( function (\w+) \) \)"))
            triggerCondition[m.Groups[1].Value] = m.Groups[2].Value;

        foreach (var kv in triggerRegion)
        {
            string trigger = kv.Key, region = kv.Value;
            if (!triggerAction.TryGetValue(trigger, out string actionName)) continue;
            string action = Body(funcs, actionName);

            if (action.Contains("RemoveUnit( GetEnteringUnit() )")) exitRegions.Add(region);

            var conditionGroups = triggerCondition.TryGetValue(trigger, out string condName)
                ? GroupsIn(funcs, condName, new HashSet<string>())
                : new List<string>();

            // Walk the action line by line: an order inside "if ( FuncNNN() ) then" applies to that function's groups
            List<string> ifGroups = null;
            foreach (string raw in action.Split('\n'))
            {
                string line = raw.Trim();
                var ifMatch = RxMatch(line, @"^if \( (\w+)\(\) \) then");
                if (ifMatch.Success) { ifGroups = GroupsIn(funcs, ifMatch.Groups[1].Value, new HashSet<string>()); continue; }
                if (line.StartsWith("else") || line.StartsWith("endif")) { ifGroups = null; continue; }

                var order = RxMatch(line, @"IssuePointOrderLocBJ\( GetEnteringUnit\(\), ""move"", GetRectCenter\(gg_rct_(\w+)\) \)");
                if (!order.Success) continue;
                transitions.Add(new Transition
                {
                    trigger = trigger, region = region, orderRect = order.Groups[1].Value,
                    groups = ifGroups ?? conditionGroups,
                });
            }
        }

        // ---- follow each group's orders to build its route ----
        var routes = new Dictionary<string, List<RouteStep>>();
        foreach (var kv in firstOrder)
        {
            string group = kv.Key;
            var steps = new List<RouteStep>();
            string current = kv.Value;
            for (int guard = 0; guard < 16; guard++)
            {
                MapRect order = GetRect(rects, current);
                if (exitRegions.Contains(current))
                {
                    steps.Add(Step(current, order.Center, order, true));
                    break;
                }

                // The trigger that fires when this group enters the region containing the order point
                Transition next = transitions.Find(t => t.groups.Contains(group) && GetRect(rects, t.region).Contains(order.Center));
                if (next == null)
                    throw new InvalidOperationException($"Group {group}: no trigger takes over at '{current}'.");
                steps.Add(Step(current, order.Center, GetRect(rects, next.region), false));
                current = next.orderRect;
            }
            routes[group] = steps;
        }

        // ---- rebuild the Spawners hierarchy ----
        GameObject old;
        while ((old = GameObject.Find("Spawners")) != null) UnityEngine.Object.DestroyImmediate(old);
        var root = new GameObject("Spawners");

        var playerNodes = new Dictionary<string, Transform>();
        foreach (var (region, amount) in spawns)
        {
            if (!Players.TryGetValue(region, out var player))
                throw new InvalidOperationException($"No player defined for spawn region '{region}'.");
            MapRect spawn = GetRect(rects, region);

            List<RouteStep> steps;
            string groupName;
            if (regionToGroup.TryGetValue(region, out groupName) && routes.TryGetValue(groupName, out steps))
            {
                // route from the map's triggers
            }
            else
            {
                // The map creates these creeps but never gives them a group or an order, so in the original they stand idle
                // (a gap in the map's logic). The intended behavior is to walk to the exit, so send them straight there.
                groupName = "(idle in the original map; routed straight to the exit)";
                string exit = new List<string>(exitRegions)[0];
                MapRect exitRect = GetRect(rects, exit);
                steps = new List<RouteStep> { Step(exit, exitRect.Center, exitRect, true) };
                Debug.LogWarning($"RouteImporter: spawn '{region}' has no group and no orders in the map (its creeps idle in the original); routed straight to '{exit}' as intended.");
            }

            if (!playerNodes.TryGetValue(player.player, out Transform node))
            {
                node = new GameObject(player.player).transform;
                node.SetParent(root.transform, false);
                playerNodes[player.player] = node;
            }

            var go = new GameObject(region.Replace('_', ' '));
            go.transform.SetParent(node, false);
            go.transform.position = new Vector3(spawn.Center.x, SpawnHeight, spawn.Center.y);
            var spawner = go.AddComponent<Spawner>();
            spawner.playerColor = player.color;
            spawner.amountOverride = amount;
            spawner.routeName = groupName;
            spawner.steps = steps.ToArray();

            Debug.Log($"RouteImporter: {region} ({(amount < 0 ? "wave count" : amount + " creeps")}) group {groupName}: " +
                      string.Join(" -> ", steps.ConvertAll(s => s.name)));
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"RouteImporter: {spawns.Count} spawners, {routes.Count} routes.");
    }

    // ---------- parsing helpers ----------

    // The JASS is machine-generated but its spacing inside call parentheses varies. In these patterns every space means
    // "optional whitespace", so the regexes stay readable.
    private static Match RxMatch(string input, string pattern) => Regex.Match(input, pattern.Replace(" ", "\\s*"));
    private static MatchCollection RxMatches(string input, string pattern) => Regex.Matches(input, pattern.Replace(" ", "\\s*"));

    private static Dictionary<string, MapRect> ParseRects(string jass)
    {
        var rects = new Dictionary<string, MapRect>();
        foreach (Match m in RxMatches(jass, @"set gg_rct_(\w+) = Rect\( (-?[\d.]+), (-?[\d.]+), (-?[\d.]+), (-?[\d.]+) \)"))
        {
            float F(int i) => float.Parse(m.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture) / Wc3Data.WorldUnitsPerCell;
            // Rect(left, bottom, right, top)
            rects[m.Groups[1].Value] = new MapRect { name = m.Groups[1].Value, min = new Vector2(F(2), F(3)), max = new Vector2(F(4), F(5)) };
        }
        return rects;
    }

    private static Dictionary<string, string> ParseFunctions(string jass)
    {
        var funcs = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(jass, @"function (\w+) takes nothing returns \w+\n(.*?)\nendfunction", RegexOptions.Singleline))
            funcs[m.Groups[1].Value] = m.Groups[2].Value;
        return funcs;
    }

    private static string Body(Dictionary<string, string> funcs, string name)
    {
        if (!funcs.TryGetValue(name, out string body)) throw new InvalidOperationException($"Function '{name}' not found in war3map.j");
        return body;
    }

    /// <summary>Unit groups a condition function tests with IsUnitInGroup (following calls into helper functions).</summary>
    private static List<string> GroupsIn(Dictionary<string, string> funcs, string function, HashSet<string> visited)
    {
        var groups = new List<string>();
        if (!visited.Add(function) || !funcs.TryGetValue(function, out string body)) return groups;

        foreach (Match m in RxMatches(body, @"IsUnitInGroup\( GetEnteringUnit\(\), udg_(\w+) \)"))
            if (!groups.Contains(m.Groups[1].Value)) groups.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(body, @"\b(Trig_\w+)\(\)"))
            foreach (string g in GroupsIn(funcs, m.Groups[1].Value, visited))
                if (!groups.Contains(g)) groups.Add(g);
        return groups;
    }

    private static MapRect GetRect(Dictionary<string, MapRect> rects, string name)
    {
        if (!rects.TryGetValue(name, out MapRect r)) throw new InvalidOperationException($"Rect '{name}' is not defined in war3map.j");
        return r;
    }

    private static RouteStep Step(string orderRectName, Vector2 target, MapRect region, bool isExit)
    {
        return new RouteStep
        {
            name = orderRectName.Replace('_', ' '),
            target = new Vector3(target.x, SpawnHeight, target.y),
            regionMin = region.min,
            regionMax = region.max,
            isExit = isExit,
        };
    }
}
