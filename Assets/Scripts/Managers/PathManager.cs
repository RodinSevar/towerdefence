using UnityEngine;
using System.Collections.Generic;

public class PathManager : MonoBehaviour
{
    public static PathManager Instance { get; private set; }

    [SerializeField]
    private Vector3[] waypoints;

    [SerializeField]
    private bool drawDebugPath = true;

    public System.Action OnMazeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            CreateDefaultPath();
        }
        
        CreateWaypointVisuals();
    }

    private void CreateDefaultPath()
    {
        // Spawns enemies on the far left, and they walk to the far right.
        waypoints = new Vector3[]
        {
            new Vector3(-90, 0.5f, 0),
            new Vector3(90, 0.5f, 0)
        };
    }

    private void CreateWaypointVisuals()
    {
        for (int i = 0; i < waypoints.Length; i++)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Waypoint_{i}";
            marker.transform.position = waypoints[i];
            marker.transform.localScale = new Vector3(2f, 0.1f, 2f); // Flat disc
            
            // Remove collider so it doesn't block rays or physics
            Destroy(marker.GetComponent<Collider>());
            
            // Set color based on type
            Material mat = marker.GetComponent<Renderer>().material;
            if (i == 0)
                mat.color = Color.blue; // Spawn (Green might blend with ground)
            else if (i == waypoints.Length - 1)
                mat.color = Color.red; // Finish
            else
                mat.color = Color.yellow; // Intermediate
        }
    }

    public Vector3 GetSpawnPoint()
    {
        if (waypoints != null && waypoints.Length > 0) return waypoints[0];
        return new Vector3(-90, 0.5f, 0);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugPath || waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Gizmos.DrawSphere(waypoints[i], 0.3f);
            if (i < waypoints.Length - 1)
            {
                Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            }
        }
    }

    public Vector3 GetWaypoint(int index)
    {
        if (index >= 0 && index < waypoints.Length)
            return waypoints[index];
        return waypoints[waypoints.Length - 1];
    }

    public int GetWaypointCount() => waypoints.Length;

    public void NotifyMazeChanged()
    {
        OnMazeChanged?.Invoke();
    }

    public bool ValidateFullMaze()
    {
        if (waypoints == null || waypoints.Length < 2) return true;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            var path = FindPath(waypoints[i], waypoints[i+1]);
            if (path == null || path.Count == 0) return false;
        }
        return true;
    }

    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        Vector2Int startNode = GridManager.Instance.WorldToGridCell(startPos);
        Vector2Int targetNode = GridManager.Instance.WorldToGridCell(targetPos);

        List<Vector2Int> path = FindPathGrid(startNode, targetNode);
        if (path == null) return null;

        List<Vector3> worldPath = new List<Vector3>();
        float cellSize = GridManager.Instance.GetCellSize();
        foreach (var node in path)
        {
            worldPath.Add(new Vector3(node.x * cellSize, startPos.y, node.y * cellSize));
        }
        
        if (worldPath.Count > 0 && Vector3.Distance(worldPath[worldPath.Count - 1], targetPos) > 0.01f)
        {
            worldPath.Add(targetPos);
        }
        
        return worldPath;
    }

    private List<Vector2Int> FindPathGrid(Vector2Int startNode, Vector2Int targetNode)
    {
        List<Vector2Int> openSet = new List<Vector2Int>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        
        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float>();
        Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float>();

        openSet.Add(startNode);
        gScore[startNode] = 0;
        fScore[startNode] = GetDistance(startNode, targetNode);

        // Increased max iterations to safely cover the entire 196x196 map (38,000+ cells)
        int maxIterations = 50000;
        int iterations = 0;

        while (openSet.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                Debug.LogWarning("A* Pathfinding exceeded max iterations.");
                return null;
            }

            Vector2Int current = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (fScore.ContainsKey(openSet[i]) && fScore.ContainsKey(current))
                {
                    if (fScore[openSet[i]] < fScore[current] || (fScore[openSet[i]] == fScore[current] && GetDistance(openSet[i], targetNode) < GetDistance(current, targetNode)))
                    {
                        current = openSet[i];
                    }
                }
            }

            if (current == targetNode)
            {
                return RetracePath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector2Int neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor) || GridManager.Instance.IsCellOccupied(neighbor))
                {
                    continue;
                }

                float tentativeGScore = gScore.ContainsKey(current) ? gScore[current] + GetDistance(current, neighbor) : float.MaxValue;
                bool containsNeighbor = openSet.Contains(neighbor);
                
                if (!gScore.ContainsKey(neighbor)) gScore[neighbor] = float.MaxValue;

                if (tentativeGScore < gScore[neighbor] || !containsNeighbor)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + GetDistance(neighbor, targetNode);

                    if (!containsNeighbor)
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

    private List<Vector2Int> RetracePath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        path.Add(current);
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private float GetDistance(Vector2Int nodeA, Vector2Int nodeB)
    {
        int dstX = Mathf.Abs(nodeA.x - nodeB.x);
        int dstY = Mathf.Abs(nodeA.y - nodeB.y);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private List<Vector2Int> GetNeighbors(Vector2Int node)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                
                if (Mathf.Abs(x) == 1 && Mathf.Abs(y) == 1)
                {
                    if (GridManager.Instance.IsCellOccupied(new Vector2Int(node.x + x, node.y)) || 
                        GridManager.Instance.IsCellOccupied(new Vector2Int(node.x, node.y + y)))
                    {
                        continue;
                    }
                }

                neighbors.Add(new Vector2Int(node.x + x, node.y + y));
            }
        }
        return neighbors;
    }
}
