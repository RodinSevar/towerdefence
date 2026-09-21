using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pathing for the creeps: maze validation ("does every spawner still reach every waypoint?") and flow fields
/// (one distance field per waypoint target, queried per creep).
///
/// Everything runs on flat arrays over the grid. A single rule, <see cref="CanStep"/>, defines which moves exist, and both
/// validation and the distance fields use it, so what validation accepts is exactly what creeps can walk.
///
/// The public API (ValidateFullMaze, GetFlowDirection, NotifyMazeChanged, ClearCache, OnMazeChanged) is deliberately
/// small so the algorithm behind it can be replaced.
/// </summary>
public class PathManager : Singleton<PathManager>
{
    public System.Action OnMazeChanged;

    /// <summary>Flow fields recomputed per simulation tick after the maze changed (a count, not a time budget, so every machine refreshes the same fields on the same tick).</summary>
    [SerializeField] private int fieldsPerTick = 1;

    private const int Size = GridManager.ArraySize;
    private const int SizeShift = 9; // log2(Size)
    private const int CellCount = Size * Size;
    private const float MaxStepHeight = 1.2f;
    private const int InfDistance = int.MaxValue;
    private const int OrthCost = 10, DiagCost = 14;

    // The 8 neighbours in the same order the original code scanned them (dx -1..1, then dy -1..1).
    private static readonly int[] Dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
    private static readonly int[] Dy = { -1, 0, 1, -1, 1, -1, 0, 1 };
    private static readonly int[] Offset = new int[8];   // index delta of each direction
    private static readonly int[] CornerA = new int[8];  // for diagonals: index delta of the two orthogonal corner cells
    private static readonly int[] CornerB = new int[8];  // (0 for orthogonal moves)
    private static readonly int[] StepCost = new int[8];

    static PathManager()
    {
        for (int d = 0; d < 8; d++)
        {
            Offset[d] = Dy[d] * Size + Dx[d];
            bool diagonal = Dx[d] != 0 && Dy[d] != 0;
            CornerA[d] = diagonal ? Dx[d] : 0;
            CornerB[d] = diagonal ? Dy[d] * Size : 0;
            StepCost[d] = diagonal ? DiagCost : OrthCost;
        }
    }

    // Static terrain data, built once from the grid heights.
    private ushort[] stepMask;   // bit d set: stepping from this cell in direction d is allowed by the terrain alone
    private bool built;

    private class Field
    {
        public int target;
        public int[] dist = new int[CellCount];
        public bool computed;
        public bool dirty = true;
    }

    private readonly Dictionary<int, Field> fields = new Dictionary<int, Field>();
    private readonly List<Field> dirtyQueue = new List<Field>();
    private readonly List<int>[] buckets = CreateBuckets();

    // Search scratch (reused, no per-call allocation)
    private int[] visitStamp = new int[CellCount];
    private int stamp;
    private readonly IntHeap heap = new IntHeap(CellCount);
    private readonly HashSet<long> checkedSegments = new HashSet<long>();

    private static List<int>[] CreateBuckets()
    {
        var b = new List<int>[DiagCost + 1];
        for (int i = 0; i < b.Length; i++) b[i] = new List<int>(1024);
        return b;
    }

    // ------------------------------------------------------------------------------------------------ public API

    /// <summary>Marks every flow field stale. They are recomputed over the next frames (see Update).</summary>
    public void ClearCache()
    {
        foreach (var f in fields.Values) MarkDirty(f);
    }

    /// <summary>Call after towers are placed or removed.</summary>
    public void NotifyMazeChanged()
    {
        ClearCache();
        OnMazeChanged?.Invoke();
    }

