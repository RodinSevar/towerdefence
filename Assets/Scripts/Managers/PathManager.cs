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
            worldPath.Add(GridManager.Instance.GetWorldPosition(node));
        }
        
        if (worldPath.Count > 0 && Vector3.Distance(worldPath[worldPath.Count - 1], targetPos) > 0.01f)
        {
            worldPath.Add(targetPos);
        }
        
        return worldPath;
    }

    private int searchID = 0;
    private float[,] gScoreGrid = new float[200, 200];
    private float[,] fScoreGrid = new float[200, 200];
    private int[,] parentXGrid = new int[200, 200];
    private int[,] parentYGrid = new int[200, 200];
    private int[,] nodeSearchID = new int[200, 200];
    private bool[,] inClosedSet = new bool[200, 200];
    private bool[,] inOpenSet = new bool[200, 200];

    private bool IsInBounds(int ax, int ay)
    {
        return ax >= 0 && ax < 200 && ay >= 0 && ay < 200;
    }

    private void EnsureNodeInitialized(int ax, int ay)
    {
        if (nodeSearchID[ax, ay] != searchID)
        {
            gScoreGrid[ax, ay] = float.MaxValue;
            fScoreGrid[ax, ay] = float.MaxValue;
            inClosedSet[ax, ay] = false;
            inOpenSet[ax, ay] = false;
            nodeSearchID[ax, ay] = searchID;
        }
    }

    private List<Vector2Int> FindPathGrid(Vector2Int startNode, Vector2Int targetNode)
    {
        searchID++;
        
        int startX = startNode.x + 100;
        int startY = startNode.y + 100;

        if (!IsInBounds(startX, startY) || !IsInBounds(targetNode.x + 100, targetNode.y + 100)) return null;

        List<Vector2Int> openSet = new List<Vector2Int>(1000); // Pre-allocate to avoid GC spikes
        
        EnsureNodeInitialized(startX, startY);
        openSet.Add(startNode);
        inOpenSet[startX, startY] = true;
        gScoreGrid[startX, startY] = 0;
        fScoreGrid[startX, startY] = GetDistance(startNode, targetNode);

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

            // Find node with lowest F score
            int bestIndex = 0;
            Vector2Int current = openSet[0];
            float bestF = fScoreGrid[current.x + 100, current.y + 100];

            for (int i = 1; i < openSet.Count; i++)
            {
                Vector2Int node = openSet[i];
                float f = fScoreGrid[node.x + 100, node.y + 100];
                if (f < bestF)
                {
                    bestF = f;
                    current = node;
                    bestIndex = i;
                }
            }

            if (current == targetNode)
            {
                return RetracePath(startNode, current);
            }

            // Fast O(1) removal using Swap and Pop
            openSet[bestIndex] = openSet[openSet.Count - 1];
            openSet.RemoveAt(openSet.Count - 1);

            int cx = current.x + 100;
            int cy = current.y + 100;
            inOpenSet[cx, cy] = false;
            inClosedSet[cx, cy] = true;

            // Inline neighbor checks to prevent massive List allocations
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    
                    Vector2Int neighbor = new Vector2Int(current.x + dx, current.y + dy);
                    if (!IsValidNeighbor(current, neighbor)) continue;
                    
                    // Prevent cutting corners through walls
                    if (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1)
                    {
                        if (!IsValidNeighbor(current, new Vector2Int(current.x + dx, current.y)) || 
                            !IsValidNeighbor(current, new Vector2Int(current.x, current.y + dy)))
                        {
                            continue;
                        }
                    }

                    int nx = neighbor.x + 100;
                    int ny = neighbor.y + 100;
                    
                    if (!IsInBounds(nx, ny)) continue;
                    EnsureNodeInitialized(nx, ny);

                    if (inClosedSet[nx, ny]) continue;

                    float tentativeGScore = gScoreGrid[cx, cy] + GetDistance(current, neighbor);
                    
                    if (!inOpenSet[nx, ny] || tentativeGScore < gScoreGrid[nx, ny])
                    {
                        parentXGrid[nx, ny] = current.x;
                        parentYGrid[nx, ny] = current.y;
                        gScoreGrid[nx, ny] = tentativeGScore;
                        fScoreGrid[nx, ny] = tentativeGScore + GetDistance(neighbor, targetNode);

                        if (!inOpenSet[nx, ny])
                        {
                            openSet.Add(neighbor);
                            inOpenSet[nx, ny] = true;
                        }
                    }
                }
            }
        }

        return null;
    }

    private List<Vector2Int> RetracePath(Vector2Int startNode, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        path.Add(current);
        while (current != startNode)
        {
            int ax = current.x + 100;
            int ay = current.y + 100;
            current = new Vector2Int(parentXGrid[ax, ay], parentYGrid[ax, ay]);
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

    private bool IsValidNeighbor(Vector2Int from, Vector2Int to)
    {
        if (GridManager.Instance.IsCellOccupied(to)) return false;
        
        float heightFrom = GridManager.Instance.GetCellHeight(from);
        float heightTo = GridManager.Instance.GetCellHeight(to);
        
        // Prevent walking straight up/down steep cliffs (height diff > 1.2)
        // Ramps have an intermediate height so they allow traversal.
        if (Mathf.Abs(heightFrom - heightTo) > 1.2f) return false;
        
        return true;
    }
}
