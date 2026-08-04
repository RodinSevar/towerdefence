using UnityEngine;
using UnityEditor;

public class WintermaulSpawnerGenerator : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Generate Wintermaul Spawners")]
    public static void GenerateSpawners()
    {
        GameObject root = GameObject.Find("Spawners");
        if (root != null)
        {
            DestroyImmediate(root);
        }

        root = new GameObject("Spawners");

        // The map is approx 196x196, from -98 to +98 on X and Z axes.
        // Let's define the player colors and approximate locations.
        // We need 18 spawners (2 per player).

        Color teal = new Color(0f, 0.8f, 0.8f);
        Color orange = new Color(1f, 0.5f, 0f);
        Color purple = new Color(0.6f, 0f, 0.8f);
        Color pink = new Color(1f, 0.4f, 0.7f);

        CreatePlayerSpawners(root, "Top Left (Red)", Color.red, new Vector3(-80, 0, 80), new Vector3(-80, 0, -80), new Vector3(0, 0, -80));
        CreatePlayerSpawners(root, "Top Middle (Blue)", Color.blue, new Vector3(0, 0, 80), new Vector3(0, 0, -80), new Vector3(0, 0, -80));
        CreatePlayerSpawners(root, "Top Right (Teal)", teal, new Vector3(80, 0, 80), new Vector3(80, 0, -80), new Vector3(0, 0, -80));

        CreatePlayerSpawners(root, "Middle Left (Orange)", orange, new Vector3(-80, 0, 0), new Vector3(-80, 0, -80), new Vector3(0, 0, -80));
        CreatePlayerSpawners(root, "Middle Right (Purple)", purple, new Vector3(80, 0, 0), new Vector3(80, 0, -80), new Vector3(0, 0, -80));

        CreatePlayerSpawners(root, "Bottom Left (Green)", Color.green, new Vector3(-80, 0, -40), new Vector3(-80, 0, -80), new Vector3(0, 0, -80));
        CreatePlayerSpawners(root, "Bottom Right (Pink)", pink, new Vector3(80, 0, -40), new Vector3(80, 0, -80), new Vector3(0, 0, -80));

        // SPLIT PLAYERS
        // Center Player (Yellow): Left spawn goes Bottom Left. Right spawn goes Bottom Right.
        GameObject center = new GameObject("Center (Yellow)");
        center.transform.SetParent(root.transform);
        CreateSpawner(center, "Spawn L", Color.yellow, new Vector3(-10, 0, 0), new Vector3(-80, 0, -80), new Vector3(0, 0, -80));
        CreateSpawner(center, "Spawn R", Color.yellow, new Vector3(10, 0, 0), new Vector3(80, 0, -80), new Vector3(0, 0, -80));

        // Bottom Middle (Gray): Split up? The user said "center player and middle bottom player will have them split up."
        // We'll split them but both eventually converge to Bottom Middle.
        GameObject bMiddle = new GameObject("Bottom Middle (Gray)");
        bMiddle.transform.SetParent(root.transform);
        CreateSpawner(bMiddle, "Spawn L", Color.gray, new Vector3(-15, 0, -60), new Vector3(-20, 0, -80), new Vector3(0, 0, -80));
        CreateSpawner(bMiddle, "Spawn R", Color.gray, new Vector3(15, 0, -60), new Vector3(20, 0, -80), new Vector3(0, 0, -80));

        Debug.Log("Successfully generated 9 Players (18 Spawners)!");
    }

    private static void CreatePlayerSpawners(GameObject root, string name, Color color, Vector3 spawnCenter, Vector3 cornerWP, Vector3 finalWP)
    {
        GameObject pNode = new GameObject(name);
        pNode.transform.SetParent(root.transform);
        
        CreateSpawner(pNode, "Spawn L", color, spawnCenter + new Vector3(-3, 0, 0), cornerWP, finalWP);
        CreateSpawner(pNode, "Spawn R", color, spawnCenter + new Vector3(3, 0, 0), cornerWP, finalWP);
    }

    private static void CreateSpawner(GameObject parent, string name, Color color, Vector3 start, Vector3 mid, Vector3 end)
    {
        GameObject sObj = new GameObject(name);
        sObj.transform.SetParent(parent.transform);
        sObj.transform.position = start;
        
        Spawner spawner = sObj.AddComponent<Spawner>();
        spawner.playerColor = color;
        
        // Offset Y slightly for visibility and navigation
        start.y = 0.5f;
        mid.y = 0.5f;
        end.y = 0.5f;

        // If mid and end are the same, we just need 2 waypoints
        if (Vector3.Distance(mid, end) < 1f)
        {
            spawner.waypoints = new Vector3[] { start, end };
        }
        else
        {
            spawner.waypoints = new Vector3[] { start, mid, end };
        }
    }
#endif
}
