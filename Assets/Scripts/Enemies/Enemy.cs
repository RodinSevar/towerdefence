using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    public enum EnemyType { Basic, Strong, Fast }

    [System.Serializable]
    public class EnemyStats
    {
        public float health = 20f;
        public float speed = 5f;
        public int goldReward = 10;
    }

    [SerializeField]
    private EnemyStats basicStats = new EnemyStats();

    [SerializeField]
    private EnemyStats strongStats = new EnemyStats() { health = 50f, speed = 3f, goldReward = 25 };

    [SerializeField]
    private EnemyStats fastStats = new EnemyStats() { health = 15f, speed = 8f, goldReward = 15 };

    private EnemyType currentType = EnemyType.Basic;
    private EnemyStats stats;
    private float currentHealth;
    private bool isAlive = true;

    // Pathfinding state
    private int targetWaypointIndex = 1; // 0 is usually spawn, so head to 1
    private List<Vector3> currentPath;
    private int currentPathNodeIndex = 0;

    private void OnEnable()
    {
        if (stats == null)
        {
            stats = basicStats;
        }
    }

    private void Start()
    {
        currentHealth = stats.health;
        
        if (PathManager.Instance != null)
        {
            PathManager.Instance.OnMazeChanged += RecalculatePath;
            RecalculatePath();
        }
    }

    private void OnDestroy()
    {
        if (PathManager.Instance != null)
        {
            PathManager.Instance.OnMazeChanged -= RecalculatePath;
        }
    }

    private void RecalculatePath()
    {
        if (PathManager.Instance == null || targetWaypointIndex >= PathManager.Instance.GetWaypointCount()) return;

        Vector3 targetPos = PathManager.Instance.GetWaypoint(targetWaypointIndex);
        currentPath = PathManager.Instance.FindPath(transform.position, targetPos);
        currentPathNodeIndex = 0;

        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning("Enemy could not find path!");
        }
    }

    private void Update()
    {
        if (!isAlive) return;
        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        if (PathManager.Instance == null) return;

        // Reached final waypoint
        if (targetWaypointIndex >= PathManager.Instance.GetWaypointCount())
        {
            ReachedEnd();
            return;
        }

        if (currentPath == null || currentPathNodeIndex >= currentPath.Count)
        {
            // Reached current major waypoint, target the next one
            targetWaypointIndex++;
            if (targetWaypointIndex < PathManager.Instance.GetWaypointCount())
            {
                RecalculatePath();
            }
            return;
        }

        Vector3 targetNode = currentPath[currentPathNodeIndex];
        Vector3 moveDir = (targetNode - transform.position).normalized;
        
        transform.position += moveDir * stats.speed * Time.deltaTime;
        
        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
        }

        float distanceToNode = Vector3.Distance(transform.position, targetNode);
        if (distanceToNode < 0.1f) // reached path node
        {
            currentPathNodeIndex++;
        }
    }

    private void ReachedEnd()
    {
        GameManager.Instance.EnemyReachedEnd();
        Die();
    }

    public void TakeDamage(float damage)
    {
        if (!isAlive) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (!isAlive) return;

        isAlive = false;
        GameManager.Instance.AddGold(stats.goldReward);
        GameManager.Instance.UnregisterEnemy(this);
        Destroy(gameObject);
    }

    public void SetEnemyType(EnemyType type)
    {
        currentType = type;

        switch (type)
        {
            case EnemyType.Basic:
                stats = basicStats;
                break;
            case EnemyType.Strong:
                stats = strongStats;
                break;
            case EnemyType.Fast:
                stats = fastStats;
                break;
        }
    }

    public float GetHealth() => currentHealth;
    public float GetMaxHealth() => stats.health;
    public EnemyType GetEnemyType() => currentType;
    public float GetProgress() => PathManager.Instance != null ? (float)targetWaypointIndex / PathManager.Instance.GetWaypointCount() : 0f;
}
