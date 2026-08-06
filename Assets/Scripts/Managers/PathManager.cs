using UnityEngine;
using System.Collections.Generic;

public class PathManager : MonoBehaviour
{
    public static PathManager Instance { get; private set; }

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
    }

    public class FlowField
    {
        public Vector2Int target;
        public float[,] distanceGrid = new float[200, 200];
        public Vector2[,] vectorGrid = new Vector2[200, 200];
        public bool hasVectors = false;
        public bool needsDistanceUpdate = true;
    }

    private Dictionary<Vector2Int, FlowField> flowFieldCache = new Dictionary<Vector2Int, FlowField>();
    private MinHeap openSet = new MinHeap(40000);

    public void ClearCache()
    {
        foreach (var ff in flowFieldCache.Values)
        {
            ff.needsDistanceUpdate = true;
            ff.hasVectors = false;
        }
    }

    public void NotifyMazeChanged()
    {
        ClearCache();
        OnMazeChanged?.Invoke();
    }

    public bool ValidateFullMaze()
    {
        if (WaveManager.Instance == null) return true;
        
        foreach (var spawner in WaveManager.Instance.activeSpawners)
        {
            if (spawner.waypoints == null || spawner.waypoints.Length < 2) continue;
            for (int i = 0; i < spawner.waypoints.Length - 1; i++)
            {
                Vector2Int startNode = GridManager.Instance.WorldToGridCell(spawner.waypoints[i]);
                Vector2Int targetNode = GridManager.Instance.WorldToGridCell(spawner.waypoints[i+1]);
                
                FlowField ff = GetFlowField(targetNode, false);
                if (ff == null || ff.distanceGrid[startNode.x + 100, startNode.y + 100] == float.MaxValue)
                {
                    Debug.Log($"Maze Validation Failed for Spawner {spawner.gameObject.name}! Start Node {startNode} cannot reach Target Node {targetNode}. Target FlowField valid: {ff != null}");
                    return false;
                }
            }
        }
        return true;
    }

    public Vector2 GetFlowDirection(Vector3 currentPos, Vector3 targetPos)
    {
        Vector2Int currentCell = GridManager.Instance.WorldToGridCell(currentPos);
        Vector2Int targetCell = GridManager.Instance.WorldToGridCell(targetPos);

        // If in the exact destination cell, steer directly towards the transform vector
        // to prevent getting stuck oscillating over the center.
        if (currentCell == targetCell)
        {
            Vector3 dir = (targetPos - currentPos).normalized;
            return new Vector2(dir.x, dir.z);
        }

        FlowField ff = GetFlowField(targetCell, true);
        if (ff == null) return Vector2.zero;

        int cx = currentCell.x + 100;
        int cy = currentCell.y + 100;
        if (cx >= 0 && cx < 200 && cy >= 0 && cy < 200)
        {
            return ff.vectorGrid[cx, cy];
        }

        return Vector2.zero;
    }

    private FlowField GetFlowField(Vector2Int targetNode, bool generateVectors = true)
    {
        if (!flowFieldCache.TryGetValue(targetNode, out FlowField cached))
        {
            cached = new FlowField();
            cached.target = targetNode;
            flowFieldCache[targetNode] = cached;
            GenerateDistanceField(cached);
        }
        else if (cached.needsDistanceUpdate)
        {
            GenerateDistanceField(cached);
        }

        if (generateVectors && !cached.hasVectors)
        {
            GenerateVectorField(cached);
        }

        return cached;
    }

    private void GenerateDistanceField(FlowField ff)
    {
        int targetX = ff.target.x + 100;
        int targetY = ff.target.y + 100;

        if (targetX < 0 || targetX >= 200 || targetY < 0 || targetY >= 200) return;

        for (int x = 0; x < 200; x++)
        {
            for (int y = 0; y < 200; y++)
            {
                ff.distanceGrid[x, y] = float.MaxValue;
            }
        }

        openSet.Clear();
        openSet.Add(ff.target, 0f);
        ff.distanceGrid[targetX, targetY] = 0f;
        ff.needsDistanceUpdate = false;
        ff.hasVectors = false;

        // DIJKSTRA - Generate Distance Field
        while (openSet.Count > 0)
        {
            Vector2Int current = openSet.RemoveFirst();
            int cx = current.x + 100;
            int cy = current.y + 100;
            float currentDist = ff.distanceGrid[cx, cy];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;

                    Vector2Int neighbor = new Vector2Int(current.x + dx, current.y + dy);
                    if (!IsValidNeighbor(neighbor, current)) continue; // NOTE: reversed! Checking if neighbor can walk to current!
                    
                    // Prevent corner cutting
                    if (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1)
                    {
                        if (!IsValidNeighbor(new Vector2Int(neighbor.x, current.y), current) || 
                            !IsValidNeighbor(new Vector2Int(current.x, neighbor.y), current))
                        {
                            continue;
                        }
                    }

                    int nx = neighbor.x + 100;
                    int ny = neighbor.y + 100;

                    if (nx < 0 || nx >= 200 || ny < 0 || ny >= 200) continue;

                    float distToNeighbor = (dx == 0 || dy == 0) ? 10f : 14f;
                    float newDist = currentDist + distToNeighbor;

                    if (newDist < ff.distanceGrid[nx, ny])
                    {
                        ff.distanceGrid[nx, ny] = newDist;
                        openSet.Add(neighbor, newDist);
                    }
                }
            }
        }
    }

    private void GenerateVectorField(FlowField ff)
    {
        int targetX = ff.target.x + 100;
        int targetY = ff.target.y + 100;

        // GENERATE VECTOR FIELD
        for (int x = 0; x < 200; x++)
        {
            for (int y = 0; y < 200; y++)
            {
                if (ff.distanceGrid[x, y] == float.MaxValue)
                {
                    ff.vectorGrid[x, y] = Vector2.zero;
                    continue; // Unreachable
                }
                if (x == targetX && y == targetY)
                {
                    ff.vectorGrid[x, y] = Vector2.zero;
                    continue; // Target has no vector
                }

                Vector2Int current = new Vector2Int(x - 100, y - 100);
                float bestDist = ff.distanceGrid[x, y];
                Vector2 bestDir = Vector2.zero;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        
                        Vector2Int neighbor = new Vector2Int(current.x + dx, current.y + dy);
                        if (!IsValidNeighbor(current, neighbor)) continue;
                        
                        // Prevent corner cutting for vectors too
                        if (Mathf.Abs(dx) == 1 && Mathf.Abs(dy) == 1)
                        {
                            if (!IsValidNeighbor(current, new Vector2Int(neighbor.x, current.y)) || 
                                !IsValidNeighbor(current, new Vector2Int(current.x, neighbor.y)))
                            {
                                continue;
                            }
                        }

                        int nx = neighbor.x + 100;
                        int ny = neighbor.y + 100;

                        if (nx < 0 || nx >= 200 || ny < 0 || ny >= 200) continue;

                        if (ff.distanceGrid[nx, ny] < bestDist)
                        {
                            bestDist = ff.distanceGrid[nx, ny];
                            bestDir = new Vector2(dx, dy).normalized;
                        }
                    }
                }
                ff.vectorGrid[x, y] = bestDir;
            }
        }
        
        ff.hasVectors = true;
    }

    private bool IsValidNeighbor(Vector2Int from, Vector2Int to)
    {
        if (GridManager.Instance.IsCellOccupied(to)) return false;
        
        float heightFrom = GridManager.Instance.GetCellHeight(from);
        float heightTo = GridManager.Instance.GetCellHeight(to);
        
        if (Mathf.Abs(heightFrom - heightTo) > 1.2f) return false;
        
        return true;
    }

    private struct HeapNode
    {
        public Vector2Int pos;
        public float fScore;
        public HeapNode(Vector2Int pos, float fScore)
        {
            this.pos = pos;
            this.fScore = fScore;
        }
    }

    private class MinHeap
    {
        private HeapNode[] elements;
        private int count;

        public MinHeap(int maxElements)
        {
            elements = new HeapNode[maxElements];
            count = 0;
        }

        public int Count => count;

        public void Clear()
        {
            count = 0;
        }

        public void Add(Vector2Int item, float fScore)
        {
            if (count >= elements.Length) return;
            HeapNode node = new HeapNode(item, fScore);
            elements[count] = node;
            SortUp(count);
            count++;
        }

        public Vector2Int RemoveFirst()
        {
            HeapNode firstItem = elements[0];
            count--;
            elements[0] = elements[count];
            SortDown(0);
            return firstItem.pos;
        }

        private void SortDown(int index)
        {
            while (true)
            {
                int childIndexLeft = index * 2 + 1;
                int childIndexRight = index * 2 + 2;
                int swapIndex = 0;

                if (childIndexLeft < count)
                {
                    swapIndex = childIndexLeft;

                    if (childIndexRight < count)
                    {
                        if (elements[childIndexRight].fScore < elements[childIndexLeft].fScore)
                        {
                            swapIndex = childIndexRight;
                        }
                    }

                    if (elements[swapIndex].fScore < elements[index].fScore)
                    {
                        Swap(index, swapIndex);
                        index = swapIndex;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private void SortUp(int index)
        {
            int parentIndex = (index - 1) / 2;

            while (true)
            {
                if (elements[index].fScore < elements[parentIndex].fScore)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                    parentIndex = (index - 1) / 2;
                }
                else
                {
                    break;
                }
            }
        }

        private void Swap(int indexA, int indexB)
        {
            HeapNode temp = elements[indexA];
            elements[indexA] = elements[indexB];
            elements[indexB] = temp;
        }
    }
}
