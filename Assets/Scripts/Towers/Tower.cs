using UnityEngine;

/// <summary>
/// A placed tower. Structure (visual, fire point, selection ring) lives in the Tower prefab;
/// stats, levels and attack style come from a <see cref="TowerData"/> asset passed to <see cref="Init"/>.
/// </summary>
public class Tower : MonoBehaviour, ISelectable
{
    [SerializeField] private TowerData data;

    [Header("Prefab parts")]
    [SerializeField] private Transform visual;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject selectionRing;

    private int currentLevelIndex = 0;
    private TowerLevel CurrentLevel => data != null && data.levels.Length > 0 ? data.levels[currentLevelIndex] : null;

    private Enemy targetEnemy = null;
    private float fireTimer = 0f;

    private LineRenderer laserLine;
    private float laserDisplayTimer = 0f;

    /// <summary>Assigns the tower's data and applies level-1 visuals. Call right after instantiating.</summary>
    public void Init(TowerData towerData)
    {
        data = towerData;
        currentLevelIndex = 0;
        ApplyCurrentLevelVisuals();
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError($"Tower '{name}' has no TowerData; call Init() after instantiating.", this);
            enabled = false;
            return;
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterUnit(transform, false);
        }

        selectionRing.GetComponent<Renderer>().material.color = Color.green;

        if (data.attackStyle == TowerAttackStyle.Laser)
        {
            laserLine = gameObject.AddComponent<LineRenderer>();
            laserLine.startWidth = 0.1f;
            laserLine.endWidth = 0.1f;
            laserLine.material = new Material(Shader.Find("Sprites/Default"));
            laserLine.startColor = Color.yellow;
            laserLine.endColor = Color.red;
            laserLine.enabled = false;
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || CurrentLevel == null) return;
        if (GameManager.Instance.IsGameOver()) return;

        if (laserLine != null && laserLine.enabled)
        {
            laserDisplayTimer -= Time.deltaTime;
            if (laserDisplayTimer <= 0)
            {
                laserLine.enabled = false;
            }
            else if (targetEnemy != null)
            {
                // Update laser position if still firing
                laserLine.SetPosition(0, firePoint.position);
                laserLine.SetPosition(1, targetEnemy.transform.position + Vector3.up * 0.4f);
            }
        }

        fireTimer += Time.deltaTime;

        FindTarget();

        if (targetEnemy != null)
        {
            AimAtTarget();

            if (fireTimer >= 1f / CurrentLevel.fireRate)
            {
                FireAtTarget();
                fireTimer = 0f;
            }
        }
    }

    private void FindTarget()
    {
        float range = CurrentLevel.range;

        // Check if current target is still valid
        if (targetEnemy != null && Vector3.Distance(transform.position, targetEnemy.transform.position) <= range)
        {
            return;
        }

        // Find nearest enemy in range
        targetEnemy = null;
        float closestDistance = range;

        Collider[] colliders = Physics.OverlapSphere(transform.position, range);
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

        TowerLevel level = CurrentLevel;

        if (data.attackStyle == TowerAttackStyle.Laser)
        {
            // Laser is instant damage
            targetEnemy.TakeDamage(level.damage);

            if (laserLine != null)
            {
                laserLine.enabled = true;
                laserLine.SetPosition(0, firePoint.position);
                laserLine.SetPosition(1, targetEnemy.transform.position + Vector3.up * 0.4f);
                laserDisplayTimer = 0.1f; // Display laser for 0.1 seconds
            }
        }
        else
        {
            GameObject projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.transform.position = firePoint.position;

            // Remove collider so it doesn't block rays
            Destroy(projObj.GetComponent<Collider>());

            projObj.transform.localScale = Vector3.one * level.projectileScale;
            projObj.GetComponent<Renderer>().material.color = level.projectileColor;

            Projectile proj = projObj.AddComponent<Projectile>();

            StatusEffect payload = null;
            if (level.hasStatusEffect)
            {
                payload = new StatusEffect(level.effectType, level.effectDuration, level.effectStrength);
            }

            proj.Initialize(targetEnemy, level.projectileSpeed, level.damage, payload);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (CurrentLevel != null)
        {
            Gizmos.DrawWireSphere(transform.position, CurrentLevel.range);
        }
    }

    private void ApplyCurrentLevelVisuals()
    {
        TowerLevel level = CurrentLevel;
        if (visual == null || level == null) return;

        visual.localScale = Vector3.one * level.visualScale;
        visual.localPosition = Vector3.zero;
        visual.GetComponent<Renderer>().material.color = level.visualColor;
    }

    public int GetCost() => CurrentLevel != null ? CurrentLevel.cost : 0;
    public float GetDamage() => CurrentLevel != null ? CurrentLevel.damage : 0f;
    public float GetRange() => CurrentLevel != null ? CurrentLevel.range : 0f;
    public float GetFireRate() => CurrentLevel != null ? CurrentLevel.fireRate : 0f;

    // ISelectable implementation
    public string GetDisplayName()
    {
        return data != null ? $"{data.displayName} Lvl {currentLevelIndex + 1}" : "Unknown Tower";
    }

    public void SetSelected(bool selected)
    {
        if (selectionRing != null)
        {
            selectionRing.SetActive(selected);
        }
    }

    public string GetStatsText()
    {
        TowerLevel level = CurrentLevel;
        if (level == null) return "";
        return $"Damage: {level.damage}\nRange: {level.range}\nFire Rate: {level.fireRate}/s";
    }

    public bool IsSellable() => true;

    /// <summary>Half of everything spent on this tower so far.</summary>
    public int GetRefundAmount()
    {
        int totalCost = 0;
        for (int i = 0; i <= currentLevelIndex; i++)
        {
            totalCost += data.levels[i].cost;
        }
        return totalCost / 2;
    }

    public bool CanUpgrade()
    {
        return data != null && currentLevelIndex < data.levels.Length - 1;
    }

    public int GetUpgradeCost()
    {
        return CanUpgrade() ? data.levels[currentLevelIndex + 1].cost : 0;
    }

    public void Upgrade()
    {
        if (CanUpgrade())
        {
            currentLevelIndex++;
            ApplyCurrentLevelVisuals();
        }
    }

    public void Sell()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(GetRefundAmount());
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

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterUnit(transform);
        }

        // Notify enemies that the maze has opened up
        if (PathManager.Instance != null)
        {
            PathManager.Instance.NotifyMazeChanged();
        }

        Destroy(gameObject);
    }
}
