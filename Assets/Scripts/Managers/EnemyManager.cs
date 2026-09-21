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
    // Spatial grid over the playable area (world is roughly -98..98 on x and z)
    private const float CellSize = 6f;
    private const float WorldHalfExtent = 104f;
    private static readonly int GridDim = Mathf.CeilToInt(WorldHalfExtent * 2f / CellSize);

    private readonly List<Enemy> enemies = new List<Enemy>(2048);
    private int[] cellHead;      // first creep index in each cell, -1 if empty
    private int[] nextInCell;    // next creep index in the same cell (linked list), parallel to `enemies`

    public int Count => enemies.Count;

    protected override void OnSingletonAwake()
    {
        cellHead = new int[GridDim * GridDim];
        nextInCell = new int[2048];
        ClearGrid();
    }

    public void Register(Enemy enemy)
    {
        enemy.ManagerIndex = enemies.Count;
        enemies.Add(enemy);
    }

    public void Unregister(Enemy enemy)
    {
        int i = enemy.ManagerIndex;
        if (i < 0 || i >= enemies.Count || enemies[i] != enemy) return;

        // Swap-remove keeps this O(1)
        int last = enemies.Count - 1;
        Enemy moved = enemies[last];
        enemies[i] = moved;
        moved.ManagerIndex = i;
        enemies.RemoveAt(last);
        enemy.ManagerIndex = -1;
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver()) return;

        float dt = Time.deltaTime;
        // Iterate backwards: creeps can remove themselves (reaching the end) while ticking.
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            if (i < enemies.Count) enemies[i].Tick(dt);
        }
        RebuildGrid();
    }

    // ------------------------------------------------------------------------------------------------ spatial queries

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
            Vector3 p = enemies[i].Position;
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
