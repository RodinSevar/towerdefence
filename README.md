# Wintermaul TD Clone - Unity Tower Defense Game

A tower defense game inspired by Wintermaul TD, built with Unity 3D.

## Setup Instructions

### Quick Start (Recommended)
1. Open the project in Unity
2. Open the **SampleScene** from `Assets/Scenes/SampleScene.unity`
3. Create an empty GameObject in the scene and name it "Boot"
4. Attach the **GameBoot** script to it (`Assets/Scripts/GameBoot.cs`)
5. Press Play

The GameBoot script will automatically set up:
- Camera and lighting
- Game ground and environment
- All game managers (GameManager, WaveManager, TowerManager, PathManager)
- Complete UI (HUD, tower selection buttons, game over screen)

## Game Features

### Core Mechanics
- **Enemy Waves**: Enemies spawn in waves and travel along a defined path
- **Tower Placement**: Click tower buttons, then click on the ground to place towers
- **Tower Types**:
  - **Gun Tower** ($100): Basic tower with medium range and rate of fire
  - **Laser Tower** ($150): Long range with high damage
  - **Ice Tower** ($120): Shorter range with slower fire rate
- **Resource Management**: Earn gold by defeating enemies, spend gold on towers
- **Lives System**: Start with 20 lives, lose 1 when an enemy reaches the end
- **Win/Lose Conditions**: Lose if lives reach 0, win if all waves are cleared

### Enemy Types
- **Basic**: Standard enemies with medium health and speed
- **Strong**: Slow but high health
- **Fast**: Low health but high speed

## Game Systems

### GameManager
- Manages game state (running, over, won)
- Tracks resources (gold, lives)
- Handles wave progression
- Events: OnGoldChanged, OnLivesChanged, OnWaveStarted, OnGameOver, OnGameWon

### WaveManager
- Spawns enemies in configurable waves
- Can be customized in the Inspector or programmatically
- Default: 5 waves with increasing difficulty

### PathManager
- Defines the path enemies follow
- Visual debug path display in Scene view
- Default: S-shaped path across the arena

### TowerManager
- Handles tower placement
- Manages active towers
- Raycast-based placement on ground

### Enemy System
- Pathfinding along waypoints
- Health and damage system
- Gold rewards on defeat

### Tower System
- Automatic targeting of nearby enemies
- Range-based detection
- Varied attack stats per tower type

## Customization Guide

### Adding More Waves
Edit the WaveManager to add more waves with different configurations:
```csharp
waves = new Wave[7]; // More waves
for (int i = 0; i < 7; i++)
{
    waves[i] = new Wave { ... };
}
```

### Modifying Tower Stats
In the Tower script, adjust TowerStats for each tower type:
```csharp
gunStats = new TowerStats() { 
    cost = 150, 
    range = 12f, 
    fireRate = 1.5f, 
    damage = 15f 
};
```

### Changing the Enemy Path
Edit waypoints in PathManager or set them in the Inspector:
```csharp
waypoints = new Vector3[] { ... };
```

### Adjusting Game Difficulty
In GameManager, modify initial settings:
```csharp
public int initialLives = 25;  // More lives
public int initialGold = 750;  // More starting gold
public float waveDelay = 3f;   // Faster waves
```

## Controls

- **Click Tower Button**: Select a tower type to place
- **Click Ground**: Place the selected tower
- **ESC Key**: Cancel tower placement
- **Mouse**: Hover over ground to see tower range (Gizmos view)

## File Structure

```
Assets/
├── Scripts/
│   ├── GameBoot.cs                 # Scene initialization
│   ├── SceneSetup.cs              # Legacy setup (not used if GameBoot present)
│   ├── Managers/
│   │   ├── GameManager.cs         # Main game controller
│   │   ├── WaveManager.cs         # Enemy wave spawning
│   │   ├── TowerManager.cs        # Tower placement management
│   │   └── PathManager.cs         # Enemy path definition
│   ├── Enemies/
│   │   └── Enemy.cs               # Enemy behavior and stats
│   ├── Towers/
│   │   └── Tower.cs               # Tower behavior and combat
│   └── UI/
│       ├── HUD.cs                 # Game info display
│       └── TowerSelectionUI.cs    # Tower selection buttons
└── Prefabs/
    └── (Tower and Enemy prefabs can be created here)
```

## Requirements
- Unity 2022 LTS or newer
- TextMeshPro (included with Unity)
- Standard rendering pipeline

## Known Limitations
- Tower placement is single-click; no preview before placement
- Towers fire instantly on ranged targets (no projectiles)
- UI is basic with no animations
- No audio system implemented
- Limited to 5 waves by default

## Future Enhancement Ideas
- Projectile visual effects
- Tower upgrades and selling
- Multiple difficulty levels
- Leaderboard system
- Sound effects and music
- More tower types and special abilities
- Interactive pause menu
- Skill tree system

## Troubleshooting

**Q: Script errors after opening the scene?**
A: Make sure TextMeshPro has been imported. Unity will prompt this on first use.

**Q: Enemies don't spawn?**
A: Check that the SpawnPoint object is created and tagged correctly. The GameBoot script handles this automatically.

**Q: Can't place towers?**
A: Make sure you're clicking on the green ground plane and the tower button is selected.

**Q: Game won't start?**
A: Ensure the GameBoot script is attached to a GameObject in the scene and the scene is saved.

---

Happy defending!
