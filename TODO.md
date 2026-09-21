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
- [ ] `TowerSelectionUI` still creates its tower buttons and tower objects at runtime; fold into the prefab / `TowerData` item below.
- [ ] **Towers and enemies as prefabs.** Enemies are assembled in `WaveManager.SpawnEnemy` (`new GameObject` + primitive cube);
      preview ghost is cloned and stripped of components. Make `Enemy` and `Tower` prefabs (visual, collider, ring);
      data (stats, levels, colors) in `ScriptableObject`s (`EnemyData`, `TowerData`) instead of switch statements in code.
- [x] `TowerManager`: shared `CanPlaceAt`, click-per-attempt (shift+drag still paints), ghost material works under URP
- [ ] **`WaveManager`:** waves hard-coded in `SetupDefaultWaves` (move to a `WaveData` asset); the no-spawner path silently
      skips the wave; per-frame `FindObjectsByType` fallback for spawners; use `Destroy`, not `DestroyImmediate`.
- [ ] **`GameManager` flow:** reset `Time.timeScale` on restart; enemy list uses O(n) `Remove` (use `HashSet`);
      `waveDelay` behaviour (next wave starts when field is empty) is a design decision worth revisiting.
- [ ] **`Tower.Update`:** `Physics.OverlapSphere` + `GetComponent<Enemy>()` per tower per targeting tick. Fine now; consider a
      shared enemy registry and distance checks if creep counts grow.
- [ ] **`PathManager` grid size** hard-coded 200x200 (vs GridManager 256 / 196). Fold into the pathing rewrite: expose
      grid dimensions from `GridManager` and use them everywhere.
- [ ] `MapGenerator` is located via reflection in `GameBoot` (`mapTexture` private field) -> add a public setter or serialize in the scene.
- [ ] Remove the empty `CreateTagIfNotExists` in `GameBoot`; scene tags ("Ground", "SpawnPoint") should be set up in the project.
- [ ] Review `mpq_files/` (raw map extract): keep, but note in README what is used vs. reference only.

## Kept on purpose
The `Tools/` menu importers in `Assets/Scripts/Editor` (`WPMImporter`, `W3RImporter`, `JassWaypointParser`,
`WintermaulSpawnerGenerator`, `ApplySpawnerCoords`) and `spawner_coords.txt` / `region_dump.txt`. They are hand-run tools that
turn the extracted WC3 map files into scene data. Re-evaluate once the spawners are final.
