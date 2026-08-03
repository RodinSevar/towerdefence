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
    }

    /// <summary>
    /// Snaps a world position to the nearest grid cell center
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        float x = Mathf.Round(worldPos.x / cellSize) * cellSize;
        float z = Mathf.Round(worldPos.z / cellSize) * cellSize;
        return new Vector3(x, worldPos.y, z);
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
        return !IsCellOccupied(cell);
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
