using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour, ISelectable
{
    [SerializeField] private EnemyData data;

    [Header("Prefab parts")]
    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private GameObject selectionRing;

    private float currentHealth;
    private bool isAlive = true;

    // Pathfinding state
    private int targetWaypointIndex = 1; // 0 is usually spawn, so head to 1
    
    private Material normalMaterial;
    private Material tintedMaterial;
    private bool tinted;
    private Transform cachedTransform;
    private Vector3 lastLookDir;

    /// <summary>Index in <see cref="EnemyManager"/>'s list (managed by it).</summary>
    public int ManagerIndex { get; set; } = -1;

    public bool IsAlive => isAlive;

    /// <summary>Cached world position (updated by Tick), so queries do not touch the Transform.</summary>
    public Vector3 Position { get; private set; }

    /// <summary>Where towers and projectiles aim: the creep's center, not its feet.</summary>
    public Vector3 AimPoint => Position + Vector3.up * 0.4f;

    private List<StatusEffect> activeEffects = new List<StatusEffect>();
    private Vector3[] myWaypoints;
    private Vector3 formationOffset;

    /// <summary>Sets up a freshly instantiated creep. Call right after Instantiate.</summary>
    public void Init(EnemyData enemyData, Vector3[] waypoints, int index, Color color)
    {
        data = enemyData;
        currentHealth = data.health;
        myWaypoints = waypoints;

        visualRenderer.transform.localScale = Vector3.one * data.visualScale;

        // Shared coloured materials (one per colour, not one per creep)
        Material template = visualRenderer.sharedMaterial;
        normalMaterial = MaterialCache.Get(template, color);
        tintedMaterial = MaterialCache.Get(template, Color.Lerp(color, Color.cyan, 0.7f));
        visualRenderer.sharedMaterial = normalMaterial;

        formationOffset = GetSpiralOffset(index, 0.8f);
        cachedTransform = transform;
        cachedTransform.position += formationOffset;
        Position = cachedTransform.position;

        EnemyManager.Instance.Register(this);
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterUnit(cachedTransform, true);
        }
    }

    /// <summary>Called once per frame by <see cref="EnemyManager"/>.</summary>
    public void Tick(float dt)
    {
        if (!isAlive) return;
        ProcessStatusEffects(dt);
        MoveAlongPath(dt);
    }

    public void ApplyStatusEffect(StatusEffect newEffect)
    {
        // Check if we already have this effect type
        foreach (var effect in activeEffects)
        {
            if (effect.type == newEffect.type)
            {
                // Refresh duration if it's longer
                if (newEffect.duration > effect.duration)
                {
                    effect.duration = newEffect.duration;
                }
                // Override strength if it's stronger
                if (newEffect.strength > effect.strength)
                {
                    effect.strength = newEffect.strength;
                }
                return;
            }
        }

        // Add new effect if it doesn't exist
        activeEffects.Add(new StatusEffect(newEffect.type, newEffect.duration, newEffect.strength));
    }

    private void ProcessStatusEffects(float dt)
    {
        if (activeEffects.Count == 0 && !tinted) return; // nothing to update, and no material writes

        bool hasSlow = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            effect.duration -= dt;

            if (effect.type == StatusEffectType.Slow)
            {
                hasSlow = true;
            }

            if (effect.duration <= 0)
            {
                activeEffects.RemoveAt(i);
            }
        }

        // Visual feedback: only touch the material when the slowed state changes
        if (visualRenderer != null && hasSlow != tinted)
        {
            tinted = hasSlow;
            visualRenderer.sharedMaterial = hasSlow ? tintedMaterial : normalMaterial;
        }
    }

    private void MoveAlongPath(float dt)
    {
        if (PathManager.Instance == null || myWaypoints == null) return;

        // Reached final waypoint
        if (targetWaypointIndex >= myWaypoints.Length)
        {
            ReachedEnd();
            return;
        }

        Vector3 targetNode = myWaypoints[targetWaypointIndex];
        
        // Ignore Y for distance check to prevent overshooting on ramps
        float dxTarget = Position.x - targetNode.x;
        float dzTarget = Position.z - targetNode.z;
        if (dxTarget * dxTarget + dzTarget * dzTarget < 1.0f) // reached major waypoint
        {
            targetWaypointIndex++;
            if (targetWaypointIndex >= myWaypoints.Length)
            {
                ReachedEnd();
                return;
            }
            targetNode = myWaypoints[targetWaypointIndex];
        }

        Vector2 flowDir = PathManager.Instance.GetFlowDirection(Position, targetNode);
        if (flowDir == Vector2.zero)
        {
            // If the flow field returns 0, we might be blocked or at the exact destination.
            // Steer directly towards target just in case, or stop.
            flowDir = new Vector2(targetNode.x - Position.x, targetNode.z - Position.z).normalized;
        }

        Vector3 moveDir = new Vector3(flowDir.x, 0, flowDir.y);
        
        float currentSpeed = data.speed;
        
        // Apply slow effects
        float maxSlow = 0f;
        foreach (var effect in activeEffects)
        {
            if (effect.type == StatusEffectType.Slow && effect.strength > maxSlow)
            {
                maxSlow = effect.strength;
            }
        }
        
        currentSpeed *= (1f - maxSlow);
        
        Position += moveDir * currentSpeed * dt;
        cachedTransform.position = Position;

        if (moveDir != Vector3.zero && moveDir != lastLookDir) // rotation only changes when the heading does
        {
            lastLookDir = moveDir;
            cachedTransform.rotation = Quaternion.LookRotation(moveDir);
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

        damage *= 1f - data.DamageReduction;

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
        GameManager.Instance.AddGold(data.goldReward);
        GameManager.Instance.UnregisterEnemy(this);
        if (EnemyManager.Instance != null) EnemyManager.Instance.Unregister(this);
        
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterUnit(transform);
        }
        
        Destroy(gameObject);
    }

    public float GetHealth() => currentHealth;
    public float GetMaxHealth() => data.health;
    public float GetProgress() => myWaypoints != null ? (float)targetWaypointIndex / myWaypoints.Length : 0f;

    // ISelectable implementation
    public string GetDisplayName() => data != null ? data.displayName : "Creep";

    public string GetStatsText()
    {
        return $"Health: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(data.health)}\n" +
               $"Armor: {data.armor}\n" +
               $"Speed: {data.speed:0.#}\n" +
               $"Reward: {data.goldReward}G";
    }

    public bool IsSellable() => false;
    
    public int GetRefundAmount() => 0;
    
    public void Sell() { } // Cannot sell creeps

    public void SetSelected(bool isSelected)
    {
        if (selectionRing != null)
        {
            selectionRing.SetActive(isSelected);
        }
    }
    
    // Upgrade System (Enemies don't upgrade)
    public bool CanUpgrade() => false;
    public int GetUpgradeCost() => 0;
    public void Upgrade() { }

    private Vector3 GetSpiralOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;
        
        int x = 0;
        int z = 0;
        int dx = 0;
        int dz = -1;
        
        for (int i = 0; i < index; i++)
        {
            if (x == z || (x < 0 && x == -z) || (x > 0 && x == 1 - z))
            {
                int temp = dx;
                dx = -dz;
                dz = temp;
            }
            x += dx;
            z += dz;
        }
        
        return new Vector3(x * spacing, 0, z * spacing);
    }
}
