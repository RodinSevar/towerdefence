# TODO


## Done
- [x] Delete `SceneSetup.cs`, `CheckSpawners.cs`, `TestPixels.cs` (Gemini debug leftovers)
- [x] Delete Unity template leftovers (`TutorialInfo/`, `Readme.asset`) and empty `Prefabs/` folders
- [x] Delete stale docs (ARCHITECTURE, PROJECT_SUMMARY, SETUP_GUIDE, CUSTOMIZATION); rewrite README
- [x] `GridManager`: named constants + single `TryGetIndex` helper instead of repeated `+128` / `256`
- [x] Remove `GameManager.GetGold()` duplicate (use `GetCurrentGold()`)

## Next (in order)
- [ ] **ON HOLD: the imported-data unknowns** (see README, "Imported map data: known unknowns"). Base unit and ability data will be extracted from the Warcraft III game files later. Until then do not simulate tower abilities, splash, attack/defense types or tune the assumed defaults; when the game data is available, replace the assumptions in `WaveImporter`/`TowerImporter` and re-run them.
- [x] `Singleton<T>` base class for the 7 manager singletons
- [x] UI now lives in the scene (built once by `Tools > Build UI`, `Assets/Scripts/Editor/UIBuilder.cs`); fields wired via `SerializedObject`, reflection and `GameBoot.SetupUI` removed. Edit the Canvas in the scene directly from now on.
- [x] Towers and enemies are prefabs (`Assets/Prefabs`) driven by `TowerData` / `EnemyData` / `WaveSet` assets in `Assets/Data`. `Tools > Build Game Data` creates missing assets/prefabs and wires the managers. Also fixed double tower registration and the sell-refund mismatch.
- [ ] Projectiles are still built at runtime (`Tower.FireAtTarget` creates a sphere); make a Projectile prefab and pool them.
- [ ] Tower/enemy icons (`TowerData.icon`) and the command-card swap to Sell/Upgrade when a tower is selected (WC3 style).
- [x] `TowerManager`: shared `CanPlaceAt`, click-per-attempt (shift+drag still paints), ghost material works under URP
- [x] Wave/game flow: a wave is cleared only when spawning is finished AND no creeps are alive (fixes early next-wave / early win); no win after game over; `HashSet` for active creeps; `timeScale` reset on load; missing spawners/enemy now log an error and don't silently skip.
- [x] Wave rules match the real map: 60s before level 1 then 30s after each clear, all creeps of a level spawn at once at every spawner, +2 growing level bonus paid on clear, start gold 60.
- [x] Real 50 levels and creep stats imported from the map (`Tools > Import WC3 Waves and Creeps`, `WaveImporter.cs`): name, HP, speed (WC3 units / 64 = cells/s), armor (WC3 damage-reduction formula), average bounty. Fields the map leaves to the base unit are assumed and listed in each `EnemyData.importNotes` (speed 300, armor 0, bounty dice 1).
- [x] Real towers and races imported (`Tools > Import WC3 Towers and Races`, `TowerImporter.cs`): 11 races, 76 towers following the map's chain (wisp `eC00` -> race building -> constructor -> towers). Cost, sell value (point value), damage (base + dice), range (/64), cooldown, projectile speed, build time, description. Race selector (top-left "Race (F12)" button replaces "Log"), lumber (start 1, +1 before level 15), `RaceManager`. Assumptions per tower are listed in `TowerData.importNotes`.
- [ ] Constructor units: the original builds towers with a constructor unit trained by the race building (wisp -> building -> constructor). Currently the active race just fills the build menu.
- [ ] Tower behaviour not simulated yet (ids kept in `TowerData.abilityIds`): stock abilities (slow, poison, chain lightning, cleave, ...; their numbers live in the game's own data, not the map), missile splash (`ua1f/h/q`), second attacks (`ua2*`), tower build time, attack/defense types. Cooldown 0 towers (the 1500-gold "ultimates") are clamped to 0.1s, which makes them extremely strong; check against the real game.
- [ ] Tower icons: the map uses `.blp` icons; convert or draw placeholders and assign `TowerData.icon`.
- [ ] `yellow_right` spawns only 1 creep per level; show a "Level N in..." countdown in the HUD; some levels change lives (see `war3map.j`).
- [ ] **`Tower.Update`:** `Physics.OverlapSphere` + `GetComponent<Enemy>()` per tower per targeting tick. Fine now; consider a
      shared enemy registry and distance checks if creep counts grow.
- [ ] **`PathManager` grid size** hard-coded 200x200 (vs GridManager 256 / 196). Fold into the pathing rewrite: expose
      grid dimensions from `GridManager` and use them everywhere.
- [ ] `MapGenerator` is located via reflection in `GameBoot` (`mapTexture` private field) -> add a public setter or serialize in the scene.
- [ ] Scene tags ("Ground", "SpawnPoint") should be set up in the project.
- [ ] Review `mpq_files/` (raw map extract): keep, but note in README what is used vs. reference only.

## Known visual problems (future)
- [ ] **Terrain walls are sometimes transparent.** Likely cause (not yet verified): `MapGenerator` only generates a wall by comparing each cell with its left and lower neighbour, and `DrawWall` uses a fixed triangle winding (`flip` only distinguishes left vs down), so a wall between a high and a low cell faces the right way only when the *current* cell is the low one. When the current cell is the higher one the wall is back-face culled (invisible from the low side). Fix: choose the winding from which side is higher, or emit both sides / use a double-sided material. Also check ramp cells (blue) where corner heights differ.
- [ ] **Terrain is derived from the pathing map, not the real terrain.** `MapLayout.png` was made from the original's pathing/texture data (`WPMImporter`), so cliffs and ramps are blocky and noisy, with only 3 height levels. `mpq_files/war3map.w3e` holds the real terrain (per-corner ground height, cliff levels, ramp flags, tile textures); rebuild the mesh from that instead, and keep the pathing map only for buildability.

## Kept on purpose
The `Tools/` menu importers in `Assets/Scripts/Editor` (`WPMImporter`, `W3RImporter`, `JassWaypointParser`,
`WintermaulSpawnerGenerator`, `ApplySpawnerCoords`) and `spawner_coords.txt` / `region_dump.txt`. They are hand-run tools that
turn the extracted WC3 map files into scene data. Re-evaluate once the spawners are final.
