using UnityEngine;

public class Tower : MonoBehaviour, ISelectable
{
    public enum TowerType { Gun, Laser, Ice }

    [System.Serializable]
    public class TowerLevel
    {
        public int cost = 100;
        public float range = 10f;
        public float fireRate = 1f;
        public float damage = 10f;
        public string displayName = "Gun Tower";
        
        [Header("Visuals")]
        public Color visualColor = Color.blue;
        public float visualScale = 0.8f;
        
        [Header("Projectile")]
        public float projectileSpeed = 15f;
        public Color projectileColor = Color.yellow;
        public float projectileScale = 0.2f;
        
        [Header("Status Effect")]
        public bool hasStatusEffect = false;
        public StatusEffectType effectType;
        public float effectDuration;
        public float effectStrength;
    }

    [SerializeField]
    private TowerType towerType = TowerType.Gun;

    // Hardcoded default arrays for now so they work without Inspector setup
    [SerializeField]
    private TowerLevel[] gunLevels = new TowerLevel[] {
        new TowerLevel() { cost = 10, range = 10f, fireRate = 1f, damage = 10f, displayName = "Gun Tower Lvl 1", visualColor = Color.blue, visualScale = 0.8f, projectileSpeed = 15f, projectileColor = Color.yellow, projectileScale = 0.2f },
        new TowerLevel() { cost = 20, range = 12f, fireRate = 1.5f, damage = 25f, displayName = "Gun Tower Lvl 2", visualColor = new Color(0, 0, 0.7f), visualScale = 1.0f, projectileSpeed = 20f, projectileColor = new Color(1f, 0.5f, 0f), projectileScale = 0.3f }
    };

    [SerializeField]
    private TowerLevel[] laserLevels = new TowerLevel[] {
        new TowerLevel() { cost = 15, range = 12f, fireRate = 2f, damage = 15f, displayName = "Laser Tower Lvl 1", visualColor = Color.yellow, visualScale = 0.8f },
        new TowerLevel() { cost = 30, range = 15f, fireRate = 3f, damage = 35f, displayName = "Laser Tower Lvl 2", visualColor = new Color(0.8f, 0.8f, 0f), visualScale = 1.0f }
    };

    [SerializeField]
    private TowerLevel[] iceLevels = new TowerLevel[] {
        new TowerLevel() { cost = 12, range = 8f, fireRate = 0.5f, damage = 5f, displayName = "Ice Tower Lvl 1", visualColor = Color.cyan, visualScale = 0.8f, projectileSpeed = 24f, projectileColor = Color.cyan, projectileScale = 0.4f, hasStatusEffect = true, effectType = StatusEffectType.Slow, effectDuration = 2f, effectStrength = 0.5f },
        new TowerLevel() { cost = 25, range = 10f, fireRate = 1f, damage = 15f, displayName = "Ice Tower Lvl 2", visualColor = new Color(0, 0.7f, 0.7f), visualScale = 1.0f, projectileSpeed = 30f, projectileColor = Color.cyan, projectileScale = 0.5f, hasStatusEffect = true, effectType = StatusEffectType.Slow, effectDuration = 3f, effectStrength = 0.7f }
    };

    private TowerLevel[] currentUpgradePath;
    private int currentLevelIndex = 0;
    
    private TowerLevel currentLevel;
    
    private Enemy targetEnemy = null;
    private float fireTimer = 0f;
    private Transform firePoint;
    private GameObject selectionRing;
    private GameObject visual;
    
    private LineRenderer laserLine;
    private float laserDisplayTimer = 0f;

    private void Start()
    {
        SetTowerType(towerType);
        TowerManager.Instance.RegisterTower(this);
        
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterUnit(transform, false);
        }

        // Create visual
        visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.transform.SetParent(transform);
        ApplyCurrentLevelVisuals();

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
        
        // Setup laser line renderer
        if (towerType == TowerType.Laser)
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
        if (GameManager.Instance == null || currentLevel == null) return;
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

