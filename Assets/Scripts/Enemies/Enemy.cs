using UnityEngine;

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
    private int currentWaypointIndex = 0;
    private float distanceToNextWaypoint = 0f;
    private bool isAlive = true;

    private void OnEnable()
    {
        // Initialize stats to Basic if not already set
        if (stats == null)
        {
            stats = basicStats;
        }
    }

    private void Start()
    {
        currentHealth = stats.health;
        UpdateDistanceToNextWaypoint();
    }

    private void Update()
    {
        if (!isAlive) return;

        MoveAlongPath();
    }

    private void MoveAlongPath()
    {
        if (PathManager.Instance == null)
        {
            Debug.LogWarning("PathManager not initialized yet!");
            return;
        }

        if (currentWaypointIndex >= PathManager.Instance.GetWaypointCount())
        {
            ReachedEnd();
            return;
        }

        Vector3 targetWaypoint = PathManager.Instance.GetWaypoint(currentWaypointIndex);
        Vector3 direction = (targetWaypoint - transform.position).normalized;

        // Move towards waypoint
        transform.position += direction * stats.speed * Time.deltaTime;

        // Rotate to face direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Check if reached waypoint
        float distanceToWaypoint = Vector3.Distance(transform.position, targetWaypoint);
        if (distanceToWaypoint < 0.5f)
        {
            currentWaypointIndex++;
            if (PathManager.Instance != null && currentWaypointIndex < PathManager.Instance.GetWaypointCount())
            {
                UpdateDistanceToNextWaypoint();
            }
        }
        

    }

    private void UpdateDistanceToNextWaypoint()
    {
        if (PathManager.Instance == null)
            return;

        if (currentWaypointIndex < PathManager.Instance.GetWaypointCount())
        {
            Vector3 nextWaypoint = PathManager.Instance.GetWaypoint(currentWaypointIndex);
            distanceToNextWaypoint = Vector3.Distance(transform.position, nextWaypoint);
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
    public float GetProgress() => PathManager.Instance != null ? (float)currentWaypointIndex / PathManager.Instance.GetWaypointCount() : 0f;
}
