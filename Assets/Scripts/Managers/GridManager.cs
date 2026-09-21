using UnityEngine;

/// <summary>
/// The logic grid: 0.5-world-unit cells (32 Warcraft III units, the map's native pathing resolution; creeps move on this grid, towers cover 4x4 of these cells) with occupancy, buildability and
/// ground height. Cell (x, z) covers world x in [x * cellSize, (x + 1) * cellSize), and the grid
/// lines up exactly with the terrain mesh and the map's pathing cells. Filled from the imported terrain by TerrainBuilder.
/// </summary>
public class GridManager : Singleton<GridManager>
{
    // Cell coordinates are centred on the origin (may be negative); arrays are indexed 0..ArraySize-1.
    public const int ArraySize = 512;
    public const int IndexOffset = ArraySize / 2;
    private const float DefaultHeight = 0.5f;

    [SerializeField]
    private float cellSize = 0.5f;

    [Tooltip("Playable map size in cells (the imported map is 384 x 384)")]
    [SerializeField]
    private int gridWidth = 384;

    [SerializeField]
    private int gridHeight = 384;

    // Flat storage, index = (z + IndexOffset) * ArraySize + (x + IndexOffset). Exposed read-only-by-convention
    // (see OccupancyGrid / HeightGrid) so the pathfinder can scan them without a method call per cell.
    private bool[] occupiedCells;
    private bool[] unbuildableCells;
    private float[] cellHeights;

    /// <summary>Tower/cliff occupancy, indexed by <see cref="CellIndex"/>. Do not write; use OccupyCell/FreeCell.</summary>
    public bool[] OccupancyGrid => occupiedCells;

    /// <summary>Cell ground heights (at the cell centre), indexed by <see cref="CellIndex"/>. Do not write; use SetCellHeight.</summary>
    public float[] HeightGrid => cellHeights;

    /// <summary>Lowest and highest playable cell coordinates.</summary>
    public Vector2Int MinCell => new Vector2Int(-gridWidth / 2, -gridHeight / 2);
    public Vector2Int MaxCell => new Vector2Int(gridWidth / 2 - 1, gridHeight / 2 - 1);

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

    /// <summary>
    /// Loads the imported terrain: blocked cells become occupied, walkable-but-unbuildable cells become unbuildable, and
    /// <paramref name="centerHeights"/> (one per data cell) sets each cell's ground height.
    /// </summary>
    public void ApplyTerrain(TerrainMapData data, float[] centerHeights)
    {
        if (data.pathingFine == null || data.pathingFine.Length != data.fineX * data.fineZ)
        {
            Debug.LogError("Terrain data has no full-resolution pathing. Run Tools > Build Terrain (or Tools > Import WC3 Terrain) to re-import it.");
            return;
        }
        int fx = data.fineX, fz = data.fineZ;
        for (int z = 0; z < fz; z++)
        {
            for (int x = 0; x < fx; x++)
            {
                var cell = new Vector2Int(x - fx / 2, z - fz / 2);
                int i = CellIndex(cell);
                if (i < 0) continue;

                byte flags = data.pathingFine[z * fx + x];
                occupiedCells[i] = (flags & TerrainMapData.PathBlocked) != 0;
                unbuildableCells[i] = (flags & TerrainMapData.PathNoBuild) != 0;

                // Height: bilinear between the coarse (1-unit) cell centres, evaluated at this cell's centre
                float wx = (x + 0.5f) * cellSize - fx * cellSize / 2f, wz = (z + 0.5f) * cellSize - fz * cellSize / 2f;
                cellHeights[i] = CoarseHeight(data, centerHeights, wx, wz);
            }
        }
    }

