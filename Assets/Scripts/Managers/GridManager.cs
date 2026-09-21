using UnityEngine;
using System.Collections.Generic;

public class GridManager : Singleton<GridManager>
{
    // Cell coordinates are centered on the origin (may be negative); arrays are indexed 0..ArraySize-1.
    public const int ArraySize = 256;
    private const int IndexOffset = ArraySize / 2;
    private const float DefaultHeight = 0.5f;

    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private int gridWidth = 196;

    [SerializeField]
    private int gridHeight = 196;

    private bool[,] occupiedCellsArray;
    private bool[,] unbuildableCellsArray;
    private float[,] cellHeightsArray;

    protected override void OnSingletonAwake()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        occupiedCellsArray = new bool[ArraySize, ArraySize];
        unbuildableCellsArray = new bool[ArraySize, ArraySize];
        cellHeightsArray = new float[ArraySize, ArraySize];

        for (int x = 0; x < ArraySize; x++)
            for (int y = 0; y < ArraySize; y++)
                cellHeightsArray[x, y] = DefaultHeight;
    }

    /// <summary>
    /// Converts a centered cell coordinate to array indices. False if outside the array.
    /// </summary>
    private static bool TryGetIndex(Vector2Int cell, out int x, out int y)
    {
        x = cell.x + IndexOffset;
        y = cell.y + IndexOffset;
        return x >= 0 && x < ArraySize && y >= 0 && y < ArraySize;
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
        if (TryGetIndex(cell, out int x, out int y))
            cellHeightsArray[x, y] = height;
    }

    public float GetCellHeight(Vector2Int cell)
    {
        return TryGetIndex(cell, out int x, out int y) ? cellHeightsArray[x, y] : DefaultHeight;
    }

    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize, GetCellHeight(cell), cell.y * cellSize);
    }

    /// <summary>
    /// Converts world position to grid cell coordinates (grid is centered on the origin)
    /// </summary>
    public Vector2Int WorldToGridCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / cellSize),
            Mathf.RoundToInt(worldPos.z / cellSize));
    }

    /// <summary>
    /// Checks if a grid cell is occupied or out of bounds
    /// </summary>
    public bool IsCellOccupied(Vector2Int cell)
    {
        if (!IsValidCell(cell)) return true;
        return !TryGetIndex(cell, out int x, out int y) || occupiedCellsArray[x, y];
    }

    /// <summary>
    /// Marks a cell as occupied (by a tower)
    /// </summary>
    public void OccupyCell(Vector2Int cell)
    {
        if (TryGetIndex(cell, out int x, out int y))
            occupiedCellsArray[x, y] = true;
    }

    /// <summary>
    /// Marks a cell as free
    /// </summary>
    public void FreeCell(Vector2Int cell)
    {
        if (TryGetIndex(cell, out int x, out int y))
            occupiedCellsArray[x, y] = false;
    }

    /// <summary>
    /// Checks if a world position can be built on (not occupied and valid)
    /// </summary>
    public bool CanBuildAt(Vector3 worldPos)
    {
        Vector2Int cell = WorldToGridCell(worldPos);
        if (!TryGetIndex(cell, out int x, out int y)) return false;

        return !IsCellOccupied(cell) && !unbuildableCellsArray[x, y];
    }

    /// <summary>
    /// Marks a cell as unbuildable (e.g. for ramps)
    /// </summary>
    public void MarkUnbuildable(Vector2Int cell)
    {
        if (TryGetIndex(cell, out int x, out int y))
            unbuildableCellsArray[x, y] = true;
    }

    /// <summary>
    /// Checks if a grid cell is within the playable bounds
    /// </summary>
    private bool IsValidCell(Vector2Int cell)
    {
        int halfWidth = gridWidth / 2;
        int halfHeight = gridHeight / 2;
        return cell.x >= -halfWidth && cell.x <= halfWidth && cell.y >= -halfHeight && cell.y <= halfHeight;
    }

    public float GetCellSize() => cellSize;
}
