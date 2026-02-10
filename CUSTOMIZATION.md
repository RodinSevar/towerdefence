# Customization Examples

## Common Modifications

### 1. Adjusting Game Difficulty

**Increase initial resources:**
```csharp
// In GameManager.cs - change these values:
public int initialLives = 30;      // Was 20
public int initialGold = 800;      // Was 500
public float waveDelay = 6f;       // Was 5f (delay between waves)
```

**Make waves harder:**
```csharp
// In WaveManager.cs - edit SetupDefaultWaves():
private void SetupDefaultWaves()
{
    waves = new Wave[7];  // Was 5, now 7 waves
    for (int i = 0; i < 7; i++)
    {
        waves[i] = new Wave
        {
            enemyCount = 8 + (i * 5),      // More enemies per wave
            spawnInterval = 0.3f - (i * 0.04f),  // Faster spawning
            enemyType = i < 3 ? Enemy.EnemyType.Basic : Enemy.EnemyType.Strong
        };
    }
}
```

**Make towers cheaper:**
```csharp
// In Tower.cs - change these values:
gunStats = new TowerStats() { 
    cost = 75,        // Was 100
    range = 10f, 
    fireRate = 1f, 
    damage = 10f 
};
```

---

### 2. Adding a New Tower Type

**Step 1: Add type to enum (in Tower.cs)**
```csharp
public enum TowerType { Gun, Laser, Ice, Cannon }  // Add Cannon
```

**Step 2: Add stats (in Tower.cs Start method)**
```csharp
[SerializeField]
private TowerStats cannonStats = new TowerStats() 
{ 
    cost = 200, 
    range = 15f, 
    fireRate = 0.5f, 
    damage = 30f, 
    displayName = "Cannon Tower" 
};
```

**Step 3: Update SetTowerType (in Tower.cs)**
```csharp
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
        case TowerType.Cannon:  // Add this
            stats = cannonStats;
            break;
    }
}
```

---

### 3. Adding a New Enemy Type

**Step 1: Add type to enum (in Enemy.cs)**
```csharp
public enum EnemyType { Basic, Strong, Fast, Flying }  // Add Flying
```

**Step 2: Add stats (in Enemy.cs)**
```csharp
[SerializeField]
private EnemyStats flyingStats = new EnemyStats() 
{ 
    health = 10f, 
    speed = 10f, 
    goldReward = 20 
};
```

**Step 3: Update SetEnemyType (in Enemy.cs)**
```csharp
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
        case EnemyType.Flying:  // Add this
            stats = flyingStats;
            break;
    }
}
```

**Step 4: Update WaveManager (in WaveManager.cs)**
```csharp
private void SetupDefaultWaves()
{
    waves = new Wave[5];
    for (int i = 0; i < 5; i++)
    {
        waves[i] = new Wave
        {
            enemyCount = 5 + (i * 3),
            spawnInterval = 0.5f - (i * 0.05f),
            // Mix enemy types as waves progress
            enemyType = i < 1 ? Enemy.EnemyType.Basic : 
                       i < 2 ? Enemy.EnemyType.Fast :
                       i < 3 ? Enemy.EnemyType.Strong :
                       i < 4 ? Enemy.EnemyType.Flying :
                       Enemy.EnemyType.Strong
        };
    }
}
```

---

### 4. Creating a Custom Enemy Path

**Edit waypoints in PathManager.cs:**
```csharp
private void CreateDefaultPath()
{
    // Create a circular path
    waypoints = new Vector3[]
    {
        new Vector3(0, 0.5f, -15),     // Bottom
        new Vector3(10, 0.5f, -10),    // Bottom right
        new Vector3(15, 0.5f, 0),      // Right
        new Vector3(10, 0.5f, 10),     // Top right
        new Vector3(0, 0.5f, 15),      // Top
        new Vector3(-10, 0.5f, 10),    // Top left
        new Vector3(-15, 0.5f, 0),     // Left
        new Vector3(-10, 0.5f, -10)    // Bottom left
    };
}
```

---

### 5. Modifying Tower Attack Behavior

**Add a slow effect to Ice Tower (in Tower.cs):**
```csharp
private void FireAtTarget()
{
    if (targetEnemy == null) return;

    targetEnemy.TakeDamage(stats.damage);
    
    // Add slow effect for Ice Tower
    if (towerType == TowerType.Ice)
    {
        // Get enemy speed temporarily
        // This is a placeholder - you'd need to modify Enemy.cs to support this
        // For now, just damage
    }

    Debug.DrawLine(firePoint.position, 
                   targetEnemy.GetComponent<Collider>().bounds.center, 
                   Color.yellow, 0.1f);
}
```

---

### 6. Adding Tower Selling

