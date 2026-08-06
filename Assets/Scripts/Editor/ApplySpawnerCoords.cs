using UnityEngine;
using UnityEditor;
using System.IO;

public class ApplySpawnerCoords
{
    [MenuItem("Tools/Apply Spawner Coords")]
    public static void ApplyCoords()
    {
        string path = "spawner_coords.txt";
        if (!File.Exists(path))
        {
            Debug.LogError("Could not find spawner_coords.txt in the root of the project!");
            return;
        }

        string[] lines = File.ReadAllLines(path);
        
        GameObject spawnersRoot = GameObject.Find("Spawners");
        if (spawnersRoot == null)
        {
            Debug.LogError("Could not find 'Spawners' root object in the Hierarchy. Please generate the spawners first.");
            return;
        }

        string currentTarget = "";
        int appliedCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.ToLower().Contains("spawn"))
            {
                currentTarget = line;
            }
            else
            {
                // It's the coordinates line
                string[] parts = line.Split(new char[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    float left = float.Parse(parts[0]);
                    float right = float.Parse(parts[1]);
                    float bottom = float.Parse(parts[2]);
                    float top = float.Parse(parts[3]);
                    
                    float centerX = (left + right) / 2f;
                    float centerZ = (bottom + top) / 2f;

                    // Convert to Unity coordinates and set Y to 1.0 to stay above ground
                    Vector3 unityPos = new Vector3(centerX / 64f, 1.0f, centerZ / 64f);

                    if (ApplyToHierarchy(spawnersRoot, currentTarget, unityPos))
                    {
                        appliedCount++;
                    }
                }
            }
        }
        
        SceneView.RepaintAll();
        Debug.Log($"Successfully applied coordinates to {appliedCount} spawner zones!");
    }

    private static bool ApplyToHierarchy(GameObject root, string targetName, Vector3 pos)
    {
        targetName = targetName.ToLower().Trim();
        
        string playerName = "";
        string spawnName = "";
        bool both = false;

        switch (targetName)
        {
            case "top left right spawn": playerName = "Top Left (Red)"; spawnName = "Spawn R"; break;
            case "top left left spawn": playerName = "Top Left (Red)"; spawnName = "Spawn L"; break;
            case "top middle left spawn": playerName = "Top Middle (Blue)"; spawnName = "Spawn L"; break;
            case "top middle right spawn": playerName = "Top Middle (Blue)"; spawnName = "Spawn R"; break;
            case "top right right spawn": playerName = "Top Right (Teal)"; spawnName = "Spawn R"; break;
            case "top right left spawn": playerName = "Top Right (Teal)"; spawnName = "Spawn L"; break;
            
            case "middle left upper spawn": playerName = "Middle Left (Orange)"; spawnName = "Spawn L"; break;
            case "middle left lower spawn": playerName = "Middle Left (Orange)"; spawnName = "Spawn R"; break;
            case "middle middle left spawn": playerName = "Center (Yellow)"; spawnName = "Spawn L"; break;
            case "midle middle right spawn": playerName = "Center (Yellow)"; spawnName = "Spawn R"; break;
            case "middle right top spawn": playerName = "Middle Right (Purple)"; spawnName = "Spawn L"; break;
            case "middle right lower spawn": playerName = "Middle Right (Purple)"; spawnName = "Spawn R"; break;

            case "bottom left single spawn": playerName = "Bottom Left (Green)"; both = true; break;
            case "bottom right single spawn": playerName = "Bottom Right (Pink)"; both = true; break;
            case "bottom middle single spawn": playerName = "Bottom Middle (Gray)"; both = true; break;
            default:
                Debug.LogError($"Unrecognized spawn name in file: '{targetName}'");
                break;
        }

        if (playerName == "") 
        {
            Debug.LogWarning("Could not map name from text file: " + targetName);
            return false;
        }

        Transform playerTransform = root.transform.Find(playerName);
        if (playerTransform == null) 
        {
            Debug.LogWarning("Could not find player object in Hierarchy: " + playerName);
            return false;
        }

        bool success = false;
        if (both)
        {
            success |= SetWaypoint(playerTransform.Find("Spawn L"), pos, 0);
            success |= SetWaypoint(playerTransform.Find("Spawn R"), pos, 20); // Offset the R spawner so it continues the rectangle!
        }
        else
        {
            success |= SetWaypoint(playerTransform.Find(spawnName), pos, 0);
        }
        return success;
    }

    private static bool SetWaypoint(Transform spawnTransform, Vector3 pos, int offset)
    {
        if (spawnTransform == null) return false;
        Spawner spawner = spawnTransform.GetComponent<Spawner>();
        if (spawner != null && spawner.waypoints != null && spawner.waypoints.Length > 0)
        {
            Undo.RecordObject(spawner, "Apply Spawner Coords");
            spawner.waypoints[0] = pos;
            spawner.indexOffset = offset;
            EditorUtility.SetDirty(spawner);
            return true;
        }
        return false;
    }
}
