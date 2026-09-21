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
    
    private Color baseColor;

    private List<StatusEffect> activeEffects = new List<StatusEffect>();
    private Vector3[] myWaypoints;
    private Vector3 formationOffset;

    /// <summary>Sets up a freshly instantiated creep. Call right after Instantiate.</summary>
    public void Init(EnemyData enemyData, Vector3[] waypoints, int index, Color color)
    {
        data = enemyData;
        currentHealth = data.health;
        myWaypoints = waypoints;

        baseColor = color;
        visualRenderer.transform.localScale = Vector3.one * data.visualScale;
        visualRenderer.material.color = color;

        formationOffset = GetSpiralOffset(index, 0.8f);
        transform.position += formationOffset;
    }

    private void Start()
    {
        selectionRing.GetComponent<Renderer>().material.color = Color.green;

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterUnit(transform, true);
        }
    }

    private void Update()
    {
        if (!isAlive) return;
        ProcessStatusEffects();
        MoveAlongPath();
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

    private void ProcessStatusEffects()
    {
        bool hasSlow = false;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            StatusEffect effect = activeEffects[i];
            effect.duration -= Time.deltaTime;

            if (effect.type == StatusEffectType.Slow)
            {
                hasSlow = true;
            }

            if (effect.duration <= 0)
            {
                activeEffects.RemoveAt(i);
            }
        }

        // Visual feedback
        if (visualRenderer != null)
        {
            if (hasSlow)
            {
                // Tint cyan if slowed
                visualRenderer.material.color = Color.Lerp(baseColor, Color.cyan, 0.7f);
            }
            else
            {
                visualRenderer.material.color = baseColor;
            }
        }
    }

    private void MoveAlongPath()
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
        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatTarget = new Vector3(targetNode.x, 0, targetNode.z);
        
        float distanceToTarget = Vector3.Distance(flatPos, flatTarget);
        if (distanceToTarget < 1.0f) // reached major waypoint
        {
            targetWaypointIndex++;
            if (targetWaypointIndex >= myWaypoints.Length)
            {
                ReachedEnd();
                return;
            }
            targetNode = myWaypoints[targetWaypointIndex];
        }

        Vector2 flowDir = PathManager.Instance.GetFlowDirection(transform.position, targetNode);
        if (flowDir == Vector2.zero)
        {
            // If the flow field returns 0, we might be blocked or at the exact destination.
            // Steer directly towards target just in case, or stop.
            flowDir = new Vector2(targetNode.x - transform.position.x, targetNode.z - transform.position.z).normalized;
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
        
        transform.position += moveDir * currentSpeed * Time.deltaTime;
        
        if (moveDir != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDir);
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
