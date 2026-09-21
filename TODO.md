# TODO


## Done
- [x] Delete `SceneSetup.cs`, `CheckSpawners.cs`, `TestPixels.cs` (Gemini debug leftovers)
- [x] Delete Unity template leftovers (`TutorialInfo/`, `Readme.asset`) and empty `Prefabs/` folders
- [x] Delete stale docs (ARCHITECTURE, PROJECT_SUMMARY, SETUP_GUIDE, CUSTOMIZATION); rewrite README
- [x] `GridManager`: named constants + single `TryGetIndex` helper instead of repeated `+128` / `256`
- [x] Remove `GameManager.GetGold()` duplicate (use `GetCurrentGold()`)

## Next (in order)
- [x] `Singleton<T>` base class for the 7 manager singletons
- [x] UI now lives in the scene (built once by `Tools > Build UI`, `Assets/Scripts/Editor/UIBuilder.cs`); fields wired via `SerializedObject`, reflection and `GameBoot.SetupUI` removed. Edit the Canvas in the scene directly from now on.
- [x] Towers and enemies are prefabs (`Assets/Prefabs`) driven by `TowerData` / `EnemyData` / `WaveSet` assets in `Assets/Data`. `Tools > Build Game Data` creates missing assets/prefabs and wires the managers. Also fixed double tower registration and the sell-refund mismatch.
- [ ] Projectiles are still built at runtime (`Tower.FireAtTarget` creates a sphere); make a Projectile prefab and pool them.
- [ ] Tower/enemy icons (`TowerData.icon`) and the command-card swap to Sell/Upgrade when a tower is selected (WC3 style).
- [x] `TowerManager`: shared `CanPlaceAt`, click-per-attempt (shift+drag still paints), ghost material works under URP
- [x] Wave/game flow: a wave is cleared only when spawning is finished AND no creeps are alive (fixes early next-wave / early win); no win after game over; `HashSet` for active creeps; `timeScale` reset on load; missing spawners/enemy now log an error and don't silently skip.
- [x] Wave rules match the real map: 60s before level 1 then 30s after each clear, all creeps of a level spawn at once at every spawner, +2 growing level bonus paid on clear, start gold 60.
- [ ] Import the real ~50 levels from `war3map.j` (`Set Levels`) into a `WaveSet`, and creep stats from `war3map.w3u`. Also: `yellow_right` spawns only 1 creep per level; show a "Level N in..." countdown in the HUD.
- [ ] **`Tower.Update`:** `Physics.OverlapSphere` + `GetComponent<Enemy>()` per tower per targeting tick. Fine now; consider a
      shared enemy registry and distance checks if creep counts grow.
- [ ] **`PathManager` grid size** hard-coded 200x200 (vs GridManager 256 / 196). Fold into the pathing rewrite: expose
      grid dimensions from `GridManager` and use them everywhere.
- [ ] `MapGenerator` is located via reflection in `GameBoot` (`mapTexture` private field) -> add a public setter or serialize in the scene.
- [ ] Scene tags ("Ground", "SpawnPoint") should be set up in the project.
- [ ] Review `mpq_files/` (raw map extract): keep, but note in README what is used vs. reference only.

## Kept on purpose
The `Tools/` menu importers in `Assets/Scripts/Editor` (`WPMImporter`, `W3RImporter`, `JassWaypointParser`,
`WintermaulSpawnerGenerator`, `ApplySpawnerCoords`) and `spawner_coords.txt` / `region_dump.txt`. They are hand-run tools that
turn the extracted WC3 map files into scene data. Re-evaluate once the spawners are final.