            if (fireTimer >= 1f / currentLevel.fireRate)
            {
                FireAtTarget();
                fireTimer = 0f;
            }
        }
    }

    private void FindTarget()
    {
        // Check if current target is still valid
        if (targetEnemy != null && Vector3.Distance(transform.position, targetEnemy.transform.position) <= currentLevel.range)
        {
            return;
        }

        // Find nearest enemy in range
        targetEnemy = null;
        float closestDistance = currentLevel.range;

        Collider[] colliders = Physics.OverlapSphere(transform.position, currentLevel.range);
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

        if (towerType == TowerType.Laser)
        {
            // Laser is instant damage
            targetEnemy.TakeDamage(currentLevel.damage);
            
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
            // Gun and Ice fire physical projectiles
            GameObject projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projObj.transform.position = firePoint.position;
            
            // Remove collider so it doesn't block rays
            Destroy(projObj.GetComponent<Collider>());
            
            projObj.transform.localScale = Vector3.one * currentLevel.projectileScale;
            projObj.GetComponent<Renderer>().material.color = currentLevel.projectileColor;
            
            Projectile proj = projObj.AddComponent<Projectile>();
            
            StatusEffect payload = null;
            if (currentLevel.hasStatusEffect)
            {
                payload = new StatusEffect(currentLevel.effectType, currentLevel.effectDuration, currentLevel.effectStrength);
            }
            
            proj.Initialize(targetEnemy, currentLevel.projectileSpeed, currentLevel.damage, payload);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (currentLevel != null)
        {
            Gizmos.DrawWireSphere(transform.position, currentLevel.range);
        }
    }

    public void SetTowerType(TowerType type)
    {
        towerType = type;
        currentLevelIndex = 0;
        
        switch (towerType)
        {
            case TowerType.Gun:
                currentUpgradePath = gunLevels;
                break;
            case TowerType.Laser:
                currentUpgradePath = laserLevels;
                break;
            case TowerType.Ice:
                currentUpgradePath = iceLevels;
                break;
        }
        
        if (currentUpgradePath != null && currentUpgradePath.Length > 0)
        {
            currentLevel = currentUpgradePath[0];
        }
    }

    private void ApplyCurrentLevelVisuals()
    {
        if (visual == null || currentLevel == null) return;
        
        visual.transform.localScale = Vector3.one * currentLevel.visualScale;
        visual.transform.localPosition = Vector3.zero;
        visual.GetComponent<Renderer>().material.color = currentLevel.visualColor;
    }

    public int GetCost() => currentLevel != null ? currentLevel.cost : 0;
    public float GetDamage() => currentLevel != null ? currentLevel.damage : 0f;
    public float GetRange() => currentLevel != null ? currentLevel.range : 0f;
    public float GetFireRate() => currentLevel != null ? currentLevel.fireRate : 0f;
    public TowerType GetTowerType() => towerType;
    public string GetDisplayName()
    {
        return currentLevel != null ? currentLevel.displayName : "Unknown Tower";
    }

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

    public string GetStatsText()
    {
        if (currentLevel == null) return "";
        return $"Damage: {currentLevel.damage}\nRange: {currentLevel.range}\nFire Rate: {currentLevel.fireRate}/s";
    }

    public bool IsSellable()
    {
        return true; // All towers can be sold
    }
    
    public int GetRefundAmount()
    {
        // Refund sum of all levels bought so far (or half of it)
        int totalCost = 0;
        for (int i = 0; i <= currentLevelIndex; i++)
        {
            totalCost += currentUpgradePath[i].cost;
        }
        return totalCost / 2;
    }

    public bool CanUpgrade()
    {
        return currentUpgradePath != null && currentLevelIndex < currentUpgradePath.Length - 1;
    }

    public int GetUpgradeCost()
    {
        if (CanUpgrade())
        {
            return currentUpgradePath[currentLevelIndex + 1].cost;
        }
        return 0;
    }

    public void Upgrade()
    {
        if (CanUpgrade())
        {
            currentLevelIndex++;
            currentLevel = currentUpgradePath[currentLevelIndex];
            ApplyCurrentLevelVisuals();
        }
    }

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
        
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterUnit(transform);
        }

        // Instantly notify A* system that the maze has opened up
        if (PathManager.Instance != null)
        {
            PathManager.Instance.NotifyMazeChanged();
        }

        Destroy(gameObject);
    }
}
