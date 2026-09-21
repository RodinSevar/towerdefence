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
- [x] Projectiles are pooled (`Projectile.Spawn`); a real Projectile prefab is still optional polish.
- [x] Spawners and routes come from the map's triggers (15 spawners, 6 groups, region-triggered orders), creeps follow `RouteStep`s and turn at region entry; `yellow_right` = 1 creep; creeps need a granted path for every order (`EnemyManager.pathRequestsPerSecond`, 150/s FIFO; the real game starts almost at once but most creeps stall at first and pause at intervals; real scheduling is engine-decompile question 9). The map never orders gray's creeps (they idle in the original, a gap in its logic), so we route them straight down to the exit; `Prevent Attack` (a creep that attacks is redirected to the exit after 1 s) is not implemented.
- [x] Performance pass (measured with `PerfBench`, level 45 = 1800 creeps + 80 towers): placement 61 ms -> 4 ms (worst frame after placing 118 -> 4 ms), average frame 14.8 -> 1.2 ms, GC 216 -> 6 KB/frame, spawn hitch 47 ms -> 5 ms, creep material leak fixed. Path system rewritten (flat grid, greedy validation, budgeted flow-field refresh), `EnemyManager` (one tick loop + spatial grid; creeps have no colliders), minimap drawn into one texture.
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
- [ ] **`PathManager` grid size** hard-coded 200x200 (vs GridManager 256 / 196). Fold into the pathing rewrite: expose
      grid dimensions from `GridManager` and use them everywhere.
- [ ] Scene tags ("Ground", "SpawnPoint") should be set up in the project.
- [ ] Review `mpq_files/` (raw map extract): keep, but note in README what is used vs. reference only.

## Known visual problems (future)
- [x] Terrain walls were 58% back-face culled (5568 of 9578 wall triangles faced into the ground); `DrawWall` now picks the winding from which side is higher. Verified on the real mesh by `PerfBench` (all 9578 face outward).
- [x] **Terrain rebuilt from the real terrain data** (was derived from the pathing map). Known rough edges: some cliff edges show sawtooth wall fins, and 47 of 6251 wall triangles still face into the ground. `MapLayout.png` was made from the original's pathing/texture data (`WPMImporter`), so cliffs and ramps are blocky and noisy, with only 3 height levels. `mpq_files/war3map.w3e` holds the real terrain (per-corner ground height, cliff levels, ramp flags, tile textures); rebuild the mesh from that instead, and keep the pathing map only for buildability.
- [x] Procedural terrain textures generated (`tools/generate_terrain_textures.py` -> `Assets/Textures/Terrain`): seamless albedo for the map's 7 ground tiles (Ndrt, Glav, Nrck, Ngrs, Nice, Nsnw, Nsnr), 2 cliffs (CNdi, CNsn) and water, plus normal maps. Stand-ins for the tileset textures in the game files. `Glav` is unknown in the original (treated as frozen gravel). Not yet used by the terrain mesh.
- [ ] Terrain rebuild plan (from `war3map.w3e`; the pathing map is already faithful, verified): true heights and 5 cliff levels, real ramps (456 corners; the old importer wrongly turned unbuildable lane cells into ramps), water at its real level (bottom sea, side edges), ground blended by tile type using the textures above, creeps/towers following ground height, minimap generated from the terrain, ship at the exit.

## Kept on purpose
The `Tools/` menu importers in `Assets/Scripts/Editor` (`W3RImporter`, `WaveImporter`, `TowerImporter`, `RouteImporter`) and `region_dump.txt`. They are hand-run tools that turn the extracted WC3 map files into scene data. `RouteImporter` replaced the old guessed spawner generator and its patch-up tools.
- [x] Pathing grid is now 0.5 units (32 WC3 units, native); towers cover 4x4 cells and snap to whole units.
- [ ] Creeps overlap each other; the original has unit collision sizes (belongs with the pathing work).
- [ ] **Tower models (on hold).** Only the Crystal Castle race has kit-built models (Kenney Tower Defense Kit, `KitTowers.cs`); the rest use the low-poly guard tower. The look is a first pass and the assembly workflow needs rethinking before more races are done. Assets on hand are listed in `ASSETS.md` (full packs in `local_assets/`, git-ignored). Gaps found: totems, portals, ancient trees, glowing orbs/vortices (generate ourselves), humanoid heroes (KayKit Adventurers/Skeletons, Quaternius Monsters). 59 of 76 original towers name a model in `war3map.w3u` (25 buildings, 29 units, 5 effects; 17 use the stock model of their base building).
- [ ] Multiplayer (LAN, lockstep; 9 players, shared team lives, per-player gold that can be traded). Done: fixed-rate `Simulation` tick, `PlayerManager` (per-player gold/lumber/races), `GameCommand`s (place/sell/upgrade tower, unlock/select race, transfer gold) run through `CommandQueue`. To do: determinism audit (float math, unordered iteration, checksum per tick), network layer (host + direct IP, command exchange, desync check), per-player UI (owner colours on towers, trade-gold UI, player list), builder-unit start spots near each player's spawn, game setup screen (player count, join by IP), interpolation of creep/projectile movement between ticks, HUD countdown from `GameManager.WaveCountdown`.
