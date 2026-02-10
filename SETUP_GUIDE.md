# Quick Setup Guide

## Step-by-Step Setup

### 1. Open the Project
- Open Unity Hub
- Open the `wintermaul_unity` project

### 2. Initial Import
- Unity may ask to import TextMeshPro - click "Import TextMeshPro Essentials"

### 3. Create Tags (Required)
Before running the game, you need to create two tags:

1. Go to **Edit → Project Settings → Tags and Layers**
2. Under "Tags", click the "+" button to add a new tag
3. Create tag named: `Ground`
4. Click "+" again and create tag named: `SpawnPoint`
5. Close the Tags and Layers window

### 4. Setup the Scene
1. Open the scene at `Assets/Scenes/SampleScene.unity`
2. In the Hierarchy, right-click and create an empty GameObject
3. Name it "Boot"
4. With "Boot" selected, drag the script `Assets/Scripts/GameBoot.cs` onto it in the Inspector (or use Add Component)
5. Save the scene (Ctrl+S)

### 5. Run the Game
- Press the Play button (or Ctrl+Alt+P)
- The game will automatically initialize with the camera, ground, enemies, and UI

## The First Time Playing

1. **Click a Tower Button** on the right side (Gun Tower, Laser Tower, or Ice Tower)
2. **Click on the Green Ground** to place the tower
3. Press **ESC** to cancel tower placement
4. **Enemies spawn automatically** after a few seconds
5. **Towers automatically target** and fire at enemies

## Controls

- **Left Mouse Button**: Place a selected tower on the ground
- **ESC Key**: Cancel tower placement
- **Close the game**: Enemies stop spawning and the game ends when all waves are clear or you run out of lives

## Troubleshooting

**Issue: Enemies don't spawn**
- Make sure tags are created (Ground and SpawnPoint)
- Check that the Boot GameObject has the GameBoot script attached

**Issue: Can't place towers**
- Make sure you clicked a tower button (it should highlight)
- Try clicking on the green ground plane in the center
- You need enough gold (starts with 500)

**Issue: Script compilation errors**
- Make sure you're using Unity 2022 LTS or newer
- Try reimporting TextMeshPro assets

**Issue: No camera or can't see the game**
- The GameBoot script automatically creates a camera
- Try pressing Ctrl+Shift+F (Frame ALL) to see all objects in the scene

## Next Steps

After the game works:
1. Try modifying wave counts and difficulty in WaveManager.cs
2. Adjust tower costs and damage values in Tower.cs
3. Create your own enemy path in PathManager.cs
4. Add new tower types or enemy types

See README.md for more advanced customization options.
