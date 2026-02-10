using UnityEngine;

public class Tower : MonoBehaviour
{
    public enum TowerType { Gun, Laser, Ice }

    [System.Serializable]
    public class TowerStats
    {
        public int cost = 100;
        public float range = 10f;
        public float fireRate = 1f;
        public float damage = 10f;
        public string displayName = "Gun Tower";
    }

    [SerializeField]
    private TowerType towerType = TowerType.Gun;

    [SerializeField]
    private TowerStats gunStats = new TowerStats() { cost = 100, range = 10f, fireRate = 1f, damage = 10f, displayName = "Gun Tower" };

    [SerializeField]
    private TowerStats laserStats = new TowerStats() { cost = 150, range = 12f, fireRate = 2f, damage = 15f, displayName = "Laser Tower" };

    [SerializeField]
    private TowerStats iceStats = new TowerStats() { cost = 120, range = 8f, fireRate = 0.5f, damage = 5f, displayName = "Ice Tower" };

    private TowerStats stats;
    private Enemy targetEnemy = null;
    private float fireTimer = 0f;
    private Transform firePoint;

    private void Start()
    {
        SetTowerType(towerType);
        TowerManager.Instance.RegisterTower(this);

        // Create visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(transform);
        visual.transform.localScale = Vector3.one * 0.8f;
        visual.transform.localPosition = Vector3.zero;
        Destroy(visual.GetComponent<Collider>());
        
        // Set color based on type
        Color color = towerType == Tower.TowerType.Gun ? Color.blue : 
                      towerType == Tower.TowerType.Laser ? Color.yellow : 
                      Color.cyan;
        visual.GetComponent<Renderer>().material.color = color;

        // Create fire point
        firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(transform);
        firePoint.localPosition = Vector3.up * 0.5f;
    }

    private void Update()
    {
        if (GameManager.Instance.IsGameOver()) return;

        FindTarget();

        if (targetEnemy != null)
        {
            AimAtTarget();
            fireTimer += Time.deltaTime;

            if (fireTimer >= 1f / stats.fireRate)
            {
                FireAtTarget();
                fireTimer = 0f;
            }
        }
    }

    private void FindTarget()
    {
        // Check if current target is still valid
        if (targetEnemy != null && Vector3.Distance(transform.position, targetEnemy.transform.position) <= stats.range)
        {
            return;
        }

        // Find nearest enemy in range
        targetEnemy = null;
        float closestDistance = stats.range;

        Collider[] colliders = Physics.OverlapSphere(transform.position, stats.range);
        foreach (Collider col in colliders)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    targetEnemy = enemy;
                }
            }
        }
    }

    private void AimAtTarget()
    {
        if (targetEnemy == null) return;

        Vector3 direction = (targetEnemy.GetComponent<Collider>().bounds.center - transform.position).normalized;
        firePoint.rotation = Quaternion.LookRotation(direction);
    }

    private void FireAtTarget()
    {
        if (targetEnemy == null) return;

        targetEnemy.TakeDamage(stats.damage);

        // Visual feedback
        Debug.DrawLine(firePoint.position, targetEnemy.GetComponent<Collider>().bounds.center, Color.yellow, 0.1f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, GetComponent<Tower>() != null ? GetComponent<Tower>().stats.range : 10f);
    }

    public void SetTowerType(TowerType type)
    {
        towerType = type;

        switch (type)
        {
            case TowerType.Gun:
                stats = gunStats;
                break;
            case TowerType.Laser:
                stats = laserStats;
                break;
            case TowerType.Ice:
                stats = iceStats;
                break;
        }
    }

    public int GetCost() => stats.cost;
    public TowerType GetTowerType() => towerType;
    public string GetDisplayName() => stats.displayName;
}