    /// <summary>
    /// True if, for every spawner, each waypoint can reach the next one with the current tower layout.
    /// Independent of the flow-field cache, so it is always exact (used by placement preview and placement).
    /// </summary>
    public bool ValidateFullMaze()
    {
        if (WaveManager.Instance == null) return true;
        EnsureBuilt();

        var grid = GridManager.Instance;
        checkedSegments.Clear();
        foreach (var spawner in WaveManager.Instance.activeSpawners)
        {
            var wp = spawner.waypoints;
            if (wp == null || wp.Length < 2) continue;
            for (int i = 0; i < wp.Length - 1; i++)
            {
                int start = GridManager.CellIndex(grid.WorldToGridCell(wp[i]));
                int target = GridManager.CellIndex(grid.WorldToGridCell(wp[i + 1]));
                if (start < 0 || target < 0) return false;
                if (!checkedSegments.Add(((long)start << 32) | (uint)target)) continue; // already verified

                if (!CanReach(start, target)) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Direction a creep at <paramref name="currentPos"/> should move to reach <paramref name="targetPos"/>
    /// (unit vector in x/z, or zero if there is no route).
    /// </summary>
    public Vector2 GetFlowDirection(Vector3 currentPos, Vector3 targetPos)
    {
        var grid = GridManager.Instance;
        Vector2Int currentCell = grid.WorldToGridCell(currentPos);
        Vector2Int targetCell = grid.WorldToGridCell(targetPos);

        // In the exact destination cell, steer directly at the target position to avoid oscillating over the center.
        if (currentCell == targetCell)
        {
            Vector3 dir = (targetPos - currentPos).normalized;
            return new Vector2(dir.x, dir.z);
        }

        int cell = GridManager.CellIndex(currentCell);
        int target = GridManager.CellIndex(targetCell);
        if (cell < 0 || target < 0) return Vector2.zero;

        EnsureBuilt();
        Field field = GetField(target);
        return BestDirection(field.dist, cell);
    }

    /// <summary>
    /// Recomputes every stale flow field now. Normally fields are refreshed a little per frame; call this from tools
    /// and tests that need the result immediately.
    /// </summary>
    public void FlushDirtyFields()
    {
        EnsureBuilt();
        while (dirtyQueue.Count > 0) RecomputeNext();
    }

    // ------------------------------------------------------------------------------------------------ per-frame refresh

    /// <summary>One simulation tick (called by <see cref="Simulation"/>): refreshes a few stale fields.</summary>
    public void SimTick()
    {
        // Until a field is refreshed, creeps keep using its previous distances, so a placement never causes a single long frame.
        for (int i = 0; i < fieldsPerTick && dirtyQueue.Count > 0; i++) RecomputeNext();
    }

    private void MarkDirty(Field f)
    {
        if (f.dirty) return;
        f.dirty = true;
        dirtyQueue.Add(f);
    }

    private void RecomputeNext()
    {
        Field f = dirtyQueue[0];
        dirtyQueue.RemoveAt(0);
        ComputeDistances(f);
    }

    private Field GetField(int target)
    {
        if (!fields.TryGetValue(target, out Field f))
        {
            f = new Field { target = target };
            fields[target] = f;
            ComputeDistances(f); // first request for this target: compute now, creeps need a field to follow
        }
        return f;
    }

    // ------------------------------------------------------------------------------------------------ terrain and moves

    private void EnsureBuilt()
    {
        if (built) return;
        var grid = GridManager.Instance;
        float[] heights = grid.HeightGrid;
        stepMask = new ushort[CellCount];

        int half = 0;
        // Only cells inside the playable area take part; find the extent from the grid itself.
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var cell = new Vector2Int(x - GridManager.IndexOffset, y - GridManager.IndexOffset);
                if (!grid.IsPlayable(cell)) continue;
                int c = y * Size + x;
                ushort mask = 0;
                for (int d = 0; d < 8; d++)
                {
                    var n = new Vector2Int(cell.x + Dx[d], cell.y + Dy[d]);
                    if (!grid.IsPlayable(n)) continue;
                    int ni = c + Offset[d];
                    if (Mathf.Abs(heights[c] - heights[ni]) > MaxStepHeight) continue;

                    if (Dx[d] != 0 && Dy[d] != 0)
                    {
                        // Diagonal: both orthogonal corner cells must also be reachable on the flat (no corner cutting)
                        var a = new Vector2Int(cell.x + Dx[d], cell.y);
                        var b = new Vector2Int(cell.x, cell.y + Dy[d]);
                        if (!grid.IsPlayable(a) || !grid.IsPlayable(b)) continue;
                        if (Mathf.Abs(heights[c] - heights[c + CornerA[d]]) > MaxStepHeight) continue;
                        if (Mathf.Abs(heights[c] - heights[c + CornerB[d]]) > MaxStepHeight) continue;
                    }
                    mask |= (ushort)(1 << d);
                }
                stepMask[c] = mask;
                half++;
            }
        }
        built = true;
    }

    /// <summary>
    /// The one movement rule. A creep in cell <paramref name="c"/> may step in direction <paramref name="d"/> if the terrain
    /// allows it (heights, playable area), the destination is free, and for diagonal steps both corner cells are free.
    /// </summary>
    private bool CanStep(bool[] occupied, int c, int d)
    {
        if ((stepMask[c] & (1 << d)) == 0) return false;
        if (occupied[c + Offset[d]]) return false;
        if (CornerA[d] != 0 && (occupied[c + CornerA[d]] || occupied[c + CornerB[d]])) return false;
        return true;
    }

    // ------------------------------------------------------------------------------------------------ reachability

    /// <summary>Greedy best-first search: is there any walkable route from start to target? Fast in open ground.</summary>
    private bool CanReach(int start, int target)
    {
        if (start == target) return true;
        bool[] occupied = GridManager.Instance.OccupancyGrid;

        stamp++;
        heap.Clear();
        visitStamp[start] = stamp;
        heap.Push(Heuristic(start, target), start);

        while (heap.Count > 0)
        {
            int u = heap.PopMin();
            for (int d = 0; d < 8; d++)
            {
                if (!CanStep(occupied, u, d)) continue;
                int v = u + Offset[d];
                if (visitStamp[v] == stamp) continue;
                if (v == target) return true;
                visitStamp[v] = stamp;
                heap.Push(Heuristic(v, target), v);
            }
        }
        return false;
    }

    private static int Heuristic(int a, int b)
    {
        int dx = (a & (Size - 1)) - (b & (Size - 1));
        int dy = (a >> SizeShift) - (b >> SizeShift);
        if (dx < 0) dx = -dx;
        if (dy < 0) dy = -dy;
        return dx > dy ? OrthCost * dx + (DiagCost - OrthCost) * dy : OrthCost * dy + (DiagCost - OrthCost) * dx;
    }

    // ------------------------------------------------------------------------------------------------ flow fields

    /// <summary>
    /// Distance to the field's target for every cell, by a bucket queue (costs are only 10 and 14). Distances are
    /// computed backwards from the target: a cell is settled from a neighbour it can step to.
    /// </summary>
    private void ComputeDistances(Field f)
    {
        f.dirty = false;
        f.computed = true;
        if (dirtyQueue.Count > 0) dirtyQueue.Remove(f); // in case it was requested while queued

        int[] dist = f.dist;
        for (int i = 0; i < dist.Length; i++) dist[i] = InfDistance;

        int target = f.target;
        bool[] occupied = GridManager.Instance.OccupancyGrid;
        dist[target] = 0;

        foreach (var b in buckets) b.Clear();
        buckets[0].Add(target);
        int pending = 1;

        for (int d0 = 0; pending > 0; d0++)
        {
            List<int> bucket = buckets[d0 % buckets.Length];
            for (int k = 0; k < bucket.Count; k++)
            {
                int u = bucket[k];
                pending--;
                if (dist[u] != d0) continue; // superseded by a shorter route

                for (int d = 0; d < 8; d++)
                {
                    int n = u - Offset[d]; // cell that steps to u in direction d
                    if ((uint)n >= CellCount) continue;
                    if (!CanStep(occupied, n, d)) continue;

                    int nd = d0 + StepCost[d];
                    if (nd < dist[n])
                    {
                        dist[n] = nd;
                        buckets[nd % buckets.Length].Add(n);
                        pending++;
                    }
                }
            }
            bucket.Clear();
        }
    }

    /// <summary>The step from <paramref name="c"/> toward the lowest-distance reachable neighbour.</summary>
    private Vector2 BestDirection(int[] dist, int c)
    {
        int best = dist[c];
        if (best == InfDistance || best == 0) return Vector2.zero; // unreachable, or at the target
        bool[] occupied = GridManager.Instance.OccupancyGrid;

        int bestDir = -1;
        for (int d = 0; d < 8; d++)
        {
            if (!CanStep(occupied, c, d)) continue;
            int nd = dist[c + Offset[d]];
            if (nd < best)
            {
                best = nd;
                bestDir = d;
            }
        }
        return bestDir < 0 ? Vector2.zero : new Vector2(Dx[bestDir], Dy[bestDir]).normalized;
    }

    // ------------------------------------------------------------------------------------------------ int min-heap

    /// <summary>Minimal binary min-heap of (key, value) int pairs, allocation-free after construction.</summary>
    private class IntHeap
    {
        private readonly int[] keys;
        private readonly int[] values;
        private int count;

        public IntHeap(int capacity)
        {
            keys = new int[capacity];
            values = new int[capacity];
        }

        public int Count => count;
        public void Clear() { count = 0; }

        public void Push(int key, int value)
        {
            if (count >= keys.Length) return;
            int i = count++;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (keys[parent] <= key) break;
                keys[i] = keys[parent];
                values[i] = values[parent];
                i = parent;
            }
            keys[i] = key;
            values[i] = value;
        }

        public int PopMin()
        {
            int result = values[0];
            count--;
            if (count > 0)
            {
                int key = keys[count], value = values[count];
                int i = 0;
                while (true)
                {
                    int child = 2 * i + 1;
                    if (child >= count) break;
                    if (child + 1 < count && keys[child + 1] < keys[child]) child++;
                    if (keys[child] >= key) break;
                    keys[i] = keys[child];
                    values[i] = values[child];
                    i = child;
                }
                keys[i] = key;
                values[i] = value;
            }
            return result;
        }
    }
}