    private static float CoarseHeight(TerrainMapData data, float[] heights, float wx, float wz)
    {
        float u = wx + data.cellsX / 2f - 0.5f, v = wz + data.cellsZ / 2f - 0.5f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, data.cellsX - 2), z0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, data.cellsZ - 2);
        float tx = Mathf.Clamp01(u - x0), tz = Mathf.Clamp01(v - z0);
        float h00 = heights[data.CellIndex(x0, z0)], h10 = heights[data.CellIndex(x0 + 1, z0)];
        float h01 = heights[data.CellIndex(x0, z0 + 1)], h11 = heights[data.CellIndex(x0 + 1, z0 + 1)];
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    /// <summary>Array index for a centred cell coordinate, or -1 if it lies outside the array.</summary>
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

    /// <summary>True if the cell is inside the playable map.</summary>
    public bool IsPlayable(Vector2Int cell)
    {
        Vector2Int min = MinCell, max = MaxCell;
        return cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y;
    }

    /// <summary>
    /// Snaps a world position to the centre of its cell, at ground height.
    /// </summary>
    public Vector3 SnapToGrid(Vector3 worldPos)
    {
        Vector2Int cell = WorldToGridCell(worldPos);
        var center = new Vector3((cell.x + 0.5f) * cellSize, 0f, (cell.y + 0.5f) * cellSize);
        center.y = GetCellHeight(cell);
        return center;
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

    /// <summary>Ground height at any world position: bilinear between the four nearest cell centres.</summary>
    public float SampleHeight(Vector3 worldPos)
    {
        float fx = worldPos.x / cellSize - 0.5f;
        float fz = worldPos.z / cellSize - 0.5f;
        int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
        float tx = fx - x0, tz = fz - z0;

        float h00 = GetCellHeight(new Vector2Int(x0, z0));
        float h10 = GetCellHeight(new Vector2Int(x0 + 1, z0));
        float h01 = GetCellHeight(new Vector2Int(x0, z0 + 1));
        float h11 = GetCellHeight(new Vector2Int(x0 + 1, z0 + 1));
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
    }

    /// <summary>World position of a cell's centre, at ground height.</summary>
    public Vector3 GetWorldPosition(Vector2Int cell)
    {
        return new Vector3((cell.x + 0.5f) * cellSize, GetCellHeight(cell), (cell.y + 0.5f) * cellSize);
    }

    /// <summary>
    /// Converts world position to the cell containing it
    /// </summary>
    public Vector2Int WorldToGridCell(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / cellSize),
            Mathf.FloorToInt(worldPos.z / cellSize));
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

    // ---- Tower footprint: towers cover 4x4 cells (128 Warcraft III units) and centre on a 1-world-unit lattice ----

    public const int Footprint = 4;
    private const int SnapCells = 2; // tower centres snap to whole world units

    /// <summary>Width of a tower footprint in world units.</summary>
    public float FootprintWorldSize => Footprint * cellSize;

    /// <summary>Bottom-left cell of the footprint whose centre (a cell corner) is nearest to <paramref name="worldPos"/>.</summary>
    public Vector2Int FootprintOrigin(Vector3 worldPos)
    {
        float snap = SnapCells * cellSize;
        return new Vector2Int(
            Mathf.RoundToInt(worldPos.x / snap) * SnapCells - Footprint / 2,
            Mathf.RoundToInt(worldPos.z / snap) * SnapCells - Footprint / 2);
    }

    /// <summary>Centre of the footprint at <paramref name="origin"/>, at the highest ground under it.</summary>
    public Vector3 FootprintCenter(Vector2Int origin)
    {
        float h = float.MinValue;
        for (int dz = 0; dz < Footprint; dz++)
            for (int dx = 0; dx < Footprint; dx++)
                h = Mathf.Max(h, GetCellHeight(new Vector2Int(origin.x + dx, origin.y + dz)));
        return new Vector3((origin.x + Footprint / 2f) * cellSize, h, (origin.y + Footprint / 2f) * cellSize);
    }

    public Vector3 SnapToFootprint(Vector3 worldPos) => FootprintCenter(FootprintOrigin(worldPos));

    public bool CanBuildFootprint(Vector2Int origin)
    {
        for (int dz = 0; dz < Footprint; dz++)
            for (int dx = 0; dx < Footprint; dx++)
            {
                var c = new Vector2Int(origin.x + dx, origin.y + dz);
                int i = CellIndex(c);
                if (i < 0 || IsCellOccupied(c) || unbuildableCells[i]) return false;
            }
        return true;
    }

    public void OccupyFootprint(Vector2Int origin) => SetFootprint(origin, true);
    public void FreeFootprint(Vector2Int origin) => SetFootprint(origin, false);

    private void SetFootprint(Vector2Int origin, bool occupied)
    {
        for (int dz = 0; dz < Footprint; dz++)
            for (int dx = 0; dx < Footprint; dx++)
            {
                int i = CellIndex(new Vector2Int(origin.x + dx, origin.y + dz));
                if (i >= 0) occupiedCells[i] = occupied;
            }
    }

    public float GetCellSize() => cellSize;
}
