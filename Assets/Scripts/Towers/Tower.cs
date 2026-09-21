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

    private Vector3 towerPosition;
    private StatusEffect payloadEffect; // shared by every shot of the current level (creeps copy it on hit)
    private Material visualTemplate; // the prefab's original material, colours are derived from it
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

        towerPosition = transform.position;

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterUnit(transform, false);
        }

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
                laserLine.SetPosition(1, targetEnemy.AimPoint);
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

        // Keep the current target while it is alive and in range
        if (targetEnemy != null && targetEnemy.IsAlive && (targetEnemy.Position - towerPosition).sqrMagnitude <= range * range)
        {
            return;
        }

        targetEnemy = EnemyManager.Instance.FindNearest(towerPosition, range);
    }

    private void AimAtTarget()
    {
        if (targetEnemy == null) return;

        Vector3 direction = (targetEnemy.AimPoint - towerPosition).normalized;
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
                laserLine.SetPosition(1, targetEnemy.AimPoint);
                laserDisplayTimer = 0.1f; // Display laser for 0.1 seconds
            }
        }
        else
        {
            Projectile.Spawn(firePoint.position, level.projectileScale, level.projectileColor,
                targetEnemy, level.projectileSpeed, level.damage, payloadEffect);
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
        payloadEffect = level != null && level.hasStatusEffect
            ? new StatusEffect(level.effectType, level.effectDuration, level.effectStrength) : null;
        if (visual == null || level == null) return;

        visual.localScale = Vector3.one * (level.visualScale * GridManager.Instance.FootprintWorldSize); // visuals were authored for one unit
        visual.localPosition = Vector3.zero;
        var visualRenderer = visual.GetComponent<Renderer>();
        if (visualTemplate == null) visualTemplate = visualRenderer.sharedMaterial;
        visualRenderer.sharedMaterial = MaterialCache.Get(visualTemplate, level.visualColor);
    }

    public int GetCost() => CurrentLevel != null ? CurrentLevel.cost : 0;
    public float GetDamage() => CurrentLevel != null ? CurrentLevel.damage : 0f;
    public float GetRange() => CurrentLevel != null ? CurrentLevel.range : 0f;
    public float GetFireRate() => CurrentLevel != null ? CurrentLevel.fireRate : 0f;

    // ISelectable implementation
    public string GetDisplayName()
    {
        if (data == null) return "Unknown Tower";
        return data.levels.Length > 1 ? $"{data.displayName} Lvl {currentLevelIndex + 1}" : data.displayName;
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
        return $"Damage: {level.damage:0.#}\nRange: {level.range:0.#}\nFire Rate: {level.fireRate:0.##}/s";
    }

    public bool IsSellable() => true;

    /// <summary>Sell value from the map data if defined, else half of everything spent on this tower.</summary>
    public int GetRefundAmount()
    {
        if (data.sellValue >= 0) return data.sellValue;

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
            GridManager.Instance.FreeFootprint(GridManager.Instance.FootprintOrigin(transform.position));
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