**Add to Tower.cs:**
```csharp
public void Sell()
{
    int refund = (int)(stats.cost * 0.8f);  // 80% refund
    GameManager.Instance.AddGold(refund);
    TowerManager.Instance.UnregisterTower(this);
    Destroy(gameObject);
}
```

**Add to HUD for click detection:**
```csharp
private void Update()
{
    if (Input.GetMouseButtonDown(1))  // Right click
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Tower tower = hit.collider.GetComponent<Tower>();
            if (tower != null)
            {
                tower.Sell();
            }
        }
    }
}
```

---

### 7. Changing Ground Size and Color

**Edit in GameBoot.cs SetupEnvironment():**
```csharp
// Change ground size
Vector3[] vertices = new Vector3[4]
{
    new Vector3(-30, 0, -30),   // Larger ground
    new Vector3(30, 0, -30),
    new Vector3(-30, 0, 30),
    new Vector3(30, 0, 30)
};

// Change ground color
renderer.material.color = new Color(0.1f, 0.6f, 0.1f);  // Different green
```

---

### 8. Adding UI Buttons for Game Controls

**Add to UI system (in GameBoot.cs SetupUI()):**
```csharp
// Create pause button
GameObject pauseButtonObj = new GameObject("PauseButton");
pauseButtonObj.transform.SetParent(canvas.transform, false);

Button pauseButton = pauseButtonObj.AddComponent<Button>();
pauseButton.onClick.AddListener(() => Time.timeScale = Time.timeScale == 0 ? 1 : 0);

// Add button visual
Image pauseImage = pauseButtonObj.AddComponent<Image>();
pauseImage.color = new Color(0.2f, 0.2f, 0.8f);

RectTransform pauseRect = pauseButtonObj.GetComponent<RectTransform>();
pauseRect.anchorMin = new Vector2(0.5f, 0);
pauseRect.anchorMax = new Vector2(0.5f, 0);
pauseRect.offsetMin = new Vector2(-100, 10);
pauseRect.offsetMax = new Vector2(100, 70);
```

---

### 9. Adding Score System

**Add to GameManager.cs:**
```csharp
private int score = 0;
public System.Action<int> OnScoreChanged;

public void AddScore(int points)
{
    score += points;
    OnScoreChanged?.Invoke(score);
}

// Call when enemy dies (in WaveManager.SpawnEnemy or Enemy.Die)
```

---

### 10. Adjusting Camera View

**Edit in GameBoot.cs SetupEnvironment():**
```csharp
// Change camera position and angle for different view
cameraObj.transform.position = new Vector3(0, 20, -15);  // Higher/further
cameraObj.transform.rotation = Quaternion.Euler(50, 0, 0);  // More overhead view
```

---

## Debugging Tips

### Enable comprehensive logging:
```csharp
// Add to GameManager.OnGoldChanged and other callbacks:
Debug.Log($"Gold changed to: {currentGold}");
Debug.Log($"Lives: {currentLives}");
Debug.Log($"Wave: {currentWave}");
```

### Visualize tower ranges in Scene View:
```csharp
// Already included in Tower.cs OnDrawGizmosSelected()
// You can see ranges when tower is selected
```

### Check enemy pathfinding:
```csharp
// Already included in PathManager.OnDrawGizmos()
// Shows cyan path in Scene view
```

### Test tower placement:
- Select TowerManager in scene
- Check in Inspector that towers register correctly
- Use console to verify gold transactions

---

## Performance Optimization Tips

1. **Use Object Pooling** for frequently spawned enemies:
   - Create a pool of enemy objects
   - Reuse instead of destroying/creating

2. **Optimize GetComponent calls**:
   - Cache component references in Start()
   - Avoid repeated GetComponent in Update()

3. **Reduce circle casts**:
   - Only towers cast, not every frame per tower
   - Increase check interval if performance drops

4. **Use LOD (Level of Detail)**:
   - Simpler visuals at distance
   - More detail when zoomed

5. **Batch rendering**:
   - Use atlased textures for enemies/towers
   - Combine meshes where possible

---

## Scripting Best Practices Used

✅ Singleton pattern for managers (GameManager, WaveManager, etc.)
✅ Event-driven architecture (OnGoldChanged, OnGameOver, etc.)
✅ Scriptable Objects ready (Wave data could use SO)
✅ Separation of concerns (each script has single responsibility)
✅ Encapsulation (private fields, public methods)

---

## Testing Checklist

- [ ] Game initializes without errors
- [ ] Enemies spawn correctly
- [ ] Enemy pathfinding works
- [ ] Towers place correctly
- [ ] Towers target and fire at enemies
- [ ] Enemies die and reward gold
- [ ] Game ends when losing all lives
- [ ] Game wins after all waves
- [ ] UI updates correctly
- [ ] Custom modifications don't break gameplay
