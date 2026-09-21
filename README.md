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

## Imported map data: known unknowns

Creeps, towers, races and waves are imported from the extracted map files by the `Tools/` importers
(`WaveImporter`, `TowerImporter`, shared reader `Wc3Data`). Re-run them after changing any assumption below.
Per-asset gaps are also recorded in each asset's `importNotes` field (Inspector).

### Why there are gaps
`war3map.w3u` only stores the fields the map author *changed*. Everything else is inherited from the base unit
(e.g. `hgtw`, `ushd`), and that base data lives in Warcraft III's own game files, which this repo does not have.
When a field is missing, the importer uses the default below and notes it. Likely source to close these gaps
(names from memory, verify): the game's unit/ability tables (`UnitBalance.slk`, `UnitWeapons.slk`, `UnitData.slk`,
`AbilityData.slk`), or measuring a real game.

### Assumed values (counts are exact, from the map data)

| Field | Assumed when missing | Creeps missing (of 50) | Towers missing (of 76) |
|---|---|---|---|
| Move speed `umvs` | 300 WC3 units/s | 10 | - |
| Armor `udef` | 0 | 4 | - |
| Bounty base / dice / sides (`ubba`/`ubdi`/`ubsi`) | 0 / 1 / 1 | 2 / 6 / 4 | - |
| Damage dice `ua1d` | 1 | - | 53 |
| Damage sides `ua1s` | 1 | - | 13 |
| Damage base `ua1b` | 0 | - | 3 |
| Range `ua1r` | 700 WC3 units | - | 14 |
| Cooldown `ua1c` | 1.0 s | - | 5 |
| Projectile speed `ua1z` | 900 WC3 units/s | - | 22 |
| Gold cost `ugol` | 0 | - | 1 |

Formulas used: damage = base + dice x (sides + 1) / 2 (average roll, no randomness yet); bounty = base + dice x
(sides + 1) / 2; armor reduction = 0.06a / (1 + 0.06a) (WC3 formula); creeps have no defense/attack-type multipliers.

### Conversions (inferred, not verified in game)
- **1 Unity cell = 64 WC3 units.** Derived from the pathing map (384 cells of 32 units, halved into our 196-cell grid).
  Speeds, ranges and projectile speeds are divided by 64. If units feel too fast/slow or ranges too long, check this first.
- Attack style: a tower with projectile speed >= 5000 is treated as instant (laser style); everything else fires a
  visible projectile (15 towers are instant). Heuristic, not from the map.

### Not simulated yet (data is imported, behaviour is not)
- **Tower abilities** (30 of 76 towers). The map only holds stock ability ids; their numbers (slow %, poison damage,
  chain count, cleave radius, ...) are in the game files. Ids in use: `Abds` (20, purpose unknown), `ACct` (10),
  `Afrb` (6), `ACcl` (6, chain lightning), `ACsw` (3), and one each of `Aspi Aslo Apoi Alit Afra Afae Absk ACvp ACfl
  ACde ACcb ACbk`. Stored in `TowerData.abilityIds`; the meaning of most is a guess from memory.
- **Missile splash** (`ua1w = msplash`, 36 towers) with radii `ua1f/ua1h/ua1q`.
- **Second attacks** (`ua2*`, 10 towers).
- **Tower build time** (`ubld`, stored) and the constructor unit that builds towers.
- **Attack/defense types**: towers have attack types (chaos 18, siege 11, normal 14, pierce 8, magic 1, unset 24);
  creep defense types are set on only 11 of 50 creeps (small, normal, fort, and `divine` on level 33's Demon Sheep), the rest are unknown. The damage
  multiplier table is not applied.
- Creep and tower **models, icons and sounds** (`.mdl`/`.blp`); everything is a primitive shape.

### Open questions about the original behaviour
- **Cooldown 0**: 11 towers (the 1500-gold "ultimates") have `ua1c = 0`. We clamp to 0.1 s, which likely makes them far
  stronger than in the real game. Unknown how WC3 treats a 0 cooldown.
- **Divine armor**: level 33 (Demon Sheep, 50 creeps, 1000 HP, bounty 100 each) has defense type `divine`. If the real game
  applied WC3's divine multipliers, the attack type of the towers hitting it matters a lot. Not modelled.
- **Countdown at game start**: `Next Level` looks like it fires when all creeps are dead and also starts level 1 with a
  60 s countdown; whether it triggers at game start (and whether a level-0 bonus is paid) is unconfirmed.
- **Level bonus** starts at 10 and grows by 2 per level, paid on clear; the "last defender gets half" bonus is not
  implemented (single player).
- **Spawners and routes** are now generated from the map's triggers (`Tools > Import WC3 Routes`): 15 spawn regions, 6 unit groups, each group's chain of region-triggered move orders ending at the Load region; `yellow_right` spawns a fixed 1 creep. Two things are NOT from the map: (1) the map creates the gray player's creeps at `Left Move 2` but never gives them a group or an order, so they would idle; we route them straight to the exit; (2) creeps wait `WaveManager.initialOrderDelay` (3 s) at their spawn before the first order, while the map issues the first order immediately. Some levels also change lives (-10, -19, +20, +80 in the triggers); not implemented.
- **Kill bounty** is paid as the average roll; the real game rolls dice per kill.

## Performance benchmark
`Assets/Scripts/Editor/PerfBench.cs` runs the real scene headless in play mode and logs `BENCH ...` lines: path validation
and placement cost, frame time and GC allocation with a full wave (level 45 = 1800 creeps) and 80 towers, per-system
attribution, spawn hitch, terrain wall facing, and a behavior check (kills, projectiles, minimap, picking) over a fixed 5 s
of game time. Close Unity, then:

```
Unity -batchmode -nographics -projectPath . -executeMethod PerfBench.Run -logFile bench.log
```
Numbers are CPU/logic only (no rendering); GPU cost is not measured.
