using UnityEngine;
using System.Collections.Generic;

public class GridManager : Singleton<GridManager>
{
    // Cell coordinates are centered on the origin (may be negative); arrays are indexed 0..ArraySize-1.
    public const int ArraySize = 256;
    public const int IndexOffset = ArraySize / 2;
    private const float DefaultHeight = 0.5f;

    [SerializeField]
    private float cellSize = 1f;

    [SerializeField]
    private int gridWidth = 196;

    [SerializeField]
    private int gridHeight = 196;

    // Flat storage, index = (y + IndexOffset) * ArraySize + (x + IndexOffset). Exposed read-only-by-convention
    // (see OccupancyGrid / HeightGrid) so the pathfinder can scan them without a method call per cell.
    private bool[] occupiedCells;
    private bool[] unbuildableCells;
    private float[] cellHeights;

    /// <summary>Tower/cliff occupancy, indexed by <see cref="CellIndex"/>. Do not write; use OccupyCell/FreeCell.</summary>
    public bool[] OccupancyGrid => occupiedCells;

    /// <summary>Cell surface heights, indexed by <see cref="CellIndex"/>. Do not write; use SetCellHeight.</summary>
    public float[] HeightGrid => cellHeights;

    protected override void OnSingletonAwake()
    {
        InitializeGrid();
    }

    private void InitializeGrid()
    {
        int count = ArraySize * ArraySize;
        occupiedCells = new bool[count];
        unbuildableCells = new bool[count];
        cellHeights = new float[count];

        for (int i = 0; i < count; i++)
            cellHeights[i] = DefaultHeight;
    }

    /// <summary>Array index for a centered cell coordinate, or -1 if it lies outside the array.</summary>
    public static int CellIndex(Vector2Int cell)
    {
        int x = cell.x + IndexOffset;
        int y = cell.y + IndexOffset;
        if (x < 0 || x >= ArraySize || y < 0 || y >= ArraySize) return -1;
        return y * ArraySize + x;
    }

    public static Vector2Int IndexToCell(int index)
    {
        return new Vector2Int(index % ArraySize - IndexOffset, index / ArraySize - IndexOffset);
    }

    /// <summary>True if the cell is inside the playable area (the grid is centered on the origin).</summary>
    public bool IsPlayable(Vector2Int cell)
    {
        int halfWidth = gridWidth / 2;
        int halfHeight = gridHeight / 2;
        return cell.x >= -halfWidth && cell.x <= halfWidth && cell.y >= -halfHeight && cell.y <= halfHeight;
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
        int i = CellIndex(cell);
        if (i >= 0) cellHeights[i] = height;
    }

    public float GetCellHeight(Vector2Int cell)
    {
        int i = CellIndex(cell);
        return i >= 0 ? cellHeights[i] : DefaultHeight;
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
        if (!IsPlayable(cell)) return true;
        int i = CellIndex(cell);
        return i < 0 || occupiedCells[i];
    }

    /// <summary>
    /// Marks a cell as occupied (by a tower)
    /// </summary>
    public void OccupyCell(Vector2Int cell)
    {
        int i = CellIndex(cell);
        if (i >= 0) occupiedCells[i] = true;
    }

    /// <summary>
    /// Marks a cell as free
    /// </summary>
    public void FreeCell(Vector2Int cell)
    {
        int i = CellIndex(cell);
        if (i >= 0) occupiedCells[i] = false;
    }

    /// <summary>
    /// Checks if a world position can be built on (not occupied and valid)
    /// </summary>
    public bool CanBuildAt(Vector3 worldPos)
    {
        Vector2Int cell = WorldToGridCell(worldPos);
        int i = CellIndex(cell);
        if (i < 0) return false;

        return !IsCellOccupied(cell) && !unbuildableCells[i];
    }

    /// <summary>
    /// Marks a cell as unbuildable (e.g. for ramps)
    /// </summary>
    public void MarkUnbuildable(Vector2Int cell)
    {
        int i = CellIndex(cell);
        if (i >= 0) unbuildableCells[i] = true;
    }

    public float GetCellSize() => cellSize;
}
