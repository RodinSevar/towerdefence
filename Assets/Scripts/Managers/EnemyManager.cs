using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns every live creep. Two jobs:
///  1. A single update loop ticks all creeps (one Unity callback instead of one per creep, which is a large saving with
///     ~1800 creeps on screen).
///  2. A uniform spatial grid, rebuilt after each tick, answers "nearest creep within range" for towers and ray picking
///     for selection, so creeps need neither physics colliders nor per-tower physics queries.
/// </summary>
public class EnemyManager : Singleton<EnemyManager>
{
    // Spatial grid over the playable area (the map is -96..96 on x and z)
    private const float CellSize = 6f;
    private const float WorldHalfExtent = 104f;
    private static readonly int GridDim = Mathf.CeilToInt(WorldHalfExtent * 2f / CellSize);

    // Slots are stable for the whole frame: a creep that dies leaves a null slot (skipped by every query) and the list is
    // compacted once at the end of Update, just before the grid is rebuilt. Reshuffling on removal instead would leave the
    // grid's indices pointing at the wrong creep for the rest of the frame.
    private readonly List<Enemy> enemies = new List<Enemy>(2048);

    // ---- path requests ----
    // In the original game every move order needs a path from the pathfinder, and requests are served from a queue at a
    // limited rate: a big group ordered to move at once starts in batches, and pauses again at each new order. Creeps
    // that need a path ask here and stand still until it is granted.
    [Tooltip("Path requests the pathfinder serves per second (0 = unlimited). A level of ~280 creeps then takes about " +
             "280 / rate seconds to get moving. Calibrated to the original's \"couple of seconds\" until the engine's real " +
             "scheduling is known.")]
    public float pathRequestsPerSecond = 150f;

    private readonly Queue<Enemy> pathQueue = new Queue<Enemy>();
    private float pathBudget;

    /// <summary>Creeps currently waiting for a path (for diagnostics).</summary>
    public int PathQueueLength => pathQueue.Count;
    private int liveCount;
    private bool hasHoles;
    private int[] cellHead;      // first creep index in each cell, -1 if empty
    private int[] nextInCell;    // next creep index in the same cell (linked list), parallel to `enemies`

    /// <summary>Number of live creeps.</summary>
    public int Count => liveCount;

    protected override void OnSingletonAwake()
    {
        cellHead = new int[GridDim * GridDim];
        nextInCell = new int[2048];
        ClearGrid();
    }

    /// <summary>A creep received a new order and needs a path before it can move.</summary>
    public void RequestPath(Enemy enemy)
    {
        if (pathRequestsPerSecond <= 0f) enemy.GrantPath();
        else pathQueue.Enqueue(enemy);
    }

    private void ServePathRequests(float dt)
    {
        if (pathQueue.Count == 0)
        {
            pathBudget = 0f;
            return;
        }

        // The budget accrues with time, so the rate is per game second regardless of frame rate.
        pathBudget += pathRequestsPerSecond * dt;
        while (pathBudget >= 1f && pathQueue.Count > 0)
        {
            Enemy e = pathQueue.Dequeue();
            if (e == null || !e.IsAlive) continue; // died while waiting: costs nothing
            e.GrantPath();
            pathBudget -= 1f;
        }
    }

    public void Register(Enemy enemy)
    {
        enemy.ManagerIndex = enemies.Count;
        enemies.Add(enemy);
        liveCount++;
    }

    public void Unregister(Enemy enemy)
    {
        int i = enemy.ManagerIndex;
        if (i < 0 || i >= enemies.Count || enemies[i] != enemy) return;

        enemies[i] = null; // tombstone; compacted at the end of Update
        enemy.ManagerIndex = -1;
        liveCount--;
        hasHoles = true;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        float dt = Time.deltaTime;
        ServePathRequests(dt);
        // Creeps can die while ticking (reaching the end); their slots just become null. Creeps spawned meanwhile are
        // appended and start ticking next frame.
        int count = enemies.Count;
        for (int i = 0; i < count; i++)
        {
            Enemy e = enemies[i];
            if (e != null) e.Tick(dt);
        }

        Compact();
        RebuildGrid();
    }

    // ------------------------------------------------------------------------------------------------ spatial queries

    private void Compact()
    {
        if (!hasHoles) return;
        int write = 0;
        for (int read = 0; read < enemies.Count; read++)
        {
            Enemy e = enemies[read];
            if (e == null) continue;
            enemies[write] = e;
            e.ManagerIndex = write;
            write++;
        }
        enemies.RemoveRange(write, enemies.Count - write);
        hasHoles = false;
    }

    private void ClearGrid()
    {
        for (int i = 0; i < cellHead.Length; i++) cellHead[i] = -1;
    }

    private int CellCoord(float world)
    {
        int c = Mathf.FloorToInt((world + WorldHalfExtent) / CellSize);
        return c < 0 ? 0 : (c >= GridDim ? GridDim - 1 : c);
    }

    private void RebuildGrid()
    {
        ClearGrid();
        if (nextInCell.Length < enemies.Count) nextInCell = new int[Mathf.NextPowerOfTwo(enemies.Count)];

        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null) { nextInCell[i] = -1; continue; }
            Vector3 p = e.Position;
            int cell = CellCoord(p.z) * GridDim + CellCoord(p.x);
            nextInCell[i] = cellHead[cell];
            cellHead[cell] = i;
        }
    }

    /// <summary>Nearest creep within <paramref name="range"/> of <paramref name="position"/>, or null.</summary>
    public Enemy FindNearest(Vector3 position, float range)
    {
        int x0 = CellCoord(position.x - range), x1 = CellCoord(position.x + range);
        int z0 = CellCoord(position.z - range), z1 = CellCoord(position.z + range);

        float bestSqr = range * range;
        Enemy best = null;
        for (int cz = z0; cz <= z1; cz++)
        {
            for (int cx = x0; cx <= x1; cx++)
            {
                for (int i = cellHead[cz * GridDim + cx]; i >= 0; i = nextInCell[i])
                {
                    Enemy e = enemies[i];
                    if (e == null) continue; // died earlier this frame
                    float sqr = (e.Position - position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = e;
                    }
                }
            }
        }
        return best;
    }

    /// <summary>
    /// First creep along the ray whose aim point is within <paramref name="radius"/> of it. Used for mouse selection
    /// (creeps have no colliders). Returns null if none; <paramref name="distance"/> is the distance along the ray.
    /// </summary>
    public Enemy PickAlongRay(Ray ray, float radius, out float distance)
    {
        Enemy best = null;
        distance = float.MaxValue;
        float radiusSqr = radius * radius;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            Vector3 toEnemy = enemies[i].AimPoint - ray.origin;
            float along = Vector3.Dot(toEnemy, ray.direction);
            if (along < 0f || along >= distance) continue;
            if (toEnemy.sqrMagnitude - along * along <= radiusSqr)
            {
                best = enemies[i];
                distance = along;
            }
        }
        return best;
    }
}
