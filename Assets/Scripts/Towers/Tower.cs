using UnityEngine;

public class Tower : MonoBehaviour, ISelectable
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
    private TowerStats gunStats = new TowerStats() { cost = 10, range = 10f, fireRate = 1f, damage = 10f, displayName = "Gun Tower" };

    [SerializeField]
    private TowerStats laserStats = new TowerStats() { cost = 15, range = 12f, fireRate = 2f, damage = 15f, displayName = "Laser Tower" };

    [SerializeField]
    private TowerStats iceStats = new TowerStats() { cost = 12, range = 8f, fireRate = 0.5f, damage = 5f, displayName = "Ice Tower" };

    private TowerStats stats;
    private Enemy targetEnemy = null;
    private float fireTimer = 0f;
    private Transform firePoint;
    private GameObject selectionRing;

    private void Start()
    {
        SetTowerType(towerType);
        TowerManager.Instance.RegisterTower(this);

        // Create visual
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(transform);
        visual.transform.localScale = Vector3.one * 0.8f;
        visual.transform.localPosition = Vector3.zero;
        
        // Set color based on type
        Color color = towerType == Tower.TowerType.Gun ? Color.blue : 
                      towerType == Tower.TowerType.Laser ? Color.yellow : 
                      Color.cyan;
        visual.GetComponent<Renderer>().material.color = color;

        // Removed the giant SphereCollider that was bleeding into adjacent cells and blocking ground clicks
        firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(transform);
        firePoint.localPosition = Vector3.up * 0.5f;

        // Create selection ring visual (a slightly larger, flat cylinder at the base)
        selectionRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        selectionRing.transform.SetParent(transform);
        selectionRing.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
        selectionRing.transform.localPosition = new Vector3(0, 0.05f, 0);
        Destroy(selectionRing.GetComponent<Collider>());
        selectionRing.GetComponent<Renderer>().material.color = Color.green;
        selectionRing.SetActive(false);
    }

    private void Update()
    {
        if (GameManager.Instance == null || stats == null) return;
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

    public int GetCost() => stats != null ? stats.cost : 0;
    public float GetDamage() => stats != null ? stats.damage : 0f;
    public float GetRange() => stats != null ? stats.range : 0f;
    public float GetFireRate() => stats != null ? stats.fireRate : 0f;
    public TowerType GetTowerType() => towerType;
    public string GetDisplayName() => stats != null ? stats.displayName : "Tower";

    public void SetSelected(bool selected)
    {
        if (selectionRing != null)
        {
            selectionRing.SetActive(selected);
        }
    }

    public void SellTower()
    {
        Sell();
    }

    // ISelectable implementation
    public string GetStatsText()
    {
        return $"Damage: {GetDamage()}\n" +
               $"Range: {GetRange()}\n" +
               $"Speed: {GetFireRate()}s";
    }

    public bool IsSellable() => true;
    
    public int GetRefundAmount() => GetCost() / 2;

    public void Sell()
    {
        // Refund 50%
        int refund = GetCost() / 2;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(refund);
        }

        // Free up the grid cell
        if (GridManager.Instance != null)
        {
            Vector2Int cell = GridManager.Instance.WorldToGridCell(transform.position);
            GridManager.Instance.FreeCell(cell);
        }

        // Unregister from the manager
        if (TowerManager.Instance != null)
        {
            TowerManager.Instance.UnregisterTower(this);
        }

        // Instantly notify A* system that the maze has opened up
        if (PathManager.Instance != null)
        {
            PathManager.Instance.NotifyMazeChanged();
        }

        Destroy(gameObject);
    }
}
