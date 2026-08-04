using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private int gridWidth = 196;

    [SerializeField]
    private int gridHeight = 196;

    private HashSet<Vector2Int> occupiedCells;
    private HashSet<Vector2Int> unbuildableCells;
    private Dictionary<Vector2Int, float> cellHeights;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        occupiedCells = new HashSet<Vector2Int>();
        unbuildableCells = new HashSet<Vector2Int>();
        cellHeights = new Dictionary<Vector2Int, float>();
    }

    /// <summary>
    /// Snaps a world position to the nearest grid cell center
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        float x = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float z = Mathf.Round(worldPos.z / cellSize) * cellSize;
        Vector2Int cell = WorldToGridCell(new Vector3(x, 0, z));
        float y = GetCellHeight(cell);
        return new Vector3(x, y, z);
    }

    public void SetCellHeight(Vector2Int cell, float height)
    {
        cellHeights[cell] = height;
    }

    public float GetCellHeight(Vector2Int cell)
    {
        if (cellHeights != null && cellHeights.TryGetValue(cell, out float height))
            return height;
        return 0.5f; // Default ground height if none set
    }

    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize, GetCellHeight(cell), cell.y * cellSize);
    }

    /// <summary>
    /// Converts world position to grid cell coordinates
    /// </summary>
    public Vector2Int WorldToGridCell(Vector3 worldPos)
    {
        // Get grid center (should be at origin)
        Vector3 localPos = worldPos;
        
        int gridX = Mathf.RoundToInt(localPos.x / cellSize);
        int gridZ = Mathf.RoundToInt(localPos.z / cellSize);
        
        return new Vector2Int(gridX, gridZ);
    }

    /// <summary>
    /// Checks if a grid cell is occupied or out of bounds
    /// </summary>
    public bool IsCellOccupied(Vector2Int cell)
    {
        if (!IsValidCell(cell)) return true;
        return occupiedCells.Contains(cell);
    }

    /// <summary>
    /// Marks a cell as occupied (by a tower) - marks center cell and adjacent cells
    /// </summary>
    public void OccupyCell(Vector2Int cell)
    {
        occupiedCells.Add(cell);
    }

    /// <summary>
    /// Marks a cell as free
    /// </summary>
    public void FreeCell(Vector2Int cell)
    {
        occupiedCells.Remove(cell);
    }

    /// <summary>
    /// Checks if a world position can be built on (not occupied and valid)
    /// </summary>
    public bool CanBuildAt(Vector3 worldPos)
    {
        Vector2Int cell = WorldToGridCell(worldPos);
        return !IsCellOccupied(cell) && !unbuildableCells.Contains(cell);
    }

    /// <summary>
    /// Marks a cell as unbuildable (e.g. for ramps)
    /// </summary>
    public void MarkUnbuildable(Vector2Int cell)
    {
        unbuildableCells.Add(cell);
    }

    /// <summary>
    /// Checks if a grid cell is within bounds
    /// </summary>
    private bool IsValidCell(Vector2Int cell)
    {
        int halfWidth = gridWidth / 2;
        int halfHeight = gridHeight / 2;
        return cell.x >= -halfWidth && cell.x <= halfWidth && cell.y >= -halfHeight && cell.y <= halfHeight;
    }

    public float GetCellSize() => cellSize;
}
