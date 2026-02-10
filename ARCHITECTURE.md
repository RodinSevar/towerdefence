# Wintermaul TD Game Architecture

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         GAME BOOT                                │
│                    (ScriptBoot.cs)                              │
│      Initializes all managers, environment, and UI              │
└────────────────────────┬────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┬────────────────┐
        │                │                │                │
        ▼                ▼                ▼                ▼
   ┌─────────┐    ┌──────────┐   ┌────────────┐   ┌──────────┐
   │GameMgr  │    │WaveMgr   │   │TowerMgr    │   │PathMgr   │
   │(Core)   │    │(Spawning)│   │(Placement) │   │(Routing) │
   └────┬────┘    └────┬─────┘   └──────┬─────┘   └────┬─────┘
        │              │                │              │
        │              ▼                │              │
        │         ┌──────────┐          │              │
        └────────→│ Enemy    │←─────────┘              │
                  │ System   │                         │
                  └────┬─────┘                         │
                       │       ◄──────────────────────┘
                       │
         ┌─────────────┼─────────────┐
         │             │             │
         ▼             ▼             ▼
    ┌────────┐  ┌──────────┐  ┌─────────┐
    │ Move   │  │ Take     │  │ Die     │
    │Along   │  │ Damage   │  │Reward   │
    │Path    │  │From Tower│  │Gold     │
    └────────┘  └──────────┘  └─────────┘
         ▲
         │
         └──────────────────┐
                            │
                            ▼
                      ┌──────────────┐
                      │ Tower System │
                      │ (Targeting & │
                      │  Combat)     │
                      └──────────────┘
                            │
        ┌───────────────────┼───────────────────┐
        │                   │                   │
        ▼                   ▼                   ▼
    ┌─────────┐      ┌─────────┐      ┌─────────┐
    │ Gun     │      │ Laser   │      │ Ice     │
    │Tower    │      │Tower    │      │Tower    │
    └─────────┘      └─────────┘      └─────────┘
     Tier 1         Tier 2           Tier 3

         ▲                  ▲
         │                  │
         └──────────────────┘
              UI Events
              & Resources
```

## Component Relationships

### GameManager (Core Controller)
- **Responsibilities**: Game state, resources, wave progression
- **Manages**: Lives, Gold, Wave counter
- **Listens To**: Enemy deaths, Game over conditions
- **Broadcasts**: OnGoldChanged, OnLivesChanged, OnWaveStarted, OnGameOver, OnGameWon

### WaveManager (Enemy Spawning)
- **Responsibilities**: Spawning enemies in waves
- **Manages**: Wave progression, Enemy spawn timing
- **Calls**: Enemy constructor, Game progression
- **Configurable**: Waves array (enemy count, spawn interval, enemy type)

### Enemy (Pathfinding & Health)
- **Responsibilities**: Movement, health, damage handling
- **Behavior**: Follows PathManager waypoints, rewards gold on death
- **Registers**: With GameManager for tracking
- **Despawns**: When reaching end or taking lethal damage

### Tower (Auto-targeting & Combat)
- **Responsibilities**: Enemy targeting, firing
- **Behavior**: Finds nearby enemies, calculates lead, fires automatically
- **Range**: Sphere-cast detection around tower position
- **Registers**: With TowerManager for tracking

### PathManager (Route Definition)
- **Responsibilities**: Enemy path waypoints
- **Provides**: Waypoint positions for enemy movement
- **Visualizes**: Debug path in scene view (cyan lines and spheres)
- **Default**: S-shaped path across arena

### TowerManager (Placement System)
- **Responsibilities**: Tower placement mechanics
- **Handles**: Selection state, raycast-based placement
- **Validates**: Gold cost before placement
- **Tracks**: Active towers

### UI System (Player Feedback)
- **HUD**: Gold, Lives, Wave number display
- **Game Over Screen**: Victory/Defeat message
- **Tower Selection**: Button-based tower type selection
- **Reactive**: Updates automatically on game events

## Data Flow

### Placing a Tower
```
Player clicks button → TowerManager.SelectTowerType()
                    → Player clicks ground
                    → Raycast hits ground
                    → TrySpendGold()
                    → Tower instantiated
                    → OnGoldChanged event
                    → HUD updates
```

### Enemy Takes Damage
```
Tower finds target → Enemy.TakeDamage()
                  → Health -= damage
                  → If health ≤ 0:
                     • Die()
                     • GameManager.AddGold()
                     • GameManager.UnregisterEnemy()
                     • Check wave completion
```

### Wave Progression
```
GameManager.StartNextWave()
  → WaveManager.StartWave()
  → Spawn enemies over time
  → All enemies defeated
  → OnWaveCompleted
  → Next wave delay
  → Repeat or Victory
```

## Enemy State Machine

```
SPAWNING → PATHFINDING → REACHED_END (lose life)
   │          │
   │          └→ TAKING_DAMAGE → DEAD (reward gold)
   │
   └→ NEW_WAVE
```

## Tower Logic Loop

```
Every Frame:
1. FindTarget() - Sphere-cast for enemies in range
2. If target exists:
   - AimAtTarget() - Turn towards enemy
   - fireTimer += deltaTime
   - If fireTimer > fireInterval:
     - FireAtTarget() - Deal damage to enemy
     - Reset timer
```

## Resource Flow

```
GOLD:
Initial: 500
├─ Earned: +10-25 per enemy killed
└─ Spent: -100-150 per tower placed

LIVES:
Initial: 20
├─ Lost: -1 per enemy reaching end
└─ Game Over: When lives = 0

WAVES:
├─ Total: 5 waves (default)
├─ Completion: When all enemies killed
└─ Victory: When all waves complete
```

## Extension Points

### Adding New Tower Types
1. Add type to `Tower.TowerType` enum
2. Create stats object in Tower class
3. Add case in `SetTowerType()` switch
4. Tower button created automatically

### Adding New Enemy Types
1. Add type to `Enemy.EnemyType` enum
2. Add stats object in Enemy class
3. Modify WaveManager to spawn new type
4. Visual representation in WaveManager.SpawnEnemy()

### Modifying Game Difficulty
- **initialLives**: Start with more/fewer lives
- **initialGold**: Start with more/fewer tower budget
- **Wave stats**: More enemies, faster spawning
- **Tower costs**: More/less expensive towers

### Custom Paths
- Edit PathManager waypoints array
- Or drag Path visualization in scene

## Performance Considerations

- **Collider checks**: Towers use Physics.OverlapSphere once per frame (optimized by tower count)
- **Pathfinding**: O(1) waypoint lookup per enemy
- **Rendering**: Primitive cubes for enemies (can replace with proper models)
- **Memory**: Enemies destroyed immediately after death (no object pooling in basic version)

## Known Limitations & Future Improvements

### Current Limitations
- No projectile visuals
- All towers fire instantly
- Basic cube visuals
- Single path only
- No tower upgrades
- No sound system

### Suggested Improvements
1. **Projectiles**: Add visual projectile objects that travel to targets
2. **Tower Upgrades**: Implement sell/repair/upgrade UI
3. **Visual Polish**: Replace cubes with actual models
4. **Audio**: Add tower fire sounds, enemy sounds, background music
5. **Difficulty Scaling**: Implement difficulty presets
6. **Tower Types**: Add more specialized tower types
7. **Enemy Types**: Add fast, flying, armored enemy variants
8. **Map Editor**: Build custom maps with waypoint editor
