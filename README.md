# Wintermaul TD (Unity)

A work-in-progress Unity clone of the Warcraft III *Wintermaul* tower defense map.

## Running
1. Open the project in Unity (see `ProjectSettings/ProjectVersion.txt`).
2. Open `Assets/Scenes/SampleScene.unity` and press Play.

`GameBoot` (`Assets/Scripts/GameBoot.cs`) creates any managers, camera, terrain and UI that aren't already in the scene.

## Layout
- `Assets/Scripts/Managers` - game state (`GameManager`), waves, towers, grid, terrain (`MapGenerator`), minimap, pathing (`PathManager`)
- `Assets/Scripts/Enemies`, `Towers`, `Gameplay` - creeps, towers/projectiles, spawners with waypoints
- `Assets/Scripts/UI`, `Player`, `Camera` - HUD, selection, minimap, input
- `Assets/Scripts/Editor` - `Tools/` menu importers that turn the extracted WC3 map files in `mpq_files/` into scene data
- `Assets/Resources/MapLayout*.png` - terrain layout image (white = cliff, blue = ramp)

## Status
See `TODO.md` for the cleanup and improvement list.
