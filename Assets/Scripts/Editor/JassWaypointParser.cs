using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class JassWaypointParser
{
    [MenuItem("Tools/Auto-Assign Waypoints from JASS")]
    public static void ParseJass()
    {
        // Automatically regenerate the region dump so we have the latest correct coordinates
        W3RImporter.DumpW3R();

        string jassPath = "mpq_files/war3map.j";
        if (!File.Exists(jassPath))
        {
            Debug.LogError("Could not find war3map.j in the mpq_files folder.");
            return;
        }

        string dumpPath = "region_dump.txt";
        if (!File.Exists(dumpPath))
        {
            Debug.LogError("Could not find region_dump.txt. Please run the W3R Importer first.");
            return;
        }

        string[] lines = File.ReadAllLines(jassPath);

        Dictionary<string, string> trigToEnterRegion = new Dictionary<string, string>();
        Dictionary<string, string> trigToActionFunc = new Dictionary<string, string>();
        Dictionary<string, string> actionFuncToDestRegion = new Dictionary<string, string>();

        // 1. Parse the JASS code to build the logical flow of triggers
        string fullJass = File.ReadAllText(jassPath);

        // Match Trigger Register: call TriggerRegisterEnterRectSimple( gg_trg_TrigName, gg_rct_RegionName )
        foreach (Match m in Regex.Matches(fullJass, @"TriggerRegisterEnterRectSimple\s*\(\s*gg_trg_([^,]+),\s*gg_rct_([^\s\)]+)"))
        {
            trigToEnterRegion[m.Groups[1].Value] = m.Groups[2].Value;
        }

        // Match Trigger Action: call TriggerAddAction( gg_trg_TrigName, function ActionFuncName )
        foreach (Match m in Regex.Matches(fullJass, @"TriggerAddAction\s*\(\s*gg_trg_([^,]+),\s*function\s+([^\s\)]+)"))
        {
            trigToActionFunc[m.Groups[1].Value] = m.Groups[2].Value;
        }

        // Parse each function block to find the destination region and unit groups
        Dictionary<string, string> funcToGroup = new Dictionary<string, string>();
        Dictionary<string, string> groupToDestFunc = new Dictionary<string, string>();

        foreach (Match funcMatch in Regex.Matches(fullJass, @"function\s+([^\s]+)\s+takes.*?endfunction", RegexOptions.Singleline))
        {
            string funcName = funcMatch.Groups[1].Value;
            string funcBody = funcMatch.Value;

            // 1. Look for movement orders inside this function body
            if (funcBody.Contains("IssuePointOrder") || funcBody.Contains("IssueTargetOrder") || funcBody.Contains("GetRectCenter"))
            {
                Match destMatch = Regex.Match(funcBody, @"gg_rct_([^\s\)\,,]+)");
                if (destMatch.Success)
                {
                    actionFuncToDestRegion[funcName] = destMatch.Groups[1].Value;
                }
            }

            // 2. Look for GroupAddUnitSimple
            Match groupAddMatch = Regex.Match(funcBody, @"GroupAddUnitSimple\s*\(\s*GetEnumUnit\(\),\s*(udg_[^\s\)]+)");
            if (groupAddMatch.Success)
            {
                funcToGroup[funcName] = groupAddMatch.Groups[1].Value;
            }

            // 3. Look for ForGroupBJ issuing orders to a group
            Match forGroupMatch = Regex.Match(funcBody, @"ForGroupBJ\s*\(\s*(udg_[^\s,]+),\s*function\s+([^\s\)]+)");
            if (forGroupMatch.Success)
            {
                groupToDestFunc[forGroupMatch.Groups[1].Value] = forGroupMatch.Groups[2].Value;
            }
        }

        // 4. Look for the initial ForGroupBJ( GetUnitsInRectOfPlayer(gg_rct_SPAWN... )
        Dictionary<string, string> spawnToFunc = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(fullJass, @"ForGroupBJ\s*\(\s*GetUnitsInRectOfPlayer\s*\(\s*gg_rct_([^,]+).*?function\s+([^\s\)]+)"))
        {
            spawnToFunc[m.Groups[1].Value] = m.Groups[2].Value;
        }

        // Link them together: EnterRegion -> DestRegion
        Dictionary<string, string> nextRegionMap = new Dictionary<string, string>();
        
        // Link the standard triggers
        foreach (var kvp in trigToEnterRegion)
        {
            string trigName = kvp.Key;
            string enterRegion = kvp.Value;

            if (trigToActionFunc.ContainsKey(trigName))
            {
                string actionFunc = trigToActionFunc[trigName];
                if (actionFuncToDestRegion.ContainsKey(actionFunc))
                {
                    nextRegionMap[enterRegion] = actionFuncToDestRegion[actionFunc];
                }
            }
        }

        // Link the spawn unit groups! (Spawn -> Func -> Group -> DestFunc -> Dest)
        foreach (var kvp in spawnToFunc)
        {
            string spawnRegion = kvp.Key;
            string funcA = kvp.Value;
            if (funcToGroup.ContainsKey(funcA))
            {
                string group = funcToGroup[funcA];
                if (groupToDestFunc.ContainsKey(group))
                {
                    string funcB = groupToDestFunc[group];
                    if (actionFuncToDestRegion.ContainsKey(funcB))
                    {
                        nextRegionMap[spawnRegion] = actionFuncToDestRegion[funcB];
                    }
                }
            }
        }


        Debug.Log($"Successfully extracted {nextRegionMap.Count} pathing connections from JASS logic!");

        // 3. Load the Unity coordinates for all regions from our text dump
        Dictionary<string, Vector3> regionCoords = new Dictionary<string, Vector3>();
        string[] dumpLines = File.ReadAllLines(dumpPath);
        string curRegion = "";
        
        foreach (string line in dumpLines)
        {
            if (line.StartsWith("Region Name: "))
            {
                // JASS replaces spaces with underscores in region names!
                curRegion = line.Replace("Region Name: ", "").Trim().Replace(" ", "_");
            }
            else if (line.StartsWith("Unity Pos:"))
            {
                Match m = Regex.Match(line, @"X:\s*([-\d\.]+),\s*Z:\s*([-\d\.]+)");
                if (m.Success && !string.IsNullOrEmpty(curRegion))
                {
                    float x = float.Parse(m.Groups[1].Value);
                    float z = float.Parse(m.Groups[2].Value);
                    regionCoords[curRegion] = new Vector3(x, 0.5f, z); // Y is 0.5f to sit above ground
                }
            }
        }

        // 4. Auto-Assign Waypoints to Spawners in the Scene
        GameObject spawnersRoot = GameObject.Find("Spawners");
        if (spawnersRoot == null)
        {
            Debug.LogError("Spawners root not found in scene!");
            return;
        }

        int applied = 0;
        foreach (Spawner spawner in spawnersRoot.GetComponentsInChildren<Spawner>())
        {
            if (spawner.waypoints == null || spawner.waypoints.Length == 0) continue;
            Vector3 startPos = spawner.waypoints[0];
            
            // Find the closest region to the spawner's start position
            string closestRegion = "";
            float closestDist = float.MaxValue;
            foreach (var kvp in regionCoords)
            {
                float dist = Vector3.Distance(startPos, kvp.Value);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestRegion = kvp.Key;
                }
            }

            // If the spawner isn't inside a region (threshold of 10 units), skip it
            if (closestDist > 10f) 
            {
                Debug.LogWarning($"Spawner {spawner.gameObject.name} is too far from any WC3 region ({closestDist:F1} units). Cannot auto-route.");
                continue;
            }

            // Trace the path from this region to the end!
            List<Vector3> path = new List<Vector3>();
            path.Add(startPos); // Waypoint 0 is the spawn location itself
            
            string currentRegion = closestRegion;
            int maxDepth = 20; // Safety break against infinite loops
            
            while (nextRegionMap.ContainsKey(currentRegion) && maxDepth > 0)
            {
                string nextRegion = nextRegionMap[currentRegion];
                
                if (regionCoords.ContainsKey(nextRegion))
                {
                    path.Add(regionCoords[nextRegion]);
                }
                
                currentRegion = nextRegion;
                maxDepth--;
            }

            // Always ensure the final destination (the bottom 'Load' / Lives area) is the last waypoint!
            // This prevents creeps from instantly dying if the JASS script obfuscated their intermediate corners.
            path.Add(new Vector3(-4f, 0f, -80f)); 

            // Assign the traced path to the spawner
            Undo.RecordObject(spawner, "Auto-assign JASS Waypoints");
            spawner.waypoints = path.ToArray();
            EditorUtility.SetDirty(spawner);
            applied++;
        }

        SceneView.RepaintAll();
        Debug.Log($"Successfully auto-wired waypoints for {applied} Spawners using WC3 JASS triggers!");
    }
}
